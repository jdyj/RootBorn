using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// Unity MainChainOpeningE2EScenarioTests 001~006의 흐름을 화면 없이 명령으로 따라간다(자동 테스트).
    /// 007(집에 가구 놓기)은 Core에 가구 배치 영역이 아직 없어 MyOwnSpace 단계에 도착하는 데까지만 간다.
    /// </summary>
    public sealed class MainChainTests
    {
        public const string ChainId = "main-story.school-opening";

        private static readonly string[] TourLocations =
            { "location.restaurant", "location.medical-clinic", "location.design-studio", "location.training-ground" };

        [Test]
        public void MainChain_RunsFromTalkingToMomToMyOwnSpace()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            GameSession current = game;
            var outcome = Play(current, () => current);

            Assert.That(outcome.Steps, Is.EqualTo(new[]
            {
                "mainquest.BackToSchool", "mainquest.AfterSchoolTour", "mainquest.FirstPartTimeJob",
                "mainquest.FirstPartTimeJob", "mainquest.MyDream", "mainquest.MyOwnSpace",
            }));
            Assert.That(game.Student.CurrentDay, Is.EqualTo(3));
            Assert.That(game.QuestChains.GetState(game.Chain(ChainId)), Is.EqualTo(QuestChainState.Tracked));
        }

        [Test]
        public void MainChain_SameResultWhenSavedAndReadAgainBetweenSteps()
        {
            using var straightSaves = new TempSaves();
            GameSession straight = TestWorld.Start(straightSaves.Store);
            var expected = Play(straight, () => straight);

            using var saves = new TempSaves();
            GameSession current = TestWorld.Start(saves.Store);
            // 명령마다 새 GameSession으로 읽어 이어 간다.
            var actual = Play(current, () => current = TestWorld.Start(saves.Store));

            Assert.That(actual.Steps, Is.EqualTo(expected.Steps));
            Assert.That(actual.Final, Is.EqualTo(expected.Final));
        }

        [Test]
        public void Step001_TalkingToMomStartsTheChain_PlanAndTeacherFillBothObjectives()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var chain = game.Chain(ChainId);

            game.TalkTo("npc.first-guide");
            Assert.That(game.QuestChains.GetState(chain), Is.EqualTo(QuestChainState.Tracked));

            Assert.That(game.SelectDailyPlanRoute(game.AvailableDailyPlanRoutes().First().Id), Is.True);
            game.TalkTo("npc.teacher");

            Assert.That(game.QuestChains.GetObjectiveProgress(chain, 0), Is.EqualTo(1), "선생님과 대화");
            Assert.That(game.QuestChains.GetObjectiveProgress(chain, 1), Is.EqualTo(1), "일과 고르기");
            Assert.That(game.QuestChains.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.BackToSchool"), "2일째 전에는 넘어가지 않는다");
        }

        [Test]
        public void PlanChosenBeforeTalkingToMom_StillCounts()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.SelectDailyPlanRoute(game.AvailableDailyPlanRoutes().First().Id);
            game.TalkTo("npc.first-guide");

            Assert.That(game.QuestChains.GetObjectiveProgress(game.Chain(ChainId), 1), Is.EqualTo(1));
        }

        [Test]
        public void TrackedObjective_FollowsTheChainStep()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.TrackedObjective().HasChain, Is.False);

            game.TalkTo("npc.first-guide");
            var tracked = game.TrackedObjective();
            Assert.That(tracked.ChainId, Is.EqualTo(ChainId));
            Assert.That(tracked.StepId, Is.EqualTo("mainquest.BackToSchool"));
            Assert.That(tracked.Objectives.Select(o => o.Required), Is.EqualTo(new[] { 1, 1 }));
        }

        internal sealed class Outcome
        {
            public string[] Steps;
            public string Final;
        }

        /// <summary>메인 체인을 MyOwnSpace까지 진행한다. between은 명령 사이마다 부르고 이어 갈 GameSession을 돌려준다.</summary>
        internal static Outcome Play(GameSession game, Func<GameSession> between)
        {
            var steps = new List<string>();
            string Step() => game.QuestChains.GetCurrentStep(game.Chain(ChainId)).Id;

            // 001: 집에서 엄마 → 일과 → 학교 선생님.
            game.TalkTo("npc.first-guide");
            game = between();
            Assert.That(game.SelectDailyPlanRoute(game.AvailableDailyPlanRoutes().First().Id), Is.True);
            game = between();
            game.TalkTo("npc.teacher");
            game = between();
            steps.Add(Step());

            // 002: 잠을 자고 다음 날.
            Assert.That(game.EndDay().Ended, Is.True);
            game = between();
            Assert.That(game.StartNextDay(), Is.True);
            game = between();
            steps.Add(Step());

            // 003: 일터 네 곳 둘러보기.
            foreach (var location in TourLocations)
            {
                game.VisitLocation(location);
                game = between();
            }
            steps.Add(Step());

            // 004: 가게 알바를 미니게임으로 한 번.
            Assert.That(game.BeginPartTimeWork("work.shift.corner-store", "work:corner-store:day2", 7).Applied, Is.True);
            game = between();
            Assert.That(game.SettleMinigame("work:corner-store:day2", true).Applied, Is.True);
            game = between();
            steps.Add(Step());

            // 005: 자고 3일째.
            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
            game = between();
            steps.Add(Step());

            // 006: 도서관에서 직업 책 읽기.
            Assert.That(game.PerformLocationActivity("opening.clue.archive-door", "location-activity:archive-door:day3").Kind,
                Is.EqualTo(LifeActivityResultKind.Applied));
            game = between();
            steps.Add(Step());

            var student = game.Student;
            var traits = string.Join(",", student.GetTraitIds().OrderBy(id => id, StringComparer.Ordinal).Select(id => id + "=" + student.GetTraitValue(id)));
            var final = string.Join("|", student.CurrentDay, student.Energy, student.Focus, student.Stress, student.TimeMinutes,
                student.TimePointsSpentToday, traits, game.Inventory.CountOf(game.Item("item.allowance")),
                game.QuestChains.GetState(game.Chain(ChainId)), string.Join(",", game.Campaign.ToSaveData().CompletedObjectiveIds));
            return new Outcome { Steps = steps.ToArray(), Final = final };
        }
    }
}
