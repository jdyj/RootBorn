using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 같이 하는 방의 플레이어 저장소(메모리에 든 원문 파일)와 방이 손님 세이브를 옮길 때 쓰는 원문 읽기·쓰기.
    /// 형식은 파일 저장소와 같고(깨진 파일·더 새 버전은 예외), 저장 한 번은 바뀐 파일 묶음 하나로 알린다.
    /// </summary>
    public sealed class MemorySaveSlotTests
    {
        [Test]
        public void ABatch_IsAnnouncedOnceWithEveryFile_AndAFileStoreReadsTheSameText()
        {
            var slot = new MemorySaveSlot("room");
            var told = new List<SaveFileChanges>();
            slot.Committed += told.Add;
            using (var batch = SaveBatches.Begin(slot, "room"))
            {
                batch.Write("room", "a.json", new RoomOutboxSaveData { Version = 1 });
                batch.Write("room", "b.json", new RoomClockSaveData { Day = 3 });
                Assert.That(slot.Files, Is.Empty, "Commit 전에는 바뀌지 않는다");
                batch.Commit();
            }

            Assert.That(told.Count, Is.EqualTo(1));
            Assert.That(told[0].Writes.Select(w => w.Key), Is.EqualTo(new[] { "a.json", "b.json" }));
            Assert.That(slot.TryRead("room", "b.json", out RoomClockSaveData clock), Is.True);
            Assert.That(clock.Day, Is.EqualTo(3));

            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            store.WriteSlotFiles("guest", told[0].Writes, null);
            Assert.That(store.TryRead("guest", "b.json", out RoomClockSaveData fromFile), Is.True);
            Assert.That(fromFile.Day, Is.EqualTo(3), "같은 형식이다");
        }

        [Test]
        public void ADiscardedBatch_ChangesAndAnnouncesNothing()
        {
            var slot = new MemorySaveSlot("room");
            int told = 0;
            slot.Committed += _ => told++;
            using (var batch = slot.BeginBatch("room")) batch.Write("room", "a.json", new RoomOutboxSaveData());
            Assert.That(told, Is.Zero);
            Assert.That(slot.Files, Is.Empty);
        }

        [Test]
        public void BrokenOrTooNewFiles_AreErrors_NotMissing()
        {
            var slot = new MemorySaveSlot("room", new[]
            {
                new KeyValuePair<string, string>("broken.json", "{ not json"),
                new KeyValuePair<string, string>("new.json", "{ \"Version\": 99, \"ReadableFrom\": 99 }"),
            });
            Assert.Throws<SaveFileCorruptException>(() => slot.TryRead("room", "broken.json", out RoomClockSaveData _));
            Assert.Throws<SaveFileTooNewException>(() => slot.TryRead("room", "new.json", out RoomClockSaveData _));
            Assert.That(slot.TryRead("other", "broken.json", out RoomClockSaveData _), Is.False, "다른 슬롯은 없다");
            Assert.Throws<ArgumentException>(() => slot.Write("other", "a.json", new RoomClockSaveData()));
        }

        [Test]
        public void SlotFiles_ReadOnlyTopLevelSaveFiles_AndRefuseNamesThatAreNotSaveFiles()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            store.Write("guest", "a.json", new RoomClockSaveData { Day = 2 });
            store.KeepDayCopy("guest", 2, 3);
            File.WriteAllText(Path.Combine(dir.Path, "guest", ".tmp"), "x");

            var files = store.ReadSlotFiles("guest");
            Assert.That(files.Select(f => f.Key), Is.EqualTo(new[] { "a.json" }), "임시 파일과 사본 폴더는 뺀다");
            Assert.That(store.ReadSlotFiles("nobody"), Is.Empty);

            Assert.Throws<ArgumentException>(() => store.WriteSlotFiles("guest", new[] { new KeyValuePair<string, string>(".save-batch", "{}") }, null));
            Assert.Throws<ArgumentException>(() => store.WriteSlotFiles("guest", null, new[] { "backups" }));
            Assert.Throws<ArgumentException>(() => store.WriteSlotFiles("guest", new[] { new KeyValuePair<string, string>("../x.json", "{}") }, null));

            store.WriteSlotFiles("guest", new[] { new KeyValuePair<string, string>("b.json", "{ \"Day\": 5 }") }, new[] { "a.json" });
            Assert.That(store.ReadSlotFiles("guest").Select(f => f.Key), Is.EqualTo(new[] { "b.json" }));
        }
    }
}
