using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Rootborn.Simulation.Pace
{
    /// <summary>나란히 보는 보고서의 유형 하나(한 판).</summary>
    public sealed class PaceCompareEntry
    {
        public string Type = string.Empty;
        /// <summary>열 이름(없으면 Type).</summary>
        public string Label;
        public PaceRunInfo Info;
        public IReadOnlyList<PaceStep> Steps;
        /// <summary>날마다 번 돈(먼 층·균형 시뮬레이터가 지갑 변화로 센 것). 없으면 null — 그 줄을 쓰지 않는다.</summary>
        public IReadOnlyList<int> DailyIncome;

        public string Column => string.IsNullOrEmpty(Label) ? Type : Label;
    }

    /// <summary>
    /// 플레이어 유형을 나란히 보는 보고서(report.md 하나, 유형마다 열 하나): 한눈에(판 전체 시간, 빈 날, 할 것 없음의 몫, 가장 긴 새로운 것 없는 구간),
    /// 하루 평균 시간 쓰임(분류별), 지점까지 걸린 시간, 되풀이 상위. 기준 축 값으로 계산한다. 숫자는 자동 테스트(실제 플레이 아님).
    /// </summary>
    public static class PaceCompare
    {
        /// <param name="header">맨 위 근거 줄들(없으면 가까운 층의 줄과 기준 가정). 먼 층·전후 비교가 자기 근거를 적는다.</param>
        public static void Write(string dir, string name, IReadOnlyList<PaceCompareEntry> entries, double secondsPerFrame, IReadOnlyList<string> header = null, string title = "플레이어 유형 비교 ")
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "report.md"), Markdown(name, entries, secondsPerFrame, header, title));
        }

        public static string Markdown(string name, IReadOnlyList<PaceCompareEntry> entries, double secondsPerFrame, IReadOnlyList<string> header = null, string title = "플레이어 유형 비교 ")
        {
            var analyses = entries.Select(e => new PaceAnalysis(e.Steps, secondsPerFrame, PaceAssumptions.Baseline)).ToList();
            var fast = entries.Select(e => new PaceAnalysis(e.Steps, secondsPerFrame, PaceAssumptions.Fastest)).ToList();
            var slow = entries.Select(e => new PaceAnalysis(e.Steps, secondsPerFrame, PaceAssumptions.Slowest)).ToList();
            var md = new StringBuilder(8192);
            md.AppendLine("# " + title + name);
            md.AppendLine();
            if (header == null)
            {
                md.AppendLine(PaceReport.EvidenceLine);
                md.AppendLine();
                md.AppendLine("기준 가정: " + PaceAssumptions.Baseline.Label + ". 유형마다 자세한 보고서는 같은 폴더 위의 `pace/" + name + "-<유형>/report.md`.");
            }
            else foreach (string line in header) md.AppendLine(line);
            md.AppendLine();

            md.AppendLine("## 한눈에");
            md.AppendLine();
            Header(md, entries);
            Row(md, "끝", entries.Select(e => e.Info.End));
            Row(md, "넘긴 날", entries.Select(e => e.Info.DaysCompleted.ToString(CultureInfo.InvariantCulture)));
            Row(md, "걸음", entries.Select(e => e.Steps.Count.ToString(CultureInfo.InvariantCulture)));
            Row(md, "판 전체(기준)", analyses.Select(a => PaceReport.Dur(a.TotalSeconds)));
            Row(md, "판 전체(가장 빠름~가장 느림)", fast.Zip(slow, (f, s) => PaceReport.Dur(f.TotalSeconds) + " ~ " + PaceReport.Dur(s.TotalSeconds)));
            Row(md, "게임이 돈 시간", entries.Select(e => PaceReport.Dur(e.Steps.Sum(s => s.GameSeconds(secondsPerFrame)))));
            Row(md, "하루 평균(기준)", analyses.Select(a => PaceReport.Dur(DayAverage(a, null))));
            Row(md, "새로운 것", entries.Select(e => e.Steps.Sum(s => s.Novelties.Count).ToString(CultureInfo.InvariantCulture)));
            Row(md, "가장 긴 새로운 것 없는 구간", analyses.Select(a =>
            {
                PaceGap g = a.LongestGaps(1).FirstOrDefault();
                return g == null ? "-" : PaceReport.Dur(g.Seconds) + "(" + g.StartDay + "일째 " + PaceReport.Clock(g.StartMinutes) + (g.Open ? ", 끝까지" : "") + ")";
            }));
            Row(md, "빈 날", analyses.Select(a =>
            {
                List<int> empty = a.EmptyDays().Where(d => d >= 1).ToList();
                return empty.Count == 0 ? "없음" : empty.Count + "일(처음 " + empty[0] + "일째)";
            }));
            Row(md, "할 것 없음의 몫", analyses.Select(a => Percent(a.TotalSeconds <= 0 ? 0 : Get(a.ByCategory, PaceCategories.NothingToDo) / a.TotalSeconds)));
            if (entries.Any(e => e.DailyIncome != null))
                Row(md, "하루 평균 수입", entries.Select(e => e.DailyIncome == null || e.DailyIncome.Count == 0 ? "-" : (e.DailyIncome.Average()).ToString("0.0", CultureInfo.InvariantCulture)));
            md.AppendLine();

            md.AppendLine("## 하루 평균 시간 쓰임 (기준, 분)");
            md.AppendLine();
            md.AppendLine("1일째부터 걸음이 있는 날의 평균(슬롯 선택·캐릭터 만들기는 뺀다).");
            md.AppendLine();
            md.AppendLine(PaceReport.DayLengthNote);
            md.AppendLine();
            Header(md, entries);
            foreach (string c in PaceCategories.All)
                Row(md, PaceCategories.Label(c), analyses.Select(a => (DayAverage(a, c) / 60.0).ToString("0.0", CultureInfo.InvariantCulture)));
            md.AppendLine();

            md.AppendLine("## 지점까지 걸린 시간");
            md.AppendLine();
            var milestones = analyses.Select(a => a.Milestones()).ToList();
            var names = new List<(string Kind, string What)>();
            foreach (var list in milestones)
                foreach (PaceMilestone m in list)
                    if (!names.Contains((m.Kind, m.What))) names.Add((m.Kind, m.What));
            if (names.Count == 0) md.AppendLine("(어느 유형도 지점에 닿지 못함)");
            else
            {
                md.Append("| 지점 | 종류 |");
                foreach (var e in entries) md.Append(' ').Append(e.Column).Append(" |");
                md.AppendLine();
                md.Append("|---|---|");
                foreach (var _ in entries) md.Append("---|");
                md.AppendLine();
                foreach (var (kind, what) in names)
                {
                    md.Append("| ").Append(what.Replace("|", "\\|")).Append(" | ").Append(kind).Append(" |");
                    foreach (var list in milestones)
                    {
                        int i = list.FindIndex(m => m.Kind == kind && m.What == what);
                        md.Append(' ').Append(i < 0 ? "닿지 못함" : list[i].Day + "일째, " + PaceReport.Dur(list[i].Seconds)).Append(" |");
                    }
                    md.AppendLine();
                }
            }
            md.AppendLine();

            md.AppendLine("## 되풀이 상위");
            md.AppendLine();
            Header(md, entries);
            for (int rank = 0; rank < 5; rank++)
                Row(md, (rank + 1) + "위", analyses.Select(a =>
                {
                    List<PaceRun> runs = a.LongestRuns(5).Where(r => r.Count > 1).ToList();
                    return rank < runs.Count ? "`" + runs[rank].Key.Replace("|", "\\|") + "` ×" + runs[rank].Count + "(" + runs[rank].Day + "일째)" : "-";
                }));
            md.AppendLine();
            md.AppendLine("## 날마다 새로운 것");
            md.AppendLine();
            Header(md, entries);
            var days = entries.SelectMany(e => e.Steps.Select(st => st.Day)).Where(d => d >= 1).Distinct().OrderBy(d => d).ToList();
            foreach (int day in days)
                Row(md, day + "일째", entries.Select(e => e.Steps.Any(st => st.Day == day) ? e.Steps.Where(st => st.Day == day).Sum(st => st.Novelties.Count).ToString(CultureInfo.InvariantCulture) : "-"));
            md.AppendLine();

            md.AppendLine("## 판");
            md.AppendLine();
            foreach (var e in entries)
            {
                md.AppendLine("- " + e.Column + " (`" + e.Type + "`): " + e.Info.End + (e.Info.WallSeconds >= 0 ? ", 벽시계 " + e.Info.WallSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "초" : ""));
                foreach (string n in e.Info.Notes) md.AppendLine("  - " + n);
            }
            return md.ToString();
        }

        private static void Header(StringBuilder md, IReadOnlyList<PaceCompareEntry> entries)
        {
            md.Append("| 항목 |");
            foreach (var e in entries) md.Append(' ').Append(e.Column).Append(" |");
            md.AppendLine();
            md.Append("|---|");
            foreach (var _ in entries) md.Append("---|");
            md.AppendLine();
        }

        private static void Row(StringBuilder md, string label, IEnumerable<string> cells)
        {
            md.Append("| ").Append(label).Append(" |");
            foreach (string c in cells) md.Append(' ').Append(c).Append(" |");
            md.AppendLine();
        }

        /// <summary>1일째부터의 하루 평균(초). category가 null이면 하루 전체.</summary>
        private static double DayAverage(PaceAnalysis a, string category)
        {
            var days = a.ByDay.Keys.Where(d => d >= 1).ToList();
            if (days.Count == 0) return 0;
            return days.Sum(d => category == null ? a.DaySeconds(d) : a.DayCategorySeconds(d, category)) / days.Count;
        }

        private static double Get(Dictionary<string, double> d, string k) => d.TryGetValue(k, out double v) ? v : 0;

        private static string Percent(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
