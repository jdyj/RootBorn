using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.Interaction;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Data;
using Rootborn.Game;

namespace Rootborn.Simulation.Pace
{
    /// <summary>한 걸음 뒤에 새로운 것을 찾는 재료: 떠 있는 화면(HUD 빼고), 맵, 이 걸음의 상호작용, 미니게임 도전 종류, 게임 한 판(없으면 null).</summary>
    public sealed class PaceProbe
    {
        public readonly List<string> OpenScreens = new List<string>(8);
        public string MapId = string.Empty;
        public readonly List<InteractionEvent> Interactions = new List<InteractionEvent>(2);
        public string MinigameMechanic;
        public GameSession Session;
        public GameDefinitions Definitions;
    }

    /// <summary>새로운 것의 한 종류. 판마다 새로 만들어 자기 기억(본 것)을 가진다. Milestone이면 "지점까지 걸린 시간" 표에도 나온다.</summary>
    public interface IPaceNoveltyKind
    {
        string Name { get; }
        bool Milestone { get; }
        void Collect(PaceProbe probe, List<PaceNovelty> found);
    }

    /// <summary>"처음 본 것" 종류의 바탕: 무엇을 처음 봤을 때 한 번 사건을 낸다.</summary>
    public abstract class FirstSeenKind : IPaceNoveltyKind
    {
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        protected FirstSeenKind(string name, bool milestone) { Name = name; Milestone = milestone; }

        public string Name { get; }
        public bool Milestone { get; }

        public void Collect(PaceProbe probe, List<PaceNovelty> found)
        {
            foreach (string what in Look(probe))
                if (!string.IsNullOrEmpty(what) && _seen.Add(what)) found.Add(new PaceNovelty(Name, what, Milestone));
        }

        /// <summary>지금 보이는 것들(본 적 있는 것도 내도 된다).</summary>
        protected abstract IEnumerable<string> Look(PaceProbe probe);
    }

    /// <summary>
    /// 새로운 것의 종류 등록 표. 새 종류는 클래스 하나와 Register에 한 줄. 종류는 데이터·화면 등록·GameSession 조회로 얻는 것만 쓰고
    /// 엔티티 id로 분기하지 않는다(무엇인지는 id 그대로 기록에 남길 뿐).
    /// </summary>
    public sealed class PaceNoveltyCatalog
    {
        public const string FirstScreen = "처음 연 화면", FirstMap = "처음 들어간 맵", FirstTarget = "처음 상호작용한 대상", FirstMinigame = "처음 한 미니게임 종류",
            FirstActivity = "처음 한 활동", FirstItem = "처음 가진 아이템", ChainStep = "체인 단계 넘어감", QuestDone = "퀘스트 끝", CareerHint = "진로 힌트",
            ChainEnd = "체인 끝", CampaignDay = "캠페인 날", CandidateOpened = "진로 후보 열림", Adult = "성인이 됨", FacilityLevel = "시설 레벨";

        private readonly List<Func<IPaceNoveltyKind>> _make = new List<Func<IPaceNoveltyKind>>();

        public PaceNoveltyCatalog() { Register(this); }

        public static void Register(PaceNoveltyCatalog r)
        {
            r.Add(() => new Seen(FirstScreen, false, p => p.OpenScreens));
            r.Add(() => new Seen(FirstMap, false, p => new[] { p.MapId }));
            r.Add(() => new Seen(FirstTarget, false, Targets));
            r.Add(() => new Seen(FirstMinigame, false, p => new[] { p.MinigameMechanic }));
            r.Add(() => new Seen(FirstActivity, false, p => p.Session?.Student.GetActivityLogIds() ?? Array.Empty<string>()));
            r.Add(() => new Seen(FirstItem, false, Items));
            r.Add(() => new Seen(ChainStep, false, ChainSteps));
            r.Add(() => new Seen(QuestDone, false, QuestsDone));
            r.Add(() => new Seen(CareerHint, false, p => p.Session?.Student.GetCareerHintIds() ?? Array.Empty<string>()));
            // 지점(설계 3.4절): 데이터에서 저절로 — 각 체인의 끝, 캠페인의 각 날, 성인이 됨, 시설 레벨마다, 진로 후보가 열림.
            r.Add(() => new Seen(ChainEnd, true, ChainsDone));
            r.Add(() => new Seen(CampaignDay, true, p => p.Session?.Campaign == null ? Array.Empty<string>() : new[] { p.Session.Campaign.CurrentDayNumber.ToString(CultureInfo.InvariantCulture) + "일째" }));
            r.Add(() => new Seen(CandidateOpened, true, Candidates));
            r.Add(() => new Seen(Adult, true, p => p.Session?.Adult != null && p.Session.Adult.IsAdult ? new[] { p.Session.Adult.FacilityId } : Array.Empty<string>()));
            r.Add(() => new Seen(FacilityLevel, true, p => p.Session?.Adult != null && p.Session.Adult.IsAdult
                ? new[] { p.Session.Adult.FacilityId + " 레벨 " + p.Session.Adult.FacilityLevel.ToString(CultureInfo.InvariantCulture) } : Array.Empty<string>()));
        }

        public void Add(Func<IPaceNoveltyKind> make) => _make.Add(make ?? throw new ArgumentNullException(nameof(make)));

        /// <summary>판 하나를 위한 새 종류들(기억이 비어 있다).</summary>
        public List<IPaceNoveltyKind> Create()
        {
            var kinds = new List<IPaceNoveltyKind>(_make.Count);
            foreach (var make in _make) kinds.Add(make());
            return kinds;
        }

        /// <summary>함수 하나로 "지금 보이는 것"을 내는 종류(대부분 이것으로 충분하다).</summary>
        private sealed class Seen : FirstSeenKind
        {
            private readonly Func<PaceProbe, IEnumerable<string>> _look;
            public Seen(string name, bool milestone, Func<PaceProbe, IEnumerable<string>> look) : base(name, milestone) { _look = look; }
            protected override IEnumerable<string> Look(PaceProbe probe) => _look(probe);
        }

        private static IEnumerable<string> Targets(PaceProbe p)
        {
            foreach (InteractionEvent e in p.Interactions) yield return e.Kind + "/" + e.TargetId;
        }

        private static IEnumerable<string> Items(PaceProbe p)
        {
            if (p.Session == null) yield break;
            foreach (var slot in p.Session.Inventory.Slots) if (slot.Item != null) yield return slot.Item.Id;
        }

        private static IEnumerable<string> ChainSteps(PaceProbe p)
        {
            if (p.Session == null) yield break;
            foreach (QuestChainProgressSaveData c in p.Session.QuestChains.ToSaveData().Chains)
                foreach (string step in c.CompletedStepIds) yield return c.ChainId + " / " + step;
        }

        private static IEnumerable<string> ChainsDone(PaceProbe p)
        {
            if (p.Session == null) yield break;
            foreach (QuestChainProgressSaveData c in p.Session.QuestChains.ToSaveData().Chains)
                if (c.ChainState == QuestChainState.Completed.ToString()) yield return c.ChainId;
        }

        private static IEnumerable<string> QuestsDone(PaceProbe p)
        {
            if (p.Session == null) yield break;
            foreach (QuestProgressSaveData q in p.Session.QuestLog.ToSaveData().Quests)
                if (q.State == QuestStage.Completed || q.State == QuestStage.RewardClaimed) yield return q.QuestId;
        }

        private static IEnumerable<string> Candidates(PaceProbe p)
        {
            if (p.Session == null || p.Definitions == null) yield break;
            foreach (CareerCandidate c in p.Definitions.All<CareerCandidate>())
                if (c.GetState(p.Session.CareerCandidates) != CareerCandidateState.Locked) yield return c.Id;
        }
    }
}
