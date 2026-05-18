using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyEventOutcome_StatusDelta", menuName = "Rootborn/Student Life/Daily Events/Outcomes/Status Delta")]
    public sealed class DailyEventStatusDeltaOutcome : DailyEventOutcomeBase
    {
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _delta;

        public override string Apply(StudentLifeProgress progress, DailyEventDefinition dailyEvent, DailyEventChoiceDefinition choice)
        {
            if (progress == null || _status == null || _delta == 0) return string.Empty;
            progress.AddStatus(_status, _delta, dailyEvent != null ? dailyEvent.Id : string.Empty);
            return DailyEventLogCodec.EncodeDelta(dailyEvent, choice, _status.Id, _delta);
        }

        public void ConfigureForTests(StatusDefinition status, int delta)
        {
            _status = status;
            _delta = delta;
        }
    }
}
