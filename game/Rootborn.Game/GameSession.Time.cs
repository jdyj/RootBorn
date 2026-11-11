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
        /// 혼자 하기의 시계를 멈출 때 true다(같이 하기는 멈추지 않는다).
        /// 날이 넘어가는 시각(07:00)이 되면 자지 않은 날로 마감하고(회복 없음) 다음 날을 시작한다. 하루를 마감하고 결과를 보는 동안은 흐르지 않는다.
        /// 저장은 게임 1시간이 바뀔 때, 체력·지침이 바뀔 때, 날이 넘어갈 때 한다(분마다는 하지 않는다 — 꺼지면 1시간이 안 되는 시각을 잃는다).
        /// 알림: 분이 가면 <see cref="GameChangeTopics.Time"/>, 체력이 바뀌면 Student, 날이 넘어가면 Day. 지난 게임 분을 돌려준다.
        /// </summary>
        public int AdvanceTime(float realSeconds, bool menuOpen)
        {
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
        private bool RollOverAwake()
        {
            if (!FinishDay(slept: false).Ended) return false;
            return StartNextDay();
        }

        private GameDayResult FinishDay(bool slept)
        {
            int day = Student.CurrentDay;
            StudentDaySummary summary;
            bool ended = slept ? Student.TrySleep(_dayEndRules, out summary) : Student.TryEndDayAwake(_dayEndRules, out summary);
            if (!ended) return new GameDayResult(false, summary, default, default);
            Room.EndDay(PlayerId); // 혼자인 방은 여기서 다음 날로 넘어간다. 같이 하는 방은 모두 마감해야 넘어간다.
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

        /// <summary>
        /// 혼자인 방의 문: 활동을 막지 않는다(하루에 할 수 있는 양은 깨어 있는 시간과 체력이 정한다). 쓴 몫은 방이 받을 수 있을 때만 알려
        /// 방의 때가 하루 안에서 나아가게 한다. 방의 때·넘기기는 4단계에서 없앤다.
        /// </summary>
        private sealed class SoloRoomGate : ITimePointGate
        {
            private readonly IRoomTime _room;
            private readonly string _participantId;

            public SoloRoomGate(IRoomTime room, string participantId)
            {
                _room = room;
                _participantId = participantId;
            }

            public bool CanSpend(int points) => points >= 0;

            public void Spent(int points)
            {
                if (points > 0 && _room.CanSpend(_participantId, points)) _room.Spend(_participantId, points);
            }
        }
    }
}
