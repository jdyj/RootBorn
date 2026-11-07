using System;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    [Serializable]
    public sealed class HouseStateSaveData
    {
        // Older saves also carry construction, room-preset and currency fields; JsonUtility ignores them.
        public int CurrentStageIndex;
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
            return state;
        }
    }
}