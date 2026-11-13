using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Rootborn.Simulation.Pace
{
    /// <summary>범위 하나: 어느 층(near·far·*)의 누구(유형·전략 이름, *)의 지표가 min 이상 max 이하여야 한다. min·max 중 없는 쪽은 보지 않는다.</summary>
    public sealed class PaceBand
    {
        public string Metric = string.Empty, Who = "*", Layer = "*";
        public double? Min, Max;

        public override string ToString() => Layer + "/" + Who + "/" + Metric + " [" + (Min?.ToString(CultureInfo.InvariantCulture) ?? "-") + ", " + (Max?.ToString(CultureInfo.InvariantCulture) ?? "-") + "]";
    }

    /// <summary>
    /// 속도 보고서 숫자의 범위 검사(설계 7절). 범위는 pace-bands.json(Simulation 테스트 폴더)에 두고 처음에는 비어 있어 아무것도 막지 않는다 — 값은 사용자가 정한다.
    /// 균형 시뮬레이터의 money-balance.json "bands"와 같이 설정 파일의 배열이다. 지표(기준 축 값, 분 단위):
    /// total-minutes(판 전체), day-average-minutes(1일째부터 하루 평균), longest-gap-minutes(가장 긴 새로운 것 없는 구간), nothing-to-do-share(할 것 없음의 몫, 0~1),
    /// empty-days(빈 날 수), milestone-minutes:&lt;지점 종류&gt;(그 종류의 첫 지점까지 — 닿지 못하면 범위 밖). 모르는 지표는 범위 밖으로 적는다(오타가 조용히 통과하지 않게).
    /// </summary>
    public static class PaceBands
    {
        public const string FileName = "pace-bands.json";
        public const string Near = "near", Far = "far";
        public const string MilestonePrefix = "milestone-minutes:";

        public static string FilePath(string repoRoot) => Path.Combine(repoRoot, "game", "tests", "Rootborn.Simulation.Tests", FileName);

        public static List<PaceBand> Read(string path)
        {
            var bands = new List<PaceBand>();
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            foreach (JsonElement b in document.RootElement.GetProperty("bands").EnumerateArray())
                bands.Add(new PaceBand
                {
                    Metric = b.GetProperty("metric").GetString(),
                    Who = b.TryGetProperty("who", out var who) ? who.GetString() : "*",
                    Layer = b.TryGetProperty("layer", out var layer) ? layer.GetString() : "*",
                    Min = b.TryGetProperty("min", out var min) && min.ValueKind == JsonValueKind.Number ? min.GetDouble() : (double?)null,
                    Max = b.TryGetProperty("max", out var max) && max.ValueKind == JsonValueKind.Number ? max.GetDouble() : (double?)null,
                });
            return bands;
        }

        /// <summary>범위 밖인 것(없으면 빈 목록). who는 PaceCompareEntry.Type과 비교한다.</summary>
        public static List<string> Check(IReadOnlyList<PaceBand> bands, string layer, IReadOnlyList<PaceCompareEntry> entries, double secondsPerFrame)
        {
            var outside = new List<string>();
            foreach (PaceCompareEntry e in entries)
            {
                PaceAnalysis a = null;
                foreach (PaceBand band in bands)
                {
                    if (band.Layer != "*" && band.Layer != layer) continue;
                    if (band.Who != "*" && band.Who != e.Type) continue;
                    a ??= new PaceAnalysis(e.Steps, secondsPerFrame, PaceAssumptions.Baseline);
                    double? value = Value(band.Metric, a, out string why);
                    if (value == null) { outside.Add(band + " " + e.Type + ": " + why); continue; }
                    if (band.Min.HasValue && value < band.Min || band.Max.HasValue && value > band.Max)
                        outside.Add(band + " " + e.Type + ": " + value.Value.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }
            return outside;
        }

        private static double? Value(string metric, PaceAnalysis a, out string why)
        {
            why = null;
            var days = a.ByDay.Keys.Where(d => d >= 1).ToList();
            switch (metric)
            {
                case "total-minutes": return a.TotalSeconds / 60;
                case "day-average-minutes": return days.Count == 0 ? 0 : days.Sum(a.DaySeconds) / days.Count / 60;
                case "longest-gap-minutes": return (a.LongestGaps(1).FirstOrDefault()?.Seconds ?? 0) / 60;
                case "nothing-to-do-share": return a.TotalSeconds <= 0 ? 0 : (a.ByCategory.TryGetValue(PaceCategories.NothingToDo, out double v) ? v : 0) / a.TotalSeconds;
                case "empty-days": return a.EmptyDays().Count(d => d >= 1);
            }
            if (metric != null && metric.StartsWith(MilestonePrefix, StringComparison.Ordinal))
            {
                string kind = metric.Substring(MilestonePrefix.Length);
                var hit = a.Milestones().Where(m => m.Kind == kind).ToList();
                if (hit.Count > 0) return hit[0].Seconds / 60;
                why = "닿지 못함";
                return null;
            }
            why = "모르는 지표";
            return null;
        }
    }
}
