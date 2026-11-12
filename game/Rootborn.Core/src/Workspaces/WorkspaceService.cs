using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.Placement;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Workspaces
{
    public enum WorkspaceResultKind { Applied, DuplicateRequest, InvalidRequest, InvalidDefinition, NotOwner, InsufficientResources, DoesNotFit, OverCapacity }

    /// <summary>
    /// 일터에 템플릿을 깔고(돈을 내거나 기본 템플릿은 공짜로), 가구를 놓으면 기술을 올린다.
    /// 적용은 미리 모두 확인하고, 하나라도 안 되면 아무것도 바꾸지 않으며, 요청 id로 한 번만 한다.
    /// 자기 시설인지와 가구 한도는 부르는 쪽이 값으로 넘긴다.
    /// </summary>
    public sealed class WorkspaceService
    {
        /// <summary>미리 확인한 배치를 저장하지 못했을 때 교환 안에서 던져 교환을 되돌린다. 밖으로 나가지 않는다.</summary>
        private sealed class LayoutNotCommitted : Exception
        {
            public LayoutNotCommitted() : base("Prevalidated workspace layout could not be committed.") { }
        }

        /// <summary>아무것도 바꾸지 않고 적용할 수 있는지 본다. 판정 순서는 <see cref="TryApplyTemplate"/>와 같다.</summary>
        public WorkspaceResultKind CanApplyTemplate(WorkspaceTemplate template, Workspace workspace, HouseFurnitureLayout layout, IReadOnlyDictionary<string, FurnitureDefinition> catalog,
            ICareerInventory inventory, WorkspaceProgress progress, bool ownsFacility, int capacity, string requestId, bool free)
            => Check(template, workspace, layout, catalog, inventory, progress, ownsFacility, capacity, requestId, free, out _);

        /// <summary>
        /// 템플릿으로 놓인 가구를 모두 바꾼다(물건이 든 상자는 남는다). 비용 교환의 commit 안에서 배치를 한 번 저장하고 요청을 기록한다.
        /// 배치 저장이 실패하면 교환을 되돌리고 <see cref="WorkspaceResultKind.DoesNotFit"/>를 준다. 공짜(free)는 기본 템플릿 깔기이고
        /// 요청 id는 일터 id다(깐 일터로도 적는다).
        /// </summary>
        public WorkspaceResultKind TryApplyTemplate(WorkspaceTemplate template, Workspace workspace, HouseFurnitureLayout layout, IReadOnlyDictionary<string, FurnitureDefinition> catalog,
            ICareerInventory inventory, WorkspaceProgress progress, bool ownsFacility, int capacity, string requestId, bool free)
        {
            var check = Check(template, workspace, layout, catalog, inventory, progress, ownsFacility, capacity, requestId, free, out var requests);
            if (check != WorkspaceResultKind.Applied) return check;
            try
            {
                bool exchanged = inventory.TryExchange(free ? null : template.Costs, null, () =>
                {
                    if (!layout.TryReplaceUserFurniture(requests)) throw new LayoutNotCommitted();
                    progress.MarkApplied(requestId);
                    if (free) progress.MarkSeeded(workspace.Id);
                });
                return exchanged ? WorkspaceResultKind.Applied : WorkspaceResultKind.InsufficientResources;
            }
            catch (LayoutNotCommitted)
            {
                return WorkspaceResultKind.DoesNotFit;
            }
        }

        /// <summary>오늘 한도 안이면 기술을 올리고 센다. 같은 슬롯·플레이어가 아니거나 규칙이 잘못됐으면 아무것도 바꾸지 않는다.</summary>
        public bool TryGrowByPlacing(PlacementSkillGrowth rule, StudentLifeProgress student, WorkspaceProgress progress)
        {
            if (rule == null || !rule.IsValid || rule.Amount <= 0 || student == null || progress == null) return false;
            if (student.SaveSlot != progress.SaveSlot || student.PlayerId != progress.PlayerId) return false;
            int day = student.CurrentDay;
            if (progress.GrowthLeftToday(day, rule.DailyLimit) <= 0) return false;
            student.AddSkill(rule.SkillId, rule.Amount);
            progress.CountGrowth(day);
            return true;
        }

        private static WorkspaceResultKind Check(WorkspaceTemplate template, Workspace workspace, HouseFurnitureLayout layout, IReadOnlyDictionary<string, FurnitureDefinition> catalog,
            ICareerInventory inventory, WorkspaceProgress progress, bool ownsFacility, int capacity, string requestId, bool free, out List<FurniturePlacementRequest> requests)
        {
            requests = null;
            if (string.IsNullOrEmpty(requestId) || layout == null || inventory == null || progress == null) return WorkspaceResultKind.InvalidRequest;
            if (free && (workspace == null || requestId != workspace.Id)) return WorkspaceResultKind.InvalidRequest;
            if (progress.HasApplied(requestId)) return WorkspaceResultKind.DuplicateRequest;

            if (template == null || !template.IsValid || workspace == null || !workspace.IsValid || catalog == null) return WorkspaceResultKind.InvalidDefinition;
            if (template.WorkspaceId != workspace.Id || layout.SurfaceId != workspace.SurfaceId) return WorkspaceResultKind.InvalidDefinition;
            var placements = template.Placements;
            var built = new List<FurniturePlacementRequest>(placements.Count);
            for (int i = 0; i < placements.Count; i++)
            {
                if (!catalog.TryGetValue(placements[i].FurnitureId, out var definition) || definition == null) return WorkspaceResultKind.InvalidDefinition;
                built.Add(new FurniturePlacementRequest(definition, placements[i].Anchor, placements[i].Direction));
            }
            // 공짜는 빈 일터에 기본 템플릿을 처음 까는 것뿐이다: 이미 놓인 가구(돈을 내고 깐 것, 플레이어가 놓은 것)나
            // 되살리지 못한 기록이 있으면 지우지 않는다.
            if (free && (template.Id != workspace.DefaultTemplateId || layout.Placed.Count != 0 || layout.UnresolvedCount != 0)) return WorkspaceResultKind.InvalidRequest;

            if (!ownsFacility) return WorkspaceResultKind.NotOwner;
            if ((long)built.Count + KeptChestCount(layout) > Math.Max(0, capacity)) return WorkspaceResultKind.OverCapacity;
            if (!layout.CanReplaceUserFurniture(built)) return WorkspaceResultKind.DoesNotFit;
            if (!free && !inventory.CanExchange(template.Costs, null)) return WorkspaceResultKind.InsufficientResources;
            requests = built;
            return WorkspaceResultKind.Applied;
        }

        /// <summary>한꺼번에 바꿔도 남는 가구(물건이 든 상자) 수. 한도에 함께 센다.</summary>
        private static int KeptChestCount(HouseFurnitureLayout layout)
        {
            int count = 0;
            var placed = layout.Placed;
            for (int i = 0; i < placed.Count; i++) if (placed[i].HoldsItems) count++;
            return count;
        }
    }
}
