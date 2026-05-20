using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyPlanRequirement_WorldState", menuName = "Rootborn/Student Life/Daily Plan/Requirements/World State")]
    public sealed class DailyPlanWorldStateRequirement : DailyPlanRouteRequirementBase
    {
        [SerializeField] private WorldStateFlagDefinition _flag;
        [SerializeField] private bool _expectedActive = true;

        public override bool IsSatisfied(StudentLifeProgress progress, DailyPlanRouteDefinition route)
        {
            return false;
        }

        public override bool IsSatisfied(in DailyPlanRouteContext context, DailyPlanRouteDefinition route)
        {
            if (_flag == null || context.WorldStateProgress == null) return false;
            bool active = context.WorldStateProgress.IsActive(_flag);
            return active == _expectedActive;
        }

        public void ConfigureForTests(WorldStateFlagDefinition flag, bool expectedActive)
        {
            _flag = flag;
            _expectedActive = expectedActive;
        }
    }
}