using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rootborn.Game;
using Rootborn.Game.Tests;
using Rootborn.Simulation.Pace;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 먼 층의 속도 기록(설계 2026-11-13-play-pace-simulation-design.md 5절): 균형 시뮬레이터의 판(GameSession 명령, 화면·걷기 없음)에서 걷기·행동·밤마다
    /// 걸음 하나를 적는다. 걷기는 가까운 층에서 잰 상호작용 종류별 실제 초(없으면 균형 시뮬레이터의 걷기 가정 — 그 판의 걷는 분을 실제 초로),
    /// 행동은 가까운 층에서 그 종류의 화면이 낸 평균 새 글자·고르기(어림 — 없으면 0), 미니게임은 균형 시뮬레이터가 쓴 실제 초를 그 판의 몫으로 나눈
    /// 제한 시간, 잠은 잠자리 종류의 잰 값, 자지 않는 밤은 날이 넘어갈 때까지 기다린 실제 초(할 것 없음). 새로운 것은 같은 등록 표(GameSession 조회).
    /// 가까운 층과 같은 걸음 모양이라 같은 분석·보고서를 쓴다. "프레임"은 실제로 돈 것이 아니라 이 초 × 60이다(<see cref="SecondsPerFrame"/>).
    /// </summary>
    internal sealed class FarPaceRecorder : IRunObserver
    {
        private readonly PaceMeasured _measured;
        private readonly float _share;
        private readonly List<IPaceNoveltyKind> _kinds = new PaceNoveltyCatalog().Create();
        private readonly PaceProbe _probe = new PaceProbe();
        private int _challenge = -1;

        public FarPaceRecorder(PaceMeasured measured, float minigameShare)
        {
            _measured = measured;
            _share = minigameShare;
        }

        public readonly List<PaceStep> Steps = new List<PaceStep>(4096);
        public readonly SortedSet<string> MeasuredKinds = new SortedSet<string>(StringComparer.Ordinal), AssumedKinds = new SortedSet<string>(StringComparer.Ordinal);

        public void Started(GameSession game)
        {
            Add(new PaceStep { Day = game.Student.CurrentDay, Minutes = game.Student.TimeMinutes, Category = PaceCategories.ReadChoose, Key = "(판 시작)", By = "far" });
            Probe(game);
        }

        public void Walked(GameSession game, PlannedStep step, int travelMinutes)
        {
            double? measured = _measured?.WalkSeconds(step.InteractionKind);
            double seconds = measured ?? DayAssumptions.RealSeconds(game.TimeRules, travelMinutes);
            Note(step.InteractionKind, measured.HasValue);
            Add(new PaceStep
            {
                Day = game.Student.CurrentDay, Minutes = game.Student.TimeMinutes - travelMinutes, GameMinutes = travelMinutes, Category = PaceCategories.Walk,
                Frames = Frames(seconds), Key = "walk:" + step.InteractionKind, By = "far", InteractionKind = step.InteractionKind,
            });
        }

        public void Acted(GameSession game, PlannedStep step, StepOutcome outcome, int day, int minutes)
        {
            PaceMeasured.Screen screen = _measured?.ScreenFor(step.InteractionKind);
            Add(new PaceStep
            {
                Day = day, Minutes = minutes, GameMinutes = Elapsed(game, day, minutes), Category = PaceCategories.ReadChoose, By = "far",
                Key = (outcome.Id ?? step.Id) + (outcome.Done ? string.Empty : " (거절)"), InteractionKind = step.InteractionKind,
                NewChars = screen == null ? 0 : (int)Math.Round(screen.Chars), Choices = screen == null ? 0 : (int)Math.Round(screen.Choices),
            });
            if (outcome.MinigameRealSeconds > 0)
                Add(new PaceStep
                {
                    Day = day, Minutes = minutes, Category = PaceCategories.Minigame, Key = "minigame:" + (outcome.Id ?? step.Id), By = "far", Challenge = ++_challenge,
                    MinigameLimitSeconds = _share > 0 ? outcome.MinigameRealSeconds / _share : outcome.MinigameRealSeconds,
                });
            Probe(game);
        }

        public void Night(GameSession game, bool slept, int day, int minutes)
        {
            int elapsed = Elapsed(game, day, minutes);
            if (slept)
            {
                double? walk = _measured?.WalkSeconds(PlannedStep.SleepKind);
                Note(PlannedStep.SleepKind, walk.HasValue);
                if (walk.HasValue) Add(new PaceStep { Day = day, Minutes = minutes, Category = PaceCategories.Walk, Frames = Frames(walk.Value), Key = "walk:" + PlannedStep.SleepKind, By = "far", InteractionKind = PlannedStep.SleepKind });
                PaceMeasured.Screen screen = _measured?.ScreenFor(PlannedStep.SleepKind);
                Add(new PaceStep
                {
                    Day = day, Minutes = minutes, GameMinutes = elapsed, Category = PaceCategories.Sleep, Key = "sleep", By = "far", InteractionKind = PlannedStep.SleepKind,
                    NewChars = screen == null ? 0 : (int)Math.Round(screen.Chars), Choices = screen == null ? 0 : (int)Math.Round(screen.Choices),
                });
            }
            else Add(new PaceStep { Day = day, Minutes = minutes, GameMinutes = elapsed, Category = PaceCategories.NothingToDo, Key = "stay-up", By = "far", Frames = Frames(DayAssumptions.RealSeconds(game.TimeRules, elapsed)) });
            Probe(game);
        }

        private void Note(string kind, bool measured)
        {
            if (kind == null) return;
            (measured ? MeasuredKinds : AssumedKinds).Add(kind);
        }

        private static int Elapsed(GameSession game, int day, int minutes) => (game.Student.CurrentDay - day) * Rootborn.Core.Time.TimeRules.MinutesPerDay + game.Student.TimeMinutes - minutes;

        private static long Frames(double seconds) => (long)Math.Round(seconds / FarPace.SecondsPerFrame);

        private void Add(PaceStep s)
        {
            s.Index = Steps.Count;
            Steps.Add(s);
        }

        private void Probe(GameSession game)
        {
            _probe.Session = game;
            _probe.Definitions = TestWorld.Definitions;
            foreach (IPaceNoveltyKind kind in _kinds) kind.Collect(_probe, Steps[Steps.Count - 1].Novelties);
        }
    }

    /// <summary>먼 층의 판 하나: 전략, 대상, 균형 결과, 속도 걸음, 잰 값을 쓴 종류와 가정으로 메운 종류.</summary>
    internal sealed class FarRun
    {
        public StrategySpec Strategy;
        public string TargetId;
        public RunResult Result;
        public List<PaceStep> Steps;
        public SortedSet<string> MeasuredKinds, AssumedKinds;
        public double WallSeconds;
    }

    /// <summary>먼 층을 돌리고 보고서를 쓴다(전략마다 한 열, 전략의 첫 대상, 기준 가정).</summary>
    internal static class FarPace
    {
        /// <summary>먼 층 걸음의 초 ↔ "프레임"(가까운 층과 같은 1/60 — 분석 코드를 같이 쓰려고).</summary>
        public const double SecondsPerFrame = 1.0 / 60.0;
        public const string DaysVariable = "ROOTBORN_PACE_FAR_DAYS";
        public const int DefaultDays = 100;

        public static int Days(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DefaultDays;
            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int days) && days >= 1) return days;
            throw new ArgumentException(DaysVariable + "는 1 이상의 정수여야 한다: '" + value + "'");
        }

        /// <summary>잰 값 파일(ROOTBORN_PACE_MEASURED, 없으면 기본 자리). 없으면 null — 걷기는 가정, 글자·고르기는 0.</summary>
        public static PaceMeasured ReadMeasured() => PaceMeasured.Read(PaceMeasured.PathFromEnvironment(TestWorld.RepoRoot));

        public static FarRun Run(BalanceConfig config, StrategySpec strategy, int days, PaceMeasured measured)
        {
            object target = RunTargets.All(strategy.Each, TestWorld.Definitions).FirstOrDefault();
            RunSetup setup = RunSetup.Baseline(config); // 기준 가정(점수 "fixed 80" 포함) 한 판
            var recorder = new FarPaceRecorder(measured, setup.MinigameShare);
            setup.Observer = recorder;
            setup.TravelByKind = TravelByKind(measured);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            RunResult result = BalanceRunner.Run(strategy, target, setup, config, days);
            watch.Stop();
            return new FarRun
            {
                Strategy = strategy, TargetId = RunTargets.IdOf(target), Result = result, Steps = recorder.Steps,
                MeasuredKinds = recorder.MeasuredKinds, AssumedKinds = recorder.AssumedKinds, WallSeconds = watch.Elapsed.TotalSeconds,
            };
        }

        /// <summary>잰 걷기 초를 게임 분(올림)으로 — 잰 종류는 균형 시뮬레이터의 걷기 가정 대신 이 분만큼 시계를 보낸다.</summary>
        private static IReadOnlyDictionary<string, int> TravelByKind(PaceMeasured measured)
        {
            if (measured == null) return null;
            var rules = TestWorld.Definitions.TimeRules;
            return measured.WalkSecondsByKind.Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => DayAssumptions.GameMinutesUp(rules, (float)kv.Value.Value), StringComparer.Ordinal);
        }

        public static List<PaceCompareEntry> Entries(IReadOnlyList<FarRun> runs) => runs.Select(r => new PaceCompareEntry
        {
            Type = r.Strategy.Id,
            Label = r.Strategy.Id + (string.IsNullOrEmpty(r.TargetId) ? string.Empty : " (" + r.TargetId + ")"),
            Info = new PaceRunInfo { Name = r.Strategy.Id, Policy = r.Strategy.Id, DaysCompleted = r.Result.Days.Count, End = r.Result.Days.Count + "일", WallSeconds = r.WallSeconds },
            Steps = r.Steps,
            DailyIncome = r.Result.Days.Select(d => d.Earned).ToList(),
        }).ToList();

        public static List<string> Header(BalanceConfig config, IReadOnlyList<FarRun> runs, PaceMeasured measured)
        {
            var setup = RunSetup.Baseline(config);
            var lines = new List<string>
            {
                "> 자동 테스트(실제 플레이 아님), 사람 쪽 시간은 가정이다. 먼 층: 균형 시뮬레이터가 GameSession 명령으로 돈 판(화면·걷기 없음)에 속도 기록을 붙였다.",
                "",
                "- 판: 전략마다 첫 대상(id 순), 기준 가정(" + setup.Id + ").",
                "- 걷기: " + (measured == null ? "잰 값 파일이 없어 모두 가정(균형 시뮬레이터의 걷기 " + setup.TravelMinutes + "분 = 실제 " + DayAssumptions.RealSeconds(TestWorld.Definitions.TimeRules, setup.TravelMinutes).ToString("0.0", CultureInfo.InvariantCulture) + "초)"
                    : "가까운 층에서 잰 값(" + measured.Source + "). 잰 종류: " + Join(runs.SelectMany(r => r.MeasuredKinds)) + ". 가정으로 메운 종류(잰 값이 없다 — 균형 시뮬레이터의 걷기 " + setup.TravelMinutes + "분, 잠자리는 걷기 없음): " + Join(runs.SelectMany(r => r.AssumedKinds))),
                "- 읽기·고르기: 가까운 층에서 그 상호작용 종류의 화면이 낸 평균 새 글자·고르기(어림 — 먼 층에는 화면이 없다). 잰 값이 없는 종류는 0.",
                "- 미니게임: 균형 시뮬레이터가 쓴 실제 초 ÷ 그 판의 몫 = 제한 시간, 그 위에 축의 몫. 자지 않는 밤은 날이 넘어갈 때까지 기다린 실제 초(할 것 없음).",
                "- " + PaceReport.DayLengthNote,
            };
            return lines;
        }

        private static string Join(IEnumerable<string> kinds)
        {
            var list = kinds.Distinct().OrderBy(k => k, StringComparer.Ordinal).ToList();
            return list.Count == 0 ? "(없음)" : string.Join(", ", list);
        }

        public static string Write(string root, string name, BalanceConfig config, IReadOnlyList<FarRun> runs, PaceMeasured measured)
        {
            string dir = Path.Combine(root, name);
            PaceCompare.Write(dir, name, Entries(runs), SecondsPerFrame, Header(config, runs, measured), "먼 층 전략 비교 ");
            return dir;
        }

        public static string RunAndWrite(string name, BalanceConfig config, int days, PaceMeasured measured, out List<FarRun> runs)
        {
            runs = config.Strategies.Select(s => Run(config, s, days, measured)).ToList();
            return Write(PaceRoot(), "far-" + name.Replace("far-", string.Empty), config, runs, measured);
        }

        /// <summary>
        /// 덮어쓰기 전후(먼 층): 같은 전략·날 수를 data/ 그대로와 덮어쓴 것(TestWorld.UseOverlay) 둘 다 돌려 far-…/report.md 하나에 전·후·차이로 쓴다.
        /// </summary>
        public static string CompareOverlay(string name, BalanceConfig config, int days, PaceMeasured measured, string overlay)
        {
            List<FarRun> before = config.Strategies.Select(s => Run(config, s, days, measured)).ToList();
            List<FarRun> after;
            List<string> changes;
            using (TestWorld.UseOverlay(overlay))
            {
                after = config.Strategies.Select(s => Run(config, s, days, measured)).ToList();
                changes = TestWorld.OverlayChanges.Select(c => c.ToString()).ToList();
            }
            string dir = Path.Combine(PaceRoot(), name.StartsWith("far-", StringComparison.Ordinal) ? name : "far-" + name);
            var header = Header(config, before, measured);
            header.Add("- 날 " + days + "일.");
            PaceDiff.Write(dir, name, changes, Entries(before), Entries(after), SecondsPerFrame, header);
            return dir;
        }

        private static string PaceRoot() => Path.Combine(TestWorld.RepoRoot, "output", "simulation", "pace");
    }
}
