using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode.Visual
{
    public sealed class GifWorldStylePreviewPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/GifWorldStylePreview.unity";

        [UnityTest]
        public IEnumerator VISUAL_PM_002_GifWorldStylePreviewRendersTerrainAndProps()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            Assert.IsNotNull(Camera.main, "Preview must expose a player-facing Main Camera.");
            Assert.IsTrue(Camera.main.orthographic, "Preview camera must be orthographic for tile readability.");

            var terrainObject = GameObject.Find("Gif Terrain Preview");
            Assert.IsNotNull(terrainObject, "Missing runtime terrain tilemap.");
            Assert.IsTrue(terrainObject.activeInHierarchy, "Runtime terrain tilemap must be active.");

            var renderer = terrainObject.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, "Runtime terrain tilemap must have a renderer.");
            Assert.IsTrue(renderer.enabled, "Runtime terrain renderer must be enabled.");

            var tilemap = terrainObject.GetComponent<Tilemap>();
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

            Assert.GreaterOrEqual(occupiedCells, 140, "Runtime terrain should be broad enough for visual judgment.");
            Assert.AreEqual(occupiedCells, fangCells, "Runtime terrain should use FangAutoTile assets.");
            Assert.GreaterOrEqual(GameObject.FindGameObjectsWithTag("EditorOnly").Length, 12, "Runtime preview should contain visible Gif prop instances.");
        }
    }
}