using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rootborn.Core.Interaction;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Screens;
using Rootborn.Ui;

namespace Rootborn.Simulation
{
    /// <summary>한 판을 얼마나 돌릴지. 기본값은 CI에서 빨리 끝나게, 환경 변수로 늘린다(PlaySettings.FromEnvironment).</summary>
    public sealed class PlaySettings
    {
        /// <summary>몇 번째 날까지 플레이하는가(새 게임은 1일째). 그 날을 넘기면 끝.</summary>
        public int Days = 2;
        /// <summary>하루에 정책이 고르는 걸음 수. 넘으면 하루 마감 순서로 넘어간다.</summary>
        public int StepsPerDay = 120;
        /// <summary>하루 마감 순서로 넘어간 뒤 이 걸음 안에 다음 날이 와야 한다(아니면 "하루를 넘기지 못함").</summary>
        public int WindDownSteps = 80;
        /// <summary>한 판의 걸음 상한(정책이 날을 넘기지 않아도 끝나게).</summary>
        public int MaxSteps = 2000;
        /// <summary>한 걸음이 돌 수 있는 프레임 상한(걷기·문 포함).</summary>
        public int MaxFramesPerStep = 4000;
        /// <summary>할 수 있는 것이 하나도 없는 관찰이 이만큼 이어지면 막힘.</summary>
        public int EmptyObservations = 3;
        /// <summary>상태가 전혀 안 바뀐 걸음이 이만큼 이어지면 막힘(할 수 있는 것이 있어도).</summary>
        public int StallSteps = 400;
        /// <summary>같은 행동을 연달아 이만큼 했는데 보이는 것이 그대로면 그날 정책의 후보에서 뺀다(PlayMemory.BanAfter). 0이면 빼지 않는다.</summary>
        public int BanRepeatsWithoutChange = 3;
        /// <summary>같은 행동을 보이는 변화 없이 이만큼 되풀이하면 막힘(빼기가 꺼져 있거나 듣지 않을 때 잡는다).</summary>
        public int SameActionStall = 30;
        /// <summary>정책이 끝까지 가야 하는 체인(그 체인이 끝나면 멈춘다). 없으면 날 수로만 멈춘다.</summary>
        public string StopWhenChainCompleted;

        public static int EnvInt(string name, int fallback)
            => int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0 ? v : fallback;
    }

    /// <summary>한 걸음의 기록.</summary>
    public readonly struct PlayStep
    {
        public PlayStep(int index, int day, int minutes, string map, string top, string by, string key)
        {
            Index = index; Day = day; Minutes = minutes; Map = map; Top = top; By = by; Key = key;
        }

        public int Index { get; }
        public int Day { get; }
        public int Minutes { get; }
        public string Map { get; }
        public string Top { get; }
        /// <summary>누가 골랐나: 정책 이름, start(시작), day-end(하루 마감 순서), replay.</summary>
        public string By { get; }
        public string Key { get; }
    }

    /// <summary>불변식이 깨졌다. 그때의 화면 쌓임·맵·자리·최근 행동이 Report에 있다.</summary>
    public sealed class PlayFailure : Exception
    {
        public PlayFailure(string what, string report) : base(what + "\n" + report) { What = what; Report = report; }
        public string What { get; }
        public string Report { get; }
    }

    /// <summary>한 판의 결과: 행동 기록과 넓이(지나간 화면·상호작용·맵, 끝낸 퀘스트), 성능 측정.</summary>
    public sealed class PlayResult
    {
        public string Name, Policy;
        public int Seed;
        public readonly List<PlayStep> Steps = new List<PlayStep>();
        public readonly SortedSet<string> ScreensOpened = new SortedSet<string>(StringComparer.Ordinal);
        public readonly SortedSet<string> InteractionKinds = new SortedSet<string>(StringComparer.Ordinal);
        public readonly SortedSet<string> Maps = new SortedSet<string>(StringComparer.Ordinal);
        public readonly List<string> Notes = new List<string>();
        public int DaysCompleted, QuestsCompleted, ChainStepsCompleted, ChainsCompleted, Reloads;
        public long Frames, MaxFramesInStep;
        /// <summary>가만히 선 프레임 할당 최대(재지 않았으면 -1 — 실제 창 몸은 재지 않는다).</summary>
        public double MaxIdleFrameBytes = -1;
        public string MaxFramesStep;
        public string LastChainStep;
        public List<string> FinalState = new List<string>();
        public PlayFailure Failure;
        public string OutputDir;
        public IEnumerable<string> Keys => Steps.Select(s => s.Key);
    }

    /// <summary>
    /// 한 판을 돈다: 관찰 → (시작 순서 / 하루 마감 순서 / 정책)이 고름 → 몸이 입력으로 실행 → 불변식 검사. 날이 바뀔 때마다 저장된 세이브를
    /// 새 GameSession으로 다시 읽어 같은지 본다. 실패하면 씨앗·정책·행동 기록을 output/simulation/&lt;이름&gt;/에 남긴다(재생 도우미가 읽는다).
    /// </summary>
    public sealed class PlayRunner
    {
        private readonly IPlayBody _body;
        private readonly IPlayPolicy _policy;
        private readonly PlaySettings _settings;
        private readonly PlayAtlas _atlas;
        private readonly PlayObserver _observer;
        private readonly Random _random;
        private readonly PlayMemory _memory = new PlayMemory();
        private readonly PlayResult _result;
        private readonly HashSet<string> _claims = new HashSet<string>(StringComparer.Ordinal);
        private readonly bool _pace;

        private readonly ISavedGameSource _saved;

        /// <param name="saved">진행이 몸의 세이브 폴더가 아닌 곳(같이 하는 방)에 있으면 날마다 다시 읽기가 그것으로 읽는다.</param>
        public PlayRunner(IPlayBody body, IPlayPolicy policy, int seed, PlaySettings settings, PlayAtlas atlas, string name, bool pace = true, ISavedGameSource saved = null)
        {
            _saved = saved ?? body as ISavedGameSource;
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _settings = settings ?? new PlaySettings();
            _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
            _observer = new PlayObserver(atlas);
            _random = new Random(seed);
            _pace = pace;
            _result = new PlayResult { Name = name, Policy = policy.Name, Seed = seed };
            _memory.BanAfter = _settings.BanRepeatsWithoutChange;
            foreach (GameScreen open in _body.Screens.OpenScreens) _result.ScreensOpened.Add(open.Id); // 달리기 전부터 떠 있던 화면(슬롯 선택)
            _body.ScreenOpened += id => _result.ScreensOpened.Add(id);
            _body.Interacted += e => _result.InteractionKinds.Add(e.Kind);
        }

        public PlayResult Result => _result;

        /// <summary>한 걸음을 한 뒤(불변식 검사 전). 실제 창 층이 스크린샷을 찍는 데 쓴다.</summary>
        public event Action<int, PlayAction> AfterStep;

        /// <summary>고른 행동을 몸이 하기 바로 앞(걸음 번호, 그 걸음의 관찰, 행동, 고른 쪽). 속도 기록이 걸음 앞 상태를 읽는 데 쓴다.</summary>
        public event Action<int, PlayView, PlayAction, string> BeforeAct;

        /// <summary>끝까지 돈다. 불변식이 깨지면 PlayFailure를 Result.Failure에 두고 돌려준다(던지지 않는다).</summary>
        public PlayResult Run()
        {
            while (Step()) { }
            return Finish();
        }

        private int _nextStep;
        private bool _done;

        /// <summary>
        /// 한 걸음만 돈다(같이 하는 방은 여러 사람의 달리는 쪽이 번갈아 한 걸음씩 — RoomPlay). 끝났거나 불변식이 깨졌으면 false(실패는 Result.Failure).
        /// </summary>
        public bool Step()
        {
            if (_done) return false;
            try
            {
                if (_nextStep >= _settings.MaxSteps || !StepOnce(_nextStep)) _done = true;
                else _nextStep++;
            }
            catch (PlayFailure f) { _result.Failure = f; _done = true; }
            catch (PlayReplayMismatch m) { _result.Failure = new PlayFailure("재생이 어긋났다", m.Message + "\n" + Situation()); _done = true; }
            return !_done;
        }

        /// <summary>끝났다(날 수를 넘겼거나, 걸음 상한, 실패).</summary>
        public bool Done => _done;

        /// <summary>바깥(같이 하는 방의 불변식)이 찾은 실패를 이 판의 실패로 남기고 멈춘다.</summary>
        public void FailFromOutside(string what, string report)
        {
            if (_result.Failure == null) _result.Failure = new PlayFailure(what, report + "\n" + Situation());
            _done = true;
        }

        /// <summary>끝 상태와 넓이를 셈해 결과를 준다.</summary>
        public PlayResult Finish()
        {
            if (_body.Session != null) _result.FinalState = StateSnapshot.Of(_body.Session);
            _result.Frames = _body.Frames;
            CountProgress();
            return _result;
        }

        /// <summary>이만큼 걸음 동안 상태(화면·자리·시각·활동 기록)가 전혀 바뀌지 않으면 할 것이 없는 것으로 본다(사람이면 자러 간다).</summary>
        public const int NothingWorksSteps = 40;

        private bool NothingWorks => _stall >= NothingWorksSteps;

        /// <summary>밤샘하는 플레이어가 서 있는 한 번의 프레임 수(10초 — 게임 24분).</summary>
        public const int IdleFrames = 600;

        private int _day = -1, _dayStartStep, _windDownFrom = -1, _empty, _stall;
        private string _lastFingerprint;

        /// <summary>한 걸음. 끝이면 false.</summary>
        private bool StepOnce(int step)
        {
            {
                PlayView view = _observer.Observe(_body);
                CheckView(view, step);
                _memory.See(view);
                if (_memory.RepeatsWithoutChange >= _settings.SameActionStall)
                    Fail("막힘: 같은 행동을 보이는 변화 없이 " + _memory.RepeatsWithoutChange + "번 되풀이했다(" + (_result.Steps.Count > 0 ? _result.Steps[_result.Steps.Count - 1].Key : "?") + ")", view);
                if (view.Day != _day && _body.Session != null)
                {
                    _result.Notes.Add(view.Day + "일째 시작(걸음 " + step + ", " + view.Minutes + "분)");
                    if (_day > 0) DayPassed(view);
                    _day = view.Day; _dayStartStep = step; _windDownFrom = -1;
                }
                if (Finished(view)) return false;
                view.StepsToday = step - _dayStartStep;

                // 같이 하는 방에서 자는 중(아직 깨어 있는 사람이 있다): 남이 잘 때까지 또는 07:00까지 기다린다. 하루 마감 순서의 걸음 수에 넣지 않는다.
                if (_body.Session != null && _body.Session.IsSleepingInRoom && view.InWorld && !(_policy is ReplayPolicy))
                {
                    if (_windDownFrom >= 0) _windDownFrom = step;
                    _empty = 0;
                    PlayAction hold = view.Hold(IdleFrames);
                    _result.Steps.Add(new PlayStep(step, view.Day, view.Minutes, view.MapId, view.TopScreen, "room-wait", hold.Key));
                    TimeBefore waited = TimeBefore.Of(_body.Session, _body.Frames);
                    BeforeAct?.Invoke(step, view, hold, "room-wait");
                    Do(hold, view);
                    AfterStep?.Invoke(step, hold);
                    CheckAfter(view, step);
                    CheckTime(waited, hold, view);
                    return true;
                }

                if (view.Actions.Count == 0)
                {
                    if (++_empty >= _settings.EmptyObservations) Fail("막힘: 할 수 있는 것이 없다", view);
                    _body.Wait(30);
                    return true;
                }
                _empty = 0;

                if (_policy is ReplayPolicy replay && replay.Finished) return false;
                (PlayAction action, string by) = Decide(view, step);
                if (action == null) Fail("막힘: 고를 것이 없다(" + _policy.Name + ")", view);
                _memory.Note(view, action);
                _result.Steps.Add(new PlayStep(step, view.Day, view.Minutes, view.MapId, view.TopScreen, by, action.Key));
                if (view.InWorld) _result.Maps.Add(view.MapId);
                TimeBefore before = TimeBefore.Of(_body.Session, _body.Frames);
                BeforeAct?.Invoke(step, view, action, by);
                Do(action, view);
                AfterStep?.Invoke(step, action);
                CheckAfter(view, step);
                CheckTime(before, action, view);
            }
            return true;
        }

        private bool Finished(PlayView view)
        {
            if (_body.Session == null) return false;
            if (view.Day > _settings.Days) return true;
            if (_settings.StopWhenChainCompleted != null)
            {
                var chain = _body.Session.Chain(_settings.StopWhenChainCompleted);
                if (_body.Session.QuestChains.GetState(chain) == Rootborn.Core.Quests.QuestChainState.Completed && view.TopScreen == null) return true;
            }
            return false;
        }

        private (PlayAction, string) Decide(PlayView view, int step)
        {
            if (_policy is ReplayPolicy) return (_policy.Choose(view, _memory, _random), _policy.Name); // 재생은 기록 그대로(뺀 행동 없음)
            PlayAction a = StartRoutine.Choose(view, _memory);
            if (a != null) return (a, "start");
            if (_pace && view.InWorld && _policy is IStaysUp)
            {
                // 밤샘하는 플레이어: 자러 가지 않는다. 오늘 걸음을 다 썼거나 할 것이 없으면 날이 넘어갈 때까지 서 있는다(떠 있는 화면은 닫는다).
                if (view.StepsToday >= _settings.StepsPerDay || NothingWorks)
                {
                    a = view.TopScreen != null || view.MovementBlocked ? DayEndRoutine.Close(view) : view.Idle(IdleFrames);
                    if (a != null) return (a, "stay-up");
                }
                return (PolicyChoose(view), _policy.Name);
            }
            // 하루 마감(잠): 오늘 걸음을 다 썼거나, HUD가 잘 시간이 지났다·지쳤다고 보이거나, 한동안 해 봐도 아무것도 바뀌지 않으면(할 것이 없다)
            // 집으로 걸어가 잔다(흐르는 시간 5단계).
            if (_pace && view.InWorld && (_windDownFrom >= 0 || view.StepsToday >= _settings.StepsPerDay || view.TimeToSleep || NothingWorks)) // 한 번 시작하면 그날은 끝까지
            {
                if (_windDownFrom < 0) _result.Notes.Add(view.Day + "일째 " + view.Minutes + "분: 하루 마감 순서(" + (view.TimeToSleep ? "HUD: " + view.Alert
                    : NothingWorks ? "할 것이 없다 — " + NothingWorksSteps + "걸음 동안 바뀐 것 없음" : "걸음 " + view.StepsToday) + ")");
                if (_windDownFrom < 0) _windDownFrom = step;
                bool playing = view.OpenScreens.Contains(ScreenCatalog.Minigame) && step - _windDownFrom < _settings.WindDownSteps / 2;
                if (playing) return (PolicyChoose(view), _policy.Name); // 하던 미니게임은 마저 한다(닫으면 포기로 정산된다)
                if (step - _windDownFrom > _settings.WindDownSteps) Fail("하루를 넘기지 못함: 하루 마감 순서로 " + _settings.WindDownSteps + "걸음 안에 다음 날이 오지 않았다", view);
                a = DayEndRoutine.Choose(view, _atlas);
                if (a != null) return (a, "day-end");
            }
            return (PolicyChoose(view), _policy.Name);
        }

        /// <summary>정책이 고른다. 오늘 보이는 변화 없이 되풀이해 뺀 행동은 후보에서 지운다(PlayMemory.RemoveBanned).</summary>
        private PlayAction PolicyChoose(PlayView view)
        {
            _memory.RemoveBanned(view);
            return _policy.Choose(view, _memory, _random);
        }

        private void Do(PlayAction action, PlayView view)
        {
            long before = _body.Frames;
            try
            {
                switch (action.Kind)
                {
                    case PlayActionKind.Click: _body.Click(action.X, action.Y); break;
                    case PlayActionKind.Key: _body.PressKey(action.ScreenKey); break;
                    case PlayActionKind.Type: _body.TypeInto(action.X, action.Y, action.Text); break;
                    case PlayActionKind.Wait: _body.Wait(action.Frames); break;
                    case PlayActionKind.WalkInteract:
                        float cell = _body.Walk.Map.Grid.CellSize;
                        if (_body.WalkNear(action.X, action.Y, cell * 1.25f))
                        {
                            var target = _body.Target;
                            if (target == null || target.Kind != action.InteractionKind || target.Id != action.Subject)
                                _result.Notes.Add("다가간 자리의 E 대상이 다르다: " + action.Key + " → " + (target == null ? "(없음)" : target.Kind + "/" + target.Id) + " (" + view.MapId + ")");
                            _body.Interact();
                        }
                        else _result.Notes.Add("걸어서 닿지 못함: " + action.Key + " (" + view.MapId + ")");
                        break;
                    case PlayActionKind.Door:
                        var door = _body.Walk.Map.Doors.FirstOrDefault(d => d.Target == action.Subject && d.Destination.HasValue);
                        if (door == null || !_body.WalkThroughDoor(door)) _result.Notes.Add("문을 지나지 못함: " + action.Key + " (" + view.MapId + ")");
                        break;
                }
            }
            catch (PlayFailure) { throw; }
            catch (Exception e)
            {
                Fail("예외: " + action.Key + " → " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace, view);
            }
            long frames = _body.Frames - before;
            if (frames > _result.MaxFramesInStep) { _result.MaxFramesInStep = frames; _result.MaxFramesStep = action.Key; }
            if (frames > _settings.MaxFramesPerStep) Fail("걸음이 너무 길다: " + action.Key + " " + frames + "프레임(한계 " + _settings.MaxFramesPerStep + ")", view);
        }

        // ---------- 불변식 ----------

        private void CheckView(PlayView view, int step)
        {
            ScreenStack stack = _body.Screens;
            int panels = 0;
            bool modal = false;
            foreach (GameScreen s in stack.OpenScreens)
            {
                if (s.Layer == ScreenLayer.Panel) panels++;
                if (s.Layer == ScreenLayer.Modal) modal = true;
            }
            if (panels > 1) Fail("화면 쌓임 규칙: 큰 패널이 " + panels + "개 열려 있다", view);
            if (modal)
            {
                foreach (PlayAction a in view.Actions)
                {
                    if (a.Screen == null) continue;
                    GameScreen owner = stack.Find(a.Screen);
                    if (owner != null && owner.Layer < ScreenLayer.Modal) Fail("화면 쌓임 규칙: 모달이 떠 있는데 아래층 " + a.Screen + "의 " + a.Key + "가 포인터를 받는다", view);
                }
            }
            GameSession s2 = _body.Session;
            if (s2 != null)
            {
                var st = s2.Student;
                if (st.Energy < 0 || st.Focus < 0 || st.Stress < 0 || st.TimeMinutes < 0)
                    Fail("수치 범위: 체력 " + st.Energy + ", 집중 " + st.Focus + ", 스트레스 " + st.Stress + ", 분 " + st.TimeMinutes, view);
                foreach (var slot in s2.Inventory.Slots)
                    if (slot.Item != null && slot.Count <= 0) Fail("수치 범위: 가방 " + slot.Item.Id + " " + slot.Count + "개", view);
                CheckClaims(s2, view);
            }
            string[] unknown = UnknownMissingKeys();
            if (unknown.Length > 0) Fail("빠진 문구 키가 새로 생겼다: " + string.Join(", ", unknown), view);
            if (_body.CollisionViolations > 0) Fail("벽을 뚫고 걸었다: " + _body.CollisionViolations + "프레임", view);
        }

        // ---------- 흐르는 시간의 불변식 ----------

        /// <summary>행동 앞의 시간 상태: 날, 그날 0시부터 센 분, 지쳤는지, 활동 기록 수, 체력.</summary>
        private readonly struct TimeBefore
        {
            public readonly bool Has;
            public readonly int Day, Minutes, Focus, Energy, BusyUntil;
            public readonly long Frames;
            public readonly bool Exhausted;

            private TimeBefore(GameSession s, long frames)
            {
                Has = true; Day = s.StudentDay; Minutes = s.StudentTimeMinutes; Exhausted = s.IsExhausted;
                Focus = s.Student.Focus; Energy = s.Student.Energy; Frames = frames; BusyUntil = s.ActivityEndsAtMinutes;
            }

            public static TimeBefore Of(GameSession s, long frames) => s == null ? default : new TimeBefore(s, frames);
        }

        /// <summary>
        /// 시계가 거꾸로 가지 않는다, 지친 동안 활동이 시작되지 않는다, 자면 다음 날 07:00(넘긴 활동 시간만큼 늦게), 자지 않고 넘긴 날은 회복이 없다,
        /// 메뉴가 열려 있으면 혼자 하기의 시계가 멈춘다(창 없는 몸이 프레임마다 센다).
        /// </summary>
        private void CheckTime(TimeBefore before, PlayAction action, PlayView view)
        {
            GameSession s = _body.Session;
            if (s == null || !before.Has) return;
            int day = s.StudentDay, minutes = s.StudentTimeMinutes;
            if (day < before.Day || day == before.Day && minutes < before.Minutes)
                Fail("시계가 거꾸로 갔다: " + before.Day + "일 " + before.Minutes + "분 → " + day + "일 " + minutes + "분(" + action.Key + ")", view);
            // 지친 동안 활동이 시작되지 않는다: 집중을 쓰거나, 흐른 프레임보다 시계가 더 건너뛰었으면(혼자 하기의 활동은 걸리는 분만큼 건너뛴다) 활동을 한 것이다.
            // 장소 첫 방문·처음 만남은 활동 기록에 남지만 비용이 없어 활동이 아니다.
            // 지치기 전에 시작한 미니게임 활동을 끝내면 그 활동의 끝나는 시각(ActivityEndsAtMinutes)까지는 건너뛸 수 있다(흐르는 시간 3.2절).
            if (before.Exhausted && day == before.Day && minutes > before.BusyUntil)
            {
                long frames = _body.Frames - before.Frames;
                int flowed = s.TimeRules == null ? 0 : (int)Math.Ceiling(frames * _body.ClockSecondsPerFrame * Rootborn.Core.Time.TimeRules.MinutesPerDay / s.TimeRules.RealSecondsPerGameDay) + 1;
                if (!s.IsSoloRoom) flowed += RoomClockSlackMinutes; // 같이 하는 방: 받은 방의 시각으로 맞출 때 조금 앞으로 뛸 수 있다
                if (s.Student.Focus < before.Focus || minutes - before.Minutes > flowed)
                    Fail("지친 동안 활동이 시작됐다(" + action.Key + "): 집중 " + before.Focus + " → " + s.Student.Focus + ", 시각 +" + (minutes - before.Minutes) + "분(흐른 것 " + flowed + "분)", view);
            }
            if (day > before.Day && s.Student.DayState == Rootborn.Core.StudentLife.StudentDayState.InProgress && s.TimeRules != null)
            {
                bool slept = s.Student.LastNight.HasSlept;
                int start = s.TimeRules.DayStartMinutes;
                // 잤으면 다음 날 단추를 누른 걸음 끝에 본다. 안 잤으면 걷는 도중에 넘어갈 수 있어 늦은 쪽은 보지 않는다.
                // 같이 하는 방은 시계가 흐르는 채로 다음 날 단추를 누른 때의 방 시각이다(끊겼다 늦게 들어오면 늦다 — 방의 불변식이 방의 시각과 같은지 본다).
                if (minutes < start || slept && s.IsSoloRoom && minutes > start + MaxMinutesAfterWaking)
                    Fail("새 날의 시각: " + minutes + "분(하루 시작 " + start + "분, " + (slept ? "잤다" : "안 잤다") + ")", view);
                if (!slept && s.Student.Energy > before.Energy)
                    Fail("자지 않고 넘긴 날에 체력이 돌아왔다: " + before.Energy + " → " + s.Student.Energy, view);
                if (!slept) _result.Notes.Add(day + "일째: 자지 않고 넘어왔다(체력 " + s.Student.Energy + (s.IsExhausted ? ", 지침" : string.Empty) + ")");
            }
            if (_body is IClockWatch watch && watch.ClockViolations > 0)
                Fail("메뉴가 열린 혼자 하기에서 시계가 흘렀다: " + watch.ClockViolations + "프레임", view);
        }

        /// <summary>새 날을 처음 본 시각이 하루 시작보다 늦어도 되는 분(넘긴 활동 시간 + 다음 날 단추를 누른 뒤 몇 프레임).</summary>
        private const int MaxMinutesAfterWaking = 90;

        /// <summary>같이 하는 방에서 개인 시계가 방의 시각을 받아 앞으로 맞추는 폭(Net RemoteRoomTime.ResyncMinutes와 같다 — 방의 불변식이 따로 본다).</summary>
        public const int RoomClockSlackMinutes = 5;

        private void CheckClaims(GameSession s, PlayView view)
        {
            List<string> claims = StateSnapshot.Claims(s);
            foreach (string twice in StateSnapshot.Duplicates(claims)) Fail("보상을 두 번 받았다: " + twice, view);
            var seen = new HashSet<string>(claims, StringComparer.Ordinal);
            foreach (string old in _claims) if (!seen.Contains(old)) Fail("받은 보상 기록이 사라졌다: " + old, view);
            foreach (string c in seen) _claims.Add(c);
        }

        private string[] UnknownMissingKeys()
        {
            var known = new HashSet<string>(Rootborn.Screens.Tests.KnownMissingKeys.All, StringComparer.Ordinal);
            return _body.Text.MissingKeys.Select(k => k.Substring(k.IndexOf(':') + 1)).Where(k => !known.Contains(k)).Distinct().ToArray();
        }

        private void CheckAfter(PlayView before, int step)
        {
            string fp = Fingerprint();
            if (fp == _lastFingerprint)
            {
                if (++_stall >= _settings.StallSteps) Fail("막힘: " + _settings.StallSteps + "걸음 동안 상태가 전혀 바뀌지 않았다", before);
            }
            else _stall = 0;
            _lastFingerprint = fp;
        }

        private string Fingerprint()
        {
            var sb = new StringBuilder(128);
            foreach (GameScreen s in _body.Screens.OpenScreens) sb.Append(s.Id).Append('|');
            if (_body.InWorld) sb.Append(_body.Walk.Map.Id).Append('@').Append((int)_body.Walk.Walker.X).Append(',').Append((int)_body.Walk.Walker.Y);
            GameSession s2 = _body.Session;
            if (s2 != null) sb.Append('#').Append(s2.StudentDay).Append(':').Append(s2.StudentTimeMinutes).Append(':').Append(s2.Student.GetActivityLogIds().Length);
            return sb.ToString();
        }

        /// <summary>날이 바뀌었다: 저장된 세이브를 새 GameSession으로 다시 읽어 지금 판과 같은지 본다.</summary>
        private void DayPassed(PlayView view)
        {
            _result.DaysCompleted++;
            ReloadCheck(view);
            if (_body is IFrameCost cost && cost.MeasuresFrameCost && view.TopScreen == null)
            {
                double bytes = cost.AllocatedBytesPerIdleFrame(60);
                if (bytes > _result.MaxIdleFrameBytes) _result.MaxIdleFrameBytes = bytes;
                if (bytes > PlayLimits.IdleFrameBytes) Fail("프레임 비용: 가만히 선 프레임 한 번에 " + bytes.ToString("0") + "바이트를 새로 잡는다(한계 " + PlayLimits.IdleFrameBytes + ")", view);
            }
        }

        public void ReloadCheck(PlayView view)
        {
            GameSession live = _body.Session;
            if (live == null) return;
            GameSession reloaded;
            bool skipHouse = false;
            if (_saved != null)
            {
                // 같이 하는 방: 방이 가진 그 사람의 세이브(+ 아직 방이 받았다고 답하지 않은 저장)로 시작한다 — 다시 들어오면 보일 진행.
                reloaded = _saved.ReloadSaved();
                if (reloaded == null) return;
                skipHouse = live.SharesHouse; // 같이 쓰는 집은 방의 것이다(방의 불변식이 따로 본다)
            }
            else reloaded = GameSession.Start(_atlas.Definitions, GameStartSettings.FromDefinitions(_atlas.Definitions),
                new FileSaveStore(_body.SaveRoot), _body.SlotId, null);
            if (_body.HouseEntered.HasValue && !skipHouse) reloaded.EnterHouse(_body.HouseEntered.Value.Grid, _body.HouseEntered.Value.Surface);
            // 집에 들어가지 않았어도 게임은 판을 이을 때 집 배치를 읽어 둔다(HouseFurnitureBlocking.Attach → GameSession.PrepareHouse) — 다시 읽은 판도 같은 길로 연다.
            // 안 열면 다시 읽은 판에는 가구 줄이 하나도 없어, 세이브에 든 배치(새 게임의 처음 배치 포함)를 "사라졌다"로 잘못 본다.
            else if (live.House != null && !skipHouse) ReloadHouses().Attach(reloaded, _body.Walk?.Map.Id);
            List<string> liveLines = StateSnapshot.Of(live), reloadedLines = StateSnapshot.Of(reloaded);
            if (skipHouse) { liveLines.RemoveAll(StateSnapshot.IsFurniture); reloadedLines.RemoveAll(StateSnapshot.IsFurniture); }
            List<string> diff = StateSnapshot.Diff(liveLines, reloadedLines);
            _result.Reloads++;
            if (diff.Count > 0) Fail("세이브 어긋남: 저장 뒤 새 GameSession으로 다시 읽은 상태가 다르다\n" + string.Join("\n", diff), view);
        }

        private Rootborn.Screens.Furniture.HouseFurnitureBlocking _reloadHouses;

        // 다시 읽은 판에 집 배치를 열어 줄 것(몸의 월드와 따로 — 몸의 걷는 격자를 건드리지 않는다). 처음 쓸 때 한 번 만든다.
        private Rootborn.Screens.Furniture.HouseFurnitureBlocking ReloadHouses()
            => _reloadHouses ??= new Rootborn.Screens.Furniture.HouseFurnitureBlocking(new Headless.HeadlessRooms(_atlas.Maps, _atlas.Interactions).RoomOf,
                _atlas.Interactions.PlacementMaps(_atlas.Maps.Catalog), _ => { });

        private void CountProgress()
        {
            GameSession s = _body.Session;
            if (s == null) return;
            foreach (var q in s.QuestLog.ToSaveData().Quests)
                if (q.State == Rootborn.Core.Quests.QuestStage.Completed || q.State == Rootborn.Core.Quests.QuestStage.RewardClaimed) _result.QuestsCompleted++;
            foreach (var c in s.QuestChains.ToSaveData().Chains)
            {
                _result.ChainStepsCompleted += c.CompletedStepIds.Length;
                if (c.ChainState == Rootborn.Core.Quests.QuestChainState.Completed.ToString()) _result.ChainsCompleted++;
                bool interesting = c.IsTracked || c.ChainState == Rootborn.Core.Quests.QuestChainState.Completed.ToString();
                if (interesting || _result.LastChainStep == null) _result.LastChainStep = c.ChainId + " / " + (c.CurrentStepId ?? "(끝)") + " / " + c.ChainState;
            }
        }

        private void Fail(string what, PlayView view) => throw new PlayFailure(what, Situation(view));

        private string Situation(PlayView view = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("정책 " + _policy.Name + ", 씨앗 " + _result.Seed + ", 걸음 " + _result.Steps.Count + ", 프레임 " + _body.Frames);
            sb.AppendLine("화면 쌓임(아래→위): " + string.Join(" > ", _body.Screens.OpenScreens.Select(s => s.Id + "[" + s.Layer + (s.BlocksMovement ? ",막음" : "") + "]")));
            if (_body.InWorld)
                sb.AppendLine("맵 " + _body.Walk.Map.Id + ", 자리 " + _body.Walk.Walker.X.ToString("0.0", CultureInfo.InvariantCulture) + "," + _body.Walk.Walker.Y.ToString("0.0", CultureInfo.InvariantCulture)
                    + ", E 대상 " + (_body.Target == null ? "(없음)" : _body.Target.Kind + "/" + _body.Target.Id));
            if (_body.Session != null) sb.AppendLine(_body.Session.StudentDay + "일째 " + _body.Session.StudentTimeMinutes + "분, 체력 " + _body.Session.Student.Energy
                + ", 집중 " + _body.Session.Student.Focus + (_body.Session.IsExhausted ? ", 지침" : string.Empty));
            if (view != null) sb.AppendLine("할 수 있던 것(" + view.Actions.Count + "): " + string.Join(", ", view.Actions.Take(40).Select(a => a.Key)));
            sb.AppendLine("최근 행동:");
            foreach (PlayStep s in _result.Steps.Skip(Math.Max(0, _result.Steps.Count - 20)))
                sb.AppendLine("  " + s.Index + " [" + s.By + "] " + s.Day + "일 " + s.Minutes + "분 " + s.Map + " " + (s.Top ?? "-") + " → " + s.Key);
            return sb.ToString();
        }
    }
}
