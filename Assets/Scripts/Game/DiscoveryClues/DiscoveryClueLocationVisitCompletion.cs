using System;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "DiscoveryClueCompletion_LocationVisit", menuName = "Rootborn/Discovery Clues/Completions/Location Visit")]
    public sealed class DiscoveryClueLocationVisitCompletion : DiscoveryClueCompletionBase
    {
        [SerializeField] private string _locationId;

        public string LocationId => string.IsNullOrEmpty(_locationId) ? string.Empty : _locationId;

        public override bool IsCompleted(in DiscoveryClueCompletionEvent completionEvent, in DiscoveryClueContext context, DiscoveryClueDefinition clue)
        {
            if (completionEvent.Kind != DiscoveryClueCompletionKind.LocationVisited) return false;
            string expected = !string.IsNullOrEmpty(_locationId) ? _locationId : clue != null && clue.RelatedLocation != null ? clue.RelatedLocation.Id : string.Empty;
            return !string.IsNullOrEmpty(expected) && string.Equals(expected, completionEvent.TargetId, StringComparison.Ordinal);
        }

        public void ConfigureForTests(string locationId)
        {
            _locationId = string.IsNullOrEmpty(locationId) ? string.Empty : locationId;
        }
    }
}
