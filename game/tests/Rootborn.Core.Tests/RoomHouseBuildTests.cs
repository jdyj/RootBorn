using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.Tests.Building;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 같이 쓰는 집의 꾸밈(방 조각 house-build, 집 꾸미기 1단계 (다)): 방이 꾸밈 요청을 다시 판정해 덮는 칸을 잡고(잡기),
    /// 요청한 쪽이 돈을 낸 뒤 쓰기로 적용하거나 못 내면 풀기. 잡힌 칸은 다른 꾸밈 요청이 바꾸지 못하고, 잡기는 방 세이브에 남는다. 자동 테스트.
    /// </summary>
    public sealed class RoomHouseBuildTests
    {
        // HouseFinishesTests와 같은 꼴의 작은 집: 두 방(3x3) 사이에 폭 1 문간 (4,3). 위 두 줄 벽은 정면(faceHeight 2).
        private static readonly string[] Plan =
        {
            "#########",
            "#########",
            "#...#...#",
            "#.......#",
            "#...#...#",
            "#########",
        };

        private const string Surface = "house", Annex = "annex";
        private const string FrameId = "frame.test";
        private const string WallPlain = "wall.plain", WallBlue = "wall.blue";
        private const string FloorPlain = "floor.plain", FloorOak = "floor.oak", FloorLocked = "floor.locked", Roof = "roof.odd";
        private const string BandCream = "band.cream", BandBrown = "band.brown", BandGreen = "band.green", DefaultBandArt = "band.default";
        private const string Coin = "coin";

        private static readonly CellPos LeftMiddle = new CellPos(2, 3);

        private static readonly RoomFinishDefinition[] Defs =
        {
            Def(WallPlain, RoomFinishSlots.Wall, null, DefaultArt(WallPlain), 1),
            Def(WallBlue, RoomFinishSlots.Wall, null, DefaultArt(WallBlue), 2),
            Def(FloorPlain, RoomFinishSlots.Floor, null, DefaultArt(FloorPlain), 1),
            Def(FloorOak, RoomFinishSlots.Floor, null, DefaultArt(FloorOak), 3),
            Def(FloorLocked, RoomFinishSlots.Floor, "unlock.floor", DefaultArt(FloorLocked), 1),
            Def(Roof, "roof", null, DefaultArt(Roof), 1),
            Def(BandCream, RoomFinishSlots.Band, null, DefaultBandArt, 0),
            Def(BandBrown, RoomFinishSlots.Band, null, "band.art.brown", 4),
            Def(BandGreen, RoomFinishSlots.Band, null, "band.art.green", 4),
        };

        private static string DefaultArt(string id) => "art." + id;

        private static RoomFinishDefinition Def(string id, string slot, string token, string art, int coins) =>
            new RoomFinishDefinition(id, slot, "name." + id, "sheet.png", art,
                coins > 0 ? new[] { new ItemAmount<string>(Coin, coins) } : Array.Empty<ItemAmount<string>>(), token);

        private static HouseBuildFrame Frame(string id = FrameId) =>
            new HouseBuildFrame(id, RoomTilePlan.Build(RoomFillTests.Grid(Plan), 2, 16, null), WallPlain, FloorPlain, DefaultBandArt);

        private static RoomHouseRules Rules(params string[] without) =>
            new RoomHouseRules(Array.Empty<FurnitureDefinition>(), _ => null,
                buildFrameFor: s => s == Surface ? Frame() : s == Annex ? Frame("frame.other") : null,
                finishes: Defs.Where(d => !without.Contains(d.Id)));

        private static RoomHouseBuild Piece() => new RoomHouseBuild(Rules());

        private static RoomRequestSaveData Req(string id, string kind, int amount, params string[] ids)
            => new RoomRequestSaveData { Piece = RoomHouseBuild.PieceName, RequestId = id, Kind = kind, Amount = amount, Ids = ids };

        private static string N(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string Finish(IRoomPiece piece, string player, string id, string finish, string tool, CellPos at, int expected, string surface = Surface)
            => piece.Apply(player, Req(id, RoomHouseBuild.Finish, expected, surface, finish, tool, N(at.X), N(at.Y)), 1).Outcome;

        private static string Band(IRoomPiece piece, string player, string id, string finish, int amount = 1, string surface = Surface)
            => piece.Apply(player, Req(id, RoomHouseBuild.Band, amount, surface, finish), 1).Outcome;

        private static string Use(IRoomPiece piece, string player, string id, string hold)
            => piece.Apply(player, Req(id, RoomHouseBuild.Use, 0, hold), 1).Outcome;

        private static string Release(IRoomPiece piece, string player, string id, string hold)
            => piece.Apply(player, Req(id, RoomHouseBuild.Release, 0, hold), 1).Outcome;

        private static (int, int, string, string)[] Cells(HouseBuildSaveData data) => data.Finishes.Select(c => (c.X, c.Y, c.Wall, c.Floor)).ToArray();

        // 왼쪽 방 + 문간(방 채우기가 덮는 칸, HouseFinishesTests와 같은 10칸)
        private static readonly CellPos[] LeftRoom =
        {
            new CellPos(1, 2), new CellPos(2, 2), new CellPos(3, 2),
            new CellPos(1, 3), new CellPos(2, 3), new CellPos(3, 3), new CellPos(4, 3),
            new CellPos(1, 4), new CellPos(2, 4), new CellPos(3, 4),
        };

        [Test]
        public void Finish_HoldsCells_UseApplies_StateShowsIt()
        {
            var piece = Piece();
            int revision = piece.Revision;
            Assert.That(Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(piece.HeldCount, Is.EqualTo(1));
            Assert.That(piece.Revision, Is.GreaterThan(revision));
            Assert.That(piece.ToSaveData().Finishes, Is.Empty, "잡기만 했다 — 꾸밈은 아직 그대로");

            Assert.That(Use(piece, "ana", "u1", "h1"), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(piece.HeldCount, Is.EqualTo(0));
            var data = piece.ToSaveData();
            Assert.That(data.FrameId, Is.EqualTo(FrameId));
            Assert.That(data.Finishes.Select(c => new CellPos(c.X, c.Y)), Is.EquivalentTo(LeftRoom));
            Assert.That(data.Finishes.All(c => c.Floor == FloorOak && c.Wall == ""), Is.True);

            var state = piece.State();
            Assert.That(state.Piece, Is.EqualTo(RoomHouseBuild.PieceName));
            Assert.That(state.Amount, Is.EqualTo(10), "기록 칸 수");
            var holds = new List<KeyValuePair<string, string>>();
            Assert.That(RoomHouseBuildLines.TryDecode(state.Ids, out var decoded, holds), Is.True);
            Assert.That(decoded.FrameId, Is.EqualTo(FrameId));
            Assert.That(decoded.Band, Is.EqualTo(""));
            Assert.That(Cells(decoded), Is.EqualTo(Cells(data)));
            Assert.That(holds, Is.Empty);
        }

        [Test]
        public void Release_FreesTheCells_NothingChanges()
        {
            var piece = Piece();
            Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10);
            Assert.That(Release(piece, "ana", "r1", "h1"), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(piece.HeldCount, Is.EqualTo(0));
            Assert.That(piece.ToSaveData().Finishes, Is.Empty, "풀기는 꾸밈을 바꾸지 않는다");
            Assert.That(Finish(piece, "bo", "h2", FloorLocked, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Applied), "풀린 칸은 다시 잡힌다");
        }

        [Test]
        public void HeldCells_RefuseAnotherRequestOnTheSameSlot_OtherSlotStillWorks()
        {
            var piece = Piece();
            Assert.That(Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Finish(piece, "bo", "h2", FloorLocked, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Refused), "같은 방 바닥");
            Assert.That(Finish(piece, "bo", "h3", FloorLocked, HouseFinishTools.Brush, new CellPos(1, 2), 1), Is.EqualTo(RoomOutcomes.Refused), "잡힌 칸 하나");
            Assert.That(Finish(piece, "ana", "h4", FloorLocked, HouseFinishTools.Brush, new CellPos(3, 4), 1), Is.EqualTo(RoomOutcomes.Refused), "자기 잡기와도 겹치지 않는다");
            Assert.That(Finish(piece, "bo", "h5", WallBlue, HouseFinishTools.Room, LeftMiddle, 4), Is.EqualTo(RoomOutcomes.Applied), "같은 방 벽지는 다른 자리");
            Assert.That(Finish(piece, "bo", "h6", FloorOak, HouseFinishTools.Brush, new CellPos(6, 3), 1), Is.EqualTo(RoomOutcomes.Applied), "다른 방 바닥");
            Assert.That(piece.HeldCount, Is.EqualTo(3));
        }

        [Test]
        public void ExpectedCountDiffers_Refused()
        {
            var piece = Piece();
            Assert.That(Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 9), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Finish(piece, "ana", "h2", FloorOak, HouseFinishTools.Room, LeftMiddle, 11), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(piece.HeldCount, Is.EqualTo(0));
            // 남이 먼저 일부를 바꿨으면 요청한 쪽이 본 칸 수와 달라 거절(돈을 내지 않는다)
            Finish(piece, "bo", "h3", FloorOak, HouseFinishTools.Brush, new CellPos(1, 2), 1);
            Use(piece, "bo", "u3", "h3");
            Assert.That(Finish(piece, "ana", "h4", FloorOak, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Finish(piece, "ana", "h5", FloorOak, HouseFinishTools.Room, LeftMiddle, 9), Is.EqualTo(RoomOutcomes.Applied));
        }

        [Test]
        public void NothingToChange_Refused()
        {
            var piece = Piece();
            Assert.That(Finish(piece, "ana", "h0", FloorPlain, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Refused), "프레임 기본과 같다");
            Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10);
            Use(piece, "ana", "u1", "h1");
            int revision = piece.Revision;
            Assert.That(Finish(piece, "bo", "h2", FloorOak, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Refused), "같은 꾸밈을 다시");
            Assert.That(piece.HeldCount, Is.EqualTo(0));
            Assert.That(piece.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void WallpaperBrushOnACellWithoutAFace_Refused()
        {
            var piece = Piece();
            Assert.That(Finish(piece, "ana", "h1", WallBlue, HouseFinishTools.Brush, new CellPos(2, 4), 1), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Finish(piece, "ana", "h2", WallBlue, HouseFinishTools.Brush, new CellPos(2, 2), 1), Is.EqualTo(RoomOutcomes.Applied));
        }

        [Test]
        public void UseOrReleaseByAnotherPlayer_Invalid()
        {
            var piece = Piece();
            Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10);
            Assert.That(Use(piece, "bo", "u1", "h1"), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Release(piece, "bo", "r1", "h1"), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Use(piece, "ana", "u2", "missing"), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(piece.HeldCount, Is.EqualTo(1));
            Assert.That(piece.ToSaveData().Finishes, Is.Empty);
            Assert.That(Use(piece, "ana", "u3", "h1"), Is.EqualTo(RoomOutcomes.Applied));
        }

        [Test]
        public void UseTwice_SecondInvalid()
        {
            var piece = Piece();
            Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10);
            Assert.That(Use(piece, "ana", "u1", "h1"), Is.EqualTo(RoomOutcomes.Applied));
            var after = Cells(piece.ToSaveData());
            Assert.That(Use(piece, "ana", "u2", "h1"), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Release(piece, "ana", "r2", "h1"), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Cells(piece.ToSaveData()), Is.EqualTo(after));
        }

        [Test]
        public void LockedFinish_IsTrustedToTheRequester()
        {
            // 해금은 요청한 사람의 진행이다(가구 놓기와 같다) — 방은 확인하지 않는다
            var piece = Piece();
            Assert.That(Finish(piece, "ana", "h1", FloorLocked, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Use(piece, "ana", "u1", "h1"), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(piece.ToSaveData().Finishes.All(c => c.Floor == FloorLocked), Is.True);
        }

        [Test]
        public void UnknownFinish_BadTool_BadNumbers_Invalid()
        {
            var piece = Piece();
            Assert.That(Finish(piece, "ana", "a", "no.such", HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Finish(piece, "ana", "b", Roof, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Invalid), "벽·바닥이 아닌 자리");
            Assert.That(Finish(piece, "ana", "c", BandBrown, HouseFinishTools.Room, LeftMiddle, 1), Is.EqualTo(RoomOutcomes.Invalid), "띠는 band 요청으로");
            Assert.That(Finish(piece, "ana", "d", FloorOak, "spray", LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Finish(piece, "ana", "e", FloorOak, HouseFinishTools.Room, LeftMiddle, 0), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Finish(piece, "ana", "f", FloorOak, HouseFinishTools.Room, LeftMiddle, -3), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(piece.Apply("ana", Req("g", RoomHouseBuild.Finish, 10, Surface, FloorOak, HouseFinishTools.Room, "two", "3"), 1).Outcome, Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(piece.Apply("ana", Req("h", RoomHouseBuild.Finish, 10, Surface, FloorOak, HouseFinishTools.Room, "2"), 1).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "Ids 수");
            Assert.That(piece.Apply("ana", Req("i", RoomHouseBuild.Finish, 10, Surface, "floor|oak", HouseFinishTools.Room, "2", "3"), 1).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "구분 글자");
            Assert.That(piece.Apply("ana", Req("j", RoomHouseBuild.Finish, 10, Surface, "", HouseFinishTools.Room, "2", "3"), 1).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "빈 id");
            Assert.That(piece.Apply("ana", Req("k", "paint", 10, Surface, FloorOak), 1).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "모르는 종류");
            Assert.That(piece.Apply("ana", null, 1).Outcome, Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Band(piece, "ana", "l", FloorOak), Is.EqualTo(RoomOutcomes.Invalid), "띠가 아닌 꾸밈");
            Assert.That(Band(piece, "ana", "m", "no.such"), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Band(piece, "ana", "n", BandBrown, amount: 2), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(piece.HeldCount, Is.EqualTo(0));
        }

        [Test]
        public void SurfaceWithoutAFrame_Refused()
        {
            var piece = Piece();
            Assert.That(Finish(piece, "ana", "h0", FloorOak, HouseFinishTools.Room, LeftMiddle, 10, surface: "garden"), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Band(piece, "ana", "b0", BandBrown, surface: "garden"), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Applied));
            // 붙은 틀과 다른 틀의 면
            Assert.That(Finish(piece, "bo", "h2", WallBlue, HouseFinishTools.Room, LeftMiddle, 4, surface: Annex), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Band(piece, "bo", "b1", BandBrown, surface: Annex), Is.EqualTo(RoomOutcomes.Refused));
        }

        [Test]
        public void UseWithoutAFrame_Refused_HoldKept_ThenANewUseIdWithRulesApplies()
        {
            // 돈은 이미 냈다: 틀을 붙일 수 없으면(규칙 없이 시작한 방) 잡은 채로 거절한다 — 잡기를 지우면 낸 돈만 사라진다(상자 조각과 같은 원칙).
            var store = new InMemorySaveStore();
            var house = new RoomShareSettings(new[] { RoomHouse.PieceName });
            var withRules = new RoomPieceSet(house, RoomPieceCatalog.CreateDefault(Rules()));
            Assert.That(ApplyIn(withRules, "ana", Req("h1", RoomHouseBuild.Finish, 10, Surface, FloorOak, HouseFinishTools.Room, "2", "3")).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            withRules.Save(store, "slot");

            var noRules = new RoomPieceSet(house, RoomPieceCatalog.CreateDefault());
            noRules.Load(store, "slot");
            noRules.TryGet(RoomHouseBuild.PieceName, out var bare);
            var use = Req("u1", RoomHouseBuild.Use, 0, "h1");
            Assert.That(ApplyIn(noRules, "ana", use).Outcome, Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(((RoomHouseBuild)bare).HeldCount, Is.EqualTo(1), "잡기는 남는다");
            Assert.That(((RoomHouseBuild)bare).ToSaveData().Finishes, Is.Empty, "꾸밈은 그대로");
            noRules.Save(store, "slot");

            var again = new RoomPieceSet(house, RoomPieceCatalog.CreateDefault(Rules()));
            again.Load(store, "slot");
            again.TryGet(RoomHouseBuild.PieceName, out var piece);
            Assert.That(ApplyIn(again, "ana", use.Copy()).Outcome, Is.EqualTo(RoomOutcomes.Refused), "같은 요청 id는 장부의 답 그대로 — 다시 쓸 때는 새 id로");
            Assert.That(((RoomHouseBuild)piece).HeldCount, Is.EqualTo(1));
            Assert.That(ApplyIn(again, "ana", Req("u2", RoomHouseBuild.Use, 0, "h1")).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(((RoomHouseBuild)piece).HeldCount, Is.EqualTo(0));
            Assert.That(((RoomHouseBuild)piece).ToSaveData().Finishes.Count(c => c.Floor == FloorOak), Is.EqualTo(10));
        }

        [Test]
        public void UseAfterTheFinishLeftTheData_Refused_HoldKept_DataBackApplies()
        {
            // 다시 시작하는 사이 데이터에서 그 꾸밈이 지워졌다: 잡기에 자리(벽지·바닥)가 없어 덮을 칸을 정할 수 없다 — 잡은 채로 거절하고, 데이터가 돌아오면 쓴다.
            var store = new InMemorySaveStore();
            var piece = Piece();
            Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10);
            Band(piece, "ana", "b1", BandBrown);
            piece.Save(store, "slot");

            var gone = new RoomHouseBuild(Rules(FloorOak, BandBrown));
            gone.Load(store, "slot");
            Assert.That(Use(gone, "ana", "u1", "h1"), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Use(gone, "ana", "u2", "b1"), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(gone.HeldCount, Is.EqualTo(2));
            Assert.That(gone.ToSaveData().Finishes, Is.Empty);
            Assert.That(gone.ToSaveData().Band, Is.EqualTo(""));
            Assert.That(Release(gone, "ana", "r1", "b1"), Is.EqualTo(RoomOutcomes.Applied), "풀기는 된다(돈을 돌려받는 길은 플레이어 쪽 몫)");
            gone.Save(store, "slot");

            var back = Piece();
            back.Load(store, "slot");
            Assert.That(Use(back, "ana", "u3", "h1"), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(back.ToSaveData().Finishes.Count(c => c.Floor == FloorOak), Is.EqualTo(10));
            Assert.That(back.HeldCount, Is.EqualTo(0));
        }

        [Test]
        public void ReleaseThenUse_Invalid_NothingChanges()
        {
            var piece = Piece();
            Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10);
            Assert.That(Release(piece, "ana", "r1", "h1"), Is.EqualTo(RoomOutcomes.Applied));
            int revision = piece.Revision;
            Assert.That(Use(piece, "ana", "u1", "h1"), Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(piece.Revision, Is.EqualTo(revision));
            Assert.That(piece.ToSaveData().Finishes, Is.Empty);
            Assert.That(piece.HeldCount, Is.EqualTo(0));
        }

        [Test]
        public void HoldOnAnotherFramesSurface_DoesNotLockTheAttachedFramesCells()
        {
            // 다른 틀의 면에서 잡은 것은 그 틀의 좌표다 — 붙은 틀의 칸을 막지 않는다.
            var store = new InMemorySaveStore();
            store.Put("slot", SaveFileNames.RoomHouseBuildHolds, new RoomHouseBuildHoldsSaveData
            {
                Version = RoomHouseBuildHoldsSaveData.KnownVersion,
                Holds = new[] { new RoomHouseBuildHoldSaveData { RequestId = "a1", PlayerId = "bo", SurfaceId = Annex, Kind = RoomHouseBuild.Finish, FinishId = FloorOak, Tool = HouseFinishTools.Room, X = 2, Y = 3, Count = 10 } },
            });
            var piece = Piece();
            piece.Load(store, "slot");
            Assert.That(Finish(piece, "ana", "h1", FloorLocked, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(piece.HeldCount, Is.EqualTo(2));
        }

        [Test]
        public void NoRules_Refused()
        {
            var piece = new RoomHouseBuild();
            Assert.That(Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Band(piece, "ana", "b1", BandBrown), Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(piece.HeldCount, Is.EqualTo(0));
        }

        [Test]
        public void Band_HoldThenUse_ChangesTheBand_SecondBandHoldWhileHeld_Refused()
        {
            var piece = Piece();
            Assert.That(Band(piece, "ana", "b1", BandCream), Is.EqualTo(RoomOutcomes.Refused), "프레임 기본과 같은 그림");
            Assert.That(Band(piece, "ana", "b2", BandBrown), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Band(piece, "bo", "b3", BandGreen), Is.EqualTo(RoomOutcomes.Refused), "잡힌 띠가 있다");
            Assert.That(Finish(piece, "bo", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Applied), "띠 잡기는 칸을 막지 않는다");
            Assert.That(piece.ToSaveData().Band, Is.EqualTo(""));
            Assert.That(Use(piece, "ana", "u2", "b2"), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(piece.ToSaveData().Band, Is.EqualTo(BandBrown));
            var holds = new List<KeyValuePair<string, string>>();
            Assert.That(RoomHouseBuildLines.TryDecode(piece.State().Ids, out var decoded, holds), Is.True);
            Assert.That(decoded.Band, Is.EqualTo(BandBrown));
            Assert.That(holds.Select(h => (h.Key, h.Value)), Is.EqualTo(new[] { ("h1", "bo") }));

            Assert.That(Band(piece, "bo", "b4", BandBrown), Is.EqualTo(RoomOutcomes.Refused), "이미 그 띠");
            Assert.That(Band(piece, "bo", "b5", BandGreen), Is.EqualTo(RoomOutcomes.Applied), "앞 잡기가 끝났다");
            Assert.That(Release(piece, "bo", "r5", "b5"), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(piece.ToSaveData().Band, Is.EqualTo(BandBrown));
        }

        [Test]
        public void SaveAndLoad_HoldsSurvive_AndStillLockTheirCells()
        {
            var store = new InMemorySaveStore();
            var piece = Piece();
            Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Room, LeftMiddle, 10);
            Band(piece, "ana", "b1", BandBrown);
            piece.Save(store, "slot");
            Assert.That(store.Contains("slot", SaveFileNames.RoomPiece(RoomHouseBuild.PieceName)), Is.True);
            Assert.That(store.Contains("slot", SaveFileNames.RoomHouseBuildHolds), Is.True);
            Assert.That(store.TryRead("slot", SaveFileNames.RoomHouseBuildHolds, out RoomHouseBuildHoldsSaveData holdsFile), Is.True);
            Assert.That(holdsFile.Version, Is.EqualTo(RoomHouseBuildHoldsSaveData.KnownVersion));
            Assert.That(holdsFile.Holds.Select(h => (h.RequestId, h.PlayerId, h.Kind)),
                Is.EquivalentTo(new[] { ("h1", "ana", RoomHouseBuild.Finish), ("b1", "ana", RoomHouseBuild.Band) }));

            var again = Piece();
            again.Load(store, "slot");
            Assert.That(again.HeldCount, Is.EqualTo(2));
            Assert.That(Finish(again, "bo", "h2", FloorLocked, HouseFinishTools.Room, LeftMiddle, 10), Is.EqualTo(RoomOutcomes.Refused), "읽은 잡기도 칸을 막는다");
            Assert.That(Band(again, "bo", "b2", BandGreen), Is.EqualTo(RoomOutcomes.Refused), "읽은 띠 잡기도");
            Assert.That(Use(again, "ana", "u1", "h1"), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(again.ToSaveData().Finishes.Count(c => c.Floor == FloorOak), Is.EqualTo(10));

            // 읽은 뒤 첫 요청이 쓰기여도 틀을 붙여 적용한다
            var third = Piece();
            third.Load(store, "slot");
            Assert.That(Use(third, "ana", "u2", "b1"), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(third.ToSaveData().Band, Is.EqualTo(BandBrown));
            Assert.That(Use(third, "ana", "u3", "h1"), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(third.ToSaveData().Finishes.Count(c => c.Floor == FloorOak), Is.EqualTo(10));

            // 아무것도 읽지 않은 조각도 두 파일을 쓴다
            var empty = new InMemorySaveStore();
            new RoomHouseBuild().Save(empty, "slot");
            Assert.That(empty.Contains("slot", SaveFileNames.RoomPiece(RoomHouseBuild.PieceName)) && empty.Contains("slot", SaveFileNames.RoomHouseBuildHolds), Is.True);
        }

        [Test]
        public void UnknownIdsAndOtherFrameRecords_KeptThroughLoadSaveAndState()
        {
            var store = new InMemorySaveStore();
            store.Put("slot", SaveFileNames.RoomPiece(RoomHouseBuild.PieceName), new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion,
                FrameId = "frame.old",
                Band = "band.gone",
                Finishes = new[]
                {
                    new HouseFinishCellSaveData { X = 1, Y = 3, Floor = FloorOak },
                    new HouseFinishCellSaveData { X = 2, Y = 3, Floor = "floor.ghost" },
                    new HouseFinishCellSaveData { X = 50, Y = 50, Wall = "wall.ghost" },
                },
            });
            store.Put("slot", SaveFileNames.RoomHouseBuildHolds, new RoomHouseBuildHoldsSaveData
            {
                Version = RoomHouseBuildHoldsSaveData.KnownVersion,
                Holds = new[] { new RoomHouseBuildHoldSaveData { RequestId = "old", PlayerId = "ana", SurfaceId = Surface, Kind = "future-kind", FinishId = "x" } },
            });
            var expected = new[] { (1, 3, "", FloorOak), (2, 3, "", "floor.ghost"), (50, 50, "wall.ghost", "") };

            var piece = Piece();
            piece.Load(store, "slot");
            Assert.That(piece.ToSaveData().FrameId, Is.EqualTo("frame.old"), "붙기 전에는 읽은 틀");
            Assert.That(Cells(piece.ToSaveData()), Is.EqualTo(expected));
            Assert.That(piece.HeldCount, Is.EqualTo(1), "모르는 종류의 잡기도 버리지 않는다");
            var holds = new List<KeyValuePair<string, string>>();
            Assert.That(RoomHouseBuildLines.TryDecode(piece.State().Ids, out var decoded, holds), Is.True);
            Assert.That((decoded.FrameId, decoded.Band), Is.EqualTo(("frame.old", "band.gone")));
            Assert.That(Cells(decoded), Is.EqualTo(expected));

            piece.Save(store, "slot");
            var again = Piece();
            again.Load(store, "slot");
            Assert.That(Cells(again.ToSaveData()), Is.EqualTo(expected));
            Assert.That(again.ToSaveData().Band, Is.EqualTo("band.gone"));

            // 다른 틀의 기록 위에 꾸미면 같은 칸 좌표에 얹는다 — 옛 기록은 남는다
            Assert.That(Finish(again, "bo", "h1", WallBlue, HouseFinishTools.Brush, new CellPos(6, 2), 1), Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Use(again, "bo", "u1", "h1"), Is.EqualTo(RoomOutcomes.Applied));
            var after = again.ToSaveData();
            Assert.That(after.FrameId, Is.EqualTo(FrameId));
            Assert.That(Cells(after), Is.EquivalentTo(expected.Append((6, 2, WallBlue, ""))));
            Assert.That(after.Band, Is.EqualTo("band.gone"));
            Assert.That(Release(again, "ana", "r0", "old"), Is.EqualTo(RoomOutcomes.Applied), "모르는 잡기도 풀 수 있다");
        }

        [Test]
        public void StateLines_StayUnder128Bytes_ForTheLongestRealIds()
        {
            const int MaxLineBytes = 128; // Net RoomShareMessageBodies.MaxIdBytes(Core 테스트는 Net을 참조하지 않는다)
            var ids = RealFinishIds().OrderByDescending(id => Encoding.UTF8.GetByteCount(id)).Take(2).ToArray();
            Assert.That(ids.Length, Is.EqualTo(2));
            string player = new string('p', 64);
            string request = Guid.NewGuid().ToString("N");

            var store = new InMemorySaveStore();
            store.Put("slot", SaveFileNames.RoomPiece(RoomHouseBuild.PieceName), new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion,
                FrameId = ids[0],
                Band = ids[0],
                Finishes = new[] { new HouseFinishCellSaveData { X = 99, Y = 99, Wall = ids[0], Floor = ids[1] } },
            });
            store.Put("slot", SaveFileNames.RoomHouseBuildHolds, new RoomHouseBuildHoldsSaveData
            {
                Version = RoomHouseBuildHoldsSaveData.KnownVersion,
                Holds = new[] { new RoomHouseBuildHoldSaveData { RequestId = request, PlayerId = player, SurfaceId = Surface, Kind = RoomHouseBuild.Finish, FinishId = ids[0], Tool = HouseFinishTools.Room, X = 99, Y = 99, Count = 1 } },
            });
            var piece = Piece();
            piece.Load(store, "slot");
            var lines = piece.State().Ids;
            Assert.That(lines.Length, Is.EqualTo(4), "틀, 띠, 칸 하나, 잡기 하나");
            foreach (var line in lines)
                Assert.That(Encoding.UTF8.GetByteCount(line), Is.LessThanOrEqualTo(MaxLineBytes), line);
        }

        private static IEnumerable<string> RealFinishIds()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "data", "building", "RoomFinishDefinition.json"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "저장소의 data/building/RoomFinishDefinition.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "data", "building", "RoomFinishDefinition.json")));
            var result = new List<string>();
            foreach (var e in doc.RootElement.EnumerateArray())
                if (e.TryGetProperty("id", out var id)) result.Add(id.GetString());
            return result;
        }

        // ---------- 방 조각 묶음과 등록 ----------

        private static RoomPieceSet Room(RoomShareSettings settings = null)
            => new RoomPieceSet(settings ?? new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(Rules()));

        private static RoomAnswer ApplyIn(RoomPieceSet room, string player, RoomRequestSaveData request)
        {
            var answers = new List<RoomAnswer>();
            room.Apply(player, new[] { request }, 1, answers, null);
            return answers[0];
        }

        [Test]
        public void ThroughTheRoomPieceSet_SameRequestIdIsAppliedOnce()
        {
            var room = Room();
            Assert.That(room.TryGet(RoomHouseBuild.PieceName, out var p), Is.True);
            var piece = (RoomHouseBuild)p;
            var finish = Req("h1", RoomHouseBuild.Finish, 10, Surface, FloorOak, HouseFinishTools.Room, "2", "3");
            Assert.That(ApplyIn(room, "ana", finish).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(ApplyIn(room, "ana", finish.Copy()).Outcome, Is.EqualTo(RoomOutcomes.Applied), "장부가 같은 답");
            Assert.That(piece.HeldCount, Is.EqualTo(1));
            Assert.That(ApplyIn(room, "bo", finish.Copy()).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "남의 요청 id");

            var use = Req("u1", RoomHouseBuild.Use, 0, "h1");
            Assert.That(ApplyIn(room, "ana", use).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            int revision = piece.Revision;
            Assert.That(ApplyIn(room, "ana", use.Copy()).Outcome, Is.EqualTo(RoomOutcomes.Applied), "다시 와도 같은 답");
            Assert.That(piece.Revision, Is.EqualTo(revision), "다시 적용하지 않는다");
            Assert.That(piece.ToSaveData().Finishes.Length, Is.EqualTo(10));
        }

        [Test]
        public void HouseBuildFollowsHouse_NotAChoosablePiece()
        {
            var catalog = RoomPieceCatalog.CreateDefault(Rules());
            var house = new RoomShareSettings(new[] { RoomHouse.PieceName });
            Assert.That(catalog.ActiveNames(house), Does.Contain(RoomHouseBuild.PieceName));
            Assert.That(catalog.Names, Does.Not.Contain(RoomHouseBuild.PieceName), "방 설정에서 고르지 않는다");
            Assert.That(catalog.Knows(RoomHouseBuild.PieceName), Is.False);
            Assert.That(catalog.Follows(RoomHouseBuild.PieceName), Is.True);
            Assert.That(Room().TryGet(RoomHouseBuild.PieceName, out _), Is.True);
            Assert.That(Room(new RoomShareSettings(new[] { RoomWallet.PieceName })).TryGet(RoomHouseBuild.PieceName, out _), Is.False, "집을 각자 쓰면 꾸밈도 각자");
            Assert.That(new RoomShares(house, catalog).Find<RoomHouseBuildClient>(), Is.Not.Null);
        }

        // ---------- 플레이어 쪽 ----------

        private static RoomShares Shares()
        {
            int next = 0;
            return new RoomShares(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(), () => "q" + (++next));
        }

        [Test]
        public void Client_DecodesData_AndHoldsOfAPlayer()
        {
            var shares = Shares();
            var client = shares.Find<RoomHouseBuildClient>();
            Assert.That(client.Known, Is.False);
            Assert.That(client.Data, Is.Not.Null);
            Assert.That(client.Data.Finishes, Is.Empty);
            int changed = 0;
            client.Changed += () => changed++;

            var piece = Piece();
            Finish(piece, "ana", "h1", FloorOak, HouseFinishTools.Brush, new CellPos(1, 2), 1);
            Use(piece, "ana", "u1", "h1");
            Band(piece, "ana", "b1", BandBrown);
            Finish(piece, "bo", "h2", WallBlue, HouseFinishTools.Room, LeftMiddle, 4);
            Finish(piece, "ana", "h3", FloorOak, HouseFinishTools.Brush, new CellPos(6, 3), 1);
            var state = piece.State();

            shares.Receive(state);
            Assert.That(client.Known, Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(client.Data.FrameId, Is.EqualTo(FrameId));
            Assert.That(Cells(client.Data), Is.EqualTo(Cells(piece.ToSaveData())));
            var mine = new List<string>();
            client.CopyHoldsOf("ana", mine);
            Assert.That(mine, Is.EqualTo(new[] { "b1", "h3" }), "요청 id 순");
            client.CopyHoldsOf("bo", mine);
            Assert.That(mine, Is.EqualTo(new[] { "h2" }));
            client.CopyHoldsOf("cy", mine);
            Assert.That(mine, Is.Empty);

            shares.Receive(new RoomPieceState(RoomHouseBuild.PieceName, state.Amount, (string[])state.Ids.Clone()));
            Assert.That(changed, Is.EqualTo(1), "같은 상태면 알리지 않는다");
        }

        [Test]
        public void Client_AnswersBeforeAListener_AreHandedOverOnce()
        {
            var shares = Shares();
            var client = shares.Find<RoomHouseBuildClient>();
            string finish = client.RequestFinish(Surface, FloorOak, HouseFinishTools.Room, LeftMiddle, 10);
            var sent = shares.Outbox.Requests.Single();
            Assert.That((sent.Piece, sent.Kind, sent.Amount), Is.EqualTo((RoomHouseBuild.PieceName, RoomHouseBuild.Finish, 10)));
            Assert.That(sent.Ids, Is.EqualTo(new[] { Surface, FloorOak, HouseFinishTools.Room, "2", "3" }));
            Assert.That(client.HasPending, Is.True);
            string band = client.RequestBand(Surface, BandBrown);
            Assert.That(shares.Outbox.Requests.Last().Amount, Is.EqualTo(1));

            shares.Receive(new RoomAnswer(RoomHouseBuild.PieceName, finish, RoomOutcomes.Applied, 0));
            shares.Receive(new RoomAnswer(RoomHouseBuild.PieceName, band, RoomOutcomes.Refused, 0));
            Assert.That(client.HasPending, Is.False);

            var heard = new List<(string, bool)>();
            client.Answered += (r, ok) => heard.Add((r.RequestId, ok));
            Assert.That(heard, Is.EqualTo(new[] { (finish, true), (band, false) }), "붙을 때 들고 있던 답을 넘긴다");
            var second = new List<string>();
            client.Answered += (r, ok) => second.Add(r.RequestId);
            Assert.That(second, Is.Empty, "한 번만");

            int changed = 0;
            client.Changed += () => changed++;
            client.AddUse(finish);
            Assert.That(client.HasPending, Is.False, "쓰기·풀기는 기다리는 요청이 아니다");
            var use = shares.Outbox.Requests.Single();
            Assert.That((use.Kind, use.Ids.Single()), Is.EqualTo((RoomHouseBuild.Use, finish)));
            shares.Receive(new RoomAnswer(RoomHouseBuild.PieceName, use.RequestId, RoomOutcomes.Applied, 0));
            Assert.That(heard.Count, Is.EqualTo(2), "쓰기의 답은 Answered가 아니다");
            Assert.That(changed, Is.EqualTo(1));
            client.AddRelease("h9");
            Assert.That(shares.Outbox.Requests.Single().Kind, Is.EqualTo(RoomHouseBuild.Release));
        }

        [Test]
        public void Client_UseRefused_SendsTheUseAgainWithANewId_InvalidOrAppliedDoNot()
        {
            // 쓰기 = 돈을 냈다. 방이 적용하지 못해 잡은 채 거절하면 쓰기를 새 id로 다시 적어 둔다(풀면 낸 돈만 사라진다).
            var shares = Shares();
            var client = shares.Find<RoomHouseBuildClient>();
            client.AddUse("h1");
            var first = shares.Outbox.Requests.Single();
            shares.Receive(new RoomAnswer(RoomHouseBuild.PieceName, first.RequestId, RoomOutcomes.Refused, 0));
            var again = shares.Outbox.Requests.Single();
            Assert.That((again.Kind, again.Ids.Single()), Is.EqualTo((RoomHouseBuild.Use, "h1")));
            Assert.That(again.RequestId, Is.Not.EqualTo(first.RequestId), "장부가 옛 id에 거절을 기억하므로 새 id");

            shares.Receive(new RoomAnswer(RoomHouseBuild.PieceName, again.RequestId, RoomOutcomes.Invalid, 0));
            Assert.That(shares.Outbox.Requests, Is.Empty, "잡기가 없거나 남의 것(Invalid)이면 다시 보내지 않는다");
            client.AddUse("h2");
            shares.Receive(new RoomAnswer(RoomHouseBuild.PieceName, shares.Outbox.Requests.Single().RequestId, RoomOutcomes.Applied, 0));
            Assert.That(shares.Outbox.Requests, Is.Empty);
            client.AddRelease("h3");
            shares.Receive(new RoomAnswer(RoomHouseBuild.PieceName, shares.Outbox.Requests.Single().RequestId, RoomOutcomes.Refused, 0));
            Assert.That(shares.Outbox.Requests, Is.Empty, "풀기는 다시 보내지 않는다");
        }

        [Test]
        public void Client_RequestsWithTheSeparator_Throw()
        {
            var client = Shares().Find<RoomHouseBuildClient>();
            Assert.Throws<ArgumentException>(() => client.RequestFinish("ho|use", FloorOak, HouseFinishTools.Room, LeftMiddle, 10));
            Assert.Throws<ArgumentException>(() => client.RequestFinish(Surface, "floor|oak", HouseFinishTools.Room, LeftMiddle, 10));
            Assert.Throws<ArgumentException>(() => client.RequestFinish(Surface, FloorOak, "ro|om", LeftMiddle, 10));
            Assert.Throws<ArgumentException>(() => client.RequestFinish(Surface, "", HouseFinishTools.Room, LeftMiddle, 10));
            Assert.Throws<ArgumentException>(() => client.RequestFinish(Surface, FloorOak, HouseFinishTools.Room, LeftMiddle, 0));
            Assert.Throws<ArgumentException>(() => client.RequestBand(Surface, "band|x"));
            Assert.Throws<ArgumentException>(() => client.RequestBand(null, BandBrown));
            Assert.That(client.HasPending, Is.False);
        }
    }
}
