using System;
using System.Collections.Generic;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Interaction;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.Time;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>지금 맵에 있는 NPC 하나와 그 순간의 자리.</summary>
    public readonly struct NpcOnMap
    {
        public readonly string NpcId;
        public readonly NpcPose Pose;

        public NpcOnMap(string npcId, NpcPose pose)
        {
            NpcId = npcId;
            Pose = pose;
        }
    }

    /// <summary>
    /// 걷는 NPC를 월드에 잇는 곳. 실행 프로그램(Host)과 창 없는 판이 같이 쓴다. NPC 자리는 Core <see cref="NpcWorld"/>가 (데이터, 날, 그날의 분)으로 정하고,
    /// 여기서는 지금 맵에 있는 NPC를 고르고 그 NPC의 상호작용 후보를 옮기고 켜고 끈다. NPC 후보는 만들 때 한 번 만들어 맵이 바뀌어도 같은 것을 쓴다.
    /// NpcWorld를 조립하는 곳은 <see cref="Create"/> 하나뿐이다.
    /// </summary>
    public sealed class NpcStage
    {
        private readonly NpcWorld _world;
        private readonly InteractionData _interactions;
        private readonly string[] _ids;
        private readonly NpcPose[] _poses;
        private readonly InteractionCandidate[] _candidates;
        private readonly NpcOnMap[] _onMap;
        private int _count;
        private string _mapId;

        public NpcStage(NpcWorld world, InteractionData interactions)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
            int n = world.Count;
            _ids = new string[n];
            _poses = new NpcPose[n];
            _candidates = new InteractionCandidate[n];
            _onMap = new NpcOnMap[n];
            for (int i = 0; i < n; i++)
            {
                _ids[i] = world.NpcId(i);
                _candidates[i] = new InteractionCandidate("npc", _ids[i], 0f, 0f, interactions.NpcPriority, true);
                _candidates[i].SetActive(false);
            }
        }

        /// <summary>
        /// 실제 데이터로 만든다: 일정, NPC 순서(data/registry.json npcs), 상호작용 데이터(NPC 자리), 맵 목록(자리를 찾는 맵 순서와 NPC 걷는 빠르기),
        /// 맵 원본(걷는 격자 — 가구 방이면 가구가 막는 칸을 더한 맵을 주는 원본), 시간 규칙.
        /// </summary>
        public static NpcStage Create(NpcScheduleLookupCache schedules, IReadOnlyList<string> npcIdsInOrder, InteractionData interactions,
                                      WorldCatalog catalog, IWorldMapSource maps, TimeRules rules)
        {
            if (interactions == null) throw new ArgumentNullException(nameof(interactions));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var mapIds = new List<string>(catalog.Maps.Count);
            foreach (MapEntry m in catalog.Maps) mapIds.Add(m.Id);
            var world = new NpcWorld(npcIdsInOrder, schedules, new MapAnchors(interactions, mapIds), maps, catalog.NpcWalker, rules);
            return new NpcStage(world, interactions);
        }

        /// <summary>지금 맵에 있는 NPC 수(마지막 Tick 기준).</summary>
        public int Count => _count;

        /// <summary>지금 맵에 있는 NPC(0..Count-1, NPC 순서).</summary>
        public NpcOnMap this[int index]
        {
            get
            {
                if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
                return _onMap[index];
            }
        }

        /// <summary>
        /// 맵에 들어섰을 때. 후보 목록을 한 번 만든다: 물건, NPC 모두(지금 맵에 없는 NPC는 꺼진 채로), 문. 이미 Tick했으면 그 자리로 바로 맞춘다.
        /// 돌려준 목록에 다른 후보(창고 등)를 더하는 것은 부르는 쪽 일이다.
        /// </summary>
        public List<InteractionCandidate> EnterMap(string mapId, MapInteractions here, IEnumerable<DoorArea> doors)
        {
            _mapId = mapId ?? string.Empty;
            MapInteractions map = here ?? MapInteractions.Empty;
            List<InteractionCandidate> list = NpcPlacement.Candidates(_interactions, map, doors);
            list.InsertRange(map.Things.Count, _candidates); // 물건 → NPC → 문(전과 같은 순서)
            Apply();
            return list;
        }

        /// <summary>
        /// 프레임마다. dayMinutes는 하루 시작(TimeRules.DayStartMinutes)부터 하루 끝까지의 축(그날 0시부터 센 분)이다. 날이 바뀌었으면 계획을 준비한다.
        /// 모든 NPC의 자리를 고치고, 지금 맵에 있는 NPC의 후보를 옮기고 켠다(없는 NPC의 후보는 끈다). 새 객체를 만들지 않는다.
        /// </summary>
        public void Tick(int day, double dayMinutes)
        {
            _world.Prepare(day);
            for (int i = 0; i < _poses.Length; i++) _poses[i] = _world.PoseOf(i, dayMinutes);
            Apply();
        }

        /// <summary>그 NPC의 마지막 Tick 자리(어느 맵에 있든). 어느 맵에도 없거나 모르는 NPC면 false.</summary>
        public bool TryPose(string npcId, out NpcPose pose)
        {
            for (int i = 0; i < _ids.Length; i++)
            {
                if (!string.Equals(_ids[i], npcId, StringComparison.Ordinal)) continue;
                pose = _poses[i];
                return pose.Present;
            }
            pose = NpcPose.Absent;
            return false;
        }

        /// <summary>가구가 바뀐 맵: 그 맵의 길과 격자, 계획을 버린다. 다음 Tick이 새 격자로 다시 만든다.</summary>
        public void ForgetMap(string mapId) => _world.ForgetMap(mapId);

        private void Apply()
        {
            _count = 0;
            for (int i = 0; i < _poses.Length; i++)
            {
                NpcPose pose = _poses[i];
                InteractionCandidate candidate = _candidates[i];
                bool here = pose.Present && _mapId != null && string.Equals(pose.MapId, _mapId, StringComparison.Ordinal);
                candidate.SetActive(here);
                if (!here) continue;
                candidate.MoveTo(pose.X, pose.Y);
                _onMap[_count++] = new NpcOnMap(_ids[i], pose);
            }
        }
    }
}
