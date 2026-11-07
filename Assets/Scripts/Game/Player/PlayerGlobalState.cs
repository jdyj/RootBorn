using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

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
            PlayerQuestRuntime.ClearSessions();
            s_inventoriesByPlayer.Clear();
            s_questLogsByPlayer.Clear();
        }

        internal static string MakePlayerStateKey(string saveSlot, string playerId)
        {
            string normalizedSaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string normalizedPlayerId = string.IsNullOrEmpty(playerId) ? PlayerIdentity.DefaultPlayerId : playerId;
            return normalizedSaveSlot + "|" + normalizedPlayerId;
        }
    }

    /// <summary>
    /// The quests a player carries through every world scene (Town, House, School, interiors): one log per
    /// save slot and player, read from the slot's quest log file once and written again after every change.
    /// The Objective Journal (Tab) lists it; NPC dialogue accepts quests and claims rewards on it.
    /// </summary>
    public static class PlayerQuestRuntime
    {
        public const string DefaultLogFileName = "quest-log.json";
        private static readonly Dictionary<string, PlayerQuestSession> s_sessions = new Dictionary<string, PlayerQuestSession>();

        /// <summary>Raised after a session's quests or quest states changed (already saved when persistent).</summary>
        public static event Action<PlayerQuestSession> Changed;

        /// <summary>Increases on every quest change, so observers can compare a number instead of rescanning quests.</summary>
        public static int Revision { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            // Domain reload may be disabled: listeners from a previous run must not survive.
            Changed = null;
            ClearSessions();
        }

        public static string LogFileName(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId)
            {
                return DefaultLogFileName;
            }

            var chars = playerId.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
                {
                    chars[i] = '_';
                }
            }

            return "quest-log-" + new string(chars) + ".json";
        }

        public static string PlayerIdOf(GameObject player)
        {
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            return identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        /// <summary>The session for a slot and player. Without a slot it lives in memory only and never touches files.</summary>
        public static PlayerQuestSession Get(string saveSlot, string playerId, IReadOnlyList<QuestDefinition> quests)
        {
            string key = PlayerGlobalState.MakePlayerStateKey(saveSlot, playerId);
            if (s_sessions.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var questArray = ToArray(quests);
            var log = PlayerGlobalState.GetQuestLog(saveSlot, playerId, questArray);
            var session = new PlayerQuestSession(saveSlot, string.IsNullOrEmpty(playerId) ? PlayerIdentity.DefaultPlayerId : playerId, log, questArray);
            s_sessions.Add(key, session);
            return session;
        }

        /// <summary>The session for a slot and player, with quest definitions from the game data registry.</summary>
        public static bool TryGet(string saveSlot, string playerId, out PlayerQuestSession session)
        {
            if (!GameDataRegistryProvider.TryGet(out var registry) || registry == null)
            {
                session = null;
                return false;
            }

            session = Get(saveSlot, playerId, registry.Quests);
            return true;
        }

        public static bool TryGetActive(string playerId, out PlayerQuestSession session)
        {
            return TryGet(ActiveSaveContext.SlotId, playerId, out session);
        }

        public static bool TryGetActive(GameObject player, out PlayerQuestSession session)
        {
            return TryGetActive(PlayerIdOf(player), out session);
        }

        internal static void ClearSessions()
        {
            foreach (var session in s_sessions.Values)
            {
                session.Detach();
            }

            s_sessions.Clear();
            Revision++;
        }

        internal static void NotifyChanged(PlayerQuestSession session)
        {
            Revision++;
            Changed?.Invoke(session);
        }

        private static QuestDefinition[] ToArray(IReadOnlyList<QuestDefinition> quests)
        {
            if (quests == null)
            {
                return Array.Empty<QuestDefinition>();
            }

            var result = new QuestDefinition[quests.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = quests[i];
            }

            return result;
        }
    }

    /// <summary>One player's quest log in one save slot. Created only through <see cref="PlayerQuestRuntime"/>.</summary>
    public sealed class PlayerQuestSession
    {
        private readonly List<QuestDefinition> _quests = new List<QuestDefinition>();
        // Saved progress of quests this session does not list yet (e.g. a dream's linked quests before the dream is
        // chosen again). It is applied when the quest is added and written back on every save, so it is never lost.
        private readonly Dictionary<string, QuestProgressSaveData> _savedNotListed = new Dictionary<string, QuestProgressSaveData>(StringComparer.Ordinal);
        private bool _detached;

        internal PlayerQuestSession(string saveSlot, string playerId, QuestLog log, QuestDefinition[] quests)
        {
            SaveSlot = saveSlot ?? string.Empty;
            PlayerId = playerId;
            QuestLog = log;
            for (int i = 0; i < quests.Length; i++)
            {
                if (quests[i] != null && !Lists(quests[i].Id))
                {
                    _quests.Add(quests[i]);
                }
            }

            LoadOnce();
            QuestLog.OnStateChanged += HandleStateChanged;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public QuestLog QuestLog { get; }
        public IReadOnlyList<QuestDefinition> Quests => _quests;
        public bool IsPersistent => !string.IsNullOrEmpty(SaveSlot);

        /// <summary>True when the saved file could not be read. Nothing is written over it, and dialogue does not bind.</summary>
        public bool LoadFailed { get; private set; }

        /// <summary>Adds quests offered outside the registry list and restores any progress already saved for them.</summary>
        public bool AddQuests(IReadOnlyList<QuestDefinition> quests)
        {
            if (quests == null)
            {
                return false;
            }

            bool added = false;
            for (int i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id) || Lists(quest.Id))
                {
                    continue;
                }

                QuestLog.AddQuest(quest);
                _quests.Add(quest);
                added = true;
                if (_savedNotListed.TryGetValue(quest.Id, out var saved))
                {
                    _savedNotListed.Remove(quest.Id);
                    QuestLog.LoadFromSaveData(new QuestLogSaveData { Quests = new[] { saved } });
                }
            }

            if (added)
            {
                PlayerQuestRuntime.NotifyChanged(this);
            }

            return added;
        }

        public bool Save()
        {
            if (!IsPersistent || LoadFailed || _detached)
            {
                return false;
            }

            var data = QuestLog.ToSaveData();
            if (_savedNotListed.Count > 0)
            {
                var merged = new List<QuestProgressSaveData>(data.Quests);
                foreach (var saved in _savedNotListed.Values)
                {
                    if (!Contains(merged, saved.QuestId))
                    {
                        merged.Add(saved);
                    }
                }

                data.Quests = merged.ToArray();
            }

            new SaveService(SaveSlot).WriteJson(PlayerQuestRuntime.LogFileName(PlayerId), JsonUtility.ToJson(data, true));
            return true;
        }

        public RewardRuntimeContext RewardContext(
            Inventory inventory,
            StudentLifeProgress student = null,
            WorldStateProgress world = null,
            FurnitureUnlockProgress furniture = null,
            StoryFlagSet storyFlags = null)
        {
            return new RewardRuntimeContext(QuestLog, inventory, null, storyFlags ?? new StoryFlagSet(), student, world, furniture);
        }

        /// <summary>
        /// Grants every reward or nothing: the log checks inventory room for all rewards first and marks the quest
        /// claimed with them, so a repeated call, before or after reload, grants nothing. Saved right after claiming.
        /// </summary>
        public bool TryClaimReward(QuestDefinition quest, in RewardRuntimeContext context)
        {
            if (quest == null || !ReferenceEquals(context.QuestLog, QuestLog) || !QuestLog.ClaimReward(quest, in context))
            {
                return false;
            }

            Save();
            return true;
        }

        internal void Detach()
        {
            _detached = true;
            QuestLog.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(QuestStateChange change)
        {
            if (_detached)
            {
                return;
            }

            Save();
            PlayerQuestRuntime.NotifyChanged(this);
        }

        private void LoadOnce()
        {
            if (!IsPersistent)
            {
                return;
            }

            string json = new SaveService(SaveSlot).ReadJson(PlayerQuestRuntime.LogFileName(PlayerId));
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            QuestLogSaveData data;
            try
            {
                data = JsonUtility.FromJson<QuestLogSaveData>(json);
            }
            catch (ArgumentException)
            {
                data = null;
            }

            if (data == null || data.Quests == null)
            {
                LoadFailed = true;
                Debug.LogWarning("[ROOTBORN] Quest log could not be read and is left unchanged: slot=" + SaveSlot + " player=" + PlayerId);
                return;
            }

            for (int i = 0; i < data.Quests.Length; i++)
            {
                var saved = data.Quests[i];
                if (saved != null && !string.IsNullOrEmpty(saved.QuestId) && !Lists(saved.QuestId))
                {
                    _savedNotListed[saved.QuestId] = saved;
                }
            }

            QuestLog.LoadFromSaveData(data);
        }

        private bool Lists(string questId)
        {
            for (int i = 0; i < _quests.Count; i++)
            {
                if (_quests[i].Id == questId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(List<QuestProgressSaveData> entries, string questId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].QuestId == questId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
