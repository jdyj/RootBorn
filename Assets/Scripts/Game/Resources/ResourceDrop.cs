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
    }
}
