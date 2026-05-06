using UnityEngine;

namespace Rootborn.Game.WorldGeneration
{
    [CreateAssetMenu(fileName = "Terrain_New", menuName = "Rootborn/World Generation/Terrain Generation")]
    public sealed class TerrainGenerationDefinition : ScriptableObject
    {
        [SerializeField] private int _width = 30;
        [SerializeField] private int _height = 20;
        [SerializeField] private TilePatternDefinition _basePattern;
        [SerializeField] private TerrainReservedArea[] _reservedAreas = System.Array.Empty<TerrainReservedArea>();
        [SerializeField] private NaturalPropSpawnDefinition[] _naturalPropSpawns = System.Array.Empty<NaturalPropSpawnDefinition>();

        public int Width => Mathf.Max(1, _width);
        public int Height => Mathf.Max(1, _height);
        public TilePatternDefinition BasePattern => _basePattern;
        public TerrainReservedArea[] ReservedAreas => _reservedAreas;
        public NaturalPropSpawnDefinition[] NaturalPropSpawns => _naturalPropSpawns;

        public void SetTestData(int width, int height, TilePatternDefinition basePattern, TerrainReservedArea[] reservedAreas, NaturalPropSpawnDefinition[] naturalPropSpawns)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            _basePattern = basePattern;
            _reservedAreas = reservedAreas ?? System.Array.Empty<TerrainReservedArea>();
            _naturalPropSpawns = naturalPropSpawns ?? System.Array.Empty<NaturalPropSpawnDefinition>();
        }
    }
}