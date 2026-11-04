namespace Rootborn.Game.StudentLife
{
    public sealed partial class LifeActivityRunner
    {
        internal bool TryPerformReserved(LifeActivityDefinition activity, StudentLifeProgress progress, string requestId, out LifeActivityResult result)
        {
            result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, progress?.SaveSlot, progress?.PlayerId, activity?.Id, requestId);
            var pending = progress?.PendingMinigame;
            if (activity == null || pending == null || pending.Kind != StudentMinigameKind.Practice || pending.RequestId != requestId || progress.HasAppliedRequest(requestId)) return false;
            var before = StudentLifeDeltaSnapshot.Capture(progress);
            activity.ApplyEffects(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(activity.Id, before.BuildResultLogs(activity.Id, progress));
            result = new LifeActivityResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, activity.Id, requestId);
            return true;
        }
    }
}
