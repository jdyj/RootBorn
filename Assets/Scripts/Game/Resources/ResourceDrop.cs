using UnityEngine;

namespace Rootborn.Game.Resources
{
    [System.Serializable]
    public struct ResourceDrop
    {
        [SerializeField] private string _resourceId;
        [SerializeField] private int _minCount;
        [SerializeField] private int _maxCount;

        public string ResourceId => _resourceId;
        public int MinCount => _minCount;
        public int MaxCount => _maxCount;

        public int Roll(System.Random rng)
        {
            int min = Mathf.Max(0, _minCount);
            int max = Mathf.Max(min, _maxCount);
            if (rng == null) return max;
            return rng.Next(min, max + 1);
        }
    }
}
