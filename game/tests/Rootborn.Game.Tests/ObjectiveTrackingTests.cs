using NUnit.Framework;
using Rootborn.Core.Objectives;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>목표 수첩의 추적 지정을 GameSession이 읽고 바꾼다(Unity objective-journal-tracking.json). 자동 테스트다.</summary>
    public sealed class ObjectiveTrackingTests
    {
        [Test]
        public void ANewGame_TracksNothing_AndWritesNoTrackingFile()
        {
            // Unity 판: 파일이 없으면 추적 HUD는 아무것도 보이지 않는다. 플레이어가 추적을 고를 때만 쓴다.
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.ObjectiveTracking.IsTracking, Is.False);

            game.TalkTo("npc.first-guide"); // 다른 명령의 저장
            Assert.That(saves.Store.TryRead<ObjectiveJournalTrackingSaveData>(TestWorld.Slot, SaveFileNames.ObjectiveJournalTracking, out _), Is.False);
        }

        [Test]
        public void TrackObjective_IsSaved_AndReadOnTheNextStart()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);

            Assert.That(game.TrackObjective("quest-chains", "quest-chain:chain.main"), Is.True);
            Assert.That(game.ObjectiveTracking.CategoryId, Is.EqualTo("quest-chains"));
            Assert.That(game.ObjectiveTracking.StableKey, Is.EqualTo("quest-chain:chain.main"));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.ObjectiveTracking.StableKey, Is.EqualTo("quest-chain:chain.main"));
            Assert.That(TestWorld.Start(saves.Store, "slot-other").ObjectiveTracking.IsTracking, Is.False, "슬롯마다 따로");
        }

        [Test]
        public void TrackObjective_WithoutAKey_ChangesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.TrackObjective("quest-chains", "quest-chain:chain.main");

            Assert.That(game.TrackObjective("other", ""), Is.False);
            Assert.That(game.TrackObjective("other", null), Is.False);
            Assert.That(game.ObjectiveTracking.StableKey, Is.EqualTo("quest-chain:chain.main"));
            Assert.That(TestWorld.Start(saves.Store).ObjectiveTracking.StableKey, Is.EqualTo("quest-chain:chain.main"));
        }
    }
}
