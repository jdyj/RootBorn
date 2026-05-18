using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "CampaignReward_CareerHint", menuName = "Rootborn/Student Life/Campaigns/Rewards/Career Hint")]
    public sealed class CampaignCareerHintReward : CampaignRewardBase
    {
        [SerializeField] private CareerDefinition _career;

        public void ConfigureForTests(string id, CareerDefinition career)
        {
            ConfigureForTests(id, id);
            _career = career;
        }

        public override void Apply(StudentLifeProgress progress)
        {
            if (progress != null) progress.UnlockCareerHint(_career);
        }
    }
}
