using System;
using System.Collections.Generic;
using Rootborn.Game.Common;

namespace Rootborn.Game.StudentLife
{
    public sealed partial class PartTimeWorkRunner
    {
        internal bool TryPerformReserved(PartTimeWorkDefinition work, StudentLifeProgress progress, Inventory inventory, string requestId, out PartTimeWorkResult result)
        {
            string location = work?.Workplace != null ? work.Workplace.LocationId : string.Empty;
            string npc = work?.Workplace != null ? work.Workplace.NpcId : string.Empty;
            result = new PartTimeWorkResult(LifeActivityResultKind.InvalidRequest, progress?.SaveSlot, progress?.PlayerId, work?.Id, work?.WorkplaceId, location, npc, requestId, null);
            var pending = progress?.PendingMinigame;
            if (work == null || inventory == null || pending == null || pending.Kind != StudentMinigameKind.PartTimeWork || pending.DefinitionId != work.Id || pending.RequestId != requestId || progress.HasAppliedRequest(requestId)) return false;
            var logs = new List<string>();
            var rewards = work.Rewards;
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward.Item == null || reward.Count <= 0) return false;
                logs.Add(PartTimeWorkLogCodec.EncodeDelta(work.Id, reward.Item.Id, reward.Count));
            }
            bool applied = inventory.TryAddAll(rewards, () =>
            {
                logs.AddRange(work.ApplyOutcomes(progress));
                progress.MarkRequestApplied(requestId);
                // Each settled session is a separate committed action; the day result totals them.
                progress.RecordActivityCompleted(work.Id, logs.ToArray(), true);
            });
            result = new PartTimeWorkResult(applied ? LifeActivityResultKind.Applied : LifeActivityResultKind.InsufficientResources, progress.SaveSlot, progress.PlayerId, work.Id, work.WorkplaceId, location, npc, requestId, applied ? logs.ToArray() : Array.Empty<string>());
            return applied;
        }
    }
}
