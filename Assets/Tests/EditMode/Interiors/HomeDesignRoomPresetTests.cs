using System;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class HomeDesignRoomPresetTests
    {
        private const string CondoCompactLayer1 = @"C:\Users\jdyj\Downloads\moderninteriors-win\6_Home_Designs\Condominium_Designs\48x48\Condominium_Design_2_layer_1_48x48.png";
        private const string CondoApartmentLayer1 = @"C:\Users\jdyj\Downloads\moderninteriors-win\6_Home_Designs\Condominium_Designs\48x48\Condominium_Design_layer_1_48x48.png";
        private const string GenericHomeLayer1 = @"C:\Users\jdyj\Downloads\moderninteriors-win\6_Home_Designs\Generic_Home_Designs\48x48\Generic_Home_1_Layer_1_48x48.png";

        [Test]
        public void AnalyzeLayer1_ExactCondominiumDesigns_ReportCellSizes()
        {
            var compact = HomeDesignLayer1GridAnalyzer.Analyze(CondoCompactLayer1, 48);
            var apartment = HomeDesignLayer1GridAnalyzer.Analyze(CondoApartmentLayer1, 48);

            Assert.IsTrue(compact.IsExactGrid);
            Assert.AreEqual(new Vector2Int(14, 6), compact.CellSize);
            Assert.AreEqual(Vector2Int.zero, compact.RemainderPixels);

            Assert.IsTrue(apartment.IsExactGrid);
            Assert.AreEqual(new Vector2Int(14, 11), apartment.CellSize);
            Assert.AreEqual(Vector2Int.zero, apartment.RemainderPixels);
        }

        [Test]
        public void AnalyzeLayer1_NonExactGenericHome_RequiresExplicitGridPolicy()
        {
            var analysis = HomeDesignLayer1GridAnalyzer.Analyze(GenericHomeLayer1, 48);

            Assert.IsFalse(analysis.IsExactGrid);
            Assert.AreEqual(new Vector2Int(14, 13), analysis.CellSize);
            Assert.AreEqual(new Vector2Int(0, 18), analysis.RemainderPixels);
        }

        [Test]
        public void CreateForTests_StoresBaseLayerCellsForPresetApplication()
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = "condo_layer_cell";
            var cells = new[]
            {
                new InteriorRoomPresetTileCell(new Vector2Int(0, 0), tile),
                new InteriorRoomPresetTileCell(new Vector2Int(13, 5), tile),
            };

            var preset = InteriorRoomPresetDefinition.CreateForTests("modern.home.condo.compact", "Condo Compact", new Vector2Int(14, 6), cells);

            Assert.AreEqual("modern.home.condo.compact", preset.StableId);
            Assert.AreEqual("Condo Compact", preset.DisplayName);
            Assert.AreEqual(new Vector2Int(14, 6), preset.Size);
            Assert.AreEqual(2, preset.BaseLayerCells.Count);
            Assert.AreSame(tile, preset.BaseLayerCells[0].Tile);
            Assert.AreEqual(new Vector2Int(13, 5), preset.BaseLayerCells[1].Cell);
        }

        [Test]
        public void ApplyBaseLayer_ClearsStaleTilesAndCentersPresetCells()
        {
            var staleTile = ScriptableObject.CreateInstance<Tile>();
            var roomTile = ScriptableObject.CreateInstance<Tile>();
            roomTile.name = "condo_room_cell";
            var preset = InteriorRoomPresetDefinition.CreateForTests(
                "modern.home.condo.compact",
                "Condo Compact",
                new Vector2Int(14, 6),
                new[]
                {
                    new InteriorRoomPresetTileCell(new Vector2Int(0, 0), roomTile),
                    new InteriorRoomPresetTileCell(new Vector2Int(13, 5), roomTile),
                });
            var go = new GameObject("RoomPresetTilemap");
            var tilemap = go.AddComponent<Tilemap>();
            tilemap.SetTile(new Vector3Int(99, 99, 0), staleTile);

            InteriorRoomPresetApplier.ApplyBaseLayer(preset, tilemap);

            Assert.IsNull(tilemap.GetTile(new Vector3Int(99, 99, 0)), "Applying a room preset must clear stale sample/debug tiles first.");
            Assert.AreSame(roomTile, tilemap.GetTile(new Vector3Int(-7, -3, 0)));
            Assert.AreSame(roomTile, tilemap.GetTile(new Vector3Int(6, 2, 0)));
            Assert.AreEqual(2, CountTiles(tilemap));
        }

        [Test]
        public void CreatePlacementMap_BuildsWalkableInteriorWithBorderCollisionAndDoor()
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            var preset = InteriorRoomPresetDefinition.CreateForTests(
                "modern.home.condo.compact",
                "Condo Compact",
                new Vector2Int(14, 6),
                new[] { new InteriorRoomPresetTileCell(Vector2Int.zero, tile) });

            var map = InteriorRoomPresetApplier.CreatePlacementMap(preset);

            Assert.AreEqual(new Vector2Int(14, 6), new Vector2Int(map.Width, map.Height));
            Assert.Greater(map.CountCells(InteriorCellKind.Floor), 0);
            Assert.Greater(map.CountCells(InteriorCellKind.Wall), 0);
            Assert.AreEqual(1, map.CountCells(InteriorCellKind.Door));
            Assert.IsTrue(InteriorPathValidator.CanReachAnyDoor(map, map.SpawnCell));
            Assert.IsTrue(map.IsCollision(new Vector2Int(0, 0)), "Outer preset border should block placement/movement.");
            Assert.IsTrue(map.IsWalkable(new Vector2Int(7, 2)), "Interior cells should remain valid furniture placement candidates.");
        }

        [Test]
        public void HOUSE_PRESET_001_FitValidationRejectsPresetOutsideGeneratedBounds()
        {
            var preset = InteriorRoomPresetDefinition.CreateForTests("preset.too-large", "Too Large", new Vector2Int(40, 40), Array.Empty<InteriorRoomPresetTileCell>());

            var map = InteriorGenerator.Generate(InteriorGenerationProfile.CreateDefaultOfficeForTests(), 1205);

            Assert.IsFalse(InteriorRoomPresetRuntimeUtility.CanFitPreset(preset, map));
        }

        [Test]
        public void HOUSE_PRESET_002_FitValidationAcceptsPresetInsideGeneratedBounds()
        {
            var preset = InteriorRoomPresetDefinition.CreateForTests("preset.fits", "Fits", new Vector2Int(6, 6), Array.Empty<InteriorRoomPresetTileCell>());

            var map = InteriorGenerator.Generate(InteriorGenerationProfile.CreateDefaultOfficeForTests(), 1205);

            Assert.IsTrue(InteriorRoomPresetRuntimeUtility.CanFitPreset(preset, map));
        }
        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
