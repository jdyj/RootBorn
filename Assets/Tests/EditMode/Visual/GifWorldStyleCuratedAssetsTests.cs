using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Visual
{
    public sealed class GifWorldStyleCuratedAssetsTests
    {
        private const string ScenePath = "Assets/Scenes/GifWorldStylePreview.unity";

        private static readonly string[] CuratedAutotiles =
        {
            "Assets/Tiles/GifWorldStyle/Autotiles/grass_base.asset",
            "Assets/Tiles/GifWorldStyle/Autotiles/grass_detail.asset",
            "Assets/Tiles/GifWorldStyle/Autotiles/sand_edge.asset",
            "Assets/Tiles/GifWorldStyle/Autotiles/stone_edge.asset",
            "Assets/Tiles/GifWorldStyle/Autotiles/water_sand.asset",
            "Assets/Tiles/GifWorldStyle/Autotiles/water_rock.asset",
        };

        private static readonly string[] CuratedPrefabs =
        {
            "Assets/Prefabs/GifWorldStyle/WorldProps/house_01.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/house_13.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/house_25.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/tree_01.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/tree_06.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/tree_14.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/tree_20.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/rock_01.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/rock_12.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/crate_01.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/barrel_01.prefab",
            "Assets/Prefabs/GifWorldStyle/WorldProps/crop_04.prefab",
        };

        [Test]
        public void CuratedGifWorldStyleAssetsExistOutsideSourceResources()
        {
            foreach (var path in CuratedAutotiles)
            {
                Assert.False(path.Contains("/Resources/"), $"Curated autotile path must not be under Resources: {path}");
                var tile = AssetDatabase.LoadAllAssetsAtPath(path).OfType<TileBase>().FirstOrDefault();
                Assert.IsNotNull(tile, $"Missing curated FangAutoTile asset at {path}.");
                var typeName = tile.GetType().FullName;
                Assert.IsTrue(typeName == "Ruccho.Fang.FangAutoTile" || typeName == "Ruccho.Utilities.FangAutoTile", $"Curated tile is not a FangAutoTile: {path} ({typeName})");
            }

            foreach (var path in CuratedPrefabs)
            {
                Assert.False(path.Contains("/Resources/"), $"Curated prefab path must not be under Resources: {path}");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(path), $"Missing curated Gif prefab at {path}.");
            }
        }

        [Test]
        public void CuratedGifWorldStylePrefabsDoNotDependOnSourceResources()
        {
            foreach (var prefabPath in CuratedPrefabs)
            {
                var dependencies = AssetDatabase.GetDependencies(prefabPath, true);
                foreach (var dependency in dependencies)
                {
                    if (dependency == prefabPath || dependency.EndsWith(".cs"))
                    {
                        continue;
                    }

                    Assert.False(
                        dependency.StartsWith("Assets/Gif/Super_Retro_Collection/Resources/"),
                        $"Curated prefab {prefabPath} still depends on source Resources asset: {dependency}");
                }
            }
        }

        [Test]
        public void PreviewSceneUsesCuratedGifTerrainTiles()
        {
            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(scene.IsValid(), $"Could not open preview scene at {ScenePath}.");
                var terrainObject = GameObject.Find("Gif Terrain Preview");
                Assert.IsNotNull(terrainObject, "Missing Gif Terrain Preview Tilemap object.");

                var tilemap = terrainObject.GetComponent<Tilemap>();
                Assert.IsNotNull(tilemap, "Gif Terrain Preview must have a Tilemap component.");

                var occupiedCells = 0;
                foreach (var position in tilemap.cellBounds.allPositionsWithin)
                {
                    var tile = tilemap.GetTile(position);
                    if (tile == null)
                    {
                        continue;
                    }

                    occupiedCells++;
                    var path = AssetDatabase.GetAssetPath(tile);
                    Assert.Contains(path, CuratedAutotiles, $"Preview scene tile at {position} should use a curated GifWorldStyle autotile, not {path}.");
                    Assert.False(path.Contains("/Resources/"), $"Preview scene must not reference source Resources autotile at {position}: {path}");
                }

                Assert.GreaterOrEqual(occupiedCells, 140, "Preview terrain should remain broad enough for visual judgment.");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousScene))
                {
                    EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
                }
            }
        }

        [Test]
        public void PreviewSceneUsesCuratedGifPropPrefabs()
        {
            var previousScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                Assert.IsTrue(scene.IsValid(), $"Could not open preview scene at {ScenePath}.");
                var propRoots = new[] { "Gif House Cluster", "Gif Forest Cluster", "Gif Prop Cluster" };
                var checkedInstances = 0;

                foreach (var rootName in propRoots)
                {
                    var root = GameObject.Find(rootName);
                    Assert.IsNotNull(root, $"Missing {rootName}.");

                    foreach (Transform child in root.transform)
                    {
                        checkedInstances++;
                        var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                        Assert.IsNotNull(source, $"Preview scene instance {child.name} should keep a prefab source link.");

                        var sourcePath = AssetDatabase.GetAssetPath(source);
                        Assert.True(
                            sourcePath.StartsWith("Assets/Prefabs/GifWorldStyle/WorldProps/"),
                            $"Preview scene instance {child.name} should use curated prefab, not {sourcePath}.");
                    }
                }

                Assert.GreaterOrEqual(checkedInstances, 12, "Preview scene should keep at least 12 curated prop prefab instances.");
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