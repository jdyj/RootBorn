using System.Collections.Generic;
using NUnit.Framework;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// GameSession이 적용된 명령 뒤에 "무엇이 바뀌었는지"를 알린다(화면이 명령을 감싸지 않아도 된다).
    /// 적용되지 않은 명령은 알리지 않는다. 한 명령은 같은 주제를 한 번만 알린다. 자동 테스트다.
    /// </summary>
    public sealed class ChangeNotificationTests
    {
        private static List<string> Listen(GameSession game)
        {
            var heard = new List<string>();
            game.Changed += topic => heard.Add(topic);
            return heard;
        }

        [Test]
        public void TopicNames_MatchTheScreenTopicNames()
        {
            // Rootborn.Screens.ChangeTopics가 이 이름을 그대로 쓴다(화면 쌓기는 GameSession.Changed를 받아 열린 화면에 넘긴다).
            Assert.That(GameChangeTopics.Student, Is.EqualTo("student"));
            Assert.That(GameChangeTopics.Inventory, Is.EqualTo("inventory"));
            Assert.That(GameChangeTopics.Quests, Is.EqualTo("quests"));
            Assert.That(GameChangeTopics.Dialogue, Is.EqualTo("dialogue"));
            Assert.That(GameChangeTopics.Day, Is.EqualTo("day"));
            Assert.That(GameChangeTopics.Career, Is.EqualTo("career"));
            Assert.That(GameChangeTopics.World, Is.EqualTo("world"));
        }

        [Test]
        public void TalkAndChoose_TellDialogueQuestsAndStudent()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var heard = Listen(game);

            game.TalkTo("npc.first-guide");
            Assert.That(heard, Is.SupersetOf(new[] { GameChangeTopics.Dialogue, GameChangeTopics.Quests, GameChangeTopics.Student }));
            Assert.That(heard, Is.Unique);

            heard.Clear();
            game.ChooseDialogueOption(0);
            Assert.That(heard, Does.Contain(GameChangeTopics.Quests));

            heard.Clear();
            Assert.That(game.ChooseDialogueOption(0), Is.False, "이미 받은 퀘스트");
            Assert.That(heard, Is.Empty, "적용되지 않은 명령은 알리지 않는다");

            game.CloseDialogue();
            Assert.That(heard, Is.EqualTo(new[] { GameChangeTopics.Dialogue }));
            heard.Clear();
            game.CloseDialogue();
            Assert.That(heard, Is.Empty, "닫힌 대화를 다시 닫으면 바뀐 것이 없다");
        }

        [Test]
        public void Activities_Days_DailyEventsAndTracking_TellTheirTopics()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var heard = Listen(game);

            game.PerformLocationActivity("location.activity.square-help-request", "square:help:1");
            Assert.That(heard, Does.Contain(GameChangeTopics.Student));
            heard.Clear();
            game.PerformLocationActivity("location.activity.square-help-request", "square:help:1");
            Assert.That(heard, Is.Empty, "같은 요청은 다시 적용되지 않는다");

            game.EndDay();
            Assert.That(heard, Is.SupersetOf(new[] { GameChangeTopics.Day, GameChangeTopics.Student }));
            heard.Clear();
            game.StartNextDay();
            Assert.That(heard, Is.SupersetOf(new[] { GameChangeTopics.Day, GameChangeTopics.Student }));

            heard.Clear();
            game.DeferDailyEvent("daily-event.library-hard-book", out _);
            Assert.That(heard, Is.EqualTo(new[] { GameChangeTopics.DailyEvents }));

            heard.Clear();
            game.TrackObjective("quest-chains", "quest-chain:x");
            Assert.That(heard, Is.EqualTo(new[] { GameChangeTopics.Objectives }));
            heard.Clear();
            game.TrackObjective("quest-chains", "");
            Assert.That(heard, Is.Empty);
        }

        [Test]
        public void AListenerThatThrows_FindsTheCommandAlreadySaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.Changed += _ => throw new System.InvalidOperationException("화면 오류");
            Assert.Throws<System.InvalidOperationException>(() => game.TalkTo("npc.first-guide"));
            Assert.That(new Rootborn.Core.StudentLife.LocationVisitProgress(TestWorld.Start(saves.Store).Student).HasMet("npc.first-guide"), Is.True, "알림 전에 이미 저장했다");
        }
    }
}
