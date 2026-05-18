using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Scenarios
{
    public sealed class ScenarioCoverageMappingTests
    {
        private const string ScenarioIdPath = "Assets/Tests/PlayMode/Scenarios/ScenarioId.cs";
        private const string TownFlow001 = "TOWN-FLOW-001";
        private const string TownFlow002 = "TOWN-FLOW-002";
        private const string TownFlow003 = "TOWN-FLOW-003";
        private const string HouseFlow001 = "HOUSE-FLOW-001";
        private const string HouseFlow002 = "HOUSE-FLOW-002";
        private const string HouseFlow003 = "HOUSE-FLOW-003";
        private const string Coverage001 = "COVERAGE-001";

        [Test]
        public void COVERAGE_001_NewPlayerFlowScenarioIdsAreDeclared()
        {
            string scenarioIds = File.ReadAllText(ScenarioIdPath);

            StringAssert.Contains("TOWN_FLOW_001", scenarioIds);
            StringAssert.Contains(TownFlow001, scenarioIds);
            StringAssert.Contains("TOWN_FLOW_002", scenarioIds);
            StringAssert.Contains(TownFlow002, scenarioIds);
            StringAssert.Contains("TOWN_FLOW_003", scenarioIds);
            StringAssert.Contains(TownFlow003, scenarioIds);
            StringAssert.Contains("HOUSE_FLOW_001", scenarioIds);
            StringAssert.Contains(HouseFlow001, scenarioIds);
            StringAssert.Contains("HOUSE_FLOW_002", scenarioIds);
            StringAssert.Contains(HouseFlow002, scenarioIds);
            StringAssert.Contains("HOUSE_FLOW_003", scenarioIds);
            StringAssert.Contains(HouseFlow003, scenarioIds);
            StringAssert.Contains("COVERAGE_001", scenarioIds);
            StringAssert.Contains(Coverage001, scenarioIds);
        }

        [Test]
        public void COVERAGE_002_TownAndHouseScenarioIdsMapToConcretePlayModeTests()
        {
            string town = File.ReadAllText("Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs");
            string house = File.ReadAllText("Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs");

            StringAssert.Contains(TownFlow001, town);
            StringAssert.Contains(TownFlow002, town);
            StringAssert.Contains(TownFlow003, town);
            StringAssert.Contains(HouseFlow001, house);
            StringAssert.Contains(HouseFlow002, house);
            StringAssert.Contains(HouseFlow003, house);
        }

        [Test]
        public void COVERAGE_003_AuditDocumentMapsPlayerFlowVerificationGate()
        {
            const string auditPath = "docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md";
            Assert.IsTrue(File.Exists(auditPath), "The player-flow scenario coverage audit must be written before completion.");
            string audit = File.ReadAllText(auditPath);

            StringAssert.Contains("Town Core Player Flow", audit);
            StringAssert.Contains("House/Interiors Player Flow", audit);
            StringAssert.Contains("Direct Visual Play Verification Gate", audit);
            StringAssert.Contains(TownFlow001, audit);
            StringAssert.Contains(HouseFlow001, audit);
            StringAssert.Contains(Coverage001, audit);
        }

        [Test]
        public void COVERAGE_004_TownCoreFlowWritesRequiredEvidencePaths()
        {
            string town = File.ReadAllText("Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs");

            StringAssert.Contains("town-core-flow-objective-journal.png", town);
            StringAssert.Contains("town-core-flow-day-result.png", town);
            StringAssert.Contains("production", town);
            StringAssert.Contains("qa", town);
            StringAssert.Contains("evidence", town);
        }

        [Test]
        public void COVERAGE_005_HouseFlowWritesBeforeAndAfterReloadEvidencePaths()
        {
            string house = File.ReadAllText("Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs");

            StringAssert.Contains("house-interior-placement-before-reload.png", house);
            StringAssert.Contains("house-interior-placement-after-reload.png", house);
            StringAssert.Contains("house-interior-placement-before-reload-probe.txt", house);
            StringAssert.Contains("house-interior-placement-after-reload-probe.txt", house);
        }
    }
}
