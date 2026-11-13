using System;
using System.Collections.Generic;
using System.Linq;

namespace Rootborn.Simulation.Pace
{
    /// <summary>새로운 것 사이의 간격 하나. Open이면 마지막 새것 뒤 판의 끝까지(다음 새것이 아직 없다).</summary>
    public sealed class PaceGap
    {
        /// <summary>간격을 연 새것의 걸음(처음 간격은 판의 첫 걸음)과 닫은 새것의 걸음(목록의 번호).</summary>
        public int FromStep, ToStep;
        public int StartDay, StartMinutes;
        public double Seconds;
        public bool Open;
        public readonly Dictionary<string, double> ByCategory = new Dictionary<string, double>(StringComparer.Ordinal);
        /// <summary>그동안 가장 많이 한 행동(이름, 횟수), 많은 순 세 개까지.</summary>
        public readonly List<(string Key, int Count)> TopKeys = new List<(string, int)>(3);
    }

    /// <summary>같은 행동을 연달아 한 묶음.</summary>
    public readonly struct PaceRun
    {
        public PaceRun(string key, int count, int fromStep, int day) { Key = key; Count = count; FromStep = fromStep; Day = day; }
        public string Key { get; }
        public int Count { get; }
        public int FromStep { get; }
        public int Day { get; }
    }

    /// <summary>지점 하나에 닿은 때: 몇 일째, 판 처음부터 실제 누적 시간(이 분석의 축 값으로).</summary>
    public readonly struct PaceMilestone
    {
        public PaceMilestone(string kind, string what, int day, int minutes, int step, double seconds)
        {
            Kind = kind; What = what; Day = day; Minutes = minutes; Step = step; Seconds = seconds;
        }

        public string Kind { get; }
        public string What { get; }
        public int Day { get; }
        public int Minutes { get; }
        public int Step { get; }
        public double Seconds { get; }
    }

    /// <summary>한 판의 속도 기록을 축 값 한 벌로 읽는다: 걸음마다 실제 시간, 분류별·날별 시간, 지루함(간격·되풀이·빈 날·할 것 없음), 지점.</summary>
    public sealed class PaceAnalysis
    {
        private readonly IReadOnlyList<PaceStep> _steps;

        public PaceAnalysis(IReadOnlyList<PaceStep> steps, double secondsPerFrame, PaceAxis axis)
        {
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
            Axis = axis;
            Real = PaceTime.RealSeconds(steps, axis, secondsPerFrame);
            Cumulative = new double[Real.Length];
            double sum = 0;
            for (int i = 0; i < Real.Length; i++)
            {
                sum += Real[i];
                Cumulative[i] = sum;
                PaceStep s = steps[i];
                Add(ByCategory, s.Category, Real[i]);
                if (!ByDay.TryGetValue(s.Day, out var day)) ByDay[s.Day] = day = new Dictionary<string, double>(StringComparer.Ordinal);
                Add(day, s.Category, Real[i]);
            }
            TotalSeconds = sum;
        }

        public PaceAxis Axis { get; }
        /// <summary>걸음마다 실제 초와 판 처음부터 그 걸음 끝까지의 누적 초.</summary>
        public double[] Real { get; }
        public double[] Cumulative { get; }
        public double TotalSeconds { get; }
        public readonly Dictionary<string, double> ByCategory = new Dictionary<string, double>(StringComparer.Ordinal);
        public readonly SortedDictionary<int, Dictionary<string, double>> ByDay = new SortedDictionary<int, Dictionary<string, double>>();

        private static void Add(Dictionary<string, double> into, string key, double value) => into[key] = (into.TryGetValue(key, out double v) ? v : 0) + value;

        public double DaySeconds(int day) => ByDay.TryGetValue(day, out var d) ? d.Values.Sum() : 0;

        public double DayCategorySeconds(int day, string category) => ByDay.TryGetValue(day, out var d) && d.TryGetValue(category, out double v) ? v : 0;

        /// <summary>그날 실제 시간 중 할 것 없음의 몫(0~1).</summary>
        public double NothingToDoShare(int day)
        {
            double total = DaySeconds(day);
            return total <= 0 ? 0 : DayCategorySeconds(day, PaceCategories.NothingToDo) / total;
        }

        /// <summary>새로운 것 사이 간격, 긴 순. 첫 간격은 판의 처음부터 첫 새것까지, 마지막은 마지막 새것 뒤 판의 끝까지(열린 간격).</summary>
        public List<PaceGap> LongestGaps(int count)
        {
            var gaps = new List<PaceGap>();
            int from = 0;
            bool started = false;
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i].Novelties.Count == 0) continue;
                gaps.Add(Gap(from, started ? from + 1 : 0, i, open: false));
                from = i; started = true;
            }
            if (_steps.Count > 0 && (!started || from < _steps.Count - 1)) gaps.Add(Gap(from, started ? from + 1 : 0, _steps.Count - 1, open: true));
            return gaps.OrderByDescending(g => g.Seconds).ThenBy(g => g.FromStep).Take(count).ToList();
        }

        private PaceGap Gap(int from, int first, int to, bool open)
        {
            var g = new PaceGap { FromStep = from, ToStep = to, Open = open, StartDay = _steps[first].Day, StartMinutes = _steps[first].Minutes };
            var keys = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = first; i <= to; i++)
            {
                g.Seconds += Real[i];
                Add(g.ByCategory, _steps[i].Category, Real[i]);
                keys[_steps[i].Key] = (keys.TryGetValue(_steps[i].Key, out int n) ? n : 0) + 1;
            }
            foreach (var k in keys.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Take(3)) g.TopKeys.Add((k.Key, k.Value));
            return g;
        }

        /// <summary>같은 행동을 연달아 한 묶음, 긴 순.</summary>
        public List<PaceRun> LongestRuns(int count)
        {
            var runs = new List<PaceRun>();
            int start = 0;
            for (int i = 1; i <= _steps.Count; i++)
            {
                if (i < _steps.Count && _steps[i].Key == _steps[start].Key) continue;
                if (_steps.Count > 0) runs.Add(new PaceRun(_steps[start].Key, i - start, start, _steps[start].Day));
                start = i;
            }
            return runs.OrderByDescending(r => r.Count).ThenBy(r => r.FromStep).Take(count).ToList();
        }

        /// <summary>날마다 가장 많이 한 행동과 횟수.</summary>
        public SortedDictionary<int, (string Key, int Count)> MostRepeatedPerDay()
        {
            var result = new SortedDictionary<int, (string, int)>();
            foreach (var day in _steps.GroupBy(s => s.Day))
            {
                var top = day.GroupBy(s => s.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Count()))
                    .OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).First();
                result[day.Key] = top;
            }
            return result;
        }

        /// <summary>걸음은 있는데 새로운 것이 하나도 없던 날.</summary>
        public List<int> EmptyDays() => _steps.GroupBy(s => s.Day).Where(d => d.All(s => s.Novelties.Count == 0)).Select(d => d.Key).OrderBy(d => d).ToList();

        /// <summary>지점(지점 종류의 새것)에 닿은 때, 걸음 순.</summary>
        public List<PaceMilestone> Milestones()
        {
            var list = new List<PaceMilestone>();
            for (int i = 0; i < _steps.Count; i++)
                foreach (PaceNovelty n in _steps[i].Novelties)
                    if (n.Milestone) list.Add(new PaceMilestone(n.Kind, n.What, _steps[i].Day, _steps[i].Minutes, i, Cumulative[i]));
            return list;
        }
    }
}
