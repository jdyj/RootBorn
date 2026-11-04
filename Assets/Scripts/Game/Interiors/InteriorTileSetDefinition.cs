using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Interiors
{
    [Serializable]
    public sealed class InteriorTileStyleOption
    {
        [SerializeField] private string _id;
        [SerializeField] private TileBase _floor;
        [SerializeField] private TileBase _wall;
        [SerializeField] private TileBase _verticalWallAccent;

        public string Id => _id ?? string.Empty;
        public TileBase Floor => _floor;
        public TileBase Wall => _wall;
        public TileBase VerticalWallAccent => _verticalWallAccent;

        public bool Matches(string styleId)
        {
            return !string.IsNullOrEmpty(styleId) && string.Equals(Id, styleId, StringComparison.Ordinal);
        }

        public static InteriorTileStyleOption CreateForTests(string id, TileBase floor, TileBase wall, TileBase verticalWallAccent)
        {
            return new InteriorTileStyleOption
            {
                _id = id,
                _floor = floor,
                _wall = wall,
                _verticalWallAccent = verticalWallAccent
            };
        }
    }

    [CreateAssetMenu(fileName = "InteriorTileSet_New", menuName = "Rootborn/Interiors/Tile Set")]
    public sealed class InteriorTileSetDefinition : ScriptableObject
    {
        [SerializeField] private TileBase _floor;
        [SerializeField] private TileBase _wall;
        [SerializeField] private TileBase _verticalWallAccent;
        [SerializeField] private TileBase _door;
        [SerializeField] private TileBase _window;
        [SerializeField] private TileBase _desk;
        [SerializeField] private TileBase _deskNorth;
        [SerializeField] private TileBase _deskEast;
        [SerializeField] private TileBase _deskSouth;
        [SerializeField] private TileBase _deskWest;
        [SerializeField] private TileBase _chair;
        [SerializeField] private TileBase _chairNorth;
        [SerializeField] private TileBase _chairEast;
        [SerializeField] private TileBase _chairSouth;
        [SerializeField] private TileBase _chairWest;
        [SerializeField] private TileBase _computer;
        [SerializeField] private TileBase _sofa;
        [SerializeField] private TileBase _sofaNorth;
        [SerializeField] private TileBase _sofaEast;
        [SerializeField] private TileBase _sofaSouth;
        [SerializeField] private TileBase _sofaWest;
        [SerializeField] private TileBase _plant;
        [SerializeField] private TileBase _collision;
        [SerializeField] private InteriorTileStyleOption[] _styleOptions = Array.Empty<InteriorTileStyleOption>();

        public TileBase Floor => _floor;
        public TileBase Wall => _wall != null ? _wall : _floor;
        public TileBase VerticalWallAccent => _verticalWallAccent != null ? _verticalWallAccent : Window;
        public TileBase Door => _door != null ? _door : _floor;
        public TileBase Window => _window != null ? _window : Wall;
        public TileBase Desk => _desk;
        public TileBase Chair => _chair;
        public TileBase Computer => _computer;
        public TileBase Sofa => _sofa;
        public TileBase Plant => _plant;
        public TileBase Collision => _collision != null ? _collision : Wall;

        public InteriorTileStyleOption ResolveStyle(string styleId)
        {
            if (string.IsNullOrEmpty(styleId) || _styleOptions == null || _styleOptions.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < _styleOptions.Length; i++)
            {
                var option = _styleOptions[i];
                if (option != null && option.Matches(styleId))
                {
                    return option;
                }
            }

            return null;
        }

        public void ConfigureForTests(TileBase floor, TileBase wall, TileBase verticalWallAccent, TileBase desk)
        {
            _floor = floor;
            _wall = wall;
            _verticalWallAccent = verticalWallAccent;
            _desk = desk;
        }

        public void ConfigureStyleOptionsForTests(InteriorTileStyleOption[] styleOptions)
        {
            _styleOptions = styleOptions ?? Array.Empty<InteriorTileStyleOption>();
        }

        public TileBase ResolveDesk(InteriorFacingDirection direction)
        {
            return ResolveDirectional(direction, _deskNorth, _deskEast, _deskSouth, _deskWest, _desk);
        }

        public TileBase ResolveChair(InteriorFacingDirection direction)
        {
            return ResolveDirectional(direction, _chairNorth, _chairEast, _chairSouth, _chairWest, _chair);
        }

        public TileBase ResolveSofa(InteriorFacingDirection direction)
        {
            return ResolveDirectional(direction, _sofaNorth, _sofaEast, _sofaSouth, _sofaWest, _sofa);
        }

        private static TileBase ResolveDirectional(InteriorFacingDirection direction, TileBase north, TileBase east, TileBase south, TileBase west, TileBase fallback)
        {
            switch (direction)
            {
                case InteriorFacingDirection.North: return north != null ? north : fallback;
                case InteriorFacingDirection.East: return east != null ? east : fallback;
                case InteriorFacingDirection.South: return south != null ? south : fallback;
                case InteriorFacingDirection.West: return west != null ? west : fallback;
                default: return fallback;
            }
        }
    }
}
