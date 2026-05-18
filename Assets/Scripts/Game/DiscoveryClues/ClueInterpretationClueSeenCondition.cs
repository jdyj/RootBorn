using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationCondition_ClueSeen", menuName = "Rootborn/Discovery Clues/Interpretations/Conditions/Clue Seen")]
    public sealed class ClueInterpretationClueSeenCondition : ClueInterpretationConditionBase
    {
        public override bool Evaluate(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation)
        {
            return context.ClueProgress != null && interpretation != null && interpretation.Clue != null && context.ClueProgress.GetRecord(interpretation.Clue.Id).Seen;
        }
    }
}
