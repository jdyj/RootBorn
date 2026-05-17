using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Interiors
{
    public enum InteriorCellKind
    {
        Void,
        Floor,
        Wall,
        Corridor,
        Door,
        Window
    }

    public enum InteriorObjectKind
    {
        None,
        Desk,
        Chair,
        Computer,
        Sofa,
        Shelf,
        Plant,
        Light,
        OfficeProp
    }

    public enum InteriorFacingDirection
    {
        None,
        North,
        East,
        South,
        West
    }

    public enum InteriorPlacementPreference
    {
        Any,
        AvoidCorridor,
        NearWall,
        NearWindow
    }

    [CreateAssetMenu(fileName = "InteriorProfile_New", menuName = "Rootborn/Interiors/Generation Profile")]
    public sealed class InteriorGenerationProfile : ScriptableObject
    {
        [SerializeField] private Vector2Int _size = new Vector2Int(28, 18);
        [SerializeField] private int _minCorridorWidth = 2;
        [SerializeField] private int _deskClusterCount = 3;
        [SerializeField] private int _plantCount = 4;
        [SerializeField] private InteriorFurniturePlacementRequest[] _placementRequests = Array.Empty<InteriorFurniturePlacementRequest>();

        public Vector2Int Size => new Vector2Int(Mathf.Max(12, _size.x), Mathf.Max(10, _size.y));
        public int MinCorridorWidth => Mathf.Max(2, _minCorridorWidth);
        public int DeskClusterCount => Mathf.Max(1, _deskClusterCount);
        public int PlantCount => Mathf.Max(0, _plantCount);
        public IReadOnlyList<InteriorFurniturePlacementRequest> PlacementRequests => _placementRequests ?? Array.Empty<InteriorFurniturePlacementRequest>();

        public void ConfigurePlacementRequestsForTests(InteriorFurniturePlacementRequest[] requests)
        {
            _placementRequests = requests ?? Array.Empty<InteriorFurniturePlacementRequest>();
        }

        public static InteriorGenerationProfile CreateDefaultOfficeForTests()
        {
            var profile = CreateInstance<InteriorGenerationProfile>();
            profile._size = new Vector2Int(28, 18);
            profile._minCorridorWidth = 2;
            profile._deskClusterCount = 3;
            profile._plantCount = 4;
            profile._placementRequests = Array.Empty<InteriorFurniturePlacementRequest>();
            return profile;
        }

        public static InteriorGenerationProfile CreateExpandedOfficeForTests()
        {
            var profile = CreateInstance<InteriorGenerationProfile>();
            profile._size = new Vector2Int(34, 22);
            profile._minCorridorWidth = 2;
            profile._deskClusterCount = 4;
            profile._plantCount = 6;
            profile._placementRequests = Array.Empty<InteriorFurniturePlacementRequest>();
            return profile;
        }
    }

    [Serializable]
    public sealed class InteriorFurniturePlacementRequest
    {
        [SerializeField] private InteriorObjectKind _objectKind = InteriorObjectKind.Plant;
        [SerializeField] private int _count = 1;
        [SerializeField] private InteriorPlacementPreference _preference = InteriorPlacementPreference.AvoidCorridor;
        [SerializeField] private InteriorFacingDirection _facingDirection = InteriorFacingDirection.None;
        [SerializeField] private bool _required;

        public InteriorObjectKind ObjectKind => _objectKind;
        public int Count => Mathf.Max(0, _count);
        public InteriorPlacementPreference Preference => _preference;
        public InteriorFacingDirection FacingDirection => _facingDirection;
        public bool Required => _required;

        public static InteriorFurniturePlacementRequest CreateForTests(InteriorObjectKind objectKind, int count, InteriorPlacementPreference preference, InteriorFacingDirection facingDirection, bool required)
        {
            return new InteriorFurniturePlacementRequest
            {
                _objectKind = objectKind,
                _count = count,
                _preference = preference,
                _facingDirection = facingDirection,
                _required = required
            };
        }
    }

    public sealed class InteriorPlacementException : Exception
    {
        public InteriorPlacementException(string message) : base(message)
        {
        }
    }

    public static class InteriorDirectionUtility
    {
        public static Vector2Int ToVector(InteriorFacingDirection direction)
        {
            switch (direction)
            {
                case InteriorFacingDirection.North: return Vector2Int.up;
                case InteriorFacingDirection.East: return Vector2Int.right;
                case InteriorFacingDirection.South: return Vector2Int.down;
                case InteriorFacingDirection.West: return Vector2Int.left;
                default: return Vector2Int.zero;
            }
        }
    }

    public readonly struct InteriorCell
    {
        public InteriorCell(Vector2Int position, InteriorCellKind kind, InteriorObjectKind objectKind, bool blocksMovement, InteriorFacingDirection facingDirection)
        {
            Position = position;
            Kind = kind;
            ObjectKind = objectKind;
            BlocksMovement = blocksMovement;
            FacingDirection = facingDirection;
        }

        public Vector2Int Position { get; }
        public InteriorCellKind Kind { get; }
        public InteriorObjectKind ObjectKind { get; }
        public bool BlocksMovement { get; }
        public InteriorFacingDirection FacingDirection { get; }
    }

    public readonly struct InteriorPlacedObject
    {
        public InteriorPlacedObject(Vector2Int cell, InteriorObjectKind objectKind, bool blocksMovement, InteriorFacingDirection facingDirection)
        {
            Cell = cell;
            ObjectKind = objectKind;
            BlocksMovement = blocksMovement;
            FacingDirection = facingDirection;
        }

        public Vector2Int Cell { get; }
        public InteriorObjectKind ObjectKind { get; }
        public bool BlocksMovement { get; }
        public InteriorFacingDirection FacingDirection { get; }
    }

    public sealed class InteriorGeneratedMap
    {
        private readonly InteriorCellKind[,] _kinds;
        private readonly InteriorObjectKind[,] _objects;
        private readonly bool[,] _objectBlocks;
        private readonly InteriorFacingDirection[,] _objectFacingDirections;
        private readonly HashSet<Vector2Int> _collision = new HashSet<Vector2Int>();
        private readonly List<InteriorPlacedObject> _placedObjects = new List<InteriorPlacedObject>();

        public InteriorGeneratedMap(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            _kinds = new InteriorCellKind[Width, Height];
            _objects = new InteriorObjectKind[Width, Height];
            _objectBlocks = new bool[Width, Height];
            _objectFacingDirections = new InteriorFacingDirection[Width, Height];
        }

        public int Width { get; }
        public int Height { get; }
        public int RoomCount { get; set; }
        public Vector2Int SpawnCell { get; set; }
        public IReadOnlyList<InteriorPlacedObject> PlacedObjects => _placedObjects;

        public IEnumerable<InteriorCell> Cells
        {
            get
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        var position = new Vector2Int(x, y);
                        yield return new InteriorCell(position, _kinds[x, y], _objects[x, y], _objectBlocks[x, y], _objectFacingDirections[x, y]);
                    }
                }
            }
        }

        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        public InteriorCellKind GetKind(Vector2Int cell)
        {
            return InBounds(cell) ? _kinds[cell.x, cell.y] : InteriorCellKind.Void;
        }

        public InteriorObjectKind GetObject(Vector2Int cell)
        {
            return InBounds(cell) ? _objects[cell.x, cell.y] : InteriorObjectKind.None;
        }

        public InteriorFacingDirection GetFacingDirection(Vector2Int cell)
        {
            return InBounds(cell) ? _objectFacingDirections[cell.x, cell.y] : InteriorFacingDirection.None;
        }

        public void SetKind(Vector2Int cell, InteriorCellKind kind)
        {
            if (!InBounds(cell)) return;
            _kinds[cell.x, cell.y] = kind;
            RecomputeCollision(cell);
        }

        public bool TryPlaceObject(Vector2Int cell, InteriorObjectKind kind, bool blocksMovement)
        {
            return TryPlaceObject(cell, kind, blocksMovement, InteriorFacingDirection.None);
        }

        public bool TryPlaceObject(Vector2Int cell, InteriorObjectKind kind, bool blocksMovement, InteriorFacingDirection facingDirection)
        {
            if (!InBounds(cell) || !IsWalkableBase(cell) || _objects[cell.x, cell.y] != InteriorObjectKind.None)
            {
                return false;
            }

            _objects[cell.x, cell.y] = kind;
            _objectBlocks[cell.x, cell.y] = blocksMovement;
            _objectFacingDirections[cell.x, cell.y] = facingDirection;
            _placedObjects.Add(new InteriorPlacedObject(cell, kind, blocksMovement, facingDirection));
            RecomputeCollision(cell);
            return true;
        }

        public bool ClearObject(Vector2Int cell)
                {
                    if (!InBounds(cell) || _objects[cell.x, cell.y] == InteriorObjectKind.None)
                    {
                        return false;
                    }
        
                    var removedKind = _objects[cell.x, cell.y];
                    _objects[cell.x, cell.y] = InteriorObjectKind.None;
                    _objectBlocks[cell.x, cell.y] = false;
                    _objectFacingDirections[cell.x, cell.y] = InteriorFacingDirection.None;
                    for (int i = _placedObjects.Count - 1; i >= 0; i--)
                    {
                        if (_placedObjects[i].Cell == cell && _placedObjects[i].ObjectKind == removedKind)
                        {
                            _placedObjects.RemoveAt(i);
                            break;
                        }
                    }
        
                    RecomputeCollision(cell);
                    return true;
                }
        
                public bool IsWalkable(Vector2Int cell)
        {
            return InBounds(cell) && IsWalkableBase(cell) && !_collision.Contains(cell);
        }

        public bool IsWalkableBase(Vector2Int cell)
        {
            if (!InBounds(cell)) return false;
            var kind = _kinds[cell.x, cell.y];
            return kind == InteriorCellKind.Floor || kind == InteriorCellKind.Corridor || kind == InteriorCellKind.Door;
        }

        public bool IsCollision(Vector2Int cell)
        {
            return InBounds(cell) && _collision.Contains(cell);
        }

        public int CountCells(InteriorCellKind kind)
        {
            int count = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (_kinds[x, y] == kind) count++;
                }
            }
            return count;
        }

        public int CountObjects(InteriorObjectKind kind)
        {
            int count = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (_objects[x, y] == kind) count++;
                }
            }
            return count;
        }

        public string Signature()
        {
            var builder = new StringBuilder(Width * Height * 5);
            builder.Append(Width).Append('x').Append(Height).Append('|');
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    builder.Append((int)_kinds[x, y]);
                    builder.Append(':');
                    builder.Append((int)_objects[x, y]);
                    builder.Append(_objectBlocks[x, y] ? '1' : '0');
                    builder.Append(':');
                    builder.Append((int)_objectFacingDirections[x, y]);
                    builder.Append(',');
                }
            }
            return builder.ToString();
        }

        private void RecomputeCollision(Vector2Int cell)
        {
            if (!InBounds(cell)) return;
            bool blocked = _kinds[cell.x, cell.y] == InteriorCellKind.Wall || _objectBlocks[cell.x, cell.y];
            if (blocked)
            {
                _collision.Add(cell);
            }
            else
            {
                _collision.Remove(cell);
            }
        }
    }

    public static class InteriorGenerator
    {
        public static InteriorGeneratedMap Generate(InteriorGenerationProfile profile, int seed)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var rng = new System.Random(seed);
            var size = profile.Size;
            var map = new InteriorGeneratedMap(size.x, size.y);
            InteriorLayoutGenerator.GenerateLayout(map, profile, rng);
            InteriorFurniturePlanner.PlaceFurniture(map, profile, rng);

            if (!InteriorPathValidator.CanReachAnyDoor(map, map.SpawnCell))
            {
                throw new InvalidOperationException("Generated House interior is not connected from spawn to a door.");
            }

            return map;
        }
    }

    public static class InteriorLayoutGenerator
    {
        public static void GenerateLayout(InteriorGeneratedMap map, InteriorGenerationProfile profile, System.Random rng)
        {
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    bool edge = x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1;
                    map.SetKind(new Vector2Int(x, y), edge ? InteriorCellKind.Wall : InteriorCellKind.Floor);
                }
            }

            int corridorCenter = Mathf.Clamp(map.Height / 2 + rng.Next(-1, 2), 4, map.Height - 5);
            int corridorMin = corridorCenter - profile.MinCorridorWidth / 2;
            int corridorMaxExclusive = corridorMin + profile.MinCorridorWidth;
            int splitX = Mathf.Clamp(map.Width / 2 + rng.Next(-2, 3), 8, map.Width - 9);

            for (int x = 1; x < map.Width - 1; x++)
            {
                for (int y = corridorMin; y < corridorMaxExclusive; y++)
                {
                    map.SetKind(new Vector2Int(x, y), InteriorCellKind.Corridor);
                }
            }

            for (int y = 1; y < map.Height - 1; y++)
            {
                if (y >= corridorMin && y < corridorMaxExclusive) continue;
                map.SetKind(new Vector2Int(splitX, y), InteriorCellKind.Wall);
            }

            int topDivider = Mathf.Clamp(corridorMaxExclusive + 3, corridorMaxExclusive + 2, map.Height - 4);
            int bottomDivider = Mathf.Clamp(corridorMin - 3, 3, corridorMin - 2);
            CarveHorizontalWall(map, bottomDivider, 1, splitX - 1, splitX / 2);
            CarveHorizontalWall(map, topDivider, splitX + 1, map.Width - 2, splitX + (map.Width - splitX) / 2);

            map.SetKind(new Vector2Int(map.Width / 2, 0), InteriorCellKind.Door);
            map.SetKind(new Vector2Int(map.Width / 2, 1), InteriorCellKind.Corridor);
            map.SpawnCell = new Vector2Int(map.Width / 2, 2);
            map.SetKind(map.SpawnCell, InteriorCellKind.Corridor);

            AddWindows(map, rng);
            map.RoomCount = 4;
        }

        private static void CarveHorizontalWall(InteriorGeneratedMap map, int y, int xMin, int xMax, int doorX)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                var cell = new Vector2Int(x, y);
                map.SetKind(cell, x == doorX ? InteriorCellKind.Door : InteriorCellKind.Wall);
            }
        }

        private static void AddWindows(InteriorGeneratedMap map, System.Random rng)
        {
            int leftWindowY = Mathf.Clamp(map.Height - 4 + rng.Next(-1, 2), 2, map.Height - 3);
            int rightWindowY = Mathf.Clamp(3 + rng.Next(-1, 2), 2, map.Height - 3);
            map.SetKind(new Vector2Int(0, leftWindowY), InteriorCellKind.Window);
            map.SetKind(new Vector2Int(map.Width - 1, rightWindowY), InteriorCellKind.Window);
        }
    }

    public static class InteriorFurniturePlanner
    {
        private static readonly InteriorFacingDirection[] CardinalDirections =
        {
            InteriorFacingDirection.North,
            InteriorFacingDirection.East,
            InteriorFacingDirection.South,
            InteriorFacingDirection.West
        };

        public static void PlaceFurniture(InteriorGeneratedMap map, InteriorGenerationProfile profile, System.Random rng)
        {
            PlaceRequestedFurniture(map, profile, rng);
            PlaceDesks(map, profile, rng);
            PlaceSofa(map, rng);
            PlacePlants(map, profile, rng);
        }

        private static void PlaceRequestedFurniture(InteriorGeneratedMap map, InteriorGenerationProfile profile, System.Random rng)
        {
            var requests = profile.PlacementRequests;
            for (int i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request == null || request.Count <= 0)
                {
                    continue;
                }

                int placed = 0;
                for (int count = 0; count < request.Count; count++)
                {
                    if (TryPlaceRequestedFurniture(map, request, rng))
                    {
                        placed++;
                    }
                }

                if (request.Required && placed < request.Count)
                {
                    throw new InteriorPlacementException("Could not satisfy required furniture placement request for " + request.ObjectKind + ": requested " + request.Count + ", placed " + placed + ".");
                }
            }
        }

        private static bool TryPlaceRequestedFurniture(InteriorGeneratedMap map, InteriorFurniturePlacementRequest request, System.Random rng)
        {
            if (request.ObjectKind == InteriorObjectKind.Desk || request.ObjectKind == InteriorObjectKind.Computer)
            {
                return TryPlaceRequestedDeskCluster(map, request, rng);
            }

            if (request.ObjectKind == InteriorObjectKind.Sofa)
            {
                return TryPlaceRequestedSofa(map, request, rng);
            }

            return TryPlaceRequestedSingleObject(map, request, rng);
        }

        private static bool TryPlaceRequestedDeskCluster(InteriorGeneratedMap map, InteriorFurniturePlacementRequest request, System.Random rng)
        {
            var candidates = CollectCandidates(map, request.Preference);
            Shuffle(candidates, rng);
            for (int i = 0; i < candidates.Count; i++)
            {
                var directions = ResolveDirections(request.FacingDirection, rng);
                for (int directionIndex = 0; directionIndex < directions.Count; directionIndex++)
                {
                    if (TryPlaceDeskCluster(map, candidates[i], directions[directionIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryPlaceRequestedSofa(InteriorGeneratedMap map, InteriorFurniturePlacementRequest request, System.Random rng)
        {
            var candidates = CollectCandidates(map, request.Preference == InteriorPlacementPreference.Any ? InteriorPlacementPreference.NearWall : request.Preference);
            Shuffle(candidates, rng);
            for (int i = 0; i < candidates.Count; i++)
            {
                var directions = ResolveDirections(request.FacingDirection, rng);
                for (int directionIndex = 0; directionIndex < directions.Count; directionIndex++)
                {
                    if (TryPlaceSofa(map, candidates[i], directions[directionIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryPlaceRequestedSingleObject(InteriorGeneratedMap map, InteriorFurniturePlacementRequest request, System.Random rng)
        {
            var candidates = CollectCandidates(map, request.Preference);
            Shuffle(candidates, rng);
            for (int i = 0; i < candidates.Count; i++)
            {
                var direction = request.FacingDirection == InteriorFacingDirection.None ? InteriorFacingDirection.None : request.FacingDirection;
                if (map.TryPlaceObject(candidates[i], request.ObjectKind, BlocksMovement(request.ObjectKind), direction))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Vector2Int> CollectCandidates(InteriorGeneratedMap map, InteriorPlacementPreference preference)
        {
            var candidates = new List<Vector2Int>();
            for (int y = 2; y < map.Height - 2; y++)
            {
                for (int x = 2; x < map.Width - 2; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!map.IsWalkable(cell)) continue;
                    if (map.GetObject(cell) != InteriorObjectKind.None) continue;
                    if (!MatchesPreference(map, cell, preference)) continue;
                    candidates.Add(cell);
                }
            }

            return candidates;
        }

        private static bool MatchesPreference(InteriorGeneratedMap map, Vector2Int cell, InteriorPlacementPreference preference)
        {
            switch (preference)
            {
                case InteriorPlacementPreference.AvoidCorridor:
                    return map.GetKind(cell) != InteriorCellKind.Corridor;
                case InteriorPlacementPreference.NearWall:
                    return map.GetKind(cell) != InteriorCellKind.Corridor && IsAdjacentTo(map, cell, InteriorCellKind.Wall);
                case InteriorPlacementPreference.NearWindow:
                    return map.GetKind(cell) != InteriorCellKind.Corridor && IsAdjacentTo(map, cell, InteriorCellKind.Window);
                default:
                    return true;
            }
        }

        private static bool IsAdjacentTo(InteriorGeneratedMap map, Vector2Int cell, InteriorCellKind kind)
        {
            return map.GetKind(cell + Vector2Int.up) == kind ||
                   map.GetKind(cell + Vector2Int.down) == kind ||
                   map.GetKind(cell + Vector2Int.left) == kind ||
                   map.GetKind(cell + Vector2Int.right) == kind;
        }

        private static List<InteriorFacingDirection> ResolveDirections(InteriorFacingDirection requestedDirection, System.Random rng)
        {
            if (requestedDirection != InteriorFacingDirection.None)
            {
                return new List<InteriorFacingDirection> { requestedDirection };
            }

            var directions = new List<InteriorFacingDirection>(CardinalDirections);
            Shuffle(directions, rng);
            return directions;
        }

        private static bool BlocksMovement(InteriorObjectKind objectKind)
        {
            switch (objectKind)
            {
                case InteriorObjectKind.Chair:
                case InteriorObjectKind.Computer:
                case InteriorObjectKind.Light:
                case InteriorObjectKind.OfficeProp:
                    return false;
                default:
                    return true;
            }
        }

        private static void PlaceDesks(InteriorGeneratedMap map, InteriorGenerationProfile profile, System.Random rng)
        {
            var candidates = new List<Vector2Int>();
            for (int y = 3; y < map.Height - 2; y++)
            {
                for (int x = 2; x < map.Width - 2; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!map.IsWalkable(cell)) continue;
                    if (map.GetKind(cell) == InteriorCellKind.Corridor) continue;
                    candidates.Add(cell);
                }
            }

            Shuffle(candidates, rng);
            int placed = 0;
            for (int i = 0; i < candidates.Count && placed < profile.DeskClusterCount; i++)
            {
                var desk = candidates[i];
                var directions = new List<InteriorFacingDirection>(CardinalDirections);
                Shuffle(directions, rng);

                for (int directionIndex = 0; directionIndex < directions.Count; directionIndex++)
                {
                    if (TryPlaceDeskCluster(map, desk, directions[directionIndex]))
                    {
                        placed++;
                        break;
                    }
                }
            }
        }

        private static bool TryPlaceDeskCluster(InteriorGeneratedMap map, Vector2Int desk, InteriorFacingDirection facingDirection)
        {
            var front = InteriorDirectionUtility.ToVector(facingDirection);
            if (front == Vector2Int.zero)
            {
                return false;
            }

            var chair = desk + front;
            var computer = desk - front;
            if (!map.IsWalkable(desk) || !map.IsWalkable(chair) || !map.IsWalkableBase(computer)) return false;
            if (map.GetKind(chair) == InteriorCellKind.Corridor || map.GetKind(computer) == InteriorCellKind.Corridor) return false;
            if (map.GetObject(chair) != InteriorObjectKind.None || map.GetObject(computer) != InteriorObjectKind.None) return false;
            if (!map.TryPlaceObject(desk, InteriorObjectKind.Desk, true, facingDirection)) return false;
            if (!map.TryPlaceObject(chair, InteriorObjectKind.Chair, false, facingDirection)) return false;
            map.TryPlaceObject(computer, InteriorObjectKind.Computer, false, facingDirection);
            return true;
        }

        private static void PlaceSofa(InteriorGeneratedMap map, System.Random rng)
        {
            var candidates = new List<Vector2Int>();
            for (int y = 2; y < map.Height - 2; y++)
            {
                for (int x = 2; x < map.Width - 2; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!map.IsWalkable(cell) || map.GetKind(cell) == InteriorCellKind.Corridor) continue;
                    candidates.Add(cell);
                }
            }

            Shuffle(candidates, rng);
            for (int i = 0; i < candidates.Count; i++)
            {
                var directions = new List<InteriorFacingDirection>(CardinalDirections);
                Shuffle(directions, rng);
                for (int directionIndex = 0; directionIndex < directions.Count; directionIndex++)
                {
                    if (TryPlaceSofa(map, candidates[i], directions[directionIndex])) return;
                }
            }
        }

        private static bool TryPlaceSofa(InteriorGeneratedMap map, Vector2Int cell, InteriorFacingDirection facingDirection)
        {
            var front = InteriorDirectionUtility.ToVector(facingDirection);
            if (front == Vector2Int.zero)
            {
                return false;
            }

            var behind = cell - front;
            if (map.GetKind(behind) != InteriorCellKind.Wall) return false;
            if (!map.IsWalkable(cell + front)) return false;
            return map.TryPlaceObject(cell, InteriorObjectKind.Sofa, true, facingDirection);
        }

        private static void PlacePlants(InteriorGeneratedMap map, InteriorGenerationProfile profile, System.Random rng)
        {
            var candidates = new List<Vector2Int>();
            for (int y = 2; y < map.Height - 2; y++)
            {
                for (int x = 2; x < map.Width - 2; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!map.IsWalkable(cell)) continue;
                    if (map.GetKind(cell) == InteriorCellKind.Corridor) continue;
                    candidates.Add(cell);
                }
            }

            Shuffle(candidates, rng);
            int placed = 0;
            for (int i = 0; i < candidates.Count && placed < profile.PlantCount; i++)
            {
                if (map.TryPlaceObject(candidates[i], InteriorObjectKind.Plant, true)) placed++;
            }
        }

        private static void Shuffle<T>(List<T> values, System.Random rng)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }

    public static class InteriorPathValidator
    {
        public static bool CanReachAnyDoor(InteriorGeneratedMap map, Vector2Int start)
        {
            if (map == null || !map.IsWalkable(start)) return false;

            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (map.GetKind(current) == InteriorCellKind.Door) return true;

                Enqueue(map, current + Vector2Int.up, visited, queue);
                Enqueue(map, current + Vector2Int.down, visited, queue);
                Enqueue(map, current + Vector2Int.left, visited, queue);
                Enqueue(map, current + Vector2Int.right, visited, queue);
            }

            return false;
        }

        private static void Enqueue(InteriorGeneratedMap map, Vector2Int cell, HashSet<Vector2Int> visited, Queue<Vector2Int> queue)
        {
            if (visited.Contains(cell) || !map.IsWalkable(cell)) return;
            visited.Add(cell);
            queue.Enqueue(cell);
        }
    }

    public sealed class InteriorTileSet : ScriptableObject
    {
        [SerializeField] private TileBase _floor;
        [SerializeField] private TileBase _wall;
        [SerializeField] private TileBase _door;
        [SerializeField] private TileBase _window;
        [SerializeField] private TileBase _desk;
        [SerializeField] private TileBase _chair;
        [SerializeField] private TileBase _computer;
        [SerializeField] private TileBase _sofa;
        [SerializeField] private TileBase _plant;
        [SerializeField] private TileBase _collision;

        public TileBase Floor => _floor;
        public TileBase Wall => _wall != null ? _wall : _floor;
        public TileBase Door => _door != null ? _door : _floor;
        public TileBase Window => _window != null ? _window : Wall;
        public TileBase Desk => _desk;
        public TileBase Chair => _chair;
        public TileBase Computer => _computer;
        public TileBase Sofa => _sofa;
        public TileBase Plant => _plant;
        public TileBase Collision => _collision != null ? _collision : Wall;
    }
}
