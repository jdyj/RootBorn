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
            AssertCategory(catalog, "HEIR");
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_002_CatalogMapsModernActiveScenariosToConcreteTestsOrRunners()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
            string catalog = File.ReadAllText(CatalogPath);

            StringAssert.Contains("TOWN-FLOW-001", catalog);
            StringAssert.Contains("TownExplorationDiscoveryE2EScenarioTests", catalog);
            StringAssert.Contains("LIFE-STUDENT-001", catalog);
            StringAssert.Contains("StudentLifeActivityTests", catalog);
            StringAssert.Contains("CAREER-CANDIDATE-001", catalog);
            StringAssert.Contains("CareerCandidateDeepeningLoopE2EScenarioTests", catalog);
            StringAssert.Contains("REL-FIRST-GUIDE-001", catalog);
            StringAssert.Contains("FirstGuideRelationshipMilestoneE2EScenarioTests", catalog);
            StringAssert.Contains("NET-SMOKE-001", catalog);
            StringAssert.Contains("Scripts/qa/run-dedicated-multiplayer-smoke.ps1", catalog);
            StringAssert.Contains("NET-HOST-001", catalog);
            StringAssert.Contains("NetworkSessionHostPlayModeTests", catalog);
        }

        [Test]
        public void SCENARIO_REGISTRY_EDIT_003_CatalogMapsResourceRecipeScenariosAfterFeatureAndDedicatedSmoke()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
            string catalog = File.ReadAllText(CatalogPath);

            StringAssert.Contains("RESOURCE-RECIPE-001", catalog, "Resource/Recipe PlayMode scenario must be active after the Town activity slice landed.");
            StringAssert.Contains("TownResourceRecipeActivityE2EScenarioTests", catalog);
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
        public void SCENARIO_REGISTRY_EDIT_012_CatalogActivatesKnowledgeStatusActivityChainScenario()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "The current Student Life scenario coverage catalog must exist.");
            string catalog = File.ReadAllText(CatalogPath);

            StringAssert.Contains("| Active | KNOW-STATUS-ACTIVITY-001 | KNOW | Knowledge/Status Activity Chain | PlayMode | Assets/Tests/PlayMode/EndToEnd/KnowledgeStatusActivityChainE2EScenarioTests.cs |", catalog, "Knowledge/Status activity chain must be active catalog evidence after the PlayMode scenario landed.");
            StringAssert.Contains("KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate", catalog);
            StringAssert.Contains("Builds/Logs/knowledge-status-activity-chain-e2e.png", catalog);
            StringAssert.DoesNotContain("| Deferred | KNOW-STATUS-ACTIVITY-001 |", catalog, "Knowledge/Status activity chain must not remain deferred after executable PlayMode coverage landed.");
        }

        private static void AssertCategory(string catalog, string category)
        {
            StringAssert.Contains("| " + category + " |", catalog, "Catalog must include category " + category + ".");
        }
    }
}
