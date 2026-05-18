using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class LocationStateActivityLogCondition : LocationStateConditionBase
    {
        [SerializeField] private string _requiredActivityId;
        [SerializeField] private int _minimumCount = 1;
        public override bool IsSatisfied(in LocationStateContext context)
        {
            if (context.StudentProgress == null || string.IsNullOrEmpty(_requiredActivityId)) return false;
            string[] ids = context.StudentProgress.GetActivityLogIds();
            int count = 0;
            for (int i = 0; i < ids.Length; i++) if (ids[i] == _requiredActivityId) count++;
            return count >= Mathf.Max(1, _minimumCount);
        }
        public void ConfigureForTests(string requiredActivityId, int minimumCount) { _requiredActivityId = requiredActivityId ?? string.Empty; _minimumCount = minimumCount; }
    }
}
