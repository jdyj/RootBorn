using UnityEngine;

namespace Rootborn.Game.Player
{
    public sealed class PlayerIdentity : MonoBehaviour
    {
        public const string DefaultPlayerId = "local-player";

        [SerializeField] private string _playerId = DefaultPlayerId;
        [SerializeField] private ulong _clientId;

        public string PlayerId => string.IsNullOrEmpty(_playerId) ? DefaultPlayerId : _playerId;
        public ulong ClientId => _clientId;

        public void Configure(string playerId, ulong clientId = 0UL)
        {
            _playerId = string.IsNullOrEmpty(playerId) ? DefaultPlayerId : playerId;
            _clientId = clientId;
        }
    }
}
