using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 견고성 4: 한 슬롯에 플레이어 둘이 번갈아 시작하고 진행해도 학생 진행·인벤토리·체인·캠페인이 섞이지 않는다.
    /// 일반 퀘스트 기록(quest-log.json)이 플레이어별로 나뉘지 않는 것은 알려진 것이라 건드리지 않는다. 자동 테스트다.
    /// </summary>
    public sealed class TwoPlayersInterleavedTests
    {
        private const string Slot = "slot-two";
        private const string One = "player-1";
        private const string Two = "player-2";

        private static GameSession As(TempSaves saves, string player) => TestWorld.Start(saves.Store, Slot, player);

        [Test]
        public void AlternatingPlayers_KeepStudentInventoryChainAndCampaignApart()
        {
            using var saves = new TempSaves();
            var one = As(saves, One);
            var two = As(saves, Two);

            // 둘 다 열어 둔 채 번갈아 명령한다.
            one.TalkTo("npc.first-guide");
            one.SelectDailyPlanRoute(one.AvailableDailyPlanRoutes().First().Id);
            Assert.That(two.BeginPartTimeWork("work.shift.corner-store", "work:p2", 3).Applied, Is.True);
            one.TalkTo("npc.teacher");
            Assert.That(two.SettleMinigame("work:p2", true).Applied, Is.True);
            Assert.That(one.EndDay().Ended, Is.True);
            Assert.That(one.StartNextDay(), Is.True);
            int paid = two.Inventory.CountOf(two.Item("item.allowance"));
            Assert.That(paid, Is.GreaterThan(0));

            var oneAgain = As(saves, One);
            var twoAgain = As(saves, Two);
            var chain = oneAgain.Chain(MainChainTests.ChainId);

            Assert.That(oneAgain.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(twoAgain.Student.CurrentDay, Is.EqualTo(1), "2의 날짜는 1이 하루를 넘겨도 그대로");
            Assert.That(oneAgain.Inventory.CountOf(oneAgain.Item("item.allowance")), Is.EqualTo(0));
            Assert.That(twoAgain.Inventory.CountOf(twoAgain.Item("item.allowance")), Is.EqualTo(paid));
            Assert.That(oneAgain.QuestChains.GetState(chain), Is.EqualTo(QuestChainState.Tracked));
            Assert.That(twoAgain.QuestChains.GetState(chain), Is.Not.EqualTo(QuestChainState.Tracked), "2는 엄마와 이야기하지 않았다");
            Assert.That(oneAgain.Campaign.ToSaveData().CompletedObjectiveIds, Is.Not.Empty);
            Assert.That(twoAgain.Campaign.ToSaveData().CompletedObjectiveIds, Is.Empty, "캠페인 목표도 따로");
        }

        [Test]
        public void EachPlayerHasOwnFiles_AndNeitherPlayersSnapshotIsOverwrittenByTheOther()
        {
            using var saves = new TempSaves();
            var one = As(saves, One);
            var two = As(saves, Two);
            one.TalkTo("npc.first-guide");
            two.TalkTo("npc.first-guide");

            var files = SlotCheckpoints.Read(saves, Slot);
            Assert.That(files.Keys, Does.Contain(SaveFileNames.AdultCareerSnapshot(One)));
            Assert.That(files.Keys, Does.Contain(SaveFileNames.AdultCareerSnapshot(Two)));
            var oneSnapshot = files[SaveFileNames.AdultCareerSnapshot(One)].ToArray();

            two.SelectDailyPlanRoute(two.AvailableDailyPlanRoutes().First().Id);
            two.TalkTo("npc.teacher");

            Assert.That(SlotCheckpoints.Read(saves, Slot)[SaveFileNames.AdultCareerSnapshot(One)].AsSpan().SequenceEqual(oneSnapshot), Is.True, "2의 명령이 1의 스냅샷을 쓰지 않는다");
        }

        [Test]
        public void ADamagedSnapshotOfOnePlayer_DoesNotStopTheOtherPlayer()
        {
            using var saves = new TempSaves();
            As(saves, One).TalkTo("npc.first-guide");
            As(saves, Two).TalkTo("npc.first-guide");
            File.WriteAllBytes(Path.Combine(saves.Path, Slot, SaveFileNames.AdultCareerSnapshot(One)), new byte[0]);

            Assert.Catch(() => As(saves, One));
            var two = As(saves, Two);
            Assert.That(two.QuestChains.GetState(two.Chain(MainChainTests.ChainId)), Is.EqualTo(QuestChainState.Tracked));
        }
    }
}
