using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "TimeSlot_New", menuName = "Rootborn/Student Life/Time Slot")]
    public sealed class TimeSlotDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private int _order;

        public int Order => _order;

        public void ConfigureForTests(string id, string displayNameKey, int order)
        {
            ConfigureForTests(id, displayNameKey);
            _order = order;
        }
    }
}
