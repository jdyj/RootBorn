using Rootborn.Core.Quests;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Tiles;
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
        /// <summary>True when an already committed action is announced again (e.g. furniture already in the room on entry).
        /// Quest chains may use it to catch up; the player's quest log does not count it again.</summary>
        public readonly bool IsReplay;

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
            CareerInterestDefinition careerInterest = null,
            bool isReplay = false)
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
            IsReplay = isReplay;
        }

        /// <summary>The definition this event happened to, by kind.</summary>
        public Object Subject
        {
            get
            {
                switch (Kind)
                {
                    case QuestEventKind.Gather: return Resource;
                    case QuestEventKind.Collect: return Item;
                    case QuestEventKind.Talk: return Npc;
                    case QuestEventKind.Defeat: return DefeatTarget;
                    case QuestEventKind.TilePlaced: return Tile;
                    case QuestEventKind.LocationActivity: return Activity;
                    case QuestEventKind.LocationVisit: return Location;
                    case QuestEventKind.PartTimeWork: return Work;
                    case QuestEventKind.FurniturePlaced: return Furniture;
                    case QuestEventKind.DailyPlanSelected: return DailyPlanRoute;
                    case QuestEventKind.CareerInterestSelected: return CareerInterest;
                    default: return null;
                }
            }
        }

        /// <summary>The event reduced to ids for Rootborn.Core's objective rules.</summary>
        public QuestEventFacts ToFacts() => new QuestEventFacts((int)Kind, QuestSubjectIds.Of(Subject), Count, SurfaceId);
    }

    /// <summary>
    /// Stable ids for quest subjects. Copies of one definition (scene copy, Addressables bundle copy) share the id;
    /// a definition without an id falls back to its own instance, so it only matches itself.
    /// </summary>
    public static class QuestSubjectIds
    {
        public static string Of(Object subject)
        {
            if (subject == null) return null;
            string id = null;
            if (subject is StudentLifeDefinitionBase studentLife) id = studentLife.Id;
            else if (subject is ResourceNodeDefinition resource) id = resource.Id;
            else if (subject is ItemDefinition item) id = item.Id;
            else if (subject is NpcDefinition npc) id = npc.Id;
            else if (subject is FurnitureDefinition furniture) id = furniture.StableId;
            else if (subject is PlaceableTileDefinition tile) id = tile.Id;
            return string.IsNullOrEmpty(id) ? "#instance:" + subject.GetInstanceID() : id;
        }

        public static string[] Of<T>(System.Collections.Generic.IReadOnlyList<T> subjects) where T : Object
        {
            if (subjects == null) return System.Array.Empty<string>();
            var ids = new string[subjects.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = subjects[i] != null ? Of(subjects[i]) : null;
            return ids;
        }
    }
}
