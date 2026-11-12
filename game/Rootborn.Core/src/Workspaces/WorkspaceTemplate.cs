using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;

namespace Rootborn.Core.Workspaces
{
    /// <summary>템플릿의 가구 한 줄: 어느 가구를 어디에 어느 방향으로. 가구는 id로 가리킨다.</summary>
    public readonly struct TemplatePlacement
    {
        private readonly string _furnitureId;

        public TemplatePlacement(string furnitureId, GridPoint anchor, FurniturePlacementDirection direction)
        {
            _furnitureId = furnitureId;
            Anchor = anchor;
            Direction = direction;
        }

        /// <summary>default 값도 빈 문자열을 준다.</summary>
        public string FurnitureId => _furnitureId ?? string.Empty;
        public GridPoint Anchor { get; }
        public FurniturePlacementDirection Direction { get; }

        public bool IsValid => FurnitureId.Length > 0 && Enum.IsDefined(typeof(FurniturePlacementDirection), Direction);
    }

    /// <summary>
    /// 일터 하나에 돈을 내고 한꺼번에 까는 가구 묶음. 잘못된 값은 던지지 않고 <see cref="IsValid"/>로 알린다(로더가 읽기 오류로 모은다).
    /// </summary>
    public sealed class WorkspaceTemplate
    {
        private readonly ItemAmount<string>[] _costs;
        private readonly TemplatePlacement[] _placements;
        private readonly string[] _furnitureIds;

        public WorkspaceTemplate(string id, string workspaceId, string displayNameKey, ItemAmount<string>[] costs, TemplatePlacement[] placements)
        {
            Id = id ?? string.Empty;
            WorkspaceId = workspaceId ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            _costs = costs != null ? (ItemAmount<string>[])costs.Clone() : Array.Empty<ItemAmount<string>>();
            _placements = placements != null ? (TemplatePlacement[])placements.Clone() : Array.Empty<TemplatePlacement>();

            var ids = new List<string>(_placements.Length);
            for (int i = 0; i < _placements.Length; i++)
            {
                string furnitureId = _placements[i].FurnitureId;
                if (furnitureId.Length > 0 && !ids.Contains(furnitureId)) ids.Add(furnitureId);
            }
            _furnitureIds = ids.ToArray();
        }

        public string Id { get; }
        public string WorkspaceId { get; }
        public string DisplayNameKey { get; }
        public IReadOnlyList<ItemAmount<string>> Costs => _costs;
        public IReadOnlyList<TemplatePlacement> Placements => _placements;

        /// <summary>템플릿이 쓰는 가구 id(처음 나온 순서, 겹치지 않게).</summary>
        public IReadOnlyList<string> FurnitureIds => _furnitureIds;

        public bool IsValid
        {
            get
            {
                if (Id.Length == 0 || WorkspaceId.Length == 0) return false;
                for (int i = 0; i < _costs.Length; i++)
                    if (string.IsNullOrEmpty(_costs[i].Item) || _costs[i].Count <= 0) return false;
                for (int i = 0; i < _placements.Length; i++)
                    if (!_placements[i].IsValid) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// 그 면에 가구를 놓으면 기술이 자란다. 하루에 <see cref="DailyLimit"/>번까지만 센다.
    /// 음수 수치는 0으로 다듬고, 빈 id는 <see cref="IsValid"/>로 알린다.
    /// </summary>
    public sealed class PlacementSkillGrowth
    {
        public PlacementSkillGrowth(string id, string surfaceId, string skillId, int amount, int dailyLimit)
        {
            Id = id ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            SkillId = skillId ?? string.Empty;
            Amount = Math.Max(0, amount);
            DailyLimit = Math.Max(0, dailyLimit);
        }

        public string Id { get; }
        public string SurfaceId { get; }
        public string SkillId { get; }
        public int Amount { get; }
        public int DailyLimit { get; }

        public bool IsValid => Id.Length > 0 && SurfaceId.Length > 0 && SkillId.Length > 0;
    }
}
