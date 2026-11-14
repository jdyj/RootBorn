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
        public void AFileThatNeedsANewerProgram_Throws_TooNew()
        {
            var path = _dir.Write(Path.Combine("slot-0", "quest-log.json"), "{\"Version\":99,\"ReadableFrom\":99,\"Quests\":[]}");

            var e = Assert.Throws<SaveFileTooNewException>(() => _store.TryRead<QuestLogSaveData>("slot-0", "quest-log.json", out _));

            Assert.That(e.Path, Is.EqualTo(path));
            Assert.That(e.FileVersion, Is.EqualTo(99));
            Assert.That(e.KnownVersion, Is.EqualTo(QuestLogSaveData.KnownVersion));
            Assert.That(File.ReadAllText(path), Is.EqualTo("{\"Version\":99,\"ReadableFrom\":99,\"Quests\":[]}"));
        }

        [TestCase("World")]
        [TestCase("QuestChains")]
        [TestCase("FurnitureUnlocks")]
        [TestCase("Wallet")]
        public void AShapeInsideTheFileThatNeedsANewerProgram_Throws_TooNew(string part)
        {
            // 스냅샷 안에 든 모양(월드 상태, 체인, 가구 해금, 지갑)도 저마다 "이 버전부터 읽을 수 있다"를 적을 수 있다.
            // 더 높은 버전만으로는 거절하지 않는다(NewerSaveInAnOlderProgramTests).
            var path = _dir.Write(Path.Combine("slot-0", "snapshot.json"), "{\"Version\":3,\"" + part + "\":{\"Version\":99,\"ReadableFrom\":99}}");

            var e = Assert.Throws<SaveFileTooNewException>(
                () => _store.TryRead<Rootborn.Core.Careers.AdultCareerSnapshotSaveData>("slot-0", "snapshot.json", out _));

            Assert.That(e.FileVersion, Is.EqualTo(99));
            Assert.That(File.ReadAllText(path), Does.Contain("99"), "읽지 못한 파일은 그대로 둔다");
        }

        [Test]
        public void KnownVersionsInsideTheFile_Read()
        {
            _dir.Write(Path.Combine("slot-0", "snapshot.json"), "{\"Version\":3,\"World\":{\"Version\":1},\"Wallet\":{\"Version\":0},\"QuestChains\":null}");
            Assert.That(_store.TryRead<Rootborn.Core.Careers.AdultCareerSnapshotSaveData>("slot-0", "snapshot.json", out var read), Is.True);
            Assert.That(read.World, Is.Not.Null);
            Assert.That(read.QuestChains, Is.Null);
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

        // ---------- 줄 끝: 세이브 바이트는 운영체제와 무관하다(\n). 옛 Windows 빌드가 쓴 \r\n 세이브도 같게 읽는다. ----------
        // 왜: 들여쓰기 줄바꿈이 운영체제를 따르면 같은 내용이 Windows와 Linux에서 다른 바이트가 되고, 손님 저장 묶음의 조각 수까지 달라진다
        // (2026-11-14 방 시뮬레이션 씨앗 954가 두 플랫폼에서 다른 판이 됐다).

        [Test]
        public void Write_UsesLfLineEndings_OnEveryPlatform()
        {
            _store.Write("slot-0", "student.json", new StudentLifeProgressSaveData { NextGuideText = "첫 줄\r\n둘째 줄" });
            using (var batch = _store.BeginBatch("slot-0"))
            {
                batch.Write("slot-0", "quest-log.json", new QuestLogSaveData());
                batch.Commit();
            }

            foreach (var file in new[] { "student.json", "quest-log.json" })
            {
                byte[] bytes = File.ReadAllBytes(PathOf("slot-0", file));
                Assert.That(Array.IndexOf(bytes, (byte)'\r'), Is.EqualTo(-1), file + ": 줄 끝에 \\r이 없다");
                Assert.That(Array.IndexOf(bytes, (byte)'\n'), Is.GreaterThan(0), file + ": 들여쓴 여러 줄이다");
            }

            Assert.That(SaveJson.Serialize(new QuestLogSaveData()), Does.Not.Contain("\r"), "방이 옮기는 저장 묶음의 원문도 같다");
            Assert.That(_store.TryRead<StudentLifeProgressSaveData>("slot-0", "student.json", out var read), Is.True);
            Assert.That(read.NextGuideText, Is.EqualTo("첫 줄\r\n둘째 줄"), "글자 안의 줄바꿈은 이스케이프돼 그대로 돌아온다");
        }

        [Test]
        public void ACrlfSaveFromAnOlderWindowsBuild_ReadsTheSame_AndIsRewrittenWithLf()
        {
            var data = new StudentLifeProgressSaveData { SaveSlot = "slot-0", Energy = 7, CurrentDay = 3, NextGuideText = "도서관" };
            string lf = SaveJson.Serialize(data);
            string crlf = lf.Replace("\n", "\r\n");
            Directory.CreateDirectory(Path.Combine(_dir.Path, "slot-0"));
            File.WriteAllText(PathOf("slot-0", "student.json"), crlf);

            Assert.That(_store.TryRead<StudentLifeProgressSaveData>("slot-0", "student.json", out var read), Is.True);
            Assert.That((read.Energy, read.CurrentDay, read.NextGuideText), Is.EqualTo((7, 3, "도서관")));
            Assert.That(File.ReadAllText(PathOf("slot-0", "student.json")), Is.EqualTo(crlf), "읽기만 해서는 쓰지 않는다");

            _store.Write("slot-0", "student.json", read);
            Assert.That(File.ReadAllText(PathOf("slot-0", "student.json")), Is.EqualTo(lf), "다시 쓰면 같은 내용의 \\n 바이트");
        }

        [Test]
        public void ACrlfSaveWithUnknownFields_KeepsThem_AndIsRewrittenWithLf()
        {
            Directory.CreateDirectory(Path.Combine(_dir.Path, "slot-0"));
            File.WriteAllText(PathOf("slot-0", "quest-log.json"), "{\r\n  \"Version\": 2,\r\n  \"Quests\": [],\r\n  \"Future\": true\r\n}");
            Assert.That(_store.TryRead<QuestLogSaveData>("slot-0", "quest-log.json", out var read), Is.True);

            _store.Write("slot-0", "quest-log.json", read);

            string text = File.ReadAllText(PathOf("slot-0", "quest-log.json"));
            Assert.That(text, Does.Not.Contain("\r"));
            Assert.That(text, Does.Contain("\"Future\": true"), "모르는 필드는 남는다");
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
