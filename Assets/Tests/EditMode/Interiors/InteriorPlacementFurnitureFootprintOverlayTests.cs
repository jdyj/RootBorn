using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.Placement;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorPlacementFurnitureFootprintOverlayTests
    {
        private GameObject _root;
        private InteriorGeneratedMap _map;
        private InteriorPlacementPreviewOverlay _overlay;
        private TilePlacementSurface _surface;
        private Tilemap _ground, _objects, _occupancy, _preview;
        private Tile _art, _valid, _invalid;
        private FurnitureDefinition _definition;
        private static readonly Vector3Int Offset = new Vector3Int(-4, -4, 0);

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("FootprintOverlayFixture", typeof(Grid));
            _ground = Map("FixtureGround");
            _objects = Map("FixtureObjects");
            _occupancy = Map("FixtureOccupancy");
            _preview = Map("FixturePreview");
            _art = ScriptableObject.CreateInstance<Tile>();
            _valid = ScriptableObject.CreateInstance<Tile>();
            _valid.color = new Color(.15f, .95f, .35f, .38f);
            _invalid = ScriptableObject.CreateInstance<Tile>();
            _invalid.color = new Color(.95f, .18f, .12f, .24f);
            _definition = FurnitureDefinition.CreateForTests("fixture.wide", "Wide fixture", "Desk", new[] { "house" },
                new[] { new FurnitureTilePart(Vector2Int.zero, _art) },
                new[] { Vector2Int.zero, Vector2Int.right }, true, FurniturePlacementPreference.Any);
            var furniture = new InteriorFurnitureDefinition(_definition.StableId, _definition.DisplayName,
                InteriorObjectKind.Desk, true, InteriorPlacementPreference.Any,
                new[] { new InteriorFurnitureTile(Vector2Int.zero, _art) }, _definition.FootprintCells, _definition);
            _map = new InteriorGeneratedMap(8, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    _map.SetKind(new Vector2Int(x, y), InteriorCellKind.Floor);
                    _ground.SetTile(new Vector3Int(x, y, 0) + Offset, _art);
                }
            _surface = _objects.gameObject.AddComponent<TilePlacementSurface>();
            ConfigureBounds(new BoundsInt(-4, -4, 0, 8, 8, 1));
            _overlay = _root.AddComponent<InteriorPlacementPreviewOverlay>();
            Set("_surface", _surface);
            Set("_furnitureTilemap", _objects);
            Set("_occupancyTilemap", _occupancy);
            Set("_overlay", _preview);
            Set("_map", _map);
            Set("_activeFurniture", furniture);
            Set("_activeDirection", FurniturePlacementDirection.North);
            Set("_request", InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Desk, 1,
                InteriorPlacementPreference.Any, InteriorFacingDirection.None, false));
            Set("_validTile", _valid);
            Set("_invalidTile", _invalid);
            Set("_validFootprintTile", _valid);
            Set("_invalidFootprintTile", _invalid);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_art);
            Object.DestroyImmediate(_valid);
            Object.DestroyImmediate(_invalid);
        }

        [Test]
        public void WideFurniture_SecondFootprintCellAtWall_IsRedDespiteFreeLegacyVerticalCluster()
        {
            _map.SetKind(new Vector2Int(4, 3), InteriorCellKind.Wall);
            Draw();
            AssertColor(new Vector2Int(3, 3), false);
            AssertColor(new Vector2Int(2, 4), true);
            AssertCounts(14, 2); // The wall blocks anchors (3,3) and (4,3) in the existing 4x4 preview domain.
        }

        [Test]
        public void WideFurniture_SecondFootprintCellOutsideSurfaceBounds_IsRed()
        {
            ConfigureBounds(new BoundsInt(-2, -2, 0, 4, 4, 1));
            Draw();
            AssertColor(new Vector2Int(5, 3), false);
            AssertColor(new Vector2Int(4, 3), true);
            AssertCounts(12, 4); // Rightmost four anchors overflow the surface by one cell.
        }

        [Test]
        public void WideFurniture_FreeFootprint_IsGreenWithoutRequiringExtraChairAndComputerCells()
        {
            _map.SetKind(new Vector2Int(3, 2), InteriorCellKind.Wall);
            _map.SetKind(new Vector2Int(2, 3), InteriorCellKind.Wall);
            Draw();
            AssertColor(new Vector2Int(3, 3), true);
            AssertColor(new Vector2Int(3, 2), false);
        }

        [Test]
        public void WideFurniture_RotationReevaluatesActualFootprintAgainstOccupancy()
        {
            _occupancy.SetTile(new Vector3Int(4, 3, 0) + Offset, _art);
            Draw();
            AssertColor(new Vector2Int(3, 3), false);
            AssertCounts(14, 2);
            Assert.AreEqual(FurniturePlacementDirection.East, _overlay.RotateActiveFurniture());
            AssertColor(new Vector2Int(3, 3), true); // East footprint is anchor plus down, not right.
            _occupancy.SetTile(new Vector3Int(3, 2, 0) + Offset, _art);
            Draw();
            AssertColor(new Vector2Int(3, 3), false);
        }

        [Test]
        public void NoSelectedFurniture_PreservesLegacyRequestCandidatePath()
        {
            Set("_activeFurniture", null);
            Draw();
            AssertCounts(16, 0);
            _map.SetKind(new Vector2Int(3, 2), InteriorCellKind.Wall);
            _map.SetKind(new Vector2Int(2, 3), InteriorCellKind.Wall);
            Draw();
            AssertColor(new Vector2Int(3, 3), false); // Legacy desk clusters still require both sides in some direction.
        }

        private void ConfigureBounds(BoundsInt bounds)
        {
            _surface.ConfigureForTests("house", _ground, _objects, _occupancy, _preview, bounds,
                TilePlacementRuleFlags.RequireGround | TilePlacementRuleFlags.RejectOccupiedCells | TilePlacementRuleFlags.StayInsideBounds);
        }
        private Tilemap Map(string name)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(_root.transform, false);
            return go.GetComponent<Tilemap>();
        }
        private void Set(string name, object value)
        {
            var field = typeof(InteriorPlacementPreviewOverlay).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(_overlay, value);
        }
        private void Draw()
        {
            // Unit test of visible candidate semantics; actual input flow remains covered by the existing PlayMode fixture.
            typeof(InteriorPlacementPreviewOverlay).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_overlay, null);
        }
        private void AssertColor(Vector2Int mapCell, bool valid)
        {
            var tile = _preview.GetTile<Tile>(new Vector3Int(mapCell.x, mapCell.y, 0) + Offset);
            Assert.IsNotNull(tile, "Expected a visible preview at " + mapCell);
            Assert.AreEqual(valid ? _valid : _invalid, tile, "Wrong anchor availability at " + mapCell);
        }
        private void AssertCounts(int valid, int invalid)
        {
            Assert.AreEqual(valid, _overlay.ValidCellCount);
            Assert.AreEqual(invalid, _overlay.InvalidCellCount);
            int green = 0, red = 0;
            foreach (var position in _preview.cellBounds.allPositionsWithin)
            {
                var tile = _preview.GetTile(position);
                if (tile == _valid) green++;
                if (tile == _invalid) red++;
            }
            Assert.AreEqual(valid, green);
            Assert.AreEqual(invalid, red);
        }
    }
}
