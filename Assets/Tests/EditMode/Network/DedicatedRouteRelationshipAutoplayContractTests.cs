using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DedicatedRouteRelationshipAutoplayContractTests
    {
        private const string SourcePath = "Assets/Scripts/Game/Common/DirectValidationRouteRelationshipAutoplayInstaller.cs";
        private const string SmokeScriptPath = "Scripts/qa/run-dedicated-route-relationship-smoke.ps1";

        [Test]
        public void DEDICATED_ROUTE_AUTOPLAY_EDIT_001_RouteRelationshipAutoplayInstallerExists()
        {
            Assert.IsTrue(File.Exists(SourcePath), "Dedicated route/relationship autoplay must live in its own installer so it does not conflict with generic direct validation autoplay.");
        }

        [Test]
        public void DEDICATED_ROUTE_AUTOPLAY_EDIT_002_InstallerDefinesDedicatedFlagSuccessFailureAndScreenshotContracts()
        {
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("-directValidationAutoplayRouteRelationship", source);
            StringAssert.Contains("-directValidationScreenshot", source);
            StringAssert.Contains("autoplay completed route-relationship", source);
            StringAssert.Contains("autoplay failed", source);
            StringAssert.Contains("ScreenCapture.CaptureScreenshot", source);
        }

        [Test]
        public void DEDICATED_ROUTE_AUTOPLAY_EDIT_003_InstallerDefinesObserveOnlyReloadContract()
        {
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("-directValidationAutoplayRouteRelationshipObserveOnly", source);
            StringAssert.Contains("autoplay completed route-relationship-observe", source);
        }

        [Test]
        public void DEDICATED_ROUTE_AUTOPLAY_EDIT_004_DedicatedPersistenceSmokeScriptUsesActionAndObserveOnlyPasses()
        {
            Assert.IsTrue(File.Exists(SmokeScriptPath), "Dedicated route/relationship persistence smoke must have a repository-level runner script.");
            string source = File.ReadAllText(SmokeScriptPath);

            StringAssert.Contains("-directValidationAutoplayRouteRelationship", source);
            StringAssert.Contains("-directValidationAutoplayRouteRelationshipObserveOnly", source);
            StringAssert.Contains("network-dedicated-route-relationship-e2e.png", source);
            StringAssert.Contains("autoplay completed route-relationship", source);
            StringAssert.Contains("autoplay completed route-relationship-observe", source);
            StringAssert.DoesNotContain("-directValidationAutoplay\"", source, "Dedicated route/relationship smoke must not launch the generic autoplay runner.");
        }
    }
}
