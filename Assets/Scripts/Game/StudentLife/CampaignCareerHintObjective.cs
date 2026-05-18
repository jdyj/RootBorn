using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "CampaignObjective_CareerHint", menuName = "Rootborn/Student Life/Campaigns/Objectives/Career Hint")]
    public sealed class CampaignCareerHintObjective : CampaignObjectiveBase
    {
        [SerializeField] private CareerDefinition _career;

        public void ConfigureForTests(string id, CareerDefinition career)
        {
            ConfigureObjectiveForTests(id, false);
            _career = career;
        }

        public override CampaignObjectiveEvaluation Evaluate(in CampaignEvaluationContext context)
        {
            return new CampaignObjectiveEvaluation(context.StudentProgress != null && context.StudentProgress.IsCareerHintUnlocked(_career) ? 1 : 0, 1);
        }
    }
}
