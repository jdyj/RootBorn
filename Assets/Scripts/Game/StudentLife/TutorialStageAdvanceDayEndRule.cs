using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DayEndRule_TutorialStageAdvance", menuName = "Rootborn/Student Life/Day End Rules/Tutorial Stage Advance")]
    public sealed class TutorialStageAdvanceDayEndRule : DayEndRuleBase
    {
        [SerializeField] private string _fromStageId;
        [SerializeField] private TutorialStageDefinition _targetStage;
        [SerializeField] private string _nextObjectiveId;

        public override void Apply(StudentLifeProgress progress)
        {
            if (progress == null || _targetStage == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_fromStageId) && progress.TutorialStageId != _fromStageId)
            {
                return;
            }

            progress.SetTutorialStage(_targetStage, _nextObjectiveId);
        }

        public void ConfigureForTests(string fromStageId, TutorialStageDefinition targetStage, string nextObjectiveId)
        {
            _fromStageId = string.IsNullOrEmpty(fromStageId) ? string.Empty : fromStageId;
            _targetStage = targetStage;
            _nextObjectiveId = string.IsNullOrEmpty(nextObjectiveId) ? string.Empty : nextObjectiveId;
        }
    }
}
