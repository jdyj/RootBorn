using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 판들의 결과를 파일 셋으로 낸다(설계 6.6절): days.csv(날마다 한 줄), summary.csv(판마다 한 줄), report.md(사람이 읽는 한글 표).
    /// 같은 입력이면 바이트까지 같다: 날짜·시각을 적지 않고, 수는 InvariantCulture, 줄 끝은 "\n", 순서는 받은 판의 순서.
    /// </summary>
    internal static class BalanceReport
    {
        public const string OutVariable = "ROOTBORN_BALANCE_OUT", DaysVariable = "ROOTBORN_BALANCE_DAYS";
        public const string DaysFile = "days.csv", SummaryFile = "summary.csv", ReportFile = "report.md";
        public const string NotPlayLine = "가정을 넣은 계산이다. 실제 플레이가 아니다.";
        public const string RecoverColumn = "쓴 값만큼 버는 날", PaybackColumn = "늘어난 수입으로 되찾는 날";
        public const string NoSpanLabel = "구간 없음", NoGainLabel = "늘지 않음";
        public const string DroppedColumnsLine = "모든 줄에 값이 없어 뺀 칸:";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>ROOTBORN_BALANCE_OUT, 없으면 &lt;저장소&gt;/output/balance.</summary>
        public static string OutputDirectory()
        {
            var value = Environment.GetEnvironmentVariable(OutVariable);
            return string.IsNullOrWhiteSpace(value) ? Path.Combine(TestWorld.RepoRoot, "output", "balance") : value;
        }

        /// <summary>ROOTBORN_BALANCE_DAYS, 없으면 설정의 날 수. 숫자가 아니거나 1 미만이면 변수 이름과 받은 값을 말하며 실패한다.</summary>
        public static int Days(BalanceConfig config)
        {
            var value = Environment.GetEnvironmentVariable(DaysVariable);
            if (string.IsNullOrWhiteSpace(value)) return config.Days;
            if (!int.TryParse(value.Trim(), NumberStyles.Integer, Inv, out int days) || days < 1)
                throw new InvalidOperationException(DaysVariable + "는 1 이상의 정수여야 한다. 받은 값: \"" + value + "\"");
            return days;
        }

        public static void Write(string directory, BalanceConfig config, int days, IReadOnlyList<RunResult> runs)
        {
            Directory.CreateDirectory(directory);
            var summaries = new List<RunSummary>(runs.Count);
            var tiers = new List<string>(runs.Count);
            foreach (var run in runs)
            {
                summaries.Add(RunSummary.From(run));
                tiers.Add(TierOf(config, run));
            }
            File.WriteAllText(Path.Combine(directory, DaysFile), DaysCsv(runs, tiers), Utf8);
            File.WriteAllText(Path.Combine(directory, SummaryFile), SummaryCsv(runs, tiers, summaries), Utf8);
            File.WriteAllText(Path.Combine(directory, ReportFile), ReportMarkdown(config, days, runs, tiers, summaries), Utf8);
        }

        /// <summary>
        /// 판의 점수 단계 이름(1단계 보고의 칸): 점수를 쓰는 전략(scored)이면 그 판의 점수 모델이 "fixed N"인 단계의 id, 아니면 "".
        /// </summary>
        public static string TierOf(BalanceConfig config, RunResult run)
        {
            var strategy = config.Strategies.FirstOrDefault(s => s.Id == run.StrategyId);
            if (strategy == null || !strategy.Scored || run.Setup == null) return "";
            var tier = config.Tiers.FirstOrDefault(t => ScoreModels.Fixed(t.Score) == run.Setup.Score);
            return tier?.Id ?? run.Setup.Score;
        }

        // ---- days.csv ----

        private static string DaysCsv(IReadOnlyList<RunResult> runs, List<string> tiers)
        {
            var b = new StringBuilder();
            Row(b, "strategy", "target", "tier", "day", "weekend", "wallet_at_end", "earned", "minigame_earned", "spent", "actions", "action_counts", "other_items_held",
                "work_minutes", "travel_minutes", "travel_real_seconds", "minigame_real_seconds", "ended_at_minutes",
                "facility_level", "upgraded", "auto_skips", "energy_at_end", "focus_at_end", "stop");
            for (int i = 0; i < runs.Count; i++)
                foreach (var d in runs[i].Days)
                    Row(b, runs[i].StrategyId, runs[i].TargetId, tiers[i], I(d.Day), B(d.Weekend), I(d.WalletAtEnd), I(d.Earned), I(d.MinigameEarned), I(d.Spent), I(d.Actions),
                        Counts(d.ActionCounts), Counts(d.OtherItems), I(d.WorkMinutes), I(d.TravelMinutes), F(d.TravelRealSeconds), F(d.MinigameRealSeconds),
                        I(d.EndedAtMinutes), I(d.FacilityLevel), B(d.Upgraded), I(d.AutoSkips), I(d.EnergyAtEnd), I(d.FocusAtEnd), d.Stop ?? "");
            return b.ToString();
        }

        // ---- summary.csv ----

        private static string SummaryCsv(IReadOnlyList<RunResult> runs, List<string> tiers, List<RunSummary> summaries)
        {
            var b = new StringBuilder();
            Row(b, "strategy", "target", "tier", "start_day", "start_weekend", "start_wallet", "days",
                "income_per_day", "income_per_game_hour", "income_per_real_minute_with_travel", "income_per_real_minute_minigame_only",
                "final_wallet", "level2_day", "level3_day", "payback_days", "payback_gaps", "recover_days", "first_auto_skip_day", "first_bag_full_day", "actions_per_day");
            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                var s = summaries[i];
                Row(b, run.StrategyId, run.TargetId, tiers[i], I(run.StartDay), B(run.StartWeekend), I(run.StartWallet), I(run.Days.Count),
                    F(s.IncomePerDay), F(s.IncomePerGameHour), F(s.IncomePerRealMinuteWithTravel), F(s.IncomePerRealMinuteMinigameOnly),
                    I(s.FinalWallet), I(s.Level2Day), I(s.Level3Day), string.Join(";", s.PaybackDays.Select(I)),
                    string.Join(";", s.PaybackGaps.Select(g => g ?? "")), string.Join(";", s.RecoverDays.Select(I)),
                    I(s.FirstAutoSkipDay), I(s.FirstBagFullDay), F(s.ActionsPerDay));
            }
            return b.ToString();
        }

        // ---- report.md ----

        private static string ReportMarkdown(BalanceConfig config, int days, IReadOnlyList<RunResult> runs, List<string> runTiers, List<RunSummary> summaries)
        {
            var a = DayAssumptions.Current;
            var b = new StringBuilder();
            b.Append("# 수치 균형 시뮬레이터 보고\n\n");
            b.Append("가정: 날 수 ").Append(I(days))
                .Append(", 점수 단계 ").Append(string.Join("·", config.Tiers.Select(t => t.Id + " " + I(t.Score))))
                .Append(", 걷기 활동마다 ").Append(I(a.TravelMinutes)).Append("분")
                .Append(", 미니게임은 제한 시간의 ").Append(F(a.MinigameShare)).Append("만큼")
                .Append(", 자는 방식 ").Append(config.BaselineSetup.Sleep).Append(".\n\n");
            b.Append(NotPlayLine).Append("\n\n");
            b.Append("- 하루 수입은 지갑이 늘어난 합 ÷ 날 수(시작 지갑은 뺀다).\n")
                .Append("- 실제 분당(걷기 포함)은 판 전체 수입 ÷ 걷기와 미니게임에 쓴 실제 분. 실제 분당(미니게임만)은 미니게임을 한 행동이 번 것만 ÷ 미니게임에 쓴 실제 분(자동 넘기기·잠의 수입은 뺀다).\n")
                .Append("- 날은 판 안에서 몇째 날(1부터). `-`는 닿지 않았거나 셀 수 없음. 올린 것이 여럿이면 `·`로 잇는다.\n")
                .Append("- ").Append(RecoverColumn).Append(": 올린 날부터 번 것의 합이 그 올리기에 쓴 값 이상이 되기까지의 날 수(올린 날 포함). `-`는 판이 끝날 때까지 못 채움.\n")
                .Append("- ").Append(PaybackColumn).Append(": 쓴 값 ÷ (올린 뒤 하루 수입 평균 − 올리기 전 평균). 앞 구간은 이전 올린 날 다음부터, 뒤 구간은 다음 올린 날 앞까지. ")
                .Append('`').Append(NoSpanLabel).Append("`은 앞이나 뒤에 올리지 않은 날이 없어(이어 올렸거나 판의 처음·끝) 셀 수 없음, ")
                .Append('`').Append(NoGainLabel).Append("`은 올린 뒤 평균이 늘지 않음.\n")
                .Append("- 표에서 모든 줄의 값이 없는 칸은 빼고 표 아래에 적는다.\n")
                .Append("- days.csv의 `other_items_held`는 그날 끝에 가진 통화 밖 아이템 개수다(그날 얻은 수가 아니다).\n");

            foreach (var strategy in config.Strategies)
            {
                var index = new List<int>();
                for (int i = 0; i < runs.Count; i++) if (runs[i].StrategyId == strategy.Id) index.Add(i);
                if (index.Count == 0) continue;

                var tiers = new List<string>();
                var targets = new List<string>();
                foreach (int i in index)
                {
                    if (!tiers.Contains(runTiers[i])) tiers.Add(runTiers[i]);
                    if (!targets.Contains(runs[i].TargetId)) targets.Add(runs[i].TargetId);
                }

                b.Append("\n## ").Append(strategy.Id).Append("\n\n");
                b.Append("대상 종류 `").Append(strategy.Each).Append("`, 시작 `").Append(strategy.Start)
                    .Append("`, 행동 차례 ").Append(string.Join(" → ", strategy.Do.Select(d => "`" + d + "`"))).Append(".\n\n");
                if (!string.IsNullOrEmpty(strategy.Note)) b.Append(strategy.Note).Append("\n\n");

                var header = new List<string> { "대상", "시작" };
                foreach (var tier in tiers)
                {
                    string p = tier == "" ? "" : tier + " ";
                    header.Add(p + "하루 수입");
                    header.Add(p + "실제 분당(걷기 포함)");
                    header.Add(p + "실제 분당(미니게임만)");
                    header.Add(p + "레벨 2 날");
                    header.Add(p + "레벨 3 날");
                    header.Add(p + RecoverColumn);
                    header.Add(p + PaybackColumn);
                    header.Add(p + "가방 찬 날");
                }
                int perTier = (header.Count - 2) / Math.Max(1, tiers.Count);

                var rows = new List<List<string>>(targets.Count);
                foreach (var target in targets)
                {
                    var cells = new List<string> { target == "" ? "(한 판)" : target, "" };
                    foreach (var tier in tiers)
                    {
                        int at = index.FirstOrDefault(i => runs[i].TargetId == target && runTiers[i] == tier, -1);
                        if (at < 0) { cells.AddRange(Enumerable.Repeat("-", perTier)); continue; }
                        var run = runs[at];
                        var s = summaries[at];
                        cells[1] = I(run.StartDay) + "일째 " + (run.StartWeekend ? "주말" : "평일") + ", 지갑 " + I(run.StartWallet);
                        cells.Add(F1(s.IncomePerDay));
                        cells.Add(F1(s.IncomePerRealMinuteWithTravel));
                        cells.Add(F1(s.IncomePerRealMinuteMinigameOnly));
                        cells.Add(Dash(s.Level2Day));
                        cells.Add(Dash(s.Level3Day));
                        cells.Add(s.RecoverDays.Length == 0 ? "-" : string.Join("·", s.RecoverDays.Select(Dash)));
                        cells.Add(s.PaybackDays.Length == 0 ? "-" : string.Join("·", s.PaybackDays.Select((p, u) => p.HasValue ? I(p) : GapLabel(s.PaybackGaps[u]))));
                        cells.Add(Dash(s.FirstBagFullDay));
                    }
                    rows.Add(cells);
                }

                // 모든 줄에서 값이 없는(`-`) 칸은 뺀다. 대상·시작 칸은 늘 둔다.
                var keep = new List<int>(header.Count);
                var dropped = new List<string>();
                for (int c = 0; c < header.Count; c++)
                {
                    if (c < 2 || rows.Any(r => r[c] != "-")) keep.Add(c);
                    else dropped.Add(header[c]);
                }
                MdRow(b, keep.Select(c => header[c]).ToList());
                MdRow(b, keep.Select(_ => "---").ToList());
                foreach (var row in rows) MdRow(b, keep.Select(c => row[c]).ToList());

                if (dropped.Count > 0) b.Append('\n').Append(DroppedColumnsLine).Append(' ').Append(string.Join(", ", dropped)).Append(".\n");
                bool firstRatio = true;
                foreach (var tier in tiers)
                {
                    int hi = -1, lo = -1, count = 0;
                    foreach (int i in index)
                    {
                        if (runTiers[i] != tier) continue;
                        count++;
                        if (hi < 0 || summaries[i].IncomePerDay > summaries[hi].IncomePerDay) hi = i;
                        if (lo < 0 || summaries[i].IncomePerDay < summaries[lo].IncomePerDay) lo = i;
                    }
                    if (count < 2) continue; // 대상이 하나면 견줄 것이 없다
                    if (firstRatio) b.Append('\n');
                    firstRatio = false;
                    b.Append("- ").Append(tier == "" ? "" : tier + ": ").Append("하루 수입 ");
                    if (summaries[hi].IncomePerDay == summaries[lo].IncomePerDay)
                        b.Append("모두 같음 (").Append(F1(summaries[hi].IncomePerDay)).Append(")\n");
                    else
                        b.Append("가장 큰 것 ").Append(Name(runs[hi])).Append(' ').Append(F1(summaries[hi].IncomePerDay))
                            .Append(" ÷ 가장 작은 것 ").Append(Name(runs[lo])).Append(' ').Append(F1(summaries[lo].IncomePerDay))
                            .Append(" = ").Append(summaries[lo].IncomePerDay > 0 ? (summaries[hi].IncomePerDay / summaries[lo].IncomePerDay).ToString("0.00", Inv) + "배" : "셀 수 없음(가장 작은 것이 0)")
                            .Append('\n');
                }
            }
            return b.ToString();
        }

        // 늘어난 수입으로 되찾는 날을 셀 수 없었던 까닭의 표 글자.
        private static string GapLabel(string gap)
            => gap == PaybackGap.NoSpan ? NoSpanLabel : gap == PaybackGap.NoGain ? NoGainLabel : "-";

        private static string Name(RunResult run) => run.TargetId == "" ? "(한 판)" : run.TargetId;

        private static void MdRow(StringBuilder b, List<string> cells)
        {
            b.Append('|');
            foreach (var c in cells) b.Append(' ').Append(c.Replace("|", "\\|")).Append(" |");
            b.Append('\n');
        }

        // ---- 칸 쓰기 ----

        private static void Row(StringBuilder b, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) b.Append(',');
                var c = cells[i] ?? "";
                if (c.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) b.Append('"').Append(c.Replace("\"", "\"\"")).Append('"');
                else b.Append(c);
            }
            b.Append('\n');
        }

        private static string Counts(SortedDictionary<string, int> counts) => string.Join(";", counts.Select(p => p.Key + "×" + I(p.Value)));
        private static string I(int value) => value.ToString(Inv);
        private static string I(int? value) => value.HasValue ? value.Value.ToString(Inv) : "";
        private static string F(float value) => value.ToString("0.###", Inv);
        private static string F(float? value) => value.HasValue ? F(value.Value) : "";
        private static string F1(float value) => value.ToString("0.0", Inv);
        private static string F1(float? value) => value.HasValue ? F1(value.Value) : "-";
        private static string Dash(int? value) => value.HasValue ? value.Value.ToString(Inv) : "-";
        private static string B(bool value) => value ? "true" : "false";
    }
}
