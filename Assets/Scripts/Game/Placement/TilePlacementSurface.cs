using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Placement
{
    public sealed class TilePlacementSurface : MonoBehaviour
    {
        [SerializeField] private string _surfaceId = string.Empty;
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField] private Tilemap _objectTilemap;
        [SerializeField] private Tilemap _occupancyTilemap;
        [SerializeField] private Tilemap _previewTilemap;
        [SerializeField] private BoundsInt _bounds;
        [SerializeField] private TilePlacementRuleFlags _placementRules = TilePlacementRuleFlags.RequireGround | TilePlacementRuleFlags.RejectOccupiedCells | TilePlacementRuleFlags.StayInsideBounds;

        public string SurfaceId => _surfaceId;
        public Tilemap GroundTilemap => _groundTilemap;
        public Tilemap ObjectTilemap => _objectTilemap;
        public Tilemap OccupancyTilemap => _occupancyTilemap;
        public Tilemap PreviewTilemap => _previewTilemap;
        public BoundsInt Bounds => _bounds;
        public TilePlacementRuleFlags PlacementRules => _placementRules;

        public void ConfigureForTests(
            string surfaceId,
            Tilemap groundTilemap,
            Tilemap objectTilemap,
            Tilemap occupancyTilemap,
            Tilemap previewTilemap,
            BoundsInt bounds,
            TilePlacementRuleFlags placementRules)
        {
            _surfaceId = surfaceId ?? string.Empty;
            _groundTilemap = groundTilemap;
            _objectTilemap = objectTilemap;
            _occupancyTilemap = occupancyTilemap;
            _previewTilemap = previewTilemap;
            _bounds = bounds;
            _placementRules = placementRules;
        }
    }
}
