using System;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    public enum HouseConstructionCellKind
    {
        Floor,
        Wall,
        Door,
        Decoration
    }

    [Serializable]
    public sealed class HouseConstructionCellSaveData
    {
        public int X;
        public int Y;
        public HouseConstructionCellKind Kind;
    }

    [Serializable]
    public sealed class HouseStateSaveData
    {
        public int CurrentStageIndex;
        public string ActiveConstructionStageId;
        public string SelectedRoomPresetId = string.Empty;
        public HouseConstructionCellSaveData[] PlacedConstructionCells = Array.Empty<HouseConstructionCellSaveData>();
        public HouseUpgradeRouteKind LatestRoute;
        public string[] CompletionHistoryIds = Array.Empty<string>();
        public HouseCurrencySaveData Currency = new HouseCurrencySaveData();
    }

    public static class HouseStatePersistence
    {
        private const string FileName = "house-state.json";

        public static HouseStateSaveData Load(string saveSlot)
        {
            if (string.IsNullOrEmpty(saveSlot)) saveSlot = "default";
            var json = new SaveService(saveSlot).ReadJson(FileName);
            if (string.IsNullOrEmpty(json)) return new HouseStateSaveData();
            var data = JsonUtility.FromJson<HouseStateSaveData>(json);
            return Normalize(data);
        }

        public static void Save(string saveSlot, HouseStateSaveData state)
        {
            if (string.IsNullOrEmpty(saveSlot)) saveSlot = "default";
            var normalized = Normalize(state);
            new SaveService(saveSlot).WriteJson(FileName, JsonUtility.ToJson(normalized, true));
        }

        private static HouseStateSaveData Normalize(HouseStateSaveData state)
        {
            state ??= new HouseStateSaveData();
            state.CurrentStageIndex = Math.Max(0, state.CurrentStageIndex);
            state.SelectedRoomPresetId ??= string.Empty;
            state.PlacedConstructionCells ??= Array.Empty<HouseConstructionCellSaveData>();
            state.CompletionHistoryIds ??= Array.Empty<string>();
            state.Currency ??= new HouseCurrencySaveData();
            return state;
        }
    }
}