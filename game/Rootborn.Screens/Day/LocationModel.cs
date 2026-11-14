using System;
using System.Collections.Generic;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.StudentLife;
using Rootborn.Game;

namespace Rootborn.Screens.Day
{
    public enum LocationRowKind { Activity, Offer }

    /// <summary>
    /// 장소 화면 목록의 한 줄: 장소 활동 또는 가게 물건(Unity LocationIdentityPanel.BindButtons — 활동 다음 물건).
    /// 활동: 이름 키, 시간 비용, 못 하는 이유(Core StudentActivityRefusal), 요구 조건. 물건: 이름(키 또는 그대로 쓰는 이름), 값, 가졌는지, 살 돈이 있는지.
    /// 물건은 가구와 잠긴 꾸밈(벽지·바닥) 둘 다다 — 어느 쪽인지는 줄 id가 가리키는 정의의 종류로 안다.
    /// </summary>
    public readonly struct LocationRow
    {
        public LocationRow(LocationRowKind kind, string id, string nameKey, string literalName, ActivityTimePolicy timePolicy,
            StudentActivityRefusalReason refusal, bool requirementsMet, int price, bool owned, bool affordable, int timeCostMinutes = 0)
        {
            TimeCostMinutes = System.Math.Max(0, timeCostMinutes);
            Kind = kind;
            Id = id ?? string.Empty;
            NameKey = nameKey ?? string.Empty;
            LiteralName = literalName ?? string.Empty;
            TimePolicy = timePolicy;
            Refusal = refusal;
            RequirementsMet = requirementsMet;
            Price = price;
            Owned = owned;
            Affordable = affordable;
        }

        public LocationRowKind Kind { get; }
        public string Id { get; }
        /// <summary>이름 현지화 키. 비었으면 LiteralName을 그대로 쓴다(가구 이름 키가 없는 데이터, Unity OfferName과 같음).</summary>
        public string NameKey { get; }
        public string LiteralName { get; }
        public ActivityTimePolicy TimePolicy { get; }
        /// <summary>활동이 걸리는 분(데이터 timeCostMinutes). 물건 줄은 0.</summary>
        public int TimeCostMinutes { get; }
        public StudentActivityRefusalReason Refusal { get; }
        public bool RequirementsMet { get; }
        public int Price { get; }
        public bool Owned { get; }
        public bool Affordable { get; }

        /// <summary>누를 수 있다(Unity 버튼 interactable과 같은 값).</summary>
        public bool Enabled => Kind == LocationRowKind.Activity ? Refusal == StudentActivityRefusalReason.None && RequirementsMet : !Owned && Affordable;
    }

    /// <summary>
    /// 장소 화면의 표시 모델(Unity LocationIdentityPanel이 그리던 값). 장소 id로 장소 정체성(LocationIdentity)을 찾아,
    /// 이름·설명·힌트·장소 상태·활동 줄·가게 물건 줄을 만든다. 판정은 Core(StudentActivityRefusal, 요구 조건, 가구 해금)를 그대로 쓴다.
    /// </summary>
    public sealed class LocationModel
    {
        public const string UnavailableKey = "life.location.unavailable";
        public const string HintsTitleKey = "life.location.hints", NoHintsKey = "life.location.no-hints", RoutePrefix = "life.route.";

        private LocationModel() { }

        public string LocationId { get; private set; }
        public bool HasIdentity { get; private set; }
        public string NameKey { get; private set; }
        public string DescriptionKey { get; private set; }
        /// <summary>힌트 줄 키(정의 순서). 없으면 빈 목록 — 화면이 "아직 없음" 문구를 붙인다.</summary>
        public IReadOnlyList<string> HintKeys { get; private set; }
        /// <summary>장소 상태 한 줄(이 장소에 들어왔을 때 GameSession.LastLocationState). 숨김이거나 다른 장소면 보이지 않는다.</summary>
        public bool ShowsState { get; private set; }
        public string StateNameKey { get; private set; }
        public string StateDescriptionKey { get; private set; }
        public IReadOnlyList<LocationRow> Rows { get; private set; }
        /// <summary>가게 첫 물건의 값을 치르는 물건을 지금 몇 개 가졌나. 가게가 아니면 -1(Unity OfferBalanceText).</summary>
        public int OfferBalance { get; private set; }

        /// <summary>그 장소에 들어갈 수 있나(Unity LocationActivityInteractor.CanInteract: 정체성이 있고 Core Location.CanVisit).</summary>
        public static bool CanOpen(GameSession session, string locationId) =>
            FindIdentity(session, locationId) != null && session.Definitions.TryGet<Location>(locationId, out Location location) && location.CanVisit(session.Student);

        public static LocationIdentity FindIdentity(GameSession session, string locationId)
        {
            if (string.IsNullOrEmpty(locationId)) return null;
            foreach (LocationIdentity identity in session.Definitions.All<LocationIdentity>())
                if (identity != null && identity.LocationId == locationId) return identity;
            return null;
        }

        public static LocationModel From(GameSession session, string locationId)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            LocationIdentity identity = FindIdentity(session, locationId);
            session.Definitions.TryGet<Location>(locationId ?? string.Empty, out Location location);
            StudentLifeProgress student = session.Student;

            var hints = new List<string>();
            if (identity != null)
                foreach (LocationHintEntry hint in identity.Hints)
                    hints.Add(string.IsNullOrEmpty(hint.DisplayKey) ? RoutePrefix + hint.GrowthRoute : hint.DisplayKey);

            var rows = new List<LocationRow>();
            if (identity != null)
                foreach (LocationActivity activity in identity.AvailableActivities)
                {
                    if (activity == null) continue;
                    rows.Add(new LocationRow(LocationRowKind.Activity, activity.Id, activity.DisplayNameKey, null, activity.TimePolicy,
                        StudentActivityRefusal.Explain(student, activity.TimePolicy, activity.EnergyCost, activity.FocusCost),
                        activity.HasSatisfiedRequirements(student), 0, false, false, activity.TimeCostMinutes));
                }

            IReadOnlyList<FurnitureDefinition> offers = string.IsNullOrEmpty(locationId) ? Array.Empty<FurnitureDefinition>() : session.FurnitureForSaleAt(locationId);
            foreach (FurnitureDefinition offer in offers)
            {
                bool owned = session.FurnitureUnlocks != null && session.FurnitureUnlocks.IsUnlocked(offer.UnlockToken);
                bool affordable = offer.PriceItem != null && session.Inventory.CountOf(offer.PriceItem) >= offer.Price;
                rows.Add(new LocationRow(LocationRowKind.Offer, offer.StableId, offer.DisplayNameKey, offer.DisplayName, null,
                    StudentActivityRefusalReason.None, true, offer.Price, owned, affordable));
            }

            // 잠긴 꾸밈(벽지·바닥)도 같은 물건 줄로, 가구 다음에. 가졌는지는 Core HouseFinishes.IsUnlocked(꾸미기 화면의 잠김과 같은 판단).
            IReadOnlyList<RoomFinishDefinition> finishes = string.IsNullOrEmpty(locationId) ? Array.Empty<RoomFinishDefinition>() : session.FinishesForSaleAt(locationId);
            ItemDefinition balanceItem = offers.Count > 0 ? offers[0].PriceItem : null;
            foreach (RoomFinishDefinition finish in finishes)
            {
                ItemDefinition priceItem = session.Definitions.TryGet<ItemDefinition>(finish.PriceItemId, out ItemDefinition found) ? found : null;
                balanceItem ??= priceItem;
                bool owned = session.IsFinishUnlocked(finish);
                bool affordable = priceItem != null && session.Inventory.CountOf(priceItem) >= finish.Price;
                rows.Add(new LocationRow(LocationRowKind.Offer, finish.Id, finish.NameKey, null, null,
                    StudentActivityRefusalReason.None, true, finish.Price, owned, affordable));
            }

            LocationStateSummaryModel state = session.LastLocationState;
            bool showsState = identity != null && location != null && state.LocationId == location.Id && state.PrimaryState != null
                && !state.IsHidden && !string.IsNullOrEmpty(state.DisplayName);

            return new LocationModel
            {
                LocationId = locationId ?? string.Empty,
                HasIdentity = identity != null,
                NameKey = location != null ? location.DisplayNameKey : string.Empty,
                DescriptionKey = identity != null ? identity.DescriptionKey : UnavailableKey,
                HintKeys = hints,
                ShowsState = showsState,
                StateNameKey = showsState ? state.DisplayName : string.Empty,
                StateDescriptionKey = showsState ? state.Description ?? string.Empty : string.Empty,
                Rows = rows,
                OfferBalance = balanceItem != null ? session.Inventory.CountOf(balanceItem) : -1,
            };
        }
    }
}
