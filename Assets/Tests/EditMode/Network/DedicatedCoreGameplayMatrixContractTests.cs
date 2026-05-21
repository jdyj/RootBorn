using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Network
{
    public sealed class DedicatedCoreGameplayMatrixContractTests
    {
        private const string MatrixScriptPath = "Scripts/qa/run-dedicated-core-gameplay-matrix.ps1";

        [Test]
        public void DEDICATED_CORE_MATRIX_EDIT_001_MatrixRunnerDefinesThreeDedicatedSlicesAndInputs()
        {
            Assert.IsTrue(File.Exists(MatrixScriptPath), "Dedicated core gameplay matrix runner must exist.");
            string source = File.ReadAllText(MatrixScriptPath);

            StringAssert.Contains("param(", source);
            StringAssert.Contains("[int]$StartPort", source, "Matrix must allocate explicit ports per slice.");
            StringAssert.Contains("[int]$WaitSeconds", source, "Matrix must expose timeout control for long dedicated slices.");
            StringAssert.Contains("run-dedicated-route-relationship-smoke.ps1", source);
            StringAssert.Contains("run-dedicated-career-daily-plan-route-smoke.ps1", source);
            StringAssert.Contains("run-dedicated-daily-plan-completion-smoke.ps1", source);
            StringAssert.Contains("route-relationship", source);
            StringAssert.Contains("career-daily-plan-route", source);
            StringAssert.Contains("daily-plan-completion", source);
            StringAssert.Contains("$StartPort +", source, "Matrix slices must not compete for the same port.");
            StringAssert.Contains("SaveSlot", source, "Matrix slices must use isolated save slots.");
        }

        [Test]
        public void DEDICATED_CORE_MATRIX_EDIT_002_MatrixRunnerRequiresPerSliceEvidenceAndScreenshots()
        {
            Assert.IsTrue(File.Exists(MatrixScriptPath), "Dedicated core gameplay matrix runner must exist.");
            string source = File.ReadAllText(MatrixScriptPath);

            StringAssert.Contains("Assert-SliceEvidence", source);
            StringAssert.Contains("Assert-Screenshot", source);
            StringAssert.Contains("autoplay completed route-relationship", source);
            StringAssert.Contains("autoplay completed route-relationship-observe", source);
            StringAssert.Contains("autoplay completed career-daily-plan-route", source);
            StringAssert.Contains("autoplay completed career-daily-plan-route-observe", source);
            StringAssert.Contains("autoplay completed daily-plan-completion", source);
            StringAssert.Contains("autoplay completed daily-plan-completion-observe", source);
            StringAssert.Contains("network-dedicated-route-relationship-e2e.png", source);
            StringAssert.Contains("network-dedicated-career-daily-plan-route-e2e.png", source);
            StringAssert.Contains("network-dedicated-daily-plan-completion-e2e.png", source);
            StringAssert.Contains("duplicateRows:0", source, "Matrix must preserve Daily Plan completion duplicate-state evidence.");
            StringAssert.Contains("hud=.*duplicates:0", source, "Matrix must preserve HUD duplicate-state evidence.");
        }

        [Test]
        public void DEDICATED_CORE_MATRIX_EDIT_003_MatrixRunnerFailsClosedAndDoesNotLaunchGenericAutoplay()
        {
            Assert.IsTrue(File.Exists(MatrixScriptPath), "Dedicated core gameplay matrix runner must exist.");
            string source = File.ReadAllText(MatrixScriptPath);

            StringAssert.Contains("Invoke-Slice", source);
            StringAssert.Contains("throw", source);
            StringAssert.Contains("ExitCode", source, "Matrix must fail if any child runner exits non-zero.");
            StringAssert.Contains("Result = \"Passed\"", source);
            StringAssert.Contains("SliceResults", source);
            StringAssert.DoesNotContain("-directValidationAutoplay\"", source, "Matrix must invoke dedicated slice scripts, not the generic autoplay flag.");
        }
    }
}
