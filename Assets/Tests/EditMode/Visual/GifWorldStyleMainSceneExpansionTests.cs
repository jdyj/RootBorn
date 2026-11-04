using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Resources;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Visual
{
    public sealed class GifWorldStyleMainSceneExpansionTests
    {
        private const string BeachScenePath = "Assets/Scenes/Beach.unity";
        private const string Kitchen2ScenePath = "Assets/Scenes/Kitchen2Interior.unity";
        private const string SandTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/sand_edge.asset";
        private const string GrassTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/grass_base.asset";
        private const string InteriorFloorTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/interior_floor.asset";
        private const string InteriorWallTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/interior_wall.asset";
        private const string InteriorCounterTilePath = "Assets/Tiles/GifWorldStyle/Autotiles/interior_counter.asset";
        private const string TreeDefinitionPath = "Assets/Data/Resources/Resource_Tree.asset";
        private const string RockDefinitionPath = "Assets/Data/Resources/Resource_Rock.asset";
        private const string GifTreePrefabPath = "Assets/Prefabs/GifWorldStyle/WorldProps/tree_01.prefab";
        private const string GifRockPrefabPath = "Assets/Prefabs/GifWorldStyle/WorldProps/rock_01.prefab";

        [Test]
        public void BeachTerrainUsesCuratedGifWorldTiles()
        {
            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(BeachScenePath, OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(scene.IsValid(), "Could not open Beach scene.");
                AssertTileExists(SandTilePath);
                AssertTileExists(GrassTilePath);

                var expectedByMap = new Dictionary<string, string>
                {
                    { "Town2LdtkTilemaps/Town2LdtkBeachTilemap", SandTilePath },
                    { "Town2LdtkTilemaps/Town2LdtkGroundTilemap", GrassTilePath },
                };

                var occupied = 0;
                var sand = 0;
                var grass = 0;
                foreach (var pair in expectedByMap)
                {
                    var tilemap = FindTilemap(pair.Key);
                    foreach (var position in tilemap.cellBounds.allPositionsWithin)
                    {
                        var tile = tilemap.GetTile(position);
                        if (tile == null)
                        {
                            continue;
                        }

                        occupied++;
                        var path = AssetDatabase.GetAssetPath(tile);
                        Assert.AreEqual(pair.Value, path, pair.Key + " uses unexpected tile at " + position + ": " + path);
                        if (path == SandTilePath)
                        {
                            sand++;
                        }
                        else if (path == GrassTilePath)
                        {
                            grass++;
                        }
                    }
                }

                Assert.GreaterOrEqual(occupied, 1200, "Beach should keep broad terrain coverage.");
                Assert.GreaterOrEqual(sand, 900, "Beach layer should mostly use Gif sand.");
                Assert.GreaterOrEqual(grass, 300, "Beach ground layer should keep a Gif grass backing.");
            }
            finally
            {
                RestoreScene(previousScene);
            }
        }

        [Test]
        public void FarmRuntimeResourceDefinitionsUseCuratedGifSprites()
        {
            var tree = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(TreeDefinitionPath);
            var rock = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(RockDefinitionPath);
            Assert.IsNotNull(tree, "Missing Tree resource definition.");
            Assert.IsNotNull(rock, "Missing Rock resource definition.");

            var gifTreeSprite = LoadPrefabSprite(GifTreePrefabPath);
            var gifRockSprite = LoadPrefabSprite(GifRockPrefabPath);
            Assert.AreSame(gifTreeSprite, tree.Sprite, "FarmAutoFiller should spawn Tree nodes with the curated Gif tree sprite via Resource_Tree.");
            Assert.AreSame(gifRockSprite, rock.Sprite, "FarmAutoFiller should spawn Rock nodes with the curated Gif rock sprite via Resource_Rock.");
        }

        [Test]
        public void Kitchen2InteriorPilotUsesCuratedGifInteriorTiles()
        {
            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(Kitchen2ScenePath, OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(scene.IsValid(), "Could not open Kitchen2Interior scene.");
                AssertTileExists(InteriorFloorTilePath);
                AssertTileExists(InteriorWallTilePath);
                AssertTileExists(InteriorCounterTilePath);

                var expectedByMap = new Dictionary<string, string>
                {
                    { "Kitchen2InteriorLdtkTilemaps/Kitchen2InteriorGroundTilemap", InteriorFloorTilePath },
                    { "Kitchen2InteriorLdtkTilemaps/Kitchen2InteriorWallTilemap", InteriorWallTilePath },
                    { "Kitchen2InteriorLdtkTilemaps/Kitchen2InteriorBorderTilemap", InteriorWallTilePath },
                    { "Kitchen2InteriorLdtkTilemaps/Kitchen2InteriorBorderOverdraw01Tilemap", InteriorWallTilePath },
                    { "Kitchen2InteriorLdtkTilemaps/Kitchen2InteriorKitchenTilemap", InteriorCounterTilePath },
                    { "Kitchen2InteriorLdtkTilemaps/Kitchen2InteriorKitchenOverdraw01Tilemap", InteriorCounterTilePath },
                    { "Kitchen2InteriorLdtkTilemaps/Kitchen2InteriorKitchenOverdraw02Tilemap", InteriorCounterTilePath },
                };

                var occupied = 0;
                foreach (var pair in expectedByMap)
                {
                    var tilemap = FindTilemap(pair.Key);
                    foreach (var position in tilemap.cellBounds.allPositionsWithin)
                    {
                        var tile = tilemap.GetTile(position);
                        if (tile == null)
                        {
                            continue;
                        }

                        occupied++;
                        var path = AssetDatabase.GetAssetPath(tile);
                        Assert.AreEqual(pair.Value, path, pair.Key + " uses unexpected tile at " + position + ": " + path);
                    }
                }

                Assert.GreaterOrEqual(occupied, 170, "Kitchen2Interior pilot should retain the main visible tile coverage.");
            }
            finally
            {
                RestoreScene(previousScene);
            }
        }

        private static Tilemap FindTilemap(string path)
        {
            var go = GameObject.Find(path);
            Assert.IsNotNull(go, "Missing tilemap object: " + path);
            var tilemap = go.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, "Object must have Tilemap: " + path);
            return tilemap;
        }

        private static void AssertTileExists(string path)
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TileBase>(path), "Missing curated Gif tile: " + path);
        }

        private static Sprite LoadPrefabSprite(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, "Missing curated Gif prefab: " + prefabPath);
            var renderer = prefab.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, "Curated Gif prefab needs SpriteRenderer: " + prefabPath);
            Assert.IsNotNull(renderer.sprite, "Curated Gif prefab needs sprite: " + prefabPath);
            return renderer.sprite;
        }

        private static void RestoreScene(string previousScene)
        {
            if (!string.IsNullOrEmpty(previousScene))
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }
    }
}
