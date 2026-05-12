using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;

namespace Rootborn.Game.Player
{
    public static class PlayerGlobalState
    {
        private static readonly Dictionary<string, Inventory> s_inventoriesByPlayer = new Dictionary<string, Inventory>();
        private static readonly Dictionary<string, QuestLog> s_questLogsByPlayer = new Dictionary<string, QuestLog>();

        public static Inventory TryGetInventoryForActiveSaveSlot()
        {
            return TryGetInventoryForActiveSaveSlot(PlayerIdentity.DefaultPlayerId);
        }

        public static Inventory TryGetInventoryForActiveSaveSlot(string playerId)
        {
            string slotId = ActiveSaveContext.SlotId;
            if (string.IsNullOrEmpty(slotId))
            {
                return null;
            }

            return GetInventory(slotId, playerId);
        }

        public static Inventory GetInventory(string saveSlot, string playerId)
        {
            string key = MakePlayerStateKey(saveSlot, playerId);
            if (!s_inventoriesByPlayer.TryGetValue(key, out var inventory))
            {
                inventory = new Inventory();
                s_inventoriesByPlayer.Add(key, inventory);
            }

            return inventory;
        }

        public static QuestLog GetQuestLog(string saveSlot, string playerId, QuestDefinition[] quests)
        {
            string key = MakePlayerStateKey(saveSlot, playerId);
            if (!s_questLogsByPlayer.TryGetValue(key, out var questLog))
            {
                questLog = new QuestLog(quests);
                s_questLogsByPlayer.Add(key, questLog);
            }

            return questLog;
        }

        public static void HandleActiveSaveContextChanged()
        {
            ClearRuntimeState();
        }

        public static void ClearForTests()
        {
            ClearRuntimeState();
        }

        private static void ClearRuntimeState()
        {
            s_inventoriesByPlayer.Clear();
            s_questLogsByPlayer.Clear();
        }

        private static string MakePlayerStateKey(string saveSlot, string playerId)
        {
            string normalizedSaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string normalizedPlayerId = string.IsNullOrEmpty(playerId) ? PlayerIdentity.DefaultPlayerId : playerId;
            return normalizedSaveSlot + "|" + normalizedPlayerId;
        }
    }
}
