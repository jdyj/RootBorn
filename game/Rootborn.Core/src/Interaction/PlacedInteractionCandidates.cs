using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;

namespace Rootborn.Core.Interaction
{
    /// <summary>
    /// 놓인 가구 가운데 조건에 맞는 것(예: 상자)마다 상호작용 후보를 만든다. Host와 창 없는 시험 세계가 같은 함수를 부른다.
    /// 가구가 바뀔 때만 부른다(프레임마다 아님).
    /// </summary>
    public static class PlacedInteractionCandidates
    {
        /// <summary>놓인 가구 후보의 우선순위: 맵 데이터의 물건·NPC보다 앞서지 않는다(같은 거리면 그쪽이 먼저).</summary>
        public const int Priority = 0;

        /// <summary>
        /// 대상 가구마다 후보 하나를 result에 더한다(result를 비우지 않는다). 자리는 차지한 칸 가운데의 평균, id는 놓인 것의 InstanceId.
        /// 차지한 칸이 없는 가구는 건너뛴다.
        /// </summary>
        /// <param name="kind">상호작용 종류 문자열(부르는 쪽이 정한다).</param>
        public static void Fill(IReadOnlyList<PlacedFurniture> placed, Func<FurnitureDefinition, bool> include, Func<GridPoint, AreaRect> cellArea,
            string kind, List<InteractionCandidate> result)
        {
            if (placed == null || include == null || cellArea == null || result == null) return;
            for (int i = 0; i < placed.Count; i++)
            {
                PlacedFurniture p = placed[i];
                if (p.OccupiedCells.Count == 0 || !include(p.Definition)) continue;
                float cx = 0f, cy = 0f;
                for (int c = 0; c < p.OccupiedCells.Count; c++)
                {
                    AreaRect a = cellArea(p.OccupiedCells[c]);
                    cx += a.CenterX;
                    cy += a.CenterY;
                }
                result.Add(new InteractionCandidate(kind, p.InstanceId, cx / p.OccupiedCells.Count, cy / p.OccupiedCells.Count, Priority, true));
            }
        }
    }
}
