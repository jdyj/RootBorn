using Rootborn.Game.Time;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Rootborn.Game.Lighting
{
    public sealed class LocalLightController : MonoBehaviour
    {
        [SerializeField] private Light2D _light;
        [SerializeField] private GameClock _clock;
        [SerializeField] private float _dayIntensity;
        [SerializeField] private float _nightIntensity = 0.85f;
        [SerializeField] private float _eveningIntensity = 0.6f;
        [SerializeField] private float _lerpSpeed = 6f;

        private float _targetIntensity;
        private LifeTimePresentation _lifeTime;

        public void Configure(Light2D targetLight, float dayIntensity, float nightIntensity)
        {
            _light = targetLight;
            _dayIntensity = Mathf.Max(0f, dayIntensity);
            _nightIntensity = Mathf.Max(0f, nightIntensity);
        }

        private void Awake()
        {
            if (_light == null)
            {
                _light = GetComponent<Light2D>();
            }
        }

        private void OnEnable()
        {
            LifeTimePresentation.AvailabilityChanged += ResolveLifeTime;
            ResolveLifeTime();
            ResolveClock();
            if (_clock != null)
            {
                _clock.OnTimeChanged += HandleClockTime;
            }
        }

        private void OnDisable()
        {
            LifeTimePresentation.AvailabilityChanged -= ResolveLifeTime;
            if (_lifeTime != null) _lifeTime.Changed -= RefreshSource;
            if (_clock != null)
            {
                _clock.OnTimeChanged -= HandleClockTime;
            }
        }

        private void Start()
        {
            ResolveLifeTime();
            ApplyImmediate();
        }

        public void BindLifeTimePresentation(LifeTimePresentation source)
        {
            if (_lifeTime != null) _lifeTime.Changed -= RefreshSource;
            _lifeTime = source;
            if (_lifeTime != null && isActiveAndEnabled) _lifeTime.Changed += RefreshSource;
            RefreshSource();
        }

        private void ResolveLifeTime() => BindLifeTimePresentation(LifeTimePresentation.FindInScene(gameObject.scene));
        private void RefreshSource() => ApplyTimeOfDay(_lifeTime != null && _lifeTime.HasTime ? _lifeTime.Hour : _clock != null ? _clock.CurrentHour : 12f);
        private void HandleClockTime(float hour)
        {
            if (_lifeTime == null || !_lifeTime.HasTime) ApplyTimeOfDay(hour);
        }

        private void Update()
        {
            if (_light == null)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, _lerpSpeed) * UnityEngine.Time.deltaTime);
            _light.intensity = Mathf.Lerp(_light.intensity, _targetIntensity, t);
        }

        public void ApplyTimeOfDay(float hour)
        {
            if (_light == null)
            {
                return;
            }

            if (GameClock.IsNightHour(hour))
            {
                _targetIntensity = _nightIntensity;
            }
            else if (GameClock.IsEveningHour(hour))
            {
                _targetIntensity = _eveningIntensity;
            }
            else
            {
                _targetIntensity = _dayIntensity;
            }

            ApplyImmediate();
        }

        private void ApplyImmediate()
        {
            if (_light != null)
            {
                _light.intensity = _targetIntensity;
            }
        }

        private void ResolveClock()
        {
            if (_clock == null)
            {
                _clock = GameClock.Instance;
            }
        }
    }
}
