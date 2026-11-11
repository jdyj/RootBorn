// 원래 파일: Assets/Tests/PlayMode/EndToEnd/TownHelpActionGrowthE2EScenarioTests.cs (HELP_001),
//            Assets/Tests/PlayMode/EndToEnd/FirstGuideRelationshipMilestoneE2EScenarioTests.cs (REL_001)
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 엄마 심부름(마을 돕기)과 광장 도움 요청(장소 활동)이 학교 밖에서 책임감과 엄마 신뢰를 키운다. 돕기마다 한 번씩, 저장하고 읽어도 그대로다.
    /// 자동 테스트다.
    /// </summary>
    public sealed class HelpAndTrustTests
    {
        private const string Errand = "town.help.first-guide-errand";
        private const string Responsibility = "trait.responsibility";
        private const string Trust = "relationship.first-guide.trust";
        private const string SquareHelp = "location.activity.square-help-request";

        [Test]
        public void Help001_TheErrandForMom_GrowsResponsibilityAndTrust_OncePerHelp()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var action = game.Definitions.Get<TownHelpAction>(Errand);
            int traitStart = game.Student.GetTraitValue(Responsibility);
            int trustStart = game.Student.GetRelationshipValue(Trust);
            int energyStart = game.Student.Energy;

            Assert.That(game.PerformTownHelp(Errand, "help:errand:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            int traitAfter = game.Student.GetTraitValue(Responsibility);
            int trustAfter = game.Student.GetRelationshipValue(Trust);
            Assert.That(traitAfter, Is.GreaterThan(traitStart));
            Assert.That(trustAfter, Is.GreaterThan(trustStart));
            Assert.That(game.Student.Energy, Is.EqualTo(energyStart - action.EnergyCost));

            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.GetTraitValue(Responsibility), Is.EqualTo(traitAfter), "다시 읽어도 한 번만");
            Assert.That(game.Student.GetRelationshipValue(Trust), Is.EqualTo(trustAfter));
            Assert.That(game.PerformTownHelp(Errand, "help:errand:1").Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));

            Assert.That(game.PerformTownHelp(Errand, "help:errand:2").Kind, Is.EqualTo(LifeActivityResultKind.Applied), "새 요청은 다시 돕는다");
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.GetTraitValue(Responsibility), Is.EqualTo(traitAfter + (traitAfter - traitStart)));
            Assert.That(game.Student.GetRelationshipValue(Trust), Is.EqualTo(trustAfter + (trustAfter - trustStart)));
        }

        [Test]
        public void Rel001_TalkToMomThenSquareHelp_RaisesTrustOncePerAction()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int trustStart = game.Student.GetRelationshipValue(Trust);
            Assert.That(game.TalkTo("npc.first-guide"), Is.Not.Null, "엄마와 대화");
            game.CloseDialogue();
            Assert.That(game.Student.GetRelationshipValue(Trust), Is.EqualTo(trustStart), "말만 걸어서는 신뢰가 오르지 않는다");

            game = TestWorld.Start(saves.Store);
            game.VisitLocation("location.town-square");
            Assert.That(game.PerformLocationActivity(SquareHelp, "square:help:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            int trustAfterFirst = game.Student.GetRelationshipValue(Trust);
            Assert.That(trustAfterFirst, Is.GreaterThan(trustStart));
            int delta = trustAfterFirst - trustStart;

            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.GetRelationshipValue(Trust), Is.EqualTo(trustAfterFirst));
            Assert.That(game.PerformLocationActivity(SquareHelp, "square:help:2").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(TestWorld.Start(saves.Store).Student.GetRelationshipValue(Trust), Is.EqualTo(trustAfterFirst + delta));
        }
    }
}
