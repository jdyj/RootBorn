using Rootborn.Game.Time;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rootborn.Game.Lighting
{
    public sealed class LightingManager : MonoBehaviour
    {
        [SerializeField] private Light2D _globalLight;
        [SerializeField] private DayNightLightingProfile _profile;
        [SerializeField] private GameClock _clock;
        private LifeTimePresentation _lifeTime;
        private Color _targetColor = Color.white;
        private float _targetIntensity = 1f;

        public void Configure(Light2D globalLight, DayNightLightingProfile profile, GameClock clock = null)
        {
            if (_clock != null) _clock.OnTimeChanged -= HandleClockTime;
            _globalLight = globalLight;
            _profile = profile;
            _clock = clock;
            ResolveClock();
            if (isActiveAndEnabled && _clock != null) _clock.OnTimeChanged += HandleClockTime;
            RefreshSource();
        }

        public void BindLifeTimePresentation(LifeTimePresentation source)
        {
            if (_lifeTime != null) _lifeTime.Changed -= RefreshSource;
            _lifeTime = source;
            if (_lifeTime != null && isActiveAndEnabled) _lifeTime.Changed += RefreshSource;
            RefreshSource();
        }

        private void OnEnable()
        {
            ResolveClock();
            if (_clock != null) _clock.OnTimeChanged += HandleClockTime;
            LifeTimePresentation.AvailabilityChanged += ResolveLifeTime;
            ResolveLifeTime();
        }

        private void OnDisable()
        {
            if (_clock != null) _clock.OnTimeChanged -= HandleClockTime;
            if (_lifeTime != null) _lifeTime.Changed -= RefreshSource;
            LifeTimePresentation.AvailabilityChanged -= ResolveLifeTime;
        }

        private void Start()
        {
            ResolveClock();
            if (_clock != null)
            {
                _clock.OnTimeChanged -= HandleClockTime;
                _clock.OnTimeChanged += HandleClockTime;
            }
            ResolveLifeTime();
        }

        private void ResolveLifeTime() => BindLifeTimePresentation(LifeTimePresentation.FindInScene(gameObject.scene));
        private void RefreshSource() => ApplyTimeOfDay(_lifeTime != null && _lifeTime.HasTime ? _lifeTime.Hour : _clock != null ? _clock.CurrentHour : 12f, float.PositiveInfinity);
        private void HandleClockTime(float hour)
        {
            if (_lifeTime == null || !_lifeTime.HasTime) CaptureTarget(hour);
        }

        private void Update() => ApplyTarget(UnityEngine.Time.deltaTime);

        public void ApplyTimeOfDay(float hour, float deltaSeconds)
        {
            CaptureTarget(hour);
            ApplyTarget(deltaSeconds);
        }

        private void ApplyTarget(float deltaSeconds)
        {
            if (_globalLight == null || _profile == null) return;
            float t = float.IsPositiveInfinity(deltaSeconds) ? 1f : 1f - Mathf.Exp(-_profile.LightingLerpSpeed * Mathf.Max(0f, deltaSeconds));
            _globalLight.color = Color.Lerp(_globalLight.color, _targetColor, t);
            _globalLight.intensity = Mathf.Lerp(_globalLight.intensity, _targetIntensity, t);
        }

        private void CaptureTarget(float hour)
        {
            if (_profile == null) return;
            _targetColor = _profile.EvaluateColor(hour);
            _targetIntensity = _profile.EvaluateIntensity(hour);
        }

        private void ResolveClock()
        {
            if (_clock == null) _clock = GameClock.Instance;
        }
    }
}
