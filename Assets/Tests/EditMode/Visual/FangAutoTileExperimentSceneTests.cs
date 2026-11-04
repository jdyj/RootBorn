using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Visual
{
    public sealed class FangAutoTileExperimentSceneTests
    {
        private const string ScenePath = "Assets/Scenes/FangAutoTileExperiment.unity";
        private const string TilemapObjectName = "Autotile Preview";

        [Test]
        public void ExperimentSceneContainsSavedFangAutoTilePreviewTilemap()
        {
            Assert.IsTrue(System.IO.File.Exists(ScenePath), $"Missing experiment scene at {ScenePath}.");

            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(scene.IsValid(), "Experiment scene did not open as a valid scene.");

                var tilemapObject = GameObject.Find(TilemapObjectName);
                Assert.IsNotNull(tilemapObject, $"Missing tilemap GameObject '{TilemapObjectName}'.");

                var tilemap = tilemapObject.GetComponent<Tilemap>();
                Assert.IsNotNull(tilemap, $"'{TilemapObjectName}' does not have a Tilemap component.");

                var usedTiles = tilemap.GetUsedTilesCount();
                Assert.GreaterOrEqual(usedTiles, 1, "The preview tilemap has no saved tile references.");

                var bounds = tilemap.cellBounds;
                var occupiedCells = 0;
                var fangCells = 0;
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

                Assert.GreaterOrEqual(occupiedCells, 20, "The preview should contain a visible patch, not a single tile.");
                Assert.AreEqual(occupiedCells, fangCells, "Every saved preview tile should be a FangAutoTile asset.");
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