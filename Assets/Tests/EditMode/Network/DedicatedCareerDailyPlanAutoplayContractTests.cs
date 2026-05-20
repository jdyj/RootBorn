using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DedicatedCareerDailyPlanAutoplayContractTests
    {
        private const string SourcePath = "Assets/Scripts/Game/Common/DirectValidationCareerDailyPlanAutoplayInstaller.cs";
        private const string SmokeScriptPath = "Scripts/qa/run-dedicated-career-daily-plan-route-smoke.ps1";

        [Test]
        public void DEDICATED_CAREER_DAILY_PLAN_EDIT_001_AutoplayInstallerDefinesActionObserveScreenshotAndEvidenceContracts()
        {
            Assert.IsTrue(File.Exists(SourcePath), "Dedicated career Daily Plan autoplay installer must exist.");
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("-directValidationAutoplayCareerDailyPlanRoute", source);
            StringAssert.Contains("-directValidationAutoplayCareerDailyPlanRouteObserveOnly", source);
            StringAssert.Contains("-directValidationScreenshot", source);
            StringAssert.Contains("autoplay completed career-daily-plan-route", source);
            StringAssert.Contains("autoplay completed career-daily-plan-route-observe", source);
            StringAssert.Contains("autoplay failed", source);
            StringAssert.Contains("careerHints=", source, "Career route smoke must log career hint evidence.");
            StringAssert.Contains("dailyPlan=day:", source, "Career route smoke must log Daily Plan evidence.");
            StringAssert.Contains("ScreenCapture.CaptureScreenshot", source);
            Assert.IsFalse(source.Contains("UnlockCareerHint("), "Dedicated autoplay must not mutate career hint state directly.");
            Assert.IsFalse(source.Contains("SelectRoute("), "Dedicated autoplay must not select Daily Plan route by direct domain call.");
            Assert.IsFalse(source.Contains("TrySelectRoute("), "Dedicated autoplay must not select Daily Plan route by direct runner call.");
        }

        [Test]
        public void DEDICATED_CAREER_DAILY_PLAN_EDIT_002_SmokeScriptRunsActionAndObservePassesWithScreenshotsAndCleanup()
        {
            Assert.IsTrue(File.Exists(SmokeScriptPath), "Dedicated career Daily Plan smoke must have a repository-level runner script.");
            string source = File.ReadAllText(SmokeScriptPath);

            StringAssert.Contains("-directValidationAutoplayCareerDailyPlanRoute", source);
            StringAssert.Contains("-directValidationAutoplayCareerDailyPlanRouteObserveOnly", source);
            StringAssert.Contains("network-dedicated-career-daily-plan-route-e2e.png", source);
            StringAssert.Contains("network-dedicated-career-daily-plan-route-e2e-reload.png", source);
            StringAssert.Contains("autoplay completed career-daily-plan-route", source);
            StringAssert.Contains("autoplay completed career-daily-plan-route-observe", source);
            StringAssert.Contains("Assert-Screenshot", source);
            StringAssert.Contains("Stop-TrackedProcesses", source);
            StringAssert.Contains("Assert-NoFatalLogs", source);
            StringAssert.Contains("careerHints=(?!empty)", source);
            StringAssert.Contains("dailyPlan=day:.*selected:(?!empty)", source);
            StringAssert.DoesNotContain("-directValidationAutoplay\"", source, "Dedicated career route smoke must not launch the generic autoplay runner.");
        }
    }
}
