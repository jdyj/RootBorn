using UnityEngine;
        
        namespace Rootborn.Game.StudentLife
        {
            [CreateAssetMenu(fileName = "OutsideSchoolOutcome_SkillDelta", menuName = "Rootborn/Student Life/Outside School/Outcomes/Skill Delta")]
            public sealed class OutsideSchoolSkillDeltaOutcome : OutsideSchoolOutcomeBase
            {
                [SerializeField] private SkillDefinition _skill;
                [SerializeField] private int _delta;
        
                public SkillDefinition Skill => _skill;
                public int Delta => _delta;
        
                public override string Apply(StudentLifeProgress progress, string activityId)
                {
                    if (progress == null || _skill == null || _delta == 0)
                    {
                        return string.Empty;
                    }
        
                    progress.AddSkill(_skill, _delta);
                    return OutsideSchoolLogCodec.EncodeDelta(activityId, _skill.Id, _delta);
                }
        
                public void ConfigureForTests(SkillDefinition skill, int delta)
                {
                    _skill = skill;
                    _delta = delta;
                }
            }
        }
        