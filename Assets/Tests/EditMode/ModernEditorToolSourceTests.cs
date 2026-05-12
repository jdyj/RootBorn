using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernEditorToolSourceTests
    {
        private const string ItemIconWiringPath = "Assets/Scripts/Editor/Tools/ItemIconWiring.cs";
        private const string AddressablesSetupPath = "Assets/Scripts/Editor/Tools/AddressablesSetup.cs";
        private const string PixelwoodSliceSetupPath = "Assets/Scripts/Editor/Tools/PixelwoodSliceSetup.cs";

        [Test]
        public void ItemIconWiring_UsesModernFarmSingleSpritesInsteadOfPixelwoodItemSheet()
        {
            string source = File.ReadAllText(ItemIconWiringPath);

            Assert.IsFalse(source.Contains("Assets/Pixelwood"), "Item icon wiring still hardcodes Pixelwood item sheet paths.");
            Assert.IsFalse(source.Contains("Wire Icons From Pixelwood"), "Item icon wiring still exposes a Pixelwood menu item.");
            StringAssert.Contains("Assets/Modern_Farm_v1.2", source);
            StringAssert.Contains("Wire Icons From Modern Farm", source);
        }

        [Test]
        public void AddressablesWireAll_DelegatesToModernAddressableSetups()
        {
            string source = File.ReadAllText(AddressablesSetupPath);

            StringAssert.Contains("ModernUiAddressablesSetup.WireSheets", source);
            StringAssert.Contains("ModernFarmAddressablesSetup.WireSheets", source);
            StringAssert.Contains("ModernInteriorsAddressablesSetup.WireSheets", source);
        }

        [Test]
        public void AddressablesSetup_DoesNotContainPixelwoodAssetCatalog()
        {
            string source = File.ReadAllText(AddressablesSetupPath);

            Assert.IsFalse(source.Contains("Assets/Pixelwood"), "Addressables setup still contains a Pixelwood asset catalog.");
            Assert.IsFalse(source.Contains("Fantasy Book"), "Addressables setup still contains Fantasy Book UI wording.");
        }

        [Test]
        public void PixelwoodSliceSetup_IsNotExposedAsExecutableEditorMenu()
        {
            string source = File.ReadAllText(PixelwoodSliceSetupPath);

            Assert.IsFalse(source.Contains("[MenuItem("), "Legacy Pixelwood slicer should remain audit-only and not be exposed as a Unity menu command.");
            Assert.IsFalse(source.Contains("Rootborn/Pixelwood"), "Legacy Pixelwood menu path should not be available after the modern conversion.");
        }
    }
}
