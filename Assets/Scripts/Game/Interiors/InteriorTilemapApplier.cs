using System.Collections.Generic;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;

namespace Rootborn.Game.Interiors
{
    public sealed class InteriorTilemapApplier : MonoBehaviour
    {
        [SerializeField] private InteriorTileSetDefinition _tileSet;
        [SerializeField] private Tilemap _floor;
        [SerializeField] private Tilemap _walls;
        [SerializeField] private Tilemap _doors;
        [SerializeField] private Tilemap _decorations;
        [SerializeField] private Tilemap _collision;

        private InteriorGeneratedMap _lastGeneratedMap;

        public InteriorGeneratedMap LastGeneratedMap => _lastGeneratedMap;

        public void Apply(InteriorGeneratedMap map)
        {
            if (map == null)
            {
                return;
            }

            _lastGeneratedMap = map;

            ResolveTilemaps();
            if (_floor == null || _walls == null || _doors == null || _decorations == null || _collision == null)
            {
                Debug.LogWarning("[ROOTBORN/Interiors] House Tilemap layers are not ready.");
                return;
            }

            ClearAll();
            var offset = new Vector3Int(-map.Width / 2, -map.Height / 2, 0);
            foreach (var cell in map.Cells)
            {
                var pos = new Vector3Int(cell.Position.x, cell.Position.y, 0) + offset;
                ApplyBaseTile(map, cell, pos);
                ApplyObjectTile(cell, pos);

                if (map.IsCollision(cell.Position))
                {
                    var collisionTile = ResolveTile(InteriorObjectKind.None, InteriorCellKind.Wall, InteriorFacingDirection.None);
                    if (_tileSet != null && _tileSet.Collision != null)
                    {
                        collisionTile = _tileSet.Collision;
                    }
                    _collision.SetTile(pos, collisionTile);
                }
            }

            MoveHouseSpawn(map, offset);
            CompressAll();
        }

        private void ApplyBaseTile(InteriorGeneratedMap map, InteriorCell cell, Vector3Int pos)
        {
            switch (cell.Kind)
            {
                case InteriorCellKind.Floor:
                case InteriorCellKind.Corridor:
                    _floor.SetTile(pos, ResolveTile(InteriorObjectKind.None, InteriorCellKind.Floor, InteriorFacingDirection.None));
                    break;
                case InteriorCellKind.Wall:
                    _walls.SetTile(pos, ResolveWallTile(map, cell.Position));
                    break;
                case InteriorCellKind.Door:
                    _floor.SetTile(pos, ResolveTile(InteriorObjectKind.None, InteriorCellKind.Floor, InteriorFacingDirection.None));
                    _doors.SetTile(pos, ResolveTile(InteriorObjectKind.None, InteriorCellKind.Door, InteriorFacingDirection.None));
                    break;
                case InteriorCellKind.Window:
                    _walls.SetTile(pos, ResolveTile(InteriorObjectKind.None, InteriorCellKind.Window, InteriorFacingDirection.None));
                    break;
            }
        }

        private TileBase ResolveWallTile(InteriorGeneratedMap map, Vector2Int cell)
        {
            if (_tileSet == null || _tileSet.VerticalWallAccent == null)
            {
                return ResolveTile(InteriorObjectKind.None, InteriorCellKind.Wall, InteriorFacingDirection.None);
            }

            if (IsVerticalWallRun(map, cell) || IsDoorFrameWall(map, cell))
            {
                return _tileSet.VerticalWallAccent;
            }

            return ResolveWallBaseTile();
        }

        private TileBase ResolveFloorBaseTile()
        {
            return _tileSet.Floor;
        }

        private TileBase ResolveWallBaseTile()
        {
            return _tileSet.Wall;
        }

        private static bool IsVerticalWallRun(InteriorGeneratedMap map, Vector2Int cell)
        {
            if (map == null)
            {
                return false;
            }

            return IsWallLike(map.GetKind(cell + Vector2Int.up)) && IsWallLike(map.GetKind(cell + Vector2Int.down));
        }

        private static bool IsDoorFrameWall(InteriorGeneratedMap map, Vector2Int cell)
        {
            if (map == null)
            {
                return false;
            }

            return map.GetKind(cell + Vector2Int.left) == InteriorCellKind.Door
                || map.GetKind(cell + Vector2Int.right) == InteriorCellKind.Door
                || map.GetKind(cell + Vector2Int.up) == InteriorCellKind.Door
                || map.GetKind(cell + Vector2Int.down) == InteriorCellKind.Door;
        }

        private static bool IsWallLike(InteriorCellKind kind)
        {
            return kind == InteriorCellKind.Wall || kind == InteriorCellKind.Window;
        }

        private void ApplyObjectTile(InteriorCell cell, Vector3Int pos)
        {
            if (cell.ObjectKind == InteriorObjectKind.None)
            {
                return;
            }

            var tile = ResolveTile(cell.ObjectKind, InteriorCellKind.Floor, cell.FacingDirection);
            if (tile != null)
            {
                _decorations.SetTile(pos, tile);
            }
        }

        private void MoveHouseSpawn(InteriorGeneratedMap map, Vector3Int offset)
        {
            var spawnCell = new Vector3Int(map.SpawnCell.x, map.SpawnCell.y, 0) + offset;
            var world = _floor.GetCellCenterWorld(spawnCell);
            world.z = 0f;

            var spawn = GameObject.Find("HouseSpawnPoint");
            if (spawn != null)
            {
                spawn.transform.position = world;
            }

            var player = GameObject.Find("Player");
            if (player != null)
            {
                player.transform.position = world;
                if (player.GetComponent<PlayerInteractionRouter>() == null)
                {
                    player.AddComponent<PlayerInteractionRouter>();
                }
            }
        }

        private TileBase ResolveTile(InteriorObjectKind objectKind, InteriorCellKind fallbackKind, InteriorFacingDirection facingDirection)
        {
            if (_tileSet != null)
            {
                switch (objectKind)
                {
                    case InteriorObjectKind.Desk: return _tileSet.ResolveDesk(facingDirection) != null ? _tileSet.ResolveDesk(facingDirection) : ResolveFloorBaseTile();
                    case InteriorObjectKind.Chair: return _tileSet.ResolveChair(facingDirection) != null ? _tileSet.ResolveChair(facingDirection) : ResolveFloorBaseTile();
                    case InteriorObjectKind.Computer: return _tileSet.Computer != null ? _tileSet.Computer : ResolveFloorBaseTile();
                    case InteriorObjectKind.Sofa: return _tileSet.ResolveSofa(facingDirection) != null ? _tileSet.ResolveSofa(facingDirection) : ResolveFloorBaseTile();
                    case InteriorObjectKind.Plant: return _tileSet.Plant != null ? _tileSet.Plant : ResolveFloorBaseTile();
                }

                switch (fallbackKind)
                {
                    case InteriorCellKind.Wall: return ResolveWallBaseTile();
                    case InteriorCellKind.Door: return _tileSet.Door;
                    case InteriorCellKind.Window: return _tileSet.Window;
                    default: return ResolveFloorBaseTile();
                }
            }

            return FindFirstExistingTile(_floor) ?? FindFirstExistingTile(_walls) ?? FindFirstExistingTile(_decorations);
        }

        private static TileBase FindFirstExistingTile(Tilemap tilemap)
        {
            if (tilemap == null)
            {
                return null;
            }

            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(pos);
                if (tile != null)
                {
                    return tile;
                }
            }

            return null;
        }

        private void ResolveTilemaps()
        {
            _floor = _floor != null ? _floor : GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            _walls = _walls != null ? _walls : GameObject.Find("HouseWallTilemap")?.GetComponent<Tilemap>();
            _doors = _doors != null ? _doors : GameObject.Find("HouseDoorTilemap")?.GetComponent<Tilemap>();
            _decorations = _decorations != null ? _decorations : GameObject.Find("HouseDecorationTilemap")?.GetComponent<Tilemap>();
            _collision = _collision != null ? _collision : GameObject.Find("HouseCollisionTilemap")?.GetComponent<Tilemap>();

            if (_doors == null || _doors == _walls)
            {
                _doors = CreateDoorTilemap();
            }

            var collisionRenderer = _collision != null ? _collision.GetComponent<TilemapRenderer>() : null;
            if (collisionRenderer != null)
            {
                collisionRenderer.enabled = false;
            }
        }

        private Tilemap CreateDoorTilemap()
        {
            var parent = _floor != null ? _floor.transform.parent : _walls != null ? _walls.transform.parent : null;
            var go = new GameObject("HouseDoorTilemap", typeof(Tilemap), typeof(TilemapRenderer));
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var renderer = go.GetComponent<TilemapRenderer>();
            var wallRenderer = _walls != null ? _walls.GetComponent<TilemapRenderer>() : null;
            if (renderer != null)
            {
                renderer.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
                renderer.mode = TilemapRenderer.Mode.Chunk;
                renderer.sortingLayerID = wallRenderer != null ? wallRenderer.sortingLayerID : SortingLayer.NameToID("Default");
                renderer.sortingOrder = wallRenderer != null ? wallRenderer.sortingOrder + 1 : 1;
            }

            return go.GetComponent<Tilemap>();
        }
        private void ClearAll()
        {
            _floor.ClearAllTiles();
            _walls.ClearAllTiles();
            if (_doors != _walls)
            {
                _doors.ClearAllTiles();
            }
            _decorations.ClearAllTiles();
            _collision.ClearAllTiles();
        }

        private void CompressAll()
        {
            _floor.CompressBounds();
            _walls.CompressBounds();
            if (_doors != _walls)
            {
                _doors.CompressBounds();
            }
            _decorations.CompressBounds();
            _collision.CompressBounds();
        }
    }
}
