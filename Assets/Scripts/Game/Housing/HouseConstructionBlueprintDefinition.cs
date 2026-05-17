using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    [CreateAssetMenu(fileName = "HouseBlueprint_New", menuName = "Rootborn/Housing/Construction Blueprint")]
    public sealed class HouseConstructionBlueprintDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private RectInt _bounds;
        [SerializeField] private HouseConstructionCellRequirement[] _requiredCells = Array.Empty<HouseConstructionCellRequirement>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public RectInt Bounds => _bounds;
        public IReadOnlyList<HouseConstructionCellRequirement> RequiredCells => _requiredCells ?? Array.Empty<HouseConstructionCellRequirement>();

        public bool IsCellAllowed(Vector2Int cell, HouseConstructionCellKind kind)
        {
            if (!_bounds.Contains(cell)) return false;
            for (int i = 0; i < RequiredCells.Count; i++)
            {
                var required = RequiredCells[i];
                if (required.Cell == cell && required.Kind == kind) return true;
            }
            return false;
        }

        public void ConfigureForTests(string id, RectInt bounds, HouseConstructionCellRequirement[] requiredCells)
        {
            _id = id;
            _bounds = bounds;
            _requiredCells = requiredCells ?? Array.Empty<HouseConstructionCellRequirement>();
        }

        public static HouseConstructionBlueprintDefinition CreateForTests(string id, RectInt bounds, HouseConstructionCellRequirement[] requiredCells)
        {
            var definition = CreateInstance<HouseConstructionBlueprintDefinition>();
            definition._id = id;
            definition._bounds = bounds;
            definition._requiredCells = requiredCells ?? Array.Empty<HouseConstructionCellRequirement>();
            return definition;
        }
    }
}