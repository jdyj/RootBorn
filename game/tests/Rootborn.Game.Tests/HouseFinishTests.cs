using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Careers;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.World;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// HOUSE-001: 짓는 집의 방 꾸밈(벽지·바닥)을 돈을 내고 바꾸고, 같은 요청은 한 번만 낸다. 돈 빼기와 꾸밈 바꾸기는 함께 바뀌고 한 저장 묶음으로 쓰인다.
    /// 실제 data/와 실제 집 맵(game/world/maps.json의 House — 출처표는 World 로더가 만든다), 임시 폴더 저장소.
    /// GameSession 명령으로 진행하는 자동 테스트다(화면 근거 아님 — 화면은 다음 작업).
    /// </summary>
    public sealed class HouseFinishTests
    {
        private const string Allowance = "item.allowance";
        // 새 집 왼쪽 위 방 안의 바닥 칸(Core RoomFillTests와 같은 평면 좌표). 그 방 채우기는 87칸(방 바탕 84 + 맞닿은 문간 3).
        private static readonly CellPos TopLeftRoom = new CellPos(3, 4);
        private const int TopLeftRoomFill = 87;

        private static readonly Lazy<HouseBuildFrame> _frame = new Lazy<HouseBuildFrame>(BuildFrame);
        private static HouseBuildFrame Frame => _frame.Value;

        private static HouseBuildFrame BuildFrame()
        {
            var map = WorldMapLoader.FromRepo(TestWorld.RepoRoot).LoadMap(HouseRoom.MapId);
            var build = map.Entry.Build;
            Assert.That(build, Is.Not.Null, "집 맵은 짓는 집이다");
            Assert.That(map.Tiles, Is.Not.Null, "짓는 집은 출처표가 있다");
            return new HouseBuildFrame(build.FrameId, map.Tiles, build.DefaultWall, build.DefaultFloor, build.BandColors);
        }

        private static RoomFinishDefinition Finish(string id) => TestWorld.Definitions.Get<RoomFinishDefinition>(id);

        // 처음부터 쓸 수 있고 프레임 기본이 아닌 꾸밈(데이터 순서의 첫 것).
        private static RoomFinishDefinition FreeNonDefault(string slot)
        {
            string fallback = slot == RoomFinishSlots.Wall ? Frame.DefaultWall : Frame.DefaultFloor;
            return TestWorld.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == slot && f.UnlockToken.Length == 0 && f.Id != fallback);
        }

        private static RoomFinishDefinition Locked(string slot)
            => TestWorld.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == slot && f.UnlockToken.Length > 0);

        // 정의의 칸당 돈 × 칸 수(수를 테스트에 적지 않는다).
        private static int MoneyFor(RoomFinishDefinition finish, int cells)
            => finish.Cost.Where(c => c.Item == Allowance).Sum(c => c.Count) * cells;

        private static int Money(GameSession game) => game.Inventory.CountOf(game.Item(Allowance));

        private static void SetMoney(GameSession game, int amount)
        {
            int now = Money(game);
            Assert.That(now, Is.LessThanOrEqualTo(amount), "준비: 지금 돈이 원하는 값보다 적다");
            if (amount > now) game.Inventory.Add(game.Item(Allowance), amount - now);
            Assert.That(Money(game), Is.EqualTo(amount));
        }

        private static GameSession InHouse(ISaveStore store, string slot = TestWorld.Slot)
        {
            var game = TestWorld.Start(store, slot);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            game.EnterHouseBuild(Frame);
            return game;
        }

        private static string BuildPath(TempSaves saves, string slot = TestWorld.Slot) => Path.Combine(saves.Path, slot, SaveFileNames.HouseBuild);

        private static Dictionary<string, byte[]> SlotFiles(TempSaves saves, string slot = TestWorld.Slot)
        {
            var dir = Path.Combine(saves.Path, slot);
            if (!Directory.Exists(dir)) return new Dictionary<string, byte[]>();
            return Directory.GetFiles(dir).ToDictionary(Path.GetFileName, File.ReadAllBytes);
        }

        private static void AssertSameFiles(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after, string why)
        {
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys), why);
            foreach (var pair in before) Assert.That(after[pair.Key], Is.EqualTo(pair.Value), why + ": " + pair.Key);
        }

        [Test]
        public void HOUSE_001_RoomFill_PaysPerChangedCell_AndChangesAllAtOnce()
        {
            using var saves = new TempSaves();
            var game = InHouse(saves.Store);
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            int cost = MoneyFor(floor, TopLeftRoomFill);
            Assert.That(cost, Is.GreaterThan(0), "전제: 시험 꾸밈은 값이 있다");
            SetMoney(game, cost + 5);
            Assert.That(game.HouseBuildOpen, Is.True);

            var preview = game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(preview.CanApply, Is.True, preview.ReasonKey);
            Assert.That(preview.ChangedCells.Count, Is.EqualTo(TopLeftRoomFill));

            var topics = new List<string>();
            game.Changed += topics.Add;
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);

            Assert.That(Money(game), Is.EqualTo(5), "87칸 × 칸당 값만큼 줄었다");
            foreach (var cell in preview.Cells) Assert.That(game.HouseFloorAt(cell), Is.EqualTo(floor.Id), "그 방의 칸이 모두 새 바닥");
            Assert.That(topics, Does.Contain(GameChangeTopics.HouseBuild).And.Contain(GameChangeTopics.Inventory));

            Assert.That(saves.Store.TryRead<HouseBuildSaveData>(TestWorld.Slot, SaveFileNames.HouseBuild, out var file), Is.True);
            Assert.That(file.Finishes.Length, Is.EqualTo(TopLeftRoomFill), "파일에 기록 87개");
            Assert.That(file.Finishes.All(f => f.Floor == floor.Id && f.Wall.Length == 0), Is.True);
            Assert.That(file.FrameId, Is.EqualTo(Frame.FrameId));
            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out var snapshot), Is.True);
            Assert.That(snapshot.Wallet.Balance, Is.EqualTo(5), "같은 묶음의 스냅샷 지갑도 줄었다");
        }

        [Test]
        public void NotEnoughMoney_ChangesNothing()
        {
            using var saves = new TempSaves();
            var game = InHouse(saves.Store);
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            int cost = MoneyFor(floor, TopLeftRoomFill);
            game.VisitLocation("location.town-square"); // 슬롯 파일이 있게(첫 저장)
            // 돈을 방 하나 값보다 1 적게 맞춘다(새 게임 시작 돈에 기대지 않는다).
            int start = Money(game);
            if (start > cost - 1) Assert.That(game.Inventory.Remove(game.Item(Allowance), start - (cost - 1)), Is.True);
            else if (start < cost - 1) game.Inventory.Add(game.Item(Allowance), cost - 1 - start);
            int money = Money(game);
            Assert.That(money, Is.EqualTo(cost - 1));
            game.VisitLocation("location.town-square"); // 맞춘 돈을 저장한 상태에서 비교
            var before = SlotFiles(saves);
            int changes = 0;
            game.Changed += _ => changes++;

            var preview = game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(preview.ReasonKey, Is.EqualTo(HouseFinishReasons.NotEnoughMoney));
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);

            Assert.That(Money(game), Is.EqualTo(money));
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(Frame.DefaultFloor));
            Assert.That(changes, Is.Zero, "알림 없음");
            AssertSameFiles(before, SlotFiles(saves), "파일 그대로");
            Assert.That(File.Exists(BuildPath(saves)), Is.False);
        }

        [Test]
        public void Locked_And_NothingToChange_ChangeNothingAndCostNothing()
        {
            using var saves = new TempSaves();
            var game = InHouse(saves.Store);
            SetMoney(game, 10_000);
            var before = SlotFiles(saves);
            int changes = 0;
            game.Changed += _ => changes++;

            var locked = Locked(RoomFinishSlots.Floor);
            Assert.That(game.IsFinishUnlocked(locked), Is.False);
            Assert.That(game.PreviewHouseFinish(locked.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.Locked));
            Assert.That(game.ApplyHouseFinish(locked.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);

            var same = game.PreviewHouseFinish(Frame.DefaultFloor, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(same.ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            Assert.That(same.Cost, Is.Empty);
            Assert.That(game.ApplyHouseFinish(Frame.DefaultFloor, HouseFinishTools.Room, TopLeftRoom), Is.False);

            Assert.That(Money(game), Is.EqualTo(10_000));
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(Frame.DefaultFloor));
            Assert.That(changes, Is.Zero);
            AssertSameFiles(before, SlotFiles(saves), "파일 그대로");
        }

        [Test]
        public void SameRequestTwice_SecondCostsNothing()
        {
            using var saves = new TempSaves();
            var game = InHouse(saves.Store);
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            int cost = MoneyFor(floor, TopLeftRoomFill);
            SetMoney(game, cost * 2);

            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);
            byte[] after = File.ReadAllBytes(BuildPath(saves));
            Assert.That(game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(Money(game), Is.EqualTo(cost), "두 번째는 돈이 들지 않는다");
            Assert.That(File.ReadAllBytes(BuildPath(saves)), Is.EqualTo(after));

            // 칸 하나만 다른 바닥으로 칠한 뒤 방 채우기: 바뀌는 칸(1)만 낸다.
            var other = TestWorld.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length == 0 && f.Id != floor.Id && f.Id != Frame.DefaultFloor);
            Assert.That(game.ApplyHouseFinish(other.Id, HouseFinishTools.Brush, TopLeftRoom), Is.True);
            int left = Money(game);
            var refill = game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(refill.ChangedCells, Is.EqualTo(new[] { TopLeftRoom }));
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);
            Assert.That(Money(game), Is.EqualTo(left - MoneyFor(floor, 1)));
        }

        [Test]
        public void SaveAndNewSession_SameRequestCostsNothing()
        {
            using var saves = new TempSaves();
            var game = InHouse(saves.Store);
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            var wall = FreeNonDefault(RoomFinishSlots.Wall);
            SetMoney(game, MoneyFor(floor, TopLeftRoomFill) + MoneyFor(wall, TopLeftRoomFill) + 3);
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);
            Assert.That(game.ApplyHouseFinish(wall.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);
            int money = Money(game);

            var next = InHouse(saves.Store);
            Assert.That(Money(next), Is.EqualTo(money));
            Assert.That(next.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(next.HouseWallAt(TopLeftRoom), Is.EqualTo(game.HouseWallAt(TopLeftRoom)));
            Assert.That(next.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            Assert.That(next.PreviewHouseFinish(wall.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            Assert.That(next.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(Money(next), Is.EqualTo(money));
        }

        [Test]
        public void SaveFailure_LeavesFilesAsBefore()
        {
            using var saves = new TempSaves();
            var setup = InHouse(saves.Store);
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            SetMoney(setup, MoneyFor(floor, TopLeftRoomFill) * 2);
            Assert.That(setup.ApplyHouseFinish(floor.Id, HouseFinishTools.Brush, TopLeftRoom), Is.True, "준비: house-build.json과 스냅샷이 있게");
            var before = SlotFiles(saves);
            Assert.That(before.ContainsKey(SaveFileNames.HouseBuild), Is.True);

            var failing = new FailingCommitStore(saves.Store);
            var game = InHouse(failing);
            Assert.Throws<IOException>(() => game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom));
            Assert.That(failing.Failed, Is.True, "묶음 커밋에서 실패했다");
            AssertSameFiles(before, SlotFiles(saves), "스냅샷 지갑과 house-build.json 모두 옛 바이트");
        }

        [Test]
        public void ApplyRefusedInsideTheExchange_MoneyFinishesAndFilesUnchanged()
        {
            using var saves = new TempSaves();
            var game = InHouse(saves.Store);
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            SetMoney(game, MoneyFor(floor, TopLeftRoomFill) * 2);
            game.VisitLocation("location.town-square"); // 슬롯 파일이 있게
            int money = Money(game);
            var before = SlotFiles(saves);
            var topics = new List<string>();
            game.Changed += topics.Add;

            // 시험용 이음매: 바꾸기 커밋 안, 꾸밈 적용 바로 앞에서 프레임을 다시 붙여 미리 보기를 낡게 한다 → Apply가 거절한다.
            var sameValues = new HouseBuildFrame(Frame.FrameId, Frame.Tiles, Frame.DefaultWall, Frame.DefaultFloor, Frame.DefaultBand);
            game.HouseFinishCommitting = () => game.EnterHouseBuild(sameValues);
            int versionBefore = game.HouseBuildVersion;

            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(game.HouseBuildVersion, Is.GreaterThan(versionBefore), "전제: 이음매가 실제로 상태를 바꿨다");
            Assert.That(Money(game), Is.EqualTo(money), "돈이 되돌려졌다");
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(Frame.DefaultFloor), "꾸밈 그대로");
            Assert.That(topics, Does.Not.Contain(GameChangeTopics.Inventory), "명령의 알림 없음(다시 붙이기의 HouseBuild 알림만)");
            game.Changed -= topics.Add;
            AssertSameFiles(before, SlotFiles(saves), "파일 그대로");
            Assert.That(File.Exists(BuildPath(saves)), Is.False);

            // 이음매를 치우면 같은 요청이 된다(거절이 남은 것을 망가뜨리지 않았다).
            game.HouseFinishCommitting = null;
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);
            Assert.That(Money(game), Is.EqualTo(money - MoneyFor(floor, TopLeftRoomFill)));
        }

        [Test]
        public void SharedHouse_RefusedWithReason()
        {
            using var saves = new TempSaves();
            var rules = new RoomHouseRules(TestWorld.Definitions.All<Rootborn.Core.Placement.FurnitureDefinition>(), s => s == HouseRoom.SurfaceId ? HouseRoom.Grid : null);
            var pieces = new RoomPieceSet(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(rules));
            var shares = new RoomShares(pieces.Settings, RoomPieceCatalog.CreateDefault(), () => "guest-r1");
            foreach (var p in pieces.Pieces) shares.Receive(p.State());
            var guest = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-guest", "guest", shares: shares);
            guest.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            guest.EnterHouseBuild(Frame);
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            guest.Inventory.Add(guest.Item(Allowance), MoneyFor(floor, TopLeftRoomFill));
            int money = Money(guest);

            var preview = guest.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(preview.ReasonKey, Is.EqualTo(HouseFinishReasons.SharedHouse));
            Assert.That(guest.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(Money(guest), Is.EqualTo(money));
            Assert.That(guest.HouseFloorAt(TopLeftRoom), Is.EqualTo(Frame.DefaultFloor));
            Assert.That(File.Exists(BuildPath(saves, "slot-guest")), Is.False);
        }

        [Test]
        public void SharedHouse_ShowsFrameDefaults_OwnFinishesKeptForTheSoloHouse()
        {
            // 같이 쓰는 집은 주인(방)의 집이다. 꾸밈 공유는 다음 계획이 만들 때까지 손님의 자기 꾸밈으로 그리지 않고 프레임 기본으로 보인다.
            // 자기 기록은 버리지도 쓰지도 않는다 — 혼자 하는 집으로 돌아오면 그대로다.
            using var saves = new TempSaves();
            const string slot = "slot-guest";
            var solo = TestWorld.Start(saves.Store, slot, "guest");
            solo.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            solo.EnterHouseBuild(Frame);
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            var wall = FreeNonDefault(RoomFinishSlots.Wall);
            solo.Inventory.Add(solo.Item(Allowance), MoneyFor(floor, TopLeftRoomFill) + MoneyFor(wall, TopLeftRoomFill));
            Assert.That(solo.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);
            var faceCell = solo.PreviewHouseFinish(wall.Id, HouseFinishTools.Room, TopLeftRoom).ChangedCells[0]; // 벽지는 정면 주인 칸에 든다
            Assert.That(solo.ApplyHouseFinish(wall.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);
            string ownWall = solo.HouseWallAt(faceCell);
            Assert.That(ownWall, Is.EqualTo(wall.Id), "전제: 자기 집 벽지를 바꿨다");
            byte[] written = File.ReadAllBytes(BuildPath(saves, slot));

            var rules = new RoomHouseRules(TestWorld.Definitions.All<Rootborn.Core.Placement.FurnitureDefinition>(), s => s == HouseRoom.SurfaceId ? HouseRoom.Grid : null);
            var pieces = new RoomPieceSet(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(rules));
            var shares = new RoomShares(pieces.Settings, RoomPieceCatalog.CreateDefault(), () => "guest-r1");
            foreach (var p in pieces.Pieces) shares.Receive(p.State());
            var guest = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, slot, "guest", shares: shares);
            guest.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            guest.EnterHouseBuild(Frame);
            Assert.That(guest.SharesHouse, Is.True, "전제: 같이 쓰는 집");
            Assert.That(guest.HouseFloorAt(TopLeftRoom), Is.EqualTo(Frame.DefaultFloor), "같이 쓰는 집은 자기 꾸밈으로 그리지 않는다");
            Assert.That(guest.HouseWallAt(faceCell), Is.EqualTo(Frame.DefaultWall));
            Assert.That(guest.HouseBandColors(), Is.EqualTo(Frame.DefaultBand));
            guest.VisitLocation("location.town-square"); // 저장하는 명령
            Assert.That(File.ReadAllBytes(BuildPath(saves, slot)), Is.EqualTo(written), "같이 쓰는 판은 자기 꾸밈 기록을 쓰지 않는다");

            var back = TestWorld.Start(saves.Store, slot, "guest");
            back.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            back.EnterHouseBuild(Frame);
            Assert.That(back.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "혼자 하는 집으로 돌아오면 자기 꾸밈이 그대로");
            Assert.That(back.HouseWallAt(faceCell), Is.EqualTo(ownWall));
            Assert.That(File.ReadAllBytes(BuildPath(saves, slot)), Is.EqualTo(written));
        }

        [Test]
        public void NotInHouse_RefusedWithReason()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.EnterHouseBuild(Frame); // 붙였지만 집 배치(House)가 없다
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            SetMoney(game, MoneyFor(floor, TopLeftRoomFill));
            Assert.That(game.HouseBuildOpen, Is.False);
            Assert.That(game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NotInHouse));
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(Money(game), Is.EqualTo(MoneyFor(floor, TopLeftRoomFill)));

            // 붙이지 않은 판도 같다.
            var plain = TestWorld.Start(saves.Store, "slot-plain");
            plain.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(plain.HouseBuildOpen, Is.False);
            Assert.That(plain.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NotInHouse));
        }

        [Test]
        public void LeavingTheHouse_NotInHouse_ReenteringKeepsFinishes()
        {
            // 짓는 집을 나가면(Host가 다른 맵에 들어설 때 LeaveHouseBuild) 꾸밀 수 없다. 집 배치(House)는 남아 있어도. 기록은 버리지 않는다 —
            // 다시 들어오면(같은 프레임) 꾸밈이 그대로이고, 저장 → 새 GameSession에서도 그대로다.
            using var saves = new TempSaves();
            var game = InHouse(saves.Store);
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            SetMoney(game, MoneyFor(floor, TopLeftRoomFill) * 2);
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);
            byte[] written = File.ReadAllBytes(BuildPath(saves));
            int money = Money(game);

            var topics = new List<string>();
            game.Changed += topics.Add;
            game.LeaveHouseBuild();
            Assert.That(game.House, Is.Not.Null, "전제: 집 배치는 남아 있다(가구 방을 떠나도 배치를 버리지 않는다)");
            Assert.That(game.HouseBuildOpen, Is.False, "집을 나가면 꾸밀 수 없다");
            Assert.That(topics, Does.Contain(GameChangeTopics.HouseBuild), "떠남도 알린다(꾸미기 화면이 닫히게)");
            var other = TestWorld.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length == 0 && f.Id != floor.Id && f.Id != Frame.DefaultFloor);
            Assert.That(game.PreviewHouseFinish(other.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NotInHouse));
            Assert.That(game.ApplyHouseFinish(other.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            Assert.That(Money(game), Is.EqualTo(money), "돈 그대로");
            Assert.That(File.ReadAllBytes(BuildPath(saves)), Is.EqualTo(written), "떠남은 파일을 쓰지 않는다");
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "기록은 들고 있다");
            topics.Clear();
            game.LeaveHouseBuild();
            Assert.That(topics, Is.Empty, "이미 떠났으면 알리지 않는다");

            game.EnterHouseBuild(Frame); // 같은 프레임으로 다시 들어온다
            Assert.That(game.HouseBuildOpen, Is.True);
            Assert.That(topics, Does.Contain(GameChangeTopics.HouseBuild));
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "다시 들어와도 꾸밈이 그대로");
            Assert.That(game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));

            var again = InHouse(saves.Store);
            Assert.That(again.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "저장 → 새 판에서도 그대로");
        }

        [Test]
        public void WithoutAttach_SaveKeepsLoadedRecords()
        {
            using var saves = new TempSaves();
            var unknown = new HouseFinishCellSaveData { X = 99, Y = 99, Wall = "finish.gone-from-data", Floor = "" };
            saves.Store.Write(TestWorld.Slot, SaveFileNames.HouseBuild, new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion, FrameId = Frame.FrameId, Band = "",
                Finishes = new[] { new HouseFinishCellSaveData { X = TopLeftRoom.X, Y = TopLeftRoom.Y, Floor = FreeNonDefault(RoomFinishSlots.Floor).Id }, unknown },
            });
            byte[] written = File.ReadAllBytes(BuildPath(saves));

            var game = TestWorld.Start(saves.Store);
            game.VisitLocation("location.town-square"); // 다른 것을 저장하는 명령(붙기 전)
            Assert.That(File.ReadAllBytes(BuildPath(saves)), Is.EqualTo(written), "붙기 전 저장해도 읽은 기록이 그대로");

            var later = InHouse(saves.Store);
            Assert.That(later.HouseFloorAt(TopLeftRoom), Is.EqualTo(FreeNonDefault(RoomFinishSlots.Floor).Id));
            Assert.That(File.ReadAllBytes(BuildPath(saves)), Is.EqualTo(written), "집에 들어서 붙이는 것만으로 파일을 쓰지 않는다");
        }

        [Test]
        public void Queries_OptionsInDataOrder_AndBandIsFrameDefault()
        {
            using var saves = new TempSaves();
            var game = InHouse(saves.Store);
            var floors = TestWorld.Definitions.All<RoomFinishDefinition>().Where(f => f.Slot == RoomFinishSlots.Floor).Select(f => f.Id);
            Assert.That(game.HouseFinishOptions(RoomFinishSlots.Floor).Select(f => f.Id), Is.EqualTo(floors), "데이터 순서, 잠긴 것 포함");
            Assert.That(game.HouseFinishOptions(RoomFinishSlots.Floor), Is.Not.InstanceOf<List<RoomFinishDefinition>>(), "기억한 목록을 고칠 수 없다");
            Assert.That(game.HouseBandColors(), Is.EqualTo(Frame.DefaultBand));
            int version = game.HouseBuildVersion;
            game.EnterHouseBuild(Frame);
            Assert.That(game.HouseBuildVersion, Is.EqualTo(version), "같은 프레임은 다시 붙이지 않는다");
        }

        /// <summary>FileSaveStore를 감싸고 묶음 커밋에서 던지는 저장소(묶음을 버리므로 파일은 옛것 그대로).</summary>
        private sealed class FailingCommitStore : IBatchSaveStore
        {
            private readonly FileSaveStore _inner;
            public FailingCommitStore(FileSaveStore inner) => _inner = inner;
            public bool Failed { get; private set; }

            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => _inner.TryRead(saveSlot, fileName, out data);
            public void Write<T>(string saveSlot, string fileName, T data) where T : class => throw new IOException("묶음 밖 쓰기는 이 테스트에 없다");
            public void Delete(string saveSlot, string fileName) => throw new IOException("묶음 밖 지우기는 이 테스트에 없다");
            public ISaveBatch BeginBatch(string saveSlot) => new Batch(this, _inner.BeginBatch(saveSlot));

            private sealed class Batch : ISaveBatch
            {
                private readonly FailingCommitStore _owner;
                private readonly ISaveBatch _inner;
                public Batch(FailingCommitStore owner, ISaveBatch inner) { _owner = owner; _inner = inner; }
                public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => _inner.TryRead(saveSlot, fileName, out data);
                public void Write<T>(string saveSlot, string fileName, T data) where T : class => _inner.Write(saveSlot, fileName, data);
                public void Delete(string saveSlot, string fileName) => _inner.Delete(saveSlot, fileName);
                public void Commit()
                {
                    _owner.Failed = true;
                    throw new IOException("시험: 묶음 커밋 실패");
                }
                public void Dispose() => _inner.Dispose();
            }
        }
    }
}
