using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Game.Characters.Spum;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public readonly struct SpumAddressablesMigrationRequest
    {
        public SpumAddressablesMigrationRequest(
            string sourcePrefabPath,
            IReadOnlyList<string> sourceAnimationClipPaths,
            IReadOnlyList<string> sourcePreviewSpritePaths,
            string outputRootPath,
            string catalogId,
            string appearanceId,
            string categoryId,
            string partId,
            string displayNameKey,
            bool isDefaultPart)
        {
            SourcePrefabPath = sourcePrefabPath;
            SourceAnimationClipPaths = sourceAnimationClipPaths ?? Array.Empty<string>();
            SourcePreviewSpritePaths = sourcePreviewSpritePaths ?? Array.Empty<string>();
            OutputRootPath = outputRootPath;
            CatalogId = catalogId;
            AppearanceId = appearanceId;
            CategoryId = categoryId;
            PartId = partId;
            DisplayNameKey = displayNameKey;
            IsDefaultPart = isDefaultPart;
        }

        public string SourcePrefabPath { get; }
        public IReadOnlyList<string> SourceAnimationClipPaths { get; }
        public IReadOnlyList<string> SourcePreviewSpritePaths { get; }
        public string OutputRootPath { get; }
        public string CatalogId { get; }
        public string AppearanceId { get; }
        public string CategoryId { get; }
        public string PartId { get; }
        public string DisplayNameKey { get; }
        public bool IsDefaultPart { get; }
    }

    public readonly struct SpumAddressablesMigrationResult
    {
        public SpumAddressablesMigrationResult(
            string addressablesGroupName,
            string partAssetPath,
            string catalogAssetPath,
            string appearanceAssetPath)
        {
            AddressablesGroupName = addressablesGroupName;
            PartAssetPath = partAssetPath;
            CatalogAssetPath = catalogAssetPath;
            AppearanceAssetPath = appearanceAssetPath;
        }

        public string AddressablesGroupName { get; }
        public string PartAssetPath { get; }
        public string CatalogAssetPath { get; }
        public string AppearanceAssetPath { get; }
    }

    public static class SpumAddressablesMigrationService
    {
        public const string CharacterVisualsGroupName = "CharacterVisuals";

        private const string DataCharactersRoot = "Assets/Data/Characters";
        private const string CharacterVisualsRoot = "Assets/CharacterVisuals";
        private const string VisualKindSpum = "spum";

        public static SpumAddressablesMigrationResult Migrate(SpumAddressablesMigrationRequest request)
        {
            ValidateRequest(request);

            AddressableAssetSettings settings = EnsureSettings();
            AddressableAssetGroup group = EnsureGroup(settings, CharacterVisualsGroupName);

            string prefabAddress = RegisterAsset(settings, group, request.SourcePrefabPath, "prefabs");
            for (int i = 0; i < request.SourceAnimationClipPaths.Count; i++)
            {
                RegisterAsset(settings, group, request.SourceAnimationClipPaths[i], "animations");
            }

            for (int i = 0; i < request.SourcePreviewSpritePaths.Count; i++)
            {
                RegisterAsset(settings, group, request.SourcePreviewSpritePaths[i], "sprites");
            }

            EnsureFolderPath(request.OutputRootPath + "/Parts");
            EnsureFolderPath(request.OutputRootPath + "/Catalogs");
            EnsureFolderPath(request.OutputRootPath + "/Appearances");

            string partPath = request.OutputRootPath + "/Parts/" + ToAssetFileName(request.PartId, "SpumPart") + ".asset";
            string catalogPath = request.OutputRootPath + "/Catalogs/" + ToAssetFileName(request.CatalogId, "SpumCatalog") + ".asset";
            string appearancePath = request.OutputRootPath + "/Appearances/" + ToAssetFileName(request.AppearanceId, "SpumAppearance") + ".asset";

            SpumPartDefinition part = LoadOrCreate<SpumPartDefinition>(partPath);
            ConfigurePart(part, request, prefabAddress, ResolvePreviewSprite(request.SourcePreviewSpritePaths));

            SpumPartCatalogDefinition catalog = LoadOrCreate<SpumPartCatalogDefinition>(catalogPath);
            ConfigureCatalog(catalog, request.CatalogId, part);

            SpumAppearanceDefinition appearance = LoadOrCreate<SpumAppearanceDefinition>(appearancePath);
            ConfigureAppearance(appearance, request.AppearanceId, catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ApplyGeneratedAssetDataAfterRefresh(request, prefabAddress, partPath, catalogPath, appearancePath);
            AssetDatabase.SaveAssets();

            return new SpumAddressablesMigrationResult(CharacterVisualsGroupName, partPath, catalogPath, appearancePath);
        }

        private static void ValidateRequest(SpumAddressablesMigrationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SourcePrefabPath))
                throw new ArgumentException("Source prefab path is required.", nameof(request));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(request.SourcePrefabPath) == null)
                throw new ArgumentException("Source prefab must reference an existing prefab asset.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.OutputRootPath))
                throw new ArgumentException("Output root path is required.", nameof(request));
            if (!IsApprovedOutputRoot(request.OutputRootPath))
                throw new ArgumentException("SPUM migration output must stay under approved ROOTBORN character asset folders.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.CatalogId))
                throw new ArgumentException("Catalog id is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.AppearanceId))
                throw new ArgumentException("Appearance id is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.CategoryId))
                throw new ArgumentException("Category id is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.PartId))
                throw new ArgumentException("Part id is required.", nameof(request));
        }

        private static bool IsApprovedOutputRoot(string outputRootPath)
        {
            string normalized = NormalizeAssetPath(outputRootPath).TrimEnd('/');
            if (normalized.Contains("/../", StringComparison.Ordinal) || normalized.EndsWith("/..", StringComparison.Ordinal))
                return false;

            return IsSameOrChild(normalized, DataCharactersRoot) || IsSameOrChild(normalized, CharacterVisualsRoot);
        }

        private static bool IsSameOrChild(string path, string root)
        {
            return path == root || path.StartsWith(root + "/", StringComparison.Ordinal);
        }

        private static AddressableAssetSettings EnsureSettings()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
                return settings;

            const string folder = "Assets/AddressableAssetsData";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "AddressableAssetsData");
            }

            settings = AddressableAssetSettings.Create(folder, "AddressableAssetSettings", true, true);
            AddressableAssetSettingsDefaultObject.Settings = settings;
            return settings;
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName)
        {
            AddressableAssetGroup existing = settings.FindGroup(groupName);
            if (existing != null)
                return existing;

            AddressableAssetGroup group = settings.CreateGroup(groupName, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            BundledAssetGroupSchema bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundleSchema != null)
            {
                bundleSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundleSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                bundleSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            }

            return group;
        }

        private static string RegisterAsset(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath, string folder)
        {
            string normalizedPath = NormalizeAssetPath(assetPath);
            string guid = AssetDatabase.AssetPathToGUID(normalizedPath);
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException("Asset not found for Addressables migration: " + normalizedPath);

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            string address = "spum/" + folder + "/" + Path.GetFileNameWithoutExtension(normalizedPath);
            entry.address = address;
            return address;
        }

        private static void ApplyGeneratedAssetDataAfterRefresh(SpumAddressablesMigrationRequest request, string prefabAddress, string partPath, string catalogPath, string appearancePath)
        {
            SpumPartDefinition part = AssetDatabase.LoadAssetAtPath<SpumPartDefinition>(partPath);
            if (part == null)
                throw new InvalidOperationException("SPUM migration failed to reload generated part asset: " + partPath);

            ConfigurePart(part, request, prefabAddress, ResolvePreviewSprite(request.SourcePreviewSpritePaths));

            SpumPartCatalogDefinition catalog = AssetDatabase.LoadAssetAtPath<SpumPartCatalogDefinition>(catalogPath);
            if (catalog == null)
                throw new InvalidOperationException("SPUM migration failed to reload generated catalog asset: " + catalogPath);

            ConfigureCatalog(catalog, request.CatalogId, part);

            SpumAppearanceDefinition appearance = AssetDatabase.LoadAssetAtPath<SpumAppearanceDefinition>(appearancePath);
            if (appearance == null)
                throw new InvalidOperationException("SPUM migration failed to reload generated appearance asset: " + appearancePath);

            ConfigureAppearance(appearance, request.AppearanceId, catalog);
        }

        private static void ConfigurePart(SpumPartDefinition part, SpumAddressablesMigrationRequest request, string addressableKey, Sprite previewSprite)
        {
            part.name = request.PartId;
            var serializedObject = new SerializedObject(part);
            serializedObject.FindProperty("_stableId").stringValue = request.PartId;
            serializedObject.FindProperty("_categoryId").stringValue = request.CategoryId;
            serializedObject.FindProperty("_displayNameKey").stringValue = request.DisplayNameKey;
            serializedObject.FindProperty("_addressableKey").stringValue = addressableKey;
            serializedObject.FindProperty("_previewSprite").objectReferenceValue = previewSprite;
            serializedObject.FindProperty("_isDefault").boolValue = request.IsDefaultPart;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            part.ConfigureForTests(request.PartId, request.CategoryId, request.IsDefaultPart);
            EditorUtility.SetDirty(part);
        }

        private static void ConfigureCatalog(SpumPartCatalogDefinition catalog, string catalogId, SpumPartDefinition part)
        {
            catalog.name = catalogId;
            catalog.ConfigureForTests(catalogId, new[] { part });
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureAppearance(SpumAppearanceDefinition appearance, string appearanceId, SpumPartCatalogDefinition catalog)
        {
            appearance.name = appearanceId;
            appearance.ConfigureForTests(appearanceId, VisualKindSpum, catalog);
            EditorUtility.SetDirty(appearance);
        }

        private static Sprite ResolvePreviewSprite(IReadOnlyList<string> spritePaths)
        {
            if (spritePaths == null || spritePaths.Count == 0)
                return null;

            return AssetDatabase.LoadAssetAtPath<Sprite>(spritePaths[0]);
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
                return existing;

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureFolderPath(string path)
        {
            string normalized = NormalizeAssetPath(path).TrimEnd('/');
            string[] parts = normalized.Split('/');
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

        private static string ToAssetFileName(string value, string fallback)
        {
            string source = string.IsNullOrWhiteSpace(value) ? fallback : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                source = source.Replace(invalid, '_');
            }

            return source.Replace('/', '_').Replace('\\', '_');
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }
    }
}
