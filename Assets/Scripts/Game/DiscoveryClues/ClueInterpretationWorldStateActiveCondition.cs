using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationCondition_WorldStateActive", menuName = "Rootborn/Discovery Clues/Interpretations/Conditions/World State Active")]
    public sealed class ClueInterpretationWorldStateActiveCondition : ClueInterpretationConditionBase
    {
        [SerializeField] private WorldStateFlagDefinition _flag;
        public override bool Evaluate(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation) => context.WorldStateProgress != null && _flag != null && context.WorldStateProgress.IsActive(_flag);
        public void ConfigureForTests(WorldStateFlagDefinition flag) => _flag = flag;
    }
}
