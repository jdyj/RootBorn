using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Game;

namespace Rootborn.Screens.Day
{
    /// <summary>일과 칸 하나: 시간대 키, 넣은 일과 길(없으면 빈 id), 끝났는지, 누를 수 있는지, 골랐는지.</summary>
    public readonly struct DailyPlanSlotView
    {
        public DailyPlanSlotView(string periodKey, string routeId, string routeNameKey, bool completed, bool interactable, bool selected)
        {
            PeriodKey = periodKey ?? string.Empty;
            RouteId = routeId ?? string.Empty;
            RouteNameKey = routeNameKey ?? string.Empty;
            Completed = completed;
            Interactable = interactable;
            Selected = selected;
        }

        public string PeriodKey { get; }
        public string RouteId { get; }
        /// <summary>일과 길 이름 키. 정의에서 찾지 못한 id면 id 그대로(Unity 판과 같음). 칸이 비었으면 빈 문자열.</summary>
        public string RouteNameKey { get; }
        public bool Completed { get; }
        public bool Interactable { get; }
        public bool Selected { get; }
    }

    /// <summary>고를 수 있는 일과 길 하나: id, 이름 키, 아이콘(정의별 테마 아이콘, 없으면 "plan").</summary>
    public readonly struct DailyPlanRouteView
    {
        public DailyPlanRouteView(string routeId, string nameKey, string image)
        {
            RouteId = routeId ?? string.Empty;
            NameKey = nameKey ?? string.Empty;
            Image = image ?? string.Empty;
        }

        public string RouteId { get; }
        public string NameKey { get; }
        public string Image { get; }
    }

    /// <summary>
    /// 일과 화면의 표시 모델(Unity DailyPlanPanel.BindSchedule·RenderSchedule이 그리던 값). GameSession과 화면 상태(보고 있는 날, 고른 칸)로 만든다.
    /// 판단은 하지 않는다: 고를 수 있는 길은 GameSession.AvailableDailyPlanRoutes, 칸의 상태는 Core 일과 기록을 그대로 옮긴다.
    /// </summary>
    public sealed class DailyPlanModel
    {
        public const string RouteIconFallback = "plan";

        private DailyPlanModel() { }

        public int CurrentDay { get; private set; }
        public int DisplayedDay { get; private set; }
        public bool IsCurrentDay => DisplayedDay == CurrentDay;
        public IReadOnlyList<DailyPlanSlotView> Slots { get; private set; }
        public IReadOnlyList<DailyPlanRouteView> Routes { get; private set; }
        public int SelectedSlot { get; private set; }
        /// <summary>일과 길 카드를 누를 수 있다: 오늘을 보고 있고, 고른 칸이 끝나지 않았고, 하루가 진행 중이다.</summary>
        public bool CanAssign { get; private set; }
        public bool CanGoPrevious { get; private set; }
        public bool CanGoNext { get; private set; }

        /// <summary>처음 열 때 고를 칸: 끝나지 않은 첫 칸(Unity BindSchedule).</summary>
        public static int FirstOpenSlot(GameSession session)
        {
            IReadOnlyList<DailyPlanSlotRecord> slots = session.DailyPlan.Slots;
            for (int i = 0; i < slots.Count; i++) if (!slots[i].Completed) return i;
            return 0;
        }

        public static DailyPlanModel From(GameSession session, int displayedDay, int selectedSlot)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            DailyPlanProgress daily = session.DailyPlan;
            StudentLifeProgress student = session.Student;
            DailyPlanRouteSet[] sets = RouteSets(session);
            var runner = new DailyPlanRouteRunner();

            int current = daily.CurrentDay;
            IReadOnlyList<DailyPlanSlotRecord> slots = daily.Slots;
            int diaryIndex = daily.Diary.Count;
            if (displayedDay != current)
            {
                bool found = false;
                for (int i = 0; i < daily.Diary.Count; i++)
                    if (daily.Diary[i].DayNumber == displayedDay) { slots = daily.Diary[i].Slots; diaryIndex = i; found = true; break; }
                if (!found) displayedDay = current;
            }

            bool isCurrent = displayedDay == current;
            bool inProgress = student.DayState == StudentDayState.InProgress;
            int selected = Math.Clamp(selectedSlot, 0, Math.Max(0, slots.Count - 1));
            var slotViews = new DailyPlanSlotView[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                DailyPlanSlotRecord slot = slots[i];
                string nameKey = string.Empty;
                if (!string.IsNullOrEmpty(slot.RouteId))
                    nameKey = runner.FindRoute(sets, slot.RouteId)?.DisplayNameKey ?? slot.RouteId;
                slotViews[i] = new DailyPlanSlotView(slot.PeriodKey, slot.RouteId, nameKey, slot.Completed,
                    isCurrent && !slot.Completed && inProgress, isCurrent && i == selected);
            }

            DailyPlanRoute[] available = session.AvailableDailyPlanRoutes();
            var routeViews = new List<DailyPlanRouteView>(available.Length);
            foreach (DailyPlanRoute route in available)
                if (route != null) routeViews.Add(new DailyPlanRouteView(route.Id, route.DisplayNameKey, ScreenIcons.For(route.Id, RouteIconFallback)));

            return new DailyPlanModel
            {
                CurrentDay = current,
                DisplayedDay = displayedDay,
                Slots = slotViews,
                Routes = routeViews,
                SelectedSlot = selected,
                CanAssign = isCurrent && slots.Count > 0 && !slots[selected].Completed && inProgress,
                CanGoPrevious = diaryIndex > 0,
                CanGoNext = !isCurrent,
            };
        }

        /// <summary>앞 일기 날(이전 버튼) 또는 다음 날(다음 버튼). 일기 끝을 넘으면 오늘.</summary>
        public static int MoveDiary(GameSession session, int displayedDay, int delta)
        {
            DailyPlanProgress daily = session.DailyPlan;
            int index = daily.Diary.Count;
            for (int i = 0; i < daily.Diary.Count; i++) if (daily.Diary[i].DayNumber == displayedDay) { index = i; break; }
            index = Math.Clamp(index + delta, 0, daily.Diary.Count);
            return index == daily.Diary.Count ? daily.CurrentDay : daily.Diary[index].DayNumber;
        }

        private static DailyPlanRouteSet[] RouteSets(GameSession session)
        {
            IReadOnlyList<DailyPlanRouteSet> all = session.Definitions.All<DailyPlanRouteSet>();
            var sets = new DailyPlanRouteSet[all.Count];
            for (int i = 0; i < sets.Length; i++) sets[i] = all[i];
            return sets;
        }
    }
}
