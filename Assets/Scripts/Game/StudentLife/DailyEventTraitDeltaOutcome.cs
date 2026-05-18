using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyEventOutcome_TraitDelta", menuName = "Rootborn/Student Life/Daily Events/Outcomes/Trait Delta")]
    public sealed class DailyEventTraitDeltaOutcome : DailyEventOutcomeBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, DailyEventDefinition dailyEvent, DailyEventChoiceDefinition choice)
        {
            if (progress == null || _trait == null || _delta == 0) return string.Empty;
            progress.AddTrait(_trait, _delta);
            return DailyEventLogCodec.EncodeDelta(dailyEvent, choice, _trait.Id, _delta);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta)
        {
            _trait = trait;
            _delta = delta;
        }
    }
}
