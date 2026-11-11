using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Rootborn.Data.Tests.Integrity
{
    /// <summary>
    /// 엔티티끼리의 참조가 data/ 안에 있는지 본다(자동 테스트).
    ///
    /// 로더는 참조를 만들 때(BuildContext.One/All/Slots/TryPeek) 가리키는 엔티티가 없으면 missing-ref를 낸다.
    /// 그러나 Core가 id 문자열만 받는 필드는 NodeReader.RefId/RefIds로 읽고, 이것은 { ref } 의 문자열을 꺼내기만 할 뿐
    /// 그 엔티티가 있는지 보지 않는다(만들기 함수에 96곳). 참조 모양이 아닌 맨 문자열 id 필드(예: targetId, npcId)도 보지 않는다.
    /// 그래서 여기서는
    /// - data/의 모든 { "ref": id, "type": 종류 }가 표에 있는지(로더가 잡는 것까지 포함해 전부 — 겹쳐도 싸다),
    /// - 이름이 Id·Ids로 끝나는 맨 문자열 필드를 종류별로 분류하고, 엔티티 id인 필드는 그 엔티티가 있는지,
    /// - registry.json의 id가 모두 엔티티로 있는지
    /// 를 본다. 지금 걸리는 것은 KnownDataGaps에 고정한다.
    /// </summary>
    public sealed class EntityReferenceTests
    {
        private static DataScan Data => DataScan.Real;

        [Test]
        public void EveryReferenceObject_PointsToAnEntityInData()
        {
            var gaps = ReferenceGaps(Data, out var count);
            TestContext.WriteLine($"참조 {count}개");
            Assert.That(count, Is.GreaterThan(1000), "참조를 찾는 방식이 데이터 모양과 어긋났다");
            GapAssert.OnlyKnown(gaps, KnownDataGaps.All, "missing-ref");
        }

        [Test]
        public void TheLoaderLeavesRefIdFieldsUnchecked_AndThisCheckFindsThem()
        {
            using var dir = new TempDir();
            dir.Write("data/registry.json", "{}");
            dir.Write("data/a/Holder.json", @"[{ ""type"": ""Holder"", ""id"": ""holder.1"",
                ""built"": { ""ref"": ""gone.1"", ""type"": ""Leaf"" },
                ""idOnly"": { ""ref"": ""gone.2"", ""type"": ""Leaf"" } }]");
            dir.Write("data/b/Leaf.json", @"[{ ""type"": ""Leaf"", ""id"": ""leaf.1"" }]");

            var result = GameDataLoader.Load(System.IO.Path.Combine(dir.Path, "data"), b =>
            {
                b.Add("Leaf", (n, c) => new object());
                // built는 만들어서 받고(로더가 확인한다), idOnly는 id 문자열만 받는다(로더가 확인하지 않는다).
                b.Add("Holder", (n, c) => new[] { c.One<object>(n, "built"), n.RefId("idOnly") });
            });

            var loaderMisses = result.Errors.Where(e => e.Kind == "missing-ref").Select(e => e.Message).ToList();
            Assert.That(loaderMisses.Count, Is.EqualTo(1), string.Join("\n", loaderMisses));
            Assert.That(loaderMisses[0], Does.Contain("gone.1"));

            var found = ReferenceGaps(DataScan.Of(dir.Path), out var count).Select(g => g.Detail).ToList();
            Assert.That(count, Is.EqualTo(2));
            Assert.That(found, Is.EquivalentTo(new[] { "Leaf/gone.1", "Leaf/gone.2" }));
        }

        /// <summary>
        /// 이름이 Id·Ids로 끝나는 맨 문자열 필드("종류.필드")가 무엇을 가리키는가.
        /// 엔티티 id인 필드는 받을 수 있는 종류를, 엔티티가 아닌 이름(도착 지점, 놓는 면, 기록 이름 등)인 필드는 빈 배열을 둔다.
        /// 데이터에 새 필드가 생기면 여기에 분류해야 테스트가 통과한다.
        /// </summary>
        private static readonly Dictionary<string, string[]> IdStringFields = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // 엔티티 id
            ["ClueInterpretationSourceDefinition.targetId"] = new[] { "NpcDefinition", "LocationDefinition" },
            ["DiscoveryClueLocationVisitCompletion.locationId"] = new[] { "LocationDefinition" },
            ["FurnitureDefinition.purchaseLocationId"] = new[] { "LocationDefinition" },
            ["FurnitureDefinition.stableId"] = new[] { "FurnitureDefinition" },
            ["GatherResourceStep.resourceId"] = new[] { "ItemDefinition" },
            ["GrowingCharacterAppearanceDefinition.facilityId"] = new[] { "FacilityDefinition" },
            ["HitGroundWithRockTrigger.expectedTargetResourceId"] = new[] { "ResourceNodeDefinition" },
            ["LocationStateActivityLogCondition.requiredActivityId"] = new[] { "LocationActivityDefinition" },
            ["MilestoneRouteDefinition.recommendedActionId"] = new[]
            {
                "TownHelpActionDefinition", "OutsideSchoolActivityDefinition", "LifeActivityDefinition", "LocationActivityDefinition",
            },
            ["OutsideSchoolLogMilestoneObjective.targetId"] = new[] { "TraitDefinition", "SkillDefinition" },
            ["QuestChainDefinition.relatedCareerInterestId"] = new[] { "CareerInterestDefinition" },
            ["RelationshipDefinition.targetId"] = new[] { "NpcDefinition" },
            ["ResourceNodeDefinition.resourceId"] = new[] { "ItemDefinition" },              // drops[].resourceId: 떨어뜨리는 아이템
            ["TownHelpActionDefinition.locationId"] = new[] { "LocationDefinition" },
            ["TownHelpActionDefinition.npcId"] = new[] { "NpcDefinition" },
            ["WearableDefinition.autoEquipFacilityId"] = new[] { "FacilityDefinition" },
            ["WorkplaceDefinition.locationId"] = new[] { "LocationDefinition" },
            ["WorkplaceDefinition.npcId"] = new[] { "NpcDefinition" },
            ["TutorialStageAdvanceDayEndRule.fromStageId"] = new[] { "TutorialStageDefinition" },
            ["WorldStateChangeQuestReward.sourceQuestChainId"] = new[] { "QuestChainDefinition", "QuestDefinition" }, // 보상을 준 퀘스트 묶음 또는 퀘스트

            // 엔티티가 아닌 이름
            ["CareerInterestFocusReward.rewardId"] = Array.Empty<string>(),            // 보상 요청 이름(받음 기록의 키)
            ["ClueInterpretationPolicyDefinition.groupId"] = Array.Empty<string>(),    // 정책 묶음 이름
            ["DayEndRuleDefinition.nextDayEntryPointId"] = Array.Empty<string>(),      // 도착 지점(game/world/maps.json spawns)
            ["FurnitureDefinition.allowedSurfaceIds"] = Array.Empty<string>(),         // 놓는 면(interactions.json placementSurface)
            ["FurniturePlacedQuestObjective.requiredSurfaceId"] = Array.Empty<string>(),
            ["LocationActivityCompletedCondition.blockedReasonId"] = Array.Empty<string>(), // 막힌 이유 이름
            ["LocationDefinition.worldAnchorId"] = Array.Empty<string>(),              // Unity 씬의 장소 앵커 이름
            ["LocationStateAvailabilityEffect.interactableObjectIds"] = Array.Empty<string>(), // Unity 씬의 물건·문·가게 칸 이름
            ["LocationStateAvailabilityEffect.portalIds"] = Array.Empty<string>(),
            ["LocationStateAvailabilityEffect.shopItemIds"] = Array.Empty<string>(),
            ["QuestStepDefinition.legacyStepIds"] = Array.Empty<string>(),             // 옛 세이브의 단계 이름
            ["TutorialStageAdvanceDayEndRule.nextObjectiveId"] = Array.Empty<string>(), // 안내 목표 이름
            ["WorldDestinationDefinition.spawnIds"] = Array.Empty<string>(),           // 도착 지점
            ["WorldStateChangeQuestReward.sourceEventId"] = Array.Empty<string>(),     // 원인 기록 이름
            ["WorldStateChangeQuestReward.sourceQuestStepId"] = Array.Empty<string>(),
            ["WorldTravelCatalogDefinition.newGameSpawnId"] = Array.Empty<string>(),   // 도착 지점
        };

        [Test]
        public void EveryIdStringField_IsClassified_AndPointsToItsEntity()
        {
            var gaps = new List<DataGap>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var checkedIds = 0;
            foreach (var (owner, node, path, obj) in Data.EntityObjects())
            {
                foreach (var property in obj.EnumerateObject())
                {
                    if (property.Name == "id" || !(property.Name.EndsWith("Id", StringComparison.Ordinal) || property.Name.EndsWith("Ids", StringComparison.Ordinal))) continue;
                    var values = Strings(property.Value);
                    if (values == null) continue;

                    var field = node.Type + "." + property.Name;
                    var where = $"{Data.Relative(node.File)} {owner} {(path.Length == 0 ? property.Name : path + "." + property.Name)}";
                    if (!IdStringFields.TryGetValue(field, out var types))
                    {
                        if (seen.Add(field)) gaps.Add(new DataGap("unclassified-id-field", field, "IdStringFields에 분류하지 않았다(예: " + where + ")"));
                        continue;
                    }

                    seen.Add(field);
                    if (types.Length == 0) continue;
                    foreach (var id in values)
                    {
                        if (id.Length == 0) continue;
                        checkedIds++;
                        if (!types.Any(type => Data.HasEntity(type, id)))
                            gaps.Add(new DataGap("missing-id", where + " " + id, $"{string.Join("|", types)}/{id}가 없다"));
                    }
                }
            }

            var unused = IdStringFields.Keys.Where(k => !seen.Contains(k)).ToList();
            TestContext.WriteLine($"id 문자열 필드 {seen.Count}종, 엔티티 id 값 {checkedIds}개");
            Assert.That(unused, Is.Empty, "데이터에 없는 필드가 IdStringFields에 남았다(지운다)");
            GapAssert.OnlyKnown(gaps, KnownDataGaps.All, "unclassified-id-field", "missing-id");
        }

        [Test]
        public void EveryRegistryId_IsAnEntity()
        {
            var gaps = new List<DataGap>();
            var registered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in Data.Table.RegistryKeys)
            {
                foreach (var id in Data.Table.RegistryIds(key))
                {
                    registered.Add(id);
                    if (!Data.Ids.Contains(id)) gaps.Add(new DataGap("missing-registry-id", "data/registry.json " + key + " " + id, "그 id의 엔티티가 data/에 없다"));
                }
            }

            var referenced = new HashSet<(string, string)>();
            foreach (var (_, _, _, obj) in Data.EntityObjects())
                if (TryReference(obj, out var type, out var id)) referenced.Add((type, id));
            var notRegistered = Data.Entities.Where(e => !registered.Contains(e.Id)).ToList();
            var unreached = notRegistered.Where(e => !referenced.Contains((e.Type, e.Id))).ToList();

            // 등록부에 없는 엔티티는 대부분 다른 엔티티가 가리키는 전략·조각이다. 어디서도 가리키지 않는 것은 종류로 찾아 읽거나(시각, 낮밤 등)
            // 게임이 쓰지 않는 것이다. 수만 보고한다.
            TestContext.WriteLine($"엔티티 {Data.Entities.Count}개, 등록부 id {registered.Count}개, 등록부에 없는 엔티티 {notRegistered.Count}개, "
                + $"그중 어디서도 가리키지 않는 것 {unreached.Count}개: "
                + string.Join(", ", unreached.GroupBy(e => e.Type).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key + " " + g.Count())));
            Assert.That(registered, Is.Not.Empty);
            GapAssert.OnlyKnown(gaps, KnownDataGaps.All, "missing-registry-id");
        }

        /// <summary>data/의 모든 { ref: 문자열, type } 가 표에 있는지. count는 본 참조 수.</summary>
        private static List<DataGap> ReferenceGaps(DataScan data, out int count)
        {
            var gaps = new List<DataGap>();
            count = 0;
            foreach (var (owner, node, path, obj) in data.EntityObjects())
            {
                if (!TryReference(obj, out var type, out var id)) continue;
                count++;
                if (!data.HasEntity(type, id)) gaps.Add(new DataGap("missing-ref", $"{data.Relative(node.File)} {owner} {path}", type + "/" + id));
            }

            return gaps;
        }

        private static bool TryReference(JsonElement obj, out string type, out string id)
        {
            type = DataScan.Str(obj, "type");
            id = obj.TryGetProperty("ref", out var reference) && reference.ValueKind == JsonValueKind.String ? reference.GetString() : null;
            // 엔티티 맨 위 객체도 type을 갖지만 ref가 없으므로 걸리지 않는다.
            return type != null && id != null;
        }

        private static List<string> Strings(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String) return new List<string> { value.GetString() };
            if (value.ValueKind != JsonValueKind.Array) return null;
            var strings = value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()).ToList();
            return strings.Count == 0 && value.GetArrayLength() > 0 ? null : strings;
        }
    }
}
