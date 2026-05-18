using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyEventOutcome_SkillDelta", menuName = "Rootborn/Student Life/Daily Events/Outcomes/Skill Delta")]
    public sealed class DailyEventSkillDeltaOutcome : DailyEventOutcomeBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, DailyEventDefinition dailyEvent, DailyEventChoiceDefinition choice)
        {
            if (progress == null || _skill == null || _delta == 0) return string.Empty;
            progress.AddSkill(_skill, _delta);
            return DailyEventLogCodec.EncodeDelta(dailyEvent, choice, _skill.Id, _delta);
        }

        public void ConfigureForTests(SkillDefinition skill, int delta)
        {
            _skill = skill;
            _delta = delta;
        }
    }
}
