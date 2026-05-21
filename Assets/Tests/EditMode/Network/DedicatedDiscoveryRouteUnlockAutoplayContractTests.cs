using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DedicatedDiscoveryRouteUnlockAutoplayContractTests
    {
        private const string SourcePath = "Assets/Scripts/Game/Common/DirectValidationDiscoveryRouteUnlockAutoplayInstaller.cs";
        private const string SmokeScriptPath = "Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1";

        [Test]
        public void DEDICATED_DISCOVERY_ROUTE_UNLOCK_EDIT_001_AutoplayInstallerDefinesActionObserveScreenshotAndEvidenceContracts()
        {
            Assert.IsTrue(File.Exists(SourcePath), "Dedicated discovery route unlock autoplay installer must exist.");
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("-directValidationAutoplayDiscoveryRouteUnlock", source);
            StringAssert.Contains("-directValidationAutoplayDiscoveryRouteUnlockObserveOnly", source);
            StringAssert.Contains("-directValidationScreenshot", source);
            StringAssert.Contains("autoplay completed discovery-route-unlock", source);
            StringAssert.Contains("autoplay completed discovery-route-unlock-observe", source);
            StringAssert.Contains("autoplay failed", source);
            StringAssert.Contains("clue=", source, "Discovery route unlock smoke must log clue evidence.");
            StringAssert.Contains("interpretation=", source, "Discovery route unlock smoke must log clue interpretation evidence.");
            StringAssert.Contains("worldState=", source, "Discovery route unlock smoke must log world-state evidence.");
            StringAssert.Contains("dailyPlan=day:", source, "Discovery route unlock smoke must log Daily Plan route evidence.");
            StringAssert.Contains("journal=", source, "Discovery route unlock smoke must log Objective Journal evidence.");
            StringAssert.Contains("hud=", source, "Discovery route unlock smoke must log TrackedObjectiveHud evidence.");
            StringAssert.Contains("save=", source, "Discovery route unlock smoke must log saved-state evidence.");
            StringAssert.Contains("duplicateRows:0", source, "Discovery route unlock smoke must log clean duplicate row evidence.");
            StringAssert.Contains("ScreenCapture.CaptureScreenshot", source);
            Assert.IsFalse(source.Contains("TryRevealSource("), "Dedicated autoplay must not reveal a clue by direct runner call.");
            Assert.IsFalse(source.Contains("TryComplete("), "Dedicated autoplay must not complete interpretation by direct runner call.");
            Assert.IsFalse(source.Contains("SetActive("), "Dedicated autoplay must not activate world state by direct domain call.");
            Assert.IsFalse(source.Contains("SelectRoute("), "Dedicated autoplay must not select Daily Plan route by direct domain call.");
            Assert.IsFalse(source.Contains("TrySelectRoute("), "Dedicated autoplay must not select Daily Plan route by direct runner call.");
            Assert.IsFalse(source.Contains("SaveProgress("), "Dedicated autoplay must not write save state directly.");
        }

        [Test]
        public void DEDICATED_DISCOVERY_ROUTE_UNLOCK_EDIT_002_SmokeScriptRunsActionAndObservePassesWithScreenshotsAndCleanup()
        {
            Assert.IsTrue(File.Exists(SmokeScriptPath), "Dedicated discovery route unlock smoke must have a repository-level runner script.");
            string source = File.ReadAllText(SmokeScriptPath);

            StringAssert.Contains("-directValidationAutoplayDiscoveryRouteUnlock", source);
            StringAssert.Contains("-directValidationAutoplayDiscoveryRouteUnlockObserveOnly", source);
            StringAssert.Contains("network-dedicated-discovery-route-unlock-e2e.png", source);
            StringAssert.Contains("network-dedicated-discovery-route-unlock-e2e-reload.png", source);
            StringAssert.Contains("autoplay completed discovery-route-unlock", source);
            StringAssert.Contains("autoplay completed discovery-route-unlock-observe", source);
            StringAssert.Contains("Assert-Screenshot", source);
            StringAssert.Contains("Stop-TrackedProcesses", source);
            StringAssert.Contains("Assert-NoFatalLogs", source);
            StringAssert.Contains("clue=.*seen:true", source);
            StringAssert.Contains("interpretation=.*completed:true", source);
            StringAssert.Contains("worldState=.*active:true", source);
            StringAssert.Contains("dailyPlan=day:.*selected:(?!empty)", source);
            StringAssert.Contains("journal=.*duplicateRows:0", source);
            StringAssert.Contains("hud=.*duplicates:0", source);
            StringAssert.Contains("save=", source);
            StringAssert.DoesNotContain("-directValidationAutoplay\"", source, "Dedicated discovery route smoke must not launch the generic autoplay runner.");
        }

        [Test]
        public void DEDICATED_DISCOVERY_ROUTE_UNLOCK_EDIT_003_SmokeScriptFailsBeforeLaunchOnStaleArtifacts()
        {
            Assert.IsTrue(File.Exists(SmokeScriptPath), "Dedicated discovery route unlock smoke must have a repository-level runner script.");
            string source = File.ReadAllText(SmokeScriptPath);

            StringAssert.Contains("Assert-FreshBuildArtifacts", source, "Dedicated discovery route unlock smoke must fail before process launch if build artifacts are stale or incomplete.");
            StringAssert.Contains("Assert-RequiredArtifact", source, "Dedicated discovery route unlock smoke must validate required executable, managed assembly, and Addressables runtime files.");
            StringAssert.Contains("rootborn-server_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn-server_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("rootborn_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("DirectValidationDiscoveryRouteUnlockAutoplayInstaller.cs", source, "Freshness guard must compare artifacts against the autoplay source required by this runner.");
            StringAssert.Contains("run-dedicated-discovery-route-unlock-smoke.ps1", source, "Freshness guard must compare artifacts against the runner script itself.");
            StringAssert.Contains("BuildServerWindows64", source, "Freshness failure output must name the server build remediation command.");
            StringAssert.Contains("BuildClientWindows64", source, "Freshness failure output must name the client build remediation command.");
        }

        [Test]
        public void DEDICATED_DISCOVERY_ROUTE_UNLOCK_EDIT_004_AutoplayTreatsAlreadyCompletedInterpretationAsIdempotentProgress()
        {
            Assert.IsTrue(File.Exists(SourcePath), "Dedicated discovery route unlock autoplay installer must exist.");
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("IsInterpretationCompleted", source, "Discovery autoplay must inspect saved interpretation progress before failing on a missing choice button.");
            StringAssert.Contains("autoplay interpretation already completed", source, "Discovery autoplay must log the idempotent already-completed interpretation path for matrix diagnostics.");
            StringAssert.Contains("WaitForWorldState", source, "Already-completed interpretation must continue through world-state, route selection, screenshot, and evidence logging.");
        }
    }
}
