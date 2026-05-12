using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Tiles
{
    [CreateAssetMenu(fileName = "Tile_Placeable", menuName = "Rootborn/Tiles/Placeable Tile")]
    public sealed class PlaceableTileDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private TileBase _tile;
        [SerializeField] private string _targetLayer = "TownDecorationTilemap";

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public TileBase Tile => _tile;
        public string TargetLayer => string.IsNullOrEmpty(_targetLayer) ? "TownDecorationTilemap" : _targetLayer;

        public void ConfigureForRuntime(string id, string displayNameKey, TileBase tile, string targetLayer)
        {
            _id = id;
            _displayNameKey = displayNameKey;
            _tile = tile;
            _targetLayer = string.IsNullOrEmpty(targetLayer) ? "TownDecorationTilemap" : targetLayer;
        }
    }
}
