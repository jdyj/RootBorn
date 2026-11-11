using System;
using System.Collections.Generic;

namespace Rootborn.Core.World
{
    /// <summary>문을 지나 도착할 맵과 그 맵의 도착 지점 id.</summary>
    public readonly struct TravelTarget : IEquatable<TravelTarget>
    {
        public readonly string MapId, SpawnId;

        public TravelTarget(string mapId, string spawnId)
        {
            MapId = mapId ?? string.Empty;
            SpawnId = spawnId ?? string.Empty;
        }

        public bool Equals(TravelTarget other) => MapId == other.MapId && SpawnId == other.SpawnId;
        public override bool Equals(object obj) => obj is TravelTarget other && Equals(other);
        public override int GetHashCode() => MapId.GetHashCode() ^ (SpawnId.GetHashCode() * 31);
        public override string ToString() => MapId + "/" + SpawnId;
    }

    /// <summary>문 하나: 맵 원본의 목적지 값(Where/Door), 닿으면 반응하는 상자, 풀린 목적지(모르면 null).</summary>
    public sealed class DoorArea
    {
        public readonly string Target;
        public readonly AreaRect Area;
        public readonly TravelTarget? Destination;

        public DoorArea(string target, AreaRect area, TravelTarget? destination)
        {
            Target = target ?? string.Empty;
            Area = area;
            Destination = destination;
        }

        /// <summary>
        /// LDtk Door 엔티티(왼쪽 위 px, 너비, 높이)로 문 상자를 만든다. Unity 판처럼 가운데는 그대로 두고
        /// 너비는 0.75칸, 높이는 1칸보다 작아지지 않게 넓힌다.
        /// </summary>
        public static DoorArea FromEntity(float x, float y, float width, float height, string target, int cellSize, TravelTarget? destination)
        {
            float w = Math.Max(0.75f * cellSize, width), h = Math.Max(1f * cellSize, height);
            return new DoorArea(target, AreaRect.Centered(x + width / 2f, y + height / 2f, w, h), destination);
        }
    }

    /// <summary>
    /// 문 값을 목적지로 바꾸는 표. 값은 앞뒤 공백을 지우고 소문자로, '-'는 공백으로 바꿔 비교한다(Unity 판 Normalize).
    /// 문 엔티티의 도착 지점 칸(Position·SpawnPointID)에 값이 있으면 표의 도착 지점 대신 그것을 쓴다.
    /// </summary>
    public sealed class DoorRoutes
    {
        private readonly Dictionary<string, TravelTarget> _routes = new Dictionary<string, TravelTarget>(StringComparer.Ordinal);

        public DoorRoutes(IEnumerable<KeyValuePair<string, TravelTarget>> routes)
        {
            if (routes == null) return;
            foreach (var pair in routes)
            {
                string key = Normalize(pair.Key);
                if (key.Length == 0) continue;
                _routes[key] = pair.Value;
            }
        }

        public static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant().Replace('-', ' ');

        public TravelTarget? Resolve(string where, string spawnOverride)
        {
            string key = Normalize(where);
            if (key.Length == 0 || !_routes.TryGetValue(key, out TravelTarget target)) return null;
            string spawn = (spawnOverride ?? string.Empty).Trim();
            return spawn.Length > 0 ? new TravelTarget(target.MapId, spawn) : target;
        }
    }

    /// <summary>
    /// 문에 들어서는 것을 지켜본다. 몸 상자가 문 상자에 새로 겹치는 순간(Unity 판 OnTriggerEnter2D) 문이 열리기 시작하고,
    /// 정해진 시간(Unity 판 0.35초)이 지나면 그 문을 돌려준다. 열리기 시작한 뒤에는 걸어 나가도 이어진다.
    /// 도착했을 때 이미 겹쳐 있던 문은 한 번 나갔다 다시 들어와야 반응한다.
    /// skin만큼 떨어져 있어도 겹친 것으로 친다: Unity 판 물리(Box2D)는 모양마다 0.01유닛 껍질이 있어 벽에 붙어 선 몸이 벽과 같은 선의 문 상자에 닿았다.
    /// </summary>
    public sealed class DoorWatch
    {
        public const float UnityOpeningSeconds = 0.35f;
        /// <summary>Unity 판 Physics2D 기본 접촉 여유 0.01유닛 × 두 모양.</summary>
        public const float UnityContactSkinCells = 0.02f;
        private readonly IReadOnlyList<DoorArea> _doors;
        private readonly bool[] _inside;
        private readonly float _openSeconds, _skin;
        private float _elapsed;

        public DoorArea Opening { get; private set; }

        public DoorWatch(IReadOnlyList<DoorArea> doors, float openSeconds = UnityOpeningSeconds, float skin = 0f)
        {
            _doors = doors ?? Array.Empty<DoorArea>();
            _inside = new bool[_doors.Count];
            _openSeconds = Math.Max(0f, openSeconds);
            _skin = Math.Max(0f, skin);
        }

        private bool Touches(DoorArea door, AreaRect body) =>
            door.Area.Overlaps(new AreaRect(body.X - _skin, body.Y - _skin, body.W + 2 * _skin, body.H + 2 * _skin));

        public void Reset(AreaRect body)
        {
            Opening = null;
            _elapsed = 0f;
            for (int i = 0; i < _doors.Count; i++) _inside[i] = Touches(_doors[i], body);
        }

        public DoorArea Update(AreaRect body, float dt)
        {
            for (int i = 0; i < _doors.Count; i++)
            {
                bool now = Touches(_doors[i], body);
                if (now && !_inside[i] && Opening == null && _doors[i].Destination.HasValue)
                {
                    Opening = _doors[i];
                    _elapsed = 0f;
                    dt = 0f; // 들어선 프레임부터 센다
                }
                _inside[i] = now;
            }
            if (Opening == null) return null;
            _elapsed += Math.Max(0f, dt);
            if (_elapsed + 1e-5f < _openSeconds) return null;
            DoorArea done = Opening;
            Opening = null;
            _elapsed = 0f;
            return done;
        }
    }
}
