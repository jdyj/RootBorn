using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.EditMode.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class DailyPlanScheduleUiTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private string _root;
        private GameObject _player;
        private StudentLifeProgressComponent _student;
        private DailyPlanProgressComponent _daily;
        private DailyPlanPanel _panel;
        private DailyPlanBoardInteractor _board;
        private DailyPlanRouteDefinition _route;
        private DailyPlanRouteSetDefinition _set;
        private LocationActivityDefinition _activity;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-daily-schedule-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root); SaveService.SetRootDirectoryForTests(_root);
            _player = Go("DailyPlanPlayer"); _player.SetActive(false);
            _student = _player.AddComponent<StudentLifeProgressComponent>();
            _student.ConfigureForTests("slot-ui", "player-ui", 100, 100, 0, 480);
            var calendar = Make<LifeCalendarDefinition>(); calendar.ConfigureForTests(2, 3); _student.Progress.ConfigureCalendar(calendar);
            _daily = _player.AddComponent<DailyPlanProgressComponent>(); _daily.Load("slot-ui", "player-ui", 1);
            _panel = Go("DailyPlanPanel", typeof(RectTransform)).AddComponent<DailyPlanPanel>();
            _activity = Make<LocationActivityDefinition>();
            _activity.ConfigureForTests("activity.ui", "Study activity", null, LocationGrowthRoute.SelfStudy, 30, 0, 0, 0, null, null);
            _route = Make<DailyPlanRouteDefinition>();
            _route.ConfigureForTests("route.ui", "Study plan", null, LocationGrowthRoute.SelfStudy, new[] { _activity }, null, null, "Study", "Read", "Continue");
            _set = Make<DailyPlanRouteSetDefinition>(); _set.ConfigureForTests("set.ui", "Schedule", new[] { _route }, 1);
            _board = Go("ActualDailyPlanBoard").AddComponent<DailyPlanBoardInteractor>();
            _board.Bind(_panel, new[] { _set }, _daily, _student);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void ActualBoardInteraction_OpensTwoSlotWeekdaySchedule()
        {
            Assert.That(_board.TryInteract(_player), Is.True);
            Assert.That(DailyPlanScheduleApi.Read<int>(_panel, "ScheduleSlotCountForTests"), Is.EqualTo(2));
            Assert.That(SlotButton(0), Is.Not.Null); Assert.That(SlotButton(1), Is.Not.Null);
            Assert.That(_panel.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ActualBoardInteraction_OnSaturdayOpensThreeSlots()
        {
            AdvanceTo(6);
            Assert.That(_board.TryInteract(_player), Is.True);
            Assert.That(DailyPlanScheduleApi.Read<int>(_panel, "ScheduleSlotCountForTests"), Is.EqualTo(3));
            Assert.That(SlotButton(2), Is.Not.Null);
        }

        [Test]
        public void SlotAndRouteButtons_AssignChosenSlot_KeepDiaryOpenForRemainingPlan()
        {
            Assert.That(_board.TryInteract(_player), Is.True);
            SlotButton(1).onClick.Invoke();
            _panel.GetRouteButtonForTests(0).onClick.Invoke();
            var slots = DailyPlanScheduleApi.Values(_daily.Progress, "Slots");
            Assert.That(DailyPlanScheduleApi.Read<string>(slots[0], "RouteId"), Is.Empty);
            Assert.That(DailyPlanScheduleApi.Read<string>(slots[1], "RouteId"), Is.EqualTo(_route.Id));
            Assert.That(_panel.gameObject.activeSelf, Is.True, "The real diary stays open to plan the other slot.");
        }

        [Test]
        public void CompletedSlotButton_IsDisabledAfterActualScheduleCredit()
        {
            var progress = _daily.EnsureProgress(_student.Progress);
            DailyPlanScheduleApi.Call<bool>(progress, "SynchronizeDay", _student.Progress);
            Assert.That(DailyPlanScheduleApi.Call<bool>(progress, "TryAssignSlot", 0, _route, _student.Progress), Is.True);
            Assert.That(DailyPlanScheduleApi.Record(progress, new[] { _set }, _activity, _student.Progress, 1, "ui-completion"), Is.True);
            Assert.That(_board.TryInteract(_player), Is.True);
            Assert.That(SlotButton(0).interactable, Is.False);
            Assert.That(SlotButton(1).interactable, Is.True);
        }

        [Test]
        public void PreviousDayButton_ShowsArchivedChoiceWithoutChangingTodaysSchedule()
        {
            var progress = _daily.EnsureProgress(_student.Progress);
            DailyPlanScheduleApi.Call<bool>(progress, "SynchronizeDay", _student.Progress);
            Assert.That(DailyPlanScheduleApi.Call<bool>(progress, "TryAssignSlot", 0, _route, _student.Progress), Is.True);
            AdvanceTo(2);
            Assert.That(_board.TryInteract(_player), Is.True);
            var method = typeof(DailyPlanPanel).GetMethod("GetPreviousDayButtonForTests", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "The diary must let the player read the archived day.");
            var previous = (Button)method.Invoke(_panel, null); Assert.That(previous, Is.Not.Null);
            previous.onClick.Invoke();
            Assert.That(DailyPlanScheduleApi.Read<int>(_panel, "DisplayedDayForTests"), Is.EqualTo(1));
            Assert.That(_daily.Progress.CurrentDay, Is.EqualTo(2));
            Assert.That(SlotButton(0).interactable, Is.False, "Historical pages are read-only.");
            StringAssert.Contains("Study plan", SlotButton(0).GetComponentInChildren<Text>().text);
        }

        [Test] public void RouteViewportWritesVisibleStencilWithoutDrawingBackground()
        {
            _board.TryInteract(_player);
            var viewport=_panel.transform.Find("RouteViewport");var mask=viewport.GetComponent<Mask>();
            Assert.That(mask,Is.Not.Null);Assert.That(mask.showMaskGraphic,Is.False);
            Assert.That(viewport.GetComponent<Image>().color.a,Is.GreaterThan(.01f),"Near-zero mask alpha discards its stencil and hides every route.");
        }
        [Test] public void ScheduleAndRouteChromeMatchFinalLayoutBounds()
        {
            _board.TryInteract(_player);Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_panel.transform);
            foreach(var button in new[]{SlotButton(0),_panel.GetRouteButtonForTests(0),_panel.GetPreviousDayButtonForTests()}){
                var rect=(RectTransform)button.transform;RectTransform corner=null;
                foreach(Transform child in rect)if(child.name.EndsWith("corner-tr"))corner=(RectTransform)child;
                Assert.That(corner,Is.Not.Null);
                Assert.That(corner.anchoredPosition.x+corner.rect.width,Is.InRange(rect.rect.xMax,rect.rect.xMax+16),button.name+" chrome must reach the final right edge.");
            }
        }
        private Button SlotButton(int index)
        {
            var method = typeof(DailyPlanPanel).GetMethod("GetScheduleSlotButtonForTests", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "The diary needs visible selectable period slots.");
            return (Button)method.Invoke(_panel, new object[] { index });
        }
        private void AdvanceTo(int day)
        {
            while (_student.Progress.CurrentDay < day)
            {
                Assert.That(_student.Progress.TryEndDay(null, out _), Is.True);
                Assert.That(_student.Progress.TryStartNextDay(), Is.True);
            }
        }
        private GameObject Go(string name, params Type[] components) { var value = new GameObject(name, components); _objects.Add(value); return value; }
        private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
    }
}
