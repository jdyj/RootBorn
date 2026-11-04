using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    /// <summary>Imports the character pack from pack.json. Safe to re-run: slices, sheets and registry entries are updated in place.</summary>
    public static class GrowingCharacterPackImporter
    {
        public const string ArtFolder = "Assets/Art/Characters/Growing";
        public const string DataFolder = "Assets/Data/Characters/Growing";
        private static readonly string[] RegistryPaths = { "Assets/Data/Registry/GameDataRegistry.asset", "Assets/Resources/GameDataRegistry.asset" };

        [Serializable]
        private sealed class PackManifest
        {
            public int ppu = 48;
            public SheetEntry[] sheets = Array.Empty<SheetEntry>();
            public AppearanceEntry[] appearances = Array.Empty<AppearanceEntry>();
        }

        [Serializable]
        private sealed class SheetEntry
        {
            public string id;
            public string file;
            public int cellWidth;
            public int cellHeight;
        }

        [Serializable]
        private sealed class AppearanceEntry
        {
            public string id;
            public string body;
            public string head;
            public string outfit;
            public int minimumStudentDay = 1;
            public bool requiresAdult;
            public string facilityId;
            public string displayNameKey;
        }

        /// <summary>Frame rectangles in sheet order. Unity's rect origin is bottom-left; rows run top to bottom.</summary>
        public static Rect[] BuildFrameRects(int textureHeight, int cellWidth, int cellHeight)
        {
            var rects = new Rect[GrowingCharacterSheetDefinition.FrameCount];
            for (int row = 0; row < GrowingCharacterSheetDefinition.Rows; row++)
            {
                for (int column = 0; column < GrowingCharacterSheetDefinition.Columns; column++)
                {
                    rects[row * GrowingCharacterSheetDefinition.Columns + column] = new Rect(column * cellWidth, textureHeight - (row + 1) * cellHeight, cellWidth, cellHeight);
                }
            }

            return rects;
        }

        public static string FrameName(string sheetId, int index) => sheetId + "_" + index.ToString("00");

        [MenuItem("Rootborn/Characters/Import Growing Character Pack")]
        public static void Import()
        {
            string manifestPath = ArtFolder + "/pack.json";
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[ROOTBORN] Missing character pack manifest: " + manifestPath);
                return;
            }

            var manifest = JsonUtility.FromJson<PackManifest>(File.ReadAllText(manifestPath));
            AssetDatabase.Refresh();
            EnsureFolder(DataFolder);
            var sheets = new Dictionary<string, GrowingCharacterSheetDefinition>();
            foreach (var entry in manifest.sheets)
            {
                sheets[entry.id] = ImportSheet(entry, manifest.ppu);
            }

            var prefab = BuildVisualPrefab();
            var appearances = new List<CharacterAppearanceDefinition>();
            foreach (var entry in manifest.appearances)
            {
                appearances.Add(ImportAppearance(entry, sheets, prefab));
            }

            AddNetworkSyncToPlayerPrefab();

            foreach (string path in RegistryPaths)
            {
                Register(path, appearances);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ROOTBORN] Character pack imported: " + manifest.sheets.Length + " sheets, " + appearances.Count + " appearances.");
        }

        private static GrowingCharacterSheetDefinition ImportSheet(SheetEntry entry, int ppu)
        {
            string texturePath = ArtFolder + "/" + entry.file;
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            int textureHeight = entry.cellHeight * GrowingCharacterSheetDefinition.Rows;
            var rects = BuildFrameRects(textureHeight, entry.cellWidth, entry.cellHeight);
            var metas = new SpriteMetaData[rects.Length];
            for (int i = 0; i < rects.Length; i++)
            {
                metas[i] = new SpriteMetaData
                {
                    name = FrameName(entry.id, i),
                    rect = rects[i],
                    alignment = (int)SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                };
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.spritesheet = metas;
            importer.SaveAndReimport();

            var byName = new Dictionary<string, Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(texturePath))
            {
                if (asset is Sprite sprite) byName[sprite.name] = sprite;
            }

            var frames = new Sprite[rects.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                byName.TryGetValue(FrameName(entry.id, i), out frames[i]);
            }

            string assetPath = DataFolder + "/Sheet_" + entry.id + ".asset";
            var sheet = LoadOrCreate<GrowingCharacterSheetDefinition>(assetPath);
            var serialized = new SerializedObject(sheet);
            serialized.FindProperty("_id").stringValue = entry.id;
            var frameProperty = serialized.FindProperty("_frames");
            frameProperty.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                frameProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return sheet;
        }

        private static GrowingCharacterAppearanceDefinition ImportAppearance(AppearanceEntry entry, Dictionary<string, GrowingCharacterSheetDefinition> sheets, GrowingCharacterVisualView prefab)
        {
            var appearance = LoadOrCreate<GrowingCharacterAppearanceDefinition>(DataFolder + "/Appearance_" + entry.id + ".asset");
            var serialized = new SerializedObject(appearance);
            serialized.FindProperty("_id").stringValue = entry.id;
            serialized.FindProperty("_visualKind").stringValue = GrowingCharacterAppearanceDefinition.VisualKindName;
            serialized.FindProperty("_body").objectReferenceValue = sheets[entry.body];
            serialized.FindProperty("_head").objectReferenceValue = sheets[entry.head];
            serialized.FindProperty("_outfit").objectReferenceValue = sheets[entry.outfit];
            serialized.FindProperty("_displayNameKey").stringValue = entry.displayNameKey ?? string.Empty;
            serialized.FindProperty("_visualPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("_minimumStudentDay").intValue = entry.minimumStudentDay;
            serialized.FindProperty("_requiresAdult").boolValue = entry.requiresAdult;
            serialized.FindProperty("_facilityId").stringValue = entry.facilityId ?? string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return appearance;
        }

        private static GrowingCharacterVisualView BuildVisualPrefab()
        {
            EnsureFolder(Path.GetDirectoryName(GrowingCharacterRuntimeInstaller.PrefabPath).Replace('\\', '/'));
            var root = new GameObject(GrowingCharacterRuntimeInstaller.VisualRootName);
            try
            {
                var view = root.AddComponent<GrowingCharacterVisualView>();
                view.WireForPrefab(Layer(root, GrowingCharacterVisualView.BodyLayerName, 0), Layer(root, GrowingCharacterVisualView.OutfitLayerName, 1), Layer(root, GrowingCharacterVisualView.HeadLayerName, 2));
                return PrefabUtility.SaveAsPrefabAsset(root, GrowingCharacterRuntimeInstaller.PrefabPath).GetComponent<GrowingCharacterVisualView>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static SpriteRenderer Layer(GameObject root, string layerName, int order)
        {
            var layer = new GameObject(layerName);
            layer.transform.SetParent(root.transform, false);
            var layerRenderer = layer.AddComponent<SpriteRenderer>();
            layerRenderer.sortingOrder = order;
            return layerRenderer;
        }

        private static void AddNetworkSyncToPlayerPrefab()
        {
            const string playerPath = "Assets/Prefabs/Player.prefab";
            var contents = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                if (contents.GetComponent<Rootborn.Network.Characters.GrowingCharacterNetworkAppearanceSync>() != null) return;
                contents.AddComponent<Rootborn.Network.Characters.GrowingCharacterNetworkAppearanceSync>();
                PrefabUtility.SaveAsPrefabAsset(contents, playerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void Register(string registryPath, List<CharacterAppearanceDefinition> appearances)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(registryPath);
            if (registry == null) return;
            var serialized = new SerializedObject(registry);
            var property = serialized.FindProperty("_characterAppearances");
            // Growing appearances come first in pack order so the default is deterministic; others keep their order.
            var kept = new List<UnityEngine.Object>();
            for (int i = 0; i < property.arraySize; i++)
            {
                var existing = property.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null && !(existing is GrowingCharacterAppearanceDefinition)) kept.Add(existing);
            }

            property.arraySize = appearances.Count + kept.Count;
            for (int i = 0; i < appearances.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = appearances[i];
            }

            for (int i = 0; i < kept.Count; i++)
            {
                property.GetArrayElementAtIndex(appearances.Count + i).objectReferenceValue = kept[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
