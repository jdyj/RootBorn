using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class LocationStateWorldStateCondition : LocationStateConditionBase
    {
        [SerializeField] private WorldStateFlagDefinition _flag;
        [SerializeField] private bool _expectedActive = true;
        public override bool IsSatisfied(in LocationStateContext context) { bool active = context.WorldStateProgress != null && context.WorldStateProgress.IsActive(_flag); return active == _expectedActive; }
        public void ConfigureForTests(WorldStateFlagDefinition flag, bool expectedActive) { _flag = flag; _expectedActive = expectedActive; }
    }
}
