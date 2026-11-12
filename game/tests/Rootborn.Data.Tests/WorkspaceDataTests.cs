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

        private static string Workspace(string id, string capacities) => @"[{ ""type"": ""WorkspaceDefinition"", ""id"": """ + id + @""",
            ""facility"": { ""ref"": ""facility.x"", ""type"": ""FacilityDefinition"" }, ""surfaceId"": """ + id + @""", ""acceptsSurfaceIds"": [ ""house"" ],
            ""capacityByLevel"": " + capacities + @", ""repeatPercent"": 70, ""rules"": [], ""defaultTemplate"": null }]";
    }
}
