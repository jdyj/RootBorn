using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DedicatedDailyPlanCompletionAutoplayContractTests
    {
        private const string SourcePath = "Assets/Scripts/Game/Common/DirectValidationDailyPlanCompletionAutoplayInstaller.cs";
        private const string SmokeScriptPath = "Scripts/qa/run-dedicated-daily-plan-completion-smoke.ps1";

        [Test]
        public void DEDICATED_DAILY_PLAN_COMPLETION_EDIT_001_AutoplayInstallerDefinesActionObserveScreenshotAndEvidenceContracts()
        {
            Assert.IsTrue(File.Exists(SourcePath), "Dedicated Daily Plan completion autoplay installer must exist.");
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("-directValidationAutoplayDailyPlanCompletion", source);
            StringAssert.Contains("-directValidationAutoplayDailyPlanCompletionObserveOnly", source);
            StringAssert.Contains("-directValidationScreenshot", source);
            StringAssert.Contains("autoplay completed daily-plan-completion", source);
            StringAssert.Contains("autoplay completed daily-plan-completion-observe", source);
            StringAssert.Contains("autoplay failed", source);
            StringAssert.Contains("dailyPlan=day:", source, "Daily Plan completion smoke must log route completion evidence.");
            StringAssert.Contains("result=", source, "Daily Plan completion smoke must log day result feedback evidence.");
            StringAssert.Contains("journal=", source, "Daily Plan completion smoke must log Objective Journal evidence.");
            StringAssert.Contains("hud=", source, "Daily Plan completion smoke must log TrackedObjectiveHud evidence.");
            StringAssert.Contains("save=", source, "Daily Plan completion smoke must log saved-state evidence.");
            StringAssert.Contains("ScreenCapture.CaptureScreenshot", source);
            Assert.IsFalse(source.Contains("SelectRoute("), "Dedicated autoplay must not select Daily Plan route by direct domain call.");
            Assert.IsFalse(source.Contains("TrySelectRoute("), "Dedicated autoplay must not select Daily Plan route by direct runner call.");
            Assert.IsFalse(source.Contains("CompleteRoute("), "Dedicated autoplay must not complete Daily Plan route by direct domain call.");
            Assert.IsFalse(source.Contains("MarkCompleted("), "Dedicated autoplay must not mark Daily Plan completion by direct domain call.");
            Assert.IsFalse(source.Contains("SaveProgress("), "Dedicated autoplay must not write Daily Plan save state directly.");
        }

        [Test]
        public void DEDICATED_DAILY_PLAN_COMPLETION_EDIT_002_SmokeScriptRunsActionAndObservePassesWithScreenshotsAndCleanup()
        {
            Assert.IsTrue(File.Exists(SmokeScriptPath), "Dedicated Daily Plan completion smoke must have a repository-level runner script.");
            string source = File.ReadAllText(SmokeScriptPath);

            StringAssert.Contains("-directValidationAutoplayDailyPlanCompletion", source);
            StringAssert.Contains("-directValidationAutoplayDailyPlanCompletionObserveOnly", source);
            StringAssert.Contains("network-dedicated-daily-plan-completion-e2e.png", source);
            StringAssert.Contains("network-dedicated-daily-plan-completion-e2e-reload.png", source);
            StringAssert.Contains("autoplay completed daily-plan-completion", source);
            StringAssert.Contains("autoplay completed daily-plan-completion-observe", source);
            StringAssert.Contains("Assert-Screenshot", source);
            StringAssert.Contains("Stop-TrackedProcesses", source);
            StringAssert.Contains("Assert-NoFatalLogs", source);
            StringAssert.Contains("dailyPlan=day:.*selected:(?!empty).*completed:(?!empty)", source);
            StringAssert.Contains("result=.*Daily Plan", source);
            StringAssert.Contains("journal=.*duplicateRows:0", source);
            StringAssert.Contains("hud=.*duplicates:0", source);
            StringAssert.DoesNotContain("-directValidationAutoplay\"", source, "Dedicated Daily Plan completion smoke must not launch the generic autoplay runner.");
        }

        [Test]
        public void DEDICATED_DAILY_PLAN_COMPLETION_EDIT_003_SmokeScriptFailsBeforeLaunchOnStaleArtifacts()
        {
            Assert.IsTrue(File.Exists(SmokeScriptPath), "Dedicated Daily Plan completion smoke must have a repository-level runner script.");
            string source = File.ReadAllText(SmokeScriptPath);

            StringAssert.Contains("Assert-FreshBuildArtifacts", source, "Dedicated Daily Plan completion smoke must fail before process launch if build artifacts are stale or incomplete.");
            StringAssert.Contains("Assert-RequiredArtifact", source, "Dedicated Daily Plan completion smoke must validate required executable, managed assembly, and Addressables runtime files.");
            StringAssert.Contains("rootborn-server_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn_Data\\Managed\\Rootborn.Game.dll", source);
            StringAssert.Contains("rootborn-server_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("rootborn_Data\\StreamingAssets\\aa\\settings.json", source);
            StringAssert.Contains("DirectValidationDailyPlanCompletionAutoplayInstaller.cs", source, "Freshness guard must compare artifacts against the autoplay source required by this runner.");
            StringAssert.Contains("run-dedicated-daily-plan-completion-smoke.ps1", source, "Freshness guard must compare artifacts against the runner script itself.");
            StringAssert.Contains("BuildServerWindows64", source, "Freshness failure output must name the server build remediation command.");
            StringAssert.Contains("BuildClientWindows64", source, "Freshness failure output must name the client build remediation command.");
        }

        [Test]
        public void DEDICATED_DAILY_PLAN_COMPLETION_EDIT_004_AutoplaySelectsCanonicalLibraryRouteByVisibleRowText()
        {
            Assert.IsTrue(File.Exists(SourcePath), "Dedicated Daily Plan completion autoplay installer must exist.");
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("daily.route.library.self-study", source, "Dedicated Daily Plan completion smoke must select the same stable Library route used by the player-facing E2E.");
            StringAssert.Contains("WaitForRouteButtonContaining", source, "Dedicated Daily Plan completion smoke must select a visible route row by text instead of the first generated row.");
            StringAssert.Contains("Library", source, "Failure output must identify the intended Library route when the visible row is missing.");
        }
    }
}
