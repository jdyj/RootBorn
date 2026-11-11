// 옮겨 옴: Assets/Scripts/Game/StudentLife/DailyEventProgressComponent.cs(DailyEventProgressPersistence 파일 이름과 읽기·쓰기)
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Names = Rootborn.Core.Save.SaveFileNames;

namespace Rootborn.Core.Tests
{
    public sealed class DailyEventSaveFilesTests
    {
        [Test]
        public void FileName_MatchesUnity()
        {
            Assert.That(Names.DailyEvent(""), Is.EqualTo("daily-event-progress.json"));
            Assert.That(Names.DailyEvent(null), Is.EqualTo("daily-event-progress.json"));
            Assert.That(Names.DailyEvent(Names.DefaultPlayerId), Is.EqualTo("daily-event-progress.json"));
            // Unity 판은 "player"를 공용 파일로 보지 않았다.
            Assert.That(Names.DailyEvent(Names.LegacyPlayerId), Is.EqualTo("daily-event-progress-player.json"));
            Assert.That(Names.DailyEvent("steam:7/a"), Is.EqualTo("daily-event-progress-steam_7_a.json"));
        }

        [Test]
        public void SavedRecords_ComeBack()
        {
            var store = new InMemorySaveStore();
            var progress = new DailyEventProgress("slot", Names.DefaultPlayerId);
            var record = progress.GetRecord("daily-event.test");
            record.MarkCompleted("choice.a", "request-1", 3, new[] { "log.a" });
            DailyEventSaveFiles.Save(store, progress);

            Assert.That(store.TryRead("slot", Names.DailyEvent(Names.DefaultPlayerId), out DailyEventProgressSaveData _), Is.True);
            var loaded = DailyEventSaveFiles.LoadOrCreate(store, "slot", Names.DefaultPlayerId);
            Assert.That(loaded.HasCompletionRequest("request-1"), Is.True);
            Assert.That(loaded.GetRecord("daily-event.test").SelectedChoiceId, Is.EqualTo("choice.a"));
        }

        [Test]
        public void ACopiedSlot_IsReadAsTheSlotItIsIn()
        {
            var store = new InMemorySaveStore();
            store.Put("copy", Names.DailyEvent(Names.DefaultPlayerId), new DailyEventProgressSaveData { SaveSlot = "original", PlayerId = "someone" });

            var loaded = DailyEventSaveFiles.LoadOrCreate(store, "copy", Names.DefaultPlayerId);

            Assert.That(loaded.SaveSlot, Is.EqualTo("copy"));
            Assert.That(loaded.PlayerId, Is.EqualTo(Names.DefaultPlayerId));
        }

        [Test]
        public void WithoutAFile_StartsEmpty()
        {
            var loaded = DailyEventSaveFiles.LoadOrCreate(new InMemorySaveStore(), "slot", "");
            Assert.That(loaded.SaveSlot, Is.EqualTo("slot"));
            Assert.That(loaded.ToSaveData().Records, Is.Empty);
        }
    }
}
