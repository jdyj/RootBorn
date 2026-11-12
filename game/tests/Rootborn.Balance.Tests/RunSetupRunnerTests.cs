using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 한 판의 가정(RunSetup)을 바꿔 돌린다: 실제 data/, 메모리 저장소, GameSession 명령. 자동 테스트다(가정을 넣은 계산, 화면 없음).
    /// 시설은 정의의 첫째, 전략은 facility-work-only가 기본.
    /// </summary>
    public sealed class RunSetupRunnerTests
    {
        private const int TenDays = 10, ThirtyDays = 30;

        private static readonly Lazy<BalanceConfig> Config = new Lazy<BalanceConfig>(() =>
        {
            var config = BalanceConfig.Read();
            config.Validate();
            return config;
        });

        private static Facility FirstFacility => TestWorld.Definitions.All<Facility>().First();

        private static readonly Lazy<RunResult> NeverRun = new Lazy<RunResult>(() => Run("facility-work-only", s => s.Sleep = "never", TenDays));
        private static readonly Lazy<RunResult> BaselineRun = new Lazy<RunResult>(() => Run("facility-work-only", _ => { }, TenDays));

        private static RunResult Run(string strategyId, Action<RunSetup> edit, int days)
        {
            var config = Config.Value;
            var setup = RunSetup.Baseline(config);
            edit(setup);
            return BalanceRunner.Run(config.Strategies.Single(s => s.Id == strategyId), FirstFacility, setup, config, days);
        }

        [Test]
        public void NeverSleeping_RunsAllDays_WithoutHanging_AndNeverRecordsSlept()
        {
            var run = NeverRun.Value;
            TestContext.Out.WriteLine(Lines(run));
            Assert.That(run.Days.Count, Is.EqualTo(TenDays));
            Assert.That(run.Days.All(d => !d.Slept && d.SleptAtMinutes == -1), Is.True, "자지 않는 판인데 잔 날이 있다");
            for (int i = 1; i < run.Days.Count; i++)
                Assert.That(run.Days[i].Day, Is.EqualTo(run.Days[i - 1].Day + 1), "날 번호가 하나씩 늘지 않는다");
        }

        [Test]
        public void NeverSleeping_GetsExhausted_AndThoseDaysStillHaveARow()
        {
            var run = NeverRun.Value;
            var exhausted = run.Days.Where(d => d.Exhausted).ToList();
            Assert.That(exhausted, Is.Not.Empty, "자지 않았는데 지친 날이 없다. 날마다 체력: " + string.Join(", ", run.Days.Select(d => d.Day + "일 " + d.EnergyAtEnd)));
            foreach (var day in exhausted)
                Assert.That(day.Stop, Is.EqualTo(StopReasons.DayRanOut), day.Day + "일째: 자지 않는 밤에 지치면 날이 넘어가기를 기다린다");
        }

        [Test]
        public void SleepingAt2200_NeverRecordsExhausted()
        {
            var run = BaselineRun.Value;
            int bedtime = TestWorld.Definitions.TimeRules.BedtimeMinutes;
            Assert.That(RunSetup.Baseline(Config.Value).Sleep, Is.EqualTo("finish-by 22:00"));
            Assert.That(run.Days.Count(d => d.Exhausted), Is.Zero, Lines(run));
            foreach (var day in run.Days)
            {
                Assert.That(day.Slept, Is.True, day.Day + "일째");
                Assert.That(day.SleptAtMinutes, Is.InRange(0, bedtime), day.Day + "일째");
            }
        }

        [Test]
        public void FinishBy0200_DoesMoreActionsPerDayThanFinishBy2200_OnDayOne()
        {
            var late = Run("facility-work-only", s => s.Sleep = "finish-by 02:00", 1);
            Assert.That(late.Days[0].Actions, Is.GreaterThan(BaselineRun.Value.Days[0].Actions), Lines(late) + " | " + Lines(BaselineRun.Value));
        }

        [Test]
        public void NoActionCrossesDayEnd_WhenStayingUp()
        {
            int dayEnd = TestWorld.Definitions.TimeRules.DayEndMinutes, bedtime = TestWorld.Definitions.TimeRules.BedtimeMinutes;
            foreach (var day in NeverRun.Value.Days)
                Assert.That(day.EndedAtMinutes, Is.LessThanOrEqualTo(dayEnd), day.Day + "일째");
            Assert.That(NeverRun.Value.Days.Count(d => d.EndedAtMinutes > bedtime), Is.GreaterThan(0), "취침 시각 뒤에 끝난 날이 없으면 이 검사는 빈 통과다: " + Lines(NeverRun.Value));
        }

        // P1: 자는 밤에 지치면 그 자리에서 자고, 다음 날은 회복해 다시 일한다.
        [Test]
        public void StayUpEvery2_ExhaustedOnASleepingNight_SleepsThere_AndTheNextDayWorks()
        {
            const int Days = 8;
            var run = Run("facility-work-only", s => s.Sleep = "stay-up-every 2", Days);
            TestContext.Out.WriteLine(Lines(run));
            int dayEnd = Rules.DayEndMinutes;
            Assert.That(run.Days.Count, Is.EqualTo(Days));
            for (int i = 0; i < run.Days.Count; i++)
                if ((i + 1) % 2 == 0) Assert.That(run.Days[i].Slept, Is.False, (i + 1) + "째 날은 밤새는 날이다");

            var tired = Enumerable.Range(0, run.Days.Count).Where(i => run.Days[i].Stop == StopReasons.Exhausted).ToList();
            Assert.That(tired, Is.Not.Empty, "자는 밤에 지쳐서 잔 날이 없다. 날마다 체력·멈춘 이유: " + string.Join(", ", run.Days.Select(d => d.Day + "일 " + d.EnergyAtEnd + " " + d.Stop)));
            foreach (int i in tired)
            {
                var day = run.Days[i];
                Assert.That(day.Slept, Is.True, day.Day + "일째: 지쳐서 멈춘 자는 밤에는 잔다");
                Assert.That(day.SleptAtMinutes, Is.InRange(day.EndedAtMinutes, dayEnd), day.Day + "일째: 잔 시각은 마지막 행동 뒤, 날 넘김 전");
            }
            var next = tired.Where(i => i + 1 < run.Days.Count).ToList();
            Assert.That(next, Is.Not.Empty, "지쳐서 잔 다음 날이 판 안에 없다");
            foreach (int i in next)
                Assert.That(run.Days[i + 1].Actions, Is.GreaterThan(0), run.Days[i + 1].Day + "일째: 지쳐서 잔 다음 날은 지친 채 시작하지 않는다");
        }

        // P2: 미니게임이 정의의 분보다 긴 일은 미니게임 길이로 판정한다.
        [Test]
        public void ALongMinigameWork_IsPlannedAtItsMinigameLength()
        {
            var works = LongMinigameWorks(FullShare);
            Assert.That(works, Is.Not.Empty, "몫 1.0에서 미니게임이 정의의 분보다 긴 알바가 없다(가짜 행동으로 시험해야 한다)");
            foreach (var work in works)
            {
                var game = TestWorld.Start(new MemorySaveStore());
                var context = new RunContext { Target = work, MinigameShare = FullShare };
                var plan = new PartTimeWorkAction().Plan(game, context);
                TestContext.Out.WriteLine(work.Id + ": 정의 " + work.TimeCostMinutes + "분, 미니게임 " + MinigameMinutes(work.Minigame, FullShare) + "분");
                Assert.That(plan.Minutes, Is.EqualTo(MinigameMinutes(work.Minigame, FullShare)), work.Id);
            }
        }

        [Test]
        public void ALongMinigameWork_NeverEndsAfterBedtime_WhenSleepingBy2200()
        {
            int bedtime = Rules.BedtimeMinutes, actions = 0;
            foreach (var work in LongMinigameWorks(FullShare))
            {
                var config = Config.Value;
                var setup = RunSetup.Baseline(config);
                setup.MinigameShare = FullShare;
                var run = BalanceRunner.Run(config.Strategies.Single(s => s.Id == "one-part-time-work"), work, setup, config, 3);
                TestContext.Out.WriteLine(Lines(run));
                foreach (var day in run.Days)
                {
                    Assert.That(day.EndedAtMinutes, Is.LessThanOrEqualTo(bedtime), work.Id + " " + day.Day + "일째");
                    actions += day.Actions;
                }
            }
            Assert.That(actions, Is.GreaterThan(0), "한 일이 없으면 이 검사는 빈 통과다");
        }

        // P3: 걷기가 1분 더 걸려 규칙 끝을 넘으면 그 행동은 하지 않는다(걷기는 남는다).
        [Test]
        public void AWalkThatRunsOneMinuteLong_DoesNotStartTheActionPastTheRule()
        {
            var config = Config.Value;
            var setup = RunSetup.Baseline(config);
            var actions = new IBalanceAction[] { new CarryOverAction(), new ExactlyToBedtimeAction() };
            var run = BalanceRunner.Run(config.Strategies.Single(s => s.Id == "facility-work-only"), FirstFacility, setup, config, 1, actions);
            var day = run.Days[0];
            TestContext.Out.WriteLine(Lines(run));
            Assert.That(day.SleptAtMinutes, Is.EqualTo(Rules.DayStartMinutes + setup.TravelMinutes + 1), "전제: 남은 조각 때문에 걷기가 1분 더 걸렸다");
            Assert.That(day.ActionCounts.ContainsKey(ExactlyToBedtimeAction.KindName), Is.False, "걷기 뒤 규칙 끝을 넘는 행동을 했다");
            Assert.That(day.EndedAtMinutes, Is.LessThanOrEqualTo(Rules.BedtimeMinutes));
            Assert.That(day.TravelMinutes, Is.EqualTo(setup.TravelMinutes), "그 걷기는 걷는 시간에 남는다");
        }

        // P4: 자지 않는 밤에 날이 이미 다 갔으면 분을 보내지 않고 날만 넘긴다(새 날이 07:01에 시작하지 않는다).
        [Test]
        public void NeverSleeping_EveryDayStartsAtDayStart()
        {
            var run = Run("facility-work-only", s => { s.Sleep = "never"; s.TravelMinutes = 0; }, 3);
            TestContext.Out.WriteLine(Lines(run));
            Assert.That(run.Days[0].EndedAtMinutes, Is.EqualTo(Rules.DayEndMinutes), "전제: 첫날 마지막 행동이 날 넘김 시각에 딱 끝난다");
            foreach (var day in run.Days)
                Assert.That(day.StartedAtMinutes, Is.EqualTo(Rules.DayStartMinutes), day.Day + "일째의 첫 행동 전 시각");
        }

        // P6: 레벨 2 전략은 레벨 2 일이 열리기 전에는 할 수 있는 가장 높은 일로 올릴 돈을 번다.
        [Test]
        public void Level2Strategy_EarnsBeforeLevel2_ThenOnlyDoesLevel2Work()
        {
            var run = Run("facility-level2-work-upgrade", s => s.Score = "fixed 100", ThirtyDays);
            TestContext.Out.WriteLine(Lines(run));
            Assert.That(Earned(run), Is.GreaterThan(0));
            int reached = run.Days.FindIndex(d => d.FacilityLevel >= 2);
            Assert.That(reached, Is.GreaterThanOrEqualTo(0), "30일 안에 레벨 2에 닿지 않았다");
            var levels = Works();
            var after = run.Days.Skip(reached + 1).SelectMany(d => d.ActionCounts.Keys).Where(levels.ContainsKey).Distinct().ToList();
            Assert.That(after, Is.Not.Empty, "레벨 2 뒤에 한 일이 없으면 이 검사는 빈 통과다");
            foreach (var id in after) Assert.That(levels[id], Is.EqualTo(2), "레벨 2에 닿은 뒤에는 레벨 2 일만 한다: " + id);
        }

        private const float FullShare = 1.0f;

        private static Rootborn.Core.Time.TimeRules Rules => TestWorld.Definitions.TimeRules;

        // 몫 share에서 미니게임을 하는 실제 초를 게임 분으로 올림한 값이 정의의 분보다 큰 알바(정의에서 찾는다).
        private static List<IMinigamePartTimeWork> LongMinigameWorks(float share)
            => RunTargets.All("part-time-work", TestWorld.Definitions).Cast<IMinigamePartTimeWork>()
                .Where(w => MinigameMinutes(w.Minigame, share) > w.TimeCostMinutes).ToList();

        private static int MinigameMinutes(Rootborn.Core.Minigames.Minigame minigame, float share)
            => (int)Math.Ceiling(DayAssumptions.MinigameSeconds(minigame, share) * Rootborn.Core.Time.TimeRules.MinutesPerDay / (double)Rules.RealSecondsPerGameDay - 1e-9);

        // 시계를 1분이 안 되게(0.999분) 보내 조각을 남긴다 — 다음 걷기가 1분 더 걸린다.
        private sealed class CarryOverAction : IBalanceAction
        {
            private const float AlmostAMinute = 0.4163f;   // 600초 ÷ 1440분 = 0.41667초가 1분
            private bool _done;
            public string Kind => "test-carry-over";
            public PlannedStep Plan(GameSession game, RunContext context)
                => _done ? null : new PlannedStep { Id = Kind, Minutes = 0, Walks = false, Do = (g, request) => { _done = true; g.AdvanceTime(AlmostAMinute, false); return new StepOutcome { Done = true }; } };
        }

        // 걷기 + 이 행동이 취침 시각에 딱 끝나게 길이를 잡는다.
        private sealed class ExactlyToBedtimeAction : IBalanceAction
        {
            public const string KindName = "test-exactly-to-bedtime";
            public string Kind => KindName;
            public PlannedStep Plan(GameSession game, RunContext context)
            {
                int minutes = game.TimeRules.BedtimeMinutes - game.Student.TimeMinutes - DayAssumptions.Current.TravelMinutes;
                if (minutes <= 0) return null;
                return new PlannedStep
                {
                    Id = Kind, Minutes = minutes, Walks = true,
                    Do = (g, request) => { g.AdvanceTime(DayAssumptions.RealSeconds(g.TimeRules, minutes) + 0.001f, false); return new StepOutcome { Done = true }; },
                };
            }
        }

        [Test]
        public void TravelZero_FitsMoreActionsThanTravelSixty()
        {
            var zero = Run("facility-work-only", s => s.TravelMinutes = 0, 1);
            var sixty = Run("facility-work-only", s => s.TravelMinutes = 60, 1);
            Assert.That(zero.Days[0].Actions, Is.GreaterThan(sixty.Days[0].Actions), Lines(zero) + " | " + Lines(sixty));
        }

        [Test]
        public void MinigameShare_ChangesMinigameRealSeconds_NotIncome()
        {
            var quarter = Run("facility-work-only", s => s.MinigameShare = 0.25f, 1).Days[0];
            var full = Run("facility-work-only", s => s.MinigameShare = 1.0f, 1).Days[0];
            Assert.That(quarter.MinigameRealSeconds, Is.GreaterThan(0f));
            Assert.That(full.Earned, Is.EqualTo(quarter.Earned), "몫은 시간만 바꾼다");
            Assert.That(full.MinigameRealSeconds, Is.EqualTo(quarter.MinigameRealSeconds * 4f).Within(0.01f));
        }

        [Test]
        public void FailEvery2_EarnsLessThanFixed80()
        {
            var failing = Run("facility-work-only", s => s.Score = "fail-every 2", TenDays);
            Assert.That(Earned(failing), Is.LessThan(Earned(BaselineRun.Value)), Lines(failing));
        }

        [Test]
        public void FacilityWorkLevel1_OnlyEverDoesLevelOneWork_EvenAfterUpgrading()
        {
            var run = Run("facility-level1-work-upgrade", s => s.Score = "fixed 100", ThirtyDays);
            TestContext.Out.WriteLine(Lines(run));
            Assert.That(run.Days.Any(d => d.Upgraded), Is.True, "올린 날이 없으면 이 검사는 빈 통과다");
            var levels = Works();
            var done = run.Days.SelectMany(d => d.ActionCounts.Keys).Where(levels.ContainsKey).Distinct().ToList();
            Assert.That(done, Is.Not.Empty);
            foreach (var id in done) Assert.That(levels[id], Is.EqualTo(1), "레벨 1 일만 해야 한다: " + id);
        }

        [Test]
        public void MasterThenAutoSkip_EventuallyAutoSkipsTheHighestWork()
        {
            var run = Run("facility-master-then-autoskip", s => s.Score = "fixed 100", ThirtyDays);
            TestContext.Out.WriteLine(Lines(run));
            var last = run.Days[run.Days.Count - 1];
            Assert.That(last.AutoSkips, Is.GreaterThan(0), "마지막 날 자동 넘기기가 없다");
            var levels = Works();
            int top = TestWorld.Definitions.All<FacilityWork>()
                .Where(w => w.FacilityId == FirstFacility.Id && w.MinimumFacilityLevel <= last.FacilityLevel)
                .Max(w => w.MinimumFacilityLevel);
            foreach (var id in last.ActionCounts.Keys.Where(levels.ContainsKey))
                Assert.That(levels[id], Is.EqualTo(top), "마지막 날 한 일이 그 레벨에서 가장 높은 일이 아니다: " + id);
        }

        private static Dictionary<string, int> Works()
            => TestWorld.Definitions.All<FacilityWork>().ToDictionary(w => w.Id, w => w.MinimumFacilityLevel, StringComparer.Ordinal);

        private static long Earned(RunResult run) => run.Days.Sum(d => (long)d.Earned);

        private static string Lines(RunResult run)
            => run.StrategyId + "/" + run.TargetId + "/" + run.Setup.Id + " | " + string.Join(" / ", run.Days.Select(d => string.Join(" ", new object[]
            {
                d.Day, "번" + d.Earned, "행동" + d.Actions, string.Join(",", d.ActionCounts.Select(p => p.Key + "×" + p.Value)), "끝" + d.EndedAtMinutes,
                "잠" + (d.Slept ? d.SleptAtMinutes.ToString() : "-"), "지침" + d.Exhausted, "체력" + d.EnergyAtEnd, "레벨" + d.FacilityLevel, "넘김" + d.AutoSkips, d.Stop,
            })));
    }
}
