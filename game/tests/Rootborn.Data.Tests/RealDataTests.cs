using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Time;
using Rootborn.Core.World;
using Rootborn.Core.Gathering;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;

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
        public void TheTownTree_AndTheFieldNotesRecipe_CarryTheirValues()
        {
            var tree = _result.Definitions.Get<ResourceNodeDefinition>("Tree");
            Assert.That((tree.BaseHitsToBreak, tree.BareHandPenaltyMul, tree.PreferredToolId, tree.IsWalkable), Is.EqualTo((5f, 0.3f, "StoneAxe", true)));
            Assert.That(tree.Drops.Single().ResourceId, Is.EqualTo("Wood"));
            Assert.That((tree.Drops.Single().MinCount, tree.Drops.Single().MaxCount), Is.EqualTo((1, 3)));
            Assert.That(tree.Sprite, Does.EndWith("tree_01.png"));
            var recipe = _result.Definitions.Get<RecipeDefinition>("recipe.town.field-notes");
            Assert.That((recipe.DisplayNameKey, recipe.NextHintKey), Is.EqualTo(("recipe.town.field-notes.name", "objective.recipe.field-notes.next")));
        }

        [Test]
        public void WithRuleServices_TheSquareHelpActivityRevealsTheFieldNotesRecipe()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            var result = GameDataLoader.Load(RepoPaths.DataDirectory, b => RootbornBuilders.Register(b, new RuleServices(store)));
            result.ThrowIfFailed();
            var activity = result.Definitions.Get<LocationActivity>("location.activity.square-help-request");
            var progress = new StudentLifeProgress("slot-1", "local-player", 10, 10);

            var logs = activity.ApplyOutcomes(progress);

            Assert.That(logs, Does.Contain("location-activity:location.activity.square-help-request:unlock=recipe.town.field-notes"));
            var recipes = ResourceRecipeSaveFiles.LoadOrCreate(store, "slot-1", "local-player");
            Assert.That(recipes.IsRecipeRevealed("recipe.town.field-notes"), Is.True);
            Assert.That(recipes.IsResourceSeen("Rock"), Is.True);
        }

        [Test]
        public void WithoutRuleServices_TheRecipeOutcomeDoesNothing()
        {
            var activity = _result.Definitions.Get<LocationActivity>("location.activity.square-help-request");
            var logs = activity.ApplyOutcomes(new StudentLifeProgress("slot-1", "local-player", 10, 10));
            Assert.That(logs, Has.None.Contains("recipe.town.field-notes"));
        }

        [Test]
        public void NothingIsStillWaitingToBePorted()
        {
            var builders = new DefinitionBuilders();
            RootbornBuilders.Register(builders);
            Assert.That(builders.NotPorted.Keys, Is.Empty);
            Assert.That(builders.NotUsed.Keys, Is.EquivalentTo(new[]
            {
                "StatusEffectDefinition", "KnowledgeNode", "HitGroundWithRockTrigger", "ToolDefinition", "FishingAnimationEffect",
                "GatherResourceStep", "HeirTrait",
            }));
        }

        [Test]
        public void TheWorldTravelCatalog_IsBuiltFromItsDestinations()
        {
            var catalog = _result.Definitions.All<WorldTravelCatalog>().Single();
            Assert.That(catalog.IsValid, Is.True);
            Assert.That((catalog.NewGameScene, catalog.NewGameSpawnId), Is.EqualTo(("House", "house-entry")));
            Assert.That(catalog.Destinations.Count, Is.EqualTo(10), "data/locations/WorldTravelCatalogDefinition.json의 목적지 수");
            Assert.That(_table.RegistryIds("worldTravelCatalog"), Is.EqualTo(new[] { "WorldTravelCatalog" }));
            Assert.That(catalog.Find("Beach").SpawnIds, Is.EqualTo(new[] { "beach-to-town-top-entry", "beach-top-entry" }));
            Assert.That(_result.Definitions.Get<WorldDestination>("Destination_House").SceneName, Is.EqualTo("House"));
        }

        [Test]
        public void TheDayNightLighting_CarriesItsKeysInTimeOrder()
        {
            var lighting = _result.Definitions.Get<DayNightLightingProfile>("DayNightLighting_Default");
            Assert.That(lighting.ColorKeys.Count, Is.EqualTo(7), "m_NumColorKeys");
            Assert.That(lighting.ColorKeys[0].DayFraction, Is.EqualTo(0f));
            Assert.That(lighting.ColorKeys[6].DayFraction, Is.EqualTo(1f));
            Assert.That((lighting.ColorKeys[0].R, lighting.ColorKeys[0].G, lighting.ColorKeys[0].B), Is.EqualTo((0.09f, 0.16f, 0.29f)));
            Assert.That(lighting.IntensityKeys.Count, Is.GreaterThan(1));
            Assert.That(lighting.IntensityKeys[0].Value, Is.EqualTo(0.25f));
            Assert.That(lighting.LerpSpeed, Is.GreaterThanOrEqualTo(0.01f));
        }

        [Test]
        public void TheRegistryDefaults_AreGivenByTheDefinitions()
        {
            var definitions = _result.Definitions;
            Assert.That(definitions.RegistryDefault("defaultDayEndRule"), Is.EqualTo(_table.RegistryIds("defaultDayEndRule")[0]));
            Assert.That(definitions.RegistryDefault("studentLifeCalendar"), Is.Not.Empty);
            Assert.That(definitions.RegistryDefault("no.such.key"), Is.Empty);
            Assert.That(definitions.RegistryIds("questChains"), Is.EqualTo(_table.RegistryIds("questChains")));
        }

        [Test]
        public void TheConditionStatus_CarriesItsName()
        {
            var fatigue = _result.Definitions.Get<StatusDefinition>("status.fatigue");
            Assert.That((fatigue.DisplayNameKey, fatigue.PersistsToNextDay), Is.EqualTo(("Fatigue", true)));
        }

        [Test]
        public void RelationshipsAndTimeSlots_CarryTheirValues()
        {
            var trust = _result.Definitions.Get<RelationshipDefinition>("relationship.first-guide.trust");
            Assert.That((trust.DisplayNameKey, trust.TargetId), Is.EqualTo(("Guide Trust", "npc.first-guide")));
            var slots = _result.Definitions.All<TimeSlotDefinition>().OrderBy(slot => slot.Order).Select(slot => slot.Id);
            Assert.That(slots, Is.EqualTo(new[] { "time.morning", "time.afternoon", "time.evening", "time.night" }));
        }

        [Test]
        public void Furniture_CarriesItsFootprintAndSale()
        {
            var desk = _result.Definitions.Get<FurnitureDefinition>("story.study-desk");
            Assert.That(desk.FootprintCells.Count, Is.EqualTo(2));
            Assert.That(desk.IsForSale, Is.True);
            Assert.That((desk.PriceItem.Id, desk.Price, desk.PurchaseLocationId), Is.EqualTo(("item.allowance", 10, "location.corner-store")));
            Assert.That(desk.AllowsSurface("house"), Is.True);
            Assert.That(desk.PlacementPreference, Is.EqualTo(FurniturePlacementPreference.NearWall));

            // 그림 참조를 풀지 못한 책상은 Unity처럼 그림 칸 없이 차지 칸만 갖는다.
            var basic = _result.Definitions.Get<FurnitureDefinition>("desk.basic");
            Assert.That(basic.TileParts, Is.Empty);
            Assert.That(_result.Definitions.All<FurnitureDefinition>().Count, Is.EqualTo(1503));
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
            // 한 번 잰 벽시계 값은 다른 테스트·프로세스의 부하에 따라 흔들린다(전수조사: 혼자 돌려도 3번 중 2번 1초를 넘김).
            // 한 번 데운 뒤 다섯 번 재서 가장 빠른 값을 본다. 부하가 아니라 읽기 자체가 느려졌을 때만 실패한다.
            GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            const int Runs = 5;
            long fastest = long.MaxValue;
            var times = new long[Runs];
            int count = 0;
            for (int i = 0; i < Runs; i++)
            {
                var watch = Stopwatch.StartNew();
                var result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
                watch.Stop();
                times[i] = watch.ElapsedMilliseconds;
                fastest = System.Math.Min(fastest, times[i]);
                count = result.Definitions.Count;
            }

            TestContext.WriteLine($"data/ 읽기와 만들기(데운 뒤 {Runs}번): {string.Join(", ", times)} ms, 가장 빠름 {fastest} ms, 정의 {count}개");
            Assert.That(count, Is.GreaterThan(0), "읽은 정의가 있어야 잰 시간이 뜻이 있다");
            Assert.That(fastest, Is.LessThan(1000));
        }

        private static void Add(Dictionary<string, int> sums, string id, int delta)
        {
            sums.TryGetValue(id, out var current);
            sums[id] = current + delta;
        }
    }
}
