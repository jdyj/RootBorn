using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Visual
{
    public sealed class GifWorldStylePreviewSceneTests
    {
        private const string ScenePath = "Assets/Scenes/GifWorldStylePreview.unity";

        [Test]
        public void PreviewSceneContainsGifTerrainAndWorldProps()
        {
            Assert.IsTrue(System.IO.File.Exists(ScenePath), $"Missing preview scene at {ScenePath}.");

            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(scene.IsValid(), "Gif world style preview scene did not open as a valid scene.");

                var terrainObject = GameObject.Find("Gif Terrain Preview");
                Assert.IsNotNull(terrainObject, "Missing Gif Terrain Preview Tilemap object.");

                var tilemap = terrainObject.GetComponent<Tilemap>();
                Assert.IsNotNull(tilemap, "Gif Terrain Preview must have a Tilemap component.");

                var occupiedCells = 0;
                var fangCells = 0;
                var bounds = tilemap.cellBounds;
                for (var x = bounds.xMin; x < bounds.xMax; x++)
                {
                    for (var y = bounds.yMin; y < bounds.yMax; y++)
                    {
                        var tile = tilemap.GetTile(new Vector3Int(x, y, 0));
                        if (tile == null)
                        {
                            continue;
                        }

                        occupiedCells++;
                        var typeName = tile.GetType().FullName;
                        if (typeName == "Ruccho.Fang.FangAutoTile" || typeName == "Ruccho.Utilities.FangAutoTile")
                        {
                            fangCells++;
                        }
                    }
                }

                Assert.GreaterOrEqual(occupiedCells, 140, "Preview terrain should be broad enough to judge a world art direction.");
                Assert.AreEqual(occupiedCells, fangCells, "Preview terrain should be composed from FangAutoTile assets.");

                Assert.GreaterOrEqual(GameObject.FindGameObjectsWithTag("EditorOnly").Length, 12, "Preview should contain tagged Gif prop instances for visual judgment.");
                Assert.IsNotNull(GameObject.Find("Gif House Cluster"), "Missing house cluster root.");
                Assert.IsNotNull(GameObject.Find("Gif Forest Cluster"), "Missing forest cluster root.");
                Assert.IsNotNull(GameObject.Find("Gif Prop Cluster"), "Missing prop cluster root.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousScene))
                {
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
                }
            }
        }
    }
}