using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Rootborn.Game.Interiors
{
    public readonly struct InteriorPlacementCandidateSet
    {
        public InteriorPlacementCandidateSet(IReadOnlyList<Vector2Int> valid, IReadOnlyList<Vector2Int> invalid)
        {
            Valid = valid ?? new List<Vector2Int>();
            Invalid = invalid ?? new List<Vector2Int>();
        }

        public IReadOnlyList<Vector2Int> Valid { get; }
        public IReadOnlyList<Vector2Int> Invalid { get; }
    }

    public readonly struct InteriorPlacementAvailabilitySummary
    {
        public InteriorPlacementAvailabilitySummary(int validCount, int invalidCount, int requestedCount, string message)
        {
            ValidCount = validCount;
            InvalidCount = invalidCount;
            RequestedCount = requestedCount;
            Message = message ?? string.Empty;
        }

        public int ValidCount { get; }
        public int InvalidCount { get; }
        public int RequestedCount { get; }
        public bool HasEnoughCandidates => ValidCount >= RequestedCount;
        public string Message { get; }
    }

    public static class InteriorPlacementAvailability
    {
        private static readonly InteriorFacingDirection[] CardinalDirections =
        {
            InteriorFacingDirection.North,
            InteriorFacingDirection.East,
            InteriorFacingDirection.South,
            InteriorFacingDirection.West
        };

        public static InteriorPlacementCandidateSet CollectCandidates(InteriorGeneratedMap map, InteriorFurniturePlacementRequest request)
        {
            var valid = new List<Vector2Int>();
            var invalid = new List<Vector2Int>();
            if (map == null || request == null)
            {
                return new InteriorPlacementCandidateSet(valid, invalid);
            }

            for (int y = 2; y < map.Height - 2; y++)
            {
                for (int x = 2; x < map.Width - 2; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (CanPlaceAt(map, cell, request))
                    {
                        valid.Add(cell);
                    }
                    else
                    {
                        invalid.Add(cell);
                    }
                }
            }

            return new InteriorPlacementCandidateSet(valid, invalid);
        }

        public static InteriorPlacementAvailabilitySummary Summarize(InteriorGeneratedMap map, InteriorFurniturePlacementRequest request)
        {
            var candidates = CollectCandidates(map, request);
            int requested = request != null ? request.Count : 0;
            string message = string.Format(CultureInfo.InvariantCulture,
                "{0} available / requested {1}", candidates.Valid.Count, requested);
            return new InteriorPlacementAvailabilitySummary(candidates.Valid.Count, candidates.Invalid.Count, requested, message);
        }

        public static bool CanPlaceAt(InteriorGeneratedMap map, Vector2Int cell, InteriorFurniturePlacementRequest request)
        {
            if (map == null || request == null || !map.InBounds(cell))
            {
                return false;
            }

            if (request.ObjectKind == InteriorObjectKind.Desk || request.ObjectKind == InteriorObjectKind.Computer)
            {
                return CanPlaceDeskCluster(map, cell, request);
            }

            if (request.ObjectKind == InteriorObjectKind.Sofa)
            {
                return CanPlaceSofa(map, cell, request);
            }

            return CanPlaceSingleObject(map, cell, request);
        }

        public static bool TryPlaceManual(InteriorGeneratedMap map, Vector2Int cell, InteriorFurniturePlacementRequest request, out string message)
        {
            if (!CanPlaceAt(map, cell, request))
            {
                message = "Cell is not valid for " + (request != null ? request.ObjectKind.ToString() : "selection");
                return false;
            }

            if (request.ObjectKind == InteriorObjectKind.Desk || request.ObjectKind == InteriorObjectKind.Computer)
            {
                var direction = ResolveManualDirection(map, cell, request);
                var front = InteriorDirectionUtility.ToVector(direction);
                map.TryPlaceObject(cell, InteriorObjectKind.Desk, true, direction);
                map.TryPlaceObject(cell + front, InteriorObjectKind.Chair, false, direction);
                map.TryPlaceObject(cell - front, InteriorObjectKind.Computer, false, direction);
                message = "Placed Desk";
                return true;
            }

            if (request.ObjectKind == InteriorObjectKind.Sofa)
            {
                var direction = ResolveManualDirection(map, cell, request);
                map.TryPlaceObject(cell, InteriorObjectKind.Sofa, true, direction);
                message = "Placed Sofa";
                return true;
            }

            map.TryPlaceObject(cell, request.ObjectKind, BlocksMovement(request.ObjectKind), request.FacingDirection);
            message = "Placed " + request.ObjectKind;
            return true;
        }

        private static bool CanPlaceSingleObject(InteriorGeneratedMap map, Vector2Int cell, InteriorFurniturePlacementRequest request)
        {
            return map.IsWalkable(cell) &&
                   map.GetObject(cell) == InteriorObjectKind.None &&
                   MatchesPreference(map, cell, request.Preference);
        }

        private static bool CanPlaceDeskCluster(InteriorGeneratedMap map, Vector2Int cell, InteriorFurniturePlacementRequest request)
        {
            var directions = ResolveDirections(request.FacingDirection);
            for (int i = 0; i < directions.Count; i++)
            {
                if (CanPlaceDeskCluster(map, cell, directions[i], request.Preference))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanPlaceDeskCluster(InteriorGeneratedMap map, Vector2Int desk, InteriorFacingDirection direction, InteriorPlacementPreference preference)
        {
            var front = InteriorDirectionUtility.ToVector(direction);
            if (front == Vector2Int.zero || !MatchesPreference(map, desk, preference))
            {
                return false;
            }

            var chair = desk + front;
            var computer = desk - front;
            return map.IsWalkable(desk) &&
                   map.IsWalkable(chair) &&
                   map.IsWalkableBase(computer) &&
                   map.GetKind(chair) != InteriorCellKind.Corridor &&
                   map.GetKind(computer) != InteriorCellKind.Corridor &&
                   map.GetObject(desk) == InteriorObjectKind.None &&
                   map.GetObject(chair) == InteriorObjectKind.None &&
                   map.GetObject(computer) == InteriorObjectKind.None;
        }

        private static bool CanPlaceSofa(InteriorGeneratedMap map, Vector2Int cell, InteriorFurniturePlacementRequest request)
        {
            var preference = request.Preference == InteriorPlacementPreference.Any ? InteriorPlacementPreference.NearWall : request.Preference;
            var directions = ResolveDirections(request.FacingDirection);
            for (int i = 0; i < directions.Count; i++)
            {
                if (CanPlaceSofa(map, cell, directions[i], preference))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanPlaceSofa(InteriorGeneratedMap map, Vector2Int cell, InteriorFacingDirection direction, InteriorPlacementPreference preference)
        {
            var front = InteriorDirectionUtility.ToVector(direction);
            if (front == Vector2Int.zero || !MatchesPreference(map, cell, preference))
            {
                return false;
            }

            return map.IsWalkable(cell) &&
                   map.GetObject(cell) == InteriorObjectKind.None &&
                   map.GetKind(cell - front) == InteriorCellKind.Wall &&
                   map.IsWalkable(cell + front);
        }

        private static InteriorFacingDirection ResolveManualDirection(InteriorGeneratedMap map, Vector2Int cell, InteriorFurniturePlacementRequest request)
        {
            var directions = ResolveDirections(request.FacingDirection);
            for (int i = 0; i < directions.Count; i++)
            {
                var test = InteriorFurniturePlacementRequest.CreateForTests(request.ObjectKind, 1, request.Preference, directions[i], request.Required);
                if (CanPlaceAt(map, cell, test))
                {
                    return directions[i];
                }
            }

            return request.FacingDirection;
        }

        private static IReadOnlyList<InteriorFacingDirection> ResolveDirections(InteriorFacingDirection requestedDirection)
        {
            if (requestedDirection != InteriorFacingDirection.None)
            {
                return new[] { requestedDirection };
            }

            return CardinalDirections;
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
    }
}
