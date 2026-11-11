using System;
using System.Collections.Generic;
using Rootborn.Core.Placement;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 가구 팔레트의 표시 모델(Unity InteriorFurniturePlacementPanel의 FilterPalette·ShowPalettePage): 놓을 수 있는 가구
    /// (GameSession.AvailableFurniture — 얻은 가구가 먼저) 중 검색어가 화면 이름이나 id에 들어간 것, 한 쪽에 PageSize개씩.
    /// 검색은 대소문자를 가리지 않는다. 쪽 번호는 0부터이고 범위 밖이면 끝 쪽으로 맞춘다. 목록을 다시 만드는 것은 검색어나 가구 목록이
    /// 바뀔 때뿐이고, 쪽을 넘길 때는 목록을 다시 만들지 않는다.
    /// </summary>
    public sealed class FurniturePaletteModel
    {
        private readonly Func<FurnitureDefinition, string> _name;
        private readonly List<FurnitureDefinition> _all = new List<FurnitureDefinition>(2048);
        private readonly List<FurnitureDefinition> _filtered = new List<FurnitureDefinition>(2048);

        /// <param name="name">가구의 화면 이름(지금 언어). 검색에 쓴다.</param>
        public FurniturePaletteModel(int pageSize, Func<FurnitureDefinition, string> name)
        {
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            PageSize = pageSize;
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public int PageSize { get; }
        public string Query { get; private set; } = string.Empty;
        public int Page { get; private set; }
        public int TotalCount => _all.Count;
        public int FilteredCount => _filtered.Count;
        public int PageCount => Math.Max(1, (_filtered.Count + PageSize - 1) / PageSize);
        public bool HasPrevious => Page > 0;
        public bool HasNext => Page + 1 < PageCount;
        public bool IsEmpty => _filtered.Count == 0;

        /// <summary>가구 목록을 바꾼다(열 때, 해금이 바뀔 때). 검색어는 그대로 두고 첫 쪽으로.</summary>
        public void SetFurniture(IReadOnlyList<FurnitureDefinition> available)
        {
            _all.Clear();
            if (available != null) foreach (FurnitureDefinition f in available) if (f != null) _all.Add(f);
            Filter(Query);
        }

        /// <summary>검색어를 바꾸고 첫 쪽으로(Unity와 같음).</summary>
        public void Filter(string query)
        {
            Query = (query ?? string.Empty).Trim();
            _filtered.Clear();
            foreach (FurnitureDefinition f in _all)
                if (Query.Length == 0 || Contains(_name(f), Query) || Contains(f.StableId, Query)) _filtered.Add(f);
            Page = 0;
        }

        public void ShowPage(int page) => Page = Math.Clamp(page, 0, PageCount - 1);

        /// <summary>지금 쪽의 칸 slot(0~PageSize-1)에 든 가구. 비었으면 null.</summary>
        public FurnitureDefinition At(int slot)
        {
            if (slot < 0 || slot >= PageSize) return null;
            int index = Page * PageSize + slot;
            return index < _filtered.Count ? _filtered[index] : null;
        }

        /// <summary>지금 쪽에 든 가구 수.</summary>
        public int CountOnPage => Math.Max(0, Math.Min(PageSize, _filtered.Count - Page * PageSize));

        private static bool Contains(string text, string query) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
