using Rootborn.Game.Resources;
using UnityEngine;

namespace Rootborn.Game.WorldGeneration
{
    [CreateAssetMenu(fileName = "Spawn_New", menuName = "Rootborn/World Generation/Natural Prop Spawn")]
    public sealed class NaturalPropSpawnDefinition : ScriptableObject
    {
        [SerializeField] private ResourceNodeDefinition _resource;
        [SerializeField] private int _targetCount = 1;
        [SerializeField] private RectInt _spawnableArea = new RectInt(0, 0, 1, 1);
        [SerializeField] private int _minDistanceBetweenProps = 1;
        [SerializeField] private int _maxAttempts = 64;

        public ResourceNodeDefinition Resource => _resource;
        public int TargetCount => Mathf.Max(0, _targetCount);
        public RectInt SpawnableArea => _spawnableArea;
        public int MinDistanceBetweenProps => Mathf.Max(0, _minDistanceBetweenProps);
        public int MaxAttempts => Mathf.Max(0, _maxAttempts);

        public int Count => TargetCount;
        public RectInt Area => SpawnableArea;
        public int MinSpacing => MinDistanceBetweenProps;

        public void SetTestData(ResourceNodeDefinition resource, int targetCount, RectInt spawnableArea, int minDistanceBetweenProps, int maxAttempts)
        {
            _resource = resource;
            _targetCount = Mathf.Max(0, targetCount);
            _spawnableArea = spawnableArea;
            _minDistanceBetweenProps = Mathf.Max(0, minDistanceBetweenProps);
            _maxAttempts = Mathf.Max(0, maxAttempts);
        }
    }
}
