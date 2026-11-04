using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;using UnityEditor.AddressableAssets;using UnityEditor.AddressableAssets.Settings;using UnityEditor.AddressableAssets.Settings.GroupSchemas;using Rootborn.UI.Modern;
namespace Rootborn.Editor.UI {
 public static class UrbanLifeIconInstaller {
  public const string AtlasPath="Assets/Art/UI/UrbanMolding/urban-life-icons-atlas.png";
  const string ThemePath="Assets/Data/UI/UrbanMoldingTheme.asset";
  [Serializable] private sealed class Manifest {public Icon[] icons;}
  [Serializable] private sealed class Icon {public string name;public int[] unity_rect_bottom_left;}
  [Serializable] private sealed class Bindings {public Binding[] entries;}
  [Serializable] private sealed class Binding {public string asset;public string key;}
  [MenuItem("Rootborn/UI/Install Approved Urban Life Icons")]
  public static void Install(){
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Install icons outside PlayMode");
   var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("output/design-comfy-20261028/asset-manifest.json"));
   if(manifest.icons==null||manifest.icons.Length!=12)throw new InvalidOperationException("Exactly twelve approved icons required");
   var bindings=JsonUtility.FromJson<Bindings>(File.ReadAllText("output/urban-ui-20261028-integration/presentation-bindings.json"));
   if(manifest.icons.Any(i=>i==null||string.IsNullOrEmpty(i.name)||i.unity_rect_bottom_left==null||i.unity_rect_bottom_left.Length!=4))throw new InvalidOperationException("Malformed icon entry");
   var keys=manifest.icons.Select(i=>i.name).ToArray();
   if(keys.Distinct(StringComparer.Ordinal).Count()!=12||manifest.icons.Any(i=>i.unity_rect_bottom_left[0]<0||i.unity_rect_bottom_left[1]<0||i.unity_rect_bottom_left[2]!=48||i.unity_rect_bottom_left[3]!=48||i.unity_rect_bottom_left[0]+48>256||i.unity_rect_bottom_left[1]+48>256))throw new InvalidOperationException("Icon names and rects must be unique and bounded native 48px sprites");
   if(bindings.entries==null||bindings.entries.Any(b=>b==null||string.IsNullOrEmpty(b.asset)||!keys.Contains(b.key)))throw new InvalidOperationException("Presentation keys must reference approved icons");
   var settings=AddressableAssetSettingsDefaultObject.Settings;var group=settings!=null?settings.FindGroup("Sprites"):null;
   var schema=group!=null?group.GetSchema<BundledAssetGroupSchema>():null;
   if(schema==null||schema.BuildPath.Id!=settings.profileSettings.GetProfileDataByName("Local.BuildPath").Id||schema.LoadPath.Id!=settings.profileSettings.GetProfileDataByName("Local.LoadPath").Id)throw new InvalidOperationException("Existing local Sprites group required");
   var theme=AssetDatabase.LoadAssetAtPath<UrbanUiThemeDefinition>(ThemePath);if(theme==null)throw new InvalidOperationException("Approved molding theme missing");
   var collision=settings.groups.Where(g=>g!=null).SelectMany(g=>g.entries).FirstOrDefault(e=>e.address==UrbanUiThemeDefinition.IconAtlasAddress&&e.AssetPath!=AtlasPath);if(collision!=null)throw new InvalidOperationException("Icon address already used by "+collision.AssetPath);
   var school=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/SchoolPixels/sprites/school.png");if(school==null)throw new InvalidOperationException("Existing school sprite missing");
   var targets=bindings.entries.Select(b=>AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(b.asset)).ToArray();if(targets.Any(x=>x==null))throw new InvalidOperationException("Presentation definition missing; no partial mapping saved");
   string backup="output/urban-ui-20261028-integration/backups/icons-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
   var previous=settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(AtlasPath))?.parentGroup;
   var paths=new[]{AtlasPath,ThemePath,AssetDatabase.GetAssetPath(settings),AssetDatabase.GetAssetPath(group)}.ToList();if(previous!=null&&previous!=group)paths.Add(AssetDatabase.GetAssetPath(previous));
   foreach(var path in paths)foreach(var file in new[]{path,path+".meta"})if(File.Exists(file)){string dest=Path.Combine(backup,file);Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(file,dest,false);}
   File.Copy("output/design-comfy-20261028/urban-life-icons-atlas.png",AtlasPath,true);AssetDatabase.ImportAsset(AtlasPath,ImportAssetOptions.ForceSynchronousImport);
   var importer=(TextureImporter)AssetImporter.GetAtPath(AtlasPath);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=16;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.isReadable=false;importer.maxTextureSize=256;
   var ts=new TextureImporterSettings();importer.ReadTextureSettings(ts);ts.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(ts);
   importer.spritesheet=manifest.icons.Select(i=>new SpriteMetaData{name="urban_icon_"+i.name,rect=new Rect(i.unity_rect_bottom_left[0],i.unity_rect_bottom_left[1],48,48),border=Vector4.zero,pivot=Vector2.one*.5f,alignment=(int)SpriteAlignment.Center}).ToArray();importer.SaveAndReimport();
   var sprites=AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToArray();if(sprites.Length!=12)throw new InvalidOperationException("Icon slices did not persist");
   var so=new SerializedObject(theme);var icons=so.FindProperty("_icons");icons.arraySize=13;
   for(int i=0;i<12;i++){var p=icons.GetArrayElementAtIndex(i);p.FindPropertyRelative("_key").stringValue=manifest.icons[i].name;p.FindPropertyRelative("_sprite").objectReferenceValue=sprites.Single(s=>s.name=="urban_icon_"+manifest.icons[i].name);}
   var schoolEntry=icons.GetArrayElementAtIndex(12);schoolEntry.FindPropertyRelative("_key").stringValue="school";schoolEntry.FindPropertyRelative("_sprite").objectReferenceValue=school;
   var presentations=so.FindProperty("_presentations");presentations.arraySize=bindings.entries.Length;
   for(int i=0;i<bindings.entries.Length;i++){var p=presentations.GetArrayElementAtIndex(i);p.FindPropertyRelative("_definition").objectReferenceValue=targets[i];p.FindPropertyRelative("_iconKey").stringValue=bindings.entries[i].key;}
   so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(theme);AssetDatabase.SaveAssetIfDirty(theme);
   var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AtlasPath),group);entry.address=UrbanUiThemeDefinition.IconAtlasAddress;EditorUtility.SetDirty(group);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(group);AssetDatabase.SaveAssetIfDirty(settings);
   if(previous!=null&&previous!=group){EditorUtility.SetDirty(previous);AssetDatabase.SaveAssetIfDirty(previous);}
   Debug.Log("[ROOTBORN] Approved urban life icons saved; "+targets.Length+" definition bindings. Backup "+backup);
  }
 }
}
