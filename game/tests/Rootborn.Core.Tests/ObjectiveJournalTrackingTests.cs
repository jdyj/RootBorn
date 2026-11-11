using NUnit.Framework;
using Rootborn.Core.Objectives;
using Rootborn.Core.Save;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Scripts/UI/Objectives/ObjectiveJournalTrackingStore.cs (세이브 모양과 읽기·쓰기 규칙)
    public sealed class ObjectiveJournalTrackingTests
    {
        private const string Slot = "slot-a";

        [Test]
        public void FileName_IsTheUnityName()
        {
            Assert.That(SaveFileNames.ObjectiveJournalTracking, Is.EqualTo("objective-journal-tracking.json"));
        }

        [Test]
        public void NoFile_MeansNothingIsTracked()
        {
            var store = new InMemorySaveStore();
            Assert.That(ObjectiveJournalTrackingSaveFiles.TryLoad(store, Slot, out var tracking), Is.False);
            Assert.That(tracking.IsTracking, Is.False);
            Assert.That(tracking.CategoryId, Is.Empty);
            Assert.That(tracking.StableKey, Is.Empty);
        }

        [Test]
        public void Save_ThenLoad_KeepsCategoryAndKey()
        {
            var store = new InMemorySaveStore();
            Assert.That(ObjectiveJournalTrackingSaveFiles.Save(store, Slot, "quests", "quest-chain:main"), Is.True);
            Assert.That(ObjectiveJournalTrackingSaveFiles.TryLoad(store, Slot, out var tracking), Is.True);
            Assert.That(tracking.IsTracking, Is.True);
            Assert.That(tracking.CategoryId, Is.EqualTo("quests"));
            Assert.That(tracking.StableKey, Is.EqualTo("quest-chain:main"));
            Assert.That(ObjectiveJournalTrackingSaveFiles.TryLoad(store, "slot-b", out _), Is.False, "파일은 슬롯마다 하나다");
        }

        [Test]
        public void Save_WithoutKeyOrSlot_WritesNothing()
        {
            // Unity Save: 슬롯이나 안정 키가 비면 쓰지 않는다. 빈 분류는 빈 글자로 쓴다.
            var store = new InMemorySaveStore();
            Assert.That(ObjectiveJournalTrackingSaveFiles.Save(store, Slot, "quests", ""), Is.False);
            Assert.That(ObjectiveJournalTrackingSaveFiles.Save(store, "", "quests", "key"), Is.False);
            Assert.That(store.Writes, Is.EqualTo(0));

            Assert.That(ObjectiveJournalTrackingSaveFiles.Save(store, Slot, null, "key"), Is.True);
            ObjectiveJournalTrackingSaveFiles.TryLoad(store, Slot, out var tracking);
            Assert.That(tracking.CategoryId, Is.Empty);
        }

        [Test]
        public void AFileWithAnEmptyKey_ReadsAsNotTracking()
        {
            // Unity TryLoad: 안정 키가 빈 파일은 "추적 없음".
            var store = new InMemorySaveStore();
            store.Put(Slot, SaveFileNames.ObjectiveJournalTracking, new ObjectiveJournalTrackingSaveData { CategoryId = "quests", StableKey = "" });
            Assert.That(ObjectiveJournalTrackingSaveFiles.TryLoad(store, Slot, out var tracking), Is.False);
            Assert.That(tracking.IsTracking, Is.False);
        }

        [Test]
        public void SaveData_IsVersioned_AndAUnityFileIsVersionZero()
        {
            IVersionedSave data = new ObjectiveJournalTrackingSaveData();
            Assert.That(data.Version, Is.EqualTo(SaveVersion.Unity));
            Assert.That(data.KnownVersion, Is.EqualTo(SaveVersion.Current));
        }
    }
}
