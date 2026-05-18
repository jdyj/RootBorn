using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class ModernOfficeFurniturePainterEditorTests
    {
        [Test]
        public void MenuItem_IsAvailableForModernOfficeFurniturePainter()
        {
            Assert.IsTrue(
                EditorApplication.ExecuteMenuItem("Rootborn/Interiors/Modern Office Furniture Painter"),
                "The Modern Office furniture painter should be available from the Rootborn editor menu.");

            var window = EditorWindow.GetWindow<Rootborn.Editor.Interiors.ModernOfficeFurniturePainterWindow>();
            Assert.IsNotNull(window);
            window.Close();
        }

        [Test]
        public void LoadSampleTiles_ReturnsGeneratedChairTiles()
        {
            var tiles = Rootborn.Editor.Interiors.ModernOfficeFurniturePainterWindow.LoadSampleTilesForTests();
            Assert.AreEqual(16, tiles.Count);
            Assert.IsTrue(tiles.Exists(entry => entry.Name == "BlackChair_00"));
            Assert.IsTrue(tiles.Exists(entry => entry.Name == "OrangeChair_05"));
            Assert.IsTrue(tiles.Exists(entry => entry.Name == "Chair_03"));
        }

        [Test]
        public void PaintTile_PlacesAndErasesSelectedTileOnTargetTilemap()
        {
            var root = new GameObject("PainterTestRoot", typeof(Grid));
            var mapObject = new GameObject("PainterTestTilemap", typeof(Tilemap), typeof(TilemapRenderer));
            mapObject.transform.SetParent(root.transform, false);
            try
            {
                var tilemap = mapObject.GetComponent<Tilemap>();
                var tile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Data/Interiors/TileSets/ModernOfficeChairSamples/BlackChair_00.asset");
                Assert.IsNotNull(tile);

                var cell = new Vector3Int(2, 3, 0);
                Rootborn.Editor.Interiors.ModernOfficeFurniturePainterWindow.PaintTileForTests(tilemap, tile, cell, erase: false);
                Assert.AreSame(tile, tilemap.GetTile(cell));

                Rootborn.Editor.Interiors.ModernOfficeFurniturePainterWindow.PaintTileForTests(tilemap, tile, cell, erase: true);
                Assert.IsNull(tilemap.GetTile(cell));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
