using System;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    [Serializable]
    public sealed class WorldProgressSaveData
    {
        public int Version;
        public string SaveSlot;
        public string CurrentScene;
        public string CurrentEntryPointId;
        public string LastPortalDisplayName;
    }

    [DisallowMultipleComponent]
    public sealed class WorldPortal : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int PortalInteractionPriority = 100;
        [SerializeField] private string _displayName;
        [SerializeField] private string _destinationScene;
        [SerializeField] private string _destinationSpawnId;
        private Func<GameObject, string, string, string, bool> _travelHandler;

        public string DisplayName => _displayName;
        public string DestinationScene => _destinationScene;
        public string DestinationSpawnId => _destinationSpawnId;
        public int InteractionPriority => PortalInteractionPriority;
        public string InteractionPrompt => string.IsNullOrEmpty(_displayName) ? "[E] Enter" : "[E] Enter " + _displayName;
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.25f, 0f);
        public Transform InteractionTransform => transform;
        public void Bind(string displayName, string destinationScene, string destinationSpawnId)
        {
            _displayName = displayName; _destinationScene = destinationScene; _destinationSpawnId = destinationSpawnId;
        }
        public void ConfigureTravelHandler(Func<GameObject, string, string, string, bool> handler) => _travelHandler = handler;
        public bool CanTravel => !string.IsNullOrWhiteSpace(_destinationScene) && !string.IsNullOrWhiteSpace(_destinationSpawnId);
        public bool CanInteract(GameObject player) => player != null && CanTravel && !WorldTravelRuntime.HasPendingTravel;
        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player) || !GameDataRegistryProvider.TryGet(out var registry)
                || WorldEntryService.ResolvePair(registry.WorldTravelCatalog, _destinationScene, _destinationSpawnId) == null) return false;
            return (_travelHandler ?? WorldTravelRuntime.TryRequestTravel)(player, _destinationScene, _destinationSpawnId, _displayName);
        }
        public bool Travel(GameObject player) => TryInteract(player);

    }
}
