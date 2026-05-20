using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyPlanRequirement_CareerHint", menuName = "Rootborn/Student Life/Daily Plan/Requirements/Career Hint")]
    public sealed class DailyPlanCareerHintRequirement : DailyPlanRouteRequirementBase
    {
        [SerializeField] private CareerDefinition _career;
        [SerializeField] private bool _expectedUnlocked = true;

        public override bool IsSatisfied(StudentLifeProgress progress, DailyPlanRouteDefinition route)
        {
            bool unlocked = progress != null && _career != null && progress.IsCareerHintUnlocked(_career);
            return unlocked == _expectedUnlocked;
        }

        public void ConfigureForTests(CareerDefinition career, bool expectedUnlocked)
        {
            _career = career;
            _expectedUnlocked = expectedUnlocked;
        }
    }
}
