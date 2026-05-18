using NUnit.Framework;
using Rootborn.Game.Placement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Placement
{
    public sealed class FurniturePlacementServiceTests
    {
        [Test]
        public void TryPlace_SingleTileFurnitureWritesObjectTileAndOccupancy()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var chairTile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var furniture = FurnitureDefinition.CreateForTests(
                    "chair.black",
                    "Black Chair",
                    "chair",
                    new[] { "test-surface" },
                    new[] { new FurnitureTilePart(Vector2Int.zero, chairTile) },
                    null,
                    true,
                    FurniturePlacementPreference.Any);

                Assert.IsTrue(FurniturePlacementService.TryPlace(fixture.Surface, furniture, Vector3Int.zero, FurniturePlacementDirection.North, out var result));

                Assert.AreEqual("chair.black", result.FurnitureId);
                Assert.AreEqual("test-surface", result.SurfaceId);
                Assert.AreEqual(Vector3Int.zero, result.AnchorCell);
                Assert.AreEqual(FurniturePlacementDirection.North, result.Direction);
                Assert.AreSame(chairTile, fixture.Objects.GetTile(Vector3Int.zero));
                Assert.IsNotNull(fixture.Occupancy.GetTile(Vector3Int.zero));
            }
            finally
            {
                Object.DestroyImmediate(chairTile);
                fixture.Destroy();
            }
        }

        [Test]
        public void TryPlace_MultiTileFurnitureWritesEveryTilePartAtomically()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var leftTile = ScriptableObject.CreateInstance<Tile>();
            var rightTile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var furniture = FurnitureDefinition.CreateForTests(
                    "desk.large",
                    "Large Desk",
                    "desk",
                    new[] { "test-surface" },
                    new[]
                    {
                        new FurnitureTilePart(Vector2Int.zero, leftTile),
                        new FurnitureTilePart(Vector2Int.right, rightTile)
                    },
                    null,
                    true,
                    FurniturePlacementPreference.Any);

                Assert.IsTrue(FurniturePlacementService.TryPlace(fixture.Surface, furniture, new Vector3Int(1, 1, 0), FurniturePlacementDirection.East, out var result));

                CollectionAssert.AreEquivalent(new[] { new Vector3Int(1, 1, 0), new Vector3Int(1, 0, 0) }, result.OccupiedCells);
                Assert.AreSame(leftTile, fixture.Objects.GetTile(new Vector3Int(1, 1, 0)));
                Assert.AreSame(rightTile, fixture.Objects.GetTile(new Vector3Int(1, 0, 0)));
                Assert.IsNotNull(fixture.Occupancy.GetTile(new Vector3Int(1, 1, 0)));
                Assert.IsNotNull(fixture.Occupancy.GetTile(new Vector3Int(1, 0, 0)));
            }
            finally
            {
                Object.DestroyImmediate(leftTile);
                Object.DestroyImmediate(rightTile);
                fixture.Destroy();
            }
        }

        [Test]
        public void TryPlace_EastDirectionRotatesFootprintAndTilePartsConsistently()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var anchorTile = ScriptableObject.CreateInstance<Tile>();
            var sideTile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var furniture = FurnitureDefinition.CreateForTests(
                    "desk.large",
                    "Large Desk",
                    "desk",
                    new[] { "test-surface" },
                    new[]
                    {
                        new FurnitureTilePart(Vector2Int.zero, anchorTile),
                        new FurnitureTilePart(Vector2Int.right, sideTile)
                    },
                    null,
                    true,
                    FurniturePlacementPreference.Any);

                Assert.IsTrue(FurniturePlacementService.TryPlace(fixture.Surface, furniture, new Vector3Int(2, 2, 0), FurniturePlacementDirection.East, out var result));

                Assert.AreEqual(FurniturePlacementDirection.East, result.Direction);
                CollectionAssert.AreEquivalent(new[] { new Vector3Int(2, 2, 0), new Vector3Int(2, 1, 0) }, result.OccupiedCells);
                Assert.AreSame(anchorTile, fixture.Objects.GetTile(new Vector3Int(2, 2, 0)));
                Assert.AreSame(sideTile, fixture.Objects.GetTile(new Vector3Int(2, 1, 0)), "Tile parts and occupancy must use the same rotated direction data.");
                Assert.IsNull(fixture.Objects.GetTile(new Vector3Int(3, 2, 0)), "East placement must not keep the unrotated +X tile part.");
            }
            finally
            {
                Object.DestroyImmediate(anchorTile);
                Object.DestroyImmediate(sideTile);
                fixture.Destroy();
            }
        }

        [Test]
        public void TryPlace_WhenPartOfFootprintIsOccupiedDoesNotWritePartialTiles()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var leftTile = ScriptableObject.CreateInstance<Tile>();
            var rightTile = ScriptableObject.CreateInstance<Tile>();
            var blocker = ScriptableObject.CreateInstance<Tile>();
            try
            {
                fixture.Occupancy.SetTile(new Vector3Int(1, 0, 0), blocker);
                var furniture = FurnitureDefinition.CreateForTests(
                    "desk.large",
                    "Large Desk",
                    "desk",
                    new[] { "test-surface" },
                    new[]
                    {
                        new FurnitureTilePart(Vector2Int.zero, leftTile),
                        new FurnitureTilePart(Vector2Int.right, rightTile)
                    },
                    null,
                    true,
                    FurniturePlacementPreference.Any);

                Assert.IsFalse(FurniturePlacementService.TryPlace(fixture.Surface, furniture, new Vector3Int(1, 1, 0), FurniturePlacementDirection.East, out var result));

                Assert.AreEqual(FurniturePlacementFailureReason.Occupied, result.FailureReason);
                Assert.IsNull(fixture.Objects.GetTile(new Vector3Int(1, 1, 0)));
                Assert.IsNull(fixture.Objects.GetTile(new Vector3Int(1, 0, 0)));
                Assert.IsNull(fixture.Occupancy.GetTile(new Vector3Int(1, 1, 0)));
                Assert.AreSame(blocker, fixture.Occupancy.GetTile(new Vector3Int(1, 0, 0)));
            }
            finally
            {
                Object.DestroyImmediate(leftTile);
                Object.DestroyImmediate(rightTile);
                Object.DestroyImmediate(blocker);
                fixture.Destroy();
            }
        }

        [Test]
        public void TryPlace_WhenAnchorIsOutsideBoundsRejectsWithoutWrites()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var furniture = FurnitureDefinition.CreateForTests(
                    "chair.black",
                    "Black Chair",
                    "chair",
                    new[] { "test-surface" },
                    new[] { new FurnitureTilePart(Vector2Int.zero, tile) },
                    null,
                    true,
                    FurniturePlacementPreference.Any);

                Assert.IsFalse(FurniturePlacementService.TryPlace(fixture.Surface, furniture, new Vector3Int(99, 99, 0), FurniturePlacementDirection.South, out var result));

                Assert.AreEqual(FurniturePlacementFailureReason.OutOfBounds, result.FailureReason);
                Assert.IsNull(fixture.Objects.GetTile(new Vector3Int(99, 99, 0)));
                Assert.IsNull(fixture.Occupancy.GetTile(new Vector3Int(99, 99, 0)));
            }
            finally
            {
                Object.DestroyImmediate(tile);
                fixture.Destroy();
            }
        }

        private sealed class PlacementSurfaceFixture
        {
            private readonly GameObject _root;

            private PlacementSurfaceFixture(GameObject root, TilePlacementSurface surface, Tilemap objects, Tilemap occupancy)
            {
                _root = root;
                Surface = surface;
                Objects = objects;
                Occupancy = occupancy;
            }

            public TilePlacementSurface Surface { get; }
            public Tilemap Objects { get; }
            public Tilemap Occupancy { get; }

            public static PlacementSurfaceFixture Create()
            {
                var root = new GameObject("PlacementSurfaceFixture");
                var ground = CreateTilemap(root.transform, "Ground");
                var objects = CreateTilemap(root.transform, "Objects");
                var occupancy = CreateTilemap(root.transform, "Occupancy");
                var preview = CreateTilemap(root.transform, "Preview");
                var groundTile = ScriptableObject.CreateInstance<Tile>();
                for (int x = -4; x <= 4; x++)
                {
                    for (int y = -4; y <= 4; y++)
                    {
                        ground.SetTile(new Vector3Int(x, y, 0), groundTile);
                    }
                }

                var surface = root.AddComponent<TilePlacementSurface>();
                surface.ConfigureForTests(
                    "test-surface",
                    ground,
                    objects,
                    occupancy,
                    preview,
                    new BoundsInt(-4, -4, 0, 9, 9, 1),
                    TilePlacementRuleFlags.RequireGround | TilePlacementRuleFlags.RejectOccupiedCells | TilePlacementRuleFlags.StayInsideBounds);
                return new PlacementSurfaceFixture(root, surface, objects, occupancy);
            }

            public void Destroy()
            {
                Object.DestroyImmediate(_root);
            }

            private static Tilemap CreateTilemap(Transform parent, string name)
            {
                var go = new GameObject(name, typeof(Grid), typeof(Tilemap), typeof(TilemapRenderer));
                go.transform.SetParent(parent, false);
                return go.GetComponent<Tilemap>();
            }
        }
    }
}
