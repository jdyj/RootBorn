using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    public readonly struct QuestEvent
    {
        public readonly QuestEventKind Kind;
        public readonly string EventKey;
        public readonly ResourceNodeDefinition Resource;
        public readonly ItemDefinition Item;
        public readonly CropDefinition Crop;
        public readonly ToolDefinition Tool;
        public readonly ScriptableObject Npc;
        public readonly ScriptableObject DefeatTarget;
        public readonly int Count;

        public QuestEvent(
            QuestEventKind kind,
            string eventKey,
            int count = 1,
            ResourceNodeDefinition resource = null,
            ItemDefinition item = null,
            CropDefinition crop = null,
            ToolDefinition tool = null,
            ScriptableObject npc = null,
            ScriptableObject defeatTarget = null)
        {
            Kind = kind;
            EventKey = eventKey;
            Count = count;
            Resource = resource;
            Item = item;
            Crop = crop;
            Tool = tool;
            Npc = npc;
            DefeatTarget = defeatTarget;
        }
    }
}
