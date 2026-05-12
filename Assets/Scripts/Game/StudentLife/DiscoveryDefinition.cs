using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public enum DiscoveryScope
    {
        Personal,
        SharedWorld
    }

    [CreateAssetMenu(fileName = "Discovery_New", menuName = "Rootborn/Student Life/Exploration/Discovery")]
    public sealed class DiscoveryDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private DiscoveryScope _scope = DiscoveryScope.Personal;
        [SerializeField] private Vector2 _worldPosition;
        [SerializeField] private float _interactionRadius = 1f;
        [SerializeField] private DiscoveryRequirementBase[] _requirements = Array.Empty<DiscoveryRequirementBase>();
        [SerializeField] private DiscoveryOutcomeBase[] _outcomes = Array.Empty<DiscoveryOutcomeBase>();

        public DiscoveryScope Scope => _scope;
        public Vector2 WorldPosition => _worldPosition;
        public float InteractionRadius => Mathf.Max(0.1f, _interactionRadius);
        public IReadOnlyList<DiscoveryRequirementBase> Requirements => _requirements;
        public IReadOnlyList<DiscoveryOutcomeBase> Outcomes => _outcomes;

        public bool HasSatisfiedRequirements(StudentLifeProgress progress)
        {
            if (progress == null)
            {
                return false;
            }

            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress))
                {
                    return false;
                }
            }

            return true;
        }

        public string[] ApplyOutcomes(StudentLifeProgress progress)
        {
            var logs = new List<string>();
            for (int i = 0; i < _outcomes.Length; i++)
            {
                string log = _outcomes[i] != null ? _outcomes[i].Apply(progress, Id) : string.Empty;
                if (!string.IsNullOrEmpty(log))
                {
                    logs.Add(log);
                }
            }

            return logs.ToArray();
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            DiscoveryScope scope,
            Vector2 worldPosition,
            float interactionRadius,
            DiscoveryRequirementBase[] requirements,
            DiscoveryOutcomeBase[] outcomes)
        {
            ConfigureForTests(id, displayNameKey);
            _scope = scope;
            _worldPosition = worldPosition;
            _interactionRadius = interactionRadius;
            _requirements = requirements ?? Array.Empty<DiscoveryRequirementBase>();
            _outcomes = outcomes ?? Array.Empty<DiscoveryOutcomeBase>();
        }
    }

    public abstract class DiscoveryRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }

    public abstract class DiscoveryOutcomeBase : ScriptableObject
    {
        public abstract string Apply(StudentLifeProgress progress, string discoveryId);
    }
}
