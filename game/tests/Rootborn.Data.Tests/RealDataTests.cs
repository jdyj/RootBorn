using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>실제 data/ 전체를 RootbornBuilders로 읽는다. 테스트가 엔티티 id를 쓰는 것은 허용된다.</summary>
    public sealed class RealDataTests
    {
        private LoadResult _result;
        private EntityTable _table;

        [OneTimeSetUp]
        public void LoadOnce()
        {
            _result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            _table = EntityTable.ReadDirectory(RepoPaths.DataDirectory, new List<LoadError>());
        }

        [Test]
        public void EveryTypeInData_IsClassified()
        {
            Assert.That(_result.UnclassifiedTypes, Is.Empty, "분류 안 된 종류: " + string.Join(", ", _result.UnclassifiedTypes));
        }

        [Test]
        public void RealData_BuildsWithoutErrors()
        {
            Assert.That(_result.Errors, Is.Empty,
                $"오류 {_result.Errors.Count}건:\n" + string.Join("\n", _result.Errors.Take(20)));
        }

        [Test]
        public void Quests_MatchTheRegistry()
        {
            // registry.json "quests"에는 6개만 있다. 발견 단서 폴더의 퀘스트 2개는 등록되지 않아 뒤에 파일 순서로 온다.
            var expected = _table.RegistryIds("quests")
                .Concat(new[] { "Quest_DiscoveryClue_LibraryFollowUp", "quest.discovery.play-followup" })
                .ToArray();

            Assert.That(_result.Definitions.All<QuestDefinition>().Select(q => q.Id), Is.EqualTo(expected));
        }

        [Test]
        public void AQuest_CarriesItsObjectives()
        {
            var quest = _result.Definitions.Get<QuestDefinition>("quest.career.chef");

            var objective = quest.Objectives.Single();
            Assert.That(objective, Is.InstanceOf<TalkQuestObjective>());
            var talk = (TalkQuestObjective)objective;
            Assert.That(talk.TargetNpcId, Is.EqualTo("npc.first-guide"));
            Assert.That(talk.RequiredCount, Is.EqualTo(1));
            Assert.That(quest.CompletionEffects.Count, Is.EqualTo(1));
        }

        [Test]
        public void AnActivity_CarriesItsCostsAndEffects()
        {
            var activity = _result.Definitions.Get<LifeActivity>("practice.chef");

            Assert.That(activity.TimeCostMinutes, Is.EqualTo(45));
            Assert.That(activity.EnergyCost, Is.EqualTo(1));
            Assert.That(activity.FocusCost, Is.EqualTo(1));
            Assert.That(activity.Category, Is.EqualTo(LifeActivityCategory.School));
            Assert.That(activity.TimePolicy.Id, Is.EqualTo("paid-activity"));

            Assert.That(_table.TryGet("LifeActivityDefinition", "practice.chef", out var node), Is.True);
            var effectRefs = node.Json.GetProperty("effects").EnumerateArray().ToList();
            var effects = (ILifeActivityEffect[])typeof(LifeActivity)
                .GetField("_effects", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(activity);
            Assert.That(effects.Length, Is.EqualTo(effectRefs.Count));

            // 효과를 실제로 적용해 JSON에 적힌 특성·기술 변화와 진로 힌트가 모두 나는지 본다.
            var expectedTraits = new Dictionary<string, int>();
            var expectedSkills = new Dictionary<string, int>();
            var expectedHints = new List<string>();
            foreach (var reference in effectRefs)
            {
                Assert.That(_table.TryGet(reference.GetProperty("type").GetString(), reference.GetProperty("ref").GetString(), out var effect), Is.True);
                var json = effect.Json;
                if (json.TryGetProperty("trait", out var trait))
                    Add(expectedTraits, trait.GetProperty("ref").GetString(), json.GetProperty("delta").GetInt32());
                else if (json.TryGetProperty("skill", out var skill))
                    Add(expectedSkills, skill.GetProperty("ref").GetString(), json.GetProperty("delta").GetInt32());
                else
                    expectedHints.Add(json.GetProperty("career").GetProperty("ref").GetString());
            }

            var progress = new StudentLifeProgress("test", "player", 10, 10);
            activity.ApplyEffects(progress);

            foreach (var pair in expectedTraits) Assert.That(progress.GetTraitValue(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            foreach (var pair in expectedSkills) Assert.That(progress.GetSkillValue(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            foreach (var career in expectedHints) Assert.That(progress.IsCareerHintUnlocked(career), Is.True, career);
            Assert.That(expectedTraits.Count + expectedSkills.Count + expectedHints.Count, Is.GreaterThan(0));
        }

        [Test]
        public void LoadingRealData_TakesUnderOneSecond()
        {
            GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            var watch = Stopwatch.StartNew();
            var result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            watch.Stop();

            TestContext.WriteLine($"data/ 읽기와 만들기(두 번째 호출): {watch.ElapsedMilliseconds} ms, 정의 {result.Definitions.Count}개");
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(1000));
        }

        private static void Add(Dictionary<string, int> sums, string id, int delta)
        {
            sums.TryGetValue(id, out var current);
            sums[id] = current + delta;
        }
    }
}
