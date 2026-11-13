using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 훑기의 파일 둘(설계 13.3절): sweep.csv(판 × 시점마다 한 줄), sweep-report.md(사람이 읽는 한글 표).
    /// 같은 입력이면 바이트까지 같다: 날짜·시각을 적지 않고, 수는 InvariantCulture, 줄 끝은 "\n", 순서는 받은 줄의 순서(계획 × 시점).
    /// 표를 만들 때 전략 id·대상 종류 이름으로 가르지 않는다 — 전략이 어느 축을 쓰는지는 그 전략의 줄에 그 축의 다른 값이 있는지로,
    /// 대상끼리 다른지는 값으로 안다. "같은 값"은 표에 보이는 글자(소수 한 자리)가 같은 것이다.
    /// </summary>
    internal static class SweepReport
    {
        public const string Folder = "sweep", CsvFile = "sweep.csv", ReportFile = "sweep-report.md";
        public const string FailuresHeading = "## 실패한 판";
        public const string SleepHeading = "## 1. 자는 때";
        public const string BestPerTravelHeading = "### 걷는 분마다 가장 많이 버는 잠 규칙";
        public const string BaselineMark = "(기준)";
        public const string TiedLine = "같은 값";
        public const string DifferentBestLine = "대상마다 1등이 다른 것:";
        public const string TopHeadingPrefix = "많이 버는";
        private const int TopCount = 20, SpreadCount = 10, FailuresShown = 200;
        private const string SleepAxisName = "자는 때", TravelAxisName = "걷는 분";
        private const double NearBestShare = 0.99;   // 걷는 분마다 1등 표: 1등의 99% 이상이면 "근소"로 함께 적는다

        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>&lt;균형 보고 폴더&gt;/sweep (ROOTBORN_BALANCE_OUT, 없으면 &lt;저장소&gt;/output/balance).</summary>
        public static string OutputDirectory() => Path.Combine(BalanceReport.OutputDirectory(), Folder);

        public static void Write(string directory, BalanceConfig config, IReadOnlyList<SweepRow> rows)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, CsvFile), Csv(rows), Utf8);
            File.WriteAllText(Path.Combine(directory, ReportFile), Markdown(config, rows), Utf8);
        }

        // ---- sweep.csv ----

        private static string Csv(IReadOnlyList<SweepRow> rows)
        {
            var b = new StringBuilder(rows.Count * 170);
            BalanceReport.Row(b, "strategy", "target", "sleep", "score", "part_time", "travel_minutes", "minigame_share", "checkpoint_day", "is_baseline", "failed",
                "income_per_day", "net_income_per_day", "income_per_game_hour", "income_per_real_minute_with_travel", "income_per_real_minute_minigame_only",
                "level2_day", "level3_day", "recover_days", "payback_days", "payback_gaps", "first_auto_skip_day", "first_bag_full_day", "actions_per_day",
                "exhaustion_seen_days", "stopped_exhausted_days", "zero_action_days", "refused_after_day_end", "nights_awake", "average_energy_at_end", "average_slept_at_minutes", "final_wallet");
            foreach (var r in rows)
            {
                var s = r.Summary;
                bool ok = r.Failure == null;
                BalanceReport.Row(b, r.StrategyId, r.TargetId, r.Setup.Sleep, r.Setup.Score, r.Setup.PartTime, I(r.Setup.TravelMinutes), F(r.Setup.MinigameShare),
                    I(r.Checkpoint), r.IsBaseline ? "true" : "false", r.Failure ?? "",
                    ok ? F(s.IncomePerDay) : "", ok ? F(s.NetIncomePerDay) : "", ok ? F(s.IncomePerGameHour) : "", ok ? F(s.IncomePerRealMinuteWithTravel) : "",
                    ok ? F(s.IncomePerRealMinuteMinigameOnly) : "",
                    ok ? I(s.Level2Day) : "", ok ? I(s.Level3Day) : "", ok ? string.Join(";", s.RecoverDays.Select(I)) : "", ok ? string.Join(";", s.PaybackDays.Select(I)) : "",
                    ok ? string.Join(";", s.PaybackGaps.Select(g => g ?? "")) : "", ok ? I(s.FirstAutoSkipDay) : "", ok ? I(s.FirstBagFullDay) : "", ok ? F(s.ActionsPerDay) : "",
                    ok ? I(s.ExhaustionSeenDays) : "", ok ? I(s.StoppedExhaustedDays) : "", ok ? I(s.ZeroActionDays) : "", ok ? I(s.RefusedAfterDayEnd) : "", ok ? I(s.NightsAwake) : "",
                    ok ? F(s.AverageEnergyAtEnd) : "", ok ? F(s.AverageSleptAtMinutes) : "", ok ? I(s.FinalWallet) : "");
            }
            return b.ToString();
        }

        // ---- sweep-report.md ----

        // 축 하나: 표에 쓰는 이름, 값의 글자(설정 순서), 가정에서 그 축의 글자, 가정의 사본에 i째 값을 넣기.
        private sealed class Axis
        {
            public string Name;
            public string[] Labels;
            public Func<RunSetup, string> Of;
            public Action<RunSetup, int> Set;
        }

        private static List<Axis> AxesOf(BalanceConfig config)
        {
            var a = config.Axes;
            return new List<Axis>
            {
                new Axis { Name = SleepAxisName, Labels = a.Sleep, Of = s => s.Sleep, Set = (s, i) => s.Sleep = a.Sleep[i] },
                new Axis { Name = "점수", Labels = a.Score, Of = s => s.Score, Set = (s, i) => s.Score = a.Score[i] },
                new Axis { Name = "알바 성공", Labels = a.PartTime, Of = s => s.PartTime, Set = (s, i) => s.PartTime = a.PartTime[i] },
                new Axis { Name = TravelAxisName, Labels = a.TravelMinutes.Select(I).ToArray(), Of = s => I(s.TravelMinutes), Set = (s, i) => s.TravelMinutes = a.TravelMinutes[i] },
                new Axis { Name = "미니게임 몫", Labels = a.MinigameShare.Select(F).ToArray(), Of = s => F(s.MinigameShare), Set = (s, i) => s.MinigameShare = a.MinigameShare[i] },
            };
        }

        // (전략, 가정, 시점) → 그 판들의 줄(대상마다 하나, 받은 순서).
        private sealed class Index
        {
            private readonly Dictionary<string, List<SweepRow>> _rows = new Dictionary<string, List<SweepRow>>(StringComparer.Ordinal);

            public Index(IReadOnlyList<SweepRow> rows)
            {
                foreach (var r in rows)
                {
                    var key = Key(r.StrategyId, r.Setup, r.Checkpoint);
                    if (!_rows.TryGetValue(key, out var list)) _rows[key] = list = new List<SweepRow>();
                    list.Add(r);
                }
            }

            public List<SweepRow> Get(string strategyId, RunSetup setup, int checkpoint)
                => _rows.TryGetValue(Key(strategyId, setup, checkpoint), out var list) ? list : null;

            private static string Key(string strategyId, RunSetup setup, int checkpoint) => strategyId + "\n" + setup.Id + "\n" + I(checkpoint);
        }

        private static string Markdown(BalanceConfig config, IReadOnlyList<SweepRow> rows)
        {
            var a = config.Axes;
            var baseline = config.BaselineSetup;
            var axes = AxesOf(config);
            var index = new Index(rows);
            int last = a.Checkpoints[a.Checkpoints.Length - 1];
            int mid = Array.IndexOf(a.Checkpoints, config.Days) >= 0 ? config.Days : last;   // 축마다·벌어짐 표의 시점: 1단계 기본 날 수, 없으면 마지막
            int runs = a.Checkpoints.Length == 0 ? 0 : rows.Count / a.Checkpoints.Length;

            var b = new StringBuilder();
            b.Append("# 수치 균형 시뮬레이터 — 모든 경우 훑기\n\n");
            BalanceReport.AppendOverlay(b);

            // 실패한 판(맨 위): 판마다 한 번(그 판의 첫 실패 줄).
            var failed = new List<SweepRow>();
            var failedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in rows)
                if (r.Failure != null && failedKeys.Add(r.StrategyId + "\n" + r.TargetId + "\n" + r.Setup.Id)) failed.Add(r);
            if (failed.Count > 0)
            {
                b.Append(FailuresHeading).Append("\n\n");
                b.Append("판 ").Append(I(failed.Count)).Append("개가 끝까지 돌지 못했다. 그 판의 줄은 표에서 빼고 sweep.csv의 `failed` 칸에 까닭을 적었다.\n\n");
                foreach (var r in failed.Take(FailuresShown))
                    b.Append("- ").Append(r.StrategyId).Append(" / ").Append(Name(r.TargetId)).Append(" / ").Append(r.Setup.Id).Append(" — ").Append(r.Failure.Replace("\n", " ")).Append('\n');
                if (failed.Count > FailuresShown) b.Append("- 외 ").Append(I(failed.Count - FailuresShown)).Append("개(sweep.csv)\n");
                b.Append('\n');
            }

            b.Append("가정: 날 수 ").Append(I(a.Days)).Append(", 시점 ").Append(string.Join("·", a.Checkpoints.Select(I))).Append("일")
                .Append(", fail-every 점수 모델의 실패 점수 ").Append(I(a.FailScore)).Append("·성공 점수 ").Append(I(a.PassScore))
                .Append(". 기준 값: ").Append(string.Join(", ", axes.Select(x => x.Name + " " + x.Of(baseline)))).Append(".\n\n");
            b.Append("판 ").Append(I(runs)).Append("개(전략 ").Append(I(config.Strategies.Count)).Append("개), 줄 ").Append(I(rows.Count)).Append("개.\n\n");
            b.Append(BalanceReport.NotPlayLine).Append("\n\n");
            b.Append("축(설정 순서):\n\n");
            foreach (var x in axes) b.Append("- ").Append(x.Name).Append(": ").Append(string.Join(", ", x.Labels.Select(l => "`" + l + "`"))).Append('\n');
            b.Append("- 전략: ").Append(string.Join(", ", config.Strategies.Select(s => "`" + s.Id + "`"))).Append('\n');
            b.Append("\n범례:\n\n")
                .Append("- 하루 수입은 그 시점까지 번 돈 합 ÷ 날 수(쓴 돈은 빼지 않는다, 시작 지갑도 뺀다). 하루 순수입은 (번 돈 − 쓴 돈) ÷ 날 수. 대상이 여럿인 전략은 대상(id 순)의 평균이다.\n")
                .Append("- 점수를 쓰는 전략은 점수 축을, 아닌 전략은 알바 성공 축을 돈다(쓰지 않는 축은 기준 값). 축마다 표는 그 축을 쓰는 전략만 보인다.\n")
                .Append("- 지침 본 날: 그날 지침(체력 0)을 한 번이라도 본 날(마지막 일 도중에 지쳐도 그 일은 끝나 보수를 받으므로 손해가 없을 수 있다). ")
                .Append("지쳐서 멈춘 날: 자는 밤에 지쳐 그 자리에서 잔 날. 행동 0인 날: 하나도 못 한 날(하루를 잃은 날). 밤샌 날: 자지 않은 밤. 평균 잔 시각은 잔 날만(자정 뒤는 24:00을 넘겨 센다).\n")
                .Append("- 자지 않는 밤의 두 방식: 글자에 `carry`가 없으면 07:00 전에 끝나는 행동만 시작한다. `carry`가 있으면 끝은 07:00을 넘어도 되지만, ")
                .Append("걷기 + (미니게임이 있으면) 미니게임을 하는 분(올림)이 07:00 1분 전보다 일찍 끝날 때만 시작한다(걷기 조각 1분 여유) — 미니게임 도중에 07:00이 되면 게임이 그 자리에서 날을 넘기고 정산을 거절하기 때문이다(보수 없음). ")
                .Append("정산만 07:00을 넘기는 것은 된다: 게임은 넘긴 시간을 다음 날 아침으로 잇고(다음 날은 07:00 + 넘긴 분에 시작), 그 행동의 보수·분은 시작한 날의 줄에 적는다. ")
                .Append("그래도 날 넘김 뒤 정산이 거절된 행동 수는 sweep.csv `refused_after_day_end`에 센다.\n")
                .Append("- `stay-up-every N until HH:MM`: N일에 하루 밤새고, 다른 밤은 그 시각까지 끝내고 잔다(없으면 취침 시각 22:00).\n")
                .Append("- 점수·성공 모델의 차례는 정산을 시도할 때마다 넘어간다(정산이 거절돼도).\n")
                .Append("- 자지 않는 밤은 다음 날을 시작할 때의 지갑 변화가 그 밤의 날에 들어간다(자는 밤은 다음 날에 들어간다). 넘겨 잇는 밤에 행동 도중 날이 넘어가면 그날의 체력 칸은 넘어간 뒤의 값이다.\n")
                .Append("- 날 넘김을 기다리는 동안(자지 않는 밤) 지치면 다음 날의 지침 본 날로 센다.\n")
                .Append("- 행동 길이는 정의의 분과 미니게임을 하는 실제 시간(게임 분으로 올림) 중 큰 것이다. 자는 규칙은 그 길이와 걷는 분이 규칙 시각 안에 끝나야 시작한다.\n")
                .Append("- 굵게: 그 표에서 하루 수입(마지막 시점)이 가장 큰 줄 — 같은 값이면 모두 굵게 하고 표 아래에 줄 수를 적는다. ")
                .Append("\"같은 값\"은 표에 보이는 소수 한 자리 글자가 같은 것이다. 위·아래 조합과 벌어짐 표는 요약(표에 보이는 값)이 같은 판을 한 줄로 묶는다.\n");

            // 1. 자는 때
            var sleepAxis = axes.Single(x => x.Name == SleepAxisName);
            b.Append('\n').Append(SleepHeading).Append("\n\n");
            b.Append("전략마다: 줄 = 잠 규칙, 다른 축은 기준 값. ").Append(I(last)).Append("일 하루 수입이 가장 큰 줄을 굵게(같은 값은 모두), 기준 줄에 ").Append(BaselineMark).Append(".\n");
            foreach (var strategy in config.Strategies)
            {
                b.Append("\n### ").Append(strategy.Id).Append("\n\n");
                if (!string.IsNullOrEmpty(strategy.Note)) b.Append(strategy.Note).Append("\n\n");
                var meanBest = SleepTable(b, config, index, strategy, null, last);

                // 대상마다: 대상끼리 하루 수입이 다르면 대상별 표, 평균의 1등과 겹치지 않는 1등인 대상은 적는다.
                var targets = TargetsOf(rows, strategy.Id);
                if (targets.Count < 2) continue;
                var differing = new List<string>();
                foreach (var target in targets)
                {
                    var best = BestSleep(config, index, strategy, target, last, baseline, out string value);
                    if (best.Count > 0 && !best.Intersect(meanBest).Any())
                        differing.Add("- " + DifferentBestLine + " " + target + " → " + string.Join("·", best) + " (" + value + ")");
                }
                if (differing.Count > 0)
                {
                    b.Append('\n');
                    foreach (var line in differing) b.Append(line).Append('\n');
                }
                if (TargetsDiffer(config, index, strategy, targets, last, baseline))
                    foreach (var target in targets)
                    {
                        b.Append("\n#### ").Append(strategy.Id).Append(" / ").Append(target).Append("\n\n");
                        SleepTable(b, config, index, strategy, target, last);
                    }
            }

            // 1-2. 전략 × 걷는 분마다 1등 잠 규칙(점수·성공·몫은 기준 값)
            b.Append('\n').Append(BestPerTravelHeading).Append("\n\n");
            b.Append("전략 × 걷는 분마다 ").Append(I(last)).Append("일 하루 수입이 가장 큰 잠 규칙(같은 값은 모두)과 그 값, 기준 잠 규칙(").Append(baseline.Sleep).Append(")의 값. 대상 평균.\n\n");
            b.Append("점수·성공·몫은 기준 값(").Append(baseline.Score).Append(", ").Append(baseline.PartTime).Append(", ").Append(F(baseline.MinigameShare))
                .Append(")이다. \"근소\"는 1등은 아니지만 1등 값의 ").Append(((1 - NearBestShare) * 100).ToString("0", CultureInfo.InvariantCulture)).Append("% 안으로 붙은 규칙과 그 값.\n\n");
            var travelAxis = axes.Single(x => x.Name == TravelAxisName);
            var perTravel = new List<List<string>>();
            foreach (var strategy in config.Strategies)
                for (int t = 0; t < travelAxis.Labels.Length; t++)
                {
                    var setup = baseline.Copy();
                    travelAxis.Set(setup, t);
                    Func<int, float?> measure = i => Mean(index.Get(strategy.Id, WithSleep(setup, sleepAxis, i), last), s => s.IncomePerDay);
                    var best = BestOver(sleepAxis, setup, measure, out string value);
                    var near = new List<string>();
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double top) && top > 0)
                        for (int i = 0; i < sleepAxis.Labels.Length; i++)
                        {
                            var v = measure(i);
                            if (v.HasValue && F1(v) != value && v.Value >= top * NearBestShare) near.Add(sleepAxis.Labels[i] + " " + F1(v));
                        }
                    var atBaseline = setup.Copy();
                    atBaseline.Sleep = baseline.Sleep;
                    perTravel.Add(new List<string>
                    {
                        strategy.Id, travelAxis.Labels[t], best.Count == 0 ? "-" : string.Join(", ", best), value,
                        near.Count == 0 ? "-" : string.Join(", ", near),
                        F1(Mean(index.Get(strategy.Id, atBaseline, last), s => s.IncomePerDay)),
                    });
                }
            Table(b, new List<string> { "전략", "걷는 분", "가장 많이 버는 잠 규칙", "하루 수입(" + I(last) + "일)", "근소", "기준 " + baseline.Sleep }, perTravel);

            // 2. 축마다
            b.Append("\n## 2. 축마다\n\n");
            b.Append("그 축만 바꾸고 다른 축은 기준 값. 칸은 전략별 ").Append(I(mid)).Append("일 하루 수입(대상 평균). 그 축을 쓰지 않는 전략은 뺀다.\n");
            foreach (var axis in axes.Where(x => x != sleepAxis))
            {
                b.Append("\n### ").Append(axis.Name).Append("\n\n");
                var users = config.Strategies.Where(s => Uses(index, s, axis, baseline, mid)).ToList();
                if (users.Count == 0)
                {
                    b.Append("이 축을 쓰는 전략이 없다.\n");
                    continue;
                }
                var header = new List<string> { axis.Name };
                header.AddRange(users.Select(s => s.Id));
                var lines = new List<List<string>>();
                for (int i = 0; i < axis.Labels.Length; i++)
                {
                    var setup = baseline.Copy();
                    axis.Set(setup, i);
                    var cells = new List<string> { axis.Labels[i] + (axis.Labels[i] == axis.Of(baseline) ? " " + BaselineMark : "") };
                    foreach (var s in users) cells.Add(Cell(Mean(index.Get(s.Id, setup, mid), x => x.IncomePerDay), index.Get(s.Id, setup, mid)));
                    lines.Add(cells);
                }
                Table(b, header, lines);
            }

            // 3. 가장 많이·적게 버는 조합(요약이 같은 판은 한 줄로)
            var finals = new List<(SweepRow Row, int Order)>();
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Checkpoint == last && rows[i].Failure == null) finals.Add((rows[i], i));
            var groups = GroupBySummary(finals);
            b.Append("\n## 3. 가장 많이·적게 버는 조합\n\n");
            b.Append(I(last)).Append("일 하루 수입으로 줄 세웠다(같으면 설정 순서). 모든 축과 전략·대상. 같은 전략에서 요약 값이 같은 판은 한 줄로 묶고, 묶인 판 수와 판마다 달랐던 축을 적는다(`여럿(n)`은 n개 값).\n");
            b.Append("\n### ").Append(TopHeadingPrefix).Append(' ').Append(I(TopCount)).Append("\n\n");
            Table(b, ComboHeader(last), groups.OrderByDescending(g => g.Rows[0].Summary.IncomePerDay).ThenBy(g => g.Order).Take(TopCount).Select((g, i) => ComboCells(i + 1, g.Rows)).ToList());
            b.Append("\n### 적게 버는 ").Append(I(TopCount)).Append("\n\n");
            Table(b, ComboHeader(last), groups.OrderBy(g => g.Rows[0].Summary.IncomePerDay).ThenBy(g => g.Order).Take(TopCount).Select((g, i) => ComboCells(i + 1, g.Rows)).ToList());

            // 4. 대상끼리 가장 벌어지는 조합
            b.Append("\n## 4. 직업끼리 가장 벌어지는 조합\n\n");
            b.Append("같은 전략·같은 가정에서 대상(시설·알바)끼리 ").Append(I(mid)).Append("일 실제 분당(걷기 포함) 수입의 가장 큰 것 ÷ 가장 작은 것. 위 ")
                .Append(I(SpreadCount)).Append(". 실제 분당(걷기 포함)은 번 것 ÷ 걷기와 미니게임에 쓴 실제 분. 같은 전략에서 결과(두 대상과 두 값)가 같은 가정은 한 줄로 묶는다.\n\n");
            var spreads = new List<(SweepRow Hi, SweepRow Lo, float Ratio, int Order)>();
            int skipped = 0;
            var seen = new Dictionary<string, List<SweepRow>>(StringComparer.Ordinal);
            var orderOf = new List<string>();
            foreach (var r in rows)
            {
                if (r.Checkpoint != mid || r.Failure != null) continue;
                string key = r.StrategyId + "\n" + r.Setup.Id;
                if (!seen.TryGetValue(key, out var list)) { seen[key] = list = new List<SweepRow>(); orderOf.Add(key); }
                list.Add(r);
            }
            for (int g = 0; g < orderOf.Count; g++)
            {
                var list = seen[orderOf[g]];
                if (list.Count < 2) continue;
                if (list.Any(r => !r.Summary.IncomePerRealMinuteWithTravel.HasValue || r.Summary.IncomePerRealMinuteWithTravel.Value <= 0f)) { skipped++; continue; }
                SweepRow hi = list[0], lo = list[0];
                foreach (var r in list)
                {
                    if (r.Summary.IncomePerRealMinuteWithTravel.Value > hi.Summary.IncomePerRealMinuteWithTravel.Value) hi = r;
                    if (r.Summary.IncomePerRealMinuteWithTravel.Value < lo.Summary.IncomePerRealMinuteWithTravel.Value) lo = r;
                }
                spreads.Add((hi, lo, hi.Summary.IncomePerRealMinuteWithTravel.Value / lo.Summary.IncomePerRealMinuteWithTravel.Value, g));
            }
            var spreadGroups = new Dictionary<string, List<(SweepRow Hi, SweepRow Lo, float Ratio, int Order)>>(StringComparer.Ordinal);
            var spreadOrder = new List<string>();
            foreach (var x in spreads.OrderByDescending(x => x.Ratio).ThenBy(x => x.Order))
            {
                string key = x.Hi.StrategyId + "\n" + x.Hi.TargetId + "\n" + F1(x.Hi.Summary.IncomePerRealMinuteWithTravel) + "\n" + x.Lo.TargetId + "\n" + F1(x.Lo.Summary.IncomePerRealMinuteWithTravel);
                if (!spreadGroups.TryGetValue(key, out var list)) { spreadGroups[key] = list = new List<(SweepRow, SweepRow, float, int)>(); spreadOrder.Add(key); }
                list.Add(x);
            }
            if (spreadOrder.Count == 0) b.Append("견줄 묶음이 없다(대상이 둘 이상인 전략이 없거나 값이 없다).\n");
            else
                Table(b, new List<string> { "전략", "자는 때", "점수", "알바 성공", "걷는 분", "몫", "가장 큰 대상", "실제 분당", "가장 작은 대상", "실제 분당", "배", "같은 결과", "달라진 축" },
                    spreadOrder.Take(SpreadCount).Select(key =>
                    {
                        var list = spreadGroups[key];
                        var x = list[0];
                        var setups = list.Select(y => y.Hi.Setup).ToList();
                        var cells = new List<string> { x.Hi.StrategyId };
                        var changed = new List<string>();
                        foreach (var axis in axes)
                        {
                            var values = setups.Select(axis.Of).Distinct().ToList();
                            cells.Add(values.Count == 1 ? values[0] : "여럿(" + I(values.Count) + ")");
                            if (values.Count > 1) changed.Add(axis.Name);
                        }
                        cells.Add(Name(x.Hi.TargetId));
                        cells.Add(F1(x.Hi.Summary.IncomePerRealMinuteWithTravel));
                        cells.Add(Name(x.Lo.TargetId));
                        cells.Add(F1(x.Lo.Summary.IncomePerRealMinuteWithTravel));
                        cells.Add(x.Ratio.ToString("0.00", CultureInfo.InvariantCulture));
                        cells.Add(I(list.Count));
                        cells.Add(changed.Count == 0 ? "-" : string.Join(", ", changed));
                        return cells;
                    }).ToList());
            if (skipped > 0) b.Append("\n어느 대상의 값이 0이거나 없어(번 것이 없거나 실제 시간을 쓰지 않음) 비를 셀 수 없어 뺀 가정: ").Append(I(skipped)).Append("개.\n");
            return b.ToString();
        }

        // 자는 때 표 하나(target이 null이면 대상 평균). 굵게 한 1등 잠 규칙들을 돌려준다.
        private static List<string> SleepTable(StringBuilder b, BalanceConfig config, Index index, StrategySpec strategy, string target, int last)
        {
            var a = config.Axes;
            var baseline = config.BaselineSetup;
            var header = new List<string> { "자는 때" };
            header.AddRange(a.Checkpoints.Select(c => "하루 수입 " + I(c) + "일"));
            header.Add("하루 순수입(" + I(last) + "일)");
            header.Add("하루 행동 수(" + I(last) + "일)");
            header.Add("지침 본 날");
            header.Add("지쳐서 멈춘 날");
            header.Add("행동 0인 날");
            header.Add("밤샌 날");
            header.Add("평균 잔 시각");

            var lines = new List<List<string>>();
            var lastIncome = new List<string>();
            for (int i = 0; i < a.Sleep.Length; i++)
            {
                var setup = baseline.Copy();
                setup.Sleep = a.Sleep[i];
                var cells = new List<string> { a.Sleep[i] };
                foreach (var c in a.Checkpoints)
                {
                    var at = Of(index.Get(strategy.Id, setup, c), target);
                    cells.Add(Cell(Mean(at, s => s.IncomePerDay), at));
                }
                var atLast = Of(index.Get(strategy.Id, setup, last), target);
                cells.Add(F1(Mean(atLast, s => s.NetIncomePerDay)));
                cells.Add(F1(Mean(atLast, s => s.ActionsPerDay)));
                cells.Add(F1(Mean(atLast, s => s.ExhaustionSeenDays)));
                cells.Add(F1(Mean(atLast, s => s.StoppedExhaustedDays)));
                cells.Add(F1(Mean(atLast, s => s.ZeroActionDays)));
                cells.Add(F1(Mean(atLast, s => s.NightsAwake)));
                cells.Add(Clock(Mean(atLast, s => s.AverageSleptAtMinutes)));
                lines.Add(cells);
                lastIncome.Add(F1(Mean(atLast, s => s.IncomePerDay)));
            }

            var bestValue = BestText(lastIncome);
            var best = new List<string>();
            int lastColumn = a.Checkpoints.Length;   // 마지막 시점의 하루 수입 칸
            for (int i = 0; i < lines.Count; i++)
            {
                if (bestValue != null && lastIncome[i] == bestValue)
                {
                    best.Add(a.Sleep[i]);
                    lines[i][0] = "**" + lines[i][0] + "**";
                    lines[i][lastColumn] = "**" + lines[i][lastColumn] + "**";
                }
                if (a.Sleep[i] == baseline.Sleep) lines[i][0] += " " + BaselineMark;
            }
            Table(b, header, lines);
            if (best.Count > 1) b.Append('\n').Append(TiedLine).Append(' ').Append(I(best.Count)).Append("줄(굵게): ").Append(string.Join(", ", best)).Append(".\n");
            return best;
        }

        // 그 대상(null이면 평균)의 1등 잠 규칙들(같은 값 모두)과 그 값.
        private static List<string> BestSleep(BalanceConfig config, Index index, StrategySpec strategy, string target, int last, RunSetup baseline, out string value)
        {
            var texts = new List<string>();
            foreach (var sleep in config.Axes.Sleep)
            {
                var setup = baseline.Copy();
                setup.Sleep = sleep;
                texts.Add(F1(Mean(Of(index.Get(strategy.Id, setup, last), target), s => s.IncomePerDay)));
            }
            value = BestText(texts) ?? "-";
            var best = new List<string>();
            for (int i = 0; i < texts.Count; i++) if (texts[i] == value) best.Add(config.Axes.Sleep[i]);
            return best;
        }

        // 축의 값마다 값을 재고 가장 큰 것들(같은 글자 모두).
        private static List<string> BestOver(Axis axis, RunSetup setup, Func<int, float?> measure, out string value)
        {
            var texts = Enumerable.Range(0, axis.Labels.Length).Select(i => F1(measure(i))).ToList();
            value = BestText(texts) ?? "-";
            var best = new List<string>();
            for (int i = 0; i < texts.Count; i++) if (texts[i] == value) best.Add(axis.Labels[i]);
            return best;
        }

        private static RunSetup WithSleep(RunSetup setup, Axis sleepAxis, int i)
        {
            var copy = setup.Copy();
            sleepAxis.Set(copy, i);
            return copy;
        }

        // 표에 보이는 글자 중 가장 큰 수의 글자("-"는 뺀다). 없으면 null.
        private static string BestText(List<string> texts)
        {
            string best = null;
            double top = double.NegativeInfinity;
            foreach (var t in texts)
            {
                if (t == "-" || !double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) continue;
                if (v > top) { top = v; best = t; }
            }
            return best;
        }

        private static List<string> TargetsOf(IReadOnlyList<SweepRow> rows, string strategyId)
        {
            var targets = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in rows) if (r.StrategyId == strategyId && seen.Add(r.TargetId)) targets.Add(r.TargetId);
            return targets;
        }

        // 대상끼리 마지막 시점 하루 수입(표의 글자)이 다른 잠 규칙이 하나라도 있는가(다른 축은 기준 값).
        private static bool TargetsDiffer(BalanceConfig config, Index index, StrategySpec strategy, List<string> targets, int last, RunSetup baseline)
        {
            foreach (var sleep in config.Axes.Sleep)
            {
                var setup = baseline.Copy();
                setup.Sleep = sleep;
                var list = index.Get(strategy.Id, setup, last);
                if (list == null) continue;
                if (targets.Select(t => F1(Mean(Of(list, t), s => s.IncomePerDay))).Distinct().Count() > 1) return true;
            }
            return false;
        }

        private static List<SweepRow> Of(List<SweepRow> rows, string target)
            => rows == null || target == null ? rows : rows.Where(r => r.TargetId == target).ToList();

        // 같은 전략에서 요약(표에 보이는 값)이 같은 판 묶음. 순서는 묶음의 첫 판.
        private sealed class Group
        {
            public List<SweepRow> Rows = new List<SweepRow>();
            public int Order;
        }

        private static List<Group> GroupBySummary(List<(SweepRow Row, int Order)> finals)
        {
            var groups = new Dictionary<string, Group>(StringComparer.Ordinal);
            var list = new List<Group>();
            foreach (var (row, order) in finals)
            {
                var s = row.Summary;
                string key = row.StrategyId + "\n" + F1(s.IncomePerDay) + "\n" + F1(s.NetIncomePerDay) + "\n" + I(s.ExhaustionSeenDays) + "\n" + I(s.StoppedExhaustedDays)
                    + "\n" + I(s.ZeroActionDays) + "\n" + I(s.NightsAwake);
                if (!groups.TryGetValue(key, out var g)) { groups[key] = g = new Group { Order = order }; list.Add(g); }
                g.Rows.Add(row);
            }
            return list;
        }

        // 그 전략이 이 축을 쓰는가: 기준 값이 아닌 값의 판이 있다(축 값이 하나뿐이면 모두 쓴다고 본다).
        private static bool Uses(Index index, StrategySpec strategy, Axis axis, RunSetup baseline, int checkpoint)
        {
            if (axis.Labels.Length == 1) return true;
            for (int i = 0; i < axis.Labels.Length; i++)
            {
                if (axis.Labels[i] == axis.Of(baseline)) continue;
                var setup = baseline.Copy();
                axis.Set(setup, i);
                if (index.Get(strategy.Id, setup, checkpoint) != null) return true;
            }
            return false;
        }

        private static List<string> ComboHeader(int last)
            => new List<string>
            {
                "순위", "전략", "대상", "자는 때", "점수", "알바 성공", "걷는 분", "몫", "하루 수입(" + I(last) + "일)", "하루 순수입", "지침 본 날", "지쳐서 멈춘 날", "행동 0인 날", "밤샌 날",
                "같은 결과", "달라진 축",
            };

        // 묶음의 첫 판의 값. 판마다 달랐던 축은 `여럿(n)`.
        private static List<string> ComboCells(int rank, List<SweepRow> group)
        {
            var r = group[0];
            var cells = new List<string> { I(rank), r.StrategyId };
            var changed = new List<string>();
            void Column(string name, Func<SweepRow, string> of)
            {
                var values = group.Select(of).Distinct().ToList();
                cells.Add(values.Count == 1 ? values[0] : "여럿(" + I(values.Count) + ")");
                if (values.Count > 1) changed.Add(name);
            }
            Column("대상", x => Name(x.TargetId));
            Column("자는 때", x => x.Setup.Sleep);
            Column("점수", x => x.Setup.Score);
            Column("알바 성공", x => x.Setup.PartTime);
            Column("걷는 분", x => I(x.Setup.TravelMinutes));
            Column("몫", x => F(x.Setup.MinigameShare));
            var s = r.Summary;
            cells.Add(F1(s.IncomePerDay));
            cells.Add(F1(s.NetIncomePerDay));
            cells.Add(I(s.ExhaustionSeenDays));
            cells.Add(I(s.StoppedExhaustedDays));
            cells.Add(I(s.ZeroActionDays));
            cells.Add(I(s.NightsAwake));
            cells.Add(I(group.Count));
            cells.Add(changed.Count == 0 ? "-" : string.Join(", ", changed));
            return cells;
        }

        // 끝까지 돈 판들의 평균(받은 순서로 더한다). 값이 없는 판은 뺀다. 판이 없으면 null.
        private static float? Mean(List<SweepRow> rows, Func<RunSummary, float?> value)
        {
            if (rows == null) return null;
            double sum = 0;
            int count = 0;
            foreach (var r in rows)
            {
                if (r.Failure != null) continue;
                var v = value(r.Summary);
                if (!v.HasValue) continue;
                sum += v.Value;
                count++;
            }
            return count == 0 ? (float?)null : (float)(sum / count);
        }

        private static string Cell(float? mean, List<SweepRow> rows)
        {
            int failedCount = rows?.Count(r => r.Failure != null) ?? 0;
            return F1(mean) + (failedCount > 0 ? " (실패 " + I(failedCount) + ")" : "");
        }

        // 그날 0시부터 센 분 → HH:MM(자정 뒤는 24:00을 넘겨 적는다, 예: 26:00).
        private static string Clock(float? minutes)
        {
            if (!minutes.HasValue) return "-";
            int m = (int)Math.Round(minutes.Value);
            return (m / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (m % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static void Table(StringBuilder b, List<string> header, List<List<string>> lines)
        {
            BalanceReport.MdRow(b, header);
            BalanceReport.MdRow(b, header.Select(_ => "---").ToList());
            foreach (var line in lines) BalanceReport.MdRow(b, line);
        }

        private static string Name(string targetId) => targetId == "" ? "(한 판)" : targetId;
        private static string I(int value) => BalanceReport.I(value);
        private static string I(int? value) => BalanceReport.I(value);
        private static string F(float value) => BalanceReport.F(value);
        private static string F(float? value) => BalanceReport.F(value);
        private static string F1(float? value) => BalanceReport.F1(value);
    }
}
