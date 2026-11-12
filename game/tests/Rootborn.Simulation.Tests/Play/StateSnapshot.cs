using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Game;

namespace Rootborn.Simulation
{
    /// <summary>
    /// 게임 한 판의 진행 상태를 줄 단위 글로(세이브에 남아야 하는 것: 날·시각·생활 수치, 특성·기술·관계·상태 값, 진로 힌트, 활동 기록, 가방,
    /// 퀘스트·체인 진행, 장래희망, 추적 목표, 집에 놓은 가구). 저장 뒤 새 GameSession으로 다시 읽은 것과 줄마다 비교한다.
    /// </summary>
    public static class StateSnapshot
    {
        public static List<string> Of(GameSession s)
        {
            var lines = new List<string>(64);
            var st = s.Student;
            // 시각은 시(時)까지: 흐르는 시계는 게임 1시간마다 저장한다(그 사이 분은 꺼지면 잃는다 — 흐르는 시간 설계, GameSession.AdvanceTime).
            lines.Add("day=" + st.CurrentDay + " hour=" + st.TimeMinutes / 60 + " state=" + st.DayState);
            lines.Add("energy=" + st.Energy + " focus=" + st.Focus + " stress=" + st.Stress + " exhausted=" + st.IsExhausted);
            lines.Add("time-points=" + s.TimePoints.Remaining + "/" + s.TimePoints.Budget);
            Values(lines, "trait", st.GetTraitIds(), st.GetTraitValue);
            Values(lines, "skill", st.GetSkillIds(), st.GetSkillValue);
            Values(lines, "relationship", st.GetRelationshipIds(), st.GetRelationshipValue);
            Values(lines, "status", st.GetStatusIds(), st.GetStatusValue);
            lines.Add("career-hints=" + string.Join(",", st.GetCareerHintIds().OrderBy(x => x, StringComparer.Ordinal)));
            lines.Add("activity-log=" + string.Join(",", st.GetActivityLogIds()));
            lines.Add("today=" + string.Join(",", st.GetTodayActivityIds()));
            foreach (ItemInventory.Slot slot in s.Inventory.Slots)
                if (slot.Item != null) lines.Add("item " + slot.Item.Id + " x" + slot.Count);
            foreach (QuestProgressSaveData q in s.QuestLog.ToSaveData().Quests.OrderBy(q => q.QuestId, StringComparer.Ordinal))
                lines.Add("quest " + q.QuestId + " " + q.State + " [" + string.Join(",", q.ObjectiveCounts) + "]");
            foreach (QuestChainProgressSaveData c in s.QuestChains.ToSaveData().Chains.OrderBy(c => c.ChainId, StringComparer.Ordinal))
                lines.Add("chain " + c.ChainId + " " + c.ChainState + " step=" + c.CurrentStepId + " tracked=" + c.IsTracked
                    + " done=[" + string.Join(",", c.CompletedStepIds) + "] progress=[" + string.Join(",", c.ObjectiveProgressValues)
                    + "] rewards=[" + string.Join(",", c.RewardClaimedIds) + "]");
            lines.Add("dream=" + s.CareerInterests.CurrentInterestId);
            if (s.House != null)
                foreach (var f in s.House.Placed.OrderBy(f => f.Definition.StableId, StringComparer.Ordinal).ThenBy(f => f.Anchor.X).ThenBy(f => f.Anchor.Y))
                    lines.Add("furniture " + f.Definition.StableId + " @" + f.Anchor.X + "," + f.Anchor.Y + " " + f.Direction);
            return lines;
        }

        /// <summary>집에 놓은 가구 줄이다(같이 쓰는 집은 방의 것이라 개인 다시 읽기 비교에서 뺀다).</summary>
        public static bool IsFurniture(string line) => line.StartsWith("furniture ", StringComparison.Ordinal);

        private static void Values(List<string> lines, string name, string[] ids, Func<string, int> value)
        {
            foreach (string id in ids.OrderBy(x => x, StringComparer.Ordinal))
                lines.Add(name + " " + id + "=" + value(id).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>두 상태의 다른 줄(앞은 지금 판, 뒤는 다시 읽은 판). 같으면 빈 목록.</summary>
        public static List<string> Diff(IReadOnlyList<string> live, IReadOnlyList<string> reloaded)
        {
            var a = new HashSet<string>(live, StringComparer.Ordinal);
            var b = new HashSet<string>(reloaded, StringComparer.Ordinal);
            var diff = new List<string>();
            foreach (string l in live) if (!b.Contains(l)) diff.Add("- " + l);
            foreach (string l in reloaded) if (!a.Contains(l)) diff.Add("+ " + l);
            return diff;
        }

        /// <summary>두 번 이상 나온 것(보상 기록에 같은 id가 겹치면 두 번 받은 것이다).</summary>
        public static List<string> Duplicates(IEnumerable<string> items)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var twice = new List<string>();
            foreach (string i in items) if (!seen.Add(i) && !twice.Contains(i)) twice.Add(i);
            return twice;
        }

        /// <summary>보상을 받은 기록: 체인의 받은 보상 id(겹치면 두 번 받은 것)와 보상을 받은 퀘스트.</summary>
        public static List<string> Claims(GameSession s)
        {
            var claims = new List<string>();
            foreach (QuestChainProgressSaveData c in s.QuestChains.ToSaveData().Chains)
                foreach (string id in c.RewardClaimedIds) claims.Add("chain:" + c.ChainId + ":" + id);
            foreach (QuestProgressSaveData q in s.QuestLog.ToSaveData().Quests)
                if (q.State == QuestStage.RewardClaimed) claims.Add("quest:" + q.QuestId);
            return claims;
        }
    }
}
