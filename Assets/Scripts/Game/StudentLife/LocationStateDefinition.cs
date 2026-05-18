using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "LocationState_New", menuName = "Rootborn/Student Life/Location State/State")]
    public sealed class LocationStateDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _descriptionKey;
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private int _priority;
        [SerializeField] private LocationStateConditionBase[] _conditions = Array.Empty<LocationStateConditionBase>();
        [SerializeField] private LocationStateEffectBase[] _effects = Array.Empty<LocationStateEffectBase>();

        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public LocationDefinition Location => _location;
        public string LocationId => _location != null ? _location.Id : string.Empty;
        public int Priority => _priority;
        public IReadOnlyList<LocationStateConditionBase> Conditions => _conditions;
        public IReadOnlyList<LocationStateEffectBase> Effects => _effects;

        public bool IsSatisfied(in LocationStateContext context)
        {
            if (_location != null && context.Location != null && _location.Id != context.Location.Id) return false;
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition != null && !condition.IsSatisfied(in context)) return false;
            }
            return true;
        }

        public void AppendEffects(LocationStateSummaryBuilder builder)
        {
            if (builder == null) return;
            for (int i = 0; i < _effects.Length; i++) _effects[i]?.AppendTo(builder);
        }

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, LocationDefinition location, int priority, LocationStateConditionBase[] conditions, LocationStateEffectBase[] effects)
        {
            ConfigureForTests(id, displayNameKey);
            _descriptionKey = string.IsNullOrEmpty(descriptionKey) ? displayNameKey : descriptionKey;
            _location = location;
            _priority = priority;
            _conditions = conditions ?? Array.Empty<LocationStateConditionBase>();
            _effects = effects ?? Array.Empty<LocationStateEffectBase>();
        }
    }
}
