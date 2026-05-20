using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Addressables
{
    public sealed class AddressablesLinkXmlPreservationTests
    {
        private const string LinkXmlPath = "Assets/AddressableAssetsData/link.xml";

        private static readonly string[] RequiredPreservedTypes =
        {
            "Rootborn.Game.Common.GameDataRegistry",
            "Rootborn.Game.DiscoveryClues.ClueInterpretationDefinition",
            "Rootborn.Game.DiscoveryClues.ClueInterpretationWorldStateOutcome",
            "Rootborn.Game.StudentLife.DailyPlanRouteDefinition",
            "Rootborn.Game.StudentLife.DailyPlanRouteSetDefinition",
            "Rootborn.Game.StudentLife.DailyPlanWorldStateRequirement",
            "Rootborn.Game.StudentLife.LocationActivityDefinition",
            "Rootborn.Game.StudentLife.LocationDefinition",
            "Rootborn.Game.WorldState.WorldStateFlagDefinition",
            "Rootborn.Game.WorldState.WorldStateProgress"
        };

        [Test]
        public void LINKXML_EDIT_001_CriticalRegistryRouteUnlockTypesArePreserved()
        {
            Assert.IsTrue(File.Exists(LinkXmlPath), $"Expected linker preserve file at {LinkXmlPath}.");

            var document = XDocument.Load(LinkXmlPath);
            var preservedTypes = new HashSet<string>(
                document.Descendants("type")
                    .Where(element => string.Equals((string)element.Attribute("preserve"), "all", StringComparison.Ordinal))
                    .Select(element => (string)element.Attribute("fullname"))
                    .Where(fullname => !string.IsNullOrWhiteSpace(fullname)),
                StringComparer.Ordinal);

            foreach (var requiredType in RequiredPreservedTypes)
            {
                Assert.IsTrue(
                    preservedTypes.Contains(requiredType),
                    $"{LinkXmlPath} must preserve '{requiredType}' with preserve=\"all\" so registry-driven route unlock data survives managed stripping.");
            }
        }
    }
}
