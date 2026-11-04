using System;using System.Linq;using NUnit.Framework;using UnityEditor;using UnityEditor.AddressableAssets;using UnityEngine;using Rootborn.UI.Modern;
namespace Rootborn.Tests.EditMode {
public sealed class UrbanLifeIconAssetsTests {
 const string Atlas="Assets/Art/UI/UrbanMolding/urban-life-icons-atlas.png";
 static readonly string[] Keys={"journal","plan","town_map","discovery","chef","care","interior","training","time","energy","focus","money"};
 [Test] public void Atlas_SavesTwelveNativeRectsAndPixelSettings(){
  var importer=AssetImporter.GetAtPath(Atlas) as TextureImporter;Assert.That(importer,Is.Not.Null,"Approved icon atlas must be installed");
  Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(importer.mipmapEnabled,Is.False);Assert.That(importer.isReadable,Is.False);Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));Assert.That(importer.spritePixelsPerUnit,Is.EqualTo(16));
  var sprites=AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToArray();Assert.That(sprites.Length,Is.EqualTo(12));
  for(int i=0;i<Keys.Length;i++){var s=sprites.Single(x=>x.name=="urban_icon_"+Keys[i]);Assert.That(s.rect,Is.EqualTo(new Rect(4+i%4*56,256-(4+i/4*56)-48,48,48)));Assert.That(s.pivot,Is.EqualTo(new Vector2(24,24)));Assert.That(s.border,Is.EqualTo(Vector4.zero));}
 }
 [Test] public void Theme_ReferencesSavedSpritesAndSchoolWithoutReplacingMolding(){
  var theme=AssetDatabase.LoadAssetAtPath<UrbanUiThemeDefinition>("Assets/Data/UI/UrbanMoldingTheme.asset");Assert.That(theme,Is.Not.Null);Assert.That(theme.PanelSprite.name,Is.EqualTo("urban_panel"));
  var method=typeof(UrbanUiThemeDefinition).GetMethod("GetIcon");Assert.That(method,Is.Not.Null,"Theme must resolve saved icon references");
  foreach(var key in Keys){var sprite=method.Invoke(theme,new object[]{key}) as Sprite;Assert.That(sprite,Is.Not.Null,key);Assert.That(AssetDatabase.GetAssetPath(sprite),Is.EqualTo(Atlas));}
  Assert.That(method.Invoke(theme,new object[]{"school"}),Is.Not.Null);
 }
 [Test] public void IconAtlas_UsesExistingLocalSpritesAddress(){
  var settings=AddressableAssetSettingsDefaultObject.Settings;var e=settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(Atlas));Assert.That(e,Is.Not.Null);Assert.That(e.address,Is.EqualTo("sprites/ui/urban-life/icons"));Assert.That(e.parentGroup.Name,Is.EqualTo("Sprites"));
 }
 [Test] public void DefinitionPresentation_UsesSavedReferencesAndUnknownFallback(){
  var theme=AssetDatabase.LoadAssetAtPath<UrbanUiThemeDefinition>("Assets/Data/UI/UrbanMoldingTheme.asset");var method=typeof(UrbanUiThemeDefinition).GetMethod("ResolveIcon");Assert.That(method,Is.Not.Null);
  var source=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Data/Careers/Interests/Interest_Investigation.asset");Assert.That(source,Is.Not.Null);
  var icon=method.Invoke(theme,new object[]{source,"plan"}) as Sprite;Assert.That(icon,Is.Not.Null);Assert.That(icon.name,Is.EqualTo("urban_icon_discovery"));
  icon=method.Invoke(theme,new object[]{null,"plan"}) as Sprite;Assert.That(icon.name,Is.EqualTo("urban_icon_plan"));
 }
}}
