using System.IO;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Save;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>짓는 집 꾸밈 세이브(house-build.json)를 파일 저장소로 쓰고 읽는다. 자동 테스트다.</summary>
    public sealed class HouseBuildSaveTests
    {
        [Test]
        public void FileStoreRoundTrip_AndVersionWritten()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            var data = new HouseBuildSaveData
            {
                FrameId = "frame.test", Band = "band.test",
                Finishes = new[]
                {
                    new HouseFinishCellSaveData { X = 3, Y = 4, Wall = "", Floor = "finish.floor.a" },
                    new HouseFinishCellSaveData { X = 5, Y = 6, Wall = "finish.wall.b", Floor = "" },
                },
            };
            Assert.That(data.Version, Is.Zero, "전제: 버전을 적지 않은 모양");

            store.Write("slot-0", SaveFileNames.HouseBuild, data);

            var json = JsonNode.Parse(File.ReadAllText(Path.Combine(dir.Path, "slot-0", SaveFileNames.HouseBuild)));
            Assert.That((int)json["Version"], Is.EqualTo(HouseBuildSaveData.KnownVersion), "저장소가 형식 버전을 채운다");
            Assert.That(json["ReadableFrom"], Is.Null, "옛 프로그램이 읽지 못하게 막지 않는다");

            Assert.That(store.TryRead<HouseBuildSaveData>("slot-0", SaveFileNames.HouseBuild, out var read), Is.True);
            Assert.That(read.Version, Is.EqualTo(HouseBuildSaveData.KnownVersion));
            Assert.That(read.FrameId, Is.EqualTo("frame.test"));
            Assert.That(read.Band, Is.EqualTo("band.test"));
            Assert.That(read.Finishes.Length, Is.EqualTo(2));
            Assert.That((read.Finishes[0].X, read.Finishes[0].Y, read.Finishes[0].Wall, read.Finishes[0].Floor), Is.EqualTo((3, 4, "", "finish.floor.a")));
            Assert.That((read.Finishes[1].X, read.Finishes[1].Y, read.Finishes[1].Wall, read.Finishes[1].Floor), Is.EqualTo((5, 6, "finish.wall.b", "")));

            // Core가 읽고 그대로 다시 써도 같은 바이트다(붙이지 않은 판).
            string first = File.ReadAllText(Path.Combine(dir.Path, "slot-0", SaveFileNames.HouseBuild));
            var finishes = new HouseFinishes();
            finishes.Load(read);
            store.Write("slot-0", SaveFileNames.HouseBuild, finishes.ToSaveData());
            Assert.That(File.ReadAllText(Path.Combine(dir.Path, "slot-0", SaveFileNames.HouseBuild)), Is.EqualTo(first));
        }
    }
}
