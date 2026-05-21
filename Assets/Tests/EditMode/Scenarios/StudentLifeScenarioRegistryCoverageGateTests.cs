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
            AssertCategory(catalog, "UI");
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
        public void SCENARIO_REGISTRY_EDIT_003_CatalogMapsResourceRecipeScenariosAfterFeatureAndDedicatedSmoke()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
            string catalog = File.ReadAllText(CatalogPath);

            StringAssert.Contains("RESOURCE-RECIPE-001", catalog, "Resource/Recipe PlayMode scenario must be active after the Town activity slice landed.");
            StringAssert.Contains("TownResourceRecipeActivityE2EScenarioTests", catalog);
            StringAssert.Contains("NET-RESOURCE-RECIPE-001", catalog, "Resource/Recipe dedicated smoke must be active after the runner landed.");
            StringAssert.Contains("Scripts/qa/run-dedicated-resource-recipe-activity-smoke.ps1", catalog);
            StringAssert.Contains("NET-CORE-MATRIX-RESOURCE-001", catalog, "Core matrix must explicitly document the Resource/Recipe slice evidence.");
            StringAssert.Contains("Resource/Recipe slice", catalog);
            StringAssert.DoesNotContain("Resource/recipe Town activity slice is the next feature after stabilization", catalog, "Resource/Recipe must no longer be deferred as future work.");
            StringAssert.DoesNotContain("Dedicated smoke depends on the resource/recipe feature slice", catalog, "Resource/Recipe dedicated smoke must no longer be deferred as future work.");
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_004_PowerShellGateExistsAndFailsClosedForMissingMappings()
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
            StringAssert.Contains("UI", gate);
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_005_ScenarioIdAndLegacyBashGateRemainConnected()
        {
            Assert.IsTrue(File.Exists(ScenarioIdPath), "ScenarioId.cs must remain available for PlayMode scenario constants.");
            Assert.IsTrue(File.Exists(BashGatePath), "The legacy Bash gate must remain available or be intentionally updated.");

            string scenarioIds = File.ReadAllText(ScenarioIdPath);
            string bashGate = File.ReadAllText(BashGatePath);

            StringAssert.Contains("TOWN_FLOW_001", scenarioIds);
            StringAssert.Contains("NET_001", scenarioIds);
            StringAssert.Contains("check-scenario-registry-sync.ps1", bashGate);
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_006_CatalogActivatesJobCareerBridgeScenario()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
            string catalog = File.ReadAllText(CatalogPath);

            StringAssert.Contains("| Active | JOB-CAREER-BRIDGE-001 | JOB | Part-Time Work/Career Bridge | PlayMode | Assets/Tests/PlayMode/EndToEnd/CareerJobRouteE2EScenarioTests.cs |", catalog, "Job/Career bridge must be active now that the CareerJobRoute E2E exists.");
            StringAssert.Contains("CareerJobRouteE2EScenarioTests", catalog);
            StringAssert.Contains("CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload", catalog);
            StringAssert.DoesNotContain("| Deferred | JOB-CAREER-BRIDGE-001 |", catalog, "Job/Career bridge must no longer remain deferred after executable PlayMode coverage landed.");
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_007_PostMvpNetworkScenariosAreExplicitPendingIds()
        {
            Assert.IsTrue(File.Exists(ScenarioIdPath), "ScenarioId.cs must remain available for pending PlayMode scenario markers.");
            string scenarioIds = File.ReadAllText(ScenarioIdPath);

            StringAssert.Contains("[PENDING]", scenarioIds);
            StringAssert.Contains("NET-002", scenarioIds);
            StringAssert.Contains("NET-003", scenarioIds);
            StringAssert.Contains("NET-004", scenarioIds);
            StringAssert.Contains("NET-005", scenarioIds);
            StringAssert.Contains("TOOL-001", scenarioIds);
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_008_CatalogActivatesObjectiveJournalRegressionScenario()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
            string catalog = File.ReadAllText(CatalogPath);

            StringAssert.Contains("| Active | OBJECTIVE-JOURNAL-REGRESSION-001 | UI | Objective Journal Regression | PlayMode | Assets/Tests/PlayMode/EndToEnd/ObjectiveJournalRegressionE2EScenarioTests.cs |", catalog, "Objective Journal regression must be active catalog evidence because it protects goal-like UI ownership.");
            StringAssert.Contains("OBJECTIVE_JOURNAL_SOURCE_COLLISION_PM_001_MultiSourceReloadNoDuplicateHud", catalog);
            StringAssert.Contains("Builds/Logs/objective-journal-source-collision-e2e.png", catalog);
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_009_CatalogActivatesDedicatedRouteRelationshipLongRun()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
            string catalog = File.ReadAllText(CatalogPath);

            StringAssert.Contains("| Active | NET-ROUTE-REL-LONGRUN-001 | NET | Dedicated Route Relationship Long-Run | QA runner | Scripts/qa/run-dedicated-route-relationship-longrun.ps1 |", catalog, "Dedicated route/relationship long-run must be active catalog evidence after the refreshed run passed.");
            StringAssert.Contains("docs/superpowers/audits/2026-05-21-dedicated-route-relationship-longrun-refresh.md", catalog);
            StringAssert.Contains("Builds/Logs/network-dedicated-route-relationship-longrun-refresh", catalog);
        }

        [Test]
                public void SCENARIO_REGISTRY_EDIT_010_CatalogReferencesDedicatedCoreMatrixRefreshEvidence()
                {
                    Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
                    string catalog = File.ReadAllText(CatalogPath);

                    StringAssert.Contains("| Active | NET-CORE-MATRIX-001 | NET | Dedicated Core Matrix | QA runner | Scripts/qa/run-dedicated-core-gameplay-matrix.ps1 |", catalog, "Dedicated core matrix must remain active catalog evidence.");
                    StringAssert.Contains("docs/superpowers/audits/2026-05-21-dedicated-core-gameplay-matrix-evidence-refresh.md", catalog, "Dedicated core matrix catalog row must reference the latest matrix evidence refresh audit.");
                    StringAssert.Contains("Builds/Logs/network-dedicated-core-gameplay-matrix-refresh", catalog, "Dedicated core matrix catalog row must reference the latest matrix evidence log directory.");
                }

                private static void AssertCategory(string catalog, string category)
        {
            StringAssert.Contains("| " + category + " |", catalog, "Catalog must include category " + category + ".");
        }
    }
}
