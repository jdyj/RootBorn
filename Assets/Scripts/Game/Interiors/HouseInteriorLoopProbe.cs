using System.Reflection;
using Rootborn.Game.Housing;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Interiors
{
    public sealed class HouseInteriorLoopProbe
    {
        public int CurrentStageIndex;
        public string SelectedRoomPresetId;
        public int GroundTileCount;
        public int WallTileCount;
        public int CollisionTileCount;
        public int SurfaceCellCount;
        public int PlacedFurnitureCount;
        public bool HasVisibleDebugOverlay;

        public static HouseInteriorLoopProbe Capture()
        {
            string slot = !string.IsNullOrEmpty(ActiveSaveContext.SlotId) ? ActiveSaveContext.SlotId : "default";
            var state = HouseStatePersistence.Load(slot);
            var overlay = FindPlacementOverlay();

            return new HouseInteriorLoopProbe
            {
                CurrentStageIndex = state.CurrentStageIndex,
                SelectedRoomPresetId = state.SelectedRoomPresetId,
                GroundTileCount = CountTiles("HouseGroundTilemap"),
                WallTileCount = CountTiles("HouseWallTilemap"),
                CollisionTileCount = CountTiles("HouseCollisionTilemap"),
                SurfaceCellCount = ReadIntProperty(overlay, "ActiveSurfaceCellCountForTests"),
                PlacedFurnitureCount = ReadIntProperty(overlay, "PlacedFurnitureCountForTests"),
                HasVisibleDebugOverlay = HasVisibleOverlay()
            };
        }

        private static MonoBehaviour FindPlacementOverlay()
        {
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].GetType().FullName == "Rootborn.UI.Interiors.InteriorPlacementPreviewOverlay")
                {
                    return behaviours[i];
                }
            }

            return null;
        }

        private static int ReadIntProperty(MonoBehaviour target, string propertyName)
        {
            if (target == null)
            {
                return 0;
            }

            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(int))
            {
                return 0;
            }

            return (int)property.GetValue(target);
        }

        private static int CountTiles(string name)
        {
            var tilemap = GameObject.Find(name)?.GetComponent<Tilemap>();
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

        private static bool HasVisibleOverlay()
        {
            var tilemaps = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                string name = tilemaps[i].gameObject.name;
                if (tilemaps[i].enabled && (name.Contains("Debug") || name.Contains("Sample") || name.Contains("GhostPreview") || name.Contains("FootprintPreview")))
                {
                    return true;
                }
            }

            return false;
        }
    }
}