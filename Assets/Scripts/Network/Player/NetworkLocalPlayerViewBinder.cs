using Rootborn.Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace Rootborn.Network.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkLocalPlayerViewBinder : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            BindCameraIfLocalOwner();
        }

        public override void OnGainedOwnership()
        {
            BindCameraIfLocalOwner();
        }

        private void BindCameraIfLocalOwner()
        {
            if (!IsOwner)
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var follow = camera.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = camera.gameObject.AddComponent<CameraFollow>();
            }

            follow.SetTarget(transform);
            Debug.Log($"[ROOTBORN] Network local player view bound owner={OwnerClientId} local={NetworkManager.Singleton?.LocalClientId}");
        }
    }
}
