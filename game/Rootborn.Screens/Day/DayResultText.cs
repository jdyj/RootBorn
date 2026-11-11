using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Game;

namespace Rootborn.Screens.Day
{
    /// <summary>
    /// 하루 결과의 구역 글(Unity StudentDayResultPanel의 Format* 와 ResolveText). 문구 키는 "day-result.*"이고 t(화면의 T)로 받는다.
    /// 기록 문자열(활동 id, 결과 로그, 마일스톤 줄 등)은 Resolve가 이름으로 바꾼다: 정의 id → 그 정의의 이름 키,
    /// 현지화에 있는 키, 장소·학교 밖·알바 결과 로그 꼴, 그리고 섞인 문장의 낱말 하나하나. 엔티티 id로 갈라지지 않는다.
    /// Resolve는 현지화에 없는 낱말을 빠진 키로 세지 않는다(기록 문자열은 키가 아니다).
    /// </summary>
    public sealed class DayResultText
    {
        private static readonly Regex Token = new Regex(@"[A-Za-z][A-Za-z0-9_.-]*", RegexOptions.CultureInvariant);
        private readonly Func<string, string> _t;
        private readonly GameText _text;
        private readonly IReadOnlyDictionary<string, string> _displayKeys;

        public DayResultText(Func<string, string> t, GameText text, IReadOnlyDictionary<string, string> displayKeys)
        {
            _t = t; _text = text; _displayKeys = displayKeys;
        }

        /// <summary>
        /// 정의 id → 이름 키 표. 모든 정의 중 Id와 DisplayNameKey(문자열)를 가진 것을 모은다(Unity RefreshDisplayKeys는 종류를 하나하나 적었다 —
        /// 여기서는 종류를 닫지 않는다). 같은 id가 여럿이면 뒤의 것이 이긴다(Unity와 같음). 한 판에 한 번 만든다.
        /// </summary>
        public static Dictionary<string, string> BuildDisplayKeys(GameSession session)
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            var props = new Dictionary<Type, (PropertyInfo Id, PropertyInfo Name)>();
            foreach (object definition in session.Definitions.All<object>())
            {
                Type type = definition.GetType();
                if (!props.TryGetValue(type, out var p))
                {
                    PropertyInfo id = type.GetProperty("Id", typeof(string)), name = type.GetProperty("DisplayNameKey", typeof(string));
                    props[type] = p = (id, name);
                }
                if (p.Id == null || p.Name == null) continue;
                string definitionId = p.Id.GetValue(definition) as string, nameKey = p.Name.GetValue(definition) as string;
                if (!string.IsNullOrEmpty(definitionId) && !string.IsNullOrEmpty(nameKey)) keys[definitionId] = nameKey;
            }
            return keys;
        }

        public string Ui(string key) => _t("day-result." + key);

        private string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, _t(key), args);

        private string Lookup(string key) => _text.Has(key, _text.Language) ? _text.Get(key) : null;

        public string Resolve(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            const string visitPrefix = "location:", visitSuffix = ":visited";
            if (value.StartsWith(visitPrefix, StringComparison.Ordinal) && value.EndsWith(visitSuffix, StringComparison.Ordinal) && value.Length > visitPrefix.Length + visitSuffix.Length)
                return Format("day-result.location-visited", Resolve(value.Substring(visitPrefix.Length, value.Length - visitPrefix.Length - visitSuffix.Length)));
            const string npcPrefix = "npc:", metSuffix = ":met";
            if (value.StartsWith(npcPrefix, StringComparison.Ordinal) && value.EndsWith(metSuffix, StringComparison.Ordinal) && value.Length > npcPrefix.Length + metSuffix.Length)
                return Format("day-result.npc-met", Resolve(value.Substring(npcPrefix.Length, value.Length - npcPrefix.Length - metSuffix.Length)));
            if (value.IndexOf('@') > 0)
            {
                string[] parts = value.Split('@');
                for (int i = 0; i < parts.Length; i++) parts[i] = Resolve(parts[i]);
                return Join(parts);
            }
            if (_displayKeys.TryGetValue(value, out string displayKey)) return Lookup(displayKey) ?? displayKey;
            string translated = Lookup(value);
            if (translated != null) return translated;
            if (LocationActivityLogCodec.TryParse(value, out var location))
                return Format("day-result.log-change", Resolve(location.ActivityId), Resolve(location.TargetId), location.IsUnlock ? Ui("unlocked") : Signed(location.Delta));
            if (OutsideSchoolLogCodec.TryParseDelta(value, out var outside))
                return Format("day-result.log-change", Resolve(outside.ActivityId), Resolve(outside.TargetId), Signed(outside.Delta));
            if (PartTimeWorkLogCodec.TryParseDelta(value, out var work))
                return Format("day-result.log-change", Resolve(work.WorkId), Resolve(work.TargetId), Signed(work.Delta));
            return Token.Replace(value, match =>
            {
                string token = match.Value;
                string key = _displayKeys.TryGetValue(token, out string k) ? k : token;
                return Lookup(key) ?? key;
            });
        }

        /// <summary>이름들을 잇는 구분자도 현지화 키다(언어마다 다를 수 있다).</summary>
        private string Join(string[] parts) => string.Join(_t("day-result.separator"), parts);

        private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString(CultureInfo.InvariantCulture);

        public string Title(int day) => Format("day-result.title", day);

        public string List(string titleKey, IReadOnlyList<string> values, string emptyKey)
        {
            string text = Ui(titleKey);
            if (values == null || values.Count == 0) return text + "\n" + Ui(emptyKey);
            foreach (string v in values) if (!string.IsNullOrEmpty(v)) text += "\n" + Resolve(v);
            return text;
        }

        /// <summary>결과 구역: 성장 → 학교 밖 → 진로 후보 → 장래희망, 뒤에 일과 칸마다 한 묶음(Unity Show + AppendDailyPlanSummary).</summary>
        public string Results(DayResultModel m)
        {
            string[] logs = m.Summary.ResultLogIds;
            // Unity는 학교 밖 구역이 비어도 빈 줄을 그대로 이어 빈칸이 생겼다. 빈 구역은 건너뛴다.
            var parts = new List<string>(4)
            {
                List("growth", PartTimeWorkLogCodec.AggregateDeltas(OutsideSchoolLogCodec.AggregateDeltas(logs)), "empty-growth"),
                OutsideSchool(logs), CareerCandidates(m.CareerCandidateLines), CareerInterest(m),
            };
            parts.RemoveAll(string.IsNullOrEmpty);
            string text = string.Join("\n\n", parts);
            foreach (DayPlanSummary plan in m.DailyPlan)
            {
                if (plan.RouteNameKey.Length == 0) continue;
                string block = Ui("daily-plan") + "\n" + Resolve(plan.RouteNameKey);
                if (plan.ActivityNameKey.Length > 0) block += "\n" + Resolve(plan.ActivityNameKey);
                block += "\n" + (plan.Completed ? Ui("completed") : Ui("selected"));
                foreach (string log in plan.ResultLogs) if (!string.IsNullOrEmpty(log)) block += "\n" + Resolve(log);
                text += "\n\n" + block;
            }
            return text;
        }

        private string OutsideSchool(string[] logs)
        {
            OutsideSchoolGrowthSummary summary = OutsideSchoolGrowthSummary.FromResultLogs(logs);
            if (summary.Entries.Length == 0) return string.Empty;
            string text = Ui("outside-school");
            foreach (var entry in summary.Entries) text += "\n" + Format("day-result.outside-school-line", Resolve(entry.ActivityId), Signed(entry.Delta), Resolve(entry.TargetId));
            return text;
        }

        private string CareerCandidates(string[] lines)
        {
            string text = Ui("career-candidates");
            if (lines == null || lines.Length == 0) return text + "\n" + Ui("empty-candidates");
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split(':'); // 후보:힌트:상태
                for (int p = 0; p < parts.Length; p++) parts[p] = Resolve(parts[p]);
                text += "\n" + Join(parts);
            }
            return text;
        }

        private string CareerInterest(DayResultModel m)
        {
            string text = Ui("career-interest");
            if (m.CareerInterestId.Length == 0) return text + "\n" + Ui("empty-interest");
            text += "\n" + Resolve(m.CareerInterestId);
            foreach (string r in m.CareerInterestRecommendations) if (!string.IsNullOrEmpty(r)) text += "\n" + Resolve(r);
            return text;
        }

        public string Quests(QuestSummaryEntry[] entries)
        {
            string text = Ui("quests");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-quests");
            foreach (QuestSummaryEntry e in entries)
                text += "\n" + Format("day-result.quest-line", Resolve(e.DisplayName), Ui("quest-state." + e.BeforeState), Ui("quest-state." + e.AfterState));
            return text;
        }

        public string Rewards(RewardSummaryEntry[] entries)
        {
            string text = Ui("rewards");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-rewards");
            foreach (RewardSummaryEntry e in entries) text += "\n" + Format("day-result.reward-line", Resolve(e.QuestDisplayName), Resolve(e.ItemDisplayName), e.Count);
            return text;
        }

        public string Inventory(InventoryDeltaEntry[] entries)
        {
            string text = Ui("inventory");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-inventory");
            foreach (InventoryDeltaEntry e in entries)
                text += "\n" + Format(e.FromReward ? "day-result.inventory-reward-line" : "day-result.inventory-line", Resolve(e.DisplayName), Signed(e.Delta));
            return text;
        }

        public string Relationships(RelationshipDeltaEntry[] entries)
        {
            string text = Ui("relationships");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-relationships");
            foreach (RelationshipDeltaEntry e in entries) text += "\n" + Format("day-result.relationship-line", Resolve(e.DisplayName), e.BeforeValue, e.AfterValue, Signed(e.Delta));
            return text;
        }

        public string Conditions(StatusDeltaEntry[] entries)
        {
            string text = Ui("conditions");
            if (entries == null || entries.Length == 0) return text + "\n" + Ui("empty-conditions");
            foreach (StatusDeltaEntry e in entries)
                text += "\n" + Format("day-result.condition-line", Resolve(e.DisplayName), e.BeforeValue, e.AfterValue, Signed(e.Delta), e.PersistsToNextDay ? Ui("persists") : Ui("recovers"));
            return text;
        }

        /// <summary>다음 날 안내: 진입점·단계·다음 목표·안내 문장, 내일 영향, 마일스톤·추천, 일과 칸의 다음 힌트.</summary>
        public string NextGuide(DayResultModel m)
        {
            StudentDaySummary s = m.Summary;
            string text = s.NextDayEntryPointId.Length == 0 ? Ui("next-day") : Format("day-result.next-line", Resolve(s.NextDayEntryPointId));
            if (s.TutorialStageId.Length > 0) text += "\n" + Format("day-result.stage-line", Resolve(s.TutorialStageId));
            if (s.NextObjectiveId.Length > 0) text += "\n" + Format("day-result.objective-line", Resolve(s.NextObjectiveId));
            if (s.NextGuideText.Length > 0) text += "\n" + Resolve(s.NextGuideText);
            foreach (string impact in m.RelationshipCondition.NextDayImpacts) if (!string.IsNullOrEmpty(impact)) text += "\n" + Resolve(impact);
            if (m.Milestones.ProgressLines.Length > 0)
            {
                text += "\n" + Ui("milestones");
                foreach (string line in m.Milestones.ProgressLines) if (!string.IsNullOrEmpty(line)) text += "\n" + Resolve(line);
            }
            if (m.Milestones.RecommendedActionIds.Length > 0)
            {
                text += "\n" + Ui("recommended");
                foreach (string id in m.Milestones.RecommendedActionIds) if (!string.IsNullOrEmpty(id)) text += "\n" + Resolve(id);
            }
            foreach (DayPlanSummary plan in m.DailyPlan)
                if (plan.RouteNameKey.Length > 0 && plan.NextHintKey.Length > 0) text += "\n" + Resolve(plan.NextHintKey);
            return text;
        }

        /// <summary>장소 상태 기록 줄들(Unity LocationStatePresentation.RecordTitle + RecordDetail).</summary>
        public IEnumerable<string> LocationStates(GameSession session, LocationStateTodaySummarySaveData[] records)
        {
            foreach (LocationStateTodaySummarySaveData r in records)
            {
                if (r == null) continue;
                string title;
                if (r.Hidden) title = _t("dream.location-state.record.hidden");
                else
                {
                    string key = session.Definitions.TryGet<LocationState>(r.StateId ?? string.Empty, out LocationState state) ? state.DisplayNameKey : r.DisplayName;
                    title = string.IsNullOrEmpty(key) ? _t("life.location-state.record.unknown") : (Lookup(key) ?? key);
                }
                string place = session.Definitions.TryGet<Location>(r.LocationId ?? string.Empty, out Location location) ? Lookup(location.DisplayNameKey) ?? location.DisplayNameKey : _t("life.location-state.record.location");
                string time = session.Definitions.TryGet<TimeSlotDefinition>(r.TimeSlotId ?? string.Empty, out TimeSlotDefinition slot) ? Lookup(slot.DisplayNameKey) ?? slot.DisplayNameKey : _t("life.location-state.record.time");
                string detail = r.Day > 0 ? Format("life.location-state.record.detail", r.Day, place, time) : Format("life.location-state.record.detail-no-day", place, time);
                yield return title + "\n" + detail;
            }
        }

        /// <summary>캠페인 구역(Unity AppendCampaignSummary). 진행 글이 없으면 빈 문자열(구역을 감춘다).</summary>
        public string Campaign(CampaignDayResultSummary c) =>
            string.IsNullOrEmpty(c.ProgressText) ? string.Empty : Ui("campaign") + "\n" + Resolve(c.SelectedRouteText + "\n" + c.ProgressText + "\n" + c.NextGuideText);

        /// <summary>체인 구역(Unity QuestChainSummaryPresentation.Build). 받은 체인이 없으면 빈 문자열.</summary>
        public string Chain(DayResultModel m)
        {
            if (!m.HasChain || m.Chain.Chain == null) return string.Empty;
            DayChainSummary c = m.Chain;
            string title = Lookup(c.Chain.DisplayNameKey);
            if (string.IsNullOrWhiteSpace(title) || title == c.Chain.Id) title = _t("quest.chain.generic");
            string state = Enum.IsDefined(typeof(QuestChainState), c.State) ? c.State.ToString().ToLowerInvariant() : "active";
            string body = title + "\n" + _t("quest.chain.state." + state);
            if (c.HasObjective)
            {
                string label = Lookup(c.ObjectiveKey);
                if (string.IsNullOrWhiteSpace(label)) label = _t("quest.chain.objective");
                body += "\n" + Format("quest.chain.objective-progress", label, c.Progress, c.Required);
            }
            return Ui("quest-chains") + "\n" + Resolve(body);
        }
    }
}
