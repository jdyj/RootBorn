using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationOutcome_RelationshipDelta", menuName = "Rootborn/Discovery Clues/Interpretations/Outcomes/Relationship Delta")]
    public sealed class ClueInterpretationRelationshipDeltaOutcome : ClueInterpretationOutcomeBase
    {
        [SerializeField] private RelationshipDefinition _relationship;
        [SerializeField] private int _delta;
        public override string OutcomeId => ClueInterpretationOutcomeIds.Relationship;
        public override bool Apply(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation, List<string> encyclopediaEntryIds, List<string> careerHintIds, List<string> followUpQuestIds, List<string> worldStateIds, List<string> relationshipIds, List<string> statusIds)
        {
            if (_relationship == null || _delta == 0) return false;
            context.StudentLifeProgress?.AddRelationship(_relationship, _delta, interpretation != null ? interpretation.Id : string.Empty);
            ClueInterpretationOutcomeIds.AddUnique(relationshipIds, _relationship.Id);
            return true;
        }
        public void ConfigureForTests(RelationshipDefinition relationship, int delta) { _relationship = relationship; _delta = delta; }
    }
}
