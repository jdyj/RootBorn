using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Rootborn.Simulation.Pace
{
    /// <summary>
    /// 덮어쓰기 전후 비교 보고서(report.md 하나): 맨 위에 바뀐 필드, 그다음 누구(유형·전략)마다 전·후·차이 — 판 전체 시간, 하루 평균, 분류별 하루 평균,
    /// 가장 긴 새로운 것 없는 구간, 할 것 없음의 몫, 새로운 것 수, 하루 평균 수입(있으면), 지점까지 걸린 시간. 기준 축 값. 자동 테스트(실제 플레이 아님).
    /// 전과 후의 목록은 같은 순서·같은 누구여야 한다.
    /// </summary>
    public static class PaceDiff
    {
        public const string ChangesTitle = "## 바뀐 필드";

        public static void Write(string dir, string name, IReadOnlyList<string> changes, IReadOnlyList<PaceCompareEntry> before, IReadOnlyList<PaceCompareEntry> after,
            double secondsPerFrame, IReadOnlyList<string> header)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "report.md"), Markdown(name, changes, before, after, secondsPerFrame, header));
        }

        public static string Markdown(string name, IReadOnlyList<string> changes, IReadOnlyList<PaceCompareEntry> before, IReadOnlyList<PaceCompareEntry> after,
            double secondsPerFrame, IReadOnlyList<string> header)
        {
            if (before.Count != after.Count) throw new ArgumentException("전과 후의 판 수가 다르다");
            var b = before.Select(e => new PaceAnalysis(e.Steps, secondsPerFrame, PaceAssumptions.Baseline)).ToList();
            var a = after.Select(e => new PaceAnalysis(e.Steps, secondsPerFrame, PaceAssumptions.Baseline)).ToList();
            var md = new StringBuilder(8192);
            md.AppendLine("# 덮어쓰기 전후 비교 " + name);
            md.AppendLine();
            md.AppendLine(ChangesTitle);
            md.AppendLine();
            if (changes.Count == 0) md.AppendLine("(없음 — 덮어쓰기가 아무것도 바꾸지 않았다)");
            foreach (string c in changes) md.AppendLine("- `" + c + "`");
            md.AppendLine();
            foreach (string line in header) md.AppendLine(line);
            md.AppendLine();
            md.AppendLine("전은 data/·game/ 그대로, 후는 덮어쓴 것. 기준 가정 " + PaceAssumptions.Baseline.Label + ".");
            md.AppendLine();

            Metric(md, "판 전체", before, i => b[i].TotalSeconds, i => a[i].TotalSeconds, Duration);
            Metric(md, "하루 평균", before, i => DayAverage(b[i], null), i => DayAverage(a[i], null), Duration);
            foreach (string c in PaceCategories.All)
                Metric(md, "하루 평균 " + PaceCategories.Label(c), before, i => DayAverage(b[i], c), i => DayAverage(a[i], c), Duration);
            Metric(md, "가장 긴 새로운 것 없는 구간", before, i => Longest(b[i]), i => Longest(a[i]), Duration);
            Metric(md, "할 것 없음의 몫", before, i => Share(b[i]), i => Share(a[i]), v => (v * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%");
            Metric(md, "새로운 것", before, i => before[i].Steps.Sum(s => s.Novelties.Count), i => after[i].Steps.Sum(s => s.Novelties.Count), v => v.ToString("0", CultureInfo.InvariantCulture));
            if (before.Any(e => e.DailyIncome != null))
                Metric(md, "하루 평균 수입", before, i => Income(before[i]), i => Income(after[i]), v => v.ToString("0.0", CultureInfo.InvariantCulture));

            md.AppendLine("## 지점까지 걸린 시간");
            md.AppendLine();
            md.AppendLine("| 누구 | 지점 | 종류 | 전 | 후 | 차이 |");
            md.AppendLine("|---|---|---|---|---|---|");
            for (int i = 0; i < before.Count; i++)
            {
                var mb = b[i].Milestones();
                var ma = a[i].Milestones();
                var names = new List<(string Kind, string What)>();
                foreach (PaceMilestone m in mb.Concat(ma)) if (!names.Contains((m.Kind, m.What))) names.Add((m.Kind, m.What));
                foreach (var (kind, what) in names)
                {
                    int x = mb.FindIndex(m => m.Kind == kind && m.What == what), y = ma.FindIndex(m => m.Kind == kind && m.What == what);
                    string diff = x >= 0 && y >= 0 ? Signed(ma[y].Seconds - mb[x].Seconds) : x < 0 ? "새로 닿음" : "못 닿게 됨";
                    md.AppendLine("| " + before[i].Column + " | " + what.Replace("|", "\\|") + " | " + kind + " | " + (x < 0 ? "닿지 못함" : mb[x].Day + "일째, " + PaceReport.Dur(mb[x].Seconds))
                        + " | " + (y < 0 ? "닿지 못함" : ma[y].Day + "일째, " + PaceReport.Dur(ma[y].Seconds)) + " | " + diff + " |");
                }
            }
            return md.ToString();
        }

        private static void Metric(StringBuilder md, string title, IReadOnlyList<PaceCompareEntry> who, Func<int, double> before, Func<int, double> after, Func<double, string> show)
        {
            md.AppendLine("### " + title);
            md.AppendLine();
            md.AppendLine("| 누구 | 전 | 후 | 차이 |");
            md.AppendLine("|---|---|---|---|");
            for (int i = 0; i < who.Count; i++)
            {
                double x = before(i), y = after(i);
                string diff = show == Duration ? Signed(y - x) : (y - x >= 0 ? "+" : "") + show(y - x);
                md.AppendLine("| " + who[i].Column + " | " + show(x) + " | " + show(y) + " | " + diff + " |");
            }
            md.AppendLine();
        }

        private static readonly Func<double, string> Duration = PaceReport.Dur;

        private static string Signed(double seconds) => Math.Abs(seconds) < 0.5 ? "0초" : (seconds > 0 ? "+" : "-") + PaceReport.Dur(Math.Abs(seconds));

        private static double DayAverage(PaceAnalysis a, string category)
        {
            var days = a.ByDay.Keys.Where(d => d >= 1).ToList();
            return days.Count == 0 ? 0 : days.Sum(d => category == null ? a.DaySeconds(d) : a.DayCategorySeconds(d, category)) / days.Count;
        }

        private static double Longest(PaceAnalysis a) => a.LongestGaps(1).FirstOrDefault()?.Seconds ?? 0;

        private static double Share(PaceAnalysis a) => a.TotalSeconds <= 0 ? 0 : (a.ByCategory.TryGetValue(PaceCategories.NothingToDo, out double v) ? v : 0) / a.TotalSeconds;

        private static double Income(PaceCompareEntry e) => e.DailyIncome == null || e.DailyIncome.Count == 0 ? 0 : e.DailyIncome.Average();
    }
}
