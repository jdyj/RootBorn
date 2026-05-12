using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "HelpActionOutcome_RelationshipDelta", menuName = "Rootborn/Student Life/Town Help/Outcomes/Relationship Delta")]
    public sealed class HelpActionRelationshipDeltaOutcome : HelpActionOutcomeBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _delta;

        public RelationshipDefinition Relationship => _relationship;
        public int Delta => _delta;

        public override string Apply(StudentLifeProgress progress, string actionId)
        {
            if (progress == null || _relationship == null || _delta == 0)
            {
                return string.Empty;
            }

            progress.AddRelationship(_relationship, _delta, actionId);
            return TownHelpActionLogCodec.EncodeDelta(actionId, _relationship.Id, _delta);
        }

        public void ConfigureForTests(RelationshipDefinition relationship, int delta)
        {
            _relationship = relationship;
            _delta = delta;
        }
    }
}
