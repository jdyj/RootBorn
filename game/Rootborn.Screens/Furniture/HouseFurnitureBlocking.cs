using System;
using System.Collections.Generic;
using Rootborn.Game;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 가구 방(가구를 놓는 면이 있는 맵)의 걷는 격자에 게임 한 판의 가구가 막는 칸을 맞춘다 — <b>플레이어가 지금 어느 맵에 있든</b>.
    /// NPC 자리를 정하는 입력은 (데이터, 날, 그날의 분)뿐이어야 해서, 집 밖에 있는 동안에도 집의 가구가 집 NPC의 길에 들어가야 한다
    /// (아니면 "마지막으로 집에 들어선 때"가 NPC 자리에 끼어 같이 하는 사람끼리 어긋난다). 실행 프로그램(Host)과 창 없는 판이 같이 쓴다.
    /// 막힌 칸이 실제로 바뀐 맵마다 blockingChanged(맵 id)를 부른다 — 부르는 쪽이 NPC 길을 버리고(ForgetMap) 그 프레임을 그리기 전에 NPC 자리를 다시 낸다.
    /// 가구 방은 맵 id가 아니라 맵 성질(면 id)로 고른다.
    /// </summary>
    public sealed class HouseFurnitureBlocking
    {
        private readonly Func<string, FurnitureRoom> _roomOf;
        private readonly (string MapId, string Surface)[] _houses;
        private readonly Action<string> _blockingChanged;

        /// <param name="roomOf">맵 id → 가구 방(가구 방이 아니면 null). 걷는 맵 원본과 같은 방을 돌려줘야 한다.</param>
        /// <param name="houseMaps">가구를 놓는 면이 있는 맵과 그 면 id, 맵 목록 순서.</param>
        /// <param name="blockingChanged">막힌 칸이 바뀐 맵 id를 받는다.</param>
        public HouseFurnitureBlocking(Func<string, FurnitureRoom> roomOf, IEnumerable<(string MapId, string Surface)> houseMaps, Action<string> blockingChanged)
        {
            _roomOf = roomOf ?? throw new ArgumentNullException(nameof(roomOf));
            if (houseMaps == null) throw new ArgumentNullException(nameof(houseMaps));
            _blockingChanged = blockingChanged ?? throw new ArgumentNullException(nameof(blockingChanged));
            var list = new List<(string, string)>();
            foreach (var h in houseMaps)
                if (!string.IsNullOrEmpty(h.MapId) && !string.IsNullOrEmpty(h.Surface)) list.Add(h);
            _houses = list.ToArray();
        }

        /// <summary>
        /// 게임 한 판을 이을 때(새 게임, 이어 하기, 방에 들어감): 집 배치를 아직 모르면 집 밖에서 읽어 둔다(<see cref="GameSession.PrepareHouse"/> —
        /// 지금 맵이 가구 방이면 그 방, 아니면 맵 목록에서 처음 나오는 집 면의 가구 방 — 일터 면은 건너뛴다). 그다음 <see cref="Apply"/>.
        /// </summary>
        public void Attach(GameSession session, string currentMapId)
        {
            if (session == null) return;
            if (session.House == null)
            {
                // 지금 맵이 가구 방이면 그 방부터, 다음은 맵 목록 순서. 일터 면은 GameSession이 읽지 않으므로 집 배치가 생길 때까지 다음 방으로 간다.
                foreach (var h in _houses)
                    if (string.Equals(h.MapId, currentMapId, StringComparison.OrdinalIgnoreCase)) { Prepare(session, h); break; }
                for (int i = 0; i < _houses.Length && session.House == null; i++) Prepare(session, _houses[i]);
            }
            Apply(session);
        }

        private void Prepare(GameSession session, (string MapId, string Surface) house)
        {
            FurnitureRoom room = _roomOf(house.MapId);
            if (room != null) session.PrepareHouse(room.Grid, house.Surface);
        }

        /// <summary>게임 한 판의 집 배치(<see cref="GameSession.House"/>)가 막는 칸을 그 면의 가구 방에 넣는다. 가구가 바뀌었다는 알림마다 부른다.</summary>
        public void Apply(GameSession session)
        {
            var layout = session?.House;
            if (layout == null) return;
            foreach (var h in _houses)
            {
                if (!string.Equals(h.Surface, layout.SurfaceId, StringComparison.Ordinal)) continue;
                FurnitureRoom room = _roomOf(h.MapId);
                if (room != null && room.SetFurnitureBlocking(layout.BlockingCells)) _blockingChanged(room.Map.Id);
            }
        }
    }
}
