using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Tools;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernArtAuditTests
    {
        [Test]
        public void BuildInventory_IncludesCoreModernPacks()
        {
            var inventory = ModernArtAudit.BuildInventory();

            Assert.Greater(inventory.Length, 0);
            Assert.IsTrue(inventory.Any(x => x.SourcePack == "Modern Interiors" && x.Path.StartsWith("Assets/moderninteriors-win/")));
            Assert.IsTrue(inventory.Any(x => x.SourcePack == "Modern Farm" && x.Path.StartsWith("Assets/Modern_Farm_v1.2/")));
            Assert.IsTrue(inventory.Any(x => x.SourcePack == "Modern User Interface" && x.Path.StartsWith("Assets/modernuserinterface-win/")));
        }

        [Test]
        public void BuildInventory_ClassifiesKnownUiAndFarmPaths()
        {
            var inventory = ModernArtAudit.BuildInventory();

            var uiSheet = inventory.FirstOrDefault(x => x.Path == "Assets/modernuserinterface-win/16x16/Modern_UI_Style_1.png");
            Assert.IsNotNull(uiSheet);
            Assert.AreEqual("ui", uiSheet.Role);
            Assert.AreEqual("16x16", uiSheet.GridCandidate);
            Assert.AreEqual("grid", uiSheet.SliceMode);
            Assert.AreEqual("core", uiSheet.Priority);

            var farmIcon = inventory.FirstOrDefault(x => x.Path.Contains("/Icons/") && x.SourcePack == "Modern Farm");
            Assert.IsNotNull(farmIcon);
            Assert.AreEqual("icon", farmIcon.Role);
            Assert.AreEqual("core", farmIcon.Priority);
        }

        [Test]
        public void FindPixelwoodReferences_FindsCurrentCodeReferences()
        {
            var references = ModernArtAudit.FindPixelwoodReferences();

            Assert.IsTrue(references.Any(x => x.Path == "Assets/Scripts/Editor/Tools/PixelwoodSliceSetup.cs"));
            Assert.IsTrue(references.Any(x => x.Path == "Assets/Scripts/Editor/Tools/AddressablesSetup.cs"));
            Assert.IsTrue(references.Any(x => x.Text.Contains("Pixelwood")));
        }

        [Test]
        public void PixelwoodReferenceMarkdown_ContainsFileAndLine()
        {
            var references = ModernArtAudit.FindPixelwoodReferences();
            string markdown = ModernArtAudit.BuildPixelwoodReferenceMarkdown(references);

            Assert.IsTrue(markdown.Contains("# Pixelwood Reference Report"));
            Assert.IsTrue(markdown.Contains("Assets/Scripts/Editor/Tools/PixelwoodSliceSetup.cs"));
            Assert.IsTrue(markdown.Contains("| Line |"));
        }
    }
}
