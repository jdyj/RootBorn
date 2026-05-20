using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalOwnershipE2EContractTests
    {
        private const string E2EPath = "Assets/Tests/PlayMode/EndToEnd/ObjectiveJournalOwnershipE2EScenarioTests.cs";

        [Test]
        public void OBJECTIVE_JOURNAL_OWNERSHIP_CONTRACT_001_E2ERequiresStrictOwnershipEvidence()
        {
            Assert.IsTrue(File.Exists(E2EPath), "Objective Journal ownership E2E must exist at " + E2EPath + ".");

            string source = File.ReadAllText(E2EPath);
            StringAssert.Contains("OBJECTIVE_JOURNAL_OWNERSHIP_E2E_001_NoLegacyPersistentGoalHud", source, "Ownership E2E must expose the named strict scenario.");
            StringAssert.Contains("Builds/Logs/objective-journal-ownership-e2e.png", source, "Ownership E2E must capture screenshot evidence.");
            StringAssert.Contains("PressTab", source, "Ownership E2E must open Objective Journal through the player-facing Tab input.");
            StringAssert.Contains("AssertNoLegacyObjectiveHudObjects", source, "Ownership E2E must assert legacy persistent objective HUD roots are absent.");
            StringAssert.Contains("AssertObjectiveRowsContainExactlyOnce", source, "Ownership E2E must assert visible Objective Journal rows are not duplicated.");
            StringAssert.Contains("AssertOneTrackedHud", source, "Ownership E2E must assert there is exactly one TrackedObjectiveHud.");
            StringAssert.Contains("AssertStableKeysUnique", source, "Ownership E2E must assert stable keys stay unique after refresh and reload.");
            StringAssert.Contains("SaveSlotCard_" , source, "Ownership E2E must start from the MainMenu SaveSlot flow.");
            StringAssert.Contains("LoadButton", source, "Ownership E2E must reload the same save slot.");
        }
    }
}
