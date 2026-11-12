using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Names = Rootborn.Core.Save.SaveFileNames;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 일터 데이터(data/workspaces): 새 종류가 등록돼 실제 데이터가 오류 없이 읽히고, 잘못된 일터·규칙·템플릿은 읽기 오류다(자동 테스트).
    /// 수치는 시험용이라 값 자체가 아니라 연결과 올바름만 본다.
    /// </summary>
    public sealed class WorkspaceDataTests
    {
        private const string ChefWorkspace = "workspace.chef";

        private static LoadResult LoadReal() => GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);

        [Test]
        public void RealData_LoadsWithoutErrors_WithWorkspaces()
        {
            var result = LoadReal();
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(result.UnclassifiedTypes, Is.Empty);
            Assert.That(result.Ok, Is.True);
            Assert.That(result.Definitions.All<Workspace>(), Is.Not.Empty, "일터가 하나 이상 있다");
            Assert.That(result.Definitions.FurnitureQuality, Is.Not.Null);
            Assert.That(result.Definitions.WorkLinks, Is.Not.Null);
        }

        [Test]
        public void DuplicateWorkspaceId_IsError()
        {
            using var dir = new TempDir();
            dir.Write("data/a/WorkspaceDefinition.json", Workspace(ChefWorkspace, "[6, 10, 14]"));
            dir.Write("data/b/WorkspaceDefinition.json", Workspace(ChefWorkspace, "[6, 10, 14]"));

            var result = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);

            Assert.That(result.Errors.Any(e => e.Kind == "duplicate" && e.Type == "WorkspaceDefinition" && e.Id == ChefWorkspace), Is.True,
                string.Join("\n", result.Errors));
        }

        [Test]
        public void InvalidWorkspace_IsLoadError()
        {
            using var good = new TempDir();
            good.Write("data/a/WorkspaceDefinition.json", Workspace("workspace.x", "[6, 10, 14]"));
            var control = GameDataLoader.Load(Path.Combine(good.Path, "data"), RootbornBuilders.Register);
            Assert.That(control.Errors.Any(e => e.Kind == "constructor" && e.Type == "WorkspaceDefinition"), Is.False, "한도 3개면 일터 자체는 올바르다");

            using var bad = new TempDir();
            bad.Write("data/a/WorkspaceDefinition.json", Workspace("workspace.x", "[6, 10]"));
            var result = GameDataLoader.Load(Path.Combine(bad.Path, "data"), RootbornBuilders.Register);

            Assert.That(result.Errors.Any(e => e.Kind == "constructor" && e.Type == "WorkspaceDefinition" && e.Id == "workspace.x"), Is.True,
                string.Join("\n", result.Errors));
            Assert.That(result.Definitions.TryGet<Workspace>("workspace.x", out _), Is.False, "올바르지 않은 일터는 정의에 오르지 않는다");
        }

        [Test]
        public void RealData_ChefWorkspace_IsValidAndLinked()
        {
            var result = LoadReal();
            result.ThrowIfFailed();
            var definitions = result.Definitions;

            var workspace = definitions.Get<Workspace>(ChefWorkspace);
            Assert.That(workspace.IsValid, Is.True);
            Assert.That(workspace.FacilityId, Is.EqualTo("facility.chef"));

            var extras = definitions.WorkLinks.ExtrasFor("work.chef.level1", SpaceScores.Empty);
            Assert.That(extras.SkillGains.Count, Is.EqualTo(1), "일을 하면 기술 하나가 자란다");
            Assert.That(extras.SkillGains[0].Amount, Is.GreaterThan(0));

            Assert.That(definitions.TryGet<WorkspaceTemplate>(workspace.DefaultTemplateId, out var template), Is.True, "기본 템플릿이 있다");
            Assert.That(template.IsValid, Is.True);
            Assert.That(template.WorkspaceId, Is.EqualTo(workspace.Id));
            Assert.That(template.Costs, Is.Empty, "기본 템플릿은 값이 없다");
        }

        [Test]
        public void EveryWorkspaceTemplate_FurnitureExists()
        {
            var result = LoadReal();
            result.ThrowIfFailed();
            var definitions = result.Definitions;
            var furniture = definitions.All<FurnitureDefinition>().ToDictionary(f => f.StableId);
            var templates = definitions.All<WorkspaceTemplate>();
            Assert.That(templates, Is.Not.Empty);

            foreach (var template in templates)
            {
                var workspace = definitions.Get<Workspace>(template.WorkspaceId);
                foreach (var id in template.FurnitureIds)
                {
                    Assert.That(furniture.TryGetValue(id, out var f), Is.True, template.Id + ": 모르는 가구 " + id);
                    Assert.That(workspace.AcceptsSurfaceIds.Any(f.AllowsSurface), Is.True, template.Id + ": " + id + "가 일터가 받는 면에 놓이지 않는다");
                    Assert.That(f.UnlockToken, Is.Empty, template.Id + ": " + id + "는 잠긴 가구다");
                }

                Assert.That(template.Placements.Count, Is.LessThanOrEqualTo(workspace.Capacity(1, null)), template.Id + ": 레벨 1 한도 안");
                Assert.That(template.Placements.Select(p => p.Anchor), Is.Unique, template.Id + ": 칸이 겹치지 않는다");
            }
        }

        [Test]
        public void QualityRule_UnknownFurniture_IsLoadError()
        {
            using var dir = new TempDir();
            dir.Write("data/a/SpaceQualityDefinition.json", @"[{ ""type"": ""SpaceQualityDefinition"", ""id"": ""quality.x"", ""displayNameKey"": ""quality.x.name"", ""iconKey"": """" }]");
            dir.Write("data/a/FurnitureQualityRule.json", @"[{ ""type"": ""FurnitureQualityRule"", ""id"": ""quality-rule.x"",
                ""furniture"": ""furniture.nope"", ""quality"": { ""ref"": ""quality.x"", ""type"": ""SpaceQualityDefinition"" }, ""score"": 2 }]");

            var result = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);

            Assert.That(result.Errors.Any(e => e.Message.Contains("furniture.nope")), Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void QualityRule_CategoryWithoutFurniture_IsLoadError()
        {
            using var dir = new TempDir();
            dir.Write("data/a/SpaceQualityDefinition.json", @"[{ ""type"": ""SpaceQualityDefinition"", ""id"": ""quality.x"", ""displayNameKey"": ""quality.x.name"", ""iconKey"": """" }]");
            dir.Write("data/a/FurnitureQualityRule.json", @"[{ ""type"": ""FurnitureQualityRule"", ""id"": ""quality-rule.x"",
                ""category"": ""NoSuchCategory"", ""quality"": { ""ref"": ""quality.x"", ""type"": ""SpaceQualityDefinition"" }, ""score"": 2 }]");

            var result = Load(dir);

            Assert.That(result.Errors.Any(e => e.Type == "FurnitureQualityRule" && e.Message.Contains("NoSuchCategory")), Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Ok, Is.False);
        }

        [Test]
        public void TwoWorkspacesOnTheSameSurface_IsLoadError()
        {
            using var dir = new TempDir();
            var a = Workspace("workspace.a", "[6, 10, 14]", facility: "facility.a", surface: "surface.same");
            var b = Workspace("workspace.b", "[6, 10, 14]", facility: "facility.b", surface: "surface.same");
            dir.Write("data/a/WorkspaceDefinition.json", a.TrimEnd(']') + "," + b.TrimStart('['));
            var result = Load(dir);

            Assert.That(result.Errors.Any(e => e.Kind == "invalid-field" && e.Type == "WorkspaceDefinition" && e.Id == "workspace.b" && e.Message.Contains("surface.same")),
                Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Errors.Any(e => e.Kind == "invalid-field" && e.Id == "workspace.a"), Is.False, "처음 것은 오류가 아니다");
        }

        [Test]
        public void TwoWorkspacesForTheSameFacility_IsLoadError()
        {
            using var dir = new TempDir();
            var a = Workspace("workspace.a", "[6, 10, 14]", facility: "facility.same", surface: "surface.a");
            var b = Workspace("workspace.b", "[6, 10, 14]", facility: "facility.same", surface: "surface.b");
            dir.Write("data/a/WorkspaceDefinition.json", a.TrimEnd(']') + "," + b.TrimStart('['));
            var result = Load(dir);

            Assert.That(result.Errors.Any(e => e.Kind == "invalid-field" && e.Type == "WorkspaceDefinition" && e.Id == "workspace.b" && e.Message.Contains("facility.same")),
                Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Errors.Any(e => e.Kind == "invalid-field" && e.Id == "workspace.a"), Is.False, "처음 것은 오류가 아니다");
        }

        [Test]
        public void DefinitionsBuiltWithoutWorkspaceTables_HaveEmptyTables()
        {
            using var dir = new TempDir();
            var errors = new System.Collections.Generic.List<LoadError>();
            var definitions = new GameDefinitions(new System.Collections.Generic.List<GameDefinitions.Entry>(), EntityTable.ReadDirectory(dir.Path, errors));

            Assert.That(definitions.FurnitureQuality, Is.Not.Null);
            Assert.That(definitions.FurnitureQuality.ScoresOf("anything"), Is.Empty);
            Assert.That(definitions.FurnitureQuality.Problems, Is.Empty);
            Assert.That(definitions.WorkLinks, Is.Not.Null);
            Assert.That(definitions.WorkLinks.ExtrasFor("work.x", SpaceScores.Empty).SkillGains, Is.Empty);
        }

        [Test]
        public void Template_UnknownFurniture_IsLoadError()
        {
            using var dir = new TempDir();
            dir.Write("data/a/WorkspaceTemplate.json", @"[{ ""type"": ""WorkspaceTemplate"", ""id"": ""template.x"",
                ""workspace"": { ""ref"": ""workspace.x"", ""type"": ""WorkspaceDefinition"" }, ""displayNameKey"": ""template.x.name"",
                ""costItems"": [], ""costCounts"": [],
                ""placements"": [ { ""furniture"": ""furniture.nope"", ""cell"": { ""x"": 1, ""y"": 1 }, ""direction"": 2 } ] }]");

            var result = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);

            Assert.That(result.Errors.Any(e => e.Type == "WorkspaceTemplate" && e.Message.Contains("furniture.nope")), Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Errors.Any(e => e.Type == "WorkspaceTemplate" && e.Kind == "missing-ref"), Is.True, "없는 일터를 가리킨다");
        }

        [Test]
        public void EveryWorkspaceType_IsRegistered()
        {
            var result = LoadReal();
            var types = Directory.GetFiles(Path.Combine(RepoPaths.DataDirectory, "workspaces"), "*.json")
                .SelectMany(file =>
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(file));
                    return document.RootElement.EnumerateArray().Select(e => e.GetProperty("type").GetString()).ToArray();
                })
                .Distinct()
                .ToArray();

            Assert.That(types, Is.Not.Empty);
            Assert.That(types.Where(t => !result.Builders.IsClassified(t)), Is.Empty);
        }

        [Test]
        public void WorkspaceProgress_RoundTripsThroughTheFileStore()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            var progress = WorkspaceSaveFiles.LoadOrCreate(store, "slot-0", Names.DefaultPlayerId);
            Assert.That(progress.GrowthLeftToday(3, 3), Is.EqualTo(3));
            WorkspaceSaveFiles.Save(store, progress);

            Assert.That(File.Exists(Path.Combine(dir.Path, "slot-0", Names.WorkspaceProgress(Names.DefaultPlayerId))), Is.True);
            Assert.That(store.TryRead("slot-0", Names.WorkspaceProgress(Names.DefaultPlayerId), out WorkspaceProgressSaveData written), Is.True);
            Assert.That(written.Version, Is.EqualTo(WorkspaceProgressSaveData.KnownVersion));
            Assert.That(SaveShapeCoverage.ShapeFor(Names.WorkspaceProgress(Names.DefaultPlayerId)), Is.EqualTo(typeof(WorkspaceProgressSaveData)));
            Assert.That(SaveShapeCoverage.ShapeFor(Names.WorkspaceLayout(ChefWorkspace, Names.DefaultPlayerId)), Is.EqualTo(typeof(FurniturePlacementLayoutSaveData)));
        }

        [Test]
        public void EmptyTemplate_IsLoadError()
        {
            using var good = new TempDir();
            good.Write("data/a/WorkspaceTemplate.json", "[" + Template("template.x", "workspace.x", "[]", "[]", Placement(0)) + "]");
            var control = Load(good);
            Assert.That(control.Errors.Any(e => e.Kind == "constructor" && e.Type == "WorkspaceTemplate"), Is.False, "가구 하나면 템플릿 자체는 올바르다");

            using var bad = new TempDir();
            bad.Write("data/a/WorkspaceTemplate.json", "[" + Template("template.x", "workspace.x", "[]", "[]", "") + "]");
            var result = Load(bad);

            AssertError(result, "constructor", "WorkspaceTemplate", "template.x");
            Assert.That(result.Definitions.TryGet<WorkspaceTemplate>("template.x", out _), Is.False, "가구 없는 템플릿은 정의에 오르지 않는다");
        }

        [TestCase("[ null ]")]
        [TestCase(@"[ { ""ref"": ""decoration.nope"", ""type"": ""SkillTierCapacityRule"" } ]")]
        public void WorkspaceWithNullOrUnbuiltRule_IsLoadError(string rules)
        {
            using var dir = new TempDir();
            dir.Write("data/a/WorkspaceDefinition.json", Workspace("workspace.x", "[6, 10, 14]", rules));
            var result = Load(dir);

            AssertError(result, "constructor", "WorkspaceDefinition", "workspace.x");
            Assert.That(result.Definitions.TryGet<Workspace>("workspace.x", out _), Is.False);
        }

        [TestCase("[ null ]")]
        [TestCase(@"[ { ""ref"": ""workspace-bonus.nope"", ""type"": ""QualityRewardPercentBonus"" } ]")]
        public void BindingWithNullOrUnbuiltBonus_IsLoadError(string bonuses)
        {
            using var dir = new TempDir();
            dir.Write("data/a/WorkBonusBinding.json", @"[{ ""type"": ""WorkBonusBinding"", ""id"": ""work-bonus.x"",
                ""work"": { ""ref"": ""work.x"", ""type"": ""FacilityWorkDefinition"" }, ""bonuses"": " + bonuses + " }]");
            var result = Load(dir);

            AssertError(result, "constructor", "WorkBonusBinding", "work-bonus.x");
        }

        [TestCase("SkillTierDefinition", @"""minimumValue"": 10, ""displayNameKey"": ""tier.x.name""")]
        [TestCase("WorkSkillGrowth", @"""work"": { ""ref"": ""work.x"", ""type"": ""FacilityWorkDefinition"" }, ""amount"": 1")]
        [TestCase("PlacementSkillGrowth", @"""surfaceId"": ""house"", ""amount"": 1, ""dailyLimit"": 3")]
        public void EmptySkill_IsLoadError(string type, string fields)
        {
            using var dir = new TempDir();
            dir.Write("data/a/" + type + ".json", @"[{ ""type"": """ + type + @""", ""id"": ""x.1"", ""skill"": null, " + fields + " }]");
            var result = Load(dir);

            AssertError(result, "constructor", type, "x.1");
        }

        [TestCase(@"[ { ""ref"": ""item.x"", ""type"": ""ItemDefinition"" } ]", "[ 0 ]", 0, "constructor")]
        [TestCase("[]", "[]", 7, "enum-range")]
        public void InvalidTemplate_IsLoadError(string costItems, string costCounts, int direction, string kind)
        {
            using var dir = new TempDir();
            dir.Write("data/a/WorkspaceTemplate.json", "[" + Template("template.x", "workspace.x", costItems, costCounts, Placement(direction)) + "]");
            var result = Load(dir);

            AssertError(result, kind, "WorkspaceTemplate", "template.x");
            Assert.That(result.Ok, Is.False);
        }

        [Test]
        public void WorkspaceFacilityPointingAtAnotherType_IsWrongRefType()
        {
            using var dir = new TempDir();
            dir.Write("data/a/SpaceQualityDefinition.json", @"[{ ""type"": ""SpaceQualityDefinition"", ""id"": ""quality.x"", ""displayNameKey"": ""quality.x.name"", ""iconKey"": """" }]");
            dir.Write("data/a/WorkspaceDefinition.json", Workspace("workspace.x", "[6, 10, 14]", "[]")
                .Replace(@"{ ""ref"": ""facility.x"", ""type"": ""FacilityDefinition"" }", @"{ ""ref"": ""quality.x"", ""type"": ""SpaceQualityDefinition"" }"));
            var result = Load(dir);

            AssertError(result, "wrong-ref-type", "WorkspaceDefinition", "workspace.x");
        }

        [Test]
        public void DefaultTemplateOfAnotherWorkspace_IsLoadError()
        {
            using var dir = new TempDir();
            var a = Workspace("workspace.a", "[6, 10, 14]", "[]", "template.b");
            var b = Workspace("workspace.b", "[6, 10, 14]", "[]");
            dir.Write("data/a/WorkspaceDefinition.json", a.TrimEnd(']') + "," + b.TrimStart('['));
            dir.Write("data/a/WorkspaceTemplate.json", "[" + Template("template.b", "workspace.b", "[]", "[]", Placement(0)) + "]");
            var result = Load(dir);

            AssertError(result, "wrong-ref-type", "WorkspaceDefinition", "workspace.a");
            Assert.That(result.Errors.Any(e => e.Type == "WorkspaceDefinition" && e.Id == "workspace.b" && e.Kind == "wrong-ref-type"), Is.False, "자기 템플릿은 괜찮다");
        }

        private static LoadResult Load(TempDir dir) => GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);

        private static void AssertError(LoadResult result, string kind, string type, string id)
        {
            Assert.That(result.Errors.Any(e => e.Kind == kind && e.Type == type && e.Id == id), Is.True,
                $"{kind} {type}/{id} 오류가 없다:\n" + string.Join("\n", result.Errors));
        }

        private static string Placement(int direction) => @"{ ""furniture"": ""furniture.x"", ""cell"": { ""x"": 1, ""y"": 1 }, ""direction"": " + direction + " }";

        private static string Template(string id, string workspace, string costItems, string costCounts, string placements) =>
            @"{ ""type"": ""WorkspaceTemplate"", ""id"": """ + id + @""", ""workspace"": { ""ref"": """ + workspace + @""", ""type"": ""WorkspaceDefinition"" },
                ""displayNameKey"": """ + id + @".name"", ""costItems"": " + costItems + @", ""costCounts"": " + costCounts + @", ""placements"": [ " + placements + " ] }";

        private static string Workspace(string id, string capacities, string rules = "[]", string defaultTemplate = null,
            string facility = "facility.x", string surface = null) =>
            @"[{ ""type"": ""WorkspaceDefinition"", ""id"": """ + id + @""",
            ""facility"": { ""ref"": """ + facility + @""", ""type"": ""FacilityDefinition"" }, ""surfaceId"": """ + (surface ?? id) + @""", ""acceptsSurfaceIds"": [ ""house"" ],
            ""capacityByLevel"": " + capacities + @", ""repeatPercent"": 70, ""rules"": " + rules + @", ""defaultTemplate"": "
            + (defaultTemplate == null ? "null" : @"{ ""ref"": """ + defaultTemplate + @""", ""type"": ""WorkspaceTemplate"" }") + " }]";
    }
}
