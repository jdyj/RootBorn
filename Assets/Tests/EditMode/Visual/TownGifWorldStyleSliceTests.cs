using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Visual
{
    public sealed class TownGifWorldStyleSliceTests
    {
        private const string ScenePath = "Assets/Scenes/Town.unity";
        private const string GroundTilemapPath = "Town2LdtkTilemaps/Town2LdtkGroundTilemap";
        private const string DirtPathTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/dirt_path.asset";
        private const string GrassTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/grass_detail.asset";

        [Test]
        public void TownSpawnNeighborhoodUsesCuratedGifAutotilePolicy()
        {
            AssertTownZonePolicy(0, 24, -24, -8, IsTownSpawnNeighborhoodDirtPath, 425, 106, 319);
        }

        [Test]
        public void TownParkRoadSampleUsesCuratedGifAutotilePolicy()
        {
            AssertTownZonePolicy(0, 24, -40, -25, IsTownParkRoadSampleDirtPath, 400, 160, 240);
        }

        private static void AssertTownZonePolicy(int minX, int maxX, int minY, int maxY, System.Func<int, int, bool> isDirtPath, int expectedCells, int expectedDirtPathCells, int expectedGrassCells)
        {
            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(scene.IsValid(), "Could not open Town scene.");

                var groundObject = GameObject.Find(GroundTilemapPath);
                Assert.IsNotNull(groundObject, "Missing Town LDtk ground tilemap.");

                var tilemap = groundObject.GetComponent<Tilemap>();
                Assert.IsNotNull(tilemap, "Town LDtk ground object must have a Tilemap.");

                var dirtPathTile = AssetDatabase.LoadAllAssetsAtPath(DirtPathTilePath).OfType<TileBase>().FirstOrDefault();
                var grassTile = AssetDatabase.LoadAllAssetsAtPath(GrassTilePath).OfType<TileBase>().FirstOrDefault();
                Assert.IsNotNull(dirtPathTile, "Missing curated Gif dirt path autotile.");
                Assert.IsNotNull(grassTile, "Missing curated Gif grass detail autotile.");

                var checkedCells = 0;
                var dirtPathCells = 0;
                var grassCells = 0;
                for (var x = minX; x <= maxX; x++)
                {
                    for (var y = minY; y <= maxY; y++)
                    {
                        var position = new Vector3Int(x, y, 0);
                        var expectedTile = isDirtPath(x, y) ? dirtPathTile : grassTile;
                        var tile = tilemap.GetTile(position);
                        checkedCells++;
                        if (expectedTile == dirtPathTile)
                        {
                            dirtPathCells++;
                        }
                        else
                        {
                            grassCells++;
                        }

                        Assert.AreSame(expectedTile, tile, "Town Gif zone cell should use the expected curated Gif autotile at " + position);
                    }
                }

                Assert.AreEqual(expectedCells, checkedCells, "Town Gif zone cell count mismatch.");
                Assert.AreEqual(expectedDirtPathCells, dirtPathCells, "Town Gif zone dirt path cell count mismatch.");
                Assert.AreEqual(expectedGrassCells, grassCells, "Town Gif zone grass cell count mismatch.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousScene))
                {
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
                }
            }
        }

        private static bool IsTownSpawnNeighborhoodDirtPath(int x, int y)
        {
            var horizontalPath = x >= 4 && x <= 22 && y >= -16 && y <= -13;
            var verticalConnector = x >= 9 && x <= 13 && y >= -22 && y <= -13;
            return horizontalPath || verticalConnector;
        }

        private static bool IsTownParkRoadSampleDirtPath(int x, int y)
        {
            var horizontalPath = x >= 0 && x <= 24 && y >= -32 && y <= -29;
            var verticalConnector = x >= 10 && x <= 14 && y >= -40 && y <= -25;
            return horizontalPath || verticalConnector;
        }
    }
}