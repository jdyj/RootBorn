using System;
using Rootborn.Game.Dialogue;
using Rootborn.Game.DiscoveryClues;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class LocationStateAvailabilityEffect : LocationStateEffectBase
    {
        [SerializeField] private LocationActivityDefinition[] _activities = Array.Empty<LocationActivityDefinition>();
        [SerializeField] private NpcDefinition[] _npcs = Array.Empty<NpcDefinition>();
        [SerializeField] private DiscoveryClueDefinition[] _clues = Array.Empty<DiscoveryClueDefinition>();
        [SerializeField] private string[] _interactableObjectIds = Array.Empty<string>();
        [SerializeField] private string[] _portalIds = Array.Empty<string>();
        [SerializeField] private string[] _shopItemIds = Array.Empty<string>();
        public override void AppendTo(LocationStateSummaryBuilder builder)
        {
            if (builder == null) return;
            builder.AddActivities(_activities); builder.AddNpcs(_npcs); builder.AddClues(_clues); builder.AddInteractableObjects(_interactableObjectIds); builder.AddPortals(_portalIds); builder.AddShopItems(_shopItemIds);
        }
        public void ConfigureForTests(LocationActivityDefinition[] activities, NpcDefinition[] npcs, DiscoveryClueDefinition[] clues, string[] interactableObjectIds, string[] portalIds, string[] shopItemIds)
        {
            _activities = activities ?? Array.Empty<LocationActivityDefinition>(); _npcs = npcs ?? Array.Empty<NpcDefinition>(); _clues = clues ?? Array.Empty<DiscoveryClueDefinition>(); _interactableObjectIds = interactableObjectIds ?? Array.Empty<string>(); _portalIds = portalIds ?? Array.Empty<string>(); _shopItemIds = shopItemIds ?? Array.Empty<string>();
        }
    }
}
