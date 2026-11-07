using Rootborn.Game.Common;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Placement;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    public readonly struct QuestEvent
    {
        public readonly QuestEventKind Kind;
        public readonly string EventKey;
        public readonly ResourceNodeDefinition Resource;
        public readonly ItemDefinition Item;
        public readonly ToolDefinition Tool;
        public readonly ScriptableObject Npc;
        public readonly ScriptableObject DefeatTarget;
        public readonly ScriptableObject Tile;
        public readonly LocationActivityDefinition Activity;
        public readonly LocationDefinition Location;
        public readonly PartTimeWorkDefinition Work;
        public readonly FurnitureDefinition Furniture;
        public readonly string SurfaceId;
        public readonly DailyPlanRouteDefinition DailyPlanRoute;
        public readonly CareerInterestDefinition CareerInterest;
        public readonly int Count;

        public QuestEvent(
            QuestEventKind kind,
            string eventKey,
            int count = 1,
            ResourceNodeDefinition resource = null,
            ItemDefinition item = null,
            ToolDefinition tool = null,
            ScriptableObject npc = null,
            ScriptableObject defeatTarget = null,
            ScriptableObject tile = null,
            LocationActivityDefinition activity = null,
            LocationDefinition location = null,
            PartTimeWorkDefinition work = null,
            FurnitureDefinition furniture = null,
            string surfaceId = null,
            DailyPlanRouteDefinition dailyPlanRoute = null,
            CareerInterestDefinition careerInterest = null)
        {
            Kind = kind;
            EventKey = eventKey;
            Count = count;
            Resource = resource;
            Item = item;
            Tool = tool;
            Npc = npc;
            DefeatTarget = defeatTarget;
            Tile = tile;
            Activity = activity;
            Location = location;
            Work = work;
            Furniture = furniture;
            SurfaceId = surfaceId ?? string.Empty;
            DailyPlanRoute = dailyPlanRoute;
            CareerInterest = careerInterest;
        }
    }
}
