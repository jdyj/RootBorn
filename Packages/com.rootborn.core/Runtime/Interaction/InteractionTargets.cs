using System;
using System.Collections.Generic;

namespace Rootborn.Core.Interaction
{
    /// <summary>
    /// 상호작용 후보 하나: 종류(npc, daily-plan-board 등 데이터의 문자열), id, 맵 픽셀 좌표의 중심, 우선순위,
    /// 눌렀을 때 무언가 하는가(Acts). 자동문처럼 대상은 되지만 눌러도 아무것도 하지 않는 후보는 Acts가 false다.
    /// </summary>
    public sealed class InteractionCandidate
    {
        public readonly string Kind, Id;
        public readonly float X, Y;
        public readonly int Priority;
        public readonly bool Acts;

        public InteractionCandidate(string kind, string id, float x, float y, int priority, bool acts)
        {
            Kind = kind ?? string.Empty;
            Id = id ?? string.Empty;
            X = x;
            Y = y;
            Priority = priority;
            Acts = acts;
        }
    }

    /// <summary>무엇과 상호작용했는가. 대화창·활동 화면은 이것을 받아 연다(다음 프로젝트).</summary>
    public readonly struct InteractionEvent
    {
        public readonly string MapId, Kind, TargetId;

        public InteractionEvent(string mapId, string kind, string targetId)
        {
            MapId = mapId ?? string.Empty;
            Kind = kind ?? string.Empty;
            TargetId = targetId ?? string.Empty;
        }

        public override string ToString() => MapId + ":" + Kind + "/" + TargetId;
    }

    /// <summary>
    /// 플레이어 앞의 상호작용 대상(Unity PlayerInteractionRouter): 플레이어 중심에서 반경 안에 중심이 있는 후보 가운데
    /// <see cref="InteractionTargetRule"/>로 고른다. 바라보는 방향은 따지지 않는다(Unity 판도 그랬다).
    /// 후보는 맵에 들어설 때나 NPC 자리가 바뀔 때만 바꾼다(프레임마다 다시 만들지 않는다).
    /// </summary>
    public sealed class InteractionTargets
    {
        /// <summary>Unity PlayerInteractionRouter._interactionRadius 1.75유닛(1유닛 = 1칸).</summary>
        public const float UnityRadiusCells = 1.75f;

        private readonly List<InteractionCandidate> _candidates = new List<InteractionCandidate>();
        private float _radius;

        public string MapId { get; set; } = string.Empty;
        public InteractionCandidate Target { get; private set; }
        public IReadOnlyList<InteractionCandidate> Candidates => _candidates;
        public event Action<InteractionEvent> Interacted;

        public InteractionTargets(float radiusPixels)
        {
            RadiusPixels = radiusPixels;
        }

        /// <summary>반경(픽셀). 칸 크기가 다른 맵에 들어서면 바꾼다.</summary>
        public float RadiusPixels
        {
            get => _radius;
            set
            {
                if (value <= 0f) throw new ArgumentException("반경은 양수여야 한다");
                _radius = value;
            }
        }

        public void SetCandidates(IEnumerable<InteractionCandidate> candidates)
        {
            _candidates.Clear();
            if (candidates != null) foreach (InteractionCandidate c in candidates) if (c != null) _candidates.Add(c);
            Target = null;
        }

        /// <summary>플레이어 중심(픽셀)으로 대상을 다시 고른다.</summary>
        public InteractionCandidate Update(float playerX, float playerY)
        {
            InteractionCandidate best = null;
            int bestPriority = int.MinValue;
            float bestDistance = float.MaxValue;
            foreach (InteractionCandidate c in _candidates)
            {
                float dx = c.X - playerX, dy = c.Y - playerY;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                if (distance > _radius) continue;
                if (!InteractionTargetRule.IsBetter(c.Priority, distance, bestPriority, bestDistance)) continue;
                best = c;
                bestPriority = c.Priority;
                bestDistance = distance;
            }
            Target = best;
            return best;
        }

        /// <summary>상호작용 키를 눌렀다. 대상이 무언가 하면 이벤트를 내고 돌려준다.</summary>
        public InteractionEvent? Press()
        {
            if (Target == null || !Target.Acts) return null;
            var e = new InteractionEvent(MapId, Target.Kind, Target.Id);
            Interacted?.Invoke(e);
            return e;
        }
    }
}
