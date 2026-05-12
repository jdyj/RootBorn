using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "LifeChoice_New", menuName = "Rootborn/Student Life/Choice")]
    public sealed class LifeChoiceDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private LifeActivityEffectBase[] _effects = Array.Empty<LifeActivityEffectBase>();

        public IReadOnlyList<LifeActivityEffectBase> Effects => _effects;

        public void ApplyEffects(StudentLifeProgress progress)
        {
            for (int i = 0; i < _effects.Length; i++)
            {
                _effects[i]?.Apply(progress);
            }
        }

        public void ConfigureForTests(string id, string displayNameKey, LifeActivityEffectBase[] effects)
        {
            ConfigureForTests(id, displayNameKey);
            _effects = effects ?? Array.Empty<LifeActivityEffectBase>();
        }
    }
}
