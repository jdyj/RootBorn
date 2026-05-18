using System;
using System.Collections.Generic;
using Rootborn.Game.Dialogue;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "Location_New", menuName = "Rootborn/Student Life/Exploration/Location")]
    public sealed class LocationDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _descriptionKey;
        [SerializeField] private LocationCategoryDefinition _category;
        [SerializeField] private string _worldAnchorId;
        [SerializeField] private Vector2 _worldPosition;
        [SerializeField] private NpcRoleDefinition[] _relatedRoles = Array.Empty<NpcRoleDefinition>();
        [SerializeField] private NpcDefinition[] _relatedNpcs = Array.Empty<NpcDefinition>();
        [SerializeField] private DiscoveryDefinition[] _discoveries = Array.Empty<DiscoveryDefinition>();
        [SerializeField] private LocationVisitRuleBase[] _visitRules = Array.Empty<LocationVisitRuleBase>();

        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public LocationCategoryDefinition Category => _category;
        public string WorldAnchorId => string.IsNullOrEmpty(_worldAnchorId) ? Id : _worldAnchorId;
        public Vector2 WorldPosition => _worldPosition;
        public IReadOnlyList<NpcRoleDefinition> RelatedRoles => _relatedRoles;
        public IReadOnlyList<NpcDefinition> RelatedNpcs => _relatedNpcs;
        public IReadOnlyList<DiscoveryDefinition> Discoveries => _discoveries;
        public IReadOnlyList<LocationVisitRuleBase> VisitRules => _visitRules;

        public bool CanVisit(StudentLifeProgress progress)
        {
            if (progress == null) return false;
            for (int i = 0; i < _visitRules.Length; i++)
            {
                var rule = _visitRules[i];
                if (rule != null && !rule.IsSatisfied(progress)) return false;
            }

            return true;
        }

        public void ConfigureForTests(string id, string displayNameKey, Vector2 worldPosition, DiscoveryDefinition[] discoveries)
        {
            ConfigureForTests(id, displayNameKey, displayNameKey, null, id, worldPosition, Array.Empty<NpcRoleDefinition>(), Array.Empty<NpcDefinition>(), discoveries, Array.Empty<LocationVisitRuleBase>());
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            string descriptionKey,
            LocationCategoryDefinition category,
            string worldAnchorId,
            Vector2 worldPosition,
            NpcRoleDefinition[] relatedRoles,
            NpcDefinition[] relatedNpcs,
            DiscoveryDefinition[] discoveries,
            LocationVisitRuleBase[] visitRules)
        {
            ConfigureForTests(id, displayNameKey);
            _descriptionKey = string.IsNullOrEmpty(descriptionKey) ? displayNameKey : descriptionKey;
            _category = category;
            _worldAnchorId = string.IsNullOrEmpty(worldAnchorId) ? id : worldAnchorId;
            _worldPosition = worldPosition;
            _relatedRoles = relatedRoles ?? Array.Empty<NpcRoleDefinition>();
            _relatedNpcs = relatedNpcs ?? Array.Empty<NpcDefinition>();
            _discoveries = discoveries ?? Array.Empty<DiscoveryDefinition>();
            _visitRules = visitRules ?? Array.Empty<LocationVisitRuleBase>();
        }
    }

    public abstract class LocationVisitRuleBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }
}
