using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rootborn.Screens;

namespace Rootborn.Simulation
{
    /// <summary>
    /// 한 판의 기록을 output/simulation/&lt;이름&gt;/에 쓴다: play.txt(정책·씨앗·설정·행동 이름 — 재생 도우미가 읽는다), summary.md(넓이와 결과),
    /// failure.md(실패했을 때). 다시 돌려 볼 때는 <see cref="ReadKeys"/>로 행동 이름을 읽어 ReplayPolicy에 넘긴다.
    /// </summary>
    public static class PlayRecord
    {
        public static string Directory(string repoRoot, string name) => Path.Combine(repoRoot, "output", "simulation", name);

        public static string Write(PlayResult result, string repoRoot, PlaySettings settings, IEnumerable<string> registeredScreens, PlayAtlas atlas)
        {
            string dir = Directory(repoRoot, result.Name);
            System.IO.Directory.CreateDirectory(dir);
            var play = new StringBuilder();
            play.AppendLine("# policy " + result.Policy);
            play.AppendLine("# seed " + result.Seed.ToString(CultureInfo.InvariantCulture));
            play.AppendLine("# columns: 걸음, 고른 쪽, 행동, (참고) 날·시각·맵·맨 위 화면");
            play.AppendLine("# days " + settings.Days + " steps-per-day " + settings.StepsPerDay + " max-steps " + settings.MaxSteps);
            play.AppendLine("# columns: 걸음, 고른 쪽, 행동, (참고) 날·시각·맵·맨 위 화면");
            foreach (PlayStep s in result.Steps)
                play.AppendLine(s.Index.ToString(CultureInfo.InvariantCulture) + "\t" + s.By + "\t" + s.Key + "\t"
                    + s.Day + "일 " + s.Minutes + "분 " + s.Map + " " + (s.Top ?? "-"));
            File.WriteAllText(Path.Combine(dir, "play.txt"), play.ToString());
            File.WriteAllText(Path.Combine(dir, "summary.md"), Summary(result, registeredScreens, atlas));
            string failure = Path.Combine(dir, "failure.md");
            if (result.Failure != null) File.WriteAllText(failure, "# " + result.Failure.What + "\n\n```\n" + result.Failure.Report + "\n```\n");
            else if (File.Exists(failure)) File.Delete(failure);
            result.OutputDir = dir;
            return dir;
        }

        /// <summary>play.txt의 행동 이름(주석 줄을 빼고 셋째 칸).</summary>
        public static List<string> ReadKeys(string playFile)
        {
            var keys = new List<string>();
            foreach (string line in File.ReadAllLines(playFile))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split('\t');
                if (parts.Length >= 3) keys.Add(parts[2]);
            }
            return keys;
        }

        public static (string Policy, int Seed) ReadHeader(string playFile)
        {
            string policy = null;
            int seed = 0;
            foreach (string line in File.ReadLines(playFile))
            {
                if (line.StartsWith("# policy ", StringComparison.Ordinal)) policy = line.Substring(9).Trim();
                else if (line.StartsWith("# seed ", StringComparison.Ordinal)) seed = int.Parse(line.Substring(7).Trim(), CultureInfo.InvariantCulture);
                else if (line.Length > 0 && line[0] != '#') break;
            }
            return (policy, seed);
        }

        public static string Summary(PlayResult r, IEnumerable<string> registeredScreens, PlayAtlas atlas)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 플레이 " + r.Name);
            sb.AppendLine();
            sb.AppendLine("- 정책 `" + r.Policy + "`, 씨앗 " + r.Seed + ", 걸음 " + r.Steps.Count + ", 프레임 " + r.Frames + ", 가장 긴 걸음 " + r.MaxFramesInStep + "프레임(`" + r.MaxFramesStep + "`)");
            sb.AppendLine("- 넘긴 날 " + r.DaysCompleted + ", 다시 읽기 검사 " + r.Reloads + "번, 가만히 선 프레임 할당 최대 " + (r.MaxIdleFrameBytes < 0 ? "(재지 않음)" : r.MaxIdleFrameBytes.ToString("0") + "바이트"));
            sb.AppendLine("- 끝낸 퀘스트 " + r.QuestsCompleted + ", 끝낸 체인 단계 " + r.ChainStepsCompleted + ", 끝낸 체인 " + r.ChainsCompleted);
            sb.AppendLine("- 체인 위치: " + (r.LastChainStep ?? "(없음)"));
            if (r.FinalState.Count > 0) sb.AppendLine("- 끝 상태: " + string.Join(" / ", r.FinalState.Take(3)));
            sb.AppendLine("- 결과: " + (r.Failure == null ? "통과" : "실패 — " + r.Failure.What.Split('\n')[0]));
            sb.AppendLine();
            sb.AppendLine("## 지나간 화면 (" + r.ScreensOpened.Count + ")");
            sb.AppendLine(string.Join(", ", r.ScreensOpened));
            sb.AppendLine();
            sb.AppendLine("## 상호작용 종류 (" + r.InteractionKinds.Count + ")");
            sb.AppendLine(string.Join(", ", r.InteractionKinds));
            sb.AppendLine();
            sb.AppendLine("## 맵 (" + r.Maps.Count + ")");
            sb.AppendLine(string.Join(", ", r.Maps));
            sb.AppendLine();
            sb.AppendLine("## 열지 못한 화면");
            foreach (var (screen, why) in Unopened(r, registeredScreens, atlas)) sb.AppendLine("- " + screen + ": " + why);
            if (r.Notes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 메모 (" + r.Notes.Count + ")");
                foreach (var g in r.Notes.GroupBy(n => n).OrderByDescending(g => g.Count()).Take(30)) sb.AppendLine("- " + g.Key + (g.Count() > 1 ? " ×" + g.Count() : ""));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 열지 못한 화면과 그 까닭: 그 화면을 여는 상호작용 종류가 지도 데이터에 없으면 "아직 닿을 수 없음(지점 없음)", 아니면 "이 판에서 안 열림".
        /// </summary>
        public static IEnumerable<(string Screen, string Why)> Unopened(PlayResult r, IEnumerable<string> registeredScreens, PlayAtlas atlas)
        {
            ScreenRegistry registry = ScreenCatalog.Create();
            var reachableByKind = new HashSet<string>(StringComparer.Ordinal);
            foreach (string kind in atlas.AllInteractionKinds())
                if (registry.TryGetByInteraction(kind, out string id)) reachableByKind.Add(id);
            foreach (string screen in registeredScreens.OrderBy(s => s, StringComparer.Ordinal))
            {
                if (r.ScreensOpened.Contains(screen)) continue;
                string why = OpenedByMissingKind(registry, screen, atlas) ? "아직 닿을 수 없음 — 그 화면을 여는 상호작용 지점이 지도 데이터에 없다" : "이 판에서 열리지 않음";
                yield return (screen, why);
            }
        }

        private static readonly string[] KnownKinds = { "exploration", "career-practice", "facility", "npc", "daily-plan-board", "location-activity", "location-visit", "student-day-end", "part-time-work" };

        private static bool OpenedByMissingKind(ScreenRegistry registry, string screen, PlayAtlas atlas)
        {
            var present = atlas.AllInteractionKinds();
            // 등록 표는 종류 → 화면만 알려 준다. 화면 쌓기의 등록 상수에 있는 종류 이름들로 거꾸로 찾는다.
            foreach (string kind in KnownKinds.Concat(new[] { Rootborn.Screens.AdultFacility.FacilityScreen.InteractionKind }))
                if (registry.TryGetByInteraction(kind, out string id) && id == screen && !present.Contains(kind)) return true;
            return false;
        }
    }
}
