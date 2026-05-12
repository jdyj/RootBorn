using System;
using System.Collections.Generic;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;

namespace Rootborn.Network.Quests
{
    public readonly struct QuestNetworkStateBroadcast
    {
        public readonly ulong ClientId;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string QuestId;
        public readonly QuestState State;
        public readonly QuestStateChangeKind Kind;
        public readonly int ObjectiveIndex;
        public readonly int ObjectiveCount;

        public QuestNetworkStateBroadcast(
            ulong clientId,
            string saveSlot,
            string playerId,
            string questId,
            QuestState state,
            QuestStateChangeKind kind,
            int objectiveIndex,
            int objectiveCount)
        {
            ClientId = clientId;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? PlayerIdentity.DefaultPlayerId : playerId;
            QuestId = string.IsNullOrEmpty(questId) ? string.Empty : questId;
            State = state;
            Kind = kind;
            ObjectiveIndex = objectiveIndex;
            ObjectiveCount = objectiveCount;
        }
    }

    public sealed class QuestNetworkStateBroadcaster
    {
        private readonly bool _isServerAuthority;
        private readonly string _saveSlot;
        private readonly HashSet<ulong> _clients = new HashSet<ulong>();
        private readonly HashSet<string> _emittedKeys = new HashSet<string>();
        private QuestLog _questLog;
        private string _playerId = PlayerIdentity.DefaultPlayerId;

        public QuestNetworkStateBroadcaster(bool isServerAuthority, string saveSlot)
        {
            _isServerAuthority = isServerAuthority;
            _saveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            LastSaveSlot = _saveSlot;
            LastPlayerId = string.Empty;
            LastState = QuestState.NotStarted.ToString();
        }

        public event Action<QuestNetworkStateBroadcast> OnBroadcast;

        public int BroadcastCount { get; private set; }
        public int RecipientDeliveryCount { get; private set; }
        public int StateChangeCount { get; private set; }
        public int DuplicateSuppressedCount { get; private set; }
        public string LastSaveSlot { get; private set; }
        public string LastPlayerId { get; private set; }
        public string LastState { get; private set; }

        public void RegisterClient(ulong clientId)
        {
            _clients.Add(clientId);
        }

        public void UnregisterClient(ulong clientId)
        {
            _clients.Remove(clientId);
        }

        public void Attach(QuestLog questLog)
        {
            AttachInternal(questLog, PlayerIdentity.DefaultPlayerId);
        }

        public void AttachForPlayer(QuestLog questLog, string playerId)
        {
            AttachInternal(questLog, playerId);
        }

        public void Detach()
        {
            if (_questLog != null)
            {
                _questLog.OnStateChanged -= HandleQuestStateChanged;
                _questLog = null;
            }
        }

        private void AttachInternal(QuestLog questLog, string playerId)
        {
            if (_questLog != null)
            {
                _questLog.OnStateChanged -= HandleQuestStateChanged;
            }

            _questLog = questLog;
            _playerId = string.IsNullOrEmpty(playerId) ? PlayerIdentity.DefaultPlayerId : playerId;
            if (_questLog != null)
            {
                _questLog.OnStateChanged += HandleQuestStateChanged;
            }
        }

        private void HandleQuestStateChanged(QuestStateChange change)
        {
            if (!_isServerAuthority || change.Quest == null)
            {
                return;
            }

            string questId = string.IsNullOrEmpty(change.Quest.Id) ? change.Quest.name : change.Quest.Id;
            string key = _saveSlot + "|" + _playerId + "|" + questId + "|" + change.Kind + "|" + change.State + "|" + change.ObjectiveIndex + "|" + change.ObjectiveCount + "|" + change.EventKey;
            if (!_emittedKeys.Add(key))
            {
                DuplicateSuppressedCount++;
                return;
            }

            StateChangeCount++;
            LastSaveSlot = _saveSlot;
            LastPlayerId = _playerId;
            LastState = change.State.ToString();

            foreach (ulong clientId in _clients)
            {
                var message = new QuestNetworkStateBroadcast(
                    clientId,
                    _saveSlot,
                    _playerId,
                    questId,
                    change.State,
                    change.Kind,
                    change.ObjectiveIndex,
                    change.ObjectiveCount);

                BroadcastCount++;
                RecipientDeliveryCount++;
                OnBroadcast?.Invoke(message);
            }
        }
    }
}
