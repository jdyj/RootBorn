using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using Rootborn.Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rootborn.Tests.EditMode.Placement
{
    public sealed class SchoolPixelAssetIntegrationTests
    {
        private const string ArtRoot = "Assets/Art/SchoolPixels";
        private const string DataRoot = "Assets/Data/Interiors/Furniture/SchoolPixels";

        [Test]
        public void SchoolAssets_AllHaveUniqueLocalAddressableEntries()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            Assert.IsNotNull(settings);
            var paths = AssetDatabase.FindAssets("", new[] { ArtRoot, DataRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png") || p.EndsWith(".asset")).ToArray();
            Assert.GreaterOrEqual(paths.Length, 58);
            foreach (var path in paths)
            {
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                Assert.IsNotNull(entry, path + " must be independently addressable.");
                Assert.IsTrue(entry.labels.Contains("SchoolPixels"), path);
                var expectedGroup = (path.EndsWith(".png") || AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) ? "Sprites" : AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path) != null ? "Data" : "Tiles";
                Assert.AreEqual(expectedGroup, entry.parentGroup.Name, path);
                Assert.AreEqual(1, settings.groups.Where(g => g != null).SelectMany(g => g.entries).Count(e => e.address == entry.address), entry.address);
                var schema = entry.parentGroup.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>();
                StringAssert.Contains("RuntimePath", schema.LoadPath.GetValue(settings), entry.address);
            }
            var registry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID("Assets/Data/Registry/GameDataRegistry.asset"));
            Assert.AreEqual("data/registry", registry.address);
        }

        [Test]
        public void GroundDepthSort_UsesFeetRatherThanCharacterCenter()
        {
            var type = typeof(PlayerController).Assembly.GetType("Rootborn.Game.Interiors.GroundDepthSort");
            Assert.IsNotNull(type, "Ground-depth sorting must exist before testing tall furniture occlusion.");
            var go = new GameObject("DepthSortTest", typeof(SortingGroup));
            try
            {
                var component = go.AddComponent(type);
                var serialized = new SerializedObject(component);
                serialized.FindProperty("_footPointOffset").vector2Value = new Vector2(0f, -0.5f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                go.transform.position = new Vector3(0, 1, 0);
                type.GetMethod("RefreshSorting").Invoke(component, null);
                Assert.AreEqual(-24, go.GetComponent<SortingGroup>().sortingOrder);
                go.transform.position = new Vector3(0, 0, 0);
                type.GetMethod("RefreshSorting").Invoke(component, null);
                Assert.AreEqual(24, go.GetComponent<SortingGroup>().sortingOrder);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SchoolSprites_KeepNativePixelsAndExplicitPivots()
        {
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".png")).ToArray();
            Assert.GreaterOrEqual(paths.Length, 35);
            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.AreEqual(48f, importer.spritePixelsPerUnit, path);
                importer.GetSourceTextureWidthAndHeight(out var w, out var h);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, path);
                Assert.AreEqual(new Vector2(w, h), sprite.rect.size, path);
                if (path.Contains("/Furniture48/")) Assert.AreEqual(new Vector2(24, 24), sprite.pivot, path);
            }
        }

        [Test]
        public void SchoolFurniture_IsRegisteredWithFootprintsIndependentOfSpriteHeight()
        {
            var definitions = AssetDatabase.FindAssets("t:FurnitureDefinition", new[] { DataRoot })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<FurnitureDefinition>).ToArray();
            Assert.GreaterOrEqual(definitions.Length, 11);
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            foreach (var definition in definitions)
            {
                CollectionAssert.Contains(registry.FurnitureDefinitions, definition);
                StringAssert.StartsWith("school.", definition.StableId);
                Assert.AreEqual(1, definition.TileParts.Count);
                Assert.IsTrue(definition.FootprintCells.All(cell => cell.y == 0), definition.StableId);
                Assert.IsTrue(definition.BlocksMovement);
            }
            Assert.AreEqual(2, definitions.Single(d => d.StableId == "school.bookcase").FootprintCells.Count);
            Assert.AreEqual(1, definitions.Single(d => d.StableId == "school.lectern").FootprintCells.Count);
        }

        [Test]
        public void SavedSchoolScene_HasRealPlayerAndSeparateFloorColliders()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/ArtReview/SchoolPixelReview.unity", OpenSceneMode.Single);
                Assert.IsTrue(scene.isLoaded);
                Assert.IsNotNull(Object.FindFirstObjectByType<PlayerController>());
                var desk = GameObject.Find("desk-1-1");
                Assert.IsNotNull(desk);
                Assert.IsNotNull(desk.GetComponent<BoxCollider2D>());
                Assert.Less(desk.GetComponent<BoxCollider2D>().size.y, desk.GetComponent<SpriteRenderer>().bounds.size.y);
                Assert.IsNull(GameObject.Find("board").GetComponent<Collider2D>());
                Assert.IsNull(GameObject.Find("book-desk-1-1").GetComponent<Collider2D>());
                Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"));
                Assert.AreEqual("desk", desk.GetComponent<SpriteRenderer>().sprite.name);
                Assert.IsFalse(scene.isDirty);
            }
            finally
            {
                if (previous.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }
}
