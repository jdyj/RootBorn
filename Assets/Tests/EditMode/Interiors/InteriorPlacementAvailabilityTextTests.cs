using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.Placement;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorPlacementAvailabilityTextTests
    {
        [Test]
        public void RotateSelection_RefreshesLabelFromTheVisibleOverlayCount()
        {
            var root = new GameObject("AvailabilityTextFixture", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<Tile>();
            var valid = ScriptableObject.CreateInstance<Tile>();
            var invalid = ScriptableObject.CreateInstance<Tile>();
            var definition = FurnitureDefinition.CreateForTests("fixture.label", "Wide fixture", "Desk", new[] { "house" },
                new[] { new FurnitureTilePart(Vector2Int.zero, tile) },
                new[] { Vector2Int.zero, Vector2Int.right }, true, FurniturePlacementPreference.Any);
            try
            {
                var ground = MakeMap(root.transform, "Ground");
                var objects = MakeMap(root.transform, "Objects");
                var occupancy = MakeMap(root.transform, "Occupancy");
                var preview = MakeMap(root.transform, "Preview");
                var map = new InteriorGeneratedMap(8, 8);
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        map.SetKind(new Vector2Int(x, y), InteriorCellKind.Floor);
                        ground.SetTile(new Vector3Int(x - 4, y - 4, 0), tile);
                    }
                occupancy.SetTile(new Vector3Int(0, -1, 0), tile); // map (4,3)
                var surface = objects.gameObject.AddComponent<TilePlacementSurface>();
                surface.ConfigureForTests("house", ground, objects, occupancy, preview, new BoundsInt(-4, -4, 0, 8, 8, 1),
                    TilePlacementRuleFlags.RequireGround | TilePlacementRuleFlags.RejectOccupiedCells | TilePlacementRuleFlags.StayInsideBounds);
                var furniture = new InteriorFurnitureDefinition(definition.StableId, definition.DisplayName,
                    InteriorObjectKind.Desk, true, InteriorPlacementPreference.Any,
                    new[] { new InteriorFurnitureTile(Vector2Int.zero, tile) }, definition.FootprintCells, definition);
                var overlay = root.AddComponent<InteriorPlacementPreviewOverlay>();
                Set(overlay, "_surface", surface);
                Set(overlay, "_furnitureTilemap", objects);
                Set(overlay, "_occupancyTilemap", occupancy);
                Set(overlay, "_overlay", preview);
                Set(overlay, "_map", map);
                Set(overlay, "_activeFurniture", furniture);
                Set(overlay, "_activeDirection", FurniturePlacementDirection.North);
                Set(overlay, "_request", InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Desk, 1,
                    InteriorPlacementPreference.Any, InteriorFacingDirection.None, false));
                Set(overlay, "_validTile", valid);
                Set(overlay, "_invalidTile", invalid);
                Set(overlay, "_validFootprintTile", valid);
                Set(overlay, "_invalidFootprintTile", invalid);
                var panel = root.AddComponent<InteriorFurniturePlacementPanel>();
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(root.transform, false);
                var label = labelObject.GetComponent<Text>();
                label.text = "stale candidate count";
                Set(panel, "_overlay", overlay);
                Set(panel, "_activeFurniture", furniture);
                Set(panel, "_availabilityText", label);

                // Existing UI callback: no new method is required for the RED test to compile.
                typeof(InteriorFurniturePlacementPanel).GetMethod("RotateSelectedFurniture", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, null);

                Assert.AreEqual(FurniturePlacementDirection.East, overlay.ActiveDirection);
                Assert.AreEqual(14, overlay.ValidCellCount, "Two anchors overlap the occupied cell in the rotated footprint.");
                Assert.AreEqual("Wide fixture: 14 available / requested 1", label.text);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(valid);
                Object.DestroyImmediate(invalid);
            }
        }

        private static Tilemap MakeMap(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Tilemap>();
        }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }
    }
}
