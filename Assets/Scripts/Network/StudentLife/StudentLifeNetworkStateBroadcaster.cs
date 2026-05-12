using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;

namespace Rootborn.Network.StudentLife
{
    public readonly struct StudentLifeNetworkStateBroadcast
    {
        public readonly ulong ClientId;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActivityId;
        public readonly string ChoiceId;
        public readonly string RequestId;
        public readonly LifeActivityResultKind Kind;

        public StudentLifeNetworkStateBroadcast(
            ulong clientId,
            string saveSlot,
            string playerId,
            string activityId,
            string choiceId,
            string requestId,
            LifeActivityResultKind kind)
        {
            ClientId = clientId;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActivityId = string.IsNullOrEmpty(activityId) ? string.Empty : activityId;
            ChoiceId = string.IsNullOrEmpty(choiceId) ? string.Empty : choiceId;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            Kind = kind;
        }
    }

    public sealed class StudentLifeNetworkStateBroadcaster
    {
        private readonly bool _isServerAuthority;
        private readonly string _saveSlot;
        private readonly HashSet<ulong> _clients = new HashSet<ulong>();
        private readonly HashSet<string> _emittedKeys = new HashSet<string>();

        public StudentLifeNetworkStateBroadcaster(bool isServerAuthority, string saveSlot)
        {
            _isServerAuthority = isServerAuthority;
            _saveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            LastSaveSlot = _saveSlot;
            LastPlayerId = string.Empty;
            LastActivityId = string.Empty;
            LastChoiceId = string.Empty;
        }

        public event Action<StudentLifeNetworkStateBroadcast> OnBroadcast;

        public int BroadcastCount { get; private set; }
        public int RecipientDeliveryCount { get; private set; }
        public int StateChangeCount { get; private set; }
        public int DuplicateSuppressedCount { get; private set; }
        public string LastSaveSlot { get; private set; }
        public string LastPlayerId { get; private set; }
        public string LastActivityId { get; private set; }
        public string LastChoiceId { get; private set; }

        public void RegisterClient(ulong clientId)
        {
            _clients.Add(clientId);
        }

        public void UnregisterClient(ulong clientId)
        {
            _clients.Remove(clientId);
        }

        public bool Publish(LifeActivityResult result)
        {
            if (!_isServerAuthority || result.Kind != LifeActivityResultKind.Applied)
            {
                return false;
            }

            string key = result.SaveSlot + "|" + result.PlayerId + "|" + result.ActivityId + "|" + result.ChoiceId + "|" + result.RequestId + "|" + result.Kind;
            if (!_emittedKeys.Add(key))
            {
                DuplicateSuppressedCount++;
                return false;
            }

            StateChangeCount++;
            LastSaveSlot = result.SaveSlot;
            LastPlayerId = result.PlayerId;
            LastActivityId = result.ActivityId;
            LastChoiceId = result.ChoiceId;

            foreach (ulong clientId in _clients)
            {
                var message = new StudentLifeNetworkStateBroadcast(
                    clientId,
                    result.SaveSlot,
                    result.PlayerId,
                    result.ActivityId,
                    result.ChoiceId,
                    result.RequestId,
                    result.Kind);

                BroadcastCount++;
                RecipientDeliveryCount++;
                OnBroadcast?.Invoke(message);
            }

            return true;
        }
    }
}
