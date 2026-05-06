using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernHudSpriteKeysTests
    {
        [Test]
        public void AllKeys_CoverCurrentStatusHudSpritePurposes()
        {
            var purposes = ModernHudSpriteKeys.All.Select(key => key.Purpose).ToHashSet();
            var expected = new[]
            {
                "HudPanel", "HintPanel", "SmallButton", "ItemSlot", "EquipmentSlot",
                "ItemsRibbon", "DescriptionRibbon", "EquipmentRibbon", "CutterShort", "CutterLong",
                "InscriptionPlus", "BookmarkAll", "BookmarkResource", "BookmarkTool", "BookmarkEquipment",
                "BookmarkMisc", "Character"
            };

            CollectionAssert.AreEquivalent(expected, purposes);
        }

        [Test]
        public void AllKeys_UseModernUiSheetAddressesOnly()
        {
            foreach (var key in ModernHudSpriteKeys.All)
            {
                StringAssert.StartsWith("sprites/ui/modern/", key.SheetAddress, key.Purpose);
                Assert.IsFalse(key.SheetAddress.Contains("book"), key.Purpose);
                Assert.IsFalse(key.SheetAddress.Contains("Pixelwood"), key.Purpose);
            }
        }

        [Test]
        public void AllKeys_ExistInSlicedModernUiSheets()
        {
            var entriesByAddress = ModernUiAddressablesSetup.GetSheetEntries()
                .ToDictionary(entry => entry.address, entry => entry.assetPath);
            var missing = new List<string>();

            foreach (var key in ModernHudSpriteKeys.All)
            {
                Assert.IsTrue(entriesByAddress.TryGetValue(key.SheetAddress, out string assetPath), key.Purpose);
                var spriteNames = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<Sprite>()
                    .Select(sprite => sprite.name)
                    .ToHashSet();
                if (!spriteNames.Contains(key.SubSpriteName))
                {
                    missing.Add($"{key.Purpose}:{key.SheetAddress}:{key.SubSpriteName}");
                }
            }

            Assert.IsEmpty(missing, "Modern HUD sprite keys missing from sliced sheets: " + string.Join(", ", missing));
        }

        [Test]
        public void AllKeys_AreIncludedInModernUiPreloadDeclarations()
        {
            var declared = new HashSet<string>();
            foreach (var (sheetAddress, subNames) in ModernUISpriteAddresses.AllSheets)
            {
                foreach (string subName in subNames)
                {
                    declared.Add(sheetAddress + ":" + subName);
                }
            }

            var missing = new List<string>();
            foreach (var key in ModernHudSpriteKeys.All)
            {
                string id = key.SheetAddress + ":" + key.SubSpriteName;
                if (!declared.Contains(id))
                {
                    missing.Add(key.Purpose + " -> " + id);
                }
            }

            Assert.IsEmpty(missing, "Modern HUD keys missing from preload declarations: " + string.Join(", ", missing));
        }

        [Test]
        public void ManagersPreload_ReferencesModernUiPreloadDeclarations()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Managers/Managers.cs");
            StringAssert.Contains("ModernUISpriteAddresses.AllSheets", source);
        }
    }
}
