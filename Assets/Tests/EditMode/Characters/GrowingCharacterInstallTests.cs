using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters
{
    public sealed class GrowingCharacterInstallTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _objects) Object.DestroyImmediate(item);
            _objects.Clear();
        }

        private GrowingCharacterSheetDefinition ValidSheet()
        {
            var sheet = ScriptableObject.CreateInstance<GrowingCharacterSheetDefinition>();
            _objects.Add(sheet);
            sheet.ConfigureForTests("sheet", new Sprite[GrowingCharacterSheetDefinition.FrameCount]);
            return sheet;
        }

        private GameDataRegistry Registry(params CharacterAppearanceDefinition[] appearances)
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            _objects.Add(registry);
            typeof(GameDataRegistry).GetField("_characterAppearances", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(registry, appearances);
            return registry;
        }

        private GrowingCharacterAppearanceDefinition Appearance()
        {
            var appearance = ScriptableObject.CreateInstance<GrowingCharacterAppearanceDefinition>();
            _objects.Add(appearance);
            appearance.ConfigureForTests("fixture", ValidSheet(), ValidSheet(), ValidSheet());
            return appearance;
        }

        [Test]
        public void GROW_IMPORT_001_FrameRectsRunTopRowFirstWithBottomLeftOrigin()
        {
            var rects = GrowingCharacterPackImporter.BuildFrameRects(236, 32, 59);
            Assert.That(rects.Length, Is.EqualTo(24));
            Assert.That(rects[0], Is.EqualTo(new Rect(0, 177, 32, 59)));
            Assert.That(rects[7], Is.EqualTo(new Rect(32, 118, 32, 59)));
            Assert.That(rects[23], Is.EqualTo(new Rect(160, 0, 32, 59)));
        }

        [Test]
        public void GROW_INSTALL_001_NoGrowingAppearanceLeavesPlayerAlone()
        {
            var player = new GameObject("player");
            _objects.Add(player);
            Assert.That(GrowingCharacterRuntimeInstaller.TryInstall(player, Registry()), Is.False);
            Assert.That(player.GetComponentInChildren<GrowingCharacterVisualView>(true), Is.Null);
        }

        [Test]
        public void GROW_INSTALL_002_InstallHidesOldVisualsAndBindsAdapter()
        {
            var player = new GameObject("player", typeof(SpriteRenderer));
            _objects.Add(player);
            var oldPart = new GameObject("Part_hair", typeof(SpriteRenderer));
            oldPart.transform.SetParent(player.transform);
            Assert.That(GrowingCharacterRuntimeInstaller.TryInstall(player, Registry(Appearance())), Is.True);
            Assert.That(player.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(oldPart.GetComponent<SpriteRenderer>().enabled, Is.False);
            var view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
            Assert.That(view, Is.Not.Null);
            Assert.That(view.BodyRenderer.enabled, Is.True);
            Assert.That(player.GetComponent<PlayerCharacterVisualAdapter>(), Is.Not.Null);
        }

        [Test]
        public void GROW_INSTALL_003_InstallTwiceKeepsOneVisual()
        {
            var player = new GameObject("player", typeof(SpriteRenderer));
            _objects.Add(player);
            var registry = Registry(Appearance());
            GrowingCharacterRuntimeInstaller.TryInstall(player, registry);
            GrowingCharacterRuntimeInstaller.TryInstall(player, registry);
            Assert.That(player.GetComponentsInChildren<GrowingCharacterVisualView>(true).Length, Is.EqualTo(1));
        }

        [Test]
        public void GROW_INSTALL_004_LayersFollowPlayerSorting()
        {
            var player = new GameObject("player", typeof(SpriteRenderer));
            _objects.Add(player);
            player.GetComponent<SpriteRenderer>().sortingOrder = 50;
            GrowingCharacterRuntimeInstaller.TryInstall(player, Registry(Appearance()));
            var view = player.GetComponentInChildren<GrowingCharacterVisualView>(true);
            Assert.That(view.BodyRenderer.sortingOrder, Is.EqualTo(50));
            Assert.That(view.HeadRenderer.sortingOrder, Is.EqualTo(52));
        }

        [Test]
        public void GROW_ASSET_001_PackIsImportedWithPixelSettingsAndRegistered()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var appearance = GrowingCharacterRuntimeInstaller.FindDefault(registry);
            Assert.That(appearance, Is.Not.Null, "Run Rootborn/Characters/Import Growing Character Pack");
            Assert.That(appearance.Id, Is.EqualTo("playground_short_brown_casual"));
            Assert.That(appearance.Body.GetFrame(0, 1), Is.Not.Null);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(appearance.Body.GetFrame(0, 1)));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }
    }
}
