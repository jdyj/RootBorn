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
    public sealed class ModernUiAddressablesSetupTests
    {
        [Test]
        public void AllModernUiSheets_AreRegisteredInModernAddressablesSetup()
        {
            var registered = ModernUiAddressablesSetup.GetSheetEntries()
                .Select(entry => entry.address)
                .ToHashSet();

            var missing = new List<string>();
            foreach (var (sheetAddress, _) in ModernUISpriteAddresses.AllSheets)
            {
                if (!registered.Contains(sheetAddress))
                {
                    missing.Add(sheetAddress);
                }
            }

            Assert.IsEmpty(missing, "Modern UI sheets missing from addressable setup: " + string.Join(", ", missing));
        }

        [Test]
        public void ModernUiAddressableEntries_PointOnlyToModernUiPack()
        {
            foreach (var (assetPath, _) in ModernUiAddressablesSetup.GetSheetEntries())
            {
                StringAssert.StartsWith("Assets/modernuserinterface-win/", assetPath);
                Assert.IsFalse(assetPath.Contains("Pixelwood"), assetPath);
            }
        }

        [Test]
        public void ModernUiAddressableEntries_HaveAssetFilesOnDisk()
        {
            var missing = new List<string>();
            foreach (var (assetPath, _) in ModernUiAddressablesSetup.GetSheetEntries())
            {
                if (!File.Exists(assetPath))
                {
                    missing.Add(assetPath);
                }
            }

            Assert.IsEmpty(missing, "Modern UI addressable files missing on disk: " + string.Join(", ", missing));
        }

        [Test]
        public void AllModernUiSubSprites_ExistInSlicedSheets()
        {
            var missing = new List<string>();
            var entriesByAddress = ModernUiAddressablesSetup.GetSheetEntries()
                .ToDictionary(entry => entry.address, entry => entry.assetPath);

            foreach (var (sheetAddress, subNames) in ModernUISpriteAddresses.AllSheets)
            {
                Assert.IsTrue(entriesByAddress.TryGetValue(sheetAddress, out string assetPath), sheetAddress);
                var spriteNames = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<Sprite>()
                    .Select(sprite => sprite.name)
                    .ToHashSet();

                foreach (string subName in subNames)
                {
                    if (!spriteNames.Contains(subName))
                    {
                        missing.Add($"{sheetAddress}:{subName}");
                    }
                }
            }

            Assert.IsEmpty(missing, "Modern UI declared sub-sprites missing from sliced sheets: " + string.Join(", ", missing));
        }

        [Test]
        public void ModernUiSheets_ArePresentInAddressableSettingsWithPreloadLabel()
        {
            var missing = ModernUiAddressablesSetup.FindMissingRegisteredSheetAddresses();
            var missingLabel = ModernUiAddressablesSetup.FindRegisteredSheetAddressesMissingPreloadLabel();

            Assert.IsEmpty(missing, "Modern UI sheets not registered in Addressable settings: " + string.Join(", ", missing));
            Assert.IsEmpty(missingLabel, "Modern UI sheets missing PreLoad label: " + string.Join(", ", missingLabel));
        }
    }
}
