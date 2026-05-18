using System;
using System.Collections.Generic;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;

namespace Rootborn.Game.Common
{
    public sealed class GameDataLookupCache
    {
        private readonly Dictionary<string, LocationDefinition> _locations = new Dictionary<string, LocationDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationIdentityDefinition> _locationIdentities = new Dictionary<string, LocationIdentityDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, LocationActivityDefinition> _locationActivities = new Dictionary<string, LocationActivityDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, DailyEventDefinition> _dailyEvents = new Dictionary<string, DailyEventDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, DailyEventKindDefinition> _dailyEventKinds = new Dictionary<string, DailyEventKindDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, NpcDefinition> _npcs = new Dictionary<string, NpcDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, TimeSlotDefinition> _timeSlots = new Dictionary<string, TimeSlotDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, NpcScheduleDefinition> _npcSchedules = new Dictionary<string, NpcScheduleDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, CampaignDefinition> _campaigns = new Dictionary<string, CampaignDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, CampaignDayDefinition> _campaignDays = new Dictionary<string, CampaignDayDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, CampaignRouteDefinition> _campaignRoutes = new Dictionary<string, CampaignRouteDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, CareerCandidateDefinition> _careerCandidates = new Dictionary<string, CareerCandidateDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, CareerHintDefinition> _careerHints = new Dictionary<string, CareerHintDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, CareerCandidateRouteDefinition> _careerCandidateRoutes = new Dictionary<string, CareerCandidateRouteDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, CareerInterestDefinition> _careerInterests = new Dictionary<string, CareerInterestDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, WorldStateFlagDefinition> _worldStateFlags = new Dictionary<string, WorldStateFlagDefinition>(StringComparer.Ordinal);

        public GameDataLookupCache(GameDataRegistry registry)
        {
            BuildCount = 1;
            AddLocations(registry != null ? registry.Locations : null);
            AddLocationIdentities(registry != null ? registry.LocationIdentities : null);
            AddLocationActivities(registry != null ? registry.LocationActivities : null);
            AddDailyEventKinds(registry != null ? registry.DailyEventKinds : null);
            AddDailyEvents(registry != null ? registry.DailyEvents : null);
            AddNpcs(registry != null ? registry.Npcs : null);
            AddTimeSlots(registry != null ? registry.TimeSlots : null);
            AddNpcSchedules(registry != null ? registry.NpcSchedules : null);
            AddCampaigns(registry != null ? registry.Campaigns : null);
            AddCampaigns(GameDataRegistryCampaignExtensions.GetCampaigns(registry));
            AddCareerCandidates(registry != null ? registry.CareerCandidates : null);
            AddCareerHints(registry != null ? registry.CareerHints : null);
            AddCareerCandidateRoutes(registry != null ? registry.CareerCandidateRoutes : null);
            AddCareerInterests(registry != null ? registry.CareerInterests : null);
            AddWorldStateFlags(GameDataRegistryWorldStateExtensions.GetWorldStateFlags(registry));
        }

        public int BuildCount { get; }

        public bool TryGetLocation(string id, out LocationDefinition location) => _locations.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out location);
        public bool TryGetLocationIdentity(string locationId, out LocationIdentityDefinition identity) => _locationIdentities.TryGetValue(string.IsNullOrEmpty(locationId) ? string.Empty : locationId, out identity);
        public bool TryGetLocationActivity(string id, out LocationActivityDefinition activity) => _locationActivities.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out activity);
        public bool TryGetDailyEvent(string id, out DailyEventDefinition dailyEvent) => _dailyEvents.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out dailyEvent);
        public bool TryGetDailyEventKind(string id, out DailyEventKindDefinition kind) => _dailyEventKinds.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out kind);
        public bool TryGetNpc(string id, out NpcDefinition npc) => _npcs.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out npc);
        public bool TryGetTimeSlot(string id, out TimeSlotDefinition timeSlot) => _timeSlots.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out timeSlot);
        public bool TryGetNpcSchedule(string npcId, out NpcScheduleDefinition schedule) => _npcSchedules.TryGetValue(string.IsNullOrEmpty(npcId) ? string.Empty : npcId, out schedule);
        public bool TryGetCampaign(string id, out CampaignDefinition campaign) => _campaigns.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out campaign);
        public bool TryGetCampaignDay(string id, out CampaignDayDefinition day) => _campaignDays.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out day);
        public bool TryGetCampaignRoute(string id, out CampaignRouteDefinition route) => _campaignRoutes.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out route);
        public bool TryGetCareerCandidate(string id, out CareerCandidateDefinition candidate) => _careerCandidates.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out candidate);
        public bool TryGetCareerHint(string id, out CareerHintDefinition hint) => _careerHints.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out hint);
        public bool TryGetCareerCandidateRoute(string id, out CareerCandidateRouteDefinition route) => _careerCandidateRoutes.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out route);
        public bool TryGetCareerInterest(string id, out CareerInterestDefinition interest) => _careerInterests.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out interest);
        public bool TryGetWorldStateFlag(string id, out WorldStateFlagDefinition flag) => _worldStateFlags.TryGetValue(string.IsNullOrEmpty(id) ? string.Empty : id, out flag);

        public LocationDefinition GetLocationOrNull(string id) { TryGetLocation(id, out var location); return location; }
        public LocationIdentityDefinition GetLocationIdentityOrNull(string locationId) { TryGetLocationIdentity(locationId, out var identity); return identity; }
        public LocationActivityDefinition GetLocationActivityOrNull(string id) { TryGetLocationActivity(id, out var activity); return activity; }
        public DailyEventDefinition GetDailyEventOrNull(string id) { TryGetDailyEvent(id, out var dailyEvent); return dailyEvent; }
        public DailyEventKindDefinition GetDailyEventKindOrNull(string id) { TryGetDailyEventKind(id, out var kind); return kind; }
        public NpcDefinition GetNpcOrNull(string id) { TryGetNpc(id, out var npc); return npc; }
        public TimeSlotDefinition GetTimeSlotOrNull(string id) { TryGetTimeSlot(id, out var timeSlot); return timeSlot; }
        public NpcScheduleDefinition GetNpcScheduleOrNull(string npcId) { TryGetNpcSchedule(npcId, out var schedule); return schedule; }
        public CampaignDefinition GetCampaignOrNull(string id) { TryGetCampaign(id, out var campaign); return campaign; }
        public CampaignDayDefinition GetCampaignDayOrNull(string id) { TryGetCampaignDay(id, out var day); return day; }
        public CampaignRouteDefinition GetCampaignRouteOrNull(string id) { TryGetCampaignRoute(id, out var route); return route; }
        public CareerCandidateDefinition GetCareerCandidateOrNull(string id) { TryGetCareerCandidate(id, out var candidate); return candidate; }
        public CareerHintDefinition GetCareerHintOrNull(string id) { TryGetCareerHint(id, out var hint); return hint; }
        public CareerCandidateRouteDefinition GetCareerCandidateRouteOrNull(string id) { TryGetCareerCandidateRoute(id, out var route); return route; }
        public CareerInterestDefinition GetCareerInterestOrNull(string id) { TryGetCareerInterest(id, out var interest); return interest; }
        public WorldStateFlagDefinition GetWorldStateFlagOrNull(string id) { TryGetWorldStateFlag(id, out var flag); return flag; }

        private void AddLocations(LocationDefinition[] locations) { if (locations == null) return; for (int i = 0; i < locations.Length; i++) { var value = locations[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _locations[value.Id] = value; } }
        private void AddLocationIdentities(LocationIdentityDefinition[] identities) { if (identities == null) return; for (int i = 0; i < identities.Length; i++) { var value = identities[i]; if (value != null && !string.IsNullOrEmpty(value.LocationId)) _locationIdentities[value.LocationId] = value; } }
        private void AddLocationActivities(LocationActivityDefinition[] activities) { if (activities == null) return; for (int i = 0; i < activities.Length; i++) { var value = activities[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _locationActivities[value.Id] = value; } }
        private void AddDailyEventKinds(DailyEventKindDefinition[] kinds) { if (kinds == null) return; for (int i = 0; i < kinds.Length; i++) { var value = kinds[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _dailyEventKinds[value.Id] = value; } }
        private void AddDailyEvents(DailyEventDefinition[] events) { if (events == null) return; for (int i = 0; i < events.Length; i++) { var value = events[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _dailyEvents[value.Id] = value; } }
        private void AddNpcs(NpcDefinition[] npcs) { if (npcs == null) return; for (int i = 0; i < npcs.Length; i++) { var value = npcs[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _npcs[value.Id] = value; } }
        private void AddTimeSlots(TimeSlotDefinition[] timeSlots) { if (timeSlots == null) return; for (int i = 0; i < timeSlots.Length; i++) { var value = timeSlots[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _timeSlots[value.Id] = value; } }
        private void AddNpcSchedules(NpcScheduleDefinition[] schedules) { if (schedules == null) return; for (int i = 0; i < schedules.Length; i++) { var schedule = schedules[i]; var npc = schedule != null ? schedule.Npc : null; if (npc != null && !string.IsNullOrEmpty(npc.Id)) _npcSchedules[npc.Id] = schedule; } }
        private void AddCareerCandidates(CareerCandidateDefinition[] candidates) { if (candidates == null) return; for (int i = 0; i < candidates.Length; i++) { var value = candidates[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _careerCandidates[value.Id] = value; } }
        private void AddCareerHints(CareerHintDefinition[] hints) { if (hints == null) return; for (int i = 0; i < hints.Length; i++) { var value = hints[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _careerHints[value.Id] = value; } }
        private void AddCareerCandidateRoutes(CareerCandidateRouteDefinition[] routes) { if (routes == null) return; for (int i = 0; i < routes.Length; i++) { var value = routes[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _careerCandidateRoutes[value.Id] = value; } }
        private void AddCareerInterests(CareerInterestDefinition[] interests) { if (interests == null) return; for (int i = 0; i < interests.Length; i++) { var value = interests[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _careerInterests[value.Id] = value; } }
        private void AddWorldStateFlags(WorldStateFlagDefinition[] flags) { if (flags == null) return; for (int i = 0; i < flags.Length; i++) { var value = flags[i]; if (value != null && !string.IsNullOrEmpty(value.Id)) _worldStateFlags[value.Id] = value; } }

        private void AddCampaigns(CampaignDefinition[] campaigns)
        {
            if (campaigns == null) return;
            for (int i = 0; i < campaigns.Length; i++)
            {
                var campaign = campaigns[i];
                if (campaign == null || string.IsNullOrEmpty(campaign.Id)) continue;
                _campaigns[campaign.Id] = campaign;
                var days = campaign.Days ?? Array.Empty<CampaignDayDefinition>();
                for (int dayIndex = 0; dayIndex < days.Length; dayIndex++)
                {
                    var day = days[dayIndex];
                    if (day == null || string.IsNullOrEmpty(day.Id)) continue;
                    _campaignDays[day.Id] = day;
                    var routes = day.Routes ?? Array.Empty<CampaignRouteDefinition>();
                    for (int routeIndex = 0; routeIndex < routes.Length; routeIndex++)
                    {
                        var route = routes[routeIndex];
                        if (route != null && !string.IsNullOrEmpty(route.Id)) _campaignRoutes[route.Id] = route;
                    }
                }
            }
        }
    }
}
