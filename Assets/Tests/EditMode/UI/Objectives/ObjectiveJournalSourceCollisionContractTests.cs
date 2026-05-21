using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalSourceCollisionContractTests
    {
        private const string RegressionE2EPath = "Assets/Tests/PlayMode/EndToEnd/ObjectiveJournalRegressionE2EScenarioTests.cs";
        private const string MatrixPath = "Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalSourceRegressionMatrixTests.cs";
        private const string CatalogPath = "Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalSourceCatalogTests.cs";
        private const string ScreenshotPath = "objective-journal-source-collision-e2e.png";

        [Test]
        public void OBJECTIVE_JOURNAL_SOURCE_COLLISION_EDIT_001_PlayerFacingRegressionWritesCanonicalScreenshotEvidence()
        {
            Assert.IsTrue(File.Exists(RegressionE2EPath), "Objective Journal source collision coverage may reuse the existing regression E2E, but the file must exist.");
            string source = File.ReadAllText(RegressionE2EPath);

            StringAssert.Contains(ScreenshotPath, source, "The player-facing Objective Journal source collision scenario must write a canonical screenshot evidence file.");
            StringAssert.Contains("OBJECTIVE_JOURNAL_SOURCE_COLLISION_PM_001", source, "The reused regression scenario must be discoverable as source-collision coverage.");
            StringAssert.Contains("PressTab", source, "The scenario must open Objective Journal through the player-facing Tab path.");
            StringAssert.Contains("TrackedObjectiveHud", source, "The scenario must verify compact tracked HUD ownership.");
            StringAssert.Contains("duplicate", source.ToLowerInvariant(), "The scenario must explicitly guard against duplicate rows or HUDs.");
        }

        [Test]
        public void OBJECTIVE_JOURNAL_SOURCE_COLLISION_EDIT_002_MatrixCoversRequiredSourceFamiliesAndCompletedRouteFiltering()
        {
            Assert.IsTrue(File.Exists(MatrixPath), "Objective Journal source collision matrix test file must exist.");
            string source = File.ReadAllText(MatrixPath);

            StringAssert.Contains("daily-plan", source);
            StringAssert.Contains("daily-plan-route-hints", source);
            StringAssert.Contains("milestones", source);
            StringAssert.Contains("career-interests", source);
            StringAssert.Contains("quest-chain-log", source);
            StringAssert.Contains("completed-route", source, "The matrix must document that completed Daily Plan routes are filtered from active guidance.");
            StringAssert.Contains("first registered source owns", source, "The matrix must lock duplicate stable-key ownership semantics.");
        }

        [Test]
        public void OBJECTIVE_JOURNAL_SOURCE_COLLISION_EDIT_003_DefaultCatalogContainsEveryReviewedGoalLikeSourceFamily()
        {
            Assert.IsTrue(File.Exists(CatalogPath), "Objective Journal source catalog tests must exist.");
            string source = File.ReadAllText(CatalogPath);

            StringAssert.Contains("student-day-guide", source);
            StringAssert.Contains("daily-plan", source);
            StringAssert.Contains("daily-plan-route-hints", source);
            StringAssert.Contains("quest-log", source);
            StringAssert.Contains("quest-chain-log", source);
            StringAssert.Contains("campaign-progress", source);
            StringAssert.Contains("milestones", source);
            StringAssert.Contains("career-interests", source);
        }
    }
}
