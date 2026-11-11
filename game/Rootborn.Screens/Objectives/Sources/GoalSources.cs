using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Core.StudentLife;
using Rootborn.Game;

namespace Rootborn.Screens.Objectives.Sources
{
    /// <summary>하루 안내(Unity StudentDayGuideObjectiveSource): 오늘 할 일 한 줄. 진행 중인 판 → 시간 다 씀 → 성인 → 튜토리얼 안내 순.</summary>
    public sealed class StudentDayGuideSource : IObjectiveJournalSource
    {
        public string SourceId => "student-day-guide";
        public string CategoryId => JournalCategories.Goals;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            StudentLifeProgress student = session.Student;
            bool adult = session.Adult.IsAdult;
            string key = string.IsNullOrEmpty(student.NextObjectiveId) ? "student-day.current" : "student-day." + student.NextObjectiveId;
            JournalText title = JournalText.Key(adult ? "ui.objectives.career_day_guide" : "ui.objectives.student_day_guide");
            JournalText hint;
            if (student.HasPendingMinigame || session.Adult.HasPendingWork) hint = JournalText.Key("ui.objectives.pending_activity_hint");
            else if (student.TimePointsRemaining <= 0) hint = JournalText.Key("ui.objectives.finish_day_hint");
            else if (adult) hint = JournalText.Key("ui.objectives.career_day_hint");
            else
            {
                string authored = session.Definitions.TryGet(student.TutorialStageId ?? string.Empty, out TutorialStage stage) ? stage.GuideText : student.NextGuideText;
                hint = string.IsNullOrEmpty(authored) ? JournalText.Key("ui.objectives.student_day_hint") : JournalText.Data(authored);
            }
            JournalText progress = JournalText.Key("ui.objectives.day_progress", student.CurrentDay, student.Energy, student.Focus);
            return new[] { new JournalItem(key, CategoryId, title, JournalText.Data(student.TutorialStageId), JournalSourceText.State(student.DayState.ToString()), progress, hint, false, 0) };
        }
    }

    /// <summary>오늘 일과(Unity DailyPlanObjectiveSource): 끝나지 않은 일과 칸마다 한 줄. 고른 길이 없으면 "일과를 고르세요".</summary>
    public sealed class DailyPlanSource : IObjectiveJournalSource
    {
        public string SourceId => "daily-plan";
        public string CategoryId => JournalCategories.Goals;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            DailyPlanProgress plan = session.DailyPlan;
            if (plan == null) return new[] { Choose() };
            DailyPlanRouteSet[] sets = session.Definitions.All<DailyPlanRouteSet>().ToArray();
            var runner = new DailyPlanRouteRunner();
            var items = new List<JournalItem>(plan.Slots.Count);
            var routes = new HashSet<string>(StringComparer.Ordinal);
            bool unassigned = false;
            foreach (DailyPlanSlotRecord slot in plan.Slots)
            {
                if (slot.Completed) continue;
                DailyPlanRoute route = runner.FindRoute(sets, slot.RouteId);
                if (route == null) { unassigned = true; continue; }
                // 길의 첫 칸은 예전 키 그대로, 같은 길의 다음 칸은 칸 번호를 붙인다(Unity와 같음).
                string key = "daily-plan:" + route.Id;
                if (!routes.Add(route.Id)) key += ":slot:" + slot.SlotIndex;
                JournalText subtitle = plan.Slots.Count > 1
                    ? JournalText.Key("ui.journal.daily.slot-subtitle", JournalText.Data(route.DisplayNameKey), JournalText.Data(slot.PeriodKey))
                    : JournalText.Data(route.DisplayNameKey);
                items.Add(new JournalItem(key, CategoryId, JournalText.Data(route.ObjectiveTitleKey), subtitle, JournalSourceText.State("Selected"),
                    JournalText.Key("ui.journal.daily.selected-for-day", plan.CurrentDay), JournalText.Data(route.ObjectiveDetailKey), false, 5 + slot.SlotIndex));
            }
            if (items.Count == 0 && unassigned) items.Add(Choose());
            return items;
        }

        private JournalItem Choose() => new JournalItem("daily-plan:choose", CategoryId, JournalText.Data("objective.daily.choose.title"), JournalText.Empty,
            JournalSourceText.State("Open"), JournalText.Key("ui.journal.daily.none"), JournalText.Data("objective.daily.choose.detail"), false, 5);
    }
}
