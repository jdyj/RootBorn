using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "Status_New", menuName = "Rootborn/Student Life/Condition Status")]
    public sealed class StatusDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private bool _persistsToNextDay = true;

        public bool PersistsToNextDay => _persistsToNextDay;

        public void ConfigureForTests(string id, string displayNameKey, bool persistsToNextDay)
        {
            ConfigureForTests(id, displayNameKey);
            _persistsToNextDay = persistsToNextDay;
        }
    }
}
