using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEditor;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class ModernOfficePaletteAssetTests
    {
        private const string OfficeFolder = "Assets/Modern_Town/1_Room_Builder_Office_48x48";
        private const string VerticalWallAccentPath = OfficeFolder + "/tile_r02_c09.asset";

        [Test]
        public void RoomBuilderOfficeTileAssets_AllHaveSprites()
        {
            var guids = AssetDatabase.FindAssets("t:Tile", new[] { OfficeFolder });

            Assert.Greater(guids.Length, 0, "The room-builder office folder should contain Tile assets.");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                Assert.IsNotNull(tile, path);
                Assert.IsNotNull(tile.sprite, path + " should reference an existing Sprite instead of rendering as a missing pink tile.");
            }
        }

        [Test]
        public void HouseOfficeTileSet_UsesRoomBuilderOfficeTilesForFurniture()
        {
            var tileSet = AssetDatabase.LoadAssetAtPath<InteriorTileSetDefinition>("Assets/Data/Interiors/TileSets/TileSetDefinition_NewPaletteOffice.asset");

            Assert.IsNotNull(tileSet);
            AssertTileFromRoomBuilder(tileSet.Desk, "desk");
            AssertTileFromRoomBuilder(tileSet.Chair, "chair");
            AssertTileFromRoomBuilder(tileSet.Computer, "computer");
            AssertTileFromRoomBuilder(tileSet.Sofa, "sofa");
            AssertTileFromRoomBuilder(tileSet.Plant, "plant");
        }

        [Test]
        public void HouseOfficeTileSet_UsesTileR02C09AsVerticalWallAccent()
        {
            var tileSet = AssetDatabase.LoadAssetAtPath<InteriorTileSetDefinition>("Assets/Data/Interiors/TileSets/TileSetDefinition_NewPaletteOffice.asset");

            Assert.IsNotNull(tileSet);
            Assert.IsNotNull(tileSet.VerticalWallAccent);
            Assert.AreEqual(VerticalWallAccentPath, AssetDatabase.GetAssetPath(tileSet.VerticalWallAccent));
        }

        private static void AssertTileFromRoomBuilder(TileBase tile, string label)
        {
            Assert.IsNotNull(tile, label);
            var path = AssetDatabase.GetAssetPath(tile);
            StringAssert.StartsWith(OfficeFolder, path, label + " should come from the original room-builder office palette folder.");
        }
    }
}
