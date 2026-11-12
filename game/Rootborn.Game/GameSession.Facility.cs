using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game
{
    /// <summary>시설 일 하나를 지금 할 수 있는지. 값이 Applied면 할 수 있고, 그 밖은 명령이 돌려줄 거절 이유다.</summary>
    public readonly struct FacilityWorkAvailability
    {
        public FacilityWorkAvailability(FacilityWork work, AdultCareerResultKind direct, AdultCareerResultKind autoSkip)
        {
            Work = work;
            Direct = direct;
            AutoSkip = autoSkip;
        }

        public FacilityWork Work { get; }

        /// <summary>직접 일하기(미니게임 있는 일은 예약, 없는 일은 바로 하기).</summary>
        public AdultCareerResultKind Direct { get; }

        /// <summary>자동 완료.</summary>
        public AdultCareerResultKind AutoSkip { get; }

        public bool CanWork => Direct == AdultCareerResultKind.Applied;
        public bool CanAutoSkip => AutoSkip == AdultCareerResultKind.Applied;
    }

    /// <summary>
    /// 시설 관리 화면이 단추마다 보일 "할 수 있는지와 못 하는 이유"(Unity FacilityManagementPanel.Refresh의 interactable들).
    /// 판정은 Core AdultCareerService·StudentMinigameService의 Can* 이 하고, 같은 상태에서 명령이 돌려줄 거절 이유와 같다.
    /// </summary>
    public sealed class FacilityAvailability
    {
        public FacilityAvailability(Facility facility, AdultCareerResultKind transition, AdultCareerResultKind upgrade, AdultCareerResultKind practice,
            IReadOnlyList<FacilityWorkAvailability> works)
        {
            Facility = facility;
            Transition = transition;
            Upgrade = upgrade;
            Practice = practice;
            Works = works ?? Array.Empty<FacilityWorkAvailability>();
        }

        public Facility Facility { get; }

        /// <summary>이 시설을 성인 진로로 고르기.</summary>
        public AdultCareerResultKind Transition { get; }

        /// <summary>시설 확장(지금 일하는 시설이 아니면 RequirementFailed).</summary>
        public AdultCareerResultKind Upgrade { get; }

        /// <summary>진로 체험(미니게임 진로 연습). 시설에 연습이 없으면 InvalidDefinition.</summary>
        public AdultCareerResultKind Practice { get; }

        /// <summary>시설의 일마다(정의 순서).</summary>
        public IReadOnlyList<FacilityWorkAvailability> Works { get; }

        public bool CanTransition => Transition == AdultCareerResultKind.Applied;
        public bool CanUpgrade => Upgrade == AdultCareerResultKind.Applied;
        public bool CanPractice => Practice == AdultCareerResultKind.Applied;
    }

    /// <summary>성인 시설 조회와 시설 방문.</summary>
    public sealed partial class GameSession
    {
        /// <summary>그 시설의 단추마다 할 수 있는지와 이유. 없는 시설이면 null. 아무것도 바꾸지 않는다.</summary>
        public FacilityAvailability FacilityStatus(string facilityId)
        {
            if (!_definitions.TryGet<Facility>(facilityId, out var facility)) return null;
            var inventory = CareerInventory;
            var works = new List<FacilityWorkAvailability>(facility.Works.Count);
            foreach (var work in facility.Works)
            {
                if (work == null) continue;
                // 명령(CompleteFacilityWork)과 같은 공간 이득을 넘겨, 이득 때문에 는 보상이 안 들어가면 조회도 같은 이유로 거절한다.
                var extras = WorkExtrasFor(work.Id);
                works.Add(new FacilityWorkAvailability(work,
                    _adultService.CanWork(work, Student, Adult, inventory, extras),
                    _adultService.CanAutoSkip(work, Student, Adult, inventory, extras)));
            }

            _definitions.TryGet<CareerPractice>(facility.PracticeId, out var practice);
            return new FacilityAvailability(facility,
                _adultService.CanTransition(facility, Student, CareerCandidates, Adult),
                _adultService.CanUpgrade(facility, Student, Adult, inventory),
                _minigames.CanBeginPractice(practice, Student, Adult),
                works);
        }

        /// <summary>이 시설을 고를 수 있는지(Applied면 된다).</summary>
        public AdultCareerResultKind CanChooseFacility(string facilityId)
        {
            _definitions.TryGet<Facility>(facilityId, out var facility);
            return _adultService.CanTransition(facility, Student, CareerCandidates, Adult);
        }

        /// <summary>지금 일하는 시설을 올릴 수 있는지(Applied면 된다).</summary>
        public AdultCareerResultKind CanUpgradeFacility()
        {
            _definitions.TryGet<Facility>(Adult.FacilityId, out var facility);
            return _adultService.CanUpgrade(facility, Student, Adult, CareerInventory);
        }

        /// <summary>
        /// 시설 관리 화면을 열었다(Unity CareerFacilityInteractor.TryInteract): 시설에 장소가 있으면 그 장소의 방문 이벤트("facility-visit")를
        /// 퀘스트·체인에 알리고 저장한다. 장소 방문 기록·캠페인·장소 상태는 건드리지 않는다(Unity 판과 같음 — VisitLocation과 다르다).
        /// 시설이 없거나 장소가 없으면 아무것도 하지 않고 false.
        /// </summary>
        public bool VisitFacility(string facilityId)
        {
            if (!_definitions.TryGet<Facility>(facilityId, out var facility) || string.IsNullOrEmpty(facility.LocationId)) return false;
            Announce(NewEvent(QuestEventKind.LocationVisit, "facility-visit", facility.LocationId), true);
            SaveAndNotify(AnnounceTopics);
            return true;
        }
    }
}
