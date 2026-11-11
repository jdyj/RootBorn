using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    public sealed class FileSaveStoreTests
    {
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

        private string PathOf(string slot, string file) => Path.Combine(_dir.Path, slot, file);

        [Test]
        public void WriteThenRead_GivesTheSameFields()
        {
            var data = new StudentLifeProgressSaveData
            {
                SaveSlot = "slot-0", PlayerId = "local-player", Energy = 11, Focus = 9, CurrentDay = 2, TimeMinutes = 510,
                Traits = new[] { new StudentLifeProgressSaveData.StatEntry { Id = "trait.taste", Value = 3 } },
                CareerHintIds = new[] { "career.chef" },
            };

            _store.Write("slot-0", "student.json", data);

            Assert.That(_store.TryRead<StudentLifeProgressSaveData>("slot-0", "student.json", out var read), Is.True);
            Assert.That(read.SaveSlot, Is.EqualTo("slot-0"));
            Assert.That(read.PlayerId, Is.EqualTo("local-player"));
            Assert.That(read.Energy, Is.EqualTo(11));
            Assert.That(read.Focus, Is.EqualTo(9));
            Assert.That(read.CurrentDay, Is.EqualTo(2));
            Assert.That(read.TimeMinutes, Is.EqualTo(510));
            Assert.That(read.Traits[0].Id, Is.EqualTo("trait.taste"));
            Assert.That(read.Traits[0].Value, Is.EqualTo(3));
            Assert.That(read.CareerHintIds, Is.EqualTo(new[] { "career.chef" }));
        }

        [Test]
        public void MissingFile_IsFalse()
        {
            Assert.That(_store.TryRead<QuestLogSaveData>("slot-0", "quest-log.json", out var data), Is.False);
            Assert.That(data, Is.Null);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{\"Quests\":[{\"QuestId\":\"q")]
        [TestCase("not json")]
        public void EmptyOrTruncatedFile_Throws_Corrupt_AndIsNotOverwritten(string content)
        {
            var path = _dir.Write(Path.Combine("slot-0", "quest-log.json"), content);

            var e = Assert.Throws<SaveFileCorruptException>(() => _store.TryRead<QuestLogSaveData>("slot-0", "quest-log.json", out _));

            Assert.That(e.Path, Is.EqualTo(path));
            Assert.That(e.Message, Does.Contain(path));
            Assert.That(File.ReadAllText(path), Is.EqualTo(content), "깨진 파일은 그대로 둔다");
        }

        [Test]
        public void FieldsMissingInTheFile_KeepTheirDefaults()
        {
            _dir.Write(Path.Combine("slot-0", "student.json"), "{\"Energy\":4}");

            Assert.That(_store.TryRead<StudentLifeProgressSaveData>("slot-0", "student.json", out var read), Is.True);

            Assert.That(read.Energy, Is.EqualTo(4));
            Assert.That(read.CurrentDay, Is.EqualTo(1), "파일에 없는 필드는 클래스의 기본값");
            Assert.That(read.Traits, Is.Empty);
        }

        [Test]
        public void UnknownFieldsInTheFile_AreIgnored()
        {
            _dir.Write(Path.Combine("slot-0", "student.json"), "{\"Energy\":4,\"NotAField\":{\"x\":1},\"Another\":[1,2]}");

            Assert.That(_store.TryRead<StudentLifeProgressSaveData>("slot-0", "student.json", out var read), Is.True);
            Assert.That(read.Energy, Is.EqualTo(4));
        }

        [Test]
        public void NewerVersion_Throws_TooNew()
        {
            var path = _dir.Write(Path.Combine("slot-0", "quest-log.json"), "{\"Version\":99,\"Quests\":[]}");

            var e = Assert.Throws<SaveFileTooNewException>(() => _store.TryRead<QuestLogSaveData>("slot-0", "quest-log.json", out _));

            Assert.That(e.Path, Is.EqualTo(path));
            Assert.That(e.FileVersion, Is.EqualTo(99));
            Assert.That(e.KnownVersion, Is.EqualTo(QuestLogSaveData.KnownVersion));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{\"Version\":99,\"Quests\":[]}"));
        }

        [Test]
        public void FileWithoutVersion_ReadsAsZero_AndIsWrittenAsKnownVersion()
        {
            _dir.Write(Path.Combine("slot-0", "quest-log.json"), "{\"Quests\":[{\"QuestId\":\"quest.a\",\"State\":1}]}");

            Assert.That(_store.TryRead<QuestLogSaveData>("slot-0", "quest-log.json", out var read), Is.True);
            Assert.That(read.Version, Is.EqualTo(SaveVersion.Unity));

            _store.Write("slot-0", "quest-log.json", read);
            Assert.That(_store.TryRead<QuestLogSaveData>("slot-0", "quest-log.json", out var again), Is.True);
            Assert.That(again.Version, Is.EqualTo(QuestLogSaveData.KnownVersion));
            Assert.That(again.Quests[0].QuestId, Is.EqualTo("quest.a"));
        }

        [TestCase("..")]
        [TestCase("a/b")]
        [TestCase("a\\b")]
        [TestCase("c:x")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("../outside")]
        public void SlotOrNameThatLeavesTheRoot_Throws(string bad)
        {
            Assert.Throws<ArgumentException>(() => _store.TryRead<QuestLogSaveData>(bad, "quest-log.json", out _));
            Assert.Throws<ArgumentException>(() => _store.TryRead<QuestLogSaveData>("slot-0", bad, out _));
            Assert.Throws<ArgumentException>(() => _store.Write(bad, "quest-log.json", new QuestLogSaveData()));
            Assert.Throws<ArgumentException>(() => _store.Write("slot-0", bad, new QuestLogSaveData()));
            Assert.Throws<ArgumentException>(() => _store.Delete(bad, "quest-log.json"));
            Assert.Throws<ArgumentException>(() => _store.Delete("slot-0", bad));
            Assert.That(Directory.GetFileSystemEntries(Path.GetDirectoryName(_dir.Path)), Has.None.EndWith("outside"));
        }

        [Test]
        public void Write_LeavesNoTempFileBehind()
        {
            _store.Write("slot-0", "quest-log.json", new QuestLogSaveData());
            _store.Write("slot-0", "quest-log.json", new QuestLogSaveData());

            Assert.That(Directory.GetFiles(Path.Combine(_dir.Path, "slot-0")), Is.EqualTo(new[] { PathOf("slot-0", "quest-log.json") }));
        }

        [Test]
        public void Korean_IsWrittenAsIs()
        {
            _store.Write("slot-0", "student.json", new StudentLifeProgressSaveData { NextGuideText = "도서관에 가 보자" });

            var text = File.ReadAllText(PathOf("slot-0", "student.json"));
            Assert.That(text, Does.Contain("도서관에 가 보자"));
            Assert.That(text, Does.Not.Contain("\\u"));
        }

        [Test]
        public void Delete_RemovesTheFile_AndMissingIsFine()
        {
            _store.Write("slot-0", "quest-log.json", new QuestLogSaveData());

            _store.Delete("slot-0", "quest-log.json");

            Assert.That(File.Exists(PathOf("slot-0", "quest-log.json")), Is.False);
            Assert.DoesNotThrow(() => _store.Delete("slot-0", "quest-log.json"));
            Assert.DoesNotThrow(() => _store.Delete("never-made", "quest-log.json"));
        }
    }
}
