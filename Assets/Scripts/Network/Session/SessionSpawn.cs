using Rootborn.Core.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Network.Session
{
    /// <summary>
    /// Where the server places a joining player: the Town spawn point single player uses, spread by join order
    /// (Rootborn.Core SpawnSpread). Without it Netcode spawned players at the prefab's origin, the far edge of the town map.
    /// </summary>
    public static class SessionSpawn
    {
        // Same fallback as the single player Town entry (TownPlayableBaselineRuntimeInstaller) when a scene has no spawn point.
        private static readonly Vector3 TownSquareFallback = new Vector3(5f, -4f, 0f);
        private const string SpawnPointName = "TownSpawnPoint";

        public static Vector3 Resolve(int joinIndex)
        {
            Vector3 origin = TownSquareFallback;
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == SpawnPointName) { origin = root.transform.position; break; }
            var offset = SpawnSpread.Offset(joinIndex);
            return new Vector3(origin.x + offset.X, origin.y + offset.Y, origin.z);
        }
    }
}
