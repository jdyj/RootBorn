using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationOutcome_StatusDelta", menuName = "Rootborn/Discovery Clues/Interpretations/Outcomes/Status Delta")]
    public sealed class ClueInterpretationStatusDeltaOutcome : ClueInterpretationOutcomeBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _delta;
        public override string OutcomeId => ClueInterpretationOutcomeIds.Status;
        public override bool Apply(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation, List<string> encyclopediaEntryIds, List<string> careerHintIds, List<string> followUpQuestIds, List<string> worldStateIds, List<string> relationshipIds, List<string> statusIds)
        {
            if (_status == null || _delta == 0) return false;
            context.StudentLifeProgress?.AddStatus(_status, _delta, interpretation != null ? interpretation.Id : string.Empty);
            ClueInterpretationOutcomeIds.AddUnique(statusIds, _status.Id);
            return true;
        }
        public void ConfigureForTests(StatusDefinition status, int delta) { _status = status; _delta = delta; }
    }
}
