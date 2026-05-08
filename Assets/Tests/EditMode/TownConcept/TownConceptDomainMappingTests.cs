using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownConceptDomainMappingTests
    {
        [Test]
        public void DomainMapping_DocumentsTownReplacementForFarmLoop()
        {
            string mapping = File.ReadAllText("docs/art/town-concept-domain-mapping.md");

            StringAssert.Contains("City resident", mapping);
            StringAssert.Contains("Town scene", mapping);
            StringAssert.Contains("Activity progress", mapping);
            StringAssert.Contains("Neighbor request", mapping);
            StringAssert.DoesNotContain("Use farming as first loop", mapping);
        }

        [Test]
        public void TownSceneAsset_Exists()
        {
            Assert.IsTrue(File.Exists("Assets/Scenes/Town.unity"), "Town scene must exist before default flow migration.");
        }
    }
}