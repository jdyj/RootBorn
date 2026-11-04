using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class UrbanMoldingAssetTests
    {
        private const string Atlas = "Assets/Art/UI/UrbanMolding/urban-molding-atlas.png";
        private const string Theme = "Assets/Data/UI/UrbanMoldingTheme.asset";

        [Test]
        public void Atlas_HasPixelArtImportSettings()
        {
            var importer = AssetImporter.GetAtPath(Atlas) as TextureImporter;
            Assert.That(importer, Is.Not.Null, "Approved atlas must be installed");
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Atlas);
            Assert.That(texture.width, Is.EqualTo(256));
            Assert.That(texture.height, Is.EqualTo(256));
        }

        [TestCase("urban_panel", 8, 152, 96, 96, 18)]
        [TestCase("urban_button", 112, 224, 96, 24, 6)]
        [TestCase("urban_slot", 112, 160, 48, 48, 6)]
        public void Sprites_HaveApprovedRectsBordersAndPivots(string name, int x, int y, int w, int h, int border)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToArray();
            Assert.That(sprites.Length, Is.EqualTo(3));
            var sprite = sprites.Single(s => s.name == name);
            Assert.That(sprite.rect, Is.EqualTo(new Rect(x,y,w,h)));
            Assert.That(sprite.border, Is.EqualTo(Vector4.one * border));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(w/2f,h/2f)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16));
        }

        [Test]
        public void Theme_SavesAllThreeAtlasReferences()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Rootborn.UI.Modern.UrbanUiThemeDefinition")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Theme type must exist");
            var theme = AssetDatabase.LoadAssetAtPath(Theme,type);
            Assert.That(theme, Is.Not.Null);
            var so = new SerializedObject(theme);
            string[] fields = {"_panelSprite","_buttonSprite","_slotSprite"};
            string[] names = {"urban_panel","urban_button","urban_slot"};
            for (int i=0; i<fields.Length; i++)
            {
                var property = so.FindProperty(fields[i]);
                Assert.That(property, Is.Not.Null, fields[i]);
                var sprite = property.objectReferenceValue as Sprite;
                Assert.That(sprite, Is.Not.Null, fields[i]);
                Assert.That(sprite.name, Is.EqualTo(names[i]));
                Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(Atlas));
            }
        }

        [TestCase(Theme,"data/ui/urban-molding","Data")]
        [TestCase(Atlas,"sprites/ui/urban-molding/atlas","Sprites")]
        public void Assets_AreRegisteredInExistingLocalGroups(string path,string address,string groupName)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null, address);
            Assert.That(entry.address, Is.EqualTo(address));
            Assert.That(entry.parentGroup.Name, Is.EqualTo(groupName));
            var schema = entry.parentGroup.GetSchema<BundledAssetGroupSchema>();
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.BuildPath.Id, Is.EqualTo(settings.profileSettings.GetProfileDataByName("Local.BuildPath").Id));
            Assert.That(schema.LoadPath.Id, Is.EqualTo(settings.profileSettings.GetProfileDataByName("Local.LoadPath").Id));
        }
    }
}

