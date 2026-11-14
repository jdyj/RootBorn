using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// TOWN-001 곁: 새 게임의 집에만 처음 배치 가구(data/building/HouseStarterLayout.json, 시험용 — 결정 대기 29번)가 놓인다.
    /// 세이브 모양은 늘리지 않는다 — 받음 기록 대신 집 배치 파일 자체가 기록이다(파일이 있으면 주지 않는다). 옛 세이브, 다른 플레이어가 논 슬롯,
    /// 같이 쓰는 집에는 주지 않고, 두 번 주지 않는다. 실제 data/와 실제 집 맵, 임시 폴더와 고정 파일 사본. GameSession 명령으로 진행하는 자동 테스트다(화면 근거 아님).
    /// </summary>
    public sealed class HouseStarterTests
    {
        private static HouseStarterLayout Starter => TestWorld.Definitions.All<HouseStarterLayout>().Single(s => s.SurfaceId == HouseRoom.SurfaceId);

        private static string LayoutPath(TempSaves saves, string slot = TestWorld.Slot)
            => Path.Combine(saves.Path, slot, SaveFileNames.HouseFurnitureLayout(0));

        private static string[] Placed(GameSession game)
            => game.House.Placed.Select(p => p.Definition.StableId + "@" + p.Anchor.X + "," + p.Anchor.Y + ":" + p.Direction).ToArray();

        private static string[] StarterLines()
            => Starter.Placements.Select(p => p.FurnitureId + "@" + p.Anchor.X + "," + p.Anchor.Y + ":" + p.Direction).ToArray();

        private static Dictionary<string, byte[]> SlotFiles(TempSaves saves, string slot = TestWorld.Slot)
        {
            var dir = Path.Combine(saves.Path, slot);
            return Directory.Exists(dir) ? Directory.GetFiles(dir).ToDictionary(Path.GetFileName, File.ReadAllBytes) : new Dictionary<string, byte[]>();
        }

        private static RoomShares SharedHouseRoom()
        {
            var rules = new RoomHouseRules(TestWorld.Definitions.All<FurnitureDefinition>(), s => s == HouseRoom.SurfaceId ? HouseRoom.Grid : null);
            var pieces = new RoomPieceSet(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(rules));
            var shares = new RoomShares(pieces.Settings, RoomPieceCatalog.CreateDefault(), () => "guest-r1");
            foreach (var p in pieces.Pieces) shares.Receive(p.State());
            return shares;
        }

        [Test]
        public void StarterFitsTheRealHouse()
        {
            // 데이터의 줄이 모두 실제 새 집에서 그 자리·방향에 놓인다(앞 줄을 놓은 뒤에). 데이터를 바꾸면 여기서 걸린다.
            var layout = new HouseFurnitureLayout(new HousePlacementModel(HouseRoom.Grid), HouseRoom.SurfaceId);
            foreach (var p in Starter.Placements)
            {
                Assert.That(TestWorld.Definitions.TryGet<FurnitureDefinition>(p.FurnitureId, out var furniture), Is.True, "데이터에 있는 가구: " + p.FurnitureId);
                Assert.That(layout.CanPlace(furniture, p.Anchor, p.Direction), Is.True, p.FurnitureId + " @ " + p.Anchor.X + "," + p.Anchor.Y);
                Assert.That(layout.TryPlace(furniture, p.Anchor, p.Direction), Is.True);
            }
        }

        [Test]
        public void NewGame_EnterHouse_StarterFurnitureIsPlaced()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);

            // 칸이 데이터대로다(StarterFitsTheRealHouse가 막히지 않음을 고정하므로 옮겨진 자리가 아니다).
            Assert.That(Placed(game), Is.EqualTo(StarterLines()));
            Assert.That(game.House.UnresolvedCount, Is.Zero);
            Assert.That(game.TryTakeHouseRelocationNotice(out _, out _), Is.False, "처음 배치는 '옛 가구를 옮겼다' 알림이 아니다");
        }

        [Test]
        public void NewGame_NoCommand_WritesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            game.EnterHouseBuild(HouseRoom.Frame);
            Assert.That(game.House.Placed.Count, Is.EqualTo(Starter.Placements.Count));
            Assert.That(SlotFiles(saves), Is.Empty, "명령 없이 끝낸 새 게임은 아무것도 쓰지 않는다");

            // 다음에도 새 게임이라 같은 결과다.
            var again = TestWorld.Start(saves.Store);
            again.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(Placed(again), Is.EqualTo(StarterLines()));
            Assert.That(SlotFiles(saves), Is.Empty);
        }

        [Test]
        public void NewGame_FirstSave_WritesStarter_NextSessionSame()
        {
            // 집을 열기 전에 저장해도 처음 배치가 쓰인다. 그 뒤로는 파일이 기록이라 다시 주지 않는다.
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.VisitLocation("location.town-square"); // 첫 저장(집을 열기 전)
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0), out var file), Is.True,
                "첫 저장 묶음에 집 배치 파일");
            Assert.That(file.Items.Select(r => r.FurnitureId + "@" + r.AnchorX + "," + r.AnchorY + ":" + r.Direction), Is.EqualTo(StarterLines()));
            byte[] written = File.ReadAllBytes(LayoutPath(saves));

            var next = TestWorld.Start(saves.Store);
            next.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(Placed(next), Is.EqualTo(StarterLines()), "새 판에서도 같은 배치, 가구가 늘지 않는다");
            next.VisitLocation("location.town-square");
            Assert.That(File.ReadAllBytes(LayoutPath(saves)), Is.EqualTo(written), "다시 쓰지 않는다");

            // 집을 연 뒤의 첫 저장도 같다(열린 배치를 쓴다).
            using var other = new TempSaves();
            var opened = TestWorld.Start(other.Store);
            opened.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            opened.VisitLocation("location.town-square");
            var reopened = TestWorld.Start(other.Store);
            reopened.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(Placed(reopened), Is.EqualTo(StarterLines()));
        }

        [Test]
        public void NewGame_ClearedStarter_IsNotGivenAgain()
        {
            // 처음 가구를 모두 치우면(빈 배치 파일) 다음 판에도 다시 주지 않는다.
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.ClearFurniture(), Is.True);
            game.VisitLocation("location.town-square");
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.House.Placed, Is.Empty, "같은 판에서 다시 들어와도");

            var next = TestWorld.Start(saves.Store);
            next.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(next.House.Placed, Is.Empty, "새 판에서도");
        }

        [TestCase("monogame-20261112/slot-robust", "slot-robust")]
        [TestCase("editor-20261105/slot-1", "slot-1")]
        public void OldFixtureSlots_GetNoStarter(string fixture, string slot)
        {
            using var saves = new TempSaves();
            saves.CopyFixture(fixture, slot);
            byte[] before = File.ReadAllBytes(LayoutPath(saves, slot));
            var expected = FurnitureSaveFiles.LoadHouseLayout(saves.Store, slot, 0).Select(r => r.FurnitureId).ToArray();

            var game = TestWorld.Start(saves.Store, slot);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.House.Placed.Select(p => p.Definition.StableId), Is.EquivalentTo(expected), "옛 가구만 있다");
            game.VisitLocation("location.town-square");
            Assert.That(File.ReadAllBytes(LayoutPath(saves, slot)), Is.EqualTo(before), "옛 배치 파일에 처음 가구를 더하지 않는다");
        }

        [Test]
        public void SecondPlayerInPlayedSlot_GetsNoStarter()
        {
            using var saves = new TempSaves();
            var first = TestWorld.Start(saves.Store);
            first.VisitLocation("location.town-square");
            byte[] written = File.ReadAllBytes(LayoutPath(saves));

            // 처음 플레이어가 받은 배치를 같이 보고, 두 번째 플레이어 몫으로 더 놓지 않는다.
            var second = TestWorld.Start(saves.Store, TestWorld.Slot, "player-2");
            second.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(Placed(second), Is.EqualTo(StarterLines()));
            second.VisitLocation("location.town-square");
            Assert.That(File.ReadAllBytes(LayoutPath(saves)), Is.EqualTo(written));

            // 배치 파일이 없는 논 슬롯(가구를 쓰지 않던 빌드에서 논 것처럼)에도 주지 않는다 — 슬롯 공용 파일이 있다.
            File.Delete(LayoutPath(saves));
            var third = TestWorld.Start(saves.Store, TestWorld.Slot, "player-3");
            third.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(third.House.Placed, Is.Empty);
            third.VisitLocation("location.town-square");
            Assert.That(File.Exists(LayoutPath(saves)), Is.False, "배치 파일을 새로 만들지 않는다");
        }

        [Test]
        public void SharedHouse_GetsNoStarter()
        {
            using var saves = new TempSaves();
            var guest = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-guest", "guest", shares: SharedHouseRoom());
            Assert.That(guest.SharesHouse, Is.True, "전제: 같이 쓰는 집");
            guest.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(guest.House.Placed, Is.Empty, "방의 집에 처음 가구를 넣지 않는다");
            guest.VisitLocation("location.town-square");
            Assert.That(File.Exists(LayoutPath(saves, "slot-guest")), Is.False, "자기 배치 파일도 쓰지 않는다");
        }

        [Test]
        public void NewGame_EnteringTheHouse_LeavesTheFurnitureGoalOpen()
        {
            // 메인 체인의 "가구 놓기" 단계(MyOwnSpace)에서 집에 들어서면 놓인 가구를 다시 알린다(ReplayPlacedFurniture).
            // 처음 배치 가구가 그 목표를 저절로 채우면 안 된다.
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            MainChainTests.Play(game, () => game);
            var chain = game.Chain(MainChainTests.ChainId);
            var step = game.QuestChains.GetCurrentStep(chain);
            Assert.That(step.Id, Is.EqualTo("mainquest.MyOwnSpace"), "전제: 가구 놓기 단계");
            Assert.That(step.Objectives.Any(o => o is Rootborn.Core.Quests.FurniturePlacedQuestObjective), Is.True, "전제: 가구를 기다린다");

            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.House.Placed.Count, Is.EqualTo(Starter.Placements.Count), "전제: 처음 가구가 놓였다");
            Assert.That(game.QuestChains.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.MyOwnSpace"), "가구 놓기 단계가 그대로");
            for (int i = 0; i < step.Objectives.Count; i++)
                if (step.Objectives[i] is Rootborn.Core.Quests.FurniturePlacedQuestObjective)
                    Assert.That(game.QuestChains.GetObjectiveProgress(chain, i), Is.Zero, "가구 놓기 목표가 남는다");

            var next = TestWorld.Start(saves.Store);
            next.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(next.QuestChains.GetCurrentStep(chain).Id, Is.EqualTo("mainquest.MyOwnSpace"), "다시 읽어도");
        }

        [Test]
        public void StarterFurniture_IsNotAcceptedByAnyFurnitureGoal()
        {
            // 지킴(데이터): 처음 배치 가구는 어느 "가구 놓기" 목표의 받는 목록에도 없다. 집에 들어설 때 놓인 가구가 다시 알려지므로(ReplayPlacedFurniture),
            // 겹치면 목표가 저절로 찬다 — 집 꾸미기(Objective_DecorateHouse)는 3개를 요구하고 처음 배치도 3개다. 받는 목록은 데이터에서 읽는다.
            var goals = TestWorld.Definitions.All<Rootborn.Core.Quests.FurniturePlacedQuestObjective>().ToList();
            Assert.That(goals, Is.Not.Empty, "전제: 데이터에 가구 놓기 목표가 있다");
            var starters = TestWorld.Definitions.All<HouseStarterLayout>().ToList();
            Assert.That(starters, Is.Not.Empty, "전제: 데이터에 처음 배치가 있다");

            var overlaps = new List<string>();
            foreach (var starter in starters)
                foreach (var goal in goals)
                {
                    // 목표가 다른 면만 센다면 이 처음 배치의 알림으로는 차지 않는다(빈 면 = 면을 가리지 않음).
                    if (!string.IsNullOrEmpty(goal.RequiredSurfaceId) && goal.RequiredSurfaceId != starter.SurfaceId) continue;
                    foreach (var p in starter.Placements)
                        if (goal.AcceptedFurnitureIds.Contains(p.FurnitureId))
                            overlaps.Add(starter.Id + " " + p.FurnitureId + " ∈ " + goal.DisplayKey);
                }

            Assert.That(overlaps, Is.Empty, "처음 배치 가구가 가구 놓기 목표를 저절로 채울 수 있다");
        }
    }
}
