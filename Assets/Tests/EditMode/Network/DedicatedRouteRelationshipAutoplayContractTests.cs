using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DedicatedRouteRelationshipAutoplayContractTests
    {
        private const string SourcePath = "Assets/Scripts/Game/Common/DirectValidationRouteRelationshipAutoplayInstaller.cs";

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
    }
}
