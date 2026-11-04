using System;
using System.IO;
using System.Linq;
using Rootborn.UI.Modern;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Rootborn.Editor.UI
{
    public static class UrbanMoldingInstaller
    {
        public const string AtlasPath = "Assets/Art/UI/UrbanMolding/urban-molding-atlas.png";
        public const string ThemePath = "Assets/Data/UI/UrbanMoldingTheme.asset";
        private const string SourcePath = "output/ui-design-comfy-v5/skin/urban-molding-atlas.png";
        private static string _backupRoot;

        [MenuItem("Rootborn/UI/Install Approved Urban Molding")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install Urban Molding outside PlayMode.");
            if (!File.Exists(SourcePath)) throw new FileNotFoundException("Approved atlas missing", SourcePath);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Existing Addressables settings required.");
            var data = RequireLocalGroup(settings, "Data");
            var sprites = RequireLocalGroup(settings, "Sprites");
            RejectAddressCollision(settings, UrbanUiThemeDefinition.Address, ThemePath);
            RejectAddressCollision(settings, UrbanUiThemeDefinition.AtlasAddress, AtlasPath);
            _backupRoot = "output/urban-ui-integration/backups/installer-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
            Backup(AtlasPath); Backup(ThemePath);
            Backup(AssetDatabase.GetAssetPath(settings));
            Backup(AssetDatabase.GetAssetPath(data)); Backup(AssetDatabase.GetAssetPath(sprites));
            BackupPreviousGroup(settings, AtlasPath); BackupPreviousGroup(settings, ThemePath);
            EnsureFolder("Assets/Art/UI/UrbanMolding");
            EnsureFolder("Assets/Data/UI");
            File.Copy(SourcePath, AtlasPath, true);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 256;
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
#pragma warning disable CS0618
            importer.spritesheet = new[]
            {
                Slice("urban_panel", new Rect(8,152,96,96),18),
                Slice("urban_button",new Rect(112,224,96,24),6),
                Slice("urban_slot",new Rect(112,160,48,48),6)
            };
#pragma warning restore CS0618
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (texture.width != 256 || texture.height != 256)
                throw new InvalidOperationException("Approved atlas must import as 256x256.");
            var subSprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToArray();
            if (subSprites.Length != 3) throw new InvalidOperationException("Expected exactly three atlas sprites.");
            var theme = AssetDatabase.LoadAssetAtPath<UrbanUiThemeDefinition>(ThemePath);
            if (theme == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ThemePath) != null)
                    throw new InvalidOperationException("Theme path already contains an unrelated asset.");
                theme = ScriptableObject.CreateInstance<UrbanUiThemeDefinition>();
                AssetDatabase.CreateAsset(theme,ThemePath);
            }
            var serialized = new SerializedObject(theme);
            SetSprite(serialized,"_panelSprite", subSprites.Single(s => s.name == "urban_panel"));
            SetSprite(serialized,"_buttonSprite",subSprites.Single(s => s.name == "urban_button"));
            SetSprite(serialized,"_slotSprite",subSprites.Single(s => s.name == "urban_slot"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssetIfDirty(theme);
            Register(settings, data, ThemePath, UrbanUiThemeDefinition.Address);
            Register(settings, sprites, AtlasPath, UrbanUiThemeDefinition.AtlasAddress);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            AssetDatabase.SaveAssetIfDirty(data);
            AssetDatabase.SaveAssetIfDirty(sprites);
            Debug.Log("[ROOTBORN] Urban Molding installed. Backup: " + _backupRoot);
        }

        private static SpriteMetaData Slice(string name, Rect rect, float border)
        {
            return new SpriteMetaData { name=name, rect=rect, border=Vector4.one*border,
                pivot=new Vector2(.5f,.5f), alignment=(int)SpriteAlignment.Center };
        }
        private static void SetSprite(SerializedObject theme,string field,Sprite sprite)
        {
            var property=theme.FindProperty(field);
            if(property==null) throw new InvalidOperationException("Missing theme field: "+field);
            property.objectReferenceValue=sprite;
        }
        private static AddressableAssetGroup RequireLocalGroup(AddressableAssetSettings settings,string name)
        {
            var group=settings.FindGroup(name);
            var schema=group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
            if(schema==null || schema.BuildPath.Id!=settings.profileSettings.GetProfileDataByName("Local.BuildPath").Id
                || schema.LoadPath.Id!=settings.profileSettings.GetProfileDataByName("Local.LoadPath").Id)
                throw new InvalidOperationException("Existing local-only "+name+" group required; no broad rewiring performed.");
            return group;
        }
        private static void RejectAddressCollision(AddressableAssetSettings settings,string address,string path)
        {
            foreach(var group in settings.groups)
            {
                if(group==null) continue;
                foreach(var entry in group.entries)
                    if(entry.address==address && entry.AssetPath!=path)
                        throw new InvalidOperationException("Address already belongs to "+entry.AssetPath+": "+address);
            }
        }
        private static void Register(AddressableAssetSettings settings,AddressableAssetGroup group,string path,string address)
        {
            var guid=AssetDatabase.AssetPathToGUID(path);
            var previous=settings.FindAssetEntry(guid)?.parentGroup;
            var entry=settings.CreateOrMoveEntry(guid,group);
            entry.address=address;
            EditorUtility.SetDirty(group);
            if(previous!=null && previous!=group)
            {
                EditorUtility.SetDirty(previous);
                AssetDatabase.SaveAssetIfDirty(previous);
            }
        }
        private static void BackupPreviousGroup(AddressableAssetSettings settings,string path)
        {
            var entry=settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            if(entry!=null) Backup(AssetDatabase.GetAssetPath(entry.parentGroup));
        }
        private static void Backup(string path)
        {
            foreach(var file in new[]{path,path+".meta"})
            {
                if(!File.Exists(file)) continue;
                var destination=Path.Combine(_backupRoot,file);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if(!File.Exists(destination)) File.Copy(file,destination);
            }
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            var parent=Path.GetDirectoryName(path).Replace('\\','/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}

