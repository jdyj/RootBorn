using System.IO;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 같이 쓰는 집의 꾸밈 조각(Core RoomHouseBuild)이 쓰는 방 세이브 두 파일 — 꾸밈 기록 <c>room-piece-house-build.json</c>과 잡기
    /// <c>room-piece-house-build-holds.json</c> — 을 파일 저장소로 쓰고 읽는다. 조각은 규칙(틀) 없이 읽고 쓴다(서버를 다시 켠 직후와 같다). 자동 테스트다.
    /// </summary>
    public sealed class RoomHouseBuildSaveTests
    {
        private const string Room = "room-1";

        [Test]
        public void FileStoreRoundTrip_BothFiles_VersionWritten()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            store.Write(Room, SaveFileNames.RoomPiece(RoomHouseBuild.PieceName), new HouseBuildSaveData
            {
                FrameId = "frame.test", Band = "finish.band.b",
                Finishes = new[]
                {
                    new HouseFinishCellSaveData { X = 3, Y = 4, Wall = "", Floor = "finish.floor.a" },
                    new HouseFinishCellSaveData { X = 5, Y = 4, Wall = "finish.wall.w", Floor = "" },
                },
            });
            store.Write(Room, SaveFileNames.RoomHouseBuildHolds, new RoomHouseBuildHoldsSaveData
            {
                Holds = new[]
                {
                    new RoomHouseBuildHoldSaveData
                    {
                        RequestId = "r2", PlayerId = "p", SurfaceId = "surface.house", Kind = RoomHouseBuild.Finish, FinishId = "finish.floor.a", Tool = "brush",
                        X = 6, Y = 7, Count = 1,
                    },
                    new RoomHouseBuildHoldSaveData { RequestId = "r1", PlayerId = "q", SurfaceId = "surface.house", Kind = RoomHouseBuild.Band, FinishId = "finish.band.b", Count = 1 },
                },
            });

            var piece = new RoomHouseBuild();
            piece.Load(store, Room);
            Assert.That(piece.HeldCount, Is.EqualTo(2));
            piece.Save(store, "room-2");

            string finishPath = Path.Combine(dir.Path, "room-2", SaveFileNames.RoomPiece(RoomHouseBuild.PieceName));
            string holdsPath = Path.Combine(dir.Path, "room-2", SaveFileNames.RoomHouseBuildHolds);
            Assert.That(File.Exists(finishPath) && File.Exists(holdsPath), Is.True, "조각 하나가 두 파일을 쓴다");
            Assert.That((int)JsonNode.Parse(File.ReadAllText(finishPath))["Version"], Is.EqualTo(HouseBuildSaveData.KnownVersion));
            Assert.That((int)JsonNode.Parse(File.ReadAllText(holdsPath))["Version"], Is.EqualTo(RoomHouseBuildHoldsSaveData.KnownVersion));
            Assert.That(JsonNode.Parse(File.ReadAllText(finishPath))["ReadableFrom"], Is.Null, "옛 프로그램이 읽지 못하게 막지 않는다");
            Assert.That(JsonNode.Parse(File.ReadAllText(holdsPath))["ReadableFrom"], Is.Null);

            Assert.That(store.TryRead<HouseBuildSaveData>("room-2", SaveFileNames.RoomPiece(RoomHouseBuild.PieceName), out var finishes), Is.True);
            Assert.That(finishes.FrameId, Is.EqualTo("frame.test"));
            Assert.That(finishes.Band, Is.EqualTo("finish.band.b"));
            Assert.That(finishes.Finishes.Length, Is.EqualTo(2));
            Assert.That((finishes.Finishes[0].X, finishes.Finishes[0].Y, finishes.Finishes[0].Floor), Is.EqualTo((3, 4, "finish.floor.a")));
            Assert.That((finishes.Finishes[1].X, finishes.Finishes[1].Wall), Is.EqualTo((5, "finish.wall.w")));

            Assert.That(store.TryRead<RoomHouseBuildHoldsSaveData>("room-2", SaveFileNames.RoomHouseBuildHolds, out var holds), Is.True);
            Assert.That(holds.Holds.Length, Is.EqualTo(2));
            var first = holds.Holds[0];
            Assert.That((first.RequestId, first.PlayerId, first.Kind, first.FinishId), Is.EqualTo(("r1", "q", RoomHouseBuild.Band, "finish.band.b")), "요청 id 순");
            var second = holds.Holds[1];
            Assert.That((second.RequestId, second.SurfaceId, second.Tool, second.X, second.Y, second.Count), Is.EqualTo(("r2", "surface.house", "brush", 6, 7, 1)));

            // 읽고 다시 써도 같은 바이트다(규칙 없이, 붙이지 않은 판).
            byte[] finishBytes = File.ReadAllBytes(finishPath), holdsBytes = File.ReadAllBytes(holdsPath);
            var again = new RoomHouseBuild();
            again.Load(store, "room-2");
            again.Save(store, "room-2");
            Assert.That(File.ReadAllBytes(finishPath), Is.EqualTo(finishBytes));
            Assert.That(File.ReadAllBytes(holdsPath), Is.EqualTo(holdsBytes));
        }

        [Test]
        public void PaidHolds_FileStoreRoundTrip_VersionWritten_NoReadableFrom_AMissingFileIsNoRecord()
        {
            // 플레이어 세이브의 돈 낸 꾸밈 잡기 파일(room-house-build-paid-<플레이어>.json).
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            const string player = SaveFileNames.DefaultPlayerId;
            string name = SaveFileNames.RoomHouseBuildPaid(player);
            Assert.That(name, Is.EqualTo("room-house-build-paid-local-player.json"));

            var paid = new RoomHouseBuildPaidHolds();
            Assert.That(store.TryRead<RoomHouseBuildPaidSaveData>(player, name, out var missing), Is.False, "옛 세이브에는 파일이 없다");
            paid.Load(missing);
            Assert.That(paid.Count, Is.Zero, "파일이 없으면 돈 낸 잡기 없음");

            paid.Mark("hold-2");
            paid.Mark("hold-1");
            store.Write(player, name, paid.ToSaveData());
            string path = Path.Combine(dir.Path, player, name);
            Assert.That((int)JsonNode.Parse(File.ReadAllText(path))["Version"], Is.EqualTo(RoomHouseBuildPaidSaveData.KnownVersion));
            Assert.That(JsonNode.Parse(File.ReadAllText(path))["ReadableFrom"], Is.Null, "옛 프로그램이 읽지 못하게 막지 않는다");

            Assert.That(store.TryRead<RoomHouseBuildPaidSaveData>(player, name, out var read), Is.True);
            var again = new RoomHouseBuildPaidHolds();
            again.Load(read);
            Assert.That(again.ToSaveData().Holds, Is.EqualTo(new[] { "hold-2", "hold-1" }));
            byte[] bytes = File.ReadAllBytes(path);
            store.Write(player, name, again.ToSaveData());
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), "읽고 다시 써도 같은 바이트");
        }

        [Test]
        public void AnEmptyPiece_WritesBothFiles_WithTheKnownVersion()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            new RoomHouseBuild().Save(store, Room);

            Assert.That(store.TryRead<HouseBuildSaveData>(Room, SaveFileNames.RoomPiece(RoomHouseBuild.PieceName), out var finishes), Is.True);
            Assert.That(finishes.Version, Is.EqualTo(HouseBuildSaveData.KnownVersion));
            Assert.That(finishes.Finishes, Is.Empty);
            Assert.That(store.TryRead<RoomHouseBuildHoldsSaveData>(Room, SaveFileNames.RoomHouseBuildHolds, out var holds), Is.True);
            Assert.That(holds.Version, Is.EqualTo(RoomHouseBuildHoldsSaveData.KnownVersion));
            Assert.That(holds.Holds, Is.Empty);
        }
    }
}
