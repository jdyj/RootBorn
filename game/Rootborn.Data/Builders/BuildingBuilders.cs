using System;
using System.Collections.Generic;
using Rootborn.Core.Building;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;

namespace Rootborn.Data.Builders
{
    /// <summary>
    /// 집 짓기(data/building): 꾸밈(벽지·바닥 종류), 새 게임의 처음 배치 가구. 등록부(registry.json)에는 넣지 않고 파일 순서대로 읽는다(일터와 같다).
    /// 올바르지 않은 정의는 던져서 읽기 오류(constructor)로 모은다. 처음 배치가 모르는 가구를 쓰거나 같은 면에 둘인 것은 <see cref="BuildingTables"/>가 본다.
    /// </summary>
    internal static class BuildingBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            // 판매 필드(priceItem, price, purchaseLocationId)는 나중에 더해져 잠긴 꾸밈에만 있다(가구와 같은 꼴). 없으면 팔지 않는다.
            b.Add("RoomFinishDefinition", BuilderHelpers.AddedLater(new[] { "priceItem", "price", "purchaseLocationId" }, (n, c) =>
            {
                var finish = new RoomFinishDefinition(n.Id, n.Str("slot"), n.Str("nameKey"), n.Str("sheet"), n.Str("art"),
                    BuilderHelpers.StrictItemAmounts(n, "costItems", "costCounts"), n.Str("unlockToken"),
                    n.RefId("priceItem"), n.Int("price"), n.Str("purchaseLocationId"));
                if (!finish.IsValid)
                    throw new InvalidOperationException("꾸밈이 올바르지 않다(id·slot·nameKey·sheet·art가 있고, 비용은 아이템과 1 이상의 개수)");
                return finish;
            }));

            // 놓을 줄은 일터 템플릿(WorkspaceTemplate)의 placements와 같은 꼴(furniture, cell, direction). 가구는 참조가 아니라 stableId 문자열이다.
            b.Add("HouseStarterLayout", (n, c) =>
            {
                var placements = new List<TemplatePlacement>();
                foreach (var p in n.Items("placements"))
                    placements.Add(new TemplatePlacement(p.Str("furniture"), BuilderHelpers.Grid(p, "cell"), p.Enum<FurniturePlacementDirection>("direction")));
                var starter = new HouseStarterLayout(n.Id, n.Str("surfaceId"), placements.ToArray());
                if (!starter.IsValid)
                    throw new InvalidOperationException("처음 배치가 올바르지 않다(id·surfaceId가 있고, 가구 줄마다 가구 id와 방향)");
                return starter;
            });
        }
    }

    /// <summary>전부 읽은 뒤 보는 집 짓기 표의 문제: 처음 배치가 모르는 가구를 쓰거나, 한 면에 처음 배치가 둘(뒤의 것을 오류로)이면 읽기 오류다.</summary>
    internal static class BuildingTables
    {
        public static void Check(GameDefinitions definitions, List<LoadError> errors)
        {
            var furnitureIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in definitions.All<FurnitureDefinition>()) furnitureIds.Add(f.StableId);

            var bySurface = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var starter in definitions.All<HouseStarterLayout>())
            {
                if (bySurface.TryGetValue(starter.SurfaceId, out var owner))
                    errors.Add(new LoadError("invalid-field", "HouseStarterLayout", starter.Id, "", $"면 '{starter.SurfaceId}'의 처음 배치는 이미 '{owner}'다"));
                else bySurface[starter.SurfaceId] = starter.Id;

                foreach (var p in starter.Placements)
                    if (!furnitureIds.Contains(p.FurnitureId))
                        errors.Add(new LoadError("missing-ref", "HouseStarterLayout", starter.Id, "", $"모르는 가구 '{p.FurnitureId}'를 쓴다"));
            }
        }
    }
}
