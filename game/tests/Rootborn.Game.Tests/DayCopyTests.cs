using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Data.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 안전 4: 다음 날을 시작할 때 슬롯 전체의 사본을 &lt;슬롯&gt;/backups/&lt;날 번호&gt;/에 남기고 최근 N개만 둔다(기본 3, 0이면 끔).
    /// 되살리는 명령은 없다. 사본 파일을 슬롯에 복사해 넣으면 그날로 읽히는지만 본다. 자동 테스트다.
    /// </summary>
    public sealed class DayCopyTests
    {
        private const string Slot = "slot-copies";

        private static string Copies(TempSaves saves) => Path.Combine(saves.Path, Slot, FileSaveStore.DayCopiesFolder);

        private static string[] CopyNames(TempSaves saves)
            => Directory.Exists(Copies(saves)) ? Directory.GetDirectories(Copies(saves)).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray() : new string[0];

        private static void NextDay(GameSession game)
        {
            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
        }

        [Test]
        public void ByDefault_TheLastThreeDaysAreKept()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store, Slot);
            Assert.That(game.DayCopiesToKeep, Is.EqualTo(GameSession.DefaultDayCopiesToKeep));
            Assert.That(GameSession.DefaultDayCopiesToKeep, Is.EqualTo(3));
            for (int i = 0; i < 5; i++) NextDay(game);

            Assert.That(game.Student.CurrentDay, Is.EqualTo(6));
            Assert.That(CopyNames(saves), Is.EqualTo(new[] { "4", "5", "6" }));
            Assert.That(game.DayCopyError, Is.Null);
        }

        [Test]
        public void ACopyHoldsEveryFileOfTheSlot_AsItWasWhenTheDayStarted()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store, Slot);
            game.TalkTo("npc.first-guide");
            NextDay(game);
            var slot = SlotCheckpoints.Read(saves, Slot);
            var copy = SlotCheckpoints.Read(saves, Slot + "/" + FileSaveStore.DayCopiesFolder + "/2");

            Assert.That(SlotCheckpoints.Same(slot, copy), Is.True);
            Assert.That(Directory.GetDirectories(Path.Combine(Copies(saves), "2")), Is.Empty, "사본 안에 사본을 넣지 않는다");
        }

        [Test]
        public void Zero_TurnsCopiesOff()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store, Slot);
            game.KeepDayCopies(0);
            NextDay(game);
            Assert.That(Directory.Exists(Copies(saves)), Is.False);
        }

        [Test]
        public void ANegativeCount_IsRefused()
        {
            using var saves = new TempSaves();
            Assert.Throws<ArgumentOutOfRangeException>(() => TestWorld.Start(saves.Store, Slot).KeepDayCopies(-1));
        }

        [Test]
        public void TurningTheCountDown_DropsOlderCopiesAtTheNextDay()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store, Slot);
            for (int i = 0; i < 3; i++) NextDay(game);
            Assert.That(CopyNames(saves), Is.EqualTo(new[] { "2", "3", "4" }));

            game.KeepDayCopies(1);
            NextDay(game);
            Assert.That(CopyNames(saves), Is.EqualTo(new[] { "5" }));
        }

        [Test]
        public void CopyingACopyBackIntoTheSlot_ReadsThatDay()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store, Slot);
            game.TalkTo("npc.first-guide");
            NextDay(game);
            NextDay(game);
            NextDay(game);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(4));

            // 슬롯을 비우고(손상됐다고 치고) 2일 사본을 복사해 넣는다.
            var folder = Path.Combine(saves.Path, Slot);
            foreach (var file in Directory.GetFiles(folder)) File.Delete(file);
            foreach (var file in Directory.GetFiles(Path.Combine(Copies(saves), "2"))) File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));

            var restored = TestWorld.Start(new FileSaveStore(saves.Path), Slot);
            Assert.That(restored.Student.CurrentDay, Is.EqualTo(2));
            NextDay(restored);
            Assert.That(TestWorld.Start(new FileSaveStore(saves.Path), Slot).Student.CurrentDay, Is.EqualTo(3), "되살린 슬롯으로 이어 간다");
        }

        [Test]
        public void ACopyIntoAnotherSlotFolder_IsReadAsThatSlot()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store, Slot);
            NextDay(game);
            var target = Path.Combine(saves.Path, "slot-restored");
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(Path.Combine(Copies(saves), "2"))) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));

            Assert.That(TestWorld.Start(saves.Store, "slot-restored").Student.CurrentDay, Is.EqualTo(2));
        }

        [Test]
        public void AStoreWithoutCopies_JustStartsTheNextDay()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(new WriteThroughStore(saves), Slot);
            NextDay(game);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(game.DayCopyError, Is.Null);
            Assert.That(Directory.Exists(Copies(saves)), Is.False);
        }

        /// <summary>사본 기능이 없는 저장소(FileSaveStore를 감싸 묶음·사본을 숨긴다).</summary>
        private sealed class WriteThroughStore : ISaveStore
        {
            private readonly TempSaves _saves;
            public WriteThroughStore(TempSaves saves) => _saves = saves;
            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => _saves.Store.TryRead(saveSlot, fileName, out data);
            public void Write<T>(string saveSlot, string fileName, T data) where T : class => _saves.Store.Write(saveSlot, fileName, data);
            public void Delete(string saveSlot, string fileName) => _saves.Store.Delete(saveSlot, fileName);
        }
    }
}
