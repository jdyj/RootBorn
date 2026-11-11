using NUnit.Framework;
using Rootborn.Core.Quests;
using Names = Rootborn.Core.Save.SaveFileNames;

namespace Rootborn.Core.Tests
{
    // 세이브 파일 이름은 Core.Save.SaveFileNames 한 곳에서 만든다. 기본 플레이어의 값은 Unity 판 파일 이름 그대로다.
    public sealed class SaveFileNamesTests
    {
        [Test]
        public void QuestChain_DefaultPlayer_UsesTheSharedName()
        {
            Assert.That(Names.QuestChain(null), Is.EqualTo("quest-chain-progress.json"));
            Assert.That(Names.QuestChain(""), Is.EqualTo("quest-chain-progress.json"));
            Assert.That(Names.QuestChain("player"), Is.EqualTo("quest-chain-progress.json"));
            Assert.That(Names.QuestChain(QuestChainProgressStore.DefaultPlayerId), Is.EqualTo("quest-chain-progress.json"));
            Assert.That(QuestChainProgressStore.DefaultPlayerId, Is.EqualTo("local-player"));
        }

        [Test]
        public void QuestChain_OtherPlayer_MatchesTheOldRule()
        {
            // 옮겨 옴: QuestChainProgressStoreTests.FileNames_DefaultPlayersShareTheLegacyFile_OthersAreHexEncoded
            Assert.That(Names.QuestChain("a:"), Is.EqualTo("quest-chain-progress-613a.json"));
            Assert.That(Names.QuestChain("한"), Is.EqualTo("quest-chain-progress-ed959c.json"), "every UTF-8 byte is encoded");
        }

        [Test]
        public void QuestLog_IsTheSharedName()
        {
            Assert.That(Names.QuestLog, Is.EqualTo("quest-log.json"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("local-player")]
        public void PlayerQuestLog_DefaultPlayerKeepsTheSharedName(string player)
        {
            // Assets/Scripts/Game/Player/PlayerGlobalState.cs PlayerQuestRuntime.LogFileName: 빈 id와 "local-player"만 공용 파일.
            Assert.That(Names.PlayerQuestLog(player), Is.EqualTo("quest-log.json"));
        }

        [Test]
        public void PlayerQuestLog_OtherPlayersGetTheirOwnSanitizedFile()
        {
            Assert.That(Names.PlayerQuestLog("player-2"), Is.EqualTo("quest-log-player-2.json"));
            Assert.That(Names.PlayerQuestLog("steam:7"), Is.EqualTo("quest-log-steam_7.json"));
            Assert.That(Names.PlayerQuestLog("player"), Is.EqualTo("quest-log-player.json"), "Unity 판은 옛 기본값 \"player\"도 따로 썼다");
        }

        [Test]
        public void AdultCareerSnapshot_LocalPlayer()
        {
            // Assets/Scripts/Game/Careers/AdultCareerSnapshotPersistence.FileNameFor: UTF-16 글자마다 4자리 16진.
            Assert.That(Names.AdultCareerSnapshot("local-player"),
                Is.EqualTo("adult-career-snapshot-006c006f00630061006c002d0070006c0061007900650072.json"));
            Assert.That(Names.AdultCareerSnapshot(""), Is.EqualTo(Names.AdultCareerSnapshot("player")), "빈 id는 player");
            Assert.That(Names.AdultCareerSnapshot("한"), Is.EqualTo("adult-career-snapshot-d55c.json"));
        }

        [Test]
        public void Sanitize_ReplacesEverythingButLettersDigitsDashAndUnderscore()
        {
            // 옮겨 옴: Rootborn.Core.StudentLife.SaveFileNames.Sanitize(합쳐서 없앰)
            Assert.That(Names.Sanitize("steam:42/a b"), Is.EqualTo("steam_42_a_b"));
            Assert.That(Names.Sanitize("local-player_1"), Is.EqualTo("local-player_1"));
            Assert.That(Names.Sanitize("한글"), Is.EqualTo("한글"), "글자는 그대로");
            Assert.That(Names.Sanitize(null), Is.EqualTo(""));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("player")]
        [TestCase("local-player")]
        public void SharedPlayerFiles_DefaultPlayersKeepTheUnityNames(string player)
        {
            Assert.That(Names.Campaign(player), Is.EqualTo("campaign-progress.json"));
            Assert.That(Names.LocationState(player), Is.EqualTo("location-state-progress.json"));
            Assert.That(Names.Milestone(player), Is.EqualTo("milestone-progress.json"));
            Assert.That(Names.Exploration(player), Is.EqualTo("exploration-progress.json"));
            Assert.That(Names.WorldState(player), Is.EqualTo("world-state-progress.json"));
        }

        [Test]
        public void PerPlayerFiles_DefaultPlayersKeepTheUnityNames()
        {
            // 빈 id는 "player"로 본다. "local-player"는 정리해도 그대로라 Unity 판 이름과 같다.
            Assert.That(Names.Encyclopedia(null), Is.EqualTo("encyclopedia-player.json"));
            Assert.That(Names.Encyclopedia("local-player"), Is.EqualTo("encyclopedia-local-player.json"));
            Assert.That(Names.DiscoveryClues(""), Is.EqualTo("discovery-clues-player.json"));
            Assert.That(Names.DiscoveryClues("local-player"), Is.EqualTo("discovery-clues-local-player.json"));
            Assert.That(Names.ClueInterpretations(null), Is.EqualTo("clue-interpretations-player.json"));
            Assert.That(Names.ClueInterpretations("local-player"), Is.EqualTo("clue-interpretations-local-player.json"));
            Assert.That(Names.WorldStateUsage(null), Is.EqualTo("world-state-usage-player.json"));
            Assert.That(Names.WorldStateUsage("local-player"), Is.EqualTo("world-state-usage-local-player.json"));
            Assert.That(Names.WorldStateShared, Is.EqualTo("world-state-progress-shared.json"));
        }

        [Test]
        public void OtherPlayers_AreSanitizedEverywhere()
        {
            Assert.That(Names.Campaign("steam:12 a"), Is.EqualTo("campaign-progress-steam_12_a.json"));
            Assert.That(Names.LocationState("p 2"), Is.EqualTo("location-state-progress-p_2.json"));
            Assert.That(Names.Milestone("p 2"), Is.EqualTo("milestone-progress-p_2.json"));
            Assert.That(Names.WorldState("steam:42/a"), Is.EqualTo("world-state-progress-steam_42_a.json"));
            Assert.That(Names.WorldStateUsage("steam:7"), Is.EqualTo("world-state-usage-steam_7.json"));
            // 아래 넷은 Unity 판이 정리하지 않았다. 새로 쓸 때는 정리한 이름을 쓴다.
            Assert.That(Names.Exploration("steam:7"), Is.EqualTo("exploration-progress-steam_7.json"));
            Assert.That(Names.Encyclopedia("steam:7"), Is.EqualTo("encyclopedia-steam_7.json"));
            Assert.That(Names.DiscoveryClues("steam:7"), Is.EqualTo("discovery-clues-steam_7.json"));
            Assert.That(Names.ClueInterpretations("steam:7"), Is.EqualTo("clue-interpretations-steam_7.json"));
        }

        [Test]
        public void CareerInterest_MatchesTheUnityRule()
        {
            // Assets/Scripts/Game/StudentLife/CareerInterestProgressComponent.cs FileNameFor: "player"는 공용 파일을 쓰지 않는다.
            Assert.That(Names.CareerInterest(null), Is.EqualTo("career-interest-progress.json"));
            Assert.That(Names.CareerInterest("local-player"), Is.EqualTo("career-interest-progress.json"));
            Assert.That(Names.CareerInterest("player"), Is.EqualTo("career-interest-progress-player.json"));
            Assert.That(Names.CareerInterest("steam:7"), Is.EqualTo("career-interest-progress-steam_7.json"));
        }

        [Test]
        public void LegacyNames_AreTheUnsanitizedUnityNames()
        {
            Assert.That(Names.LegacyExploration("steam:7"), Is.EqualTo("exploration-progress-steam:7.json"));
            Assert.That(Names.LegacyExploration("local-player"), Is.EqualTo("exploration-progress.json"));
            Assert.That(Names.LegacyEncyclopedia("steam:7"), Is.EqualTo("encyclopedia-steam:7.json"));
            Assert.That(Names.LegacyEncyclopedia(null), Is.EqualTo("encyclopedia-player.json"));
            Assert.That(Names.LegacyDiscoveryClues("steam:7"), Is.EqualTo("discovery-clues-steam:7.json"));
            Assert.That(Names.LegacyClueInterpretations("steam:7"), Is.EqualTo("clue-interpretations-steam:7.json"));
        }
    }
}
