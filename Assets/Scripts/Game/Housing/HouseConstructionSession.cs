using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    public sealed class HouseConstructionSession
    {
        private readonly HouseConstructionBlueprintDefinition _blueprint;
        private readonly Dictionary<Vector2Int, HouseConstructionCellKind> _placed = new Dictionary<Vector2Int, HouseConstructionCellKind>();

        public HouseConstructionSession(HouseConstructionBlueprintDefinition blueprint)
        {
            _blueprint = blueprint;
        }

        public bool IsComplete => _blueprint != null && HasAllRequiredCells();

        public bool TryPlace(Vector2Int cell, HouseConstructionCellKind kind)
        {
            if (_blueprint == null || !_blueprint.IsCellAllowed(cell, kind)) return false;
            _placed[cell] = kind;
            return true;
        }

        public HouseConstructionCellSaveData[] ToSaveData()
        {
            var result = new HouseConstructionCellSaveData[_placed.Count];
            int index = 0;
            foreach (var pair in _placed)
            {
                result[index++] = new HouseConstructionCellSaveData { X = pair.Key.x, Y = pair.Key.y, Kind = pair.Value };
            }
            return result;
        }

        private bool HasAllRequiredCells()
        {
            var required = _blueprint.RequiredCells;
            for (int i = 0; i < required.Count; i++)
            {
                var cell = required[i];
                if (!_placed.TryGetValue(cell.Cell, out var kind) || kind != cell.Kind) return false;
            }
            return required.Count > 0;
        }
    }
}
