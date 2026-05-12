using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using Unity.Netcode;
using UnityEngine;

namespace Rootborn.Network.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerIdentity))]
    public sealed class NetworkPlayerIdentityBinder : NetworkBehaviour
    {
        private PlayerIdentity _identity;

        private void Awake()
        {
            _identity = GetComponent<PlayerIdentity>();
        }

        public override void OnNetworkSpawn()
        {
            if (_identity == null)
            {
                _identity = GetComponent<PlayerIdentity>();
            }

            string playerId = $"client-{OwnerClientId}";
            _identity.Configure(playerId, OwnerClientId);
            var progress = GetComponent<StudentLifeProgressComponent>();
            if (progress != null)
            {
                progress.SynchronizeWithPlayerIdentity();
            }
            Debug.Log($"[ROOTBORN] Network player spawned owner={OwnerClientId} local={NetworkManager.Singleton?.LocalClientId} isOwner={IsOwner} isServer={IsServer} playerId={_identity.PlayerId}");
        }
    }
}
