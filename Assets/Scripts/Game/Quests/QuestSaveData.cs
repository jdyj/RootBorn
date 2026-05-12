using System;

namespace Rootborn.Game.Quests
{
    [Serializable]
    public sealed class QuestLogSaveData
    {
        public QuestProgressSaveData[] Quests = Array.Empty<QuestProgressSaveData>();
    }

    [Serializable]
    public sealed class QuestProgressSaveData
    {
        public string QuestId;
        public QuestState State;
        public string StateName;
        public int[] ObjectiveCounts = Array.Empty<int>();
        public string[] ProcessedEventKeys = Array.Empty<string>();
    }
}