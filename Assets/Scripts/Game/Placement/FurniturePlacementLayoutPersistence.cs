using System;
using System.Collections.Generic;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.Placement
{
    [Serializable]
    public sealed class FurniturePlacementLayoutSaveData
    {
        public FurniturePlacementSaveData[] Items = Array.Empty<FurniturePlacementSaveData>();
    }

    public static class FurniturePlacementLayoutPersistence
    {
        public const string HouseLayoutFileName = "house-furniture-layout.json";

        public static bool HasHouseLayout()
        {
            return !string.IsNullOrEmpty(CreateService().ReadJson(HouseLayoutFileName));
        }

        public static string GetHouseLayoutFileNameForStage(int stageIndex)
        {
            return "house-furniture-layout-stage-" + Mathf.Max(0, stageIndex) + ".json";
        }

        public static void SaveHouseLayout(IReadOnlyList<FurniturePlacementSaveData> items)
        {
            SaveLayout(HouseLayoutFileName, items);
        }

        public static bool TryLoadHouseLayout(List<FurniturePlacementSaveData> destination)
        {
            return TryLoadLayout(HouseLayoutFileName, destination);
        }

        public static void SaveHouseLayoutForStage(int stageIndex, IReadOnlyList<FurniturePlacementSaveData> items)
        {
            SaveLayout(GetHouseLayoutFileNameForStage(stageIndex), items);
        }

        public static bool TryLoadHouseLayoutForStage(int stageIndex, List<FurniturePlacementSaveData> destination)
        {
            return TryLoadLayout(GetHouseLayoutFileNameForStage(stageIndex), destination);
        }

        private static void SaveLayout(string fileName, IReadOnlyList<FurniturePlacementSaveData> items)
        {
            var layout = new FurniturePlacementLayoutSaveData
            {
                Items = ToArray(items)
            };

            CreateService().WriteJson(fileName, JsonUtility.ToJson(layout, true));
        }

        private static bool TryLoadLayout(string fileName, List<FurniturePlacementSaveData> destination)
        {
            if (destination == null)
            {
                return false;
            }

            string json = CreateService().ReadJson(fileName);
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                var layout = JsonUtility.FromJson<FurniturePlacementLayoutSaveData>(json);
                destination.Clear();
                if (layout == null || layout.Items == null)
                {
                    return false;
                }

                for (int i = 0; i < layout.Items.Length; i++)
                {
                    if (layout.Items[i] != null)
                    {
                        destination.Add(layout.Items[i]);
                    }
                }

                return destination.Count > 0;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static SaveService CreateService()
        {
            string slot = !string.IsNullOrEmpty(ActiveSaveContext.SlotId) ? ActiveSaveContext.SlotId : "default";
            return new SaveService(slot);
        }

        private static FurniturePlacementSaveData[] ToArray(IReadOnlyList<FurniturePlacementSaveData> items)
        {
            if (items == null || items.Count == 0)
            {
                return Array.Empty<FurniturePlacementSaveData>();
            }

            var result = new FurniturePlacementSaveData[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                result[i] = items[i];
            }

            return result;
        }
    }
}