using System;
using System.Collections.Generic;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [CreateAssetMenu(fileName = "WorldStateFlag_New", menuName = "Rootborn/World State/Flag")]
    public sealed class WorldStateFlagDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private LocationDefinition _relatedLocation;
        [SerializeField] private NpcDefinition[] _relatedNpcs = Array.Empty<NpcDefinition>();
        [SerializeField] private ScriptableObject[] _relatedQuestChains = Array.Empty<ScriptableObject>();
        [SerializeField] private WorldStateScopeDefinition _scopeDefinition;
        [SerializeField] private WorldStateScopeKind _scope = WorldStateScopeKind.Shared;
        [SerializeField] private WorldStateChangeKind _changeKind;
        [SerializeField] private string _nextActionKey;
        [SerializeField] private WorldStateSummarySurface[] _visibleSurfaces = Array.Empty<WorldStateSummarySurface>();
        [SerializeField] private WorldStateBadgeKind[] _badges = Array.Empty<WorldStateBadgeKind>();
        [SerializeField] private int _sortPriority;
        [SerializeField] private int _effectVersion = 1;

        public string Id => _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? _id : _displayNameKey;
        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public LocationDefinition RelatedLocation => _relatedLocation;
        public NpcDefinition[] RelatedNpcs => _relatedNpcs;
        public ScriptableObject[] RelatedQuestChains => _relatedQuestChains;
        public WorldStateScopeKind Scope => _scopeDefinition != null ? _scopeDefinition.Kind : _scope;
        public WorldStateChangeKind ChangeKind => _changeKind;
        public string NextActionKey => _nextActionKey;
        public WorldStateBadgeKind[] Badges => _badges;
        public int SortPriority => _sortPriority;
        public int EffectVersion => Mathf.Max(1, _effectVersion);

        public bool IsVisibleOn(WorldStateSummarySurface surface)
        {
            if (_visibleSurfaces == null || _visibleSurfaces.Length == 0) return true;
            for (int i = 0; i < _visibleSurfaces.Length; i++) if (_visibleSurfaces[i] == surface) return true;
            return false;
        }

        public bool IsRelatedTo(LocationDefinition location)
        {
            return location != null && _relatedLocation == location;
        }

        public string FirstNpcDisplayName()
        {
            if (_relatedNpcs == null || _relatedNpcs.Length == 0 || _relatedNpcs[0] == null) return string.Empty;
            return _relatedNpcs[0].DisplayNameKey;
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            string descriptionKey,
            LocationDefinition relatedLocation,
            ScriptableObject[] relatedNpcs,
            ScriptableObject[] relatedQuestChains,
            WorldStateScopeKind scope,
            WorldStateChangeKind changeKind,
            string nextActionKey,
            WorldStateSummarySurface[] visibleSurfaces,
            WorldStateBadgeKind[] badges,
            int sortPriority,
            int effectVersion)
        {
            _id = id;
            _displayNameKey = string.IsNullOrEmpty(displayNameKey) ? id : displayNameKey;
            _descriptionKey = string.IsNullOrEmpty(descriptionKey) ? _displayNameKey : descriptionKey;
            _relatedLocation = relatedLocation;
            _relatedNpcs = FilterNpcs(relatedNpcs);
            _relatedQuestChains = relatedQuestChains ?? Array.Empty<ScriptableObject>();
            _scopeDefinition = null;
            _scope = scope;
            _changeKind = changeKind;
            _nextActionKey = nextActionKey ?? string.Empty;
            _visibleSurfaces = visibleSurfaces ?? Array.Empty<WorldStateSummarySurface>();
            _badges = badges ?? Array.Empty<WorldStateBadgeKind>();
            _sortPriority = sortPriority;
            _effectVersion = Mathf.Max(1, effectVersion);
        }

        private static NpcDefinition[] FilterNpcs(ScriptableObject[] values)
        {
            if (values == null || values.Length == 0) return Array.Empty<NpcDefinition>();
            var result = new List<NpcDefinition>(values.Length);
            for (int i = 0; i < values.Length; i++) if (values[i] is NpcDefinition npc) result.Add(npc);
            return result.ToArray();
        }
    }
}
