using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters
{
    public sealed class GrowingCharacterStageTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _objects) Object.DestroyImmediate(item);
            _objects.Clear();
        }

        private GrowingCharacterAppearanceDefinition Look(string id, int day, bool adult, string facility)
        {
            var appearance = ScriptableObject.CreateInstance<GrowingCharacterAppearanceDefinition>();
            _objects.Add(appearance);
            appearance.ConfigureForTests(id, null, null, null);
            appearance.ConfigureRulesForTests(day, adult, facility);
            return appearance;
        }

        private CharacterAppearanceDefinition[] Looks()
        {
            return new CharacterAppearanceDefinition[]
            {
                Look("child", 1, false, ""),
                Look("school", 2, false, ""),
                Look("office", 1, true, ""),
                Look("chef", 1, true, "facility.chef"),
            };
        }

        [TestCase(1, "child")]
        [TestCase(2, "school")]
        [TestCase(30, "school")]
        public void GROW_STAGE_001_StudentLookFollowsDay(int day, string expected)
        {
            Assert.That(GrowingCharacterStageSelector.Select(Looks(), day, false, null).Id, Is.EqualTo(expected));
        }

        [Test]
        public void GROW_STAGE_002_AdultUsesFacilityLook()
        {
            Assert.That(GrowingCharacterStageSelector.Select(Looks(), 40, true, "facility.chef").Id, Is.EqualTo("chef"));
        }

        [Test]
        public void GROW_STAGE_003_AdultWithoutOwnLookFallsBackToGeneralAdult()
        {
            Assert.That(GrowingCharacterStageSelector.Select(Looks(), 40, true, "facility.soldier").Id, Is.EqualTo("office"));
        }

        [Test]
        public void GROW_STAGE_004_StudentNeverGetsAdultLook()
        {
            var looks = new CharacterAppearanceDefinition[] { Look("office", 1, true, "") };
            Assert.That(GrowingCharacterStageSelector.Select(looks, 10, false, null), Is.Null);
        }

        [Test]
        public void GROW_STAGE_005_NullEntriesAndOtherVisualKindsAreIgnored()
        {
            var looks = new CharacterAppearanceDefinition[] { null, Look("child", 1, false, "") };
            Assert.That(GrowingCharacterStageSelector.Select(looks, 1, false, null).Id, Is.EqualTo("child"));
            Assert.That(GrowingCharacterStageSelector.Select(null, 1, false, null), Is.Null);
        }

        [Test]
        public void GROW_STAGE_006_InstallAnnouncesPlayerAndView()
        {
            var appearance = Look("child", 1, false, "");
            var player = new GameObject("player");
            _objects.Add(player);
            var registry = ScriptableObject.CreateInstance<Rootborn.Game.Common.GameDataRegistry>();
            _objects.Add(registry);
            var sheet = ScriptableObject.CreateInstance<GrowingCharacterSheetDefinition>();
            _objects.Add(sheet);
            sheet.ConfigureForTests("sheet", new Sprite[GrowingCharacterSheetDefinition.FrameCount]);
            appearance.ConfigureForTests("child", sheet, sheet, sheet);
            typeof(Rootborn.Game.Common.GameDataRegistry).GetField("_characterAppearances", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(registry, new CharacterAppearanceDefinition[] { appearance });
            GameObject seen = null;
            void Handler(GameObject target, GrowingCharacterVisualView view) => seen = target;
            GrowingCharacterRuntimeInstaller.Installed += Handler;
            try
            {
                GrowingCharacterRuntimeInstaller.TryInstall(player, registry);
            }
            finally
            {
                GrowingCharacterRuntimeInstaller.Installed -= Handler;
            }

            Assert.That(seen, Is.EqualTo(player));
        }

        [Test]
        public void GROW_ASSET_002_PackAssetsCarryTheStageRules()
        {
            var registry = AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var looks = registry.CharacterAppearances;
            Assert.That(GrowingCharacterStageSelector.Select(looks, 1, false, null).Id, Does.Contain("casual"));
            Assert.That(GrowingCharacterStageSelector.Select(looks, 2, false, null).Id, Does.Contain("school_uniform"));
            Assert.That(GrowingCharacterStageSelector.Select(looks, 50, true, "facility.chef").Id, Does.Contain("chef"));
            Assert.That(GrowingCharacterStageSelector.Select(looks, 50, true, "facility.none").Id, Does.Contain("office"));
        }
    }
}
