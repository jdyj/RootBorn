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
    public sealed class ModernFarmAddressablesSetupTests
    {
        [Test]
        public void AllModernFarmSheets_AreRegisteredInModernFarmAddressablesSetup()
        {
            var registered = ModernFarmAddressablesSetup.GetSheetEntries()
                .Select(entry => entry.address)
                .ToHashSet();

            var missing = new List<string>();
            foreach (var (sheetAddress, _) in ModernFarmSpriteAddresses.AllSheets)
            {
                if (!registered.Contains(sheetAddress))
                {
                    missing.Add(sheetAddress);
                }
            }

            Assert.IsEmpty(missing, "Modern Farm sheets missing from addressable setup: " + string.Join(", ", missing));
        }

        [Test]
        public void ModernFarmAddressableEntries_PointOnlyToModernFarmPack()
        {
            foreach (var (assetPath, _) in ModernFarmAddressablesSetup.GetSheetEntries())
            {
                StringAssert.StartsWith("Assets/Modern_Farm_v1.2/16x16/", assetPath);
                Assert.IsFalse(assetPath.Contains("Pixelwood"), assetPath);
            }
        }

        [Test]
        public void ModernFarmAddressableEntries_HaveAssetFilesOnDisk()
        {
            var missing = new List<string>();
            foreach (var (assetPath, _) in ModernFarmAddressablesSetup.GetSheetEntries())
            {
                if (!File.Exists(assetPath))
                {
                    missing.Add(assetPath);
                }
            }

            Assert.IsEmpty(missing, "Modern Farm addressable files missing on disk: " + string.Join(", ", missing));
        }

        [Test]
        public void AllModernFarmSubSprites_ExistInSlicedSheets()
        {
            var missing = new List<string>();
            var entriesByAddress = ModernFarmAddressablesSetup.GetSheetEntries()
                .ToDictionary(entry => entry.address, entry => entry.assetPath);

            foreach (var (sheetAddress, subNames) in ModernFarmSpriteAddresses.AllSheets)
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

            Assert.IsEmpty(missing, "Modern Farm declared sub-sprites missing from sliced sheets: " + string.Join(", ", missing));
        }

        [Test]
        public void ModernFarmSheets_ArePresentInAddressableSettingsWithPreloadLabel()
        {
            var missing = ModernFarmAddressablesSetup.FindMissingRegisteredSheetAddresses();
            var missingLabel = ModernFarmAddressablesSetup.FindRegisteredSheetAddressesMissingPreloadLabel();

            Assert.IsEmpty(missing, "Modern Farm sheets not registered in Addressable settings: " + string.Join(", ", missing));
            Assert.IsEmpty(missingLabel, "Modern Farm sheets missing PreLoad label: " + string.Join(", ", missingLabel));
        }
    }
}
