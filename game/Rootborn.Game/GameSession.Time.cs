using System;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Game
{
    /// <summary>
    /// 흐르는 시간(설계 2026-11-12-flowing-time-design.md 6절 2단계): 프레임마다 "지난 실제 초"를 받아 개인 시계를 보내고(<see cref="AdvanceTime"/>),
    /// 잠(<see cref="Sleep"/>)과 자지 않고 넘어간 날을 하루 마감 흐름에 잇는다. 규칙(밤의 체력, 지침, 회복)은 Core가 판단한다.
    /// 시간 규칙이 없는 정의(하루 길이가 없는 시험 데이터)면 시계는 흐르지 않고 옛 동작(아침 8시 시작, 안 자도 회복)이다.
    /// </summary>
    public sealed partial class GameSession
    {
        private FlowingClock _flow;

        /// <summary>흐르는 시간의 규칙(game/time.json + data/time). 없으면 null.</summary>
        public TimeRules TimeRules => _definitions.TimeRules;

        /// <summary>혼자인 방(GameSession이 만든 방)인가. 혼자 할 때만 패널·모달·대화가 시계를 멈춘다.</summary>
        public bool IsSoloRoom { get; private set; }

        // ---------- 조회 ----------

        /// <summary>보이는 시각(0~1439분). 그날 0시부터 센 분을 24시간으로 감는다(26:00 → 02:00).</summary>
        public int ClockMinutes => TimeRules.ClockMinutes(Student.TimeMinutes);

        /// <summary>
        /// 분 아래까지 든 시각(그날 0시부터 센 분, 정수부는 늘 <c>Student.TimeMinutes</c>). NPC 걷기가 분마다 튀지 않게 쓴다.
        /// 혼자 하기: 흐르는 시계의 남은 조각. 같이 하는 방: 개인 시계의 분이 바뀐 뒤 들어온 실제 초 ÷ 게임 1분의 실제 초(0.999에서 자른다, 분이 바뀌면 0부터).
        /// 시간 규칙이 없으면 조각은 0이다.
        /// </summary>
        public double DayMinutesExact => Student.TimeMinutes + MinuteFraction();

        // 같이 하는 방의 조각: 개인 시계가 마지막으로 본 분과 그 뒤 들어온 실제 초.
        private double _sinceMinuteSeconds;
        private int _lastSeenMinutes = int.MinValue;

        private double MinuteFraction()
        {
            if (IsSoloRoom) return _flow?.MinuteFraction ?? 0d;
            // 방을 잇는 쪽이 시계를 옮겼으면(SyncWithRoom) 아직 그 분의 실제 초를 받지 않았다.
            if (Student.TimeMinutes != _lastSeenMinutes || TimeRules == null) return 0d;
            double secondsPerMinute = TimeRules.RealSecondsPerGameDay / (double)Core.Time.TimeRules.MinutesPerDay;
            return secondsPerMinute <= 0d ? 0d : Math.Min(FlowingClock.MaxMinuteFraction, _sinceMinuteSeconds / secondsPerMinute);
        }

        // 같이 하는 방: 분이 바뀌면 조각을 0부터, 같은 분이면 실제 초를 더한다(방의 시계는 멈추지 않으므로 menuOpen과 무관하다).
        private void TrackRoomMinuteFraction(float realSeconds)
        {
            if (Student.TimeMinutes != _lastSeenMinutes)
            {
                _lastSeenMinutes = Student.TimeMinutes;
                _sinceMinuteSeconds = 0d;
                return;
            }
            if (float.IsNaN(realSeconds) || realSeconds <= 0f) return;
            _sinceMinuteSeconds += realSeconds;
        }

        /// <summary>체력이 0이 되어 지쳤다. 걷기·대화·잠만 된다. 자야 풀린다.</summary>
        public bool IsExhausted => Student.IsExhausted;

        /// <summary>취침 시각까지 남은 분(지났으면 0). 시간 규칙이 없으면 -1.</summary>
        public int MinutesUntilBedtime => TimeRules == null ? -1 : Math.Max(0, TimeRules.BedtimeMinutes - Student.TimeMinutes);

        /// <summary>날이 넘어가는 시각(다음 날 하루 시작)까지 남은 분. 시간 규칙이 없으면 -1.</summary>
        public int MinutesUntilDayEnd => TimeRules == null ? -1 : Student.MinutesUntilDayEnd;

        /// <summary>마지막 활동이 끝나는 시각(그날 0시부터 센 분). 혼자 하기에서는 곧바로 그 시각이 되므로 지금 시각 이하다.</summary>
        public int ActivityEndsAtMinutes => Student.BusyUntilMinutes;

        /// <summary>활동을 하는 중(같이 하기에서 끝나는 시각 전). 혼자 하기에서는 늘 false.</summary>
        public bool IsBusy => Student.IsBusy;

        /// <summary>지금 시간대에 묶인 일과 계획판 칸(없으면 -1). 칸은 시간 포인트가 아니라 시간대에 묶인다(Core DailyPlanProgress.TimeSlotFor).</summary>
        public int CurrentDailyPlanSlotIndex => DailyPlan.SlotIndexAt(Student.TimeMinutes);

        // ---------- 명령 ----------

        /// <summary>
        /// 시간 흐르기: Host가 프레임마다 지난 실제 초를 넘긴다(테스트는 손으로 돌린다). menuOpen은 패널·모달·대화가 열려 있거나 창이 비활성이라
        /// 혼자 하기의 시계를 멈출 때 true다. 같이 하는 방은 개인 시계를 방의 시각에 맞출 뿐 스스로 흐르지 않는다(방의 시계는 서버에서 흐르고 멈추지 않는다).
        /// 날이 넘어가는 시각(07:00)이 되면 자지 않은 날로 마감하고(회복 없음) 다음 날을 시작한다. 하루를 마감하고 결과를 보는 동안은 흐르지 않는다.
        /// 저장은 게임 1시간이 바뀔 때, 체력·지침이 바뀔 때, 날이 넘어갈 때 한다(분마다는 하지 않는다 — 꺼지면 1시간이 안 되는 시각을 잃는다).
        /// 알림: 분이 가면 <see cref="GameChangeTopics.Time"/>, 체력이 바뀌면 Student, 날이 넘어가면 Day. 지난 게임 분을 돌려준다.
        /// </summary>
        public int AdvanceTime(float realSeconds, bool menuOpen)
        {
            // 같이 하는 방: 시계는 방의 것이다. 개인 시계는 방의 시각을 따라간다(GameSession.Room).
            if (!IsSoloRoom)
            {
                FollowRoom(out int followed);
                TrackRoomMinuteFraction(realSeconds);
                return followed;
            }

            if (_flow == null || Student.DayState != StudentDayState.InProgress) return 0;
            int minutes = _flow.Advance(realSeconds, menuOpen && IsSoloRoom);
            if (minutes <= 0 && !Student.HasDayRunOut) return 0;

            int hourBefore = Student.TimeMinutes / 60, energyBefore = Student.Energy, dayBefore = Student.CurrentDay;
            bool exhaustedBefore = Student.IsExhausted;
            int moved = 0;
            // 한 번에 큰 초가 와도(창을 오래 띄운 뒤 등) 날마다 끊어 보낸다. 같이 하는 방에서 다음 날을 못 열면 멈춘다.
            for (int guard = 0; guard < 64; guard++)
            {
                if (Student.HasDayRunOut && !RollOverAwake()) break;
                if (minutes <= 0 || Student.DayState != StudentDayState.InProgress) break;
                int step = Math.Min(minutes, Math.Max(1, Student.MinutesUntilDayEnd));
                moved += Student.AdvanceClock(step);
                minutes -= step;
            }

            bool newDay = Student.CurrentDay != dayBefore;
            bool vitals = Student.Energy != energyBefore || Student.IsExhausted != exhaustedBefore;
            if (newDay) return moved; // 날 넘김이 이미 저장하고 알렸다.
            if (vitals || Student.TimeMinutes / 60 != hourBefore) Save();
            if (vitals) Notify(null, GameChangeTopics.Student, GameChangeTopics.Time);
            else if (moved > 0) Notify(null, GameChangeTopics.Time);
            return moved;
        }

        /// <summary>
        /// 잠: 지금 시각에 자고 하루를 마감한다(지금의 하루 마감과 같은 흐름 — 하루 끝 규칙, 방에 마감 알림, 마일스톤·캠페인 하루 결과, 저장).
        /// 회복은 하루 끝 규칙이 잔 시각으로 정한다(취침 시각까지 자면 전부, 늦으면 덜). 지침이 풀린다. 다음 날은 <see cref="StartNextDay"/>로 07:00에 시작한다.
        /// 지친 동안에도 잘 수 있다. 낮에 자도 다음 날 07:00에 일어난다.
        /// </summary>
        public GameDayResult Sleep() => FinishDay(slept: true);

        // 자지 않고 날이 넘어갔다: 회복 없이 마감하고 곧바로 다음 날을 연다(혼자인 방은 마감하는 순간 다음 날). 열었으면 true.
        // 같이 하는 방이 아직 다음 날을 알리지 않았으면 기다렸다가 방이 다음 날이 되는 순간 연다(FollowRoom).
        private bool RollOverAwake()
        {
            if (!FinishDay(slept: false).Ended) return false;
            if (StartNextDay()) return true;
            _rollOverPending = true;
            return false;
        }

        private GameDayResult FinishDay(bool slept)
        {
            int day = Student.CurrentDay;
            StudentDaySummary summary;
            bool ended = slept ? Student.TrySleep(_dayEndRules, out summary) : Student.TryEndDayAwake(_dayEndRules, out summary);
            if (!ended) return new GameDayResult(false, summary, default, default);
            // 혼자인 방은 여기서 다음 날로 넘어간다. 같이 하는 방은 모두 잠들거나 07:00이 되어야 넘어간다.
            // 자지 않고 넘긴 날(방은 이미 다음 날)의 잠은 방이 받지 않는다(지난 날의 잠).
            Room.SleepOn(PlayerId, day);
            var milestones = Milestones.ApplyDayResult(_milestones, Student, Inventory, "day:" + day);
            var campaign = ApplyCampaign("campaign-result-day-" + day);
            LastDayResult = new GameDayResult(true, summary, milestones, campaign);
            SaveAndNotify(RewardTopics, GameChangeTopics.Day, GameChangeTopics.Time);
            return LastDayResult;
        }

        // ---------- 읽기·저장 ----------

        // 시간 규칙을 넣고 시계 파일(student-clock-*.json)을 읽는다. 없으면(옛 세이브·새 게임) 저장된 시각에서 새로 센다.
        private void LoadClock(ISaveStore store)
        {
            var rules = _definitions.TimeRules;
            Student.ConfigureTimeRules(rules);
            store.TryRead<StudentClockSaveData>(SaveSlot, SaveFileNames.StudentClock(PlayerId), out var clock);
            Student.RestoreClock(clock);
            _flow = rules != null ? new FlowingClock(rules.RealSecondsPerGameDay) : null;
        }

        private void SaveClock(ISaveStore store) => store.Write(SaveSlot, SaveFileNames.StudentClock(PlayerId), Student.ToClockSaveData());
    }
}
