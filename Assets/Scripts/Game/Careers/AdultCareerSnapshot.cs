using System;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;

namespace Rootborn.Game.Careers
{
    [Serializable]
    public sealed class AdultCareerSnapshot
    {
        public int Version = 2;
        public AdultCareerProgressSaveData Adult;
        public StudentLifeProgressSaveData Student;
        public PlayerInventorySaveData Inventory;
        public WorldStateProgressSaveData World;
        public QuestChainLogSaveData QuestChains;
        public FurnitureUnlockProgressSaveData FurnitureUnlocks;
    }

    public sealed class AdultCareerSession
    {
        internal AdultCareerSession(AdultCareerProgress progress, StudentLifeProgress student, Inventory inventory, WorldStateProgress world, string equippedToolItemId, QuestChainLogSaveData questChains, FurnitureUnlockProgress furnitureUnlocks)
        { Progress = progress; Student = student; Inventory = inventory; World = world; EquippedToolItemId = equippedToolItemId ?? string.Empty; ReplaceQuestChains(questChains); FurnitureUnlocks = furnitureUnlocks; }
        public AdultCareerProgress Progress { get; }
        public StudentLifeProgress Student { get; }
        public Inventory Inventory { get; }
        public WorldStateProgress World { get; }
        public string EquippedToolItemId { get; }
        public QuestChainLogSaveData QuestChains { get; private set; }
        public FurnitureUnlockProgress FurnitureUnlocks { get; }
        public void ReplaceQuestChains(QuestChainLogSaveData value)
        {
            QuestChains = value == null ? new QuestChainLogSaveData() : UnityEngine.JsonUtility.FromJson<QuestChainLogSaveData>(UnityEngine.JsonUtility.ToJson(value));
        }
    }
}
