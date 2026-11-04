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
