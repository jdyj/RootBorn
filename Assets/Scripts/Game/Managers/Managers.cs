using System.Threading.Tasks;
using UnityEngine;

namespace Rootborn.Game.Managers
{
    /// <summary>
    /// Global runtime manager entry point. Lives on @Managers and survives scene changes.
    /// </summary>
    public sealed class Managers : MonoBehaviour
    {
        private static Managers s_instance;
        public static Managers Instance => s_instance;

        private readonly ResourceManager _resource = new ResourceManager();
        private readonly DataManager _data = new DataManager();

        public static ResourceManager Resource => Instance != null ? Instance._resource : null;
        public static DataManager Data => Instance != null ? Instance._data : null;

        public bool IsBootstrapped { get; private set; }

        public static Managers EnsureExists()
        {
            if (s_instance != null) return s_instance;

            var existing = GameObject.Find("@Managers");
            if (existing == null)
            {
                existing = new GameObject("@Managers");
            }
            s_instance = existing.GetComponent<Managers>();
            if (s_instance == null)
            {
                s_instance = existing.AddComponent<Managers>();
            }
            DontDestroyOnLoad(existing);
            return s_instance;
        }

        public static async Task BootstrapAsync()
        {
            var managers = EnsureExists();
            if (managers.IsBootstrapped) return;

            await managers._resource.InitializeAsync();
            await managers._data.InitAsync(managers._resource);
            managers.IsBootstrapped = true;
            Debug.Log("[ROOTBORN/Managers] Bootstrap complete.");
        }
    }
}
