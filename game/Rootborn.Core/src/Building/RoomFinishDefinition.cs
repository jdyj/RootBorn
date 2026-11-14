using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Building
{
    /// <summary>
    /// 꾸밈이 들어가는 자리 이름. 데이터의 <c>slot</c> 값과 같은 글이다(닫힌 종류가 아니다 — 새 자리는 데이터와 그리는 쪽이 정한다).
    /// 쓰는 곳은 "이 칸의 벽지인가 바닥인가"를 고르는 한 군데뿐이고, 꾸밈 id로 분기하지 않는다.
    /// </summary>
    public static class RoomFinishSlots
    {
        public const string Wall = "wall";
        public const string Floor = "floor";
    }

    /// <summary>
    /// 꾸밈 조각 이름 중 꾸밈 하나를 대표하는 것(시트 좌표표의 <c>&lt;Art&gt;/&lt;조각&gt;</c>에서 조각 쪽, 출처표의 글과 같다).
    /// 화면의 꾸밈 카드가 벽은 정면 윗조각·아랫조각, 바닥은 바닥 조각을 보인다. 닫힌 종류가 아니라 글 상수다.
    /// </summary>
    public static class RoomFinishPieces
    {
        /// <summary>벽 정면 윗조각(정면 두 칸 중 위).</summary>
        public const string FaceUpper = "face:2";
        /// <summary>벽 정면 아랫조각(바닥에 닿는 칸).</summary>
        public const string FaceLower = "face:1";
        /// <summary>바닥 조각(그림자 없는 바닥).</summary>
        public const string Floor = "floor";
    }

    /// <summary>
    /// 꾸밈(벽지·바닥 종류) 하나. 어느 시트(<see cref="Sheet"/>)의 어느 조각 묶음(<see cref="Art"/>)을 쓰는지는 정의가 말하고,
    /// 조각의 자리는 시트의 좌표표가 말한다. 비용은 칸당이다(값은 시험용). 해금 토큰이 비면 처음부터 쓸 수 있다.
    /// 잘못된 값은 던지지 않고 <see cref="IsValid"/>로 알린다(로더가 읽기 오류로 모은다).
    /// </summary>
    public sealed class RoomFinishDefinition
    {
        private readonly ItemAmount<string>[] _cost;

        public RoomFinishDefinition(string id, string slot, string nameKey, string sheet, string art, IReadOnlyList<ItemAmount<string>> cost, string unlockToken)
        {
            Id = id ?? string.Empty;
            Slot = slot ?? string.Empty;
            NameKey = nameKey ?? string.Empty;
            Sheet = sheet ?? string.Empty;
            Art = art ?? string.Empty;
            UnlockToken = unlockToken ?? string.Empty;
            _cost = new ItemAmount<string>[cost?.Count ?? 0];
            for (int i = 0; i < _cost.Length; i++) _cost[i] = cost[i];
        }

        public string Id { get; }
        public string Slot { get; }
        public string NameKey { get; }
        public string Sheet { get; }
        public string Art { get; }
        public string UnlockToken { get; }

        /// <summary>칸당 비용. 복사본이다.</summary>
        public IReadOnlyList<ItemAmount<string>> Cost => _cost;

        public bool IsValid
        {
            get
            {
                if (Id.Length == 0 || Slot.Length == 0 || Sheet.Length == 0 || Art.Length == 0) return false;
                foreach (var amount in _cost)
                    if (string.IsNullOrEmpty(amount.Item) || amount.Count <= 0) return false;
                return true;
            }
        }
    }
}
