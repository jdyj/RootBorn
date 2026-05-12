using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Editor.Tools;
using Rootborn.Game.Common;

namespace Rootborn.Tests.EditMode
{
    public sealed class UISpriteAddressesTests
    {
        [Test]
        public void ModernUiSheets_AreAllRegisteredInAddressablesSetup()
        {
            var registered = new HashSet<string>(AddressablesSetup.GetUiSpriteAddresses());
            var missing = new List<string>();

            foreach (var (sheetAddr, _) in ModernUISpriteAddresses.AllSheets)
            {
                if (!registered.Contains(sheetAddr))
                {
                    missing.Add(sheetAddr);
                }
            }

            Assert.IsEmpty(missing,
                "ModernUISpriteAddresses.AllSheets missing from AddressablesSetup entries: " +
                string.Join(", ", missing));
        }

        [Test]
        public void ModernUiSpriteEntries_HaveAssetFileOnDisk()
        {
            var entries = AddressablesSetup.GetUiSpriteEntries();
            var missing = new List<string>();
            foreach (var (path, _) in entries)
            {
                if (!File.Exists(path))
                {
                    missing.Add(path);
                }
            }

            Assert.IsEmpty(missing,
                "AddressablesSetup Modern UI entries point at missing assets: " +
                string.Join(", ", missing));
        }

        [Test]
        public void ModernUiSheets_DeclareProbeSubSpritesForPreload()
        {
            foreach (var (sheetAddr, subNames) in ModernUISpriteAddresses.AllSheets)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(sheetAddr));
                Assert.IsNotNull(subNames);
                Assert.Greater(subNames.Length, 0, sheetAddr + " must declare at least one preload probe sub-sprite.");
            }
        }
    }
}
