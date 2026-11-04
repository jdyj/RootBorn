using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Objectives;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class UrbanGrowthPresentationTests
    {
        [Test]
        public void Journal_CloseButtonClosesAndCategoryIconsDoNotBlockClicks()
        {
            var go = new GameObject("journal-urban-fixture", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<ObjectiveJournalPanel>(); panel.Show();
                var close = go.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == "ObjectiveCloseButton");
                Assert.That(close, Is.Not.Null, "The approved shell needs an actual close action.");
                var categories = go.GetComponentsInChildren<Button>(true).Where(x => x.name.StartsWith("ObjectiveCategory_")).ToArray();
                Assert.That(categories.Length, Is.EqualTo(4));
                foreach (var button in categories)
                {
                    var icon = button.transform.Find("CategoryIcon"); Assert.That(icon, Is.Not.Null);
                    Assert.That(icon.GetComponent<Image>().raycastTarget, Is.False);
                    Assert.That(icon.GetComponents<Component>().Any(x => x != null && x.GetType().Name == "UrbanUiIcon"), Is.True);
                }
                close.onClick.Invoke(); Assert.That(panel.IsVisible, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Journal_RefreshReusesRowsAndLongDetailIsMasked()
        {
            var go = new GameObject("journal-reuse-fixture", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<ObjectiveJournalPanel>();
                var items = new[] { new ObjectiveJournalItem("fixture", "Goals", "A real objective", new string('x', 3000), "Active", "1/2", "Continue", false, 0) };
                panel.SetItems("Goals", items); panel.Show();
                var row = go.GetComponentsInChildren<Button>(true).First(x => x.name == "ObjectiveRow_0");
                panel.SetItems("Goals", items);
                Assert.That(go.GetComponentsInChildren<Button>(true).First(x => x.name == "ObjectiveRow_0"), Is.SameAs(row));
                var detail = go.transform.Find("ObjectiveJournalRoot/ObjectiveDetail");
                Assert.That(detail.GetComponent<ScrollRect>(), Is.Not.Null);
                Assert.That(detail.GetComponentsInChildren<RectMask2D>(true).Length, Is.GreaterThan(0));
                StringAssert.Contains(new string('x', 100), panel.VisibleText);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DailyPlan_RouteCardsHaveDefinitionBoundIconAndLargeHitTarget()
        {
            var go = new GameObject("daily-urban-fixture", typeof(RectTransform));
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            try
            {
                route.ConfigureForTests("fixture", "fixture.route", null, LocationGrowthRoute.SelfStudy, Array.Empty<LocationActivityDefinition>(), Array.Empty<DailyPlanRouteRequirementBase>(), Array.Empty<DailyPlanRouteOutcomeBase>(), "title", "detail", "next");
                var panel = go.AddComponent<DailyPlanPanel>(); panel.Bind(new[] { route }, null, null);
                var button = panel.GetRouteButtonForTests(0); Assert.That(button, Is.Not.Null);
                Assert.That(button.GetComponent<LayoutElement>().preferredHeight, Is.GreaterThanOrEqualTo(88f));
                var icon = button.transform.Find("RouteIcon"); Assert.That(icon, Is.Not.Null);
                Assert.That(icon.GetComponents<Component>().Any(x => x != null && x.GetType().Name == "UrbanUiIcon"), Is.True);
                Assert.That(icon.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(go.transform.Find("PlanTitle"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(route); }
        }

        [Test]
        public void DailyPlan_CloseRemainsAvailableOutsideScheduleMode()
        {
            var go = new GameObject("daily-close-fixture", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<DailyPlanPanel>(); panel.Bind(Array.Empty<DailyPlanRouteDefinition>(), null, null);
                var close = go.GetComponentsInChildren<Button>(true).FirstOrDefault(x => x.name == "PlanCloseButton");
                Assert.That(close, Is.Not.Null); Assert.That(close.gameObject.activeInHierarchy, Is.True);
                close.onClick.Invoke(); Assert.That(go.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void DailyPlan_ScheduleFooterLeavesOuterMoldingInset()
        {
            var go=new GameObject("daily-footer-fixture",typeof(RectTransform));
            try{var panel=go.AddComponent<DailyPlanPanel>();panel.BindSchedule(Array.Empty<DailyPlanRouteDefinition>(),null,null);var navigation=go.transform.Find("DiaryNavigation") as RectTransform;Assert.That(navigation,Is.Not.Null);Assert.That(navigation.offsetMin.y,Is.GreaterThanOrEqualTo(36));Assert.That(navigation.offsetMax.y,Is.LessThanOrEqualTo(94));}
            finally{Object.DestroyImmediate(go);}
        }
        [Test]
        public void CareerInterest_MoreThanSixRemainReachableAndDetailsAreClipped()
        {
            var go = new GameObject("career-urban-fixture", typeof(RectTransform));
            var interests = new CareerInterestDefinition[8];
            try
            {
                for (int i=0;i<interests.Length;i++) interests[i] = ScriptableObject.CreateInstance<CareerInterestDefinition>();
                var panel = go.AddComponent<CareerInterestPanel>();
                panel.Show(interests, (CareerInterestProgress)null, null, null, 1);
                Assert.That(panel.GetInterestButtonForTests(7), Is.Not.Null, "All configured interests need reachable cards, not a fixed six-item cap.");
                Assert.That(panel.GetInterestButtonForTests(7).gameObject.activeSelf, Is.True);
                var root = go.transform.Find("CareerInterestRoot");
                Assert.That(root.GetComponentsInChildren<ScrollRect>(true).Any(x => x.horizontal), Is.True);
                Assert.That(root.GetComponentsInChildren<ScrollRect>(true).Any(x => x.vertical && x.viewport.GetComponent<RectMask2D>() != null), Is.True);
            }
            finally { Object.DestroyImmediate(go); foreach (var item in interests) if (item != null) Object.DestroyImmediate(item); }
        }

        [Test]
        public void CareerInterest_CardIconAndClosePreserveActualButtonPath()
        {
            var go = new GameObject("career-card-fixture", typeof(RectTransform));
            var interest = ScriptableObject.CreateInstance<CareerInterestDefinition>();
            try
            {
                var panel = go.AddComponent<CareerInterestPanel>(); panel.Show(new[] { interest }, (CareerInterestProgress)null, null, null, 1);
                var button = panel.GetInterestButtonForTests(0); var icon = button.transform.Find("InterestIcon");
                Assert.That(icon, Is.Not.Null); Assert.That(icon.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(icon.GetComponents<Component>().Any(x => x != null && x.GetType().Name == "UrbanUiIcon"), Is.True);
                var close=go.GetComponentsInChildren<Button>(true).First(x=>x.name=="CloseButton");close.onClick.Invoke();
                Assert.That(panel.IsOpen, Is.False);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(interest); }
        }
    }
}
