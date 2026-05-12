using UnityEngine;

namespace Rootborn.Game.World
{
    [DisallowMultipleComponent]
    public sealed class WorldSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string _spawnId;

        public string SpawnId => _spawnId;

        public void Bind(string spawnId)
        {
            _spawnId = spawnId;
        }
    }
}
