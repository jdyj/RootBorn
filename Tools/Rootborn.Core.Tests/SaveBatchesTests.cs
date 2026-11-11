using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    /// <summary>묶음을 모르는 저장소(메모리 저장소, Net 쪽 저장소)는 SaveBatches.Begin이 지금처럼 바로 쓰는 묶음으로 감싼다.</summary>
    public sealed class SaveBatchesTests
    {
        [Test]
        public void AStoreWithoutBatches_GetsWritesAndDeletesRightAway()
        {
            var store = new InMemorySaveStore();
            store.Put("s", "old.json", new StudentLifeProgressSaveData { CurrentDay = 1 });

            using (var batch = SaveBatches.Begin(store, "s"))
            {
                batch.Write("s", "a.json", new StudentLifeProgressSaveData { CurrentDay = 2 });
                Assert.That(store.Contains("s", "a.json"), Is.True, "바로 쓴다");
                batch.Delete("s", "old.json");
                Assert.That(store.Contains("s", "old.json"), Is.False);
                Assert.That(batch.TryRead<StudentLifeProgressSaveData>("s", "a.json", out var read), Is.True);
                Assert.That(read.CurrentDay, Is.EqualTo(2));
                batch.Commit();
            }

            Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test]
        public void ABatchStore_IsAskedForItsOwnBatch()
        {
            var store = new CountingBatchStore();
            using (var batch = SaveBatches.Begin(store, "s")) batch.Commit();
            Assert.That(store.Begun, Is.EqualTo(1));
        }

        private sealed class CountingBatchStore : IBatchSaveStore
        {
            private readonly InMemorySaveStore _inner = new InMemorySaveStore();
            public int Begun { get; private set; }

            public ISaveBatch BeginBatch(string saveSlot)
            {
                Begun++;
                return SaveBatches.Begin(_inner, saveSlot);
            }

            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => _inner.TryRead(saveSlot, fileName, out data);
            public void Write<T>(string saveSlot, string fileName, T data) where T : class => _inner.Write(saveSlot, fileName, data);
            public void Delete(string saveSlot, string fileName) => _inner.Delete(saveSlot, fileName);
        }
    }
}
