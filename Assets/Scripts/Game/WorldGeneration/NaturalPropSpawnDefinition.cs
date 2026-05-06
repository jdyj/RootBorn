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
        [SerializeField] private int _clusterSize = 1;
        [SerializeField] private int _clusterRadius = 0;
        [SerializeField] private int _densityPermille = 0;

        public ResourceNodeDefinition Resource => _resource;
        public int TargetCount
        {
            get
            {
                if (_targetCount > 0)
                {
                    return _targetCount;
                }

                int cells = Mathf.Max(0, _spawnableArea.width) * Mathf.Max(0, _spawnableArea.height);
                return Mathf.Max(0, Mathf.FloorToInt(cells * Mathf.Max(0, _densityPermille) / 1000f));
            }
        }

        public RectInt SpawnableArea => _spawnableArea;
        public int MinDistanceBetweenProps => Mathf.Max(0, _minDistanceBetweenProps);
        public int MaxAttempts => Mathf.Max(0, _maxAttempts);
        public int ClusterSize => Mathf.Max(1, _clusterSize);
        public int ClusterRadius => Mathf.Max(0, _clusterRadius);
        public int DensityPermille => Mathf.Max(0, _densityPermille);

        public int Count => TargetCount;
        public RectInt Area => SpawnableArea;
        public int MinSpacing => MinDistanceBetweenProps;

        public void SetTestData(ResourceNodeDefinition resource, int targetCount, RectInt spawnableArea, int minDistanceBetweenProps, int maxAttempts)
        {
            SetTestData(resource, targetCount, spawnableArea, minDistanceBetweenProps, maxAttempts, 1, 0, 0);
        }

        public void SetTestData(ResourceNodeDefinition resource, int targetCount, RectInt spawnableArea, int minDistanceBetweenProps, int maxAttempts, int clusterSize, int clusterRadius, int densityPermille)
        {
            _resource = resource;
            _targetCount = Mathf.Max(0, targetCount);
            _spawnableArea = spawnableArea;
            _minDistanceBetweenProps = Mathf.Max(0, minDistanceBetweenProps);
            _maxAttempts = Mathf.Max(0, maxAttempts);
            _clusterSize = Mathf.Max(1, clusterSize);
            _clusterRadius = Mathf.Max(0, clusterRadius);
            _densityPermille = Mathf.Max(0, densityPermille);
        }
    }
}
