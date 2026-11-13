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
    /// 날 넘김(07:00)을 넘겨 끝나는 행동을 게임이 어떻게 정산하는지(보강 S1) — 시뮬레이터 없이 GameSession 명령만으로 본 사실(2026-11-13 원격에서 확인). 자동 테스트.
    /// 시각 맞추기는 준비 단계다.
    /// - 미니게임은 07:00 전에 끝나고 정산만 07:00을 넘기면: 정산된다(보수는 시작한 날), 시계는 같은 날 07:00을 넘겨 간다.
    ///   다음 시계 흐름에서 날이 넘어가고, 다음 날은 07:00 + 넘긴 분에 시작한다(넘긴 분은 다음 날 아침에서 빠진다).
    /// - 미니게임을 하는 도중에 07:00이 되면: 그 자리에서 날이 넘어가고 정산이 거절된다(보수 없음, 쓴 시간은 다음 날로 이어 잃는다).
    /// </summary>
    public sealed class CarryFactsTests
    {
        private const float Share = 0.5f;
        private const int SettleMargin = 20;   // 미니게임이 07:00 몇 분 전에 끝나게 시작하는가

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

        private static int MinigameMinutes(Rootborn.Core.Minigames.Minigame minigame) => DayAssumptions.GameMinutesUp(Rules, DayAssumptions.MinigameSeconds(minigame, Share));

        [Test]
        public void FacilityWork_WhoseSettlementCrossesDayEnd_IsPaidOnTheStartDay_AndTheNextDayStartsLateByTheOverrun()
        {
            var work = LevelOneWork;
            int mg = MinigameMinutes(work.Minigame);
            Assert.That(mg, Is.LessThan(work.TimeCostMinutes), "전제: 미니게임이 정의의 분보다 짧다");
            var game = RunStarts.Get("adult").Start(new MemorySaveStore(), FirstFacility);
            int start = Rules.DayEndMinutes - mg - SettleMargin;
            ClockTo(game, start);
            int day = game.Student.CurrentDay, wallet = game.Wallet.Balance, at = game.Student.TimeMinutes;

            Assert.That(game.ReserveFacilityWork(work.Id, "carry:1", 1).Applied, Is.True);
            DayAssumptions.PlayMinigame(game, work.Minigame, Share);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day), "미니게임은 07:00 전에 끝난다");
            Assert.That(game.SettleFacilityWork("carry:1", 80).Applied, Is.True, "07:00을 넘겨 끝나도 정산된다");
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day), "정산 직후에는 아직 같은 날");
            Assert.That(game.Student.TimeMinutes, Is.EqualTo(at + work.TimeCostMinutes), "시계는 같은 날 07:00을 넘겨 간다");
            Assert.That(game.Wallet.Balance, Is.GreaterThan(wallet), "보수는 시작한 날에 받는다");

            int overrun = game.Student.TimeMinutes - Rules.DayEndMinutes;
            game.AdvanceTime(0f, false);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day + 1), "다음 시계 흐름에서 날이 넘어간다");
            Assert.That(game.Student.TimeMinutes, Is.EqualTo(Rules.DayStartMinutes + overrun), "넘긴 분은 다음 날 아침에서 빠진다");
            Assert.That(game.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
        }

        [Test]
        public void FacilityWork_WhoseMinigameCrossesDayEnd_IsNotPaid()
        {
            var work = LevelOneWork;
            int mg = MinigameMinutes(work.Minigame);
            var game = RunStarts.Get("adult").Start(new MemorySaveStore(), FirstFacility);
            ClockTo(game, Rules.DayEndMinutes - mg / 2);
            int day = game.Student.CurrentDay, wallet = game.Wallet.Balance;

            Assert.That(game.ReserveFacilityWork(work.Id, "carry:1", 1).Applied, Is.True);
            DayAssumptions.PlayMinigame(game, work.Minigame, Share);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day + 1), "미니게임 도중에 날이 넘어간다");
            Assert.That(game.Student.TimeMinutes, Is.GreaterThan(Rules.DayStartMinutes), "남은 미니게임 분은 다음 날 아침에 흐른다");
            Assert.That(game.SettleFacilityWork("carry:1", 80).Applied, Is.False, "넘어간 뒤의 정산은 거절된다(다음 날 시작이 예약을 포기 처리)");
            Assert.That(game.Wallet.Balance, Is.EqualTo(wallet), "보수 없음");
        }

        [Test]
        public void PartTimeWork_WhoseSettlementCrossesDayEnd_IsPaid_AndTheMinigameCrossingIsNot()
        {
            var work = RunTargets.All("part-time-work", TestWorld.Definitions).Cast<IMinigamePartTimeWork>()
                .FirstOrDefault(w => MinigameMinutes(w.Minigame) < w.TimeCostMinutes && CanBeginAtNight(w));
            Assert.That(work, Is.Not.Null, "밤에 시작되고 미니게임이 정의의 분보다 짧은 알바가 없다");
            int mg = MinigameMinutes(work.Minigame);

            var paid = TestWorld.Start(new MemorySaveStore());
            ClockTo(paid, Rules.DayEndMinutes - mg - SettleMargin);
            int day = paid.Student.CurrentDay, at = paid.Student.TimeMinutes, wallet = paid.Wallet.Balance;
            Assert.That(paid.BeginPartTimeWork(work.Id, "carry:1", 1).Applied, Is.True);
            DayAssumptions.PlayMinigame(paid, work.Minigame, Share);
            Assert.That(paid.SettleMinigame("carry:1", true).Applied, Is.True, work.Id);
            Assert.That(paid.Student.CurrentDay, Is.EqualTo(day));
            Assert.That(paid.Student.TimeMinutes, Is.EqualTo(at + work.TimeCostMinutes));
            Assert.That(paid.Wallet.Balance, Is.GreaterThan(wallet));
            int overrun = paid.Student.TimeMinutes - Rules.DayEndMinutes;
            paid.AdvanceTime(0f, false);
            Assert.That(paid.Student.TimeMinutes, Is.EqualTo(Rules.DayStartMinutes + overrun));

            var lost = TestWorld.Start(new MemorySaveStore());
            ClockTo(lost, Rules.DayEndMinutes - mg / 2);
            day = lost.Student.CurrentDay;
            wallet = lost.Wallet.Balance;
            Assert.That(lost.BeginPartTimeWork(work.Id, "carry:2", 1).Applied, Is.True);
            DayAssumptions.PlayMinigame(lost, work.Minigame, Share);
            Assert.That(lost.Student.CurrentDay, Is.EqualTo(day + 1));
            Assert.That(lost.SettleMinigame("carry:2", true).Applied, Is.False);
            Assert.That(lost.Wallet.Balance, Is.EqualTo(wallet));
        }

        private static bool CanBeginAtNight(IMinigamePartTimeWork work)
        {
            var game = TestWorld.Start(new MemorySaveStore());
            ClockTo(game, Rules.DayEndMinutes - MinigameMinutes(work.Minigame) - SettleMargin);
            return game.BeginPartTimeWork(work.Id, "probe", 1).Applied;
        }
    }
}
