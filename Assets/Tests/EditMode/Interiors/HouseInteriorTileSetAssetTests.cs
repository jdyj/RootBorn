using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEditor;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class HouseInteriorTileSetAssetTests
    {
        private const string OfficeFolder = "Assets/Modern_Town/1_Room_Builder_Office_48x48";

        [Test]
        public void HouseOfficeTileSet_UsesRoomBuilderOfficeFurnitureTiles()
        {
            var tileSet = AssetDatabase.LoadAssetAtPath<InteriorTileSetDefinition>("Assets/Data/Interiors/TileSets/TileSetDefinition_NewPaletteOffice.asset");
            Assert.IsNotNull(tileSet);

            AssertFurnitureTileFromRoomBuilderOffice(tileSet.Desk, "desk");
            AssertFurnitureTileFromRoomBuilderOffice(tileSet.Chair, "chair");
            AssertFurnitureTileFromRoomBuilderOffice(tileSet.Computer, "computer");
            AssertFurnitureTileFromRoomBuilderOffice(tileSet.Sofa, "sofa");
            AssertFurnitureTileFromRoomBuilderOffice(tileSet.Plant, "plant");
        }

        private static void AssertFurnitureTileFromRoomBuilderOffice(TileBase tileBase, string label)
        {
            Assert.IsNotNull(tileBase, label);
            var tile = tileBase as Tile;
            Assert.IsNotNull(tile, label + " should be a Unity Tile asset.");
            Assert.IsNotNull(tile.sprite, label + " tile should reference a sprite.");

            string tilePath = AssetDatabase.GetAssetPath(tileBase);
            StringAssert.StartsWith(OfficeFolder, tilePath, label + " should come from the room-builder office palette folder.");
        }
    }
}
