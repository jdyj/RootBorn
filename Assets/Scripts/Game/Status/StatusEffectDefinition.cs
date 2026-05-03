using UnityEngine;

namespace Rootborn.Game.Status
{
    [CreateAssetMenu(fileName = "Status_New", menuName = "Rootborn/Status/Status Effect")]
    public sealed class StatusEffectDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;
        [SerializeField] private float _maxValue = 100f;
        [SerializeField] private float _decayPerSecond = 1f;
        [SerializeField] private AnimationCurve _gameplayPenalty = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public string Id => _id;
        public string DisplayKey => _displayKey;
        public float MaxValue => _maxValue;
        public float DecayPerSecond => _decayPerSecond;

        public float EvaluatePenalty01(float currentValue)
        {
            float t = Mathf.Clamp01(currentValue / Mathf.Max(0.0001f, _maxValue));
            return Mathf.Clamp01(_gameplayPenalty.Evaluate(t));
        }
    }
}
