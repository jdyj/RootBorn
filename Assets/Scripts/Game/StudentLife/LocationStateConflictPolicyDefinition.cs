using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "LocationStateConflictPolicy_New", menuName = "Rootborn/Student Life/Location State/Conflict Policy")]
    public sealed class LocationStateConflictPolicyDefinition : ScriptableObject
    {
        [SerializeField] private LocationStateConflictMode _mode = LocationStateConflictMode.HighestPriority;
        public LocationStateConflictMode Mode => _mode;
        public void ConfigureForTests(LocationStateConflictMode mode) { _mode = mode; }
        public static LocationStateConflictPolicyDefinition HighestPriority() { var policy = CreateInstance<LocationStateConflictPolicyDefinition>(); policy.ConfigureForTests(LocationStateConflictMode.HighestPriority); return policy; }
        public static LocationStateConflictPolicyDefinition MergeAll() { var policy = CreateInstance<LocationStateConflictPolicyDefinition>(); policy.ConfigureForTests(LocationStateConflictMode.MergeAll); return policy; }
    }
}
