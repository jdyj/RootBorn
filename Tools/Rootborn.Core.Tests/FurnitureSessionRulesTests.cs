// 옮겨 옴: Assets/Scripts/UI/Interiors/AuthoredHousePlacementBinding.cs(가구 배치 이벤트, 놓인 가구 다시 알리기),
//          Assets/Scripts/Game/Housing/HouseStatePersistence.cs(집 단계), InteriorFurnitureCatalog(목록에 보이는 가구), 가게 판매 목록
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;

namespace Rootborn.Core.Tests
{
    public sealed class FurnitureSessionRulesTests
    {
        private static readonly ItemDefinition Coin = new ItemDefinition("item.test-coin", 9999);

        private static FurnitureDefinition Piece(string id, string token = null, int price = 0, string location = null)
            => new FurnitureDefinition(id, id, null, "Decor", new[] { "house" }, new[] { new FurnitureTilePart(new GridPoint(0, 0), "a.png", "a") },
                null, false, FurniturePlacementPreference.Any, token, price > 0 ? Coin : null, price, location);

        private static HouseFurnitureLayout Room()
        {
            var floor = Enumerable.Range(0, 5).SelectMany(x => Enumerable.Range(0, 5).Select(y => new GridPoint(x, y)));
            return new HouseFurnitureLayout(new HousePlacementModel(new PlacementGrid(floor, null, new GridPoint(0, 0), null)), "house");
        }

        // --- 목록과 판매 ---

        [Test]
        public void IsAvailable_FreePiecesAndUnlockedPiecesOnly()
        {
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            var free = Piece("free");
            var locked = Piece("locked", "unlock.locked");

            Assert.That(FurnitureCatalog.IsAvailable(free, null), Is.True);
            Assert.That(FurnitureCatalog.IsAvailable(locked, unlocks), Is.False);
            Assert.That(FurnitureCatalog.IsAvailable(locked, null), Is.False);
            Assert.That(FurnitureCatalog.IsAvailable(null, unlocks), Is.False);
            unlocks.TryUnlock("unlock.locked");
            Assert.That(FurnitureCatalog.IsAvailable(locked, unlocks), Is.True);
        }

        [Test]
        public void ForSaleAt_ListsOnlyPiecesSoldAtThatPlace_InCatalogOrder()
        {
            var catalog = new[]
            {
                Piece("b.sold-here", "unlock.b", 10, "location.shop"),
                Piece("free"),
                Piece("elsewhere", "unlock.c", 10, "location.other"),
                Piece("a.sold-here", "unlock.a", 5, "location.shop"),
                Piece("no-price", "unlock.d", 0, "location.shop"),
            };

            Assert.That(FurnitureCatalog.ForSaleAt(catalog, "location.shop").Select(f => f.StableId), Is.EqualTo(new[] { "b.sold-here", "a.sold-here" }));
            Assert.That(FurnitureCatalog.ForSaleAt(catalog, ""), Is.Empty);
            Assert.That(FurnitureCatalog.ForSaleAt(null, "location.shop"), Is.Empty);
        }

        // --- 집 단계 ---

        [Test]
        public void HouseStage_IsZeroWithoutAFile_AndNeverNegative()
        {
            var store = new InMemorySaveStore();
            Assert.That(FurnitureSaveFiles.LoadHouseStage(store, "slot"), Is.EqualTo(0));

            store.Put("slot", SaveFileNames.HouseState, new HouseStateSaveData { CurrentStageIndex = -3 });
            Assert.That(FurnitureSaveFiles.LoadHouseStage(store, "slot"), Is.EqualTo(0));

            store.Put("slot", SaveFileNames.HouseState, new HouseStateSaveData { CurrentStageIndex = 2 });
            Assert.That(FurnitureSaveFiles.LoadHouseStage(store, "slot"), Is.EqualTo(2));
            Assert.That(FurnitureSaveFiles.LoadHouseStage(null, "slot"), Is.EqualTo(0));
            Assert.That(SaveFileNames.HouseState, Is.EqualTo("house-state.json"), "Unity HouseStatePersistence.FileName");
        }

        // --- 퀘스트 이벤트 ---

        [Test]
        public void PlacedEvent_NamesThePieceAndTheSurface()
        {
            var placed = FurnitureQuestEvents.Placed(Piece("desk"), "house", "key-1");

            Assert.That(placed.Kind, Is.EqualTo(QuestEventKind.FurniturePlaced));
            Assert.That(placed.EventKey, Is.EqualTo("key-1"));
            Assert.That(placed.SubjectId, Is.EqualTo("desk"));
            Assert.That(placed.SurfaceId, Is.EqualTo("house"));
            Assert.That(placed.Count, Is.EqualTo(1));
            Assert.That(placed.IsReplay, Is.False);
        }

        [Test]
        public void Replays_OneEventPerDistinctPlacedPiece_InPlacedOrder_MarkedAsReplay()
        {
            var room = Room();
            var desk = Piece("desk");
            var lamp = Piece("lamp");
            Assert.That(room.TryPlace(desk, new GridPoint(2, 2), FurniturePlacementDirection.North), Is.True);
            Assert.That(room.TryPlace(lamp, new GridPoint(3, 3), FurniturePlacementDirection.North), Is.True);
            Assert.That(room.TryPlace(desk, new GridPoint(4, 4), FurniturePlacementDirection.North), Is.True);
            int n = 0;

            var replays = FurnitureQuestEvents.Replays(room, action => action + ":" + ++n);

            Assert.That(replays.Select(e => e.SubjectId), Is.EqualTo(new[] { "desk", "lamp" }));
            Assert.That(replays.All(e => e.IsReplay && e.SurfaceId == "house" && e.Kind == QuestEventKind.FurniturePlaced), Is.True);
            Assert.That(replays.Select(e => e.EventKey), Is.EqualTo(new[] { "furniture-placed:1", "furniture-placed:2" }));
            Assert.That(FurnitureQuestEvents.Replays(null, a => a), Is.Empty);
        }

        [Test]
        public void AnyCurrentStepWantsFurniture_LooksAtEachChainsCurrentStep()
        {
            var wantsFurniture = new QuestChainDefinition("chain.room", "n", "d", "", new[]
            {
                new QuestStepDefinition("step.talk", "n", "d", new QuestObjective[] { new TalkQuestObjective("talk", 1, "npc.mom") }),
                new QuestStepDefinition("step.room", "n", "d", new QuestObjective[] { new FurniturePlacedQuestObjective("room", 1, new[] { "desk" }, "house") }),
            });
            var talkOnly = new QuestChainDefinition("chain.talk", "n", "d", "", new[]
            {
                new QuestStepDefinition("step.talk", "n", "d", new QuestObjective[] { new TalkQuestObjective("talk", 1, "npc.mom") }),
            });
            var chains = new[] { wantsFurniture, talkOnly };
            var log = new QuestChainLog(chains);
            log.Accept(wantsFurniture);

            Assert.That(FurnitureQuestEvents.AnyCurrentStepWantsFurniture(log, chains), Is.False, "첫 단계는 대화");

            log.RecordEventWithContext(new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.mom"), default, default);
            log.EvaluateStepProgress(wantsFurniture, default, default);
            Assert.That(log.GetCurrentStep(wantsFurniture).Id, Is.EqualTo("step.room"));
            Assert.That(FurnitureQuestEvents.AnyCurrentStepWantsFurniture(log, chains), Is.True);
            Assert.That(FurnitureQuestEvents.AnyCurrentStepWantsFurniture(null, chains), Is.False);
            Assert.That(FurnitureQuestEvents.AnyCurrentStepWantsFurniture(log, null), Is.False);
        }
    }
}
