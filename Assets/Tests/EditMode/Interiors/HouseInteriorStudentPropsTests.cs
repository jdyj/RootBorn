using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class HouseInteriorStudentPropsTests
    {
        private const string ScenePath = "Assets/Scenes/HouseInterior.unity";
        private const string ArtRoot = "Assets/Art/StudentLifePixels/Sprites/";

        [TestCase("ToyKitchen", "toy_kitchen.png", 7.25f, -6.5f, 1.4f, 0.35f)]
        [TestCase("SnackDeliveryCrate", "snack_delivery_crate.png", 2.5f, -12.5f, 0.75f, 0.3f)]
        public void SavedNormalHouseProp_HasPersistentArtSeparateGroundContactAndDepthSorting(
            string objectName, string file, float x, float y, float width, float depth)
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var root = Find<Transform>(scene, "[StudentLifeHouseProps]");
                Assert.IsNotNull(root, "Normal Town house entry must contain the new saved household art.");
                var prop = root.Find(objectName);
                Assert.IsNotNull(prop, objectName);
                Assert.AreEqual(new Vector3(x, y, 0f), prop.position);
                Assert.AreEqual(Vector3.one, prop.localScale, "Keep the approved native 48 PPU scale.");

                var visual = prop.Find("Visual");
                Assert.IsNotNull(visual);
                var renderer = visual.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(renderer);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + file), renderer.sprite);
                Assert.IsTrue(AssetDatabase.Contains(renderer.sprite));
                Assert.IsNotNull(renderer.sprite.texture);
                Assert.AreEqual(48f, renderer.sprite.pixelsPerUnit);
                Assert.AreEqual(Vector3.zero, visual.localPosition);
                Assert.AreEqual(0, visual.GetComponents<Collider2D>().Length, "Visual silhouette must not define the walking footprint.");

                var contact = prop.Find("GroundContact");
                Assert.IsNotNull(contact);
                var collider = contact.GetComponent<BoxCollider2D>();
                Assert.IsNotNull(collider);
                Assert.IsFalse(collider.isTrigger);
                Assert.AreEqual(new Vector2(width, depth), collider.size);
                Assert.Less(collider.size.y, renderer.sprite.bounds.size.y * 0.5f);
                Assert.AreEqual(new Vector2(0f, depth * 0.5f), collider.offset);
                Assert.AreEqual(0, contact.GetComponents<SpriteRenderer>().Length);

                var group = prop.GetComponent<SortingGroup>();
                Assert.IsNotNull(group);
                Assert.IsNotNull(prop.GetComponent<GroundDepthSort>());
                Assert.AreEqual(10000 - Mathf.RoundToInt(y * 48f), group.sortingOrder);
                Assert.Greater(group.sortingOrder, 11, "The prop must render above all existing interior Tilemaps.");

                var ground = Find<Tilemap>(scene, "HouseInteriorGroundTilemap");
                Assert.IsNotNull(ground);
                var min = ground.WorldToCell(prop.position + new Vector3(-width * 0.5f + 0.01f, 0.01f, 0f));
                var max = ground.WorldToCell(prop.position + new Vector3(width * 0.5f - 0.01f, depth - 0.01f, 0f));
                for (int cy = min.y; cy <= max.y; cy++)
                    for (int cx = min.x; cx <= max.x; cx++)
                    {
                        var cell = new Vector3Int(cx, cy, 0);
                        Assert.IsTrue(ground.HasTile(cell), objectName + " must sit on existing ground at " + cell);
                        foreach (var layerName in new[] { "HouseInteriorWall3dTilemap", "HouseInteriorHouse2Tilemap", "HouseInteriorBorderTilemap" })
                            Assert.IsFalse(Find<Tilemap>(scene, layerName).HasTile(cell), objectName + " overlaps " + layerName + " at " + cell);
                    }
                Assert.Greater(Mathf.Abs(prop.position.x - 5.5f), width * 0.5f + 0.5f, "Keep the central return path clear.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SavedNormalHouse_RetainsTheAuthoredLdtkLayersAndEntry()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                Assert.IsNotNull(Find<Transform>(scene, "House_1"));
                Assert.AreEqual(new Vector3(0.5f, -6.5f, 0f), Find<Transform>(scene, "HouseInteriorSpawnPoint").position);
                AssertLayer(scene, "HouseInteriorGroundTilemap", 154, 0);
                AssertLayer(scene, "HouseInteriorWall3dTilemap", 64, 2);
                AssertLayer(scene, "HouseInteriorWall3dOverdraw01Tilemap", 1, 3);
                AssertLayer(scene, "HouseInteriorBorderTilemap", 8, 5);
                AssertLayer(scene, "HouseInteriorHouse2Tilemap", 17, 7);
                var player = Find<Transform>(scene, "Player");
                var playerSort = player.GetComponent<GroundDepthSort>();
                Assert.IsNotNull(playerSort, "Direct Play and a travelling Town player must use the same foot sorting.");
                Assert.IsNotNull(player.GetComponent<SortingGroup>());
                var sortData = new SerializedObject(playerSort);
                Assert.AreEqual("HouseInterior", sortData.FindProperty("_activeSceneName").stringValue);
                Assert.IsTrue(sortData.FindProperty("_hasScopedFallback").boolValue, "Original no-group fallback must survive scene serialization.");
                Assert.IsFalse(sortData.FindProperty("_fallbackGroupEnabled").boolValue);
                Assert.AreEqual(0, sortData.FindProperty("_fallbackSortingOrder").intValue);
                Assert.AreEqual(new Vector2(0f, -0.5f), sortData.FindProperty("_footPointOffset").vector2Value);
                Assert.AreEqual(48, sortData.FindProperty("_unitsPerWorld").intValue);
                Assert.AreEqual(10000, sortData.FindProperty("_baseSortingOrder").intValue);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void AssertLayer(Scene scene, string name, int expectedCount, int order)
        {
            var map = Find<Tilemap>(scene, name);
            Assert.IsNotNull(map);
            int count = 0;
            foreach (var cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) count++;
            Assert.AreEqual(expectedCount, count, name);
            Assert.AreEqual(order, map.GetComponent<TilemapRenderer>().sortingOrder, name);
        }

        private static T Find<T>(Scene scene, string name) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<T>(true))
                    if (component.gameObject.name == name) return component;
            return null;
        }
    }
}
