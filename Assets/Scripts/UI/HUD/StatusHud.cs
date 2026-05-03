using Rootborn.Game.Status;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.HUD
{
    public sealed class StatusHud : MonoBehaviour
    {
        [SerializeField] private PlayerStatus _playerStatus;
        [SerializeField] private StatusEffectDefinition _trackedStatus;
        [SerializeField] private Slider _slider;
        [SerializeField] private Text _label;

        private void Update()
        {
            if (_playerStatus == null || _trackedStatus == null) return;
            var v = _playerStatus.Get(_trackedStatus);
            if (v == null) return;
            float ratio = _trackedStatus.MaxValue > 0f ? v.Current / _trackedStatus.MaxValue : 0f;
            if (_slider != null) _slider.value = Mathf.Clamp01(ratio);
            if (_label != null) _label.text = $"{_trackedStatus.DisplayKey}: {Mathf.RoundToInt(v.Current)}";
        }
    }
}
