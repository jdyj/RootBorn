using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationCondition_PreviousChoice", menuName = "Rootborn/Student Life/Exploration Choices/Conditions/Previous Choice")]
    public sealed class ExplorationPreviousChoiceCondition : ExplorationConditionBase
    {
        [SerializeField] private ExplorationInteractionDefinition _interaction;
        [SerializeField] private ExplorationChoiceDefinition _choice;

        public override bool Evaluate(in ExplorationInteractionContext context, ExplorationChoiceDefinition choice)
        {
            if (context.ExplorationProgress == null || _interaction == null || _choice == null) return false;
            return context.ExplorationProgress.GetRecord(_interaction.Id).HasSelectedChoice(_choice.Id);
        }

        public void ConfigureForTests(ExplorationInteractionDefinition interaction, ExplorationChoiceDefinition requiredChoice, string lockedReasonKey)
        {
            _interaction = interaction;
            _choice = requiredChoice;
            SetLockedReasonForTests(lockedReasonKey);
        }
    }
}
