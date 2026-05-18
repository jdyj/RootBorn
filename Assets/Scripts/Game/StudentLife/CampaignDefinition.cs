using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "Campaign_New", menuName = "Rootborn/Student Life/Campaigns/Campaign")]
    public sealed class CampaignDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _descriptionKey;
        [SerializeField] private CampaignDayDefinition[] _days = Array.Empty<CampaignDayDefinition>();
        [SerializeField] private CampaignRewardBase[] _rewards = Array.Empty<CampaignRewardBase>();

        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? Id : _descriptionKey;
        public CampaignDayDefinition[] Days => _days;
        public CampaignRewardBase[] Rewards => _rewards;

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, CampaignDayDefinition[] days, CampaignRewardBase[] rewards)
        {
            ConfigureForTests(id, displayNameKey);
            _descriptionKey = string.IsNullOrEmpty(descriptionKey) ? id : descriptionKey;
            _days = days ?? Array.Empty<CampaignDayDefinition>();
            _rewards = rewards ?? Array.Empty<CampaignRewardBase>();
        }

        public CampaignDayDefinition GetDayByNumber(int dayNumber)
        {
            int safeDay = Mathf.Max(1, dayNumber);
            for (int i = 0; i < _days.Length; i++) if (_days[i] != null && _days[i].DayNumber == safeDay) return _days[i];
            return _days.Length > 0 ? _days[Mathf.Min(_days.Length - 1, safeDay - 1)] : null;
        }
    }
}
