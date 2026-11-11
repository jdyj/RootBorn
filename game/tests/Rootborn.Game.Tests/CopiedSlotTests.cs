using System.IO;
using NUnit.Framework;
using Rootborn.Data.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 슬롯 폴더를 다른 이름의 폴더로 복사해(또는 날마다 남기는 사본 backups/&lt;날&gt;/을 다른 슬롯 폴더로 복사해) 이어 하면,
    /// 세이브 안의 슬롯 이름이 옛 슬롯이어도 지금 슬롯으로 읽고, 첫 저장과 다시 읽기까지 된다. 자동 테스트다.
    /// </summary>
    public sealed class CopiedSlotTests
    {
        private const string Original = "slot-1";
        private const string Copy = "slot-copied";

        private static void CopyFolder(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        private static void NextDay(GameSession game)
        {
            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
        }

        // 진행 객체는 자기 슬롯 이름으로 저장한다. 하나라도 옛 이름이면 첫 저장이 옛 슬롯으로 간다.
        private static void AssertEveryDomainIsIn(GameSession game, string slot)
        {
            Assert.Multiple(() =>
            {
                Assert.That(game.Student.SaveSlot, Is.EqualTo(slot), "학생");
                Assert.That(game.Adult.SaveSlot, Is.EqualTo(slot), "성인");
                Assert.That(game.World.SaveSlot, Is.EqualTo(slot), "월드 상태");
                Assert.That(game.Campaign?.SaveSlot ?? slot, Is.EqualTo(slot), "캠페인");
                Assert.That(game.Milestones.SaveSlot, Is.EqualTo(slot), "마일스톤");
                Assert.That(game.CareerInterests.SaveSlot, Is.EqualTo(slot), "진로 관심");
                Assert.That(game.LocationStates.SaveSlot, Is.EqualTo(slot), "장소 상태");
                Assert.That(game.FurnitureUnlocks.SaveSlot, Is.EqualTo(slot), "가구 해금");
                Assert.That(game.Exploration.SaveSlot, Is.EqualTo(slot), "탐색");
                Assert.That(game.DiscoveryClues.SaveSlot, Is.EqualTo(slot), "발견 단서");
                Assert.That(game.ClueInterpretations.SaveSlot, Is.EqualTo(slot), "단서 해석");
                Assert.That(game.Encyclopedia.SaveSlot, Is.EqualTo(slot), "도감");
                Assert.That(game.DailyEvents.SaveSlot, Is.EqualTo(slot), "하루 이벤트");
                Assert.That(game.ResourceRecipes().SaveSlot, Is.EqualTo(slot), "제작법");
            });
        }

        [Test]
        public void ASlotFolderCopiedUnderAnotherName_SavesAndReadsBackAsTheNewSlot()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store, Original);
            game.TalkTo("npc.first-guide");
            NextDay(game);
            CopyFolder(Path.Combine(saves.Path, Original), Path.Combine(saves.Path, Copy));

            var copied = TestWorld.Start(new FileSaveStore(saves.Path), Copy);
            Assert.That(copied.Student.CurrentDay, Is.EqualTo(2));
            AssertEveryDomainIsIn(copied, Copy);
            Assert.DoesNotThrow(() => NextDay(copied), "첫 저장에서 옛 슬롯 이름으로 쓰지 않는다");

            var again = TestWorld.Start(new FileSaveStore(saves.Path), Copy);
            Assert.That(again.Student.CurrentDay, Is.EqualTo(3));
            Assert.That(TestWorld.Start(new FileSaveStore(saves.Path), Original).Student.CurrentDay, Is.EqualTo(2), "원래 슬롯은 그대로");
        }

        [Test]
        public void ADayCopyPutIntoAnotherSlotFolder_SavesAndReadsBack()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store, Original);
            game.TalkTo("npc.first-guide");
            NextDay(game);
            NextDay(game);
            CopyFolder(Path.Combine(saves.Path, Original, FileSaveStore.DayCopiesFolder, "2"), Path.Combine(saves.Path, Copy));

            var restored = TestWorld.Start(new FileSaveStore(saves.Path), Copy);
            Assert.That(restored.Student.CurrentDay, Is.EqualTo(2));
            AssertEveryDomainIsIn(restored, Copy);
            Assert.DoesNotThrow(() => NextDay(restored));
            Assert.That(TestWorld.Start(new FileSaveStore(saves.Path), Copy).Student.CurrentDay, Is.EqualTo(3));
        }
    }
}
