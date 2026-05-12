using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [CreateAssetMenu(fileName = "Dialogue_New", menuName = "Rootborn/Dialogue/Dialogue Definition")]
    public sealed class DialogueDefinition : ScriptableObject
    {
        [SerializeField] private string[] _lineKeys = System.Array.Empty<string>();
        [SerializeField] private DialogueChoiceDefinition[] _choices = System.Array.Empty<DialogueChoiceDefinition>();
        [SerializeField] private TutorialStageDefinition _requiredTutorialStage;
        [SerializeField] private DialogueCondition[] _conditions = System.Array.Empty<DialogueCondition>();

        public string[] LineKeys => _lineKeys;
        public DialogueChoiceDefinition[] Choices => _choices;
        public TutorialStageDefinition RequiredTutorialStage => _requiredTutorialStage;
        public DialogueCondition[] Conditions => _conditions;

        public bool IsAvailable(StudentLifeProgress progress)
        {
            if (_requiredTutorialStage != null && (progress == null || progress.TutorialStageId != _requiredTutorialStage.Id))
            {
                return false;
            }

            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition != null && !condition.IsSatisfied(progress))
                {
                    return false;
                }
            }

            return true;
        }

        public void ConfigureForTests(string[] lineKeys, DialogueChoiceDefinition[] choices, DialogueCondition[] conditions)
        {
            _lineKeys = lineKeys ?? System.Array.Empty<string>();
            _choices = choices ?? System.Array.Empty<DialogueChoiceDefinition>();
            _conditions = conditions ?? System.Array.Empty<DialogueCondition>();
            _requiredTutorialStage = null;
        }
    }
}
