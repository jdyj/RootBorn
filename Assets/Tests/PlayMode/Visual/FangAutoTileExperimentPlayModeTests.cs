using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode.Visual
{
    public sealed class FangAutoTileExperimentPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/FangAutoTileExperiment.unity";

        [UnityTest]
        public IEnumerator VISUAL_PM_001_FangAutoTileExperimentRendersPreviewTilemap()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            var camera = Camera.main;
            Assert.IsNotNull(camera, "Experiment scene must expose a player-facing Main Camera.");
            Assert.IsTrue(camera.orthographic, "Experiment camera should be orthographic for tilemap inspection.");

            var tilemapObject = GameObject.Find("Autotile Preview");
            Assert.IsNotNull(tilemapObject, "Autotile Preview must exist in PlayMode.");

            var tilemap = tilemapObject.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, "Autotile Preview must have a Tilemap component in PlayMode.");

            var renderer = tilemapObject.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, "Autotile Preview must have a TilemapRenderer in PlayMode.");
            Assert.IsTrue(renderer.enabled, "Autotile Preview renderer must be enabled.");
            Assert.IsTrue(tilemapObject.activeInHierarchy, "Autotile Preview must be active in the runtime hierarchy.");

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

            Assert.GreaterOrEqual(occupiedCells, 20, "PlayMode preview should contain a visible tile patch.");
            Assert.AreEqual(occupiedCells, fangCells, "Every runtime preview cell should use a FangAutoTile asset.");
        }
    }
}