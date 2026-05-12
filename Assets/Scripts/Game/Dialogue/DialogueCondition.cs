using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.Dialogue
{
    [CreateAssetMenu(fileName = "DialogueCondition_New", menuName = "Rootborn/Dialogue/Dialogue Condition")]
    public sealed class DialogueCondition : ScriptableObject
    {
        [SerializeField] private TutorialStageDefinition _requiredTutorialStage;
        [SerializeField] private RelationshipDefinition _minimumRelationship;
        [SerializeField] private int _minimumRelationshipValue;
        [SerializeField] private StatusDefinition _maximumStatus;
        [SerializeField] private int _maximumStatusValue = int.MaxValue;

        public bool IsSatisfied(StudentLifeProgress progress)
        {
            if (progress == null)
            {
                return _requiredTutorialStage == null && _minimumRelationship == null && _maximumStatus == null;
            }

            if (_requiredTutorialStage != null && progress.TutorialStageId != _requiredTutorialStage.Id)
            {
                return false;
            }

            if (_minimumRelationship != null && progress.GetRelationshipValue(_minimumRelationship) < _minimumRelationshipValue)
            {
                return false;
            }

            if (_maximumStatus != null && progress.GetStatusValue(_maximumStatus) > _maximumStatusValue)
            {
                return false;
            }

            return true;
        }

        public void ConfigureForTests(
            TutorialStageDefinition requiredTutorialStage,
            RelationshipDefinition minimumRelationship,
            int minimumRelationshipValue,
            StatusDefinition maximumStatus,
            int maximumStatusValue)
        {
            _requiredTutorialStage = requiredTutorialStage;
            _minimumRelationship = minimumRelationship;
            _minimumRelationshipValue = minimumRelationshipValue;
            _maximumStatus = maximumStatus;
            _maximumStatusValue = maximumStatusValue;
        }
    }
}
