using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Placement;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Placement
{
    public sealed class FurniturePlacementSaveTests
    {
        [Test]
        public void ToSaveData_StoresStableFurnitureSurfaceCellAndDirection()
        {
            var instance = new FurniturePlacementInstance(
                "desk.large#1",
                "desk.large",
                "house",
                new Vector3Int(2, 3, 0),
                FurniturePlacementDirection.West,
                new[] { new Vector3Int(2, 3, 0), new Vector3Int(2, 4, 0) },
                new[] { new Vector3Int(2, 3, 0), new Vector3Int(2, 4, 0) });

            var saveData = FurniturePlacementSaveData.FromInstance(instance, "open");

            Assert.AreEqual("desk.large", saveData.FurnitureId);
            Assert.AreEqual("house", saveData.SurfaceId);
            Assert.AreEqual(2, saveData.AnchorX);
            Assert.AreEqual(3, saveData.AnchorY);
            Assert.AreEqual(FurniturePlacementDirection.West, saveData.Direction);
            Assert.AreEqual("open", saveData.StateJson);
        }

        [Test]
        public void Restore_RebuildsTilemapsFromCatalogLookup()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var leftTile = ScriptableObject.CreateInstance<Tile>();
            var rightTile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var definition = CreateTwoTileDesk(leftTile, rightTile);
                var registry = new FurniturePlacementRegistry();
                var saveData = new[]
                {
                    new FurniturePlacementSaveData
                    {
                        FurnitureId = "desk.large",
                        SurfaceId = "test-surface",
                        AnchorX = 1,
                        AnchorY = 1,
                        Direction = FurniturePlacementDirection.East
                    }
                };

                var restored = FurniturePlacementSaveUtility.Restore(fixture.Surface, registry, saveData, new[] { definition });

                Assert.AreEqual(1, restored.RestoredCount);
                Assert.AreEqual(0, restored.MissingDefinitionCount);
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
        public void Restore_SkipsMissingDefinitionsWithoutCrashing()
        {
            var fixture = PlacementSurfaceFixture.Create();
            try
            {
                var registry = new FurniturePlacementRegistry();
                var saveData = new[]
                {
                    new FurniturePlacementSaveData
                    {
                        FurnitureId = "missing.furniture",
                        SurfaceId = "test-surface",
                        AnchorX = 0,
                        AnchorY = 0,
                        Direction = FurniturePlacementDirection.North
                    }
                };

                var restored = FurniturePlacementSaveUtility.Restore(fixture.Surface, registry, saveData, new FurnitureDefinition[0]);

                Assert.AreEqual(0, restored.RestoredCount);
                Assert.AreEqual(1, restored.MissingDefinitionCount);
                Assert.IsNull(fixture.Objects.GetTile(Vector3Int.zero));
                Assert.IsNull(fixture.Occupancy.GetTile(Vector3Int.zero));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        public void Restore_RepeatedLoadDoesNotDuplicateTilesOrRegistryInstances()
        {
            var fixture = PlacementSurfaceFixture.Create();
            var leftTile = ScriptableObject.CreateInstance<Tile>();
            var rightTile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var definition = CreateTwoTileDesk(leftTile, rightTile);
                var registry = new FurniturePlacementRegistry();
                var saveData = new[]
                {
                    new FurniturePlacementSaveData
                    {
                        FurnitureId = "desk.large",
                        SurfaceId = "test-surface",
                        AnchorX = 1,
                        AnchorY = 1,
                        Direction = FurniturePlacementDirection.East
                    }
                };

                FurniturePlacementSaveUtility.Restore(fixture.Surface, registry, saveData, new[] { definition });
                var restored = FurniturePlacementSaveUtility.Restore(fixture.Surface, registry, saveData, new[] { definition });

                Assert.AreEqual(1, restored.RestoredCount);
                Assert.AreEqual(1, CountInstances(registry));
                Assert.AreSame(leftTile, fixture.Objects.GetTile(new Vector3Int(1, 1, 0)));
                Assert.AreSame(rightTile, fixture.Objects.GetTile(new Vector3Int(1, 0, 0)));
            }
            finally
            {
                Object.DestroyImmediate(leftTile);
                Object.DestroyImmediate(rightTile);
                fixture.Destroy();
            }
        }

        [Test]
        public void HOUSE_FURNITURE_SAVE_001_StageScopedLayoutsDoNotOverwriteEachOther()
        {
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "house-furniture-stage-scope", DisplayName = "house-furniture-stage-scope" });

            var stage0 = new[]
            {
                new FurniturePlacementSaveData
                {
                    FurnitureId = "chair",
                    AnchorX = 0,
                    AnchorY = 0,
                    Direction = FurniturePlacementDirection.North
                }
            };

            var stage1 = new[]
            {
                new FurniturePlacementSaveData
                {
                    FurnitureId = "desk",
                    AnchorX = 2,
                    AnchorY = 2,
                    Direction = FurniturePlacementDirection.East
                }
            };

            FurniturePlacementLayoutPersistence.SaveHouseLayoutForStage(0, stage0);
            FurniturePlacementLayoutPersistence.SaveHouseLayoutForStage(1, stage1);

            var loaded = new List<FurniturePlacementSaveData>();
            Assert.IsTrue(FurniturePlacementLayoutPersistence.TryLoadHouseLayoutForStage(1, loaded));
            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual("desk", loaded[0].FurnitureId);
        }
        private static int CountInstances(FurniturePlacementRegistry registry)
        {
            int count = 0;
            foreach (var _ in registry.Instances)
            {
                count++;
            }

            return count;
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
                var root = new GameObject("PlacementSaveFixture");
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
