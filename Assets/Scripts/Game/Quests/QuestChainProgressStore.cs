using System;
using System.Collections.Generic;
using System.Text;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    /// <summary>Own one store for a runtime session. Logs and files are isolated by save slot and player.</summary>
    public sealed class QuestChainProgressStore
    {
        private readonly Dictionary<string, Dictionary<string, QuestChainLog>> _logs =
            new Dictionary<string, Dictionary<string, QuestChainLog>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, Action<StudentLifeProgress>>> _boundSaves =
            new Dictionary<string, Dictionary<string, Action<StudentLifeProgress>>>(StringComparer.Ordinal);

        public void BindLog(StudentLifeProgress student, QuestChainLog liveLog, Action<StudentLifeProgress> saveBoundLog = null)
        {
            if (student == null || liveLog == null) return;
            if (!_logs.TryGetValue(student.SaveSlot, out var players))
            {
                players = new Dictionary<string, QuestChainLog>(StringComparer.Ordinal);
                _logs.Add(student.SaveSlot, players);
            }
            players[student.PlayerId] = liveLog;
            if (!_boundSaves.TryGetValue(student.SaveSlot, out var saves))
            {
                saves = new Dictionary<string, Action<StudentLifeProgress>>(StringComparer.Ordinal);
                _boundSaves.Add(student.SaveSlot, saves);
            }
            saves[student.PlayerId] = saveBoundLog;
        }

        public QuestChainLog GetOrCreate(GameDataRegistry registry, StudentLifeProgress student)
        {
            if (registry == null || student == null) return null;
            if (!_logs.TryGetValue(student.SaveSlot, out var players))
            {
                players = new Dictionary<string, QuestChainLog>(StringComparer.Ordinal);
                _logs.Add(student.SaveSlot, players);
            }
            if (players.TryGetValue(student.PlayerId, out var existing)) return existing;
            var log = new QuestChainLog(registry.QuestChains);
            var initial = log.ToSaveData();
            BindIdentity(initial, student);
            log.LoadFromSaveData(initial);
            string json = new SaveService(student.SaveSlot).ReadJson(FileNameFor(student.PlayerId));
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var saved = JsonUtility.FromJson<QuestChainLogSaveData>(json);
                    log.LoadFromSaveData(FilterIdentity(saved, student));
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning("[ROOTBORN] Quest chain save could not be read: " + exception.Message);
                }
            }
            players.Add(student.PlayerId, log);
            return log;
        }

        public void Record(GameDataRegistry registry, StudentLifeProgress student, QuestEvent questEvent, RewardRuntimeContext rewards)
        {
            if (student == null || registry == null || string.IsNullOrEmpty(questEvent.EventKey)) return;
            if (IsBound(student)) return; // Only the owning aggregate may execute commands on this log.
            if (rewards.StudentLifeProgress != null && (rewards.StudentLifeProgress.SaveSlot != student.SaveSlot
                || rewards.StudentLifeProgress.PlayerId != student.PlayerId)) return;
            var log = GetOrCreate(registry, student);
            var context = new QuestRuntimeContext(rewards.QuestLog, null, rewards.KnowledgeProgress, rewards.StoryFlags, student);
            if (questEvent.Kind == QuestEventKind.Talk && questEvent.Npc != null)
            {
                var chains = registry.QuestChains;
                for (int i = 0; chains != null && i < chains.Length; i++)
                {
                    var chain = chains[i];
                    if (chain != null && chain.StartNpc == questEvent.Npc && log.GetState(chain) == QuestChainState.Available
                        && log.AcceptWithContext(chain, context)) log.Track(chain);
                }
            }
            log.RecordEventWithContext(questEvent, context, rewards);
            Save(student);
        }

        public void Save(StudentLifeProgress student)
        {
            if (student != null && _boundSaves.TryGetValue(student.SaveSlot, out var saves)
                && saves.TryGetValue(student.PlayerId, out var save))
            {
                save?.Invoke(student);
                return;
            }
            if (student == null || !_logs.TryGetValue(student.SaveSlot, out var players)
                || !players.TryGetValue(student.PlayerId, out var log)) return;
            new SaveService(student.SaveSlot).WriteJson(FileNameFor(student.PlayerId), JsonUtility.ToJson(log.ToSaveData(), true));
        }

        public void Clear() { _logs.Clear(); _boundSaves.Clear(); }

        private bool IsBound(StudentLifeProgress student) => _boundSaves.TryGetValue(student.SaveSlot, out var saves)
            && saves.ContainsKey(student.PlayerId);

        private static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId || playerId == "player")
                return "quest-chain-progress.json";
            // Encoding every UTF-8 byte avoids collisions such as player:a versus player?a.
            var bytes = Encoding.UTF8.GetBytes(playerId);
            var name = new StringBuilder("quest-chain-progress-");
            for (int i = 0; i < bytes.Length; i++) name.Append(bytes[i].ToString("x2"));
            return name.Append(".json").ToString();
        }

        private static void BindIdentity(QuestChainLogSaveData saved, StudentLifeProgress student)
        {
            foreach (var record in saved.Chains)
            {
                record.SaveSlot = student.SaveSlot;
                record.PlayerId = student.PlayerId;
            }
        }

        private static QuestChainLogSaveData FilterIdentity(QuestChainLogSaveData saved, StudentLifeProgress student)
        {
            var matching = new List<QuestChainProgressSaveData>();
            if (saved?.Chains != null) foreach (var record in saved.Chains)
            {
                if (record == null) continue;
                bool exact = record.SaveSlot == student.SaveSlot && record.PlayerId == student.PlayerId;
                bool legacy = (student.PlayerId == PlayerIdentity.DefaultPlayerId || student.PlayerId == "player")
                    && (string.IsNullOrEmpty(record.SaveSlot) || record.SaveSlot == "default" || record.SaveSlot == student.SaveSlot)
                    && (string.IsNullOrEmpty(record.PlayerId) || record.PlayerId == "player" || record.PlayerId == PlayerIdentity.DefaultPlayerId);
                if (!exact && !legacy) continue;
                record.SaveSlot = student.SaveSlot;
                record.PlayerId = student.PlayerId;
                matching.Add(record);
            }
            return new QuestChainLogSaveData { Chains = matching.ToArray() };
        }
    }
}
