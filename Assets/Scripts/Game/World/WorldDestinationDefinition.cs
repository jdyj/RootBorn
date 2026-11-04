using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.World
{
    [CreateAssetMenu(menuName = "Rootborn/World/Destination")]
    public sealed class WorldDestinationDefinition : ScriptableObject
    {
        [SerializeField] private string _sceneName;
        [SerializeField] private string[] _spawnIds = Array.Empty<string>();
        public string SceneName => _sceneName;
        public IReadOnlyList<string> SpawnIds => _spawnIds;
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_sceneName) || _spawnIds == null || _spawnIds.Length == 0) return false;
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (var spawn in _spawnIds)
                    if (string.IsNullOrWhiteSpace(spawn) || !unique.Add(spawn)) return false;
                return true;
            }
        }
        public bool ContainsSpawn(string spawnId)
        {
            if (!IsValid || string.IsNullOrWhiteSpace(spawnId)) return false;
            foreach (var spawn in _spawnIds) if (string.Equals(spawn, spawnId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
