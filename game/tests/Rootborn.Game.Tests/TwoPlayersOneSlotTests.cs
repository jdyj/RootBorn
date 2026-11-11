// 원래 파일: Assets/Tests/PlayMode/Quests/PlayerGlobalStateMultiplayerPlayModeTests.cs (PLAYER_STATE_NET_PM_001, PM_002)
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Quests;

namespace Rootborn.Game.Tests
{
    /// <summary>한 세이브 슬롯의 두 플레이어는 인벤토리와 퀘스트 체인을 따로 가진다. 한쪽 세션을 다시 만들어도 다른 쪽은 그대로다. 자동 테스트다.</summary>
    public sealed class TwoPlayersOneSlotTests
    {
        private const string Slot = "slot-mp";

        [Test]
        public void PlayerStateNet001_TwoPlayersKeepSeparateInventories_AcrossRecreatingOne()
        {
            using var saves = new TempSaves();
            var one = TestWorld.Start(saves.Store, Slot, "player-1");
            var two = TestWorld.Start(saves.Store, Slot, "player-2");

            Assert.That(one.BeginPartTimeWork("work.shift.corner-store", "work:p1", 1).Applied, Is.True);
            Assert.That(one.SettleMinigame("work:p1", true).Applied, Is.True);
            int paid = one.Inventory.CountOf(one.Item("item.allowance"));
            Assert.That(paid, Is.GreaterThan(0));

            var oneAgain = TestWorld.Start(saves.Store, Slot, "player-1");
            var twoAgain = TestWorld.Start(saves.Store, Slot, "player-2");
            Assert.That(oneAgain.Inventory.CountOf(oneAgain.Item("item.allowance")), Is.EqualTo(paid));
            Assert.That(twoAgain.Inventory.CountOf(twoAgain.Item("item.allowance")), Is.EqualTo(0));
            Assert.That(twoAgain.Student.TimePointsSpentToday, Is.EqualTo(two.Student.TimePointsSpentToday), "다른 플레이어의 시간은 쓰지 않는다");
        }

        [Test]
        public void PlayerStateNet_TwoPlayersKeepSeparateQuestChains()
        {
            using var saves = new TempSaves();
            var one = TestWorld.Start(saves.Store, Slot, "player-1");
            TestWorld.Start(saves.Store, Slot, "player-2");
            one.TalkTo("npc.first-guide"); // 메인 체인을 시작한다.

            var chain = one.Chain(MainChainTests.ChainId);
            Assert.That(TestWorld.Start(saves.Store, Slot, "player-1").QuestChains.GetState(chain), Is.EqualTo(QuestChainState.Tracked));
            Assert.That(TestWorld.Start(saves.Store, Slot, "player-2").QuestChains.GetState(chain), Is.Not.EqualTo(QuestChainState.Tracked));
        }

        [Test]
        public void PlayerStateNetPm002_TwoPlayersKeepSeparateQuestLogs_InTheSameSlot()
        {
            using var saves = new TempSaves();
            var one = TestWorld.Start(saves.Store, Slot, "player-1");
            var two = TestWorld.Start(saves.Store, Slot, "player-2");

            Assert.That(one.AcceptQuest("quest.gatherWood"), Is.True);
            Assert.That(one.QuestLog.GetState("quest.gatherWood"), Is.EqualTo(QuestStage.Active));
            Assert.That(two.QuestLog.GetState("quest.gatherWood"), Is.EqualTo(QuestStage.NotStarted));

            // 다시 읽어도 나뉘어 있다. 다른 플레이어가 저장해도 player-1의 기록을 덮지 않는다.
            Assert.That(TestWorld.Start(saves.Store, Slot, "player-2").AcceptQuest("quest.gatherWood"), Is.True);
            Assert.That(TestWorld.Start(saves.Store, Slot, "player-1").QuestLog.GetState("quest.gatherWood"), Is.EqualTo(QuestStage.Active));
            Assert.That(HasFile(saves, "quest-log-player-1.json"), Is.True);
            Assert.That(HasFile(saves, "quest-log-player-2.json"), Is.True);
            Assert.That(HasFile(saves, "quest-log.json"), Is.False, "기본 플레이어가 아니면 공용 파일을 쓰지 않는다");
        }

        [Test]
        public void DefaultPlayer_KeepsUsingTheSharedQuestLogFile()
        {
            using var saves = new TempSaves();
            Assert.That(TestWorld.Start(saves.Store, Slot, null).AcceptQuest("quest.gatherWood"), Is.True);
            Assert.That(HasFile(saves, "quest-log.json"), Is.True);
        }

        private static bool HasFile(TempSaves saves, string name) => File.Exists(Path.Combine(saves.Path, Slot, name));
    }
}
