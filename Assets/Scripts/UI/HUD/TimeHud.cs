using Rootborn.Game.Time;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.HUD
{
    public sealed class TimeHud : MonoBehaviour
    {
        [SerializeField] private GameClock _clock;
        [SerializeField] private Text _label;

        private void Update()
        {
            if (_clock == null || _label == null) return;
            _label.text = $"Day {_clock.Day} ({Mathf.RoundToInt(_clock.DayProgress01 * 100f)}%)";
        }
    }
}
