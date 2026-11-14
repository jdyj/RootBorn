using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Rootborn.Simulation.Rooms
{
    /// <summary>
    /// 다시 들어온 뒤 진행 견주기에서 "앞으로 나아가는 것만" 허용하는 줄 비교(StateSnapshot의 quest·chain·skill 줄).
    /// - 퀘스트·체인: 허용할 때(나가 있는 동안 날이 실제로 바뀌었거나 방에 보낸 요청이 걸려 있었다)만 나아감을 허용한다 — 있던 것이 사라지지 않고,
    ///   퀘스트 단계가 뒤로 가지 않고(같은 단계면 목표 수가 줄지 않음), 체인은 끝난 것이 다시 열리지 않고 끝낸 걸음·받은 보상이 줄지 않으며,
    ///   나아가지 않은 체인은 걸음·추적·진행 수가 그대로(진행 수는 줄지 않음)다. 새로 생긴 것은 된다. 허용하지 않으면 줄이 같아야 한다.
    /// - 기술: 허용 표(기술 id → 늘어도 되는 최대 양)에 있는 기술만 0 이상 그 양 이하로 늘 수 있다. 나머지 기술은 같아야 한다.
    /// 화면·입력 근거가 아니다.
    /// </summary>
    public static class RejoinProgress
    {
        public static bool IsQuest(string line) => line.StartsWith("quest ", StringComparison.Ordinal);
        public static bool IsChain(string line) => line.StartsWith("chain ", StringComparison.Ordinal);
        public static bool IsSkill(string line) => line.StartsWith("skill ", StringComparison.Ordinal);
        public static bool Handles(string line) => IsQuest(line) || IsChain(line) || IsSkill(line);

        private static readonly string[] QuestOrder = { "NotStarted", "Active", "Completed", "RewardClaimed" };
        private static readonly HashSet<string> ChainEnded = new HashSet<string>(StringComparer.Ordinal) { "Completed", "Failed", "Expired" };

        /// <summary>before·after의 quest·chain·skill 줄을 견준 다른 점(없으면 빈 목록).</summary>
        public static List<string> Compare(IEnumerable<string> before, IEnumerable<string> after, bool progressMayMove, IReadOnlyDictionary<string, int> skillAllowance)
        {
            var diff = new List<string>();
            List<string> b = before.Where(Handles).ToList(), a = after.Where(Handles).ToList();
            CompareProgress(b.Where(IsQuest).ToList(), a.Where(IsQuest).ToList(), progressMayMove, Quest, diff);
            CompareProgress(b.Where(IsChain).ToList(), a.Where(IsChain).ToList(), progressMayMove, Chain, diff);
            CompareSkills(b.Where(IsSkill), a.Where(IsSkill), skillAllowance, diff);
            return diff;
        }

        private delegate string Forward(string before, string after);

        private static void CompareProgress(List<string> before, List<string> after, bool mayMove, Forward forward, List<string> diff)
        {
            if (!mayMove) { diff.AddRange(StateSnapshot.Diff(before, after)); return; }
            var now = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string l in after) now[IdOf(l)] = l;
            foreach (string l in before)
            {
                if (!now.TryGetValue(IdOf(l), out string later)) { diff.Add("사라짐: " + l); continue; }
                if (l == later) continue;
                string why = forward(l, later);
                if (why != null) diff.Add("뒤로 감(" + why + "): " + l + " → " + later);
            }
        }

        private static string IdOf(string line) => line.Split(' ')[1];

        // "quest <id> <단계> [수,수]"
        private static string Quest(string before, string after)
        {
            string[] x = before.Split(' '), y = after.Split(' ');
            int sb = Array.IndexOf(QuestOrder, x[2]), sa = Array.IndexOf(QuestOrder, y[2]);
            if (sb < 0 || sa < 0) return "모르는 단계";
            if (sa < sb) return "단계";
            if (sa > sb) return null;
            int[] cb = Numbers(Between(before, '[', ']')), ca = Numbers(Between(after, '[', ']'));
            if (cb.Length != ca.Length) return "목표 수 길이";
            for (int i = 0; i < cb.Length; i++) if (ca[i] < cb[i]) return "목표 수";
            return null;
        }

        // "chain <id> <상태> step=<걸음> tracked=<bool> done=[..] progress=[..] rewards=[..]"
        private static string Chain(string before, string after)
        {
            string[] x = before.Split(' '), y = after.Split(' ');
            string stateB = x[2], stateA = y[2];
            if (ChainEnded.Contains(stateB)) return "끝난 체인이 바뀜";
            if (stateA == "Available" && stateB != "Available") return "상태";
            List<string> doneB = Items(Field(before, "done=")), doneA = Items(Field(after, "done="));
            List<string> rewardsB = Items(Field(before, "rewards=")), rewardsA = Items(Field(after, "rewards="));
            if (doneB.Except(doneA, StringComparer.Ordinal).Any()) return "끝낸 걸음";
            if (rewardsB.Except(rewardsA, StringComparer.Ordinal).Any()) return "받은 보상";
            bool advanced = ChainEnded.Contains(stateA) || doneA.Count > doneB.Count || rewardsA.Count > rewardsB.Count || (stateB == "Available" && stateA != "Available");
            if (advanced) return null;
            // 나아가지 않았다: 걸음·추적은 그대로, 진행 수는 줄지 않는다(활성·추적·멈춤·막힘 사이의 상태 바뀜은 시간 조건으로 될 수 있다).
            if (Field(before, "step=") != Field(after, "step=")) return "걸음";
            if (Field(before, "tracked=") != Field(after, "tracked=")) return "추적";
            int[] pb = Numbers(Field(before, "progress=")), pa = Numbers(Field(after, "progress="));
            if (pb.Length != pa.Length) return "진행 수 길이";
            for (int i = 0; i < pb.Length; i++) if (pa[i] < pb[i]) return "진행 수";
            return null;
        }

        private static void CompareSkills(IEnumerable<string> before, IEnumerable<string> after, IReadOnlyDictionary<string, int> allowance, List<string> diff)
        {
            Dictionary<string, int> b = Skills(before), a = Skills(after);
            foreach (string id in b.Keys.Union(a.Keys).OrderBy(x => x, StringComparer.Ordinal))
            {
                int vb = b.TryGetValue(id, out int v1) ? v1 : 0, va = a.TryGetValue(id, out int v2) ? v2 : 0;
                if (va == vb) continue;
                int allowed = allowance != null && allowance.TryGetValue(id, out int n) ? n : 0;
                if (va < vb) diff.Add("기술이 줄었다: " + id + " " + vb + " → " + va);
                else if (va - vb > allowed)
                    diff.Add("기술이 걸려 있던 요청이 허용하는 것보다 자랐다: " + id + " " + vb + " → " + va + "(늘어도 되는 양 " + allowed + ")");
            }
        }

        // "skill <id>=<값>"
        private static Dictionary<string, int> Skills(IEnumerable<string> lines)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string l in lines)
            {
                int eq = l.LastIndexOf('=');
                map[l.Substring(6, eq - 6)] = int.Parse(l.Substring(eq + 1), CultureInfo.InvariantCulture);
            }
            return map;
        }

        private static string Field(string line, string name)
        {
            int at = line.IndexOf(" " + name, StringComparison.Ordinal);
            if (at < 0) return string.Empty;
            int start = at + 1 + name.Length;
            if (start < line.Length && line[start] == '[') return line.Substring(start, line.IndexOf(']', start) - start + 1);
            int end = line.IndexOf(' ', start);
            return end < 0 ? line.Substring(start) : line.Substring(start, end - start);
        }

        private static string Between(string line, char open, char close)
        {
            int s = line.LastIndexOf(open), e = line.LastIndexOf(close);
            return s < 0 || e < s ? string.Empty : line.Substring(s, e - s + 1);
        }

        private static List<string> Items(string bracketed)
        {
            string inner = bracketed.Trim('[', ']');
            return inner.Length == 0 ? new List<string>() : inner.Split(',').ToList();
        }

        private static int[] Numbers(string bracketed)
            => Items(bracketed).Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
    }
}
