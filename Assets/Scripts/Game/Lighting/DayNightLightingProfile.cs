using UnityEngine;

namespace Rootborn.Game.Lighting
{
    [CreateAssetMenu(fileName = "DayNightLighting_Default", menuName = "Rootborn/Lighting/Day Night Lighting Profile")]
    public sealed class DayNightLightingProfile : ScriptableObject
    {
        [SerializeField] private Gradient _globalColor = CreateDefaultColorGradient();
        [SerializeField] private AnimationCurve _globalIntensity = CreateDefaultIntensityCurve();
        [SerializeField] private float _lightingLerpSpeed = 2f;

        public float LightingLerpSpeed => Mathf.Max(0.01f, _lightingLerpSpeed);

        public Color EvaluateColor(float hour)
        {
            return _globalColor.Evaluate(Mathf.Repeat(hour, 24f) / 24f);
        }

        public float EvaluateIntensity(float hour)
        {
            return Mathf.Max(0f, _globalIntensity.Evaluate(Mathf.Repeat(hour, 24f)));
        }

        private static Gradient CreateDefaultColorGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.09f, 0.16f, 0.29f), 0f),
                    new GradientColorKey(new Color(0.56f, 0.65f, 0.78f), 5f / 24f),
                    new GradientColorKey(new Color(1f, 0.89f, 0.69f), 7f / 24f),
                    new GradientColorKey(Color.white, 12f / 24f),
                    new GradientColorKey(new Color(1f, 0.69f, 0.4f), 17.5f / 24f),
                    new GradientColorKey(new Color(0.18f, 0.29f, 0.47f), 20f / 24f),
                    new GradientColorKey(new Color(0.09f, 0.16f, 0.29f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static AnimationCurve CreateDefaultIntensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(5f, 0.55f),
                new Keyframe(7f, 0.95f),
                new Keyframe(12f, 1f),
                new Keyframe(17.5f, 0.85f),
                new Keyframe(20f, 0.35f),
                new Keyframe(24f, 0.25f));
        }
    }
}
