using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 날 넘김(07:00)을 넘겨 끝나는 행동을 게임이 어떻게 정산하는지(설계 13절 보강 S1). 시뮬레이터 없이 GameSession 명령만으로 본다 — 자동 테스트.
    /// 걷기·시각 맞추기는 준비 단계다.
    /// </summary>
    public sealed class CarryFactsTests
    {
        private const float Share = 0.5f;

        private static Rootborn.Core.Time.TimeRules Rules => TestWorld.Definitions.TimeRules;

        private static Facility FirstFacility => TestWorld.Definitions.All<Facility>().First();

        private static FacilityWork LevelOneWork
            => TestWorld.Definitions.All<FacilityWork>().Where(w => w.FacilityId == FirstFacility.Id && w.MinimumFacilityLevel == 1 && w.Minigame != null)
                .OrderBy(w => w.Id, StringComparer.Ordinal).First();

        private static void ClockTo(GameSession game, int minutes)
        {
            int left = minutes - game.Student.TimeMinutes;
            if (left > 0) game.AdvanceTime(DayAssumptions.RealSeconds(game.TimeRules, left) + 0.001f, false);
        }

        private static string State(GameSession game, string label)
            => label + ": 날 " + game.Student.CurrentDay + ", 시각 " + game.Student.TimeMinutes + ", 지갑 " + game.Wallet.Balance + ", 체력 " + game.Student.Energy
               + ", 지침 " + game.IsExhausted + ", 하루 " + game.Student.DayState;

        [TestCase(-60, TestName = "Facility_EndsAfterDayEnd_WhenOnlyTheSettlementCrosses")]
        [TestCase(-30, TestName = "Facility_MinigameItselfCrossesDayEnd")]
        public void FacilityWorkCrossingDayEnd(int startBeforeEndMinusWork)
        {
            var work = LevelOneWork;
            var game = RunStarts.Get("adult").Start(new MemorySaveStore(), FirstFacility);
            int mg = DayAssumptions.GameMinutesUp(game.TimeRules, DayAssumptions.MinigameSeconds(work.Minigame, Share));
            // 첫째: 미니게임은 07:00 전에 끝나고 정산이 07:00을 넘긴다. 둘째: 미니게임 도중에 07:00이 된다.
            int start = startBeforeEndMinusWork == -60 ? Rules.DayEndMinutes - mg - 20 : Rules.DayEndMinutes - mg / 2;
            ClockTo(game, start);
            TestContext.Out.WriteLine(work.Id + " 정의 " + work.TimeCostMinutes + "분, 미니게임 " + mg + "분, 시작 " + start + ", 날 끝 " + Rules.DayEndMinutes);
            TestContext.Out.WriteLine(State(game, "시작 전"));
            var reserved = game.ReserveFacilityWork(work.Id, "carry:1", 1);
            TestContext.Out.WriteLine("예약 " + reserved.Kind);
            DayAssumptions.PlayMinigame(game, work.Minigame, Share);
            TestContext.Out.WriteLine(State(game, "미니게임 뒤"));
            var settled = game.SettleFacilityWork("carry:1", 80);
            TestContext.Out.WriteLine("정산 " + settled.Kind);
            TestContext.Out.WriteLine(State(game, "정산 뒤"));
            if (!settled.Applied) TestContext.Out.WriteLine("포기 " + game.ForfeitFacilityWork("carry:1").Kind);
            game.AdvanceTime(0f, false);
            TestContext.Out.WriteLine(State(game, "AdvanceTime(0) 뒤"));
            game.AdvanceTime(DayAssumptions.RealSeconds(game.TimeRules, 1) + 0.001f, false);
            TestContext.Out.WriteLine(State(game, "1분 뒤"));
        }

        [TestCase(-20, TestName = "PartTime_EndsAfterDayEnd_WhenOnlyTheSettlementCrosses")]
        [TestCase(-1, TestName = "PartTime_MinigameItselfCrossesDayEnd")]
        public void PartTimeWorkCrossingDayEnd(int mode)
        {
            foreach (var target in RunTargets.All("part-time-work", TestWorld.Definitions).Cast<IMinigamePartTimeWork>())
            {
                int mg = DayAssumptions.GameMinutesUp(Rules, DayAssumptions.MinigameSeconds(target.Minigame, Share));
                if (mg >= target.TimeCostMinutes) continue;
                var game = TestWorld.Start(new MemorySaveStore());
                int start = mode == -20 ? Rules.DayEndMinutes - mg - 20 : Rules.DayEndMinutes - mg / 2;
                ClockTo(game, start);
                var begun = game.BeginPartTimeWork(target.Id, "carry:1", 1);
                if (!begun.Applied) { TestContext.Out.WriteLine(target.Id + " 시작 안 됨 " + begun.Kind); continue; }
                TestContext.Out.WriteLine(target.Id + " 정의 " + target.TimeCostMinutes + "분, 미니게임 " + mg + "분, 시작 " + start);
                DayAssumptions.PlayMinigame(game, target.Minigame, Share);
                TestContext.Out.WriteLine(State(game, "미니게임 뒤"));
                var settled = game.SettleMinigame("carry:1", true);
                TestContext.Out.WriteLine("정산 " + settled.Kind);
                TestContext.Out.WriteLine(State(game, "정산 뒤"));
                game.AdvanceTime(0f, false);
                TestContext.Out.WriteLine(State(game, "AdvanceTime(0) 뒤"));
                return;
            }
            Assert.Fail("밤에 시작되는 알바가 없다");
        }
    }
}
