using System;
using System.Collections.Generic;
using Rootborn.Core.Building;

namespace Rootborn.Screens.Furniture
{
    /// <summary>꾸밈 카드 하나: 정의, 쓸 수 있는가(GameSession.IsFinishUnlocked), 위·아래 그림 id(아래는 벽 카드만).</summary>
    public sealed class FinishCard
    {
        public FinishCard(RoomFinishDefinition definition, bool unlocked, string upperImage, string lowerImage)
        {
            Definition = definition;
            Unlocked = unlocked;
            UpperImage = upperImage;
            LowerImage = lowerImage;
        }

        public RoomFinishDefinition Definition { get; }
        public bool Unlocked { get; }
        /// <summary>카드 위(또는 하나뿐인) 그림 id("시트 PNG#그림 키/조각" — Host 좌표표가 푼다).</summary>
        public string UpperImage { get; }
        /// <summary>카드 아래 그림 id. 바닥 카드는 null.</summary>
        public string LowerImage { get; }
    }

    /// <summary>
    /// 꾸미기 분류의 표시 모델: 지금 자리(벽지 / 바닥)의 꾸밈 카드(데이터 순서, 잠긴 것 포함)와 쪽. GameSession 조회(HouseFinishOptions·
    /// IsFinishUnlocked)로 만든다 — 잠김 판단은 GameSession이 한다. 그림 id는 정의마다 처음 한 번만 만든다. 꾸밈 id로 분기하지 않는다.
    /// </summary>
    public sealed class FinishPaletteModel
    {
        private readonly int _pageSize;
        private readonly List<FinishCard> _cards = new List<FinishCard>(16);
        private readonly Dictionary<RoomFinishDefinition, (string Upper, string Lower)> _images = new Dictionary<RoomFinishDefinition, (string, string)>();

        public FinishPaletteModel(int pageSize)
        {
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            _pageSize = pageSize;
        }

        /// <summary>지금 자리(<see cref="RoomFinishSlots"/>의 글).</summary>
        public string Slot { get; private set; } = RoomFinishSlots.Wall;
        public int Page { get; private set; }
        public int Count => _cards.Count;
        public int PageCount => Math.Max(1, (_cards.Count + _pageSize - 1) / _pageSize);
        public bool HasPrevious => Page > 0;
        public bool HasNext => Page < PageCount - 1;
        public bool IsEmpty => _cards.Count == 0;

        /// <summary>그 자리의 카드를 다시 만든다. 자리가 바뀌면 첫 쪽으로, 같으면 쪽을 지킨다(잠김이 풀렸을 때).</summary>
        public void Set(string slot, IReadOnlyList<RoomFinishDefinition> options, Func<RoomFinishDefinition, bool> unlocked)
        {
            slot ??= string.Empty;
            if (!string.Equals(slot, Slot, StringComparison.Ordinal)) Page = 0;
            Slot = slot;
            _cards.Clear();
            if (options != null)
                foreach (RoomFinishDefinition d in options)
                {
                    if (d == null) continue;
                    (string upper, string lower) = ImagesOf(d);
                    _cards.Add(new FinishCard(d, unlocked == null || unlocked(d), upper, lower));
                }
            Page = Math.Min(Page, PageCount - 1);
        }

        /// <summary>지금 쪽의 i번째 카드(없으면 null).</summary>
        public FinishCard At(int i)
        {
            if (i < 0 || i >= _pageSize) return null;
            int index = Page * _pageSize + i;
            return index < _cards.Count ? _cards[index] : null;
        }

        public void ShowPage(int page) => Page = Math.Max(0, Math.Min(page, PageCount - 1));

        /// <summary>그 정의의 카드(쪽과 무관). 없으면 null(데이터가 바뀌었다).</summary>
        public FinishCard Find(RoomFinishDefinition definition)
        {
            foreach (FinishCard c in _cards) if (ReferenceEquals(c.Definition, definition)) return c;
            return null;
        }

        /// <summary>조각 그림 id: 시트 PNG#그림 키/조각(조각 좌표표의 이름 꼴).</summary>
        public static string ImageId(RoomFinishDefinition finish, string piece) => finish.Sheet + "#" + finish.Art + "/" + piece;

        // 벽 자리는 정면 윗조각과 아랫조각을 위아래로, 그 밖의 자리는 바닥 조각 하나.
        private (string, string) ImagesOf(RoomFinishDefinition d)
        {
            if (_images.TryGetValue(d, out var hit)) return hit;
            hit = string.Equals(d.Slot, RoomFinishSlots.Wall, StringComparison.Ordinal)
                ? (ImageId(d, RoomFinishPieces.FaceUpper), ImageId(d, RoomFinishPieces.FaceLower))
                : (ImageId(d, RoomFinishPieces.Floor), null);
            _images[d] = hit;
            return hit;
        }
    }
}
