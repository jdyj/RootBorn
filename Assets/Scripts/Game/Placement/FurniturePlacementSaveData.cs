using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Placement
{
    [Serializable]
    public sealed class FurniturePlacementSaveData
    {
        public string FurnitureId = string.Empty;
        public string SurfaceId = string.Empty;
        public int AnchorX;
        public int AnchorY;
        public FurniturePlacementDirection Direction;
        public string StateJson = string.Empty;
        public string TileName = string.Empty;

        public static FurniturePlacementSaveData FromInstance(FurniturePlacementInstance instance, string stateJson)
        {
            return new FurniturePlacementSaveData
            {
                FurnitureId = instance.FurnitureId,
                SurfaceId = instance.SurfaceId,
                AnchorX = instance.AnchorCell.x,
                AnchorY = instance.AnchorCell.y,
                Direction = instance.Direction,
                StateJson = stateJson ?? string.Empty
            };
        }

        public Vector3Int ToAnchorCell()
        {
            return new Vector3Int(AnchorX, AnchorY, 0);
        }
    }

    public readonly struct FurniturePlacementRestoreResult
    {
        public FurniturePlacementRestoreResult(int restoredCount, int missingDefinitionCount, int failedPlacementCount)
        {
            RestoredCount = restoredCount;
            MissingDefinitionCount = missingDefinitionCount;
            FailedPlacementCount = failedPlacementCount;
        }

        public int RestoredCount { get; }
        public int MissingDefinitionCount { get; }
        public int FailedPlacementCount { get; }
    }

    public static class FurniturePlacementSaveUtility
    {
        public static FurniturePlacementRestoreResult Restore(
            TilePlacementSurface surface,
            FurniturePlacementRegistry registry,
            IReadOnlyList<FurniturePlacementSaveData> saveData,
            IReadOnlyList<FurnitureDefinition> catalog)
        {
            if (surface == null || registry == null)
            {
                return new FurniturePlacementRestoreResult(0, 0, 0);
            }

            registry.ClearSurface(surface);
            int restored = 0;
            int missing = 0;
            int failed = 0;
            if (saveData == null)
            {
                return new FurniturePlacementRestoreResult(0, 0, 0);
            }

            for (int i = 0; i < saveData.Count; i++)
            {
                var item = saveData[i];
                if (item == null || !string.Equals(item.SurfaceId, surface.SurfaceId, StringComparison.Ordinal))
                {
                    continue;
                }

                var definition = FindDefinition(catalog, item.FurnitureId);
                if (definition == null)
                {
                    missing++;
                    continue;
                }

                if (registry.TryPlace(surface, definition, item.ToAnchorCell(), item.Direction, out _, out _))
                {
                    restored++;
                }
                else
                {
                    failed++;
                }
            }

            return new FurniturePlacementRestoreResult(restored, missing, failed);
        }

        private static FurnitureDefinition FindDefinition(IReadOnlyList<FurnitureDefinition> catalog, string furnitureId)
        {
            if (catalog == null || string.IsNullOrEmpty(furnitureId))
            {
                return null;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                var definition = catalog[i];
                if (definition != null && string.Equals(definition.StableId, furnitureId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }
    }
}
