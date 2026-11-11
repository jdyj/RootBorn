using System.Collections.Generic;
using NUnit.Framework;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 도감 본 표시 명령(GameSession.MarkEncyclopediaSeen)의 변경 알림: 적용되면 저장 뒤 발견 주제로 한 번, 적용되지 않으면 알리지 않는다.
    /// 화면(도감)이 이 명령만 부르므로 알림이 화면 갱신의 근거다. 저장 뒤 다시 읽기는 DiscoveryGatherAndSeenTests가 본다. 자동 테스트.
    /// </summary>
    public sealed class EncyclopediaSeenNoticeTests
    {
        private const string Discovery = "discovery.clocktower-notice";
        private const string Entry = "ency.location.first-discovery";

        [Test]
        public void Applied_AnnouncesDiscoveryOnce_AfterSaving_NotAppliedSaysNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var topics = new List<string>();
            bool savedWhenAnnounced = false;
            game.Changed += topic =>
            {
                topics.Add(topic);
                savedWhenAnnounced = !TestWorld.Start(saves.Store).Encyclopedia.IsNew(Entry);
            };

            Assert.That(game.MarkEncyclopediaSeen(Entry), Is.False);
            Assert.That(topics, Is.Empty, "열리지 않은 항목은 알리지 않는다");

            game.Discover(Discovery);
            topics.Clear();
            Assert.That(game.MarkEncyclopediaSeen(Entry), Is.True);
            Assert.That(topics, Is.EqualTo(new[] { GameChangeTopics.Discovery }));
            Assert.That(savedWhenAnnounced, Is.True, "알릴 때는 이미 저장되어 있다");

            topics.Clear();
            Assert.That(game.MarkEncyclopediaSeen(Entry), Is.False);
            Assert.That(topics, Is.Empty, "이미 본 항목은 알리지 않는다");
            Assert.That(game.MarkEncyclopediaSeen("ency.none"), Is.False);
            Assert.That(topics, Is.Empty);
        }
    }
}
