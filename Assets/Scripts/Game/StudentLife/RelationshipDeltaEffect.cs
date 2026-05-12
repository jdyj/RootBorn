using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "RelationshipDeltaEffect_New", menuName = "Rootborn/Student Life/Effects/Relationship Delta")]
    public sealed class RelationshipDeltaEffect : LifeActivityEffectBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _delta;

        public RelationshipDefinition Relationship => _relationship;
        public int Delta => _delta;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.AddRelationship(_relationship, _delta, string.Empty);
        }

        public void ConfigureForTests(RelationshipDefinition relationship, int delta)
        {
            _relationship = relationship;
            _delta = delta;
        }
    }
}
