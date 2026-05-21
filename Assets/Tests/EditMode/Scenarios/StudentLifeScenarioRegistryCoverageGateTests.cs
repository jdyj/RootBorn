using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Scenarios
{
    public sealed class StudentLifeScenarioRegistryCoverageGateTests
    {
        private const string CatalogPath = "docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md";
        private const string PowerShellGatePath = "Scripts/ci/check-scenario-registry-sync.ps1";
        private const string BashGatePath = "Scripts/ci/check-scenario-registry-sync.sh";
        private const string ScenarioIdPath = "Assets/Tests/PlayMode/Scenarios/ScenarioId.cs";

        [Test]
        public void SCENARIO_REGISTRY_EDIT_001_CurrentCatalogExistsAndCoversRequiredCategories()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
            string catalog = File.ReadAllText(CatalogPath);

            AssertCategory(catalog, "TOWN");
            AssertCategory(catalog, "LIFE");
            AssertCategory(catalog, "CAREER");
            AssertCategory(catalog, "REL");
            AssertCategory(catalog, "JOB");
            AssertCategory(catalog, "KNOW");
            AssertCategory(catalog, "STATUS");
            AssertCategory(catalog, "NET");
            AssertCategory(catalog, "GEN");
            AssertCategory(catalog, "HEIR");
            AssertCategory(catalog, "CROP");
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_002_CatalogMapsModernActiveScenariosToConcreteTestsOrRunners()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
            string catalog = File.ReadAllText(CatalogPath);

            StringAssert.Contains("TOWN-FLOW-001", catalog);
            StringAssert.Contains("IntegratedVerticalSliceFoundationE2ETests", catalog);
            StringAssert.Contains("LIFE-STUDENT-001", catalog);
            StringAssert.Contains("StudentLifeActivityTests", catalog);
            StringAssert.Contains("CAREER-DAILY-PLAN-001", catalog);
            StringAssert.Contains("CareerDailyPlanRouteE2EScenarioTests", catalog);
            StringAssert.Contains("REL-SECOND-NPC-001", catalog);
            StringAssert.Contains("SecondNpcRelationshipMilestoneE2EScenarioTests", catalog);
            StringAssert.Contains("NET-DISCOVERY-ROUTE-001", catalog);
            StringAssert.Contains("Scripts/qa/run-dedicated-discovery-route-unlock-smoke.ps1", catalog);
            StringAssert.Contains("NET-CORE-MATRIX-001", catalog);
            StringAssert.Contains("Scripts/qa/run-dedicated-core-gameplay-matrix.ps1", catalog);
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_003_PowerShellGateExistsAndFailsClosedForMissingMappings()
        {
            Assert.IsTrue(File.Exists(PowerShellGatePath), "The scenario registry sync PowerShell gate must exist.");
            string gate = File.ReadAllText(PowerShellGatePath);

            StringAssert.Contains("2026-05-21-student-life-scenario-registry-coverage.md", gate);
            StringAssert.Contains("Missing active scenario mapping", gate);
            StringAssert.Contains("Missing mapped evidence path", gate);
            StringAssert.Contains("Missing deferred scenario reason", gate);
            StringAssert.Contains("Missing deferred scenario future validation", gate);
            StringAssert.Contains("TOWN", gate);
            StringAssert.Contains("LIFE", gate);
            StringAssert.Contains("CAREER", gate);
            StringAssert.Contains("REL", gate);
            StringAssert.Contains("JOB", gate);
            StringAssert.Contains("NET", gate);
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_004_ScenarioIdAndLegacyBashGateRemainConnected()
        {
            Assert.IsTrue(File.Exists(ScenarioIdPath), "ScenarioId.cs must remain available for PlayMode scenario constants.");
            Assert.IsTrue(File.Exists(BashGatePath), "The legacy Bash gate must remain available or be intentionally updated.");

            string scenarioIds = File.ReadAllText(ScenarioIdPath);
            string bashGate = File.ReadAllText(BashGatePath);

            StringAssert.Contains("TOWN_FLOW_001", scenarioIds);
            StringAssert.Contains("NET_001", scenarioIds);
            StringAssert.Contains("check-scenario-registry-sync.ps1", bashGate);
        }

        private static void AssertCategory(string catalog, string category)
        {
            StringAssert.Contains("| " + category + " |", catalog, "Catalog must include category " + category + ".");
        }
    }
}
