using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownConceptSourceAuditTests
    {
        [Test]
        public void ModernSocietyActivityKinds_DoNotExposeFarmHelp()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/ModernSociety/ModernActivityKind.cs");
            StringAssert.DoesNotContain("FarmHelp", source);
        }

        [Test]
        public void TownConceptDocs_RecordFarmResidueAudit()
        {
            Assert.IsTrue(File.Exists("docs/art/town-concept-audit.md"));
            string audit = File.ReadAllText("docs/art/town-concept-audit.md");
            StringAssert.Contains("Farm Residue Classes", audit);
            StringAssert.Contains("Style1 To Style2 UI Decision", audit);
        }
    }
}