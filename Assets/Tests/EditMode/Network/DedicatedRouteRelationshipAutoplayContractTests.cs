using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DedicatedRouteRelationshipAutoplayContractTests
    {
        private const string SourcePath = "Assets/Scripts/Game/Common/DirectValidationRouteRelationshipAutoplayInstaller.cs";
        private const string SmokeScriptPath = "Scripts/qa/run-dedicated-route-relationship-smoke.ps1";
        private const string LongRunScriptPath = "Scripts/qa/run-dedicated-route-relationship-longrun.ps1";

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

        [Test]
        public void DEDICATED_ROUTE_AUTOPLAY_EDIT_005_RouteSelectionFailureLogsPanelAndRouteState()
        {
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("DescribeDailyPlanUiState", source, "Dedicated route selection failures must include Daily Plan panel and row diagnostics, not only a generic selectable-button failure.");
            StringAssert.Contains("Daily Plan route button not selectable ", source);
            StringAssert.Contains("RouteRowCountForTests", source);
            StringAssert.Contains("EmptyStateTextForTests", source);
            StringAssert.Contains("GetRouteButtonForTests", source);
        }

        [Test]
        public void DEDICATED_ROUTE_AUTOPLAY_EDIT_006_ObserveOnlyWaitsForDailyPlanRuntimeBeforeSnapshot()
        {
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("WaitForDailyPlanProgress", source, "Observe-only reload evidence must wait for DailyPlanProgressComponent instead of snapshotting dailyPlan=missing before runtime installers finish.");
            StringAssert.Contains("DailyPlanProgressComponent", source);
            StringAssert.Contains("route-relationship daily plan progress not ready", source);
        }

        [Test]
        public void DEDICATED_ROUTE_AUTOPLAY_EDIT_007_LongRunScriptRunsActionThenObserveCyclesWithScreenshotsAndCleanup()
        {
            Assert.IsTrue(File.Exists(LongRunScriptPath), "Dedicated route/relationship long-run regression script must exist.");
            string source = File.ReadAllText(LongRunScriptPath);

            StringAssert.Contains("param(", source);
            StringAssert.Contains("[int]$Cycles", source);
            StringAssert.Contains("[int]$StartPort", source);
            StringAssert.Contains("-directValidationAutoplayRouteRelationship", source);
            StringAssert.Contains("-directValidationAutoplayRouteRelationshipObserveOnly", source);
            StringAssert.Contains("network-dedicated-route-relationship-longrun-cycle", source);
            StringAssert.Contains("Assert-Screenshot", source);
            StringAssert.Contains("Assert-NoTrackedProcessesAlive", source);
            StringAssert.Contains("autoplay completed route-relationship-observe", source);
        }

        [Test]
        public void DEDICATED_ROUTE_AUTOPLAY_EDIT_008_SmokeScriptFailsBeforeLaunchOnStaleArtifacts()
        {
            Assert.IsTrue(File.Exists(SmokeScriptPath), "Dedicated route/relationship persistence smoke must have a repository-level runner script.");
            string source = File.ReadAllText(SmokeScriptPath);

            StringAssert.Contains("Assert-FreshBuildArtifacts", source, "Dedicated route/relationship smoke must fail before process launch if build artifacts are stale or incomplete.");
            StringAssert.Contains("Assert-RequiredArtifact", source, "Dedicated route/relationship smoke must validate required executable, managed assembly, and Addressables runtime files.");
            StringAssert.Contains("rootborn-server_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn-server_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("rootborn_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("DirectValidationRouteRelationshipAutoplayInstaller.cs", source, "Freshness guard must compare artifacts against the autoplay source required by this runner.");
            StringAssert.Contains("run-dedicated-route-relationship-smoke.ps1", source, "Freshness guard must compare artifacts against the runner script itself.");
            StringAssert.Contains("BuildServerWindows64", source, "Freshness failure output must name the server build remediation command.");
            StringAssert.Contains("BuildClientWindows64", source, "Freshness failure output must name the client build remediation command.");
        }

        [Test]
        public void DEDICATED_ROUTE_AUTOPLAY_EDIT_009_LongRunScriptFailsBeforeLaunchOnStaleArtifacts()
        {
            Assert.IsTrue(File.Exists(LongRunScriptPath), "Dedicated route/relationship long-run regression script must exist.");
            string source = File.ReadAllText(LongRunScriptPath);

            StringAssert.Contains("Assert-FreshBuildArtifacts", source, "Dedicated route/relationship long-run must fail before process launch if build artifacts are stale or incomplete.");
            StringAssert.Contains("Assert-RequiredArtifact", source, "Dedicated route/relationship long-run must validate required executable, managed assembly, and Addressables runtime files.");
            StringAssert.Contains("rootborn-server_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn-server_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("rootborn_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("DirectValidationRouteRelationshipAutoplayInstaller.cs", source, "Freshness guard must compare artifacts against the autoplay source required by this runner.");
            StringAssert.Contains("run-dedicated-route-relationship-longrun.ps1", source, "Freshness guard must compare artifacts against the long-run runner script itself.");
            StringAssert.Contains("BuildServerWindows64", source, "Freshness failure output must name the server build remediation command.");
            StringAssert.Contains("BuildClientWindows64", source, "Freshness failure output must name the client build remediation command.");
        }
    }
}
