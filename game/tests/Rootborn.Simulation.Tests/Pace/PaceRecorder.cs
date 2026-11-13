using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using Rootborn.Core.Interaction;
using Rootborn.Core.Minigames;
using Rootborn.Game;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Minigames;
using Rootborn.Simulation.Headless;
using Rootborn.Ui;

namespace Rootborn.Simulation.Pace
{
    /// <summary>
    /// 자동 플레이어의 걸음마다 속도를 적는다: 걸음 앞(BeforeAct)에 분류·새로 나온 글자·고르기·미니게임 도전을, 걸음 뒤(AfterStep)에 돈 프레임·
    /// 학생 시계가 간 분·새로운 것을. 프레임 루프 안에서는 아무것도 하지 않는다(걸음 단위로만 적는다).
    /// 걸음 사이에 돈 프레임(할 수 있는 것이 없는 관찰의 기다리기, 날마다의 프레임 비용 측정)은 "관찰 사이" 걸음으로 할 것 없음에 넣는다.
    /// </summary>
    public sealed class PaceRecorder
    {
        public const string BetweenKey = "(관찰 사이 프레임)";

        private readonly IPlayBody _body;
        private readonly PlayAtlas _atlas;
        private readonly PaceClassifier _classifier;
        private readonly List<IPaceNoveltyKind> _kinds;
        private readonly NewTextCounter _text = new NewTextCounter();
        private readonly List<PaceStep> _steps = new List<PaceStep>(1024);
        private readonly List<InteractionEvent> _interactions = new List<InteractionEvent>(4);
        private readonly DrawList _draw = new DrawList();
        private readonly TestTextMeasurer _measure = new TestTextMeasurer();
        private readonly List<string> _lines = new List<string>(64);
        private readonly PaceProbe _probe = new PaceProbe();

        private PaceStep _pending;
        private long _lastFrames, _startFrames;
        private int _startDay, _startMinutes;
        private bool _hadSession;
        private ChallengeSession _challenge;
        private int _challengeIndex = -1;
        private double _limitPending;

        public PaceRecorder(IPlayBody body, PlayRunner runner, PlayAtlas atlas, ScreenRegistry registry = null)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
            _classifier = new PaceClassifier(registry ?? ScreenCatalog.Create(), DayResultScreen.InteractionKind);
            _kinds = new PaceNoveltyCatalog().Create();
            FramesBeforeFirstStep = _lastFrames = body.Frames;
            body.Interacted += e => _interactions.Add(e);
            runner.BeforeAct += Before;
            runner.AfterStep += (step, action) => After();
        }

        public IReadOnlyList<PaceStep> Steps => _steps;
        public long FramesBeforeFirstStep { get; }

        private void Before(int step, PlayView view, PlayAction action, string by)
        {
            long now = _body.Frames;
            if (now > _lastFrames) AddBetween(now - _lastFrames, view);
            GameSession session = _body.Session;
            string top = view.TopScreen;
            string category = _classifier.Classify(top, action, session != null && session.IsBusy);
            _pending = new PaceStep
            {
                Index = _steps.Count,
                Day = view.Day,
                Minutes = view.Minutes,
                Category = category,
                Key = action.Key,
                By = by ?? string.Empty,
                Map = view.MapId ?? string.Empty,
                Top = top,
                InteractionKind = action.Kind == PlayActionKind.WalkInteract || action.Kind == PlayActionKind.Door ? action.InteractionKind : null,
                Subject = action.Kind == PlayActionKind.WalkInteract || action.Kind == PlayActionKind.Door ? action.Subject : null,
                NewChars = _text.Count(VisibleText()),
                Choices = _classifier.Choices(top, action, category),
            };
            ReadChallenge(category);
            _startFrames = now;
            _hadSession = session != null;
            _startDay = session?.StudentDay ?? view.Day;
            _startMinutes = session?.StudentTimeMinutes ?? view.Minutes;
        }

        private void After()
        {
            if (_pending == null) return;
            GameSession session = _body.Session;
            _pending.Frames = _body.Frames - _startFrames;
            if (session != null && _hadSession) _pending.GameMinutes = (session.StudentDay - _startDay) * Rootborn.Core.Time.TimeRules.MinutesPerDay + session.StudentTimeMinutes - _startMinutes;
            Probe(_pending);
            _steps.Add(_pending);
            _pending = null;
            _lastFrames = _body.Frames;
        }

        /// <summary>판이 끝난 뒤: 마지막 걸음 뒤에 돈 프레임(끝을 알아차리기 전의 관찰 — 날마다의 프레임 비용 측정 등)을 마지막 걸음에 넣는다.</summary>
        public void Finish()
        {
            long now = _body.Frames;
            if (now <= _lastFrames || _steps.Count == 0) return;
            _steps[_steps.Count - 1].Frames += now - _lastFrames; // 끝을 알아차린 관찰의 프레임은 마지막 걸음에 붙인다(새 날의 빈 줄을 만들지 않게)
            _lastFrames = now;
        }

        private void AddBetween(long frames, PlayView view)
        {
            var s = new PaceStep
            {
                Index = _steps.Count, Day = view.Day, Minutes = view.Minutes, Category = PaceCategories.NothingToDo, Key = BetweenKey, By = "runner",
                Map = view.MapId ?? string.Empty, Top = view.TopScreen, Frames = frames,
            };
            _steps.Add(s);
            _lastFrames += frames;
        }

        /// <summary>미니게임 도전: 처음 보인 도전의 제한 시간(보너스 포함 Duration)을 그 도전의 첫 미니게임 걸음에 단다.</summary>
        private void ReadChallenge(string category)
        {
            ChallengeSession current = CurrentChallenge();
            if (current != null && current != _challenge)
            {
                _challenge = current;
                _challengeIndex++;
                _limitPending = current.Duration;
            }
            if (current == null) _challenge = null;
            if (current != null && category == PaceCategories.Minigame)
            {
                _pending.Challenge = _challengeIndex;
                _pending.MinigameLimitSeconds = _limitPending;
                _limitPending = 0;
            }
        }

        private ChallengeSession CurrentChallenge()
        {
            foreach (GameScreen s in _body.Screens.OpenScreens)
                if (s is MinigameScreen m && m.IsOpen && m.Session != null && m.Session.Current != null) return m.Session.Current;
            return null;
        }

        /// <summary>떠 있는 화면(HUD·미니게임 빼고)이 지금 그리는 글줄 — 사람이 화면에서 보는 글.</summary>
        private List<string> VisibleText()
        {
            _lines.Clear();
            foreach (GameScreen s in _body.Screens.OpenScreens)
            {
                if (!s.IsOpen || !_classifier.CountsText(s.Id)) continue;
                _draw.Clear();
                Emit(s.Root);
                foreach (DrawCommand c in _draw.Commands) if (c.Kind == DrawKind.Text) _lines.Add(c.Text);
            }
            return _lines;
        }

        private void Emit(UiNode node)
        {
            if (!node.Visible || node.Covered) return;
            node.Emit(_draw, _measure);
            for (int i = 0; i < node.Children.Count; i++) Emit(node.Children[i]);
        }

        private void Probe(PaceStep step)
        {
            PaceProbe p = _probe;
            p.OpenScreens.Clear();
            foreach (GameScreen s in _body.Screens.OpenScreens) if (s.Layer != ScreenLayer.Hud) p.OpenScreens.Add(s.Id);
            p.MapId = _body.InWorld ? _body.Walk.Map.Id : string.Empty;
            p.Interactions.Clear();
            p.Interactions.AddRange(_interactions);
            _interactions.Clear();
            p.MinigameMechanic = CurrentChallenge()?.Mechanic.ToString();
            p.Session = _body.Session;
            p.Definitions = _atlas.Definitions;
            foreach (IPaceNoveltyKind kind in _kinds) kind.Collect(p, step.Novelties);
        }
    }

    /// <summary>속도 기록을 붙인 한 판의 결과.</summary>
    public sealed class PacePlayResult
    {
        public PlayResult Play;
        public List<PaceStep> Steps;
        public long FramesBeforeFirstStep;
        public PaceRunInfo Info;
        public string OutputDir;
    }

    /// <summary>창 없는 몸으로 정책 하나를 돌리며 속도를 적고 output/simulation/pace/&lt;이름&gt;/에 보고서와 플레이 기록(play.txt 등)을 쓴다.</summary>
    public static class PacePlay
    {
        /// <summary>N일 판의 날 수(손으로 도는 보고). 없으면 7(설계 9절 4번).</summary>
        public const string DaysVariable = "ROOTBORN_PACE_DAYS";
        public const int DefaultDays = 7;
        /// <summary>데이터 덮어쓰기 폴더(Game 테스트 TestWorld.OverlayVariable와 같은 이름). 손으로 도는 비교 보고가 전후 비교로 바뀐다.</summary>
        public const string OverlayVariable = "ROOTBORN_DATA_OVERLAY";

        public static int ParseDays(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DefaultDays;
            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int days) && days >= 1) return days;
            throw new ArgumentException(DaysVariable + "는 1 이상의 정수여야 한다: '" + value + "'");
        }

        /// <param name="overlay">데이터 덮어쓰기 폴더(없으면 data/ 그대로).</param>
        public static PacePlayResult Run(string policy, int seed, PlaySettings settings, string name, string overlay = null)
        {
            PlayAtlas atlas = PlayAtlas.Load(PlayAtlas.FindRepoRoot(), overlay);
            using var body = new HeadlessBody(atlas);
            IPlayPolicy made = PlayerTypeCatalog.Has(policy) ? PlayerTypeCatalog.Create(policy, atlas) : PlayPolicyCatalog.Default().Create(policy, atlas);
            var runner = new PlayRunner(body, made, seed, settings, atlas, name);
            var recorder = new PaceRecorder(body, runner, atlas);
            var watch = Stopwatch.StartNew();
            PlayResult play = runner.Run();
            watch.Stop();
            recorder.Finish();
            var info = new PaceRunInfo
            {
                Name = name, Policy = policy, Seed = seed, DaysCompleted = play.DaysCompleted, Frames = play.Frames, WallSeconds = watch.Elapsed.TotalSeconds,
                End = End(play, settings),
            };
            info.Notes.Add("설정: 날 " + settings.Days + ", 하루 걸음 상한 " + settings.StepsPerDay + ", 판 걸음 상한 " + settings.MaxSteps
                + (settings.StopWhenChainCompleted == null ? string.Empty : ", 멈추는 체인 `" + settings.StopWhenChainCompleted + "`"));
            info.Notes.Add("체인 위치: " + (play.LastChainStep ?? "(없음)") + ", 끝낸 체인 단계 " + play.ChainStepsCompleted + ", 끝낸 퀘스트 " + play.QuestsCompleted);
            info.Notes.Add("한 판 기록(행동·불변식): output/simulation/" + name + "/");
            if (atlas.OverlayChanges.Count > 0) info.Notes.Add("덮어쓴 데이터로 돈 판: " + string.Join("; ", atlas.OverlayChanges));
            string dir = PaceReport.Directory(atlas.RepoRoot, name);
            List<PaceStep> steps = recorder.Steps.ToList();
            PaceReport.Write(dir, info, steps, HeadlessBody.FrameSeconds);
            PlayRecord.Write(play, atlas.RepoRoot, settings, ScreenCatalog.Create().Screens.Select(s => s.Id), atlas);
            return new PacePlayResult { Play = play, Steps = steps, FramesBeforeFirstStep = recorder.FramesBeforeFirstStep, Info = info, OutputDir = dir };
        }

        /// <summary>플레이어 유형 넷을 같은 설정으로 돌려 유형마다 보고서(&lt;이름&gt;-&lt;유형&gt;)와 비교 보고서(&lt;이름&gt;/report.md)를 쓴다.</summary>
        public static string Compare(string name, PlaySettings settings, int seed) => Compare(name, settings, seed, out _);

        /// <param name="failures">알려진 결함이 아닌 불변식 실패(유형 이름과 무엇).</param>
        public static string Compare(string name, PlaySettings settings, int seed, out List<string> failures) => Compare(name, settings, seed, null, out failures, out _);

        /// <summary>
        /// 유형 넷을 돌려 비교 보고서(&lt;이름&gt;/report.md)와 잰 값 파일(&lt;이름&gt;/pace-measured.json — 먼 층이 읽는다)을 쓴다.
        /// </summary>
        public static string Compare(string name, PlaySettings settings, int seed, string overlay, out List<string> failures, out List<PaceCompareEntry> entries)
        {
            failures = new List<string>();
            entries = new List<PaceCompareEntry>();
            foreach (string type in PlayerTypeCatalog.Names)
            {
                PacePlayResult r = Run(type, seed, settings, name + "-" + type, overlay);
                if (r.Play.Failure != null && Rootborn.Simulation.Tests.KnownDefects.Matches(r.Play.Failure) == null) failures.Add(type + ": " + FirstLine(r.Play.Failure.What));
                entries.Add(new PaceCompareEntry { Type = type, Label = PlayerTypeCatalog.Label(type), Info = r.Info, Steps = r.Steps });
            }
            string dir = PaceReport.Directory(PlayAtlas.FindRepoRoot(), name);
            PaceCompare.Write(dir, name, entries, HeadlessBody.FrameSeconds);
            PaceMeasured.FromRuns(entries.Select(e => (e.Info.Name, e.Steps)), HeadlessBody.FrameSeconds).Write(Path.Combine(dir, PaceMeasured.FileName));
            return dir;
        }

        /// <summary>
        /// 덮어쓰기 전후 비교(가까운 층): 같은 설정·씨앗으로 유형 넷을 data/ 그대로와 덮어쓴 것 둘 다 돌려 &lt;이름&gt;/report.md 하나에 전·후·차이로 쓴다.
        /// 유형마다 보고서는 &lt;이름&gt;-before-&lt;유형&gt;, &lt;이름&gt;-after-&lt;유형&gt;.
        /// </summary>
        public static string CompareOverlay(string name, PlaySettings settings, int seed, string overlay, out List<string> failures)
        {
            Compare(name + "-before", settings, seed, null, out List<string> f1, out List<PaceCompareEntry> before);
            Compare(name + "-after", settings, seed, overlay, out List<string> f2, out List<PaceCompareEntry> after);
            failures = f1.Concat(f2).ToList();
            var changes = PlayAtlas.Load(PlayAtlas.FindRepoRoot(), overlay).OverlayChanges.Select(c => c.ToString()).ToList();
            string dir = PaceReport.Directory(PlayAtlas.FindRepoRoot(), name);
            PaceDiff.Write(dir, name, changes, before, after, HeadlessBody.FrameSeconds, new[] { PaceReport.EvidenceLine, "가까운 층: 플레이어 유형 넷, 날 " + settings.Days + ", 씨앗 " + seed + "." });
            return dir;
        }

        private static string FirstLine(string s)
        {
            int nl = s.IndexOf('\n');
            return nl < 0 ? s : s.Substring(0, nl);
        }

        private static string End(PlayResult play, PlaySettings settings)
        {
            if (play.Failure != null) return "불변식 실패 — " + play.Failure.What.Split('\n')[0];
            if (settings.StopWhenChainCompleted != null && play.FinalState.Any(l => l.StartsWith("chain " + settings.StopWhenChainCompleted + " Completed", StringComparison.Ordinal)))
                return "체인을 끝냈다(" + play.DaysCompleted + "일 넘김)";
            if (play.Steps.Count >= settings.MaxSteps) return "판 걸음 상한 " + settings.MaxSteps + "에 닿음(" + play.DaysCompleted + "일 넘김)";
            return play.DaysCompleted + "일을 넘겼다(날 수 " + settings.Days + ")";
        }
    }
}
