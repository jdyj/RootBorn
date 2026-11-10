using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 생활 활동을 한 번 실행한다. 순서: 요청 확인 → 중복 요청 → 시간·체력·집중 → 요구 조건 → 적용.
    /// 거절되면 진행 상태를 바꾸지 않는다. 같은 requestId는 한 번만 적용된다.
    /// </summary>
    public sealed class LifeActivityRunner
    {
        public bool TryPerform(LifeActivity activity, StudentLifeProgress progress, string requestId, out LifeActivityResult result)
            => TryPerformCore(activity, null, false, progress, requestId, out result);

        public bool TryPerformChoice(LifeActivity activity, LifeChoice choice, StudentLifeProgress progress, string requestId, out LifeActivityResult result)
            => TryPerformCore(activity, choice, true, progress, requestId, out result);

        private static bool TryPerformCore(LifeActivity activity, LifeChoice choice, bool withChoice, StudentLifeProgress progress, string requestId, out LifeActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string activityId = activity == null ? string.Empty : activity.Id;
            string choiceId = choice == null ? string.Empty : choice.Id;

            LifeActivityResult Result(LifeActivityResultKind kind, string[] changedTraitIds) => withChoice
                ? new LifeActivityResult(kind, saveSlot, playerId, activityId, requestId, choiceId, changedTraitIds)
                : new LifeActivityResult(kind, saveSlot, playerId, activityId, requestId);

            result = Result(LifeActivityResultKind.InvalidRequest, Array.Empty<string>());
            if (activity == null || progress == null || string.IsNullOrEmpty(requestId) || (withChoice && choice == null)) return false;
            if (progress.HasAppliedRequest(requestId))
            {
                result = Result(LifeActivityResultKind.DuplicateRequest, Array.Empty<string>());
                return false;
            }
            if (!progress.CanUseTimePolicy(activity.TimePolicy) || !progress.CanSpend(activity.EnergyCost, activity.FocusCost))
            {
                result = Result(LifeActivityResultKind.InsufficientResources, Array.Empty<string>());
                return false;
            }
            if (!activity.HasSatisfiedRequirements(progress))
            {
                result = Result(LifeActivityResultKind.RequirementFailed, Array.Empty<string>());
                return false;
            }

            DeltaSnapshot before = DeltaSnapshot.Capture(progress);
            progress.TryUseTimePolicy(activity.TimePolicy);
            progress.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            activity.ApplyEffects(progress);
            if (withChoice) choice.ApplyEffects(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(activity.Id, before.BuildResultLogs(activity.Id, progress));
            result = Result(LifeActivityResultKind.Applied, withChoice ? before.BuildChangedTraitIds(progress) : Array.Empty<string>());
            return true;
        }

        /// <summary>적용 전 수치를 기억해 두었다가, 적용 후 달라진 것을 "활동:+id=변화량" 기록으로 만든다.</summary>
        private sealed class DeltaSnapshot
        {
            private readonly Dictionary<string, int> _traits = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _skills = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _relationships = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _statuses = new Dictionary<string, int>();
            private readonly HashSet<string> _careers = new HashSet<string>();

            public static DeltaSnapshot Capture(StudentLifeProgress progress)
            {
                var snapshot = new DeltaSnapshot();
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
}
