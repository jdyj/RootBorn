using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Core.World;
using Rootborn.Data.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 같이 쓰는 집의 꾸밈 조각(Core RoomHouseBuild)이 방 세이브에 더한 두 파일 — <c>room-piece-house-build.json</c>(꾸밈 기록)과
    /// <c>room-piece-house-build-holds.json</c>(잡기) — 의 하위호환·상위호환(스킬 save-compat-change).
    /// 하위: 두 파일이 없는 옛 방 슬롯이 열리고 같이 쓰는 집이 기본 꾸밈으로 보이며 읽기만으로 아무것도 쓰지 않는다. 데이터에 없는 꾸밈 id·다른 틀·
    /// 벽/격자 밖 칸의 기록과 잡기를 버리지 않는다. 손님 폴더의 그 사람 자기 꾸밈 파일은 건드리지 않는다.
    /// 상위: 꾸밈 조각을 모르는 옛 방(서버)·옛 플레이어 쪽은 두 파일을 건드리지 않고 가구·상자를 그대로 다룬다. 옛 플레이어 쪽이 보낼 목록의 꾸밈 요청 줄을
    /// 만나면 어떻게 되는지도 고정한다.
    /// "옛 프로그램"은 꾸밈 조각 줄만 뺀 등록 표(<see cref="Older"/>)로 흉내 낸 것이다 — 실제 옛 빌드 실행 파일·옛 서버로 연 것이 아니다.
    /// 방은 서버처럼 이 테스트가 손으로 돌린다(요청 반영 → 답이 있으면 방 저장 → 답을 그 사람에게 → 바뀐 상태를 모두에게). 손님의 세이브는 서버와 같이
    /// &lt;방 슬롯&gt;/guests/&lt;플레이어 id&gt;/에 둔다. 실제 data/와 실제 집 맵, 임시 폴더와 고정 폴더 사본. GameSession 명령으로 진행하는 자동 테스트다.
    /// </summary>
    public sealed class RoomHouseBuildCompatTests
    {
        private const string Allowance = "item.allowance";
        private const string RoomSlot = "room-1";
        private const string Guest = TestWorld.Player;
        private const string Unknown = "finish.gone-from-data";
        private const string Wood = "Wood";
        private const int TopLeftRoomFill = 87; // HouseFinishTests와 같은 방(방 바탕 84 + 맞닿은 문간 3)
        private const string Fixture = "room-20261114";
        private static CellPos TopLeftRoom => HouseRoom.TopLeftRoom;
        private static CellPos NextToTopLeft => new CellPos(TopLeftRoom.X + 1, TopLeftRoom.Y);
        private static string FinishFile => SaveFileNames.RoomPiece(RoomHouseBuild.PieceName);
        private static string HoldsFile => SaveFileNames.RoomHouseBuildHolds;

        // ---------- 방(서버 흉내) ----------

        private static RoomHouseRules Rules()
        {
            var d = TestWorld.Definitions;
            return new RoomHouseRules(d.All<FurnitureDefinition>(), s => s == HouseRoom.SurfaceId ? HouseRoom.Grid : null,
                new FurnitureStorageCatalog(d.All<FurnitureStorageDefinition>()), d.All<ItemDefinition>(),
                buildFrameFor: s => s == HouseRoom.SurfaceId ? HouseRoom.Frame : null, finishes: d.All<RoomFinishDefinition>());
        }

        private static RoomPieceCatalog Current(RoomHouseRules rules = null) => RoomPieceCatalog.CreateDefault(rules);

        /// <summary>꾸밈 조각이 생기기 전의 등록 표: <see cref="RoomPieceCatalog.CreateDefault"/>와 같은데 <c>Follow(house-build)</c> 줄만 없다.</summary>
        private static RoomPieceCatalog Older(RoomHouseRules rules = null) => new RoomPieceCatalog()
            .Add(RoomWallet.PieceName, () => new RoomWallet(), outbox => new RoomWalletClient(outbox))
            .Add(RoomHouse.PieceName, () => new RoomHouse(rules), outbox => new RoomHouseClient(outbox))
            .Add(RoomWorldFlags.PieceName, () => new RoomWorldFlags(), outbox => new RoomWorldFlagsClient(outbox))
            .Follow(RoomChests.PieceName, RoomHouse.PieceName, () => new RoomChests(rules), outbox => new RoomChestsClient(outbox));

        private sealed class Room
        {
            private readonly ISaveStore _store;
            private readonly string _slot;
            private readonly List<(string Player, RoomShares Shares)> _players = new List<(string, RoomShares)>();

            /// <summary>그 슬롯의 방을 그 등록 표로 다시 켠다(설정 파일을 읽고 조각·장부를 읽는다 — 서버를 다시 켠 것과 같다).</summary>
            public Room(ISaveStore store, string slot, RoomPieceCatalog catalog)
            {
                _store = store;
                _slot = slot;
                Assert.That(RoomSettingsSaveFiles.TryReadSaved(store, slot, out var settings, out _), Is.True, "방 설정 파일이 있다");
                Pieces = new RoomPieceSet(settings, catalog);
                Pieces.Load(store, slot);
            }

            /// <summary>새 방: 설정·시계를 쓰고(서버가 새 방을 만들 때처럼) 빈 조각을 저장한다. 방 지갑 잔액은 준비 단계.</summary>
            public static Room Create(ISaveStore store, string slot, RoomPieceCatalog catalog, int walletBalance, params string[] shared)
            {
                RoomSettingsSaveFiles.LoadOrCreate(store, slot, true, new RoomShareSettings(shared), out bool created);
                Assert.That(created, Is.True, "준비: 새 방");
                RoomClockSaveFiles.Save(store, slot, new RoomClock(TestWorld.Definitions.TimeRules, 1));
                if (walletBalance > 0)
                    store.Write(slot, SaveFileNames.RoomPiece(RoomWallet.PieceName), new RoomWalletSaveData { Version = RoomWalletSaveData.KnownVersion, Balance = walletBalance });
                var room = new Room(store, slot, catalog);
                room.Pieces.Save(store, slot);
                return room;
            }

            public RoomPieceSet Pieces { get; }

            public RoomHouseBuild Build => Pieces.TryGet(RoomHouseBuild.PieceName, out var p) ? (RoomHouseBuild)p : null;
            public RoomHouse House => Pieces.TryGet(RoomHouse.PieceName, out var p) ? (RoomHouse)p : null;
            public RoomChests Chests => Pieces.TryGet(RoomChests.PieceName, out var p) ? (RoomChests)p : null;
            public RoomWallet Wallet => Pieces.TryGet(RoomWallet.PieceName, out var p) ? (RoomWallet)p : null;

            /// <summary>그 신원으로 들어온다(그 프로그램의 등록 표로 플레이어 쪽 조각을 만든다). 들어오면 방의 상태를 모두 받는다.</summary>
            public RoomShares Join(string playerId, RoomPieceCatalog playerCatalog)
            {
                var shares = new RoomShares(Pieces.Settings, playerCatalog, () => Guid.NewGuid().ToString("N"));
                shares.RoomPlayerId = playerId;
                foreach (var piece in Pieces.Pieces) shares.Receive(piece.State());
                _players.Add((playerId, shares));
                return shares;
            }

            public void Leave(RoomShares shares) => _players.RemoveAll(p => p.Shares == shares);

            /// <summary>그 사람의 보낼 요청(미룬 것 빼고)을 한 번 반영한다: 답이 있으면 방을 저장하고, 답을 그 사람에게, 바뀐 상태를 모두에게(서버 순서).</summary>
            public void Deliver(RoomShares from) => Apply(from, loseAnswers: false);

            /// <summary>방은 반영하고 저장했지만 그 사람에게 가는 답과 상태가 사라졌다(보낸 뒤 끊김).</summary>
            public void DeliverAndLoseTheAnswers(RoomShares from) => Apply(from, loseAnswers: true);

            /// <summary>보낼 것이 없을 때까지 되풀이한다(잡기 → 지갑 → 쓰기처럼 답이 새 요청을 낳는다).</summary>
            public void DeliverAll(RoomShares from)
            {
                for (int round = 0; round < 12 && Sendable(from).Count > 0; round++) Deliver(from);
                Assert.That(Sendable(from), Is.Empty, "준비: 보낼 요청이 끝났다");
            }

            private static List<RoomRequestSaveData> Sendable(RoomShares from)
                => from.Outbox.Requests.Where(r => !from.Outbox.IsDeferred(r.RequestId)).Select(r => r.Copy()).ToList();

            private void Apply(RoomShares from, bool loseAnswers)
            {
                var (player, _) = _players.First(p => p.Shares == from);
                var answers = new List<RoomAnswer>();
                var changed = new List<IRoomPiece>();
                Pieces.Apply(player, Sendable(from), 1, answers, changed);
                if (answers.Count > 0) Pieces.Save(_store, _slot);
                if (!loseAnswers) foreach (var a in answers) from.Receive(a);
                foreach (var piece in changed)
                    foreach (var p in _players)
                        if (!loseAnswers || p.Shares != from) p.Shares.Receive(piece.State());
            }
        }

        // ---------- 플레이어 ----------

        private static FileSaveStore Guests(TempSaves saves) => new FileSaveStore(GuestsFolder(saves.Path));

        private static string GuestsFolder(string root) => Path.Combine(root, RoomSlot, "guests");

        /// <param name="knowsHouseBuild">false면 옛 빌드처럼 꾸미는 집에 들어서지 않는다(EnterHouseBuild가 없던 판).</param>
        private static GameSession Play(ISaveStore guests, RoomShares shares, string player = Guest, bool knowsHouseBuild = true)
        {
            var game = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, guests, player, player, shares: shares);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            if (knowsHouseBuild) game.EnterHouseBuild(HouseRoom.Frame);
            return game;
        }

        private static IEnumerable<RoomFinishDefinition> PaidFloors()
            => TestWorld.Definitions.All<RoomFinishDefinition>()
                .Where(f => f.Slot == RoomFinishSlots.Floor && f.UnlockToken.Length == 0 && f.Id != HouseRoom.Frame.DefaultFloor && f.Cost.Count > 0);

        private static RoomFinishDefinition PaidFloor() => PaidFloors().First();
        private static RoomFinishDefinition OtherPaidFloor() => PaidFloors().Skip(1).First();

        private static RoomFinishDefinition FreeWall()
            => TestWorld.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == RoomFinishSlots.Wall && f.UnlockToken.Length == 0 && f.Id != HouseRoom.Frame.DefaultWall);

        private static int PerCell(RoomFinishDefinition finish) => finish.Cost.Where(c => c.Item == Allowance).Sum(c => c.Count);

        private static int Money(GameSession game) => game.Inventory.CountOf(game.Item(Allowance));

        private static void Give(GameSession game, int amount) => game.Inventory.Add(game.Item(Allowance), amount);

        private static string ChestFurniture => TestWorld.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId;

        private static string FreeChair(GameSession game) => game.AvailableFurniture().First(f => f.StableId != ChestFurniture).StableId;

        private static string PlaceChest(Room room, RoomShares shares, GameSession game)
        {
            game.PlaceFurniture(ChestFurniture, HouseRoom.FirstFreeAnchor(game, ChestFurniture), FurniturePlacementDirection.North);
            room.DeliverAll(shares);
            return game.House.Placed.Single(p => p.Definition.StableId == ChestFurniture).InstanceId;
        }

        private static int HouseBuildRequests(RoomShares shares, string kind = null)
            => shares.Outbox.Requests.Count(r => r.Piece == RoomHouseBuild.PieceName && (kind == null || r.Kind == kind));

        private static RoomOutboxSaveData SavedOutbox(ISaveStore guests, string player = Guest)
        {
            Assert.That(guests.TryRead<RoomOutboxSaveData>(player, SaveFileNames.RoomOutbox(player), out var outbox), Is.True, "보낼 목록 파일이 있다");
            return outbox;
        }

        // 왼쪽 위 방 밖의 바닥 칸 하나(방 채우기에 들지 않는 칸).
        private static CellPos FloorOutsideTopLeftRoom(GameSession game)
        {
            var fill = game.PreviewHouseFinish(PaidFloor().Id, HouseFinishTools.Room, TopLeftRoom).Cells;
            var grid = HouseRoom.Frame.Tiles.Grid;
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    if (grid.At(x, y) == RoomCells.Floor && !fill.Contains(new CellPos(x, y))) return new CellPos(x, y);
            throw new InvalidOperationException("왼쪽 위 방 밖의 바닥 칸이 없다");
        }

        private static CellPos FirstWallCell()
        {
            var grid = HouseRoom.Frame.Tiles.Grid;
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    if (grid.At(x, y) == RoomCells.Wall) return new CellPos(x, y);
            throw new InvalidOperationException("집에 벽 칸이 없다");
        }

        // ---------- 파일 ----------

        /// <summary>그 폴더 맨 위 파일(하위 폴더 제외)의 이름 → 바이트. 방의 파일만 본다(손님 폴더는 따로).</summary>
        private static SortedDictionary<string, byte[]> TopFiles(string folder)
        {
            var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            if (!Directory.Exists(folder)) return files;
            foreach (var path in Directory.GetFiles(folder)) files[Path.GetFileName(path)] = File.ReadAllBytes(path);
            return files;
        }

        /// <summary>그 폴더 아래 모든 파일(상대 경로, '/') → 바이트.</summary>
        private static SortedDictionary<string, byte[]> AllFiles(string folder)
        {
            var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                files[Path.GetRelativePath(folder, path).Replace('\\', '/')] = File.ReadAllBytes(path);
            return files;
        }

        private static bool Same(IDictionary<string, byte[]> a, IDictionary<string, byte[]> b)
            => a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var other) && other.AsSpan().SequenceEqual(pair.Value));

        private static string RoomFolder(TempSaves saves) => Path.Combine(saves.Path, RoomSlot);

        /// <summary>고정 폴더의 방 슬롯(손님 폴더까지)을 이 임시 폴더로 복사한다(읽기만).</summary>
        private static void CopyRoomFixture(TempSaves saves, string fixture = Fixture)
        {
            string source = Path.Combine(TestWorld.FixtureSaves, fixture, RoomSlot);
            Assert.That(Directory.Exists(source), Is.True, "고정 폴더가 있다: " + source);
            foreach (var path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(RoomFolder(saves), Path.GetRelativePath(source, path));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(path, target);
            }
        }

        private static HouseBuildSaveData ReadFinishes(TempSaves saves)
        {
            Assert.That(saves.Store.TryRead<HouseBuildSaveData>(RoomSlot, FinishFile, out var data), Is.True, "방의 꾸밈 기록 파일이 있다");
            return data;
        }

        private static RoomHouseBuildHoldsSaveData ReadHolds(TempSaves saves)
        {
            Assert.That(saves.Store.TryRead<RoomHouseBuildHoldsSaveData>(RoomSlot, HoldsFile, out var data), Is.True, "방의 잡기 파일이 있다");
            return data;
        }

        private static HouseFinishCellSaveData RecordAt(HouseBuildSaveData data, CellPos cell) => data.Finishes.SingleOrDefault(f => f.X == cell.X && f.Y == cell.Y);

        [Test]
        public void TheOlderCatalog_IsTheDefaultWithoutTheHouseBuildLine()
        {
            var settings = new RoomShareSettings(new[] { RoomWallet.PieceName, RoomHouse.PieceName, RoomWorldFlags.PieceName });
            Assert.That(Older().ActiveNames(settings), Is.EqualTo(Current().ActiveNames(settings).Where(n => n != RoomHouseBuild.PieceName)),
                "전제: 옛 등록 표는 지금 표에서 꾸밈 조각 줄만 뺀 것");
            Assert.That(Current().ActiveNames(settings), Does.Contain(RoomHouseBuild.PieceName));
        }

        // ---------- 하위호환 ----------

        [Test]
        public void OldRoomSlotWithoutTheFiles_OpensWithDefaults_ReadingWritesNothing()
        {
            using var saves = new TempSaves();
            var guests = Guests(saves);
            var rules = Rules();
            var made = Room.Create(saves.Store, RoomSlot, Current(rules), 0, RoomHouse.PieceName);
            var firstShares = made.Join(Guest, Current());
            var first = Play(guests, firstShares);
            string chair = FreeChair(first);
            first.PlaceFurniture(chair, HouseRoom.FirstFreeAnchor(first, chair), FurniturePlacementDirection.North);
            made.DeliverAll(firstShares);
            made.Leave(firstShares);
            // 꾸밈 조각이 생기기 전의 방 슬롯: 두 파일을 지운다.
            File.Delete(Path.Combine(RoomFolder(saves), FinishFile));
            File.Delete(Path.Combine(RoomFolder(saves), HoldsFile));
            var before = TopFiles(RoomFolder(saves));
            Assert.That(before.Keys, Does.Not.Contain(FinishFile).And.Not.Contain(HoldsFile), "전제: 꾸밈 조각 파일이 없다");
            Assert.That(before.Keys, Does.Contain(SaveFileNames.RoomPiece(RoomHouse.PieceName)), "전제: 가구가 든 집 조각은 있다");

            var room = new Room(saves.Store, RoomSlot, Current(rules));
            Assert.That(room.Build, Is.Not.Null, "집을 같이 쓰는 방이면 꾸밈 조각이 켜진다");
            Assert.That(room.Build.ToSaveData().Finishes, Is.Empty, "빈 기록");
            Assert.That(room.Build.ToSaveData().Band, Is.Empty);
            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(room.House.Placed.Count, Is.EqualTo(1), "옛 가구가 그대로");
            Assert.That(Same(before, TopFiles(RoomFolder(saves))), Is.True, "읽기만으로 방 파일을 쓰지 않는다");

            var shares = room.Join(Guest, Current());
            var game = Play(guests, shares);
            Assert.That(game.SharesHouseBuild, Is.True);
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "같이 쓰는 집이 기본 꾸밈으로 보인다");
            Assert.That(game.HouseWallAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultWall));
            Assert.That(game.HouseBandColors(), Is.EqualTo(HouseRoom.Frame.DefaultBand));
            Assert.That(game.House.Placed.Count, Is.EqualTo(1), "옛 가구가 보인다");
            Assert.That(Same(before, TopFiles(RoomFolder(saves))), Is.True, "들어와 보는 것만으로 방 파일을 쓰지 않는다");

            // 처음 꾸미면 그때 두 파일이 생기고, 옛 파일은 가구가 그대로다.
            var floor = PaidFloor();
            Give(game, PerCell(floor) * TopLeftRoomFill);
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False, "방에 요청만 했다");
            room.DeliverAll(shares);
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
            Assert.That(ReadFinishes(saves).Finishes.Length, Is.EqualTo(TopLeftRoomFill));
            Assert.That(ReadHolds(saves).Holds, Is.Empty);
            Assert.That(new Room(saves.Store, RoomSlot, Current(rules)).House.Placed.Count, Is.EqualTo(1), "다시 켠 방에도 가구가 그대로");
        }

        [Test]
        public void UnknownFinishIdsInTheRoom_KeptAndDrawnAsDefault()
        {
            using var saves = new TempSaves();
            var guests = Guests(saves);
            var rules = Rules();
            Room.Create(saves.Store, RoomSlot, Current(rules), 0, RoomHouse.PieceName);
            var floor = PaidFloor();
            var wall = FreeWall();
            var wallCell = FirstWallCell();
            var outside = new CellPos(HouseRoom.Frame.Tiles.Grid.Columns + 5, HouseRoom.Frame.Tiles.Grid.Rows + 5);
            // 다른 틀(옛 집)에서 쓴 기록: 데이터에 없는 꾸밈, 아는 꾸밈, 벽 칸, 격자 밖, 모르는 띠.
            saves.Store.Write(RoomSlot, FinishFile, new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion, FrameId = "frame.an-older-house", Band = Unknown,
                Finishes = new[]
                {
                    new HouseFinishCellSaveData { X = TopLeftRoom.X, Y = TopLeftRoom.Y, Wall = Unknown, Floor = Unknown },
                    new HouseFinishCellSaveData { X = NextToTopLeft.X, Y = NextToTopLeft.Y, Wall = "", Floor = floor.Id },
                    new HouseFinishCellSaveData { X = wallCell.X, Y = wallCell.Y, Wall = "", Floor = floor.Id },
                    new HouseFinishCellSaveData { X = outside.X, Y = outside.Y, Wall = wall.Id, Floor = floor.Id },
                },
            });
            // 남의 잡기: 데이터에 없는 꾸밈, 모르는 종류(뒤 판이 더할 것), 지금 없는 면(다른 틀).
            saves.Store.Write(RoomSlot, HoldsFile, new RoomHouseBuildHoldsSaveData
            {
                Version = RoomHouseBuildHoldsSaveData.KnownVersion,
                Holds = new[]
                {
                    new RoomHouseBuildHoldSaveData { RequestId = "h-gone", PlayerId = "someone", SurfaceId = HouseRoom.SurfaceId, Kind = RoomHouseBuild.Finish, FinishId = Unknown, Tool = HouseFinishTools.Room, X = TopLeftRoom.X, Y = TopLeftRoom.Y, Count = 3 },
                    new RoomHouseBuildHoldSaveData { RequestId = "h-kind", PlayerId = "someone", SurfaceId = HouseRoom.SurfaceId, Kind = "paint-inner-wall", FinishId = floor.Id, X = 1, Y = 1, Count = 1 },
                    new RoomHouseBuildHoldSaveData { RequestId = "h-surface", PlayerId = "someone", SurfaceId = "surface.gone", Kind = RoomHouseBuild.Finish, FinishId = floor.Id, Tool = HouseFinishTools.Brush, X = TopLeftRoom.X, Y = TopLeftRoom.Y, Count = 1 },
                },
            });

            var room = new Room(saves.Store, RoomSlot, Current(rules));
            Assert.That(room.Build.HeldCount, Is.EqualTo(3), "읽을 때 잡기를 버리지 않는다");
            var shares = room.Join(Guest, Current());
            var game = Play(guests, shares);

            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "데이터에 없는 바닥은 기본으로 보인다");
            Assert.That(game.HouseWallAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultWall), "데이터에 없는 벽지는 기본으로 보인다");
            Assert.That(game.HouseBandColors(), Is.EqualTo(HouseRoom.Frame.DefaultBand), "데이터에 없는 띠는 기본으로 보인다");
            Assert.That(game.HouseFloorAt(NextToTopLeft), Is.EqualTo(floor.Id), "틀 id가 달라도 같은 칸 좌표의 아는 기록이 든다");

            // 다른 칸 하나를 꾸며 방이 두 파일을 다시 쓰게 한다.
            var elsewhere = FloorOutsideTopLeftRoom(game);
            var other = OtherPaidFloor();
            Give(game, PerCell(other));
            Assert.That(game.ApplyHouseFinish(other.Id, HouseFinishTools.Brush, elsewhere), Is.False);
            room.DeliverAll(shares);
            Assert.That(game.HouseFloorAt(elsewhere), Is.EqualTo(other.Id), "준비: 꾸밈이 됐다");

            var data = ReadFinishes(saves);
            Assert.That(RecordAt(data, TopLeftRoom)?.Floor, Is.EqualTo(Unknown), "모르는 바닥 기록이 남는다");
            Assert.That(RecordAt(data, TopLeftRoom)?.Wall, Is.EqualTo(Unknown), "모르는 벽지 기록이 남는다");
            Assert.That(RecordAt(data, NextToTopLeft)?.Floor, Is.EqualTo(floor.Id));
            Assert.That(RecordAt(data, wallCell)?.Floor, Is.EqualTo(floor.Id), "벽 칸 기록이 남는다");
            Assert.That(RecordAt(data, outside)?.Wall, Is.EqualTo(wall.Id), "격자 밖 기록이 남는다");
            Assert.That(RecordAt(data, outside)?.Floor, Is.EqualTo(floor.Id));
            Assert.That(data.Band, Is.EqualTo(Unknown), "모르는 띠가 남는다");
            Assert.That(data.FrameId, Is.EqualTo(HouseRoom.Frame.FrameId), "다시 쓴 파일은 지금 틀 id");
            Assert.That(ReadHolds(saves).Holds.Select(h => h.RequestId), Is.EquivalentTo(new[] { "h-gone", "h-kind", "h-surface" }), "남의 잡기가 그대로 다시 쓰인다");
            Assert.That(ReadHolds(saves).Holds.Single(h => h.RequestId == "h-kind").Kind, Is.EqualTo("paint-inner-wall"));

            // 다음 판(서버를 다시 켜고 다시 들어온다)에도 같다.
            room.Leave(shares);
            var again = new Room(saves.Store, RoomSlot, Current(rules));
            Assert.That(again.Build.HeldCount, Is.EqualTo(3));
            var next = Play(guests, again.Join(Guest, Current()));
            Assert.That(next.HouseFloorAt(TopLeftRoom), Is.EqualTo(HouseRoom.Frame.DefaultFloor));
            Assert.That(next.HouseFloorAt(elsewhere), Is.EqualTo(other.Id));
            Assert.That(RecordAt(again.Build.ToSaveData(), TopLeftRoom)?.Floor, Is.EqualTo(Unknown));
        }

        [Test]
        public void GuestsOwnHouseBuildFile_InTheGuestsFolder_Untouched()
        {
            using var saves = new TempSaves();
            var guests = Guests(saves);
            var rules = Rules();
            var room = Room.Create(saves.Store, RoomSlot, Current(rules), 0, RoomHouse.PieceName);
            var other = OtherPaidFloor();
            // 그 사람이 혼자 꾸민 자기 집 기록(슬롯 공용 파일 house-build.json)이 손님 폴더에 있다.
            guests.Write(Guest, SaveFileNames.HouseBuild, new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion, FrameId = HouseRoom.Frame.FrameId, Band = "",
                Finishes = new[] { new HouseFinishCellSaveData { X = NextToTopLeft.X, Y = NextToTopLeft.Y, Wall = "", Floor = other.Id } },
            });
            string ownPath = Path.Combine(GuestsFolder(saves.Path), Guest, SaveFileNames.HouseBuild);
            byte[] own = File.ReadAllBytes(ownPath);

            var shares = room.Join(Guest, Current());
            var game = Play(guests, shares);
            Assert.That(game.HouseFloorAt(NextToTopLeft), Is.EqualTo(HouseRoom.Frame.DefaultFloor), "같이 쓰는 집에서는 자기 꾸밈이 아니라 방의 것");
            var floor = PaidFloor();
            Give(game, PerCell(floor) * TopLeftRoomFill);
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.DeliverAll(shares);
            Assert.That(game.HouseFloorAt(NextToTopLeft), Is.EqualTo(floor.Id));
            int day = game.Student.CurrentDay;
            if (game.Student.DayState == StudentDayState.InProgress) game.VisitLocation("location.town-square");
            if (game.Student.DayState == StudentDayState.InProgress) Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day + 1), "준비: 여러 번 저장했다");

            Assert.That(File.ReadAllBytes(ownPath), Is.EqualTo(own), "손님 폴더의 자기 꾸밈 파일은 바이트 그대로");
            Assert.That(RecordAt(ReadFinishes(saves), NextToTopLeft)?.Floor, Is.EqualTo(floor.Id), "꾸밈은 방의 파일에");

            var solo = TestWorld.Start(guests, Guest);
            solo.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            solo.EnterHouseBuild(HouseRoom.Frame);
            Assert.That(solo.HouseFloorAt(NextToTopLeft), Is.EqualTo(other.Id), "같은 슬롯을 혼자 하면 자기 꾸밈이 보인다");
        }

        // ---------- 상위호환 ----------

        [Test]
        public void AnOlderServer_LeavesTheNewFilesUntouched()
        {
            using var saves = new TempSaves();
            var guests = Guests(saves);
            var rules = Rules();
            var room = Room.Create(saves.Store, RoomSlot, Current(rules), 0, RoomHouse.PieceName);
            var aShares = room.Join(Guest, Current());
            var bShares = room.Join("b", Current());
            var a = Play(guests, aShares);
            var b = Play(guests, bShares, "b");
            string chest = PlaceChest(room, aShares, a);
            a.Inventory.Add(a.Item(Wood), 8);
            Assert.That(a.PutIntoStorage(chest, Wood, 5), Is.EqualTo(StorageTransferResult.Requested));
            room.DeliverAll(aShares);
            var floor = PaidFloor();
            Give(a, PerCell(floor) * TopLeftRoomFill);
            Assert.That(a.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.DeliverAll(aShares);
            // b의 잡기 하나가 걸려 있다(방은 잡았고 b는 답을 받기 전에 끊겼다).
            var other = OtherPaidFloor();
            Give(b, PerCell(other));
            int bMoney = Money(b);
            Assert.That(b.ApplyHouseFinish(other.Id, HouseFinishTools.Brush, NextToTopLeft), Is.False);
            room.DeliverAndLoseTheAnswers(bShares);
            Assert.That(room.Build.HeldCount, Is.EqualTo(1), "준비: 잡기 하나");
            room.Leave(aShares);
            room.Leave(bShares);
            string finishPath = Path.Combine(RoomFolder(saves), FinishFile), holdsPath = Path.Combine(RoomFolder(saves), HoldsFile);
            byte[] finishBytes = File.ReadAllBytes(finishPath), holdsBytes = File.ReadAllBytes(holdsPath);
            // a의 마지막 쓰기의 답은 저장을 부르지 않아 디스크의 보낼 목록에 그 쓰기가 남아 있다(작업 2 보고). 방 장부에는 이미 반영(Applied)으로 있다 —
            // 옛 서버도 같은 장부로 같은 답을 주므로 해롭지 않다(아래에서 a가 옛 서버에 그 줄을 보낸다).
            foreach (var line in SavedOutbox(guests).Requests.Where(r => r.Piece == RoomHouseBuild.PieceName))
                Assert.That(room.Pieces.TryAnswer(Guest, line.RequestId, out var known) && known.Outcome == RoomOutcomes.Applied, Is.True,
                    "전제: a의 보낼 목록에 남은 꾸밈 줄은 방이 이미 반영한 것: " + line.Kind);

            // 옛 서버가 같은 방 슬롯을 연다: 꾸밈 조각을 모른다. 옛 플레이어 쪽(a)이 가구·상자를 다루고 하루를 넘긴다.
            var old = new Room(saves.Store, RoomSlot, Older(rules));
            Assert.That(old.Build, Is.Null, "전제: 옛 서버에는 꾸밈 조각이 없다");
            var oldShares = old.Join(Guest, Older());
            var oldA = Play(guests, oldShares, knowsHouseBuild: false);
            Assert.That(oldA.SharesHouse, Is.True);
            Assert.That(oldA.House.Placed.Count, Is.EqualTo(1), "상자가 보인다");
            string chair = FreeChair(oldA);
            Assert.That(oldA.PlaceFurniture(chair, HouseRoom.FirstFreeAnchor(oldA, chair), FurniturePlacementDirection.North), Is.False);
            old.DeliverAll(oldShares);
            Assert.That(oldA.House.Placed.Count, Is.EqualTo(2), "옛 서버에서도 가구를 놓는다");
            int wood = oldA.Inventory.CountOf(oldA.Item(Wood));
            Assert.That(oldA.TakeFromStorage(chest, Wood, 2), Is.EqualTo(StorageTransferResult.Requested));
            old.DeliverAll(oldShares);
            Assert.That(oldA.Inventory.CountOf(oldA.Item(Wood)), Is.EqualTo(wood + 2), "옛 서버에서도 상자를 쓴다");
            Assert.That(old.Chests.CountOf(chest, Wood), Is.EqualTo(3));
            int day = oldA.Student.CurrentDay;
            if (oldA.Student.DayState == StudentDayState.InProgress) oldA.VisitLocation("location.town-square");
            if (oldA.Student.DayState == StudentDayState.InProgress) Assert.That(oldA.EndDay().Ended, Is.True);
            Assert.That(oldA.StartNextDay(), Is.True);
            Assert.That(oldA.Student.CurrentDay, Is.EqualTo(day + 1));
            old.Pieces.Save(saves.Store, RoomSlot);

            Assert.That(File.ReadAllBytes(finishPath), Is.EqualTo(finishBytes), "옛 서버는 꾸밈 기록 파일을 건드리지 않는다");
            Assert.That(File.ReadAllBytes(holdsPath), Is.EqualTo(holdsBytes), "옛 서버는 잡기 파일을 건드리지 않는다");

            // 새 서버로 돌아오면 꾸밈·잡기가 이어지고, 옛 서버에서 한 것도 남는다. b가 다시 들어오면 그 잡기를 끝낸다(한 번만 낸다).
            old.Leave(oldShares);
            var back = new Room(saves.Store, RoomSlot, Current(rules));
            Assert.That(back.Build.ToSaveData().Finishes.Length, Is.EqualTo(TopLeftRoomFill));
            Assert.That(back.Build.HeldCount, Is.EqualTo(1));
            var backA = Play(guests, back.Join(Guest, Current()));
            Assert.That(backA.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "꾸밈이 그대로");
            Assert.That(backA.House.Placed.Count, Is.EqualTo(2), "옛 서버에서 놓은 가구도 남는다");
            Assert.That(back.Chests.CountOf(chest, Wood), Is.EqualTo(3));
            var backBShares = back.Join("b", Current());
            var backB = Play(guests, backBShares, "b");
            back.DeliverAll(backBShares);
            Assert.That(back.Build.HeldCount, Is.Zero);
            Assert.That(backB.HouseFloorAt(NextToTopLeft), Is.EqualTo(other.Id), "걸려 있던 잡기가 끝난다");
            Assert.That(Money(backB), Is.EqualTo(bMoney - PerCell(other)), "한 번만 낸다");
        }

        [Test]
        public void AnOlderPlayer_KeepsTheHouseBuildLinesUntilAnswered_ThenTheOlderRoomAnswersInvalid_AndTheyAreDropped()
        {
            // 보낼 목록(room-outbox-<플레이어>.json, 모양 RoomOutboxSaveData)에 꾸밈 요청 줄(조각 house-build)이 든 세이브를 옛 플레이어 쪽이 읽는다.
            // 고정하는 옛 동작: 읽을 때 버리지 않는다 → 저장할 때 그대로 다시 쓴다 → 방에 보낸다 → 옛 방은 모르는 조각이라 Invalid로 답하고 장부에 적는다 →
            // 플레이어 쪽은 답을 받은 요청을 목록에서 지운다(그 조각의 플레이어 쪽이 없으니 아무 일도 하지 않는다). 예외는 없다.
            using var saves = new TempSaves();
            var guests = Guests(saves);
            var rules = Rules();
            var room = Room.Create(saves.Store, RoomSlot, Current(rules), 0, RoomHouse.PieceName);
            var shares = room.Join(Guest, Current());
            var game = Play(guests, shares);
            var floor = PaidFloor();
            Give(game, PerCell(floor) * TopLeftRoomFill);
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(shares); // 방이 잡고 답한다 → 낸다(쓰기를 같은 저장에)
            Assert.That(HouseBuildRequests(shares, RoomHouseBuild.Use), Is.EqualTo(1), "준비: 돈을 낸 쓰기가 보낼 목록에");
            room.Leave(shares); // 쓰기를 보내기 전에 꺼졌다
            var line = SavedOutbox(guests).Requests.Single(r => r.Piece == RoomHouseBuild.PieceName);
            Assert.That(line.Kind, Is.EqualTo(RoomHouseBuild.Use), "전제: 세이브의 보낼 목록에 꾸밈 쓰기 줄");
            string holdsPath = Path.Combine(RoomFolder(saves), HoldsFile);
            byte[] holdsBytes = File.ReadAllBytes(holdsPath);

            var old = new Room(saves.Store, RoomSlot, Older(rules));
            var oldShares = old.Join(Guest, Older());
            GameSession oldGame = null;
            Assert.DoesNotThrow(() => oldGame = Play(guests, oldShares, knowsHouseBuild: false), "옛 플레이어 쪽이 모르는 조각의 줄로 시작을 거절하지 않는다");
            Assert.That(oldShares.Outbox.Requests.Any(r => r.RequestId == line.RequestId), Is.True, "읽을 때 버리지 않는다");
            oldGame.VisitLocation("location.town-square"); // 저장
            Assert.That(SavedOutbox(guests).Requests.Any(r => r.RequestId == line.RequestId && r.Kind == RoomHouseBuild.Use && r.Ids.SequenceEqual(line.Ids)),
                Is.True, "저장할 때 그대로 다시 쓴다");

            old.Deliver(oldShares); // 방에 보낸다
            Assert.That(old.Pieces.TryAnswer(Guest, line.RequestId, out var answer), Is.True, "옛 방이 그 줄을 받았다");
            Assert.That(answer.Outcome, Is.EqualTo(RoomOutcomes.Invalid), "모르는 조각이라 Invalid");
            Assert.That(oldShares.Outbox.Requests.Any(r => r.RequestId == line.RequestId), Is.False, "답을 받은 줄은 지운다");
            oldGame.VisitLocation("location.town-square");
            Assert.That(SavedOutbox(guests).Requests.Any(r => r.Piece == RoomHouseBuild.PieceName), Is.False, "다음 저장의 보낼 목록에는 없다");
            Assert.That(File.ReadAllBytes(holdsPath), Is.EqualTo(holdsBytes), "방의 잡기 파일은 그대로(잡기가 남는다)");
        }

        // 결함 재현(task-4-report): 옛 플레이어 쪽이 돈을 낸 꾸밈 쓰기 줄을 옛 방에 보내 지운 뒤 새 판으로 돌아오면 그 잡기가 떠도는 잡기로 풀려
        // 낸 돈이 사라졌다. 돈 낸 잡기를 플레이어 세이브의 따로 된 파일(room-house-build-paid-<플레이어>.json)에 표시해 고쳤다.
        [Test]
        public void AfterAnOlderPlayerDroppedAPaidUse_BackInTheNewProgram_TheMoneyIsNotLost()
        {
            using var saves = new TempSaves();
            var guests = Guests(saves);
            var rules = Rules();
            var room = Room.Create(saves.Store, RoomSlot, Current(rules), 0, RoomHouse.PieceName);
            var shares = room.Join(Guest, Current());
            var game = Play(guests, shares);
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            Give(game, cost);
            int money = Money(game);
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(shares); // 잡음 → 낸다
            Assert.That(Money(game), Is.EqualTo(money - cost), "준비: 돈을 냈다");
            room.Leave(shares);

            var old = new Room(saves.Store, RoomSlot, Older(rules));
            var oldShares = old.Join(Guest, Older());
            var oldGame = Play(guests, oldShares, knowsHouseBuild: false);
            old.DeliverAll(oldShares);
            oldGame.VisitLocation("location.town-square");
            old.Leave(oldShares);

            var back = new Room(saves.Store, RoomSlot, Current(rules));
            var backShares = back.Join(Guest, Current());
            var backGame = Play(guests, backShares);
            back.DeliverAll(backShares);
            Assert.That(backGame.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "돈을 낸 꾸밈이 된다");
            Assert.That(Money(backGame), Is.EqualTo(money - cost), "돈은 한 번만");
        }

        /// <summary>
        /// 상위호환: 돈을 낸 표시(room-house-build-paid-&lt;플레이어&gt;.json)는 꾸밈 조각을 모르는 옛 플레이어 쪽·옛 방을 거쳐도 바이트 그대로 남고,
        /// 새 프로그램으로 돌아오면 쓰기를 다시 보내 꾸밈이 되고 돈·인테리어 기술은 한 번만이다. 쓰기가 적용된 뒤 다음 저장에서 표시가 지워진다.
        /// 손님 길(방이 손님 세이브를 맡는다 — &lt;방 슬롯&gt;/guests/&lt;id&gt;/)과 방장 길(이 PC의 방 슬롯, 방이 아는 신원 ≠ 게임 한 판의 플레이어 id) 둘 다.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void APaidRecord_SurvivesAnOlderPlayerAndAnOlderRoom_BackInTheNewProgram_TheUseGoesAgainOnce(bool hostPath)
        {
            using var saves = new TempSaves();
            var rules = Rules();
            // 손님 길: 게임 한 판의 저장소는 방의 손님 폴더(슬롯 = 플레이어 id). 방장 길: 이 PC의 방 슬롯 그 자체(플레이어 id = 기본 플레이어).
            ISaveStore playerStore = hostPath ? saves.Store : Guests(saves);
            string slot = hostPath ? RoomSlot : Guest;
            string player = hostPath ? SaveFileNames.DefaultPlayerId : Guest;
            string identity = hostPath ? "host-room-id" : Guest;
            string playerFolder = hostPath ? RoomFolder(saves) : Path.Combine(GuestsFolder(saves.Path), Guest);
            string paidPath = Path.Combine(playerFolder, SaveFileNames.RoomHouseBuildPaid(player));
            GameSession Begin(RoomShares shares, bool knowsHouseBuild = true)
            {
                var g = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, playerStore, slot, player, shares: shares);
                g.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
                if (knowsHouseBuild) g.EnterHouseBuild(HouseRoom.Frame);
                return g;
            }

            var room = Room.Create(saves.Store, RoomSlot, Current(rules), 0, RoomHouse.PieceName);
            var shares = room.Join(identity, Current());
            var game = Begin(shares);
            Assert.That(game.PlayerId, Is.EqualTo(player));
            if (hostPath) Assert.That(shares.RoomPlayerId, Is.Not.EqualTo(game.PlayerId), "전제: 방장 길은 두 id가 다르다");
            var floor = PaidFloor();
            int cost = PerCell(floor) * TopLeftRoomFill;
            Give(game, cost);
            int money = Money(game);
            var growth = TestWorld.Definitions.All<Rootborn.Core.Workspaces.PlacementSkillGrowth>().First(g => g.SurfaceId == HouseRoom.SurfaceId);
            int skill = game.Student.GetSkillValue(growth.SkillId);
            Assert.That(File.Exists(paidPath), Is.False, "전제: 돈을 내기 전에는 표시 파일이 없다");
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            string hold = shares.Outbox.Requests.Single(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Finish).RequestId;
            room.Deliver(shares); // 잡음 → 낸다(쓰기와 표시가 같은 저장에)
            Assert.That(Money(game), Is.EqualTo(money - cost), "준비: 돈을 냈다");
            Assert.That(playerStore.TryRead<RoomHouseBuildPaidSaveData>(slot, SaveFileNames.RoomHouseBuildPaid(player), out var paid), Is.True,
                "돈을 낸 저장 묶음에 표시 파일이 든다");
            Assert.That(paid.Holds, Is.EqualTo(new[] { hold }));
            room.Leave(shares); // 쓰기를 보내기 전에 꺼졌다
            byte[] paidBytes = File.ReadAllBytes(paidPath);

            // 옛 빌드(옛 방 + 옛 플레이어 쪽)가 같은 방 슬롯을 연다: 쓰기 줄을 옛 방에 보내 Invalid를 받아 지우고, 저장한다.
            var old = new Room(saves.Store, RoomSlot, Older(rules));
            var oldShares = old.Join(identity, Older());
            var oldGame = Begin(oldShares, knowsHouseBuild: false);
            old.DeliverAll(oldShares);
            oldGame.VisitLocation("location.town-square");
            old.Pieces.Save(saves.Store, RoomSlot);
            old.Leave(oldShares);
            Assert.That(playerStore.TryRead<RoomOutboxSaveData>(slot, SaveFileNames.RoomOutbox(player), out var oldOutbox)
                && oldOutbox.Requests.Any(r => r.Piece == RoomHouseBuild.PieceName), Is.False, "전제: 옛 빌드가 쓰기 줄을 지웠다");
            Assert.That(File.ReadAllBytes(paidPath), Is.EqualTo(paidBytes), "옛 빌드는 표시 파일을 건드리지 않는다");

            // 새 프로그램으로 돌아온다: 표시가 있는 잡기는 풀지 않고 쓰기를 새 id로 다시 보낸다.
            var back = new Room(saves.Store, RoomSlot, Current(rules));
            var backShares = back.Join(identity, Current());
            var backGame = Begin(backShares);
            Assert.That(HouseBuildRequests(backShares, RoomHouseBuild.Release), Is.Zero, "돈 낸 표시가 있으면 풀지 않는다");
            var use = backShares.Outbox.Requests.Single(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Use);
            Assert.That(use.Ids, Is.EqualTo(new[] { hold }));
            Assert.That(back.Pieces.TryAnswer(identity, use.RequestId, out _), Is.False, "새 id(옛 방 장부의 Invalid와 겹치지 않는다)");
            Assert.That(playerStore.TryRead<RoomOutboxSaveData>(slot, SaveFileNames.RoomOutbox(player), out var resent)
                && resent.Requests.Any(r => r.RequestId == use.RequestId), Is.True, "다시 적은 쓰기가 저장됐다(손님이면 그 저장과 함께 방으로 간다)");
            back.DeliverAll(backShares);
            Assert.That(backGame.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "돈을 낸 꾸밈이 된다");
            Assert.That(back.Build.HeldCount, Is.Zero);
            Assert.That(Money(backGame), Is.EqualTo(money - cost), "돈은 한 번만");
            Assert.That(backGame.Student.GetSkillValue(growth.SkillId), Is.EqualTo(skill + growth.Amount), "인테리어 기술도 한 번만");

            backGame.VisitLocation("location.town-square"); // 다음 저장
            Assert.That(playerStore.TryRead<RoomHouseBuildPaidSaveData>(slot, SaveFileNames.RoomHouseBuildPaid(player), out var after), Is.True);
            Assert.That(after.Holds, Is.Empty, "쓰기가 적용된 잡기의 표시는 다음 저장에서 지운다");
        }

        // ---------- 고정 폴더 room-20261114 ----------

        /// <summary>
        /// 지금 프로그램이 쓴 방 슬롯을 고정 폴더로 내보낸다(손으로만): 돈·집을 같이 쓰는 방에 손님 하나(기본 플레이어)가 상자를 놓고 나무 5개를 넣고,
        /// 왼쪽 위 방 바닥을 방 지갑으로 꾸미고(87칸), 그 옆 칸 하나를 붓으로 요청해 방이 잡았지만 답을 받기 전에 끊긴 때.
        /// 쓰는 법: ROOTBORN_FIXTURE_OUT=&lt;저장소&gt;/game/tests/Rootborn.Data.Tests/Fixtures/saves/room-20261114 dotnet test game/tests/Rootborn.Game.Tests --filter Name~WriteCurrentRoomFixture
        /// </summary>
        [Test, Explicit("고정 파일을 새로 만들 때만 손으로 돌린다")]
        public void WriteCurrentRoomFixture()
        {
            string target = Environment.GetEnvironmentVariable("ROOTBORN_FIXTURE_OUT");
            Assert.That(target, Is.Not.Null.And.Not.Empty, "ROOTBORN_FIXTURE_OUT에 새 폴더를 준다");
            string roomFolder = Path.Combine(target, RoomSlot);
            Assert.That(Directory.Exists(roomFolder), Is.False, "이미 있는 고정 폴더는 고치지 않는다: " + roomFolder);

            using var saves = new TempSaves();
            var guests = Guests(saves);
            var room = Room.Create(saves.Store, RoomSlot, Current(Rules()), 10_000, RoomWallet.PieceName, RoomHouse.PieceName);
            var shares = room.Join(Guest, Current());
            var game = Play(guests, shares);
            Assert.That(game.SharesWallet && game.SharesHouseBuild, Is.True);
            string chest = PlaceChest(room, shares, game);
            game.Inventory.Add(game.Item(Wood), 8);
            Assert.That(game.PutIntoStorage(chest, Wood, 5), Is.EqualTo(StorageTransferResult.Requested));
            room.DeliverAll(shares);
            Assert.That(room.Chests.CountOf(chest, Wood), Is.EqualTo(5));
            Assert.That(game.ApplyHouseFinish(PaidFloor().Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.DeliverAll(shares);
            Assert.That(room.Build.ToSaveData().Finishes.Length, Is.EqualTo(TopLeftRoomFill));
            Assert.That(game.ApplyHouseFinish(OtherPaidFloor().Id, HouseFinishTools.Brush, NextToTopLeft), Is.False);
            room.DeliverAndLoseTheAnswers(shares);
            Assert.That(room.Build.HeldCount, Is.EqualTo(1));
            Assert.That(SavedOutbox(guests).Requests.Count(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Finish), Is.EqualTo(1));

            foreach (var pair in AllFiles(RoomFolder(saves)))
            {
                string path = Path.Combine(roomFolder, pair.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pair.Value);
            }
        }

        [Test]
        public void Room20261114_Opens_ReadingWritesNothing_TheGuestComesBackAndFinishesTheHoldOnce()
        {
            using var saves = new TempSaves();
            CopyRoomFixture(saves);
            var guests = Guests(saves);
            var before = AllFiles(RoomFolder(saves));
            Assert.That(before.Keys, Does.Contain(FinishFile).And.Contain(HoldsFile), "전제: 11-14 방 고정 폴더에 꾸밈 조각 두 파일이 있다");
            var rules = Rules();

            var room = new Room(saves.Store, RoomSlot, Current(rules));
            Assert.That(room.Build.ToSaveData().Finishes.Length, Is.EqualTo(TopLeftRoomFill));
            Assert.That(room.Build.HeldCount, Is.EqualTo(1));
            Assert.That(room.House.Placed.Count, Is.EqualTo(1), "상자 하나");
            string chest = room.House.Placed[0].InstanceId;
            Assert.That(room.Chests.CountOf(chest, Wood), Is.EqualTo(5));
            int wallet = room.Wallet.Balance;
            Assert.That(Same(before, AllFiles(RoomFolder(saves))), Is.True, "읽기만으로 아무것도 쓰지 않는다");

            var shares = room.Join(Guest, Current());
            var game = Play(guests, shares);
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(PaidFloor().Id), "꾸밈이 보인다");
            Assert.That(game.WaitingForRoom, Is.True, "보낼 목록의 잡기 요청을 기다린다");
            room.DeliverAll(shares); // 장부가 같은 답(잡음)을 다시 준다 → 방 지갑으로 낸다 → 쓰기
            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(game.HouseFloorAt(NextToTopLeft), Is.EqualTo(OtherPaidFloor().Id), "걸려 있던 꾸밈이 끝난다");
            Assert.That(room.Wallet.Balance, Is.EqualTo(wallet - PerCell(OtherPaidFloor())), "방 지갑에서 한 번만");
            Assert.That(room.Wallet.HeldTotal, Is.Zero);
            room.DeliverAll(shares);
            Assert.That(room.Wallet.Balance, Is.EqualTo(wallet - PerCell(OtherPaidFloor())), "다시 돌려도 더 나가지 않는다");
        }

        // ---------- 고정 폴더 room-20261114b: 돈 낸 표시가 처음 든 방 ----------

        private const string PaidFixture = "room-20261114b";

        /// <summary>
        /// 지금 프로그램이 쓴 방 슬롯을 고정 폴더로 내보낸다(손으로만): 집을 같이 쓰는(돈은 각자) 방에 손님 하나(기본 플레이어)가 왼쪽 위 방 바닥을 자기 지갑으로
        /// 꾸미려 했고, 방이 잡아 돈을 냈지만(쓰기와 돈 낸 표시가 같은 저장에) 쓰기가 방에 닿기 전에 끊긴 때.
        /// 쓰는 법: ROOTBORN_FIXTURE_OUT=&lt;저장소&gt;/game/tests/Rootborn.Data.Tests/Fixtures/saves/room-20261114b dotnet test game/tests/Rootborn.Game.Tests --filter Name~WritePaidRecordRoomFixture
        /// </summary>
        [Test, Explicit("고정 파일을 새로 만들 때만 손으로 돌린다")]
        public void WritePaidRecordRoomFixture()
        {
            string target = Environment.GetEnvironmentVariable("ROOTBORN_FIXTURE_OUT");
            Assert.That(target, Is.Not.Null.And.Not.Empty, "ROOTBORN_FIXTURE_OUT에 새 폴더를 준다");
            string roomFolder = Path.Combine(target, RoomSlot);
            Assert.That(Directory.Exists(roomFolder), Is.False, "이미 있는 고정 폴더는 고치지 않는다: " + roomFolder);

            using var saves = new TempSaves();
            var guests = Guests(saves);
            var room = Room.Create(saves.Store, RoomSlot, Current(Rules()), 0, RoomHouse.PieceName);
            var shares = room.Join(Guest, Current());
            var game = Play(guests, shares);
            var floor = PaidFloor();
            Give(game, PerCell(floor) * TopLeftRoomFill + 7);
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.False);
            room.Deliver(shares); // 잡음 → 낸다
            Assert.That(Money(game), Is.EqualTo(7));
            Assert.That(room.Build.HeldCount, Is.EqualTo(1));
            Assert.That(SavedOutbox(guests).Requests.Count(r => r.Piece == RoomHouseBuild.PieceName && r.Kind == RoomHouseBuild.Use), Is.EqualTo(1));
            Assert.That(guests.TryRead<RoomHouseBuildPaidSaveData>(Guest, SaveFileNames.RoomHouseBuildPaid(Guest), out var paid) && paid.Holds.Length == 1, Is.True);
            room.Leave(shares);

            foreach (var pair in AllFiles(RoomFolder(saves)))
            {
                string path = Path.Combine(roomFolder, pair.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pair.Value);
            }
        }

        [Test]
        public void Room20261114b_Opens_ReadingWritesNothing_TheGuestComesBack_TheUseApplies_MoneyOnce_TheRecordIsForgotten()
        {
            using var saves = new TempSaves();
            CopyRoomFixture(saves, PaidFixture);
            var guests = Guests(saves);
            var before = AllFiles(RoomFolder(saves));
            string paidName = "guests/" + Guest + "/" + SaveFileNames.RoomHouseBuildPaid(Guest);
            Assert.That(before.Keys, Does.Contain(paidName), "전제: 11-14b 방 고정 폴더의 손님 세이브에 돈 낸 표시가 있다");
            var room = new Room(saves.Store, RoomSlot, Current(Rules()));
            Assert.That(room.Build.HeldCount, Is.EqualTo(1));
            Assert.That(Same(before, AllFiles(RoomFolder(saves))), Is.True, "읽기만으로 아무것도 쓰지 않는다");

            var shares = room.Join(Guest, Current());
            var game = Play(guests, shares);
            Assert.That(Money(game), Is.EqualTo(7), "돈은 이미 냈다");
            Assert.That(HouseBuildRequests(shares, RoomHouseBuild.Release), Is.Zero);
            Assert.That(HouseBuildRequests(shares, RoomHouseBuild.Use), Is.EqualTo(1), "세이브의 쓰기가 간다");
            room.DeliverAll(shares);
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(PaidFloor().Id));
            Assert.That(room.Build.HeldCount, Is.Zero);
            Assert.That(Money(game), Is.EqualTo(7), "한 번만");
            game.VisitLocation("location.town-square");
            Assert.That(guests.TryRead<RoomHouseBuildPaidSaveData>(Guest, SaveFileNames.RoomHouseBuildPaid(Guest), out var after) && after.Holds.Length == 0, Is.True,
                "끝난 잡기의 표시는 다음 저장에서 지운다");
        }

        [Test]
        public void Room20261114b_ThroughAnOlderBuildFirst_TheRecordStays_BackInTheNewProgramTheUseApplies_MoneyOnce()
        {
            using var saves = new TempSaves();
            CopyRoomFixture(saves, PaidFixture);
            var guests = Guests(saves);
            var rules = Rules();
            string paidPath = Path.Combine(GuestsFolder(saves.Path), Guest, SaveFileNames.RoomHouseBuildPaid(Guest));
            byte[] paidBytes = File.ReadAllBytes(paidPath);

            var old = new Room(saves.Store, RoomSlot, Older(rules));
            var oldShares = old.Join(Guest, Older());
            var oldGame = Play(guests, oldShares, knowsHouseBuild: false);
            Assert.That(Money(oldGame), Is.EqualTo(7), "옛 빌드에서도 열리고 돈이 그대로");
            old.DeliverAll(oldShares); // 쓰기 줄 → 옛 방 Invalid → 지움
            oldGame.VisitLocation("location.town-square");
            old.Pieces.Save(saves.Store, RoomSlot);
            old.Leave(oldShares);
            Assert.That(SavedOutbox(guests).Requests.Any(r => r.Piece == RoomHouseBuild.PieceName), Is.False, "전제: 옛 빌드가 쓰기 줄을 지웠다");
            Assert.That(File.ReadAllBytes(paidPath), Is.EqualTo(paidBytes), "옛 빌드는 돈 낸 표시를 건드리지 않는다");

            var back = new Room(saves.Store, RoomSlot, Current(rules));
            var backShares = back.Join(Guest, Current());
            var backGame = Play(guests, backShares);
            Assert.That(HouseBuildRequests(backShares, RoomHouseBuild.Release), Is.Zero);
            back.DeliverAll(backShares);
            Assert.That(backGame.HouseFloorAt(TopLeftRoom), Is.EqualTo(PaidFloor().Id), "돈을 낸 꾸밈이 된다");
            Assert.That(back.Build.HeldCount, Is.Zero);
            Assert.That(Money(backGame), Is.EqualTo(7), "한 번만");
        }
    }
}
