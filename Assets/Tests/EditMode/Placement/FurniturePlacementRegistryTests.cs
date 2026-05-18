using NUnit.Framework;
using Rootborn.Game.Placement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Placement
{
    public sealed class FurniturePlacementRegistryTests
    {
        [Test]
        public void TryPlace_StoresInstanceAndFindsOwnerByAnyOccupiedCell()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var leftTile = ScriptableObject.CreateInstance<Tile>();
            var rightTile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var furniture = CreateTwoTileDesk(leftTile, rightTile);
                var registry = new FurniturePlacementRegistry();

                Assert.IsTrue(registry.TryPlace(fixture.Surface, furniture, Vector3Int.zero, FurniturePlacementDirection.East, out var instance, out var placement));

                Assert.IsTrue(placement.Success);
                Assert.AreEqual("desk.large", instance.FurnitureId);
                Assert.AreEqual("test-surface", instance.SurfaceId);
                Assert.IsFalse(string.IsNullOrEmpty(instance.InstanceId));
                Assert.IsTrue(registry.TryFindAt("test-surface", Vector3Int.zero, out var first));
                Assert.IsTrue(registry.TryFindAt("test-surface", Vector3Int.down, out var second));
                Assert.AreEqual(instance.InstanceId, first.InstanceId);
                Assert.AreEqual(instance.InstanceId, second.InstanceId);
            }
            finally
            {
                Object.DestroyImmediate(leftTile);
                Object.DestroyImmediate(rightTile);
                fixture.Destroy();
            }
        }

        [Test]
        public void TryDelete_ClearsObjectAndOccupancyCells()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var leftTile = ScriptableObject.CreateInstance<Tile>();
            var rightTile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var registry = new FurniturePlacementRegistry();
                Assert.IsTrue(registry.TryPlace(fixture.Surface, CreateTwoTileDesk(leftTile, rightTile), Vector3Int.zero, FurniturePlacementDirection.North, out var instance, out _));

                Assert.IsTrue(registry.TryDelete(fixture.Surface, instance.InstanceId));

                Assert.IsNull(fixture.Objects.GetTile(Vector3Int.zero));
                Assert.IsNull(fixture.Objects.GetTile(Vector3Int.right));
                Assert.IsNull(fixture.Occupancy.GetTile(Vector3Int.zero));
                Assert.IsNull(fixture.Occupancy.GetTile(Vector3Int.right));
                Assert.IsFalse(registry.TryFindAt("test-surface", Vector3Int.zero, out _));
            }
            finally
            {
                Object.DestroyImmediate(leftTile);
                Object.DestroyImmediate(rightTile);
                fixture.Destroy();
            }
        }

        [Test]
        public void TryMove_WhenDestinationValidMovesTilesAndEmptiesOriginalCells()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var leftTile = ScriptableObject.CreateInstance<Tile>();
            var rightTile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var registry = new FurniturePlacementRegistry();
                Assert.IsTrue(registry.TryPlace(fixture.Surface, CreateTwoTileDesk(leftTile, rightTile), Vector3Int.zero, FurniturePlacementDirection.North, out var instance, out _));

                Assert.IsTrue(registry.TryMove(fixture.Surface, instance.InstanceId, new Vector3Int(2, 0, 0), FurniturePlacementDirection.West, out var moved, out var placement));

                Assert.IsTrue(placement.Success);
                Assert.AreEqual(new Vector3Int(2, 0, 0), moved.AnchorCell);
                Assert.AreEqual(FurniturePlacementDirection.West, moved.Direction);
                Assert.IsNull(fixture.Objects.GetTile(Vector3Int.zero));
                Assert.IsNull(fixture.Occupancy.GetTile(Vector3Int.zero));
                Assert.AreSame(leftTile, fixture.Objects.GetTile(new Vector3Int(2, 0, 0)));
                Assert.AreSame(rightTile, fixture.Objects.GetTile(new Vector3Int(2, 1, 0)));
            }
            finally
            {
                Object.DestroyImmediate(leftTile);
                Object.DestroyImmediate(rightTile);
                fixture.Destroy();
            }
        }

        [Test]
        public void TryMove_WhenDestinationBlockedPreservesOriginalTilesAndRegistry()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var leftTile = ScriptableObject.CreateInstance<Tile>();
            var rightTile = ScriptableObject.CreateInstance<Tile>();
            var blocker = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var registry = new FurniturePlacementRegistry();
                Assert.IsTrue(registry.TryPlace(fixture.Surface, CreateTwoTileDesk(leftTile, rightTile), Vector3Int.zero, FurniturePlacementDirection.North, out var instance, out _));
                fixture.Occupancy.SetTile(new Vector3Int(3, 0, 0), blocker);

                Assert.IsFalse(registry.TryMove(fixture.Surface, instance.InstanceId, new Vector3Int(2, 0, 0), FurniturePlacementDirection.North, out var moved, out var placement));

                Assert.AreEqual(FurniturePlacementFailureReason.Occupied, placement.FailureReason);
                Assert.AreEqual(instance.AnchorCell, moved.AnchorCell);
                Assert.AreSame(leftTile, fixture.Objects.GetTile(Vector3Int.zero));
                Assert.AreSame(rightTile, fixture.Objects.GetTile(Vector3Int.right));
                Assert.IsNotNull(fixture.Occupancy.GetTile(Vector3Int.zero));
                Assert.IsNotNull(fixture.Occupancy.GetTile(Vector3Int.right));
                Assert.IsTrue(registry.TryFindAt("test-surface", Vector3Int.right, out var found));
                Assert.AreEqual(instance.InstanceId, found.InstanceId);
            }
            finally
            {
                Object.DestroyImmediate(leftTile);
                Object.DestroyImmediate(rightTile);
                Object.DestroyImmediate(blocker);
                fixture.Destroy();
            }
        }

        private static FurnitureDefinition CreateTwoTileDesk(TileBase leftTile, TileBase rightTile)
        {
            return FurnitureDefinition.CreateForTests(
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
                var root = new GameObject("PlacementRegistryFixture");
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
