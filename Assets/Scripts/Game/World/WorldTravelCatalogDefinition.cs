using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.World
{
    [CreateAssetMenu(menuName = "Rootborn/World/Travel Catalog")]
    public sealed class WorldTravelCatalogDefinition : ScriptableObject
    {
        [SerializeField] private WorldDestinationDefinition[] _destinations = Array.Empty<WorldDestinationDefinition>();
        [SerializeField] private WorldDestinationDefinition _newGameDestination;
        [SerializeField] private string _newGameSpawnId;
        public IReadOnlyList<WorldDestinationDefinition> Destinations => _destinations;
        public WorldDestinationDefinition NewGameDestination => _newGameDestination;
        public string NewGameSpawnId => _newGameSpawnId;
        public bool IsValid
        {
            get
            {
                if (_destinations == null || _destinations.Length == 0 || _newGameDestination == null) return false;
                var scenes = new HashSet<string>(StringComparer.Ordinal);
                bool openingRegistered = false;
                foreach (var destination in _destinations)
                {
                    if (destination == null || !destination.IsValid || !scenes.Add(destination.SceneName)) return false;
                    if (destination == _newGameDestination) openingRegistered = true;
                }
                return openingRegistered && _newGameDestination.ContainsSpawn(_newGameSpawnId);
            }
        }
        public WorldDestinationDefinition FindDestination(string sceneName)
        {
            if (!IsValid || string.IsNullOrWhiteSpace(sceneName)) return null;
            foreach (var destination in _destinations)
                if (string.Equals(destination.SceneName, sceneName, StringComparison.Ordinal)) return destination;
            return null;
        }
    }
}
