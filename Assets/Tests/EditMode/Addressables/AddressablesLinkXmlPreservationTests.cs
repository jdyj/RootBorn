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
        private const string LinkXmlMetaPath = "Assets/AddressableAssetsData/link.xml.meta";
        private const string RepositoryGuardPath = "Scripts/ci/check-addressables-linkxml-preserved.ps1";

        private static readonly string[] RequiredPreservedTypes =
        {
            "Rootborn.Game.Common.DirectValidationAutoplayInstaller",
            "Rootborn.Game.Common.DirectValidationDailyPlanCompletionAutoplayInstaller",
            "Rootborn.Game.Common.DirectValidationRouteRelationshipAutoplayInstaller",
            "Rootborn.Game.Common.DirectValidationTrace",
            "Rootborn.Game.Common.DirectValidationTraceInstaller",
            "Rootborn.Game.Common.GameDataRegistry",
            "Rootborn.Game.DiscoveryClues.ClueInterpretationDefinition",
            "Rootborn.Game.DiscoveryClues.ClueInterpretationWorldStateOutcome",
            "Rootborn.Game.StudentLife.DailyPlanRouteDefinition",
            "Rootborn.Game.StudentLife.DailyPlanRouteSetDefinition",
            "Rootborn.Game.StudentLife.DailyPlanWorldStateRequirement",
            "Rootborn.Game.StudentLife.HelpActionRelationshipDeltaOutcome",
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
                    $"{LinkXmlPath} must preserve '{requiredType}' with preserve=\"all\" so registry-driven data and direct executable validation runners survive managed stripping.");
            }
        }

        [Test]
        public void LINKXML_EDIT_002_LinkXmlAssetAndRepositoryGuardArePresent()
        {
            Assert.IsTrue(File.Exists(LinkXmlPath), $"Expected linker preserve file at {LinkXmlPath}.");
            Assert.IsTrue(File.Exists(LinkXmlMetaPath), $"Expected linker preserve meta file at {LinkXmlMetaPath}; Unity tooling must not leave this asset deleted without its meta.");
            Assert.IsTrue(File.Exists(RepositoryGuardPath), $"Expected repository-level guard at {RepositoryGuardPath} so non-Unity verification catches accidental link.xml deletion.");
        }

        [Test]
        public void LINKXML_EDIT_003_RepositoryGuardChecksGitTrackingDeletionAndRemediation()
        {
            Assert.IsTrue(File.Exists(RepositoryGuardPath), $"Expected repository-level guard at {RepositoryGuardPath}.");
            string source = File.ReadAllText(RepositoryGuardPath);

            StringAssert.Contains("git ls-files --error-unmatch", source, "Repository guard must fail if link.xml or its meta are not tracked by git.");
            StringAssert.Contains("git status --porcelain -- $path", source, "Repository guard must inspect git deletion state, not only filesystem existence.");
            StringAssert.Contains("Contains(\"D\")", source, "Repository guard must fail when git status marks either required path deleted.");
            StringAssert.Contains("git restore -- Assets/AddressableAssetsData/link.xml Assets/AddressableAssetsData/link.xml.meta", source, "Repository guard failure output must show the exact remediation command used after Unity test/build tooling deletes link.xml.");
        }
    }
}
