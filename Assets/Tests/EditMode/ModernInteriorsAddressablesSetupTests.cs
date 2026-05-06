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
    public sealed class ModernInteriorsAddressablesSetupTests
    {
        [Test]
        public void AllModernInteriorsSheets_AreRegisteredInModernInteriorsAddressablesSetup()
        {
            var registered = ModernInteriorsAddressablesSetup.GetSheetEntries()
                .Select(entry => entry.address)
                .ToHashSet();

            var missing = new List<string>();
            foreach (var (sheetAddress, _) in ModernInteriorsSpriteAddresses.AllSheets)
            {
                if (!registered.Contains(sheetAddress))
                {
                    missing.Add(sheetAddress);
                }
            }

            Assert.IsEmpty(missing, "Modern Interiors sheets missing from addressable setup: " + string.Join(", ", missing));
        }

        [Test]
        public void ModernInteriorsAddressableEntries_PointOnlyToModernInteriorsPack()
        {
            foreach (var (assetPath, _) in ModernInteriorsAddressablesSetup.GetSheetEntries())
            {
                StringAssert.StartsWith("Assets/moderninteriors-win/", assetPath);
                Assert.IsFalse(assetPath.Contains("Pixelwood"), assetPath);
            }
        }

        [Test]
        public void ModernInteriorsAddressableEntries_HaveAssetFilesOnDisk()
        {
            var missing = new List<string>();
            foreach (var (assetPath, _) in ModernInteriorsAddressablesSetup.GetSheetEntries())
            {
                if (!File.Exists(assetPath))
                {
                    missing.Add(assetPath);
                }
            }

            Assert.IsEmpty(missing, "Modern Interiors addressable files missing on disk: " + string.Join(", ", missing));
        }

        [Test]
        public void AllModernInteriorsSubSprites_ExistInSlicedSheets()
        {
            var missing = new List<string>();
            var entriesByAddress = ModernInteriorsAddressablesSetup.GetSheetEntries()
                .ToDictionary(entry => entry.address, entry => entry.assetPath);

            foreach (var (sheetAddress, subNames) in ModernInteriorsSpriteAddresses.AllSheets)
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

            Assert.IsEmpty(missing, "Modern Interiors declared sub-sprites missing from sliced sheets: " + string.Join(", ", missing));
        }

        [Test]
        public void ModernInteriorsSheets_ArePresentInAddressableSettingsWithPreloadLabel()
        {
            var missing = ModernInteriorsAddressablesSetup.FindMissingRegisteredSheetAddresses();
            var missingLabel = ModernInteriorsAddressablesSetup.FindRegisteredSheetAddressesMissingPreloadLabel();

            Assert.IsEmpty(missing, "Modern Interiors sheets not registered in Addressable settings: " + string.Join(", ", missing));
            Assert.IsEmpty(missingLabel, "Modern Interiors sheets missing PreLoad label: " + string.Join(", ", missingLabel));
        }
    }
}
