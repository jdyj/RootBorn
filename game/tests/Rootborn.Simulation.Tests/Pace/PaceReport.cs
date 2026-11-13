using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Rootborn.Simulation.Pace
{
    /// <summary>판 하나의 바깥 정보(보고서 머리에 쓴다).</summary>
    public sealed class PaceRunInfo
    {
        public string Name = string.Empty, Policy = string.Empty;
        public int Seed;
        public int DaysCompleted;
        public long Frames;
        /// <summary>판이 어떻게 끝났는가(날 수를 채움, 체인을 끝냄, 걸음 상한, 실패 …).</summary>
        public string End = string.Empty;
        /// <summary>이 판을 돌리는 데 든 벽시계 시간(초, 재지 않았으면 음수) — 하루를 프레임 단위로 도는 비용.</summary>
        public double WallSeconds = -1;
        public readonly List<string> Notes = new List<string>();
    }

    /// <summary>
    /// 속도 보고서 세 파일: report.md(한 장 요약), timeline.csv(걸음마다), novelty.csv(새로운 것의 사건). 테스트 산출물이라 한국어로 쓴다
    /// (게임 화면 문구가 아니다). 숫자는 자동 테스트(실제 플레이 아님)이고 사람 쪽 시간은 가정이다.
    /// </summary>
    public static class PaceReport
    {
        public const string EvidenceLine = "> 자동 테스트(실제 플레이 아님), 사람 쪽 시간은 가정이다. 게임이 돈 시간만 실제로 돈 값(창 없는 몸의 프레임 수 ÷ 초당 60프레임)이다.";

        public static string Directory(string repoRoot, string name) => Path.Combine(repoRoot, "output", "simulation", "pace", name);

        public static void Write(string dir, PaceRunInfo info, IReadOnlyList<PaceStep> steps, double secondsPerFrame)
        {
            System.IO.Directory.CreateDirectory(dir);
            var baseline = new PaceAnalysis(steps, secondsPerFrame, PaceAssumptions.Baseline);
            File.WriteAllText(Path.Combine(dir, "report.md"), Markdown(info, steps, secondsPerFrame, baseline));
            File.WriteAllText(Path.Combine(dir, "timeline.csv"), Timeline(steps, secondsPerFrame, baseline));
            File.WriteAllText(Path.Combine(dir, "novelty.csv"), Novelty(steps, baseline));
        }

        // ---------- report.md ----------

        private static string Markdown(PaceRunInfo info, IReadOnlyList<PaceStep> steps, double spf, PaceAnalysis b)
        {
            var md = new StringBuilder(8192);
            md.AppendLine("# 속도 보고 " + info.Name);
            md.AppendLine();
            md.AppendLine(EvidenceLine);
            md.AppendLine();
            double game = steps.Sum(s => s.GameSeconds(spf));
            md.AppendLine("## 판");
            md.AppendLine();
            md.AppendLine("- 정책 `" + info.Policy + "`, 씨앗 " + info.Seed + ", 걸음 " + steps.Count + ", 프레임 " + info.Frames + "(게임이 돈 시간 " + Dur(game) + "), 넘긴 날 " + info.DaysCompleted);
            md.AppendLine("- 끝: " + info.End);
            if (info.WallSeconds >= 0) md.AppendLine("- 이 PC에서 돌린 시간(벽시계): " + info.WallSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "초");
            foreach (string n in info.Notes) md.AppendLine("- " + n);
            md.AppendLine();

            md.AppendLine("## 실제 시간 범위 (축 값마다)");
            md.AppendLine();
            md.AppendLine("기준(**굵게**)에서 축 하나만 바꾼 줄, 그리고 가장 빠른 사람·가장 느린 사람. 미니게임은 도전마다 자동 풀이 시간과 제한 시간 몫 중 큰 쪽.");
            md.AppendLine();
            md.AppendLine("| 가정 | 판 전체 | 그중 게임이 돈 시간 | 읽기 | 고르기 | 미니게임 보탬 |");
            md.AppendLine("|---|---|---|---|---|---|");
            var rows = PaceAssumptions.OneAtATime();
            rows.Add(PaceAssumptions.Fastest);
            rows.Add(PaceAssumptions.Slowest);
            foreach (PaceAxis axis in rows)
            {
                var a = new PaceAnalysis(steps, spf, axis);
                double read = steps.Sum(axis.ReadingSeconds), choose = steps.Sum(axis.ChoiceSeconds);
                double extra = a.TotalSeconds - game - read - choose;
                string label = axis.SameAs(PaceAssumptions.Baseline) ? "**" + axis.Label + " (기준)**" : axis.SameAs(PaceAssumptions.Fastest) ? axis.Label + " (가장 빠름)"
                    : axis.SameAs(PaceAssumptions.Slowest) ? axis.Label + " (가장 느림)" : axis.Label;
                md.AppendLine("| " + label + " | " + Dur(a.TotalSeconds) + " | " + Dur(game) + " | " + Dur(read) + " | " + Dur(choose) + " | " + Dur(extra) + " |");
            }
            md.AppendLine();

            md.AppendLine("## 지점까지 걸린 시간");
            md.AppendLine();
            List<PaceMilestone> milestones = b.Milestones();
            if (milestones.Count == 0) md.AppendLine("(이 판에서 닿은 지점 없음)");
            else
            {
                var fast = new PaceAnalysis(steps, spf, PaceAssumptions.Fastest).Milestones();
                var slow = new PaceAnalysis(steps, spf, PaceAssumptions.Slowest).Milestones();
                md.AppendLine("| 지점 | 종류 | 날 | 게임 시각 | 기준 | 가장 빠름 | 가장 느림 |");
                md.AppendLine("|---|---|---|---|---|---|---|");
                for (int i = 0; i < milestones.Count; i++)
                {
                    PaceMilestone m = milestones[i];
                    md.AppendLine("| " + Cell(m.What) + " | " + m.Kind + " | " + m.Day + "일째 | " + Clock(m.Minutes) + " | " + Dur(m.Seconds) + " | " + Dur(fast[i].Seconds) + " | " + Dur(slow[i].Seconds) + " |");
                }
            }
            md.AppendLine();

            md.AppendLine("## 하루 시간 쓰임 (기준, 분)");
            md.AppendLine();
            md.Append("| 날 |");
            foreach (string c in PaceCategories.All) md.Append(' ').Append(PaceCategories.Label(c)).Append(" |");
            md.AppendLine(" 합 | 할 것 없음 몫 | 새로운 것 | 게임 시계가 간 분 |");
            md.Append("|---|");
            foreach (string _ in PaceCategories.All) md.Append("---|");
            md.AppendLine("---|---|---|---|");
            foreach (int day in b.ByDay.Keys)
            {
                md.Append("| ").Append(day).Append(" |");
                foreach (string c in PaceCategories.All) md.Append(' ').Append(Min(b.DayCategorySeconds(day, c))).Append(" |");
                int novel = steps.Where(s => s.Day == day).Sum(s => s.Novelties.Count);
                int gameMinutes = steps.Where(s => s.Day == day).Sum(s => s.GameMinutes);
                md.AppendLine(" " + Min(b.DaySeconds(day)) + " | " + (b.NothingToDoShare(day) * 100).ToString("0", CultureInfo.InvariantCulture) + "% | " + novel + " | " + gameMinutes + " |");
            }
            md.Append("| 전체 |");
            foreach (string c in PaceCategories.All) md.Append(' ').Append(Min(b.ByCategory.TryGetValue(c, out double v) ? v : 0)).Append(" |");
            md.AppendLine(" " + Min(b.TotalSeconds) + " | " + (b.TotalSeconds <= 0 ? 0 : (b.ByCategory.TryGetValue(PaceCategories.NothingToDo, out double idle) ? idle : 0) / b.TotalSeconds * 100).ToString("0", CultureInfo.InvariantCulture)
                + "% | " + steps.Sum(s => s.Novelties.Count) + " | " + steps.Sum(s => s.GameMinutes) + " |");
            md.AppendLine();
            md.AppendLine("게임 시계가 간 분에는 혼자 하기의 활동이 건너뛴 분이 든다(활동은 실제 시간을 쓰지 않고 시계를 건너뛴다).");
            md.AppendLine();

            md.AppendLine("## 가장 긴 지루한 구간 (새로운 것 사이 간격, 기준)");
            md.AppendLine();
            md.AppendLine("| 순위 | 시작 | 걸음 | 길이 | 분류별 | 많이 한 행동 |");
            md.AppendLine("|---|---|---|---|---|---|");
            int rank = 0;
            foreach (PaceGap g in b.LongestGaps(10))
            {
                string cats = string.Join(", ", g.ByCategory.Where(kv => kv.Value >= 0.5).OrderByDescending(kv => kv.Value).Select(kv => PaceCategories.Label(kv.Key) + " " + Dur(kv.Value)));
                string keys = string.Join(", ", g.TopKeys.Select(k => "`" + Cell(k.Key) + "` ×" + k.Count));
                md.AppendLine("| " + (++rank) + " | " + g.StartDay + "일째 " + Clock(g.StartMinutes) + " | " + g.FromStep + "→" + g.ToStep + (g.Open ? "(끝까지)" : "") + " | " + Dur(g.Seconds) + " | " + cats + " | " + keys + " |");
            }
            md.AppendLine();

            md.AppendLine("## 되풀이");
            md.AppendLine();
            md.AppendLine("연달아 같은 행동(긴 순):");
            md.AppendLine();
            foreach (PaceRun r in b.LongestRuns(10).Where(r => r.Count > 1)) md.AppendLine("- `" + Cell(r.Key) + "` ×" + r.Count + " (" + r.Day + "일째, 걸음 " + r.FromStep + "부터)");
            md.AppendLine();
            md.AppendLine("날마다 가장 많이 한 행동:");
            md.AppendLine();
            foreach (var kv in b.MostRepeatedPerDay()) md.AppendLine("- " + kv.Key + "일째: `" + Cell(kv.Value.Key) + "` ×" + kv.Value.Count);
            md.AppendLine();

            md.AppendLine("## 빈 날");
            md.AppendLine();
            List<int> empty = b.EmptyDays();
            md.AppendLine(empty.Count == 0 ? "새로운 것이 하나도 없는 날: 없음" : "새로운 것이 하나도 없는 날: " + empty.Count + "일(처음 " + empty[0] + "일째) — " + string.Join(", ", empty));
            md.AppendLine();

            md.AppendLine("## 가정 값");
            md.AppendLine();
            md.AppendLine("- 읽기(분당 글자, 공백 빼고): " + string.Join(", ", PaceAssumptions.Reading.Select(r => r.Name + " " + (r.CharsPerMinute > 0 ? r.CharsPerMinute.ToString("0", CultureInfo.InvariantCulture) : "읽지 않음"))));
            md.AppendLine("- 고르기(한 번 초): " + string.Join(", ", PaceAssumptions.Choice.Select(c => c.Name + " " + c.Seconds.ToString("0.#", CultureInfo.InvariantCulture))));
            md.AppendLine("- 미니게임(제한 시간의 몫): " + string.Join(", ", PaceAssumptions.MinigameShares.Select(m => m.ToString("0.##", CultureInfo.InvariantCulture))));
            md.AppendLine("- 기준: " + PaceAssumptions.Baseline.Label + "(설계 9절 1번). 다른 축 값은 이 세션이 대신 정한 것.");
            md.AppendLine("- 새로 나온 글자: 걸음 앞 관찰에서 HUD·미니게임을 뺀 떠 있는 화면의 글줄 중 앞 관찰에 없던 것. 다시 연 화면의 글은 다시 센다.");
            md.AppendLine("- 고르기: 떠 있는 화면에서 누르기·키·글자 치기 한 번이 한 번(걷기·미니게임·기다리기는 0).");
            md.AppendLine();
            md.AppendLine("## 넣지 못한 새로운 것의 종류");
            md.AppendLine();
            foreach (var (kind, why) in PaceNoveltyCatalog.NotIncluded) md.AppendLine("- " + kind + ": " + why);
            return md.ToString();
        }

        // ---------- csv ----------

        private static string Timeline(IReadOnlyList<PaceStep> steps, double spf, PaceAnalysis b)
        {
            var csv = new StringBuilder(steps.Count * 96 + 256);
            csv.AppendLine("step,day,clock,category,game_seconds,game_minutes,new_chars,choices,minigame_limit_seconds,human_seconds_baseline,real_seconds_baseline,by,top,key,novelties");
            PaceAxis axis = PaceAssumptions.Baseline;
            for (int i = 0; i < steps.Count; i++)
            {
                PaceStep s = steps[i];
                double game = s.GameSeconds(spf);
                Row(csv, s.Index.ToString(CultureInfo.InvariantCulture), s.Day.ToString(CultureInfo.InvariantCulture), Clock(s.Minutes), s.Category,
                    Num(game), s.GameMinutes.ToString(CultureInfo.InvariantCulture), s.NewChars.ToString(CultureInfo.InvariantCulture), s.Choices.ToString(CultureInfo.InvariantCulture),
                    Num(s.MinigameLimitSeconds), Num(b.Real[i] - game), Num(b.Real[i]), s.By, s.Top ?? string.Empty, s.Key, string.Join(" | ", s.Novelties.Select(n => n.ToString())));
            }
            return csv.ToString();
        }

        private static string Novelty(IReadOnlyList<PaceStep> steps, PaceAnalysis b)
        {
            var csv = new StringBuilder(1024);
            csv.AppendLine("step,day,clock,kind,what,milestone,real_seconds_baseline");
            for (int i = 0; i < steps.Count; i++)
                foreach (PaceNovelty n in steps[i].Novelties)
                    Row(csv, steps[i].Index.ToString(CultureInfo.InvariantCulture), steps[i].Day.ToString(CultureInfo.InvariantCulture), Clock(steps[i].Minutes), n.Kind, n.What,
                        n.Milestone ? "1" : "0", Num(b.Cumulative[i]));
            return csv.ToString();
        }

        private static void Row(StringBuilder csv, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) csv.Append(',');
                string c = cells[i] ?? string.Empty;
                if (c.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) csv.Append('"').Append(c.Replace("\"", "\"\"")).Append('"');
                else csv.Append(c);
            }
            csv.AppendLine();
        }

        // ---------- 모양 ----------

        private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Min(double seconds) => (seconds / 60.0).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>시간 길이: 1시간 넘으면 "1시간 05분", 아니면 "12분 30초".</summary>
        public static string Dur(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int total = (int)Math.Round(seconds);
            if (total >= 3600) return (total / 3600) + "시간 " + (total % 3600 / 60).ToString("00", CultureInfo.InvariantCulture) + "분";
            if (total >= 60) return (total / 60) + "분 " + (total % 60).ToString("00", CultureInfo.InvariantCulture) + "초";
            return total + "초";
        }

        public static string Clock(int minutes)
        {
            int m = ((minutes % 1440) + 1440) % 1440;
            return (m / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (m % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static string Cell(string s) => (s ?? string.Empty).Replace("|", "\\|");
    }
}
