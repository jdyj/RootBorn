using System;
using System.Collections.Generic;
using Rootborn.Core.Save;
using SaveNames = Rootborn.Core.Save.SaveFileNames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Quests
{
    /// <summary>
    /// One store per runtime session. Keeps one chain log per save slot and player, loads it from that player's file
    /// on first use, and never lets one player load another's records. A log bound by its owner is used as it is.
    /// Files go through <see cref="ISaveStore"/>; a file that exists but cannot be read throws instead of starting over.
    /// </summary>
    public sealed class QuestChainProgressStore
    {
        /// <summary>The game's default local player id (PlayerIdentity.DefaultPlayerId). It and "player" share the legacy file.</summary>
        public const string DefaultPlayerId = SaveNames.DefaultPlayerId;
        private const string LegacyPlayerId = "player";

        private readonly ISaveStore _storage;
        private readonly Dictionary<string, Dictionary<string, QuestChainLog>> _logs =
            new Dictionary<string, Dictionary<string, QuestChainLog>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, Action<StudentLifeProgress>>> _boundSaves =
            new Dictionary<string, Dictionary<string, Action<StudentLifeProgress>>>(StringComparer.Ordinal);

        public QuestChainProgressStore(ISaveStore storage)
        {
            _storage = storage;
        }

        /// <summary>Uses <paramref name="liveLog"/> for this player. Saving then only calls <paramref name="saveBoundLog"/>.</summary>
        public void BindLog(StudentLifeProgress student, QuestChainLog liveLog, Action<StudentLifeProgress> saveBoundLog = null)
        {
            if (student == null || liveLog == null) return;
            Players(_logs, student.SaveSlot)[student.PlayerId] = liveLog;
            Players(_boundSaves, student.SaveSlot)[student.PlayerId] = saveBoundLog;
        }

        public QuestChainLog GetOrCreate(IReadOnlyList<QuestChainDefinition> chains, StudentLifeProgress student)
        {
            if (student == null) return null;
            var players = Players(_logs, student.SaveSlot);
            // A log already held (or bound by its owner) is returned even without a chain list; the game returned null.
            if (players.TryGetValue(student.PlayerId, out var existing)) return existing;
            if (chains == null) return null;
            var log = new QuestChainLog(chains);
            var initial = log.ToSaveData();
            BindIdentity(initial, student);
            log.LoadFromSaveData(initial);
            if (_storage != null && _storage.TryRead<QuestChainLogSaveData>(student.SaveSlot, SaveNames.QuestChain(student.PlayerId), out var saved))
                log.LoadFromSaveData(FilterIdentity(saved, student));
            players.Add(student.PlayerId, log);
            return log;
        }

        /// <summary>
        /// Talking to a chain's start NPC accepts and tracks it, then the event is recorded and the log saved.
        /// Ignored for events without a key, for a log its owner bound, and for rewards that belong to another player.
        /// </summary>
        public void Record(IReadOnlyList<QuestChainDefinition> chains, StudentLifeProgress student, QuestEvent questEvent, RewardRuntimeContext rewards)
        {
            if (student == null || chains == null || string.IsNullOrEmpty(questEvent.EventKey)) return;
            if (IsBound(student)) return; // Only the owning aggregate may execute commands on this log.
            if (rewards.StudentLifeProgress != null && (rewards.StudentLifeProgress.SaveSlot != student.SaveSlot
                || rewards.StudentLifeProgress.PlayerId != student.PlayerId)) return;
            var log = GetOrCreate(chains, student);
            var context = new QuestRuntimeContext(rewards.QuestLog, null, rewards.KnowledgeProgress, rewards.StoryFlags, student);
            if (questEvent.Kind == QuestEventKind.Talk && !string.IsNullOrEmpty(questEvent.SubjectId))
            {
                for (int i = 0; i < chains.Count; i++)
                {
                    var chain = chains[i];
                    if (chain != null && chain.IsStartedByTalkingTo(questEvent.SubjectId) && log.GetState(chain) == QuestChainState.Available
                        && log.AcceptWithContext(chain, context)) log.Track(chain);
                }
            }

            log.RecordEventWithContext(questEvent, context, rewards);
            Save(student);
        }

        public void Save(StudentLifeProgress student)
        {
            if (student != null && _boundSaves.TryGetValue(student.SaveSlot, out var saves) && saves.TryGetValue(student.PlayerId, out var save))
            {
                save?.Invoke(student);
                return;
            }

            if (student == null || !_logs.TryGetValue(student.SaveSlot, out var players) || !players.TryGetValue(student.PlayerId, out var log)) return;
            _storage?.Write(student.SaveSlot, SaveNames.QuestChain(student.PlayerId), log.ToSaveData());
        }

        public void Clear()
        {
            _logs.Clear();
            _boundSaves.Clear();
        }

        private bool IsBound(StudentLifeProgress student) => _boundSaves.TryGetValue(student.SaveSlot, out var saves) && saves.ContainsKey(student.PlayerId);

        private static Dictionary<string, T> Players<T>(Dictionary<string, Dictionary<string, T>> bySlot, string saveSlot)
        {
            if (!bySlot.TryGetValue(saveSlot, out var players))
            {
                players = new Dictionary<string, T>(StringComparer.Ordinal);
                bySlot.Add(saveSlot, players);
            }

            return players;
        }

        private static void BindIdentity(QuestChainLogSaveData saved, StudentLifeProgress student)
        {
            foreach (var record in saved.Chains)
            {
                record.SaveSlot = student.SaveSlot;
                record.PlayerId = student.PlayerId;
            }
        }

        // Exact identity, or (for the default player only) old records written before identities were saved.
        private static QuestChainLogSaveData FilterIdentity(QuestChainLogSaveData saved, StudentLifeProgress student)
        {
            var matching = new List<QuestChainProgressSaveData>();
            if (saved?.Chains != null)
            {
                foreach (var record in saved.Chains)
                {
                    if (record == null) continue;
                    bool exact = record.SaveSlot == student.SaveSlot && record.PlayerId == student.PlayerId;
                    bool legacy = (student.PlayerId == DefaultPlayerId || student.PlayerId == LegacyPlayerId)
                        && (string.IsNullOrEmpty(record.SaveSlot) || record.SaveSlot == "default" || record.SaveSlot == student.SaveSlot)
                        && (string.IsNullOrEmpty(record.PlayerId) || record.PlayerId == LegacyPlayerId || record.PlayerId == DefaultPlayerId);
                    if (!exact && !legacy) continue;
                    record.SaveSlot = student.SaveSlot;
                    record.PlayerId = student.PlayerId;
                    matching.Add(record);
                }
            }

            return new QuestChainLogSaveData { Chains = matching.ToArray() };
        }
    }
}
