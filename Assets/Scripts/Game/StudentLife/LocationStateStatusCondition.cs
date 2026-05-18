using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class LocationStateStatusCondition : LocationStateConditionBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _minimumValue;
        public override bool IsSatisfied(in LocationStateContext context) => context.StudentProgress != null && context.StudentProgress.GetStatusValue(_status) >= Mathf.Max(0, _minimumValue);
        public void ConfigureForTests(StatusDefinition status, int minimumValue) { _status = status; _minimumValue = minimumValue; }
    }
}
