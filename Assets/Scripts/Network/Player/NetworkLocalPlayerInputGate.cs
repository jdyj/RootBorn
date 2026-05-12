using Rootborn.Game.Player;
using Unity.Netcode;
using UnityEngine;

namespace Rootborn.Network.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkLocalPlayerInputGate : NetworkBehaviour
    {
        private PlayerController _controller;
        private GatherInteractor _gatherInteractor;
        private PlayerInteractionRouter _interactionRouter;

        private void Awake()
        {
            EnsureInputComponents();
            ResolveComponents();
        }

        private void Update()
        {
            EnsureInputComponents();
            bool hadController = _controller != null;
            bool hadGather = _gatherInteractor != null;
            bool hadRouter = _interactionRouter != null;
            ResolveComponents();
            bool allowLocalInput = ShouldAllowLocalInput();
            if ((!hadController && _controller != null) || (!hadGather && _gatherInteractor != null) || (!hadRouter && _interactionRouter != null) || NeedsAuthorityReapply(allowLocalInput))
            {
                ApplyInputAuthority();
            }
        }

        public override void OnNetworkSpawn()
        {
            ApplyInputAuthority();
        }

        public override void OnGainedOwnership()
        {
            ApplyInputAuthority();
        }

        public override void OnLostOwnership()
        {
            ApplyInputAuthority();
        }

        private void EnsureInputComponents()
        {
            if (gameObject.GetComponent<GatherInteractor>() == null)
            {
                gameObject.AddComponent<GatherInteractor>();
            }

            if (gameObject.GetComponent<PlayerInteractionRouter>() == null)
            {
                gameObject.AddComponent<PlayerInteractionRouter>();
            }
        }

        private void ResolveComponents()
        {
            if (_controller == null) _controller = GetComponent<PlayerController>();
            if (_gatherInteractor == null) _gatherInteractor = GetComponent<GatherInteractor>();
            if (_interactionRouter == null) _interactionRouter = GetComponent<PlayerInteractionRouter>();
        }

        private bool NeedsAuthorityReapply(bool allowLocalInput)
        {
            return (_controller != null && _controller.enabled != allowLocalInput) ||
                   (_gatherInteractor != null && _gatherInteractor.enabled != allowLocalInput) ||
                   (_interactionRouter != null && _interactionRouter.enabled != allowLocalInput);
        }

        private void ApplyInputAuthority()
        {
            EnsureInputComponents();
            ResolveComponents();
            bool allowLocalInput = ShouldAllowLocalInput();
            if (_controller != null) _controller.enabled = allowLocalInput;
            if (_gatherInteractor != null) _gatherInteractor.enabled = allowLocalInput;
            if (_interactionRouter != null) _interactionRouter.enabled = allowLocalInput;
            Debug.Log($"[ROOTBORN] Network input gate owner={OwnerClientId} local={NetworkManager.Singleton?.LocalClientId} isOwner={IsOwner} allowLocalInput={allowLocalInput}");
        }

        private bool ShouldAllowLocalInput()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !IsSpawned)
            {
                return true;
            }

            return IsOwner;
        }
    }
}
