using NUnit.Framework;
using Rootborn.Game.Placement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Placement
{
    public sealed class FurniturePlacementDomainTests
    {
        [Test]
        public void CreateForTests_RejectsMissingStableId()
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var part = new FurnitureTilePart(Vector2Int.zero, tile);

                Assert.Throws<System.ArgumentException>(() => FurnitureDefinition.CreateForTests(
                    string.Empty,
                    "Chair",
                    "chair",
                    new[] { "house" },
                    new[] { part },
                    null,
                    true,
                    FurniturePlacementPreference.Any));
            }
            finally
            {
                Object.DestroyImmediate(tile);
            }
        }

        [Test]
        public void CreateForTests_RejectsDefinitionWithoutTileParts()
        {
            Assert.Throws<System.ArgumentException>(() => FurnitureDefinition.CreateForTests(
                "chair.black",
                "Black Chair",
                "chair",
                new[] { "house" },
                new FurnitureTilePart[0],
                null,
                true,
                FurniturePlacementPreference.Any));
        }

        [Test]
        public void CreateForTests_UsesTilePartCellsAsDefaultFootprint()
        {
            var left = ScriptableObject.CreateInstance<Tile>();
            var right = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var definition = FurnitureDefinition.CreateForTests(
                    "desk.large",
                    "Large Desk",
                    "desk",
                    new[] { "house", "test-surface" },
                    new[]
                    {
                        new FurnitureTilePart(Vector2Int.zero, left),
                        new FurnitureTilePart(Vector2Int.right, right)
                    },
                    null,
                    true,
                    FurniturePlacementPreference.AvoidBlockedCells);

                Assert.AreEqual("desk.large", definition.StableId);
                Assert.AreEqual("Large Desk", definition.DisplayName);
                Assert.AreEqual("desk", definition.Category);
                CollectionAssert.AreEquivalent(new[] { "house", "test-surface" }, definition.AllowedSurfaceIds);
                Assert.AreEqual(2, definition.TileParts.Count);
                CollectionAssert.AreEquivalent(new[] { Vector2Int.zero, Vector2Int.right }, definition.FootprintCells);
                Assert.IsTrue(definition.BlocksMovement);
                Assert.AreEqual(FurniturePlacementPreference.AvoidBlockedCells, definition.PlacementPreference);
            }
            finally
            {
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
            }
        }

        [Test]
        public void TilePlacementSurface_ExposesRequiredSurfaceTilemapsAndBounds()
        {
            var root = new GameObject("SurfaceRoot");
            try
            {
                var ground = CreateTilemap(root.transform, "Ground");
                var objects = CreateTilemap(root.transform, "Objects");
                var occupancy = CreateTilemap(root.transform, "Occupancy");
                var preview = CreateTilemap(root.transform, "Preview");
                var bounds = new BoundsInt(-4, -3, 0, 8, 6, 1);

                var surface = root.AddComponent<TilePlacementSurface>();
                surface.ConfigureForTests("house", ground, objects, occupancy, preview, bounds, TilePlacementRuleFlags.RequireGround | TilePlacementRuleFlags.RejectOccupiedCells);

                Assert.AreEqual("house", surface.SurfaceId);
                Assert.AreSame(ground, surface.GroundTilemap);
                Assert.AreSame(objects, surface.ObjectTilemap);
                Assert.AreSame(occupancy, surface.OccupancyTilemap);
                Assert.AreSame(preview, surface.PreviewTilemap);
                Assert.AreEqual(bounds, surface.Bounds);
                Assert.IsTrue(surface.PlacementRules.HasFlag(TilePlacementRuleFlags.RequireGround));
                Assert.IsTrue(surface.PlacementRules.HasFlag(TilePlacementRuleFlags.RejectOccupiedCells));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Tilemap CreateTilemap(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(Grid), typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Tilemap>();
        }
    }
}
