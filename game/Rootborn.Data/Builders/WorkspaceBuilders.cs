using System;
using System.Collections.Generic;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;

namespace Rootborn.Data.Builders
{
    /// <summary>
    /// 일터(data/workspaces): 성질, 가구 성질 규칙, 기술 단계, 일터와 꾸미기 규칙, 공간 이득, 기술 성장, 템플릿.
    /// 일터·템플릿·일·시설·기술은 서로 id로 가리킨다(일터 ↔ 기본 템플릿은 서로 가리켜 만들면 끝나지 않는다).
    /// 가리키는 엔티티가 표에 있는지는 들여다보고 확인한다. 올바르지 않은 정의는 던져서 읽기 오류(constructor)로 모은다.
    /// 엔티티 하나에서 나오지 않는 두 표(가구 성질 표, 일 연결 표)는 <see cref="WorkspaceTables"/>가 전부 읽은 뒤 만든다.
    /// </summary>
    internal static class WorkspaceBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("SpaceQualityDefinition", (n, c) => new SpaceQuality(n.Id, n.Str("displayNameKey"), n.Str("iconKey")));

            // category 또는 furniture(가구 stableId) 하나만 쓴다. 둘 다나 둘 다 없음, 모르는 가구는 표가 문제로 내고 로더가 오류로 옮긴다.
            b.Add("FurnitureQualityRule", (n, c) =>
            {
                var o = n.Optional("category", "furniture");
                return new FurnitureQualityRule(n.Id, o.Str("category"), o.Str("furniture"), c.One<SpaceQuality>(n, "quality")?.Id, n.Int("score"));
            });

            b.Add("SkillTierDefinition", (n, c) =>
                new SkillTier(n.Id, Require(CheckedRef(n, c, "skill", "SkillDefinition"), "skill"), n.Int("minimumValue"), n.Str("displayNameKey")));

            b.Add("SkillTierCapacityRule", (n, c) => new SkillTierCapacityRule(c.One<SkillTier>(n, "tier"), n.Int("capacityBonus")));
            b.Add("SkillTierScoreRule", (n, c) => new SkillTierScoreRule(c.One<SkillTier>(n, "tier"), n.Int("scorePercentBonus")));

            b.Add("WorkspaceDefinition", (n, c) =>
            {
                var workspace = new Workspace(n.Id, CheckedRef(n, c, "facility", "FacilityDefinition"), n.Str("surfaceId"), n.Strings("acceptsSurfaceIds"),
                    n.IntArray("capacityByLevel"), n.Int("repeatPercent"), NoNulls(c.Slots<IDecorationRule>(n, "rules"), "rules"),
                    CheckedRef(n, c, "defaultTemplate", "WorkspaceTemplate"));
                if (!workspace.IsValid)
                    throw new InvalidOperationException("일터가 올바르지 않다(시설·면이 있고, capacityByLevel 3개가 0 이상, repeatPercent 0~100)");
                return workspace;
            });

            b.Add("QualityRewardPercentBonus", (n, c) => new QualityRewardPercentBonus(
                Require(c.One<SpaceQuality>(n, "quality")?.Id, "quality"), n.Int("scorePerStep"), n.Int("percentPerStep"), n.Int("maximumPercent")));
            b.Add("QualityReputationBonus", (n, c) => new QualityReputationBonus(
                Require(c.One<SpaceQuality>(n, "quality")?.Id, "quality"), n.Int("scorePerStep"), n.Int("reputationPerStep"), n.Int("maximumReputation")));

            b.Add("WorkBonusBinding", (n, c) => new WorkBonusBinding(
                n.Id, Require(CheckedRef(n, c, "work", "FacilityWorkDefinition"), "work"), NoNulls(c.Slots<IWorkspaceBonus>(n, "bonuses"), "bonuses")));

            b.Add("WorkSkillGrowth", (n, c) => new WorkSkillGrowth(
                n.Id, Require(CheckedRef(n, c, "work", "FacilityWorkDefinition"), "work"), Require(CheckedRef(n, c, "skill", "SkillDefinition"), "skill"),
                n.Int("amount")));

            b.Add("PlacementSkillGrowth", (n, c) =>
            {
                var growth = new PlacementSkillGrowth(n.Id, n.Str("surfaceId"), CheckedRef(n, c, "skill", "SkillDefinition"), n.Int("amount"), n.Int("dailyLimit"));
                if (!growth.IsValid) throw new InvalidOperationException("꾸미며 배우는 기술이 올바르지 않다(면과 기술이 있어야 한다)");
                return growth;
            });

            // 가구가 있는지는 모든 가구를 읽은 뒤 WorkspaceTables가 본다(가구는 참조가 아니라 stableId 문자열이다).
            b.Add("WorkspaceTemplate", (n, c) =>
            {
                var template = new WorkspaceTemplate(n.Id, CheckedRef(n, c, "workspace", "WorkspaceDefinition"), n.Str("displayNameKey"),
                    BuilderHelpers.StrictItemAmounts(n, "costItems", "costCounts"), Placements(n));
                if (!template.IsValid)
                    throw new InvalidOperationException("템플릿이 올바르지 않다(일터가 있고, 값은 1 이상, 가구 줄마다 가구 id와 방향)");
                // 가구 없는 템플릿은 적용하면 일터 가구를 모두 치우기만 한다(값을 내도 아무것도 깔리지 않는다).
                if (template.Placements.Count == 0) throw new InvalidOperationException("템플릿에 가구가 없다(placements가 비었다)");
                return template;
            });
        }

        private static TemplatePlacement[] Placements(NodeReader n)
        {
            var placements = new List<TemplatePlacement>();
            foreach (var p in n.Items("placements"))
                placements.Add(new TemplatePlacement(p.Str("furniture"), BuilderHelpers.Grid(p, "cell"), p.Enum<FurniturePlacementDirection>("direction")));
            return placements.ToArray();
        }

        /// <summary>참조의 id. 가리키는 엔티티가 표에 없으면 missing-ref, 다른 종류면 wrong-ref-type 오류를 남긴다(만들지는 않는다).</summary>
        private static string CheckedRef(NodeReader n, BuildContext c, string field, string expectedType)
        {
            var id = n.RefId(field);
            if (id.Length == 0) return id;
            if (c.TryPeek(n, field, out var target) && target.Type != expectedType)
                n.Report("wrong-ref-type", $"필드 '{field}'의 {target.Type}/{target.Id}는 {expectedType}여야 한다");
            return id;
        }

        private static string Require(string id, string field)
        {
            if (string.IsNullOrEmpty(id)) throw new InvalidOperationException($"'{field}'가 비어 있다");
            return id;
        }

        /// <summary>null 원소(빈 참조나 만들지 못한 것)가 있으면 던진다. 조용히 빼지 않는다.</summary>
        private static T[] NoNulls<T>(List<T> slots, string field) where T : class
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == null) throw new InvalidOperationException($"'{field}'[{i}]가 비었거나 만들지 못했다");
            return slots.ToArray();
        }
    }

    /// <summary>
    /// 전부 읽은 뒤 한 번 만드는 일터 표(통화·시간 규칙과 같은 자리): 가구 성질 표와 일 연결 표.
    /// 표의 문제와, 템플릿이 모르는 가구를 쓰거나 일터와 템플릿이 서로 맞지 않는 것은 읽기 오류다.
    /// </summary>
    internal static class WorkspaceTables
    {
        public static void Build(GameDefinitions definitions, List<LoadError> errors)
        {
            var furniture = definitions.All<FurnitureDefinition>();
            var table = new FurnitureQualityTable(definitions.All<FurnitureQualityRule>(), furniture);
            foreach (var problem in table.Problems) errors.Add(new LoadError("constructor", "FurnitureQualityRule", "", "", problem));
            definitions.FurnitureQuality = table;
            definitions.WorkLinks = new WorkLinks(definitions.All<WorkBonusBinding>(), definitions.All<WorkSkillGrowth>());

            var furnitureIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in furniture) furnitureIds.Add(f.StableId);
            foreach (var template in definitions.All<WorkspaceTemplate>())
                foreach (var id in template.FurnitureIds)
                    if (!furnitureIds.Contains(id))
                        errors.Add(new LoadError("missing-ref", "WorkspaceTemplate", template.Id, "", $"모르는 가구 '{id}'를 쓴다"));

            foreach (var workspace in definitions.All<Workspace>())
            {
                if (workspace.DefaultTemplateId.Length == 0) continue;
                if (definitions.TryGet<WorkspaceTemplate>(workspace.DefaultTemplateId, out var template) && template.WorkspaceId != workspace.Id)
                    errors.Add(new LoadError("wrong-ref-type", "WorkspaceDefinition", workspace.Id, "",
                        $"기본 템플릿 '{template.Id}'는 다른 일터 '{template.WorkspaceId}'의 것이다"));
            }
        }
    }
}
