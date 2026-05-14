using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using Rootborn.Game.Characters.Spum;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters.Spum
{
    public sealed class SpumAddressablesMigrationTests
    {
        private const string TempRoot = "Assets/Tests/EditMode/Characters/Spum/GeneratedMigrationTemp";
        private const string SourceRoot = TempRoot + "/Source";
        private const string OutputRoot = "Assets/Data/Characters/SpumMigrationTests";

        private readonly List<string> _addressableAssetPaths = new List<string>();
        private bool _createdCharacterVisualsGroup;

        [SetUp]
        public void SetUp()
        {
            _addressableAssetPaths.Clear();
            _createdCharacterVisualsGroup = AddressableAssetSettingsDefaultObject.Settings == null ||
                AddressableAssetSettingsDefaultObject.Settings.FindGroup("CharacterVisuals") == null;
            DeleteIfExists(TempRoot);
            DeleteIfExists(OutputRoot);
            CreateFolderPath(SourceRoot);
            CreateFolderPath(OutputRoot);
        }

        [TearDown]
        public void TearDown()
        {
            CleanupAddressablesEntries();
            DeleteIfExists(TempRoot);
            DeleteIfExists(OutputRoot);
        }

        [Test]
        public void SPUM_ADDRESSABLES_MIGRATION_001_RegistersSourceAssetsInCharacterVisualsGroup()
        {
            string prefabPath = CreatePrefab(SourceRoot + "/Student.prefab");
            string clipPath = CreateAnimationClip(SourceRoot + "/StudentIdle.anim");
            string spritePath = CreateSprite(SourceRoot + "/StudentPreview.png");

            var request = new SpumAddressablesMigrationRequest(
                sourcePrefabPath: prefabPath,
                sourceAnimationClipPaths: new[] { clipPath },
                sourcePreviewSpritePaths: new[] { spritePath },
                outputRootPath: OutputRoot,
                catalogId: "spum.catalog.test.student",
                appearanceId: "appearance.spum.test.student",
                categoryId: "body",
                partId: "spum.body.test.default",
                displayNameKey: "character.spum.test.body",
                isDefaultPart: true);

            SpumAddressablesMigrationResult result = SpumAddressablesMigrationService.Migrate(request);

            Assert.AreEqual("CharacterVisuals", result.AddressablesGroupName);
            AssertAddressableEntry(prefabPath, "spum/prefabs/Student");
            AssertAddressableEntry(clipPath, "spum/animations/StudentIdle");
            AssertAddressableEntry(spritePath, "spum/sprites/StudentPreview");
        }

        [Test]
        public void SPUM_ADDRESSABLES_MIGRATION_002_CreatesPartCatalogAndAppearanceAssets()
        {
            string prefabPath = CreatePrefab(SourceRoot + "/Student.prefab");
            string clipPath = CreateAnimationClip(SourceRoot + "/StudentIdle.anim");
            string spritePath = CreateSprite(SourceRoot + "/StudentPreview.png");
            var request = new SpumAddressablesMigrationRequest(
                prefabPath,
                new[] { clipPath },
                new[] { spritePath },
                OutputRoot,
                "spum.catalog.test.student",
                "appearance.spum.test.student",
                "body",
                "spum.body.test.default",
                "character.spum.test.body",
                true);

            SpumAddressablesMigrationResult result = SpumAddressablesMigrationService.Migrate(request);

            var part = AssetDatabase.LoadAssetAtPath<SpumPartDefinition>(result.PartAssetPath);
            var catalog = AssetDatabase.LoadAssetAtPath<SpumPartCatalogDefinition>(result.CatalogAssetPath);
            var appearance = AssetDatabase.LoadAssetAtPath<SpumAppearanceDefinition>(result.AppearanceAssetPath);

            Assert.IsNotNull(part);
            Assert.IsNotNull(catalog);
            Assert.IsNotNull(appearance);
            Assert.AreEqual("spum.body.test.default", part.StableId);
            Assert.AreEqual("body", part.CategoryId);
            Assert.IsTrue(part.IsDefault);
            Assert.AreEqual("spum.catalog.test.student", catalog.Id);
            CollectionAssert.Contains(catalog.Parts, part);
            Assert.AreEqual("appearance.spum.test.student", appearance.Id);
            Assert.AreEqual("spum", appearance.VisualKind);
            Assert.AreSame(catalog, appearance.Catalog);
        }

        [Test]
        public void SPUM_ADDRESSABLES_MIGRATION_003_RejectsOutputPathOutsideApprovedRootbornFolders()
        {
            string prefabPath = CreatePrefab(SourceRoot + "/Student.prefab");
            var request = new SpumAddressablesMigrationRequest(
                prefabPath,
                Array.Empty<string>(),
                Array.Empty<string>(),
                TempRoot + "/OutsideApprovedRoots",
                "spum.catalog.test.student",
                "appearance.spum.test.student",
                "body",
                "spum.body.test.default",
                "character.spum.test.body",
                true);

            Assert.Throws<ArgumentException>(() => SpumAddressablesMigrationService.Migrate(request));
        }

        private void AssertAddressableEntry(string assetPath, string expectedAddress)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.IsNotNull(settings, "Addressables settings must exist after migration.");
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.FindAssetEntry(guid);
            Assert.IsNotNull(entry, "Expected addressable entry for " + assetPath);
            Assert.AreEqual(expectedAddress, entry.address);
            Assert.AreEqual("CharacterVisuals", entry.parentGroup.Name);
        }

        private string CreatePrefab(string path)
        {
            var gameObject = new GameObject("Student");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(gameObject, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            _addressableAssetPaths.Add(path);
            return path;
        }

        private string CreateAnimationClip(string path)
        {
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            _addressableAssetPaths.Add(path);
            return path;
        }

        private string CreateSprite(string path)
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
            _addressableAssetPaths.Add(path);
            return path;
        }

        private void CleanupAddressablesEntries()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            var group = settings.FindGroup("CharacterVisuals");
            if (group == null)
                return;

            if (_createdCharacterVisualsGroup)
            {
                settings.RemoveGroup(group);
            }
            else
            {
                for (int i = 0; i < _addressableAssetPaths.Count; i++)
                {
                    string guid = AssetDatabase.AssetPathToGUID(_addressableAssetPaths[i]);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        settings.RemoveAssetEntry(guid);
                    }
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void CreateFolderPath(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
