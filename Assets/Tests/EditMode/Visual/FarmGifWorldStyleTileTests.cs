using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Visual
{
    public sealed class FarmGifWorldStyleTileTests
    {
        private const string ScenePath = "Assets/Scenes/Farm.unity";
        private const string GroundPath = "Grid/Ground";
        private const string GrassBaseTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/grass_base.asset";
        private const string DirtPathTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/dirt_path.asset";
        private const string RoadPaverTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/road_paver.asset";

        private static readonly string[] AllowedCuratedTilePaths =
        {
            GrassBaseTilePath,
            DirtPathTilePath,
            RoadPaverTilePath,
        };

        [Test]
        public void FarmGroundUsesCuratedGifFarmTiles()
        {
            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(scene.IsValid(), "Could not open Farm scene.");

                var groundObject = GameObject.Find(GroundPath);
                Assert.IsNotNull(groundObject, "Farm must keep the editable Ground tilemap at Grid/Ground.");
                var tilemap = groundObject.GetComponent<Tilemap>();
                Assert.IsNotNull(tilemap, "Grid/Ground must have a Tilemap component.");

                var allowed = new HashSet<string>(AllowedCuratedTilePaths);
                var occupied = 0;
                var grassBase = 0;
                var dirtPath = 0;
                var roadPaver = 0;

                foreach (var position in tilemap.cellBounds.allPositionsWithin)
                {
                    var tile = tilemap.GetTile(position);
                    if (tile == null)
                    {
                        continue;
                    }

                    occupied++;
                    var assetPath = AssetDatabase.GetAssetPath(tile);
                    Assert.True(allowed.Contains(assetPath), "Farm Ground uses a non-curated Gif farm tile at " + position + ": " + assetPath);

                    if (assetPath == GrassBaseTilePath)
                    {
                        grassBase++;
                    }
                    else if (assetPath == DirtPathTilePath)
                    {
                        dirtPath++;
                    }
                    else if (assetPath == RoadPaverTilePath)
                    {
                        roadPaver++;
                    }
                }

                Assert.GreaterOrEqual(occupied, 1100, "Farm should have a broad saved Gif ground field instead of an empty Ground tilemap.");
                Assert.GreaterOrEqual(grassBase, 800, "Farm base terrain should use the clean Gif grass base tile so the tile-only pass does not look like placed props.");
                Assert.GreaterOrEqual(dirtPath, 160, "Farm should include enough Gif dirt path/plot cells for a farm layout.");
                Assert.GreaterOrEqual(roadPaver, 30, "Farm should include a small paved entrance area using the Gif road paver tile.");
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
