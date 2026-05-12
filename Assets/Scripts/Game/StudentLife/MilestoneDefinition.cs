using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "Milestone_New", menuName = "Rootborn/Student Life/Milestones/Milestone")]
    public sealed class MilestoneDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private MilestoneObjectiveBase[] _objectives = Array.Empty<MilestoneObjectiveBase>();
        [SerializeField] private MilestoneRewardBase[] _rewards = Array.Empty<MilestoneRewardBase>();
        [SerializeField] private MilestoneRouteDefinition[] _routes = Array.Empty<MilestoneRouteDefinition>();
        [SerializeField] private int _requiredObjectiveCount;

        public MilestoneObjectiveBase[] Objectives => _objectives;
        public MilestoneRewardBase[] Rewards => _rewards;
        public MilestoneRouteDefinition[] Routes => _routes;
        public int RequiredObjectiveCount => _requiredObjectiveCount <= 0 ? (_objectives == null ? 0 : _objectives.Length) : _requiredObjectiveCount;

        public void ConfigureForTests(string id, string displayNameKey, MilestoneObjectiveBase[] objectives, MilestoneRewardBase[] rewards, MilestoneRouteDefinition[] routes, int requiredObjectiveCount)
        {
            ConfigureForTests(id, displayNameKey);
            _objectives = objectives ?? Array.Empty<MilestoneObjectiveBase>();
            _rewards = rewards ?? Array.Empty<MilestoneRewardBase>();
            _routes = routes ?? Array.Empty<MilestoneRouteDefinition>();
            _requiredObjectiveCount = Mathf.Max(0, requiredObjectiveCount);
        }
    }
}
