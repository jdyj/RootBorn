using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// FileSaveStore 묶음 쓰기: 준비 폴더에 다 쓰고 끝났다는 표시를 남긴 뒤 바꿔 넣는다. 묶음 도중 어디서 꺼져도(단계 k에서 멈춤)
    /// 새 저장소로 다시 읽으면 슬롯이 전부 옛것이거나 전부 새것이다. 표시 전에 멈추면 옛것, 표시 뒤에 멈추면 새것이다. 자동 테스트다.
    /// </summary>
    public sealed class BatchSaveStoreTests
    {
        private const string Slot = "slot-0";
        private static readonly string[] Files = { "a.json", "b.json", "c.json" };
        private const string OldState = "a.json=1 b.json=1 c.json=- gone.json=1";
        private const string NewState = "a.json=2 b.json=2 c.json=2 gone.json=-";

        private TempDir _dir;

        [SetUp]
        public void SetUp() => _dir = new TempDir();

        [TearDown]
        public void TearDown() => _dir.Dispose();

        private sealed class SimulatedCrash : Exception
        {
        }

        private static StudentLifeProgressSaveData Day(int day) => new StudentLifeProgressSaveData { SaveSlot = Slot, CurrentDay = day };

        // 옛 슬롯: a·b는 1일, gone.json이 있다. 새 묶음: a·b·c를 2일로 쓰고 gone.json을 지운다.
        private static void WriteOld(FileSaveStore store)
        {
            store.Write(Slot, "a.json", Day(1));
            store.Write(Slot, "b.json", Day(1));
            store.Write(Slot, "gone.json", Day(1));
        }

        private static void WriteNew(ISaveBatch batch)
        {
            foreach (var file in Files) batch.Write(Slot, file, Day(2));
            batch.Delete(Slot, "gone.json");
        }

        private static void CommitNew(FileSaveStore store)
        {
            using var batch = store.BeginBatch(Slot);
            WriteNew(batch);
            batch.Commit();
        }

        private static string State(FileSaveStore store)
        {
            var parts = new List<string>();
            foreach (var file in Files.Concat(new[] { "gone.json" }))
                parts.Add(file + "=" + (store.TryRead<StudentLifeProgressSaveData>(Slot, file, out var data) ? data.CurrentDay.ToString() : "-"));
            return string.Join(" ", parts);
        }

        /// <summary>끊기지 않은 묶음 하나가 지나는 단계 이름들.</summary>
        private static List<string> StepLabels()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            WriteOld(store);
            var labels = new List<string>();
            store.BatchStepForTests = labels.Add;
            CommitNew(store);
            return labels;
        }

        public static IEnumerable<int> StopPoints() => Enumerable.Range(0, StepLabels().Count);

        [Test]
        public void Commit_WritesEverything_AndLeavesNoBatchFolder()
        {
            var store = new FileSaveStore(_dir.Path);
            WriteOld(store);
            using (var batch = store.BeginBatch(Slot))
            {
                WriteNew(batch);
                Assert.That(State(store), Is.EqualTo(OldState), "Commit 전에는 슬롯이 그대로다");
                batch.Commit();
            }

            Assert.That(State(store), Is.EqualTo(NewState));
            Assert.That(Directory.GetDirectories(Path.Combine(_dir.Path, Slot)), Is.Empty);
            Assert.That(Directory.GetFiles(Path.Combine(_dir.Path, Slot)).Select(Path.GetFileName), Is.EquivalentTo(Files));
        }

        [Test]
        public void DisposingWithoutCommit_ChangesNothing()
        {
            var store = new FileSaveStore(_dir.Path);
            WriteOld(store);
            using (var batch = store.BeginBatch(Slot)) WriteNew(batch);
            Assert.That(State(store), Is.EqualTo(OldState));
            Assert.That(Directory.GetDirectories(Path.Combine(_dir.Path, Slot)), Is.Empty);
        }

        [Test]
        public void ReadingInsideTheBatch_SeesItsOwnWritesAndDeletes()
        {
            var store = new FileSaveStore(_dir.Path);
            WriteOld(store);
            using var batch = store.BeginBatch(Slot);
            WriteNew(batch);
            Assert.That(batch.TryRead<StudentLifeProgressSaveData>(Slot, "c.json", out var c), Is.True);
            Assert.That(c.CurrentDay, Is.EqualTo(2));
            Assert.That(batch.TryRead<StudentLifeProgressSaveData>(Slot, "gone.json", out _), Is.False);
            Assert.That(batch.TryRead<StudentLifeProgressSaveData>(Slot, "b.json", out var b) && b.CurrentDay == 2, Is.True);
        }

        [Test]
        public void ABatchWritesOnlyItsOwnSlot()
        {
            var store = new FileSaveStore(_dir.Path);
            using var batch = store.BeginBatch(Slot);
            Assert.Throws<ArgumentException>(() => batch.Write("other", "a.json", Day(1)));
            Assert.Throws<ArgumentException>(() => batch.Delete("other", "a.json"));
        }

        [Test]
        public void ThereIsAMarkStep_BetweenStagingAndReplacing()
        {
            var labels = StepLabels();
            int mark = labels.IndexOf("mark");
            Assert.That(mark, Is.EqualTo(Files.Length), "준비 쓰기 다음에 표시가 있다: " + string.Join(", ", labels));
            Assert.That(labels.Count, Is.GreaterThan(mark + Files.Length), "표시 뒤에 바꿔 넣기마다 단계가 있다");
        }

        [TestCaseSource(nameof(StopPoints))]
        public void StoppingAfterStepK_ReadsAllOldBeforeTheMark_AndAllNewAfterIt(int k)
        {
            var labels = StepLabels();
            int mark = labels.IndexOf("mark");
            var store = new FileSaveStore(_dir.Path);
            WriteOld(store);
            int steps = 0;
            store.BatchStepForTests = _ => { if (steps++ == k) throw new SimulatedCrash(); };
            Assert.Throws<SimulatedCrash>(() => CommitNew(store));

            var reopened = new FileSaveStore(_dir.Path);
            var state = State(reopened);
            TestContext.Out.WriteLine($"단계 {k}({labels[k]}) 뒤에 멈춤 → {state}");
            Assert.That(state, Is.EqualTo(k < mark ? OldState : NewState));
            Assert.That(Directory.GetDirectories(Path.Combine(_dir.Path, Slot)), Is.Empty, "다시 읽으면 남은 묶음을 치운다");

            // 이어서 쓴 다음 묶음도 온전히 적용된다.
            CommitNew(reopened);
            Assert.That(State(reopened), Is.EqualTo(NewState));
        }

        [TestCaseSource(nameof(StopPoints))]
        public void StoppingAfterStepK_ThenASingleWrite_FinishesTheBatchFirst(int k)
        {
            var store = new FileSaveStore(_dir.Path);
            WriteOld(store);
            int steps = 0;
            store.BatchStepForTests = _ => { if (steps++ == k) throw new SimulatedCrash(); };
            Assert.Throws<SimulatedCrash>(() => CommitNew(store));
            var before = State(new FileSaveStore(_dir.Path));

            // 다시 읽기 전에 파일 하나만 쓰는 명령이 와도 남은 묶음이 그 쓰기를 덮지 않는다.
            var reopened = new FileSaveStore(_dir.Path);
            reopened.Write(Slot, "a.json", Day(9));
            Assert.That(State(reopened), Is.EqualTo("a.json=9" + before.Substring("a.json=1".Length)));
        }

        [Test]
        public void ASlotWithoutBatchFolder_ReadsAsBefore()
        {
            // 묶음 쓰기 전에 만든 세이브(표시도 준비 폴더도 없다)는 그대로 읽힌다.
            _dir.Write(Path.Combine(Slot, "a.json"), "{ \"CurrentDay\": 4 }");
            var store = new FileSaveStore(_dir.Path);
            Assert.That(store.TryRead<StudentLifeProgressSaveData>(Slot, "a.json", out var data), Is.True);
            Assert.That(data.CurrentDay, Is.EqualTo(4));
            Assert.That(Directory.GetFiles(Path.Combine(_dir.Path, Slot)).Select(Path.GetFileName), Is.EquivalentTo(new[] { "a.json" }));
        }

        [Test]
        public void ABatchInAnotherSlot_DoesNotTouchThisSlot()
        {
            var store = new FileSaveStore(_dir.Path);
            WriteOld(store);
            using (var batch = store.BeginBatch("slot-1"))
            {
                batch.Write("slot-1", "a.json", Day(5));
                batch.Commit();
            }

            Assert.That(State(store), Is.EqualTo(OldState));
            Assert.That(store.TryRead<StudentLifeProgressSaveData>("slot-1", "a.json", out var other) && other.CurrentDay == 5, Is.True);
        }
    }
}
