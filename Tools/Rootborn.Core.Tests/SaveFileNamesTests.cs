using NUnit.Framework;
using Rootborn.Core.Quests;

namespace Rootborn.Core.Tests
{
    // 세이브 파일 이름은 Core.Save.SaveFileNames 한 곳에서 만든다. 값은 Unity 판 파일 이름 그대로다.
    public sealed class SaveFileNamesTests
    {
        [Test]
        public void QuestChain_DefaultPlayer_UsesTheSharedName()
        {
            Assert.That(Save.SaveFileNames.QuestChain(null), Is.EqualTo("quest-chain-progress.json"));
            Assert.That(Save.SaveFileNames.QuestChain(""), Is.EqualTo("quest-chain-progress.json"));
            Assert.That(Save.SaveFileNames.QuestChain("player"), Is.EqualTo("quest-chain-progress.json"));
            Assert.That(Save.SaveFileNames.QuestChain(QuestChainProgressStore.DefaultPlayerId), Is.EqualTo("quest-chain-progress.json"));
            Assert.That(QuestChainProgressStore.DefaultPlayerId, Is.EqualTo("local-player"));
        }

        [Test]
        public void QuestChain_OtherPlayer_MatchesTheOldRule()
        {
            // 옮겨 옴: QuestChainProgressStoreTests.FileNames_DefaultPlayersShareTheLegacyFile_OthersAreHexEncoded
            Assert.That(Save.SaveFileNames.QuestChain("a:"), Is.EqualTo("quest-chain-progress-613a.json"));
            Assert.That(Save.SaveFileNames.QuestChain("한"), Is.EqualTo("quest-chain-progress-ed959c.json"), "every UTF-8 byte is encoded");
        }

        [Test]
        public void QuestLog_IsTheSharedName()
        {
            Assert.That(Save.SaveFileNames.QuestLog, Is.EqualTo("quest-log.json"));
        }

        [Test]
        public void AdultCareerSnapshot_LocalPlayer()
        {
            // Assets/Scripts/Game/Careers/AdultCareerSnapshotPersistence.FileNameFor: UTF-16 글자마다 4자리 16진.
            Assert.That(Save.SaveFileNames.AdultCareerSnapshot("local-player"),
                Is.EqualTo("adult-career-snapshot-006c006f00630061006c002d0070006c0061007900650072.json"));
            Assert.That(Save.SaveFileNames.AdultCareerSnapshot(""), Is.EqualTo(Save.SaveFileNames.AdultCareerSnapshot("player")), "빈 id는 player");
            Assert.That(Save.SaveFileNames.AdultCareerSnapshot("한"), Is.EqualTo("adult-career-snapshot-d55c.json"));
        }
    }
}
