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
            foreach (var run in runs) summaries.Add(RunSummary.From(run));
            File.WriteAllText(Path.Combine(directory, DaysFile), DaysCsv(runs), Utf8);
            File.WriteAllText(Path.Combine(directory, SummaryFile), SummaryCsv(runs, summaries), Utf8);
            File.WriteAllText(Path.Combine(directory, ReportFile), ReportMarkdown(config, days, runs, summaries), Utf8);
        }

        // ---- days.csv ----

        private static string DaysCsv(IReadOnlyList<RunResult> runs)
        {
            var b = new StringBuilder();
            Row(b, "strategy", "target", "tier", "day", "weekend", "wallet_at_end", "earned", "spent", "actions", "action_counts", "other_items",
                "work_minutes", "travel_minutes", "travel_real_seconds", "minigame_real_seconds", "ended_at_minutes",
                "facility_level", "upgraded", "auto_skips", "energy_at_end", "focus_at_end", "stop");
            foreach (var run in runs)
                foreach (var d in run.Days)
                    Row(b, run.StrategyId, run.TargetId, run.TierId, I(d.Day), B(d.Weekend), I(d.WalletAtEnd), I(d.Earned), I(d.Spent), I(d.Actions),
                        Counts(d.ActionCounts), Counts(d.OtherItems), I(d.WorkMinutes), I(d.TravelMinutes), F(d.TravelRealSeconds), F(d.MinigameRealSeconds),
                        I(d.EndedAtMinutes), I(d.FacilityLevel), B(d.Upgraded), I(d.AutoSkips), I(d.EnergyAtEnd), I(d.FocusAtEnd), d.Stop ?? "");
            return b.ToString();
        }

        // ---- summary.csv ----

        private static string SummaryCsv(IReadOnlyList<RunResult> runs, List<RunSummary> summaries)
        {
            var b = new StringBuilder();
            Row(b, "strategy", "target", "tier", "start_day", "start_weekend", "start_wallet", "days",
                "income_per_day", "income_per_game_hour", "income_per_real_minute_with_travel", "income_per_real_minute_minigame_only",
                "final_wallet", "level2_day", "level3_day", "payback_days", "first_auto_skip_day", "first_bag_full_day", "actions_per_day");
            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                var s = summaries[i];
                Row(b, run.StrategyId, run.TargetId, run.TierId, I(run.StartDay), B(run.StartWeekend), I(run.StartWallet), I(run.Days.Count),
                    F(s.IncomePerDay), F(s.IncomePerGameHour), F(s.IncomePerRealMinuteWithTravel), F(s.IncomePerRealMinuteMinigameOnly),
                    I(s.FinalWallet), I(s.Level2Day), I(s.Level3Day), string.Join(";", s.PaybackDays.Select(I)),
                    I(s.FirstAutoSkipDay), I(s.FirstBagFullDay), F(s.ActionsPerDay));
            }
            return b.ToString();
        }

        // ---- report.md ----

        private static string ReportMarkdown(BalanceConfig config, int days, IReadOnlyList<RunResult> runs, List<RunSummary> summaries)
        {
            var a = DayAssumptions.Current;
            var b = new StringBuilder();
            b.Append("# 수치 균형 시뮬레이터 보고\n\n");
            b.Append("가정: 날 수 ").Append(I(days))
                .Append(", 점수 단계 ").Append(string.Join("·", config.Tiers.Select(t => t.Id + " " + I(t.Score))))
                .Append(", 걷기 활동마다 ").Append(I(a.TravelMinutes)).Append("분")
                .Append(", 미니게임은 제한 시간의 ").Append(F(a.MinigameShare)).Append("만큼")
                .Append(", 자는 방식 ").Append(config.Sleep).Append(".\n\n");
            b.Append(NotPlayLine).Append("\n\n");
            b.Append("하루 수입은 지갑이 늘어난 합 ÷ 날 수(시작 지갑은 뺀다). 실제 분당 수입은 걷기와 미니게임에 쓴 실제 분으로 나눈다. ")
                .Append("날은 판 안에서 몇째 날(1부터). `-`는 닿지 않았거나 셀 수 없음.\n");

            foreach (var strategy in config.Strategies)
            {
                var index = new List<int>();
                for (int i = 0; i < runs.Count; i++) if (runs[i].StrategyId == strategy.Id) index.Add(i);
                if (index.Count == 0) continue;

                var tiers = new List<string>();
                var targets = new List<string>();
                foreach (int i in index)
                {
                    if (!tiers.Contains(runs[i].TierId)) tiers.Add(runs[i].TierId);
                    if (!targets.Contains(runs[i].TargetId)) targets.Add(runs[i].TargetId);
                }

                b.Append("\n## ").Append(strategy.Id).Append("\n\n");
                b.Append("대상 종류 `").Append(strategy.Each).Append("`, 시작 `").Append(strategy.Start)
                    .Append("`, 행동 차례 ").Append(string.Join(" → ", strategy.Do.Select(d => "`" + d + "`"))).Append(".\n\n");

                var header = new List<string> { "대상", "시작" };
                foreach (var tier in tiers)
                {
                    string p = tier == "" ? "" : tier + " ";
                    header.Add(p + "하루 수입");
                    header.Add(p + "실제 분당(걷기 포함)");
                    header.Add(p + "실제 분당(미니게임만)");
                    header.Add(p + "레벨 2 날");
                    header.Add(p + "레벨 3 날");
                    header.Add(p + "되찾는 날");
                    header.Add(p + "가방 찬 날");
                }
                MdRow(b, header);
                MdRow(b, header.Select(_ => "---").ToList());

                foreach (var target in targets)
                {
                    var cells = new List<string> { target == "" ? "(한 판)" : target, "" };
                    foreach (var tier in tiers)
                    {
                        int at = index.FirstOrDefault(i => runs[i].TargetId == target && runs[i].TierId == tier, -1);
                        if (at < 0) { cells.AddRange(Enumerable.Repeat("-", 7)); continue; }
                        var run = runs[at];
                        var s = summaries[at];
                        cells[1] = I(run.StartDay) + "일째 " + (run.StartWeekend ? "주말" : "평일") + ", 지갑 " + I(run.StartWallet);
                        cells.Add(F1(s.IncomePerDay));
                        cells.Add(F1(s.IncomePerRealMinuteWithTravel));
                        cells.Add(F1(s.IncomePerRealMinuteMinigameOnly));
                        cells.Add(Dash(s.Level2Day));
                        cells.Add(Dash(s.Level3Day));
                        cells.Add(s.PaybackDays.Length == 0 ? "-" : string.Join("·", s.PaybackDays.Select(Dash)));
                        cells.Add(Dash(s.FirstBagFullDay));
                    }
                    MdRow(b, cells);
                }

                b.Append('\n');
                foreach (var tier in tiers)
                {
                    int hi = -1, lo = -1;
                    foreach (int i in index)
                    {
                        if (runs[i].TierId != tier) continue;
                        if (hi < 0 || summaries[i].IncomePerDay > summaries[hi].IncomePerDay) hi = i;
                        if (lo < 0 || summaries[i].IncomePerDay < summaries[lo].IncomePerDay) lo = i;
                    }
                    b.Append("- ").Append(tier == "" ? "" : tier + ": ")
                        .Append("하루 수입 가장 큰 것 ").Append(Name(runs[hi])).Append(' ').Append(F1(summaries[hi].IncomePerDay))
                        .Append(" ÷ 가장 작은 것 ").Append(Name(runs[lo])).Append(' ').Append(F1(summaries[lo].IncomePerDay))
                        .Append(" = ").Append(summaries[lo].IncomePerDay > 0 ? (summaries[hi].IncomePerDay / summaries[lo].IncomePerDay).ToString("0.00", Inv) + "배" : "셀 수 없음(가장 작은 것이 0)")
                        .Append('\n');
                }
            }
            return b.ToString();
        }

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
