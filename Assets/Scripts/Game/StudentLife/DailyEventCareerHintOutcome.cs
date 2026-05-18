using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyEventOutcome_CareerHint", menuName = "Rootborn/Student Life/Daily Events/Outcomes/Career Hint")]
    public sealed class DailyEventCareerHintOutcome : DailyEventOutcomeBase
    {
        [SerializeField] private CareerDefinition _career;

        public override string Apply(StudentLifeProgress progress, DailyEventDefinition dailyEvent, DailyEventChoiceDefinition choice)
        {
            if (progress == null || _career == null) return string.Empty;
            bool wasUnlocked = progress.IsCareerHintUnlocked(_career);
            progress.UnlockCareerHint(_career);
            return wasUnlocked ? string.Empty : DailyEventLogCodec.EncodeUnlock(dailyEvent, choice, _career.Id);
        }

        public void ConfigureForTests(CareerDefinition career)
        {
            _career = career;
        }
    }
}
