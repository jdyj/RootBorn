using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Game;

namespace Rootborn.Screens.AdultFacility
{
    /// <summary>일 한 줄의 표시 값. 숙련·성공 횟수·걸리는 분·최대 보상은 조회일 뿐이고, 할 수 있는지는 명령이 거절 이유로 말한다.</summary>
    public sealed class FacilityWorkRow
    {
        public FacilityWorkRow(string id, string nameKey, int minutes, int mastery, int maximumMastery, int successes, ItemAmount<string>[] maximumRewards,
            int rewardPercentBonus = 0, int reputationBonus = 0)
        {
            Id = id; NameKey = nameKey; Minutes = minutes; Mastery = mastery; MaximumMastery = maximumMastery; Successes = successes; MaximumRewards = maximumRewards;
            RewardPercentBonus = rewardPercentBonus; ReputationBonus = reputationBonus;
        }

        public string Id { get; }
        public string NameKey { get; }
        /// <summary>일이 걸리는 분(데이터 timeCostMinutes). 시간 포인트는 보이지 않는다(흐르는 시간 설계 4절).</summary>
        public int Minutes { get; }
        public int Mastery { get; }
        public int MaximumMastery { get; }
        public int Successes { get; }
        /// <summary>점수 100일 때 지금 시설 단계에서 받는 보상(Core FacilityWork.TryBuildRewards).</summary>
        public ItemAmount<string>[] MaximumRewards { get; }
        /// <summary>지금 일터 꾸미기로 받는 이득(GameSession.WorkExtrasFor): 보상 비율 +%, 명성 +. 둘 다 0이면 화면은 이득 줄을 숨긴다.</summary>
        public int RewardPercentBonus { get; }
        public int ReputationBonus { get; }
        public bool HasBonus => RewardPercentBonus > 0 || ReputationBonus > 0;
    }

    /// <summary>
    /// 성인 시설 관리 화면의 표시 모델(Unity FacilityManagementPanel.Refresh): 시설 한 곳의 제목, 단계·평판, 일 목록, 다음 단계 비용.
    /// GameSession의 값만 읽는다. 가능 여부는 판단하지 않는다.
    /// </summary>
    public sealed class FacilityModel
    {
        public static readonly FacilityModel Empty = new FacilityModel();

        private FacilityModel() { TitleKey = string.Empty; Works = Array.Empty<FacilityWorkRow>(); NextCosts = Array.Empty<ItemAmount<string>>(); }

        public bool Exists { get; private set; }
        public string FacilityId { get; private set; } = string.Empty;
        public string PracticeId { get; private set; } = string.Empty;
        /// <summary>이 시설이 지금 성인 진로로 골라져 있는가.</summary>
        public bool Selected { get; private set; }
        public string TitleKey { get; private set; }
        public int Level { get; private set; }
        public int Reputation { get; private set; }
        public bool RouteComplete { get; private set; }
        /// <summary>끝나지 않은 일이 예약돼 있는가(미니게임 판이 남아 있다).</summary>
        public bool HasPendingWork { get; private set; }
        /// <summary>다음 단계가 있으면 그 번호(없으면 0)와 올리는 비용.</summary>
        public int NextLevel { get; private set; }
        public ItemAmount<string>[] NextCosts { get; private set; }
        public FacilityWorkRow[] Works { get; private set; }

        public static FacilityModel From(GameSession session, string facilityId)
        {
            if (session == null || string.IsNullOrEmpty(facilityId) || !session.Definitions.TryGet(facilityId, out Facility facility)) return Empty;
            AdultCareerProgress adult = session.Adult;
            bool selected = adult.IsAdult && adult.FacilityId == facility.Id;
            FacilityLevel current = selected ? facility.GetLevel(adult.FacilityLevel) : null;
            FacilityLevel next = selected ? facility.GetLevel(adult.FacilityLevel + 1) : null;
            var model = new FacilityModel
            {
                Exists = true, FacilityId = facility.Id, PracticeId = facility.PracticeId, Selected = selected,
                TitleKey = current != null ? current.DisplayNameKey : facility.DisplayNameKey,
                Level = selected ? adult.FacilityLevel : 0, Reputation = selected ? adult.Reputation : 0,
                RouteComplete = selected && adult.HasEnding(facility) && current != null && current.CompletesRoute,
                HasPendingWork = adult.HasPendingWork,
                NextLevel = next != null ? next.Level : 0,
                NextCosts = next != null ? next.BuildCosts() : Array.Empty<ItemAmount<string>>(),
            };
            // 아직 고르지 않았으면 1단계 보상 비율로 보여 준다(고른 뒤 처음 받을 몫).
            int percent = (current ?? facility.GetLevel(1))?.RewardPercent ?? 100;
            var rows = new List<FacilityWorkRow>(facility.Works.Count);
            foreach (FacilityWork work in facility.Works)
            {
                if (work == null) continue;
                work.TryBuildRewards(100, percent, out ItemAmount<string>[] rewards);
                WorkExtras extras = session.WorkExtrasFor(work.Id);
                rows.Add(new FacilityWorkRow(work.Id, work.DisplayNameKey, work.TimeCostMinutes, adult.GetMastery(work), work.MaximumMastery, adult.GetSuccessfulCompletions(work), rewards,
                    extras.RewardPercentBonus, extras.ReputationBonus));
            }
            model.Works = rows.ToArray();
            return model;
        }

        /// <summary>명령 결과 문구 키. 결과 종류 하나에 키 하나(새 결과는 문구표에 키 하나로 더한다).</summary>
        public static string ResultKey(AdultCareerResultKind kind) => "facility.result." + kind;
    }
}
