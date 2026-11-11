using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 활동 효과를 적용하기 전 수치를 기억해 두었다가, 적용 후 달라진 것을 변화 기록으로 만든다.
    /// 기록 모양: "활동:+id=변화량"(특성·기술·관계·상태, 0은 빼고 음수도 그대로), "활동:unlock=진로"(새로 열린 진로 힌트).
    /// 생활 활동 실행기와 예약 활동 정산이 같이 쓴다.
    /// </summary>
    internal sealed class ActivityDeltaSnapshot
    {
        private readonly Dictionary<string, int> _traits = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _skills = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _relationships = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _statuses = new Dictionary<string, int>();
        private readonly HashSet<string> _careers = new HashSet<string>();

        public static ActivityDeltaSnapshot Capture(StudentLifeProgress progress)
        {
            var snapshot = new ActivityDeltaSnapshot();
            Copy(snapshot._traits, progress.GetTraitIds(), progress.GetTraitValue);
            Copy(snapshot._skills, progress.GetSkillIds(), progress.GetSkillValue);
            Copy(snapshot._relationships, progress.GetRelationshipIds(), progress.GetRelationshipValue);
            Copy(snapshot._statuses, progress.GetStatusIds(), progress.GetStatusValue);
            foreach (string careerId in progress.GetCareerHintIds()) snapshot._careers.Add(careerId);
            return snapshot;
        }

        public string[] BuildChangedTraitIds(StudentLifeProgress progress)
        {
            var changed = new List<string>();
            foreach (string id in progress.GetTraitIds())
            {
                _traits.TryGetValue(id, out int before);
                if (progress.GetTraitValue(id) != before) changed.Add(id);
            }
            return changed.ToArray();
        }

        public string[] BuildResultLogs(string activityId, StudentLifeProgress progress)
        {
            var logs = new List<string>();
            string prefix = string.IsNullOrEmpty(activityId) ? "activity" : activityId;
            AddDeltaLogs(logs, prefix, progress.GetTraitIds(), progress.GetTraitValue, _traits);
            AddDeltaLogs(logs, prefix, progress.GetSkillIds(), progress.GetSkillValue, _skills);
            AddDeltaLogs(logs, prefix, progress.GetRelationshipIds(), progress.GetRelationshipValue, _relationships);
            AddDeltaLogs(logs, prefix, progress.GetStatusIds(), progress.GetStatusValue, _statuses);
            foreach (string careerId in progress.GetCareerHintIds())
                if (!_careers.Contains(careerId)) logs.Add(prefix + ":unlock=" + careerId);
            return logs.ToArray();
        }

        private static void Copy(Dictionary<string, int> target, string[] ids, Func<string, int> getter)
        {
            for (int i = 0; i < ids.Length; i++) target[ids[i]] = getter(ids[i]);
        }

        private static void AddDeltaLogs(List<string> logs, string prefix, string[] ids, Func<string, int> getter, Dictionary<string, int> beforeValues)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                beforeValues.TryGetValue(ids[i], out int before);
                int delta = getter(ids[i]) - before;
                if (delta != 0) logs.Add(prefix + ":+" + ids[i] + "=" + delta.ToString());
            }
        }
    }
}
