using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "MilestoneRoute_New", menuName = "Rootborn/Student Life/Milestones/Route")]
    public sealed class MilestoneRouteDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private MilestoneObjectiveBase _objective;
        [SerializeField] private string _recommendedActionId;

        public MilestoneObjectiveBase Objective => _objective;
        public string RecommendedActionId => string.IsNullOrEmpty(_recommendedActionId) ? string.Empty : _recommendedActionId;

        public void ConfigureForTests(string id, MilestoneObjectiveBase objective, string recommendedActionId)
        {
            ConfigureForTests(id, id);
            _objective = objective;
            _recommendedActionId = string.IsNullOrEmpty(recommendedActionId) ? string.Empty : recommendedActionId;
        }
    }
}
