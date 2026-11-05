using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class HousePersistentTileArtTests
    {
        private const string HousePath = "Assets/Scenes/House.unity";

        // A player build cannot read texture pixels, so a Sprite collider without a stored physics shape produces no collider at all.
        [Test]
        public void BuildScenes_CollisionTilesDoNotDependOnReadingTexturePixels()
        {
            var problems = new List<string>();
            // Opening whole scenes logs unrelated lighting errors; only the tile data is under test.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;
                var scene = EditorSceneManager.OpenPreviewScene(entry.path);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var collider in root.GetComponentsInChildren<TilemapCollider2D>(true))
                    {
                        var map = collider.GetComponent<Tilemap>();
                        var tiles = new TileBase[map.GetUsedTilesCount()];
                        map.GetUsedTilesNonAlloc(tiles);
                        foreach (var used in tiles)
                            if (used is Tile tile && tile.colliderType == Tile.ColliderType.Sprite && (tile.sprite == null || tile.sprite.GetPhysicsShapeCount() == 0))
                                problems.Add(entry.path + "/" + map.name + "/" + tile.name);
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            Assert.That(problems.Count, Is.Zero, "First: " + string.Join(", ", problems.GetRange(0, System.Math.Min(5, problems.Count))));
        }

        // Each saved cell caches its tile's collider type. A stale Sprite flag makes a player build read texture pixels.
        [Test]
        public void Town_SavedCollisionCells_CarryGridColliderFlags()
        {
            string[] docs = string.Join("\n", System.IO.File.ReadAllLines("Assets/Scenes/Town.unity")).Split(new[] { "\n--- " }, System.StringSplitOptions.None);
            var owners = new HashSet<string>();
            string Owner(string doc) => System.Text.RegularExpressions.Regex.Match(doc, @"m_GameObject: \{fileID: (\d+)\}").Groups[1].Value;
            foreach (var doc in docs) if (doc.Contains("\nTilemapCollider2D:\n")) owners.Add(Owner(doc));
            Assert.That(owners, Is.Not.Empty);
            int checkedMaps = 0;
            foreach (var doc in docs)
            {
                if (!doc.Contains("\nTilemap:\n") || !owners.Contains(Owner(doc))) continue;
                checkedMaps++;
                Assert.That(doc, Does.Not.Contain("m_AllTileFlags: 1073741825"));
            }
            Assert.That(checkedMaps, Is.EqualTo(owners.Count));
        }

        // Separate tile colliders catch a player sliding along a wall at every seam.
        [Test]
        public void Town_CollisionTilemaps_MergeIntoOneColliderAtLoad()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Town.unity");
            try
            {
                int checkedMaps = 0;
                Rootborn.Game.World.AuthoredWorldScene.MergeCollisionTilemaps(scene);
                Rootborn.Game.World.AuthoredWorldScene.MergeCollisionTilemaps(scene);
                foreach (var root in scene.GetRootGameObjects())
                foreach (var collider in root.GetComponentsInChildren<TilemapCollider2D>(true))
                {
                    checkedMaps++;
                    Assert.That(collider.compositeOperation, Is.EqualTo(Collider2D.CompositeOperation.Merge), collider.name);
                    Assert.That(collider.GetComponent<CompositeCollider2D>(), Is.Not.Null, collider.name);
                    Assert.That(collider.GetComponent<Rigidbody2D>().bodyType, Is.EqualTo(RigidbodyType2D.Static), collider.name);
                }
                Assert.That(checkedMaps, Is.GreaterThan(0));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        // Walking from the spawn point must never leave the walled area, or the player ends up in the black outside the room.
        [Test]
        public void SavedHouse_WallsEncloseTheRoom()
        {
            var scene = EditorSceneManager.OpenPreviewScene(HousePath);
            try
            {
                var walls = Find<Tilemap>(scene, "HouseCollisionTilemap");
                Assert.IsNotNull(walls);
                walls.CompressBounds();
                var bounds = walls.cellBounds;
                var start = new Vector3Int(0, -7, 0);
                Assert.That(walls.HasTile(start), Is.False, "The spawn cell is open floor.");
                var seen = new HashSet<Vector3Int> { start };
                var open = new Stack<Vector3Int>();
                open.Push(start);
                var escapes = new List<string>();
                var steps = new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
                while (open.Count > 0)
                {
                    var cell = open.Pop();
                    foreach (var step in steps)
                    {
                        var next = cell + step;
                        if (walls.HasTile(next) || !seen.Add(next)) continue;
                        if (next.x < bounds.xMin || next.x >= bounds.xMax || next.y < bounds.yMin || next.y >= bounds.yMax) { escapes.Add(cell.ToString()); continue; }
                        open.Push(next);
                    }
                }
                Assert.That(escapes, Is.Empty, "Open wall cells next to the outside.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SavedHouse_FloorAndWallsReferencePersistentTexturedSprites()
        {
            var scene = EditorSceneManager.OpenPreviewScene(HousePath);
            try
            {
                foreach (var layerName in new[] { "HouseGroundTilemap", "HouseWallTilemap" })
                {
                    var layer = Find<Tilemap>(scene, layerName);
                    Assert.IsNotNull(layer, layerName);
                    int occupied = 0;
                    foreach (var cell in layer.cellBounds.allPositionsWithin)
                    {
                        var tile = layer.GetTile(cell);
                        if (tile == null) continue;
                        occupied++;
                        AssertPersistentArt(tile, layerName + " " + cell);
                    }
                    Assert.Greater(occupied, 0, layerName + " must not pass by being empty.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void HouseRuntimeTileSet_BaseAndSelectableStylesReferencePersistentTexturedSprites()
        {
            var scene = EditorSceneManager.OpenPreviewScene(HousePath);
            try
            {
                var applier = Find<InteriorTilemapApplier>(scene, "[HouseInteriorGenerator]");
                Assert.IsNotNull(applier);
                var serialized = new SerializedObject(applier);
                var tileSet = serialized.FindProperty("_tileSet").objectReferenceValue as InteriorTileSetDefinition;
                Assert.IsNotNull(tileSet);
                AssertTileSet(tileSet);
                var stages = serialized.FindProperty("_upgradeStages");
                for (int i = 0; i < stages.arraySize; i++)
                {
                    var stage = stages.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (stage == null) continue;
                    var stageSet = new SerializedObject(stage).FindProperty("_tileSet").objectReferenceValue as InteriorTileSetDefinition;
                    if (stageSet != null) AssertTileSet(stageSet);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void AssertTileSet(InteriorTileSetDefinition tileSet)
        {
            var serialized = new SerializedObject(tileSet);
            foreach (var field in new[] { "_floor", "_wall", "_verticalWallAccent", "_door", "_window", "_collision" })
                AssertPersistentArt(serialized.FindProperty(field).objectReferenceValue as TileBase, tileSet.name + "." + field);
            var styles = serialized.FindProperty("_styleOptions");
            for (int i = 0; i < styles.arraySize; i++)
            {
                var style = styles.GetArrayElementAtIndex(i);
                foreach (var field in new[] { "_floor", "_wall", "_verticalWallAccent" })
                {
                    var value = style.FindPropertyRelative(field).objectReferenceValue as TileBase;
                    // Optional null values intentionally inherit the valid base tile.
                    if (value != null) AssertPersistentArt(value, tileSet.name + ".style[" + i + "]." + field);
                }
            }
        }

        private static void AssertPersistentArt(TileBase tileBase, string label)
        {
            Assert.IsNotNull(tileBase, label);
            var tile = tileBase as Tile;
            Assert.IsNotNull(tile, label + " must be a Unity Tile.");
            Assert.IsTrue(AssetDatabase.Contains(tile), label + " tile must be a saved asset.");
            StringAssert.EndsWith(".asset", AssetDatabase.GetAssetPath(tile), label);
            Assert.IsNotNull(tile.sprite, label + " sprite");
            Assert.IsNotNull(tile.sprite.texture, label + " texture (the previous embedded 4x4 sprites lost this reference)");
            Assert.IsTrue(AssetDatabase.Contains(tile.sprite), label + " sprite must be imported/persistent.");
            Assert.IsTrue(AssetDatabase.Contains(tile.sprite.texture), label + " texture must be persistent.");
            Assert.IsNotEmpty(AssetDatabase.GetAssetPath(tile.sprite.texture), label);
        }

        private static T Find<T>(Scene scene, string objectName) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<T>(true))
                    if (component.gameObject.name == objectName) return component;
            return null;
        }
    }
}
