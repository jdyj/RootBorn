// 원래 흐름: Assets/Scripts/UI/Quests/QuestChainLogPanel.cs RefreshButtons(받기·추적·보상 단추)와 추적 단추(log.Track).
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Quests;

namespace Rootborn.Game.Tests
{
    /// <summary>체인 기록 화면의 조회(받을 수 있는지, 보상 받을 수 있는지)와 추적 명령. 자동 테스트다.</summary>
    public sealed class QuestChainQueryTests
    {
        private const string Main = MainChainTests.ChainId;

        [Test]
        public void CanAcceptQuestChain_IsTrueOnlyWhileAvailable_AndDoesNotAccept()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.CanAcceptQuestChain(Main), Is.True);
            Assert.That(game.QuestChains.GetState(game.Chain(Main)), Is.EqualTo(QuestChainState.Available), "조회는 받지 않는다");

            Assert.That(game.AcceptQuestChain(Main), Is.True);
            Assert.That(game.CanAcceptQuestChain(Main), Is.False, "이미 받았다");
            Assert.That(game.CanAcceptQuestChain("questchain.none"), Is.False);
        }

        [Test]
        public void CanClaimQuestChainRewards_IsFalseBeforeTheChainEnds()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.AcceptQuestChain(Main);
            Assert.That(game.CanClaimQuestChainRewards(Main), Is.False);
            Assert.That(game.ClaimQuestChainRewards(Main), Is.False, "명령도 같다");
            Assert.That(game.CanClaimQuestChainRewards("questchain.none"), Is.False);
        }

        [Test]
        public void TrackQuestChain_SwitchesTheTrackedChain_AndIsSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.TrackQuestChain(Main), Is.False, "받지 않은 체인은 추적할 수 없다");
            Assert.That(game.AcceptQuestChain(Main), Is.True);
            var other = game.Definitions.All<QuestChainDefinition>().Select(c => c.Id).First(id => id != Main && game.CanAcceptQuestChain(id));
            Assert.That(game.AcceptQuestChain(other), Is.True, "받으면 추적한다");
            Assert.That(game.QuestChains.GetState(game.Chain(Main)), Is.EqualTo(QuestChainState.Paused));

            int changes = 0;
            game.Changed += _ => changes++;
            Assert.That(game.TrackQuestChain(Main), Is.True);
            Assert.That(changes, Is.GreaterThan(0));
            Assert.That(game.QuestChains.GetState(game.Chain(Main)), Is.EqualTo(QuestChainState.Tracked));
            Assert.That(game.QuestChains.GetState(game.Chain(other)), Is.EqualTo(QuestChainState.Paused));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.QuestChains.GetState(again.Chain(Main)), Is.EqualTo(QuestChainState.Tracked), "추적이 저장된다");
            Assert.That(again.QuestChains.GetState(again.Chain(other)), Is.EqualTo(QuestChainState.Paused));
        }
    }
}
