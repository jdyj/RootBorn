// 원래 흐름: Assets/Scripts/UI/StudentLife/CareerInterestRuntimeInstaller.cs(장래희망 화면은 registry.CareerInterests만 보였다),
//            Assets/Scripts/UI/StudentLife/CareerInterestPanel.cs SelectAndShow·ApplyLinkedQuests(고르면 이어진 퀘스트를 받는다).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Data;
using Rootborn.Data.Builders;

namespace Rootborn.Game.Tests
{
    /// <summary>장래희망 목록과 장래희망에 이어진 퀘스트. 자동 테스트다.</summary>
    public sealed class CareerInterestChoiceTests
    {
        [Test]
        public void CareerInterestChoices_AreTheRegisteredInterestsInRegistryOrder()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var registered = TestWorld.Definitions.RegistryIds("careerInterests");
            Assert.That(registered, Is.Not.Empty);
            Assert.That(TestWorld.Definitions.All<CareerInterest>().Count, Is.GreaterThan(registered.Count), "등록부에 없는 장래희망 정의도 데이터에 있다");

            Assert.That(game.CareerInterestChoices().Select(c => c.Interest.Id), Is.EqualTo(registered), "Unity 판처럼 등록부의 것만, 등록부 순서로");
        }

        [Test]
        public void ChoosingADream_AcceptsItsLinkedQuests_OnceAndSaved()
        {
            var data = CopyDataWithLinkedQuest("interest.chef", "quest.gatherWood");
            try
            {
                var loaded = GameDataLoader.Load(data, RootbornBuilders.Register);
                loaded.ThrowIfFailed();
                var definitions = loaded.Definitions;
                var settings = GameStartSettings.FromDefinitions(definitions);
                using var saves = new TempSaves();
                GameSession Start() => GameSession.Start(definitions, settings, saves.Store, TestWorld.Slot, TestWorld.Player);

                GameSession game = Start();
                game = MainChainTests.PlayToMyDream(game, () => game, new List<string>());
                Assert.That(game.PerformLocationActivity(game.LocationActivitiesAt("location.restaurant")[0].Activity.Id, "intro:restaurant").Kind,
                    Is.EqualTo(LifeActivityResultKind.Applied));
                Assert.That(game.QuestLog.GetState("quest.gatherWood"), Is.EqualTo(QuestStage.NotStarted));

                Assert.That(game.SelectCareerInterest("interest.chef", "interest:chef").Applied, Is.True);
                Assert.That(game.QuestLog.GetState("quest.gatherWood"), Is.EqualTo(QuestStage.Active), "장래희망에 이어진 퀘스트를 받는다");
                Assert.That(Start().QuestLog.GetState("quest.gatherWood"), Is.EqualTo(QuestStage.Active), "저장된다");

                Assert.That(game.AcceptCareerInterestQuests(), Is.EqualTo(0), "이미 받은 퀘스트는 다시 받지 않는다");
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(data), true);
            }
        }

        [Test]
        public void AcceptCareerInterestQuests_WithoutADream_DoesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int changes = 0;
            game.Changed += _ => changes++;
            Assert.That(game.AcceptCareerInterestQuests(), Is.EqualTo(0));
            Assert.That(changes, Is.EqualTo(0));
        }

        // 지금 데이터의 장래희망에는 이어진 퀘스트가 없다. data/를 임시 폴더로 복사해 한 장래희망에 퀘스트 하나를 이어 둔다.
        // data 옆의 game/ 설정 파일(time.json, economy.json)도 같은 자리에 복사한다(정의 로더가 data 폴더 옆에서 읽는다).
        private static string CopyDataWithLinkedQuest(string interestId, string questId)
        {
            var root = Path.Combine(Path.GetTempPath(), "rootborn-game-tests", System.Guid.NewGuid().ToString("N"));
            var target = Path.Combine(root, "data");
            var gameSide = Path.Combine(TestWorld.RepoRoot, "game");
            Directory.CreateDirectory(Path.Combine(root, "game"));
            foreach (var name in new[] { "time.json", "economy.json" })
                File.Copy(Path.Combine(gameSide, name), Path.Combine(root, "game", name));
            foreach (var file in Directory.GetFiles(TestWorld.DataDirectory, "*", SearchOption.AllDirectories))
            {
                var copy = Path.Combine(target, Path.GetRelativePath(TestWorld.DataDirectory, file));
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                File.Copy(file, copy);
            }

            var path = Path.Combine(target, "careers", "CareerInterestDefinition.json");
            var text = File.ReadAllText(path);
            int at = text.IndexOf("\"id\": \"" + interestId + "\"", System.StringComparison.Ordinal);
            int field = text.IndexOf("\"linkedQuests\": []", at, System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0));
            Assert.That(field, Is.GreaterThan(at));
            text = text.Substring(0, field) + "\"linkedQuests\": [{ \"ref\": \"" + questId + "\", \"type\": \"QuestDefinition\" }]"
                + text.Substring(field + "\"linkedQuests\": []".Length);
            File.WriteAllText(path, text);
            return target;
        }
    }
}
