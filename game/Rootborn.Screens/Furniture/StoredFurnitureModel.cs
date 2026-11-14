using System;
using System.Collections.Generic;
using Rootborn.Core.Placement;

namespace Rootborn.Screens.Furniture
{
    /// <summary>보관 목록의 한 줄: 읽은 기록 안의 자리(<see cref="StoredFurniture.Index"/>), 가구, 저장된 방향, 물건이 들었는가.</summary>
    public sealed class StoredFurnitureRow
    {
        public StoredFurnitureRow(int index, FurnitureDefinition definition, FurniturePlacementDirection direction, bool holdsItems)
        {
            Index = index;
            Definition = definition;
            Direction = direction;
            HoldsItems = holdsItems;
        }

        public int Index { get; }
        public FurnitureDefinition Definition { get; }
        public FurniturePlacementDirection Direction { get; }
        public bool HoldsItems { get; }
    }

    /// <summary>
    /// 가구 배치 화면의 "보관" 분류가 그릴 값만 담은 표시 모델(GameSession.StoredFurniture 조회로 만든다). 규칙은 판단하지 않는다 —
    /// 칸에 놓을 수 있는지는 GameSession이 판단한다(같이 쓰는 집이어도 줄을 고를 수 있다: 놓으면 방에 요청한다).
    /// </summary>
    public sealed class StoredFurnitureModel
    {
        private StoredFurnitureModel(List<StoredFurnitureRow> rows)
        {
            Rows = rows;
        }

        public IReadOnlyList<StoredFurnitureRow> Rows { get; }
        public bool IsEmpty => Rows.Count == 0;

        public static StoredFurnitureModel Build(IReadOnlyList<StoredFurniture> stored, Func<FurnitureDefinition, string> name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            var rows = new List<StoredFurnitureRow>(stored?.Count ?? 0);
            if (stored != null)
                foreach (StoredFurniture s in stored) rows.Add(new StoredFurnitureRow(s.Index, s.Definition, s.Direction, s.HoldsItems));
            return new StoredFurnitureModel(rows);
        }

        /// <summary>그 기록 자리(Index)의 줄 번호. 없으면 -1.</summary>
        public int RowOf(int sourceIndex)
        {
            for (int i = 0; i < Rows.Count; i++) if (Rows[i].Index == sourceIndex) return i;
            return -1;
        }
    }
}
