using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "CampaignDay_New", menuName = "Rootborn/Student Life/Campaigns/Day")]
    public sealed class CampaignDayDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private int _dayNumber = 1;
        [SerializeField] private string _themeKey;
        [SerializeField] private string _directionText;
        [SerializeField] private string _nextGuideText;
        [SerializeField] private CampaignRouteDefinition[] _routes = Array.Empty<CampaignRouteDefinition>();

        public int DayNumber => Mathf.Max(1, _dayNumber);
        public string ThemeKey => string.IsNullOrEmpty(_themeKey) ? DisplayNameKey : _themeKey;
        public string DirectionText => string.IsNullOrEmpty(_directionText) ? ThemeKey : _directionText;
        public string NextGuideText => string.IsNullOrEmpty(_nextGuideText) ? string.Empty : _nextGuideText;
        public CampaignRouteDefinition[] Routes => _routes;

        public void ConfigureForTests(string id, int dayNumber, string themeKey, string directionText, string nextGuideText, CampaignRouteDefinition[] routes)
        {
            ConfigureForTests(id, themeKey);
            _dayNumber = Mathf.Max(1, dayNumber);
            _themeKey = string.IsNullOrEmpty(themeKey) ? id : themeKey;
            _directionText = string.IsNullOrEmpty(directionText) ? _themeKey : directionText;
            _nextGuideText = string.IsNullOrEmpty(nextGuideText) ? string.Empty : nextGuideText;
            _routes = routes ?? Array.Empty<CampaignRouteDefinition>();
        }

        public bool HasSchoolFreeCompletionRoute()
        {
            for (int i = 0; i < _routes.Length; i++) if (_routes[i] != null && _routes[i].CanCompleteWithoutSchoolClass) return true;
            return false;
        }
    }
}
