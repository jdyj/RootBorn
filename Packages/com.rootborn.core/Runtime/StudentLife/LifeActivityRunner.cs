using System;

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

            var before = ActivityDeltaSnapshot.Capture(progress);
            progress.TryUseTimePolicy(activity.TimePolicy);
            progress.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            activity.ApplyEffects(progress);
            if (withChoice) choice.ApplyEffects(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(activity.Id, before.BuildResultLogs(activity.Id, progress));
            result = Result(LifeActivityResultKind.Applied, withChoice ? before.BuildChangedTraitIds(progress) : Array.Empty<string>());
            return true;
        }
    }
}
