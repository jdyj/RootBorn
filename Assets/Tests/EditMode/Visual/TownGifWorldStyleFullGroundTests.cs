using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Visual
{
    public sealed class TownGifWorldStyleFullGroundTests
    {
        private const string ScenePath = "Assets/Scenes/Town.unity";
        private const string GrassDetailTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/grass_detail.asset";
        private const string RoadPaverTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/road_paver.asset";
        private const string DirtPathTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/dirt_path.asset";

        private static readonly string[] GroundFamilyTilemaps =
        {
            "Town2LdtkTilemaps/Town2LdtkGroundTilemap",
            "Town2LdtkTilemaps/Town2LdtkGround2Tilemap",
        };

        private static readonly string[] AllowedCuratedTilePaths =
        {
            "Assets/Tiles/GifWorldStyle/Autotiles/grass_base.asset",
            GrassDetailTilePath,
            "Assets/Tiles/GifWorldStyle/Autotiles/sand_edge.asset",
            "Assets/Tiles/GifWorldStyle/Autotiles/stone_edge.asset",
            RoadPaverTilePath,
            DirtPathTilePath,
            "Assets/Tiles/GifWorldStyle/Autotiles/water_sand.asset",
            "Assets/Tiles/GifWorldStyle/Autotiles/water_rock.asset",
        };

        [Test]
        public void TownGroundFamilyUsesOnlyCuratedGifAutotiles()
        {
            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(scene.IsValid(), "Could not open Town scene.");
                Assert.IsNotNull(AssetDatabase.LoadAllAssetsAtPath(GrassDetailTilePath).OfType<TileBase>().FirstOrDefault(), "Missing curated Gif grass detail autotile.");
                Assert.IsNotNull(AssetDatabase.LoadAllAssetsAtPath(RoadPaverTilePath).OfType<TileBase>().FirstOrDefault(), "Missing curated Gif road paver autotile.");
                Assert.IsNotNull(AssetDatabase.LoadAllAssetsAtPath(DirtPathTilePath).OfType<TileBase>().FirstOrDefault(), "Missing curated Gif dirt path autotile.");

                var allowed = new HashSet<string>(AllowedCuratedTilePaths);
                var occupied = 0;
                var curated = 0;
                var grassDetail = 0;
                var roadPaver = 0;
                var dirtPath = 0;

                foreach (var tilemapPath in GroundFamilyTilemaps)
                {
                    var tilemapObject = GameObject.Find(tilemapPath);
                    Assert.IsNotNull(tilemapObject, "Missing ground-family tilemap: " + tilemapPath);
                    var tilemap = tilemapObject.GetComponent<Tilemap>();
                    Assert.IsNotNull(tilemap, "Ground-family object must have a Tilemap: " + tilemapPath);

                    foreach (var position in tilemap.cellBounds.allPositionsWithin)
                    {
                        var tile = tilemap.GetTile(position);
                        if (tile == null)
                        {
                            continue;
                        }

                        occupied++;
                        var assetPath = AssetDatabase.GetAssetPath(tile);
                        Assert.False(assetPath.StartsWith("Assets/LDtk/Generated/TownTiles/"), "Ground-family tilemap still uses LDtk tile at " + position + ": " + assetPath);
                        Assert.True(allowed.Contains(assetPath), "Ground-family tilemap uses unexpected non-curated tile at " + position + ": " + assetPath);
                        if (assetPath == GrassDetailTilePath)
                        {
                            grassDetail++;
                        }
                        if (assetPath == RoadPaverTilePath)
                        {
                            roadPaver++;
                        }
                        if (assetPath == DirtPathTilePath)
                        {
                            dirtPath++;
                        }
                        curated++;
                    }
                }

                Assert.GreaterOrEqual(occupied, 5200, "Town ground-family conversion should keep the existing broad ground coverage.");
                Assert.AreEqual(occupied, curated, "Every occupied ground-family cell should use a curated Gif autotile.");
                Assert.GreaterOrEqual(grassDetail, 2300, "Broad Town grass should use the textured Gif grass detail autotile instead of the flatter grass base.");
                Assert.GreaterOrEqual(roadPaver, 2500, "Town roads and paved areas should primarily use the less repetitive Gif road paver autotile.");
                Assert.GreaterOrEqual(dirtPath, 200, "Broad beige paths should use the textured Gif dirt path autotile instead of a flat sand edge fill.");
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