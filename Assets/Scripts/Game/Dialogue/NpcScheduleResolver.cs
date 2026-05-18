using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;

namespace Rootborn.Game.Dialogue
{
    public readonly struct NpcScheduleResult
    {
        public readonly NpcDefinition Npc;
        public readonly LocationDefinition Location;
        public readonly DialogueDefinition Dialogue;
        public readonly string DialogueKey;
        public readonly string InteractionHintKey;
        public readonly string EventNoticeKey;
        public readonly NpcScheduleEntry Entry;

        public NpcScheduleResult(NpcDefinition npc, LocationDefinition location, DialogueDefinition dialogue, string dialogueKey, string interactionHintKey, string eventNoticeKey, NpcScheduleEntry entry)
        {
            Npc = npc;
            Location = location;
            Dialogue = dialogue;
            DialogueKey = string.IsNullOrEmpty(dialogueKey) ? string.Empty : dialogueKey;
            InteractionHintKey = string.IsNullOrEmpty(interactionHintKey) ? string.Empty : interactionHintKey;
            EventNoticeKey = string.IsNullOrEmpty(eventNoticeKey) ? string.Empty : eventNoticeKey;
            Entry = entry;
        }
    }

    public sealed class NpcScheduleResolver
    {
        private readonly GameDataLookupCache _cache;

        public NpcScheduleResolver(GameDataLookupCache cache)
        {
            _cache = cache;
        }

        public NpcScheduleResult Resolve(NpcScheduleDefinition schedule, NpcScheduleContext context)
        {
            if (schedule == null)
            {
                return default;
            }

            NpcScheduleEntry best = null;
            for (int i = 0; i < schedule.Entries.Count; i++)
            {
                var entry = schedule.Entries[i];
                if (entry == null || !entry.IsMatch(context)) continue;
                if (best == null || entry.Priority > best.Priority) best = entry;
            }

            var location = best != null && best.Location != null ? best.Location : schedule.FallbackLocation;
            var dialogue = best != null ? best.Dialogue : (schedule.Npc != null ? schedule.Npc.DefaultDialogue : null);
            return new NpcScheduleResult(
                schedule.Npc,
                location,
                dialogue,
                best != null ? best.DialogueKey : string.Empty,
                best != null ? best.InteractionHintKey : string.Empty,
                best != null ? best.EventNoticeKey : string.Empty,
                best);
        }

        public static string ResolveTimeSlotId(StudentLifeProgress progress)
        {
            int minutes = progress != null ? progress.TimeMinutes : 8 * 60;
            if (minutes <= 0 || minutes == 8 * 60) return "time.morning";
            if (minutes < 17 * 60) return "time.afternoon";
            if (minutes < 21 * 60) return "time.evening";
            return "time.night";
        }
    }
}
