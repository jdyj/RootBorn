using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "Relationship_New", menuName = "Rootborn/Student Life/Relationship")]
    public sealed class RelationshipDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _targetId;

        public string TargetId => string.IsNullOrEmpty(_targetId) ? Id : _targetId;

        public void ConfigureForTests(string id, string displayNameKey, string targetId)
        {
            ConfigureForTests(id, displayNameKey);
            _targetId = string.IsNullOrEmpty(targetId) ? string.Empty : targetId;
        }
    }
}
