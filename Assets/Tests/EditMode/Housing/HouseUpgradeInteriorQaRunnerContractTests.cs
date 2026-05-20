using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Housing
{
    public sealed class HouseUpgradeInteriorQaRunnerContractTests
    {
        private const string ScriptPath = "Scripts/qa/run-house-upgrade-interior-loop.ps1";

        [Test]
        public void HOUSE_QA_RUNNER_001_ScriptRequiresStrictE2EAndEvidenceMarkers()
        {
            Assert.IsTrue(File.Exists(ScriptPath), "House upgrade/interior QA runner must exist at " + ScriptPath + ".");

            string source = File.ReadAllText(ScriptPath);
            StringAssert.Contains("HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload", source, "Runner must execute the strict House upgrade/interior E2E method.");
            StringAssert.Contains("Builds/Logs/house-upgrade-interior-loop-e2e.png", source, "Runner must verify screenshot evidence.");
            StringAssert.Contains("check-addressables-linkxml-preserved.ps1", source, "Runner must verify Addressables link.xml preservation.");
            StringAssert.Contains("HOUSE_QA_STAGE_MARKER", source, "Runner must print a stage evidence marker.");
            StringAssert.Contains("HOUSE_QA_PRESET_MARKER", source, "Runner must print a room preset evidence marker.");
            StringAssert.Contains("HOUSE_QA_FURNITURE_LAYOUT_MARKER", source, "Runner must print a furniture layout evidence marker.");
            StringAssert.Contains("HOUSE_QA_RUNTIME_TILEMAP_MARKER", source, "Runner must print a runtime tilemap evidence marker.");
            StringAssert.Contains("HOUSE_QA_OVERLAY_CLEANUP_MARKER", source, "Runner must print an overlay cleanup evidence marker.");
            StringAssert.Contains("HOUSE_QA_ADDRESSABLES_MARKER", source, "Runner must print an Addressables preservation evidence marker.");
            StringAssert.Contains("throw", source, "Runner must fail closed when required evidence is missing.");
        }
    }
}
