using UnityEngine;

namespace Rootborn.Game.Heir
{
    [CreateAssetMenu(fileName = "Trait_New", menuName = "Rootborn/Heir/Trait")]
    public sealed class HeirTrait : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;
        [SerializeField] private float _hungerDecayMul = 1f;
        [SerializeField] private float _fatigueDecayMul = 1f;
        [SerializeField] private float _gatherSpeedMul = 1f;
        [SerializeField] private float _learnSpeedMul = 1f;
        [SerializeField] private bool _isInheritable = true;
        [SerializeField] private bool _isRandomOnly;

        public string Id => _id;
        public string DisplayKey => _displayKey;
        public float HungerDecayMul => _hungerDecayMul;
        public float FatigueDecayMul => _fatigueDecayMul;
        public float GatherSpeedMul => _gatherSpeedMul;
        public float LearnSpeedMul => _learnSpeedMul;
        public bool IsInheritable => _isInheritable;
        public bool IsRandomOnly => _isRandomOnly;
    }
}
