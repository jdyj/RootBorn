using System;
using System.Collections.Generic;
using Rootborn.Data;

namespace Rootborn.Game
{
    /// <summary>
    /// 새 게임을 시작할 때 쓰는 값. 정의 id는 registry.json의 기본값 키에서 읽는다.
    /// 시작 에너지·집중·시각은 데이터에 없고 Unity StudentLifeProgressComponent의 직렬화 기본값(12, 10, 480분)이다.
    /// </summary>
    public sealed class GameStartSettings
    {
        public GameStartSettings(string calendarId, string dayEndRuleId, string tutorialStageId, string campaignId, string locationStateConflictPolicyId,
            int startEnergy = UnityStartEnergy, int startFocus = UnityStartFocus, int startTimeMinutes = UnityStartTimeMinutes)
        {
            CalendarId = calendarId ?? string.Empty;
            DayEndRuleId = dayEndRuleId ?? string.Empty;
            TutorialStageId = tutorialStageId ?? string.Empty;
            CampaignId = campaignId ?? string.Empty;
            LocationStateConflictPolicyId = locationStateConflictPolicyId ?? string.Empty;
            StartEnergy = startEnergy;
            StartFocus = startFocus;
            StartTimeMinutes = startTimeMinutes;
        }

        // Unity StudentLifeSceneInteraction.cs:16-19 (StudentLifeProgressComponent 직렬화 기본값).
        public const int UnityStartEnergy = 12;
        public const int UnityStartFocus = 10;
        public const int UnityStartTimeMinutes = 8 * 60;

        public string CalendarId { get; }
        public string DayEndRuleId { get; }
        public string TutorialStageId { get; }
        public string CampaignId { get; }
        public string LocationStateConflictPolicyId { get; }
        public int StartEnergy { get; }
        public int StartFocus { get; }
        public int StartTimeMinutes { get; }

        /// <summary>
        /// data/registry.json의 studentLifeCalendar, defaultDayEndRule, defaultTutorialStage, defaultStudentCampaign,
        /// locationStateConflictPolicies(첫 것)를 읽는다. GameDefinitions가 이 키들을 내주지 않아 표를 한 번 더 읽는다.
        /// </summary>
        public static GameStartSettings FromRegistry(string dataDirectory)
        {
            var errors = new List<LoadError>();
            var table = EntityTable.ReadDirectory(dataDirectory, errors);
            if (errors.Count > 0) throw new InvalidOperationException("registry.json을 읽지 못했다: " + errors[0]);
            string First(string key)
            {
                var ids = table.RegistryIds(key);
                return ids.Count > 0 ? ids[0] : string.Empty;
            }

            return new GameStartSettings(First("studentLifeCalendar"), First("defaultDayEndRule"), First("defaultTutorialStage"),
                First("defaultStudentCampaign"), First("locationStateConflictPolicies"));
        }
    }
}
