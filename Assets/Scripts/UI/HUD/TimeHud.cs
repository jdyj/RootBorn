using Rootborn.Game.Time;
using Rootborn.Network.Time;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.HUD
{
    public sealed class TimeHud : MonoBehaviour
    {
        [SerializeField] private GameClock _clock;
        [SerializeField] private Text _label;

        public void BindLabelForRuntime(Text label)
        {
            _label = label;
        }

        private void Update()
        {
            if (_label == null) return;

            var worldTime = NetworkWorldTimeState.Active;
            if (worldTime != null)
            {
                _label.text = $"Day {worldTime.CurrentDay} {worldTime.WeekdayName} {worldTime.TimeOfDay} {worldTime.SchedulePhase}";
                return;
            }

            if (_clock == null) return;
            _label.text = $"Day {_clock.Day} ({Mathf.RoundToInt(_clock.DayProgress01 * 100f)}%)";
        }
    }
}
