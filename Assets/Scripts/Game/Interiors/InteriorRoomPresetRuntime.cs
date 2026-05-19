using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rootborn.Game.Interiors
{
    public static class InteriorRoomPresetCatalog
    {
        public const string DefaultPresetRoot = "Assets/Data/Interiors/RoomPresets/AutoImported/ModernInteriorsHomeDesigns";

        public static IReadOnlyList<InteriorRoomPresetDefinition> LoadPresets()
        {
#if UNITY_EDITOR
            if (!AssetDatabase.IsValidFolder(DefaultPresetRoot))
            {
                return System.Array.Empty<InteriorRoomPresetDefinition>();
            }

            return AssetDatabase.FindAssets("t:InteriorRoomPresetDefinition", new[] { DefaultPresetRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<InteriorRoomPresetDefinition>)
                .Where(preset => preset != null)
                .OrderBy(preset => preset.StableId, System.StringComparer.Ordinal)
                .ToArray();
#else
            return System.Array.Empty<InteriorRoomPresetDefinition>();
#endif
        }
    }

    public static class InteriorRoomPresetRuntimeUtility
    {
        public static bool CanFitPreset(InteriorRoomPresetDefinition preset, InteriorGeneratedMap map)
        {
            if (preset == null || map == null)
            {
                return false;
            }

            return preset.Size.x > 0 &&
                   preset.Size.y > 0 &&
                   preset.Size.x <= map.Width &&
                   preset.Size.y <= map.Height;
        }

        public static void SaveSelectedPreset(string saveSlot, string presetId)
        {
            var state = Rootborn.Game.Housing.HouseStatePersistence.Load(saveSlot);
            state.SelectedRoomPresetId = presetId ?? string.Empty;
            Rootborn.Game.Housing.HouseStatePersistence.Save(saveSlot, state);
        }
    }
    public sealed class InteriorRoomPresetRuntimeProbe : MonoBehaviour
    {
        [SerializeField] private string _lastPresetId = string.Empty;
        [SerializeField] private string _lastPresetName = string.Empty;
        [SerializeField] private Vector2Int _lastPresetSize;
        [SerializeField] private int _lastBaseTileCount;

        public string LastPresetId => _lastPresetId;
        public string LastPresetName => _lastPresetName;
        public Vector2Int LastPresetSize => _lastPresetSize;
        public int LastBaseTileCount => _lastBaseTileCount;

        public static InteriorRoomPresetRuntimeProbe Record(InteriorRoomPresetDefinition preset, Tilemap baseLayer)
        {
            var probe = FindFirstObjectByType<InteriorRoomPresetRuntimeProbe>();
            if (probe == null)
            {
                var go = new GameObject("[InteriorRoomPresetRuntimeProbe]");
                probe = go.AddComponent<InteriorRoomPresetRuntimeProbe>();
            }

            probe._lastPresetId = preset != null ? preset.StableId : string.Empty;
            probe._lastPresetName = preset != null ? preset.DisplayName : string.Empty;
            probe._lastPresetSize = preset != null ? preset.Size : Vector2Int.zero;
            probe._lastBaseTileCount = CountTiles(baseLayer);
            return probe;
        }

        private static int CountTiles(Tilemap tilemap)
        {
            if (tilemap == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
