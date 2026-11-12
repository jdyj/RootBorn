using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.Time;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 상위호환: 더 새 프로그램이 쓴 세이브를 이 프로그램이 읽고 플레이하고 다시 쓴다. 모르는 필드는 버리지 않고 그대로 다시 쓴다.
    /// 읽기를 거절하는 것은 파일이 "이 버전부터 읽을 수 있다"(ReadableFrom)고 적었고 그것이 이 프로그램이 아는 버전보다 높을 때뿐이다.
    /// 이 테스트의 "더 새 프로그램이 쓴 파일"은 테스트가 직접 쓴 JSON이다. 자동 테스트다.
    /// </summary>
    public sealed class NewerSaveInAnOlderProgramTests
    {
        private const string Slot = "slot-0";
        private TempDir _dir;
        private FileSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDir();
            _store = new FileSaveStore(_dir.Path);
        }

        [TearDown]
        public void TearDown() => _dir.Dispose();

        private JsonNode FileJson(string file) => JsonNode.Parse(File.ReadAllText(Path.Combine(_dir.Path, Slot, file)));

        [Test]
        public void ANewerVersion_IsRead_AndItsUnknownFieldsSurviveARewrite()
        {
            _dir.Write(Path.Combine(Slot, "quest-log.json"), "{\"Version\":99,\"Quests\":[],\"Future\":{\"a\":1},\"FutureList\":[1,2]}");

            Assert.That(_store.TryRead<QuestLogSaveData>(Slot, "quest-log.json", out var read), Is.True);
            Assert.That(read.Quests, Is.Empty);

            // 영역은 읽은 객체가 아니라 새로 만든 모양을 쓴다(ToSaveData).
            _store.Write(Slot, "quest-log.json", new QuestLogSaveData { Quests = new[] { new QuestProgressSaveData { QuestId = "q" } } });

            var json = FileJson("quest-log.json");
            Assert.That((int)json["Future"]["a"], Is.EqualTo(1), "모르는 필드");
            Assert.That(json["FutureList"].AsArray().Count, Is.EqualTo(2), "모르는 목록");
            Assert.That((int)json["Version"], Is.EqualTo(99), "형식 버전을 낮춰 쓰지 않는다");
            Assert.That((string)json["Quests"][0]["QuestId"], Is.EqualTo("q"), "이 프로그램이 아는 필드는 새 값");
        }

        [Test]
        public void UnknownFieldsOfAShapeInsideTheFile_SurviveToo()
        {
            _dir.Write(Path.Combine(Slot, "snapshot.json"), "{\"Version\":3,\"Wallet\":{\"Version\":99,\"Balance\":5,\"Debt\":7}}");

            Assert.That(_store.TryRead<AdultCareerSnapshotSaveData>(Slot, "snapshot.json", out var read), Is.True);
            Assert.That(read.Wallet.Balance, Is.EqualTo(5));

            _store.Write(Slot, "snapshot.json", new AdultCareerSnapshotSaveData { Wallet = new WalletSaveData { Balance = 9 } });

            var wallet = FileJson("snapshot.json")["Wallet"];
            Assert.That((int)wallet["Balance"], Is.EqualTo(9));
            Assert.That((int)wallet["Debt"], Is.EqualTo(7));
            Assert.That((int)wallet["Version"], Is.EqualTo(99));
        }

        [Test]
        public void ABatch_KeepsUnknownFieldsToo()
        {
            _dir.Write(Path.Combine(Slot, "quest-log.json"), "{\"Version\":2,\"Quests\":[],\"Future\":true}");

            using (var batch = _store.BeginBatch(Slot))
            {
                batch.Write(Slot, "quest-log.json", new QuestLogSaveData());
                batch.Commit();
            }

            Assert.That((bool)FileJson("quest-log.json")["Future"], Is.True);
        }

        [Test]
        public void AFileWithNothingUnknown_IsWrittenExactlyAsBefore()
        {
            _store.Write(Slot, "quest-log.json", new QuestLogSaveData());
            string first = File.ReadAllText(Path.Combine(_dir.Path, Slot, "quest-log.json"));
            Assert.That(_store.TryRead<QuestLogSaveData>(Slot, "quest-log.json", out var read), Is.True);

            _store.Write(Slot, "quest-log.json", read);

            Assert.That(File.ReadAllText(Path.Combine(_dir.Path, Slot, "quest-log.json")), Is.EqualTo(first));
            Assert.That(first, Is.EqualTo(SaveJson.Serialize(new QuestLogSaveData())));
        }

        [Test]
        public void AFileThatSaysItNeedsANewerProgram_IsRefused_AndLeftAlone()
        {
            // 옛 프로그램이 읽으면 안 되는 변경(사용자가 허락한 예외)은 파일에 ReadableFrom을 적는다.
            string text = "{\"Version\":99,\"ReadableFrom\":99,\"Quests\":[]}";
            var path = _dir.Write(Path.Combine(Slot, "quest-log.json"), text);

            var e = Assert.Throws<SaveFileTooNewException>(() => _store.TryRead<QuestLogSaveData>(Slot, "quest-log.json", out _));

            Assert.That(e.FileVersion, Is.EqualTo(99));
            Assert.That(e.KnownVersion, Is.EqualTo(QuestLogSaveData.KnownVersion));
            Assert.That(File.ReadAllText(path), Is.EqualTo(text));
        }

        [Test]
        public void ReadableFrom_ThisProgramKnows_IsRead()
        {
            _dir.Write(Path.Combine(Slot, "quest-log.json"), "{\"Version\":99,\"ReadableFrom\":1,\"Quests\":[]}");
            Assert.That(_store.TryRead<QuestLogSaveData>(Slot, "quest-log.json", out _), Is.True);
        }

        [Test]
        public void AShapeInsideTheFileThatNeedsANewerProgram_IsRefused()
        {
            string text = "{\"Version\":3,\"Wallet\":{\"Version\":99,\"ReadableFrom\":99}}";
            var path = _dir.Write(Path.Combine(Slot, "snapshot.json"), text);

            Assert.Throws<SaveFileTooNewException>(() => _store.TryRead<AdultCareerSnapshotSaveData>(Slot, "snapshot.json", out _));
            Assert.That(File.ReadAllText(path), Is.EqualTo(text));
        }

        [Test]
        public void ADeletedSlot_DoesNotBringItsUnknownFieldsBack()
        {
            _dir.Write(Path.Combine(Slot, "quest-log.json"), "{\"Version\":2,\"Quests\":[],\"Future\":true}");
            Assert.That(_store.TryRead<QuestLogSaveData>(Slot, "quest-log.json", out _), Is.True);
            Directory.Delete(Path.Combine(_dir.Path, Slot), true);

            _store.Write(Slot, "quest-log.json", new QuestLogSaveData());

            Assert.That(FileJson("quest-log.json")["Future"], Is.Null);
        }

        [Test]
        public void UnknownFieldsInsideListRecords_AreNotKept_KnownLimit()
        {
            // 목록 안 기록은 이 프로그램이 순서를 바꾸거나 더하고 빼므로 옛 기록과 짝을 지을 수 없다.
            // 그래서 새 프로그램은 잃으면 안 되는 새 값을 목록 기록 안에 넣지 않는다(스킬 save-compat-change).
            _dir.Write(Path.Combine(Slot, "quest-log.json"), "{\"Version\":2,\"Quests\":[{\"QuestId\":\"q\",\"Future\":1}]}");
            Assert.That(_store.TryRead<QuestLogSaveData>(Slot, "quest-log.json", out var read), Is.True);

            _store.Write(Slot, "quest-log.json", read);

            Assert.That(FileJson("quest-log.json")["Quests"][0]["Future"], Is.Null);
        }

        [Test]
        public void TheMemorySlot_KeepsUnknownFieldsToo()
        {
            var slot = new MemorySaveSlot("room", new[] { new KeyValuePair<string, string>("clock.json", "{\"Version\":99,\"Future\":4}") });
            Assert.That(slot.TryRead("room", "clock.json", out RoomClockSaveData read), Is.True);

            slot.Write("room", "clock.json", new RoomClockSaveData());
            Assert.That((int)JsonNode.Parse(slot.Files["clock.json"])["Future"], Is.EqualTo(4));

            using (var batch = slot.BeginBatch("room"))
            {
                batch.Write("room", "clock.json", new RoomClockSaveData());
                batch.Commit();
            }

            Assert.That((int)JsonNode.Parse(slot.Files["clock.json"])["Future"], Is.EqualTo(4));
            Assert.That((int)JsonNode.Parse(slot.Files["clock.json"])["Version"], Is.EqualTo(99));
        }
    }
}
