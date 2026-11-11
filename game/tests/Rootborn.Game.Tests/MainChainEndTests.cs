using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// Unity MainChainOpeningE2EScenarioTests 007~011을 화면 없이 명령으로 따라간다(자동 테스트): 집에 가구 놓기로 체인 완료,
    /// 알바로 번 용돈으로 가구 사기, 산 가구로 완료, 장래희망 고르기, 같은 날 두 번째 바꾸기 거절. 방 칸은 실제 집 맵에서 만든다.
    /// </summary>
    public sealed class MainChainEndTests
    {
        private const string ChainId = MainChainTests.ChainId;
        private const string FreeDesk = "growth31.assembly.desk";
        private const string Shop = "location.corner-store";

        private static GameSession Same(GameSession game) => game;

        private static GameSession ToMyOwnSpace(TempSaves saves)
        {
            GameSession game = TestWorld.Start(saves.Store);
            var steps = new List<string>();
            game = MainChainTests.PlayToMyDream(game, () => game, steps);
            Assert.That(game.PerformLocationActivity("opening.clue.archive-door", "location-activity:archive-door:day3").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(StepOf(game), Is.EqualTo("mainquest.MyOwnSpace"));
            return game;
        }

        private static GameSession ToMyDream(TempSaves saves)
        {
            GameSession game = TestWorld.Start(saves.Store);
            game = MainChainTests.PlayToMyDream(game, () => game, new List<string>());
            Assert.That(StepOf(game), Is.EqualTo("mainquest.MyDream"));
            return game;
        }

        private static string StepOf(GameSession game) => game.QuestChains.GetCurrentStep(game.Chain(ChainId))?.Id;
        private static QuestChainState StateOf(GameSession game) => game.QuestChains.GetState(game.Chain(ChainId));

        private static bool Place(GameSession game, string furnitureId)
            => game.PlaceFurniture(furnitureId, HouseRoom.FirstFreeAnchor(game, furnitureId), FurniturePlacementDirection.North);

        [Test]
        public void Step007_PlacingAcceptedFurnitureAtHome_CompletesTheChain()
        {
            using var saves = new TempSaves();
            var game = ToMyOwnSpace(saves);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(StateOf(game), Is.EqualTo(QuestChainState.Tracked), "들어오기만 해서는 끝나지 않는다");

            Assert.That(Place(game, FreeDesk), Is.True);
            Assert.That(StateOf(game), Is.EqualTo(QuestChainState.Completed));
            Assert.That(StateOf(TestWorld.Start(saves.Store)), Is.EqualTo(QuestChainState.Completed), "다시 읽어도 완료");
        }

        [Test]
        public void Step007_FurnitureThatTheStepDoesNotAccept_DoesNotComplete()
        {
            using var saves = new TempSaves();
            var game = ToMyOwnSpace(saves);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            var accepted = game.QuestChains.GetCurrentStep(game.Chain(ChainId)).Objectives.OfType<FurniturePlacedQuestObjective>().Single().AcceptedFurnitureIds;
            var other = game.AvailableFurniture().First(f => !accepted.Contains(f.StableId));

            Assert.That(Place(game, other.StableId), Is.True);
            Assert.That(StateOf(game), Is.EqualTo(QuestChainState.Tracked));
        }

        [Test]
        public void Step008And010_ShiftsPayForFurniture_BoughtOnce_ThenPlacingItCompletesTheChain()
        {
            using var saves = new TempSaves();
            var game = ToMyOwnSpace(saves);
            var offer = game.FurnitureForSaleAt(Shop)[0];
            var objective = game.QuestChains.GetCurrentStep(game.Chain(ChainId)).Objectives.OfType<FurniturePlacedQuestObjective>().Single();
            Assert.That(objective.AcceptedFurnitureIds, Does.Contain(offer.StableId), "MyOwnSpace가 가게 가구를 받는다");
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.AvailableFurniture().Any(f => f.StableId == offer.StableId), Is.False, "사기 전에는 목록에 없다");

            var allowance = game.Item("item.allowance");
            for (int shift = 0; game.Inventory.CountOf(allowance) < offer.Price; shift++)
            {
                Assert.That(shift, Is.LessThan(10), "알바로 값을 모으지 못했다");
                int before = game.Inventory.CountOf(allowance);
                string request = "work:corner-store:shift-" + shift;
                if (!game.BeginPartTimeWork("work.shift.corner-store", request, shift).Applied)
                {
                    // 오늘 더 할 수 없으면 자고 다음 날 한다.
                    Assert.That(game.EndDay().Ended, Is.True);
                    Assert.That(game.StartNextDay(), Is.True);
                    Assert.That(game.BeginPartTimeWork("work.shift.corner-store", request, shift).Applied, Is.True);
                }

                Assert.That(game.SettleMinigame(request, true).Applied, Is.True);
                Assert.That(game.Inventory.CountOf(allowance), Is.GreaterThan(before), "알바가 용돈을 주지 않았다");
            }

            int money = game.Inventory.CountOf(allowance);
            Assert.That(game.PurchaseFurniture(offer.StableId), Is.EqualTo(FurniturePurchaseResult.Purchased));
            Assert.That(game.Inventory.CountOf(allowance), Is.EqualTo(money - offer.Price), "값은 한 번만 낸다");
            Assert.That(game.PurchaseFurniture(offer.StableId), Is.EqualTo(FurniturePurchaseResult.AlreadyOwned));
            Assert.That(game.Inventory.CountOf(allowance), Is.EqualTo(money - offer.Price));

            game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.AvailableFurniture()[0].StableId, Is.EqualTo(offer.StableId), "산 가구가 목록 맨 앞");
            Assert.That(Place(game, offer.StableId), Is.True);
            Assert.That(StateOf(game), Is.EqualTo(QuestChainState.Completed));
        }

        [Test]
        public void FurniturePlacedBeforeTheStep_CountsWhenTheNextDayStarts()
        {
            using var saves = new TempSaves();
            var game = ToMyDream(saves);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(Place(game, FreeDesk), Is.True);
            Assert.That(StepOf(game), Is.EqualTo("mainquest.MyDream"), "MyDream은 가구를 받지 않는다");

            Assert.That(game.PerformLocationActivity("opening.clue.archive-door", "location-activity:archive-door:day3").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(StepOf(game), Is.EqualTo("mainquest.MyOwnSpace"));
            Assert.That(StateOf(game), Is.EqualTo(QuestChainState.Tracked), "일찍 놓은 가구는 바로 세지 않는다");

            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
            Assert.That(StateOf(game), Is.EqualTo(QuestChainState.Completed), "새 날이 시작되면 놓인 가구를 다시 알린다");
            Assert.That(game.QuestLog.ToSaveData(), Is.Not.Null);
        }

        [Test]
        public void FurniturePlacedBeforeTheStep_CountsWhenEnteringTheHouseAgain()
        {
            using var saves = new TempSaves();
            var game = ToMyDream(saves);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(Place(game, FreeDesk), Is.True);
            game.PerformLocationActivity("opening.clue.archive-door", "location-activity:archive-door:day3");

            var again = TestWorld.Start(saves.Store);
            Assert.That(StateOf(again), Is.EqualTo(QuestChainState.Tracked));
            again.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(StateOf(again), Is.EqualTo(QuestChainState.Completed));
            Assert.That(StateOf(TestWorld.Start(saves.Store)), Is.EqualTo(QuestChainState.Completed), "다시 알린 결과도 저장된다");
        }

        [Test]
        public void ReplayingWhenNoStepWantsFurniture_LeavesTheChainUntouched()
        {
            using var saves = new TempSaves();
            var game = ToMyDream(saves);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(Place(game, FreeDesk), Is.True);
            var before = game.QuestChains.ToSaveData().Chains.Single(c => c.ChainId == ChainId).ProcessedEventKeys.Length;

            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.QuestChains.ToSaveData().Chains.Single(c => c.ChainId == ChainId).ProcessedEventKeys.Length, Is.EqualTo(before),
                "기다리는 단계가 없으면 다시 알리지 않는다");
        }

        [Test]
        public void Step009_ChoosingADreamAtMyDream_IsKeptAndMovesTheChainOn()
        {
            using var saves = new TempSaves();
            var game = ToMyDream(saves);
            var introduction = game.LocationActivitiesAt("location.restaurant")[0].Activity.Id;
            Assert.That(game.PerformLocationActivity(introduction, "location-activity:restaurant:intro").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            var dream = game.CareerInterestChoices().FirstOrDefault(c => c.Evaluation.Allowed).Interest;
            Assert.That(dream, Is.Not.Null, "식당 소개를 들으면 장래희망 하나를 고를 수 있다");

            Assert.That(game.SelectCareerInterest(dream.Id, "interest:first").Applied, Is.True);
            Assert.That(game.CareerInterests.CurrentInterestId, Is.EqualTo(dream.Id));
            Assert.That(StepOf(game), Is.EqualTo("mainquest.MyOwnSpace"), "장래희망을 고르면 MyDream이 끝난다");
            Assert.That(TestWorld.Start(saves.Store).CareerInterests.CurrentInterestId, Is.EqualTo(dream.Id));
        }

        [Test]
        public void Step011_ASecondDreamChangeTheSameDayIsRefused_AndStaysRefusedAfterReading()
        {
            using var saves = new TempSaves();
            var game = ToMyDream(saves);
            foreach (var location in new[] { "location.restaurant", "location.medical-clinic" })
                Assert.That(game.PerformLocationActivity(game.LocationActivitiesAt(location)[0].Activity.Id, "intro:" + location).Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            var allowed = game.CareerInterestChoices().Where(c => c.Evaluation.Allowed).Select(c => c.Interest).ToArray();
            Assert.That(allowed.Length, Is.GreaterThanOrEqualTo(2), "두 소개 뒤에는 두 장래희망을 고를 수 있다");

            Assert.That(game.SelectCareerInterest(allowed[0].Id, "interest:first").Applied, Is.True);
            Assert.That(game.SelectCareerInterest(allowed[1].Id, "interest:second").Applied, Is.False);
            Assert.That(game.CareerInterests.CurrentInterestId, Is.EqualTo(allowed[0].Id));
            Assert.That(game.CareerInterestChoices().Single(c => c.Interest.Id == allowed[1].Id).Evaluation.Allowed, Is.False);

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.CareerInterests.CurrentInterestId, Is.EqualTo(allowed[0].Id));
            Assert.That(again.SelectCareerInterest(allowed[1].Id, "interest:after-reading").Applied, Is.False, "다시 읽어도 같은 날 제한은 남는다");
            Assert.That(again.CareerInterests.CurrentInterestId, Is.EqualTo(allowed[0].Id));
        }

        [Test]
        public void MainChain_RunsToCompletion_TheSameWhenEveryCommandIsReadAgain()
        {
            using var straightSaves = new TempSaves();
            GameSession straight = TestWorld.Start(straightSaves.Store);
            var expected = PlayToEnd(straight, Same);

            using var saves = new TempSaves();
            GameSession current = TestWorld.Start(saves.Store);
            var actual = PlayToEnd(current, _ => current = TestWorld.Start(saves.Store));

            Assert.That(actual.Steps, Is.EqualTo(expected.Steps));
            Assert.That(actual.Final, Is.EqualTo(expected.Final));
            Assert.That(actual.Final, Does.Contain(QuestChainState.Completed.ToString()));
        }

        private static MainChainTests.Outcome PlayToEnd(GameSession game, Func<GameSession, GameSession> between)
        {
            var outcome = MainChainTests.Play(game, () => game = between(game));
            game = between(game);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(Place(game, FreeDesk), Is.True);
            game = between(game);
            Assert.That(StateOf(game), Is.EqualTo(QuestChainState.Completed));
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            return new MainChainTests.Outcome { Steps = outcome.Steps.Concat(new[] { StateOf(game).ToString() }).ToArray(), Final = MainChainTests.Fingerprint(game) };
        }
    }
}
