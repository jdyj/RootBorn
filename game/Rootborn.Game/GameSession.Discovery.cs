using System;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Gathering;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game
{
    /// <summary>발견 지점, 도감 해금·본 표시, 자원 채집 명령.</summary>
    public sealed partial class GameSession
    {
        private Random _dropRandom = new Random();

        /// <summary>자원에서 나오는 물건 수를 굴리는 난수(Unity GatherInteractor처럼 기본은 시드 없는 난수). 테스트가 고정할 때 넘긴다.</summary>
        public void UseDropRandom(Random random) => _dropRandom = random ?? new Random();

        /// <summary>
        /// 발견 지점을 살핀다(Unity DiscoveryPointInteractor.Interact): Core DiscoveryRunner가 한 번만 적용하고, 적용되면 도감 해금을 평가한 뒤 저장한다.
        /// Unity 판처럼 퀘스트·캠페인에는 알리지 않는다.
        /// </summary>
        public DiscoveryResult Discover(string discoveryId)
        {
            _definitions.TryGet<Discovery>(discoveryId, out var discovery);
            bool applied = new DiscoveryRunner().TryDiscover(discovery, Student, out var result);
            if (applied) UnlockEncyclopediaEntries();
            return Saved(result, applied, null, GameChangeTopics.Student, GameChangeTopics.Discovery);
        }

        /// <summary>
        /// 조건을 만족한 도감 항목을 연다(Unity EncyclopediaProgressComponent.EvaluateUnlocks — Unity 판은 장면을 열 때와 발견 뒤에 불렀다).
        /// 연 항목이 있으면 저장한다. 연 항목 수를 돌려준다.
        /// </summary>
        public int EvaluateEncyclopediaUnlocks()
        {
            int unlocked = UnlockEncyclopediaEntries();
            if (unlocked > 0) SaveAndNotify(null, GameChangeTopics.Discovery);
            return unlocked;
        }

        /// <summary>도감 항목의 새 표시를 지우고 저장한다(Unity 도감 화면이 항목을 열 때). 열리지 않았거나 이미 본 항목이면 아무것도 바꾸지 않는다.</summary>
        public bool MarkEncyclopediaSeen(string entryId)
        {
            bool applied = Encyclopedia.MarkSeen(entryId);
            return Saved(applied, applied, null, GameChangeTopics.Discovery);
        }

        /// <summary>
        /// 자원을 한 번 친다(Unity GatherInteractor.DoInteract의 자원 부분). 힘 쌓기·부서짐·나온 물건은 Core ResourceGathering이 정한다.
        /// 친 때마다(부서지지 않아도) 일반 퀘스트 로그에 채집 이벤트를 남긴다 — Unity 판처럼 체인에는 알리지 않는다.
        /// state는 화면이 자원 하나마다 갖는다(Unity 판처럼 세이브에 들어가지 않는다). 도구가 없으면 맨손.
        /// </summary>
        public GatherHitResult GatherResource(string nodeId, ResourceNodeState state, string toolId = null, float toolPowerMultiplier = 1f)
        {
            if (!_definitions.TryGet<ResourceNodeDefinition>(nodeId, out var node)) return GatherHitResult.Ignored;
            var result = ResourceGathering.Hit(node, state, toolId, toolPowerMultiplier, Inventory, FindItem, _dropRandom);
            if (result == GatherHitResult.Ignored) return result;
            QuestLog.RecordEvent(new QuestEvent(QuestEventKind.Gather, _newEventKey("gather"), node.Id));
            SaveAndNotify(null, GameChangeTopics.Quests, GameChangeTopics.Inventory);
            return result;
        }

        private int UnlockEncyclopediaEntries()
            => EncyclopediaUnlocker.EvaluateUnlocks(_definitions.All<EncyclopediaEntry>(), new EncyclopediaUnlockContext(Student, null, Inventory), Encyclopedia, _utcNowTicks(), null);
    }
}
