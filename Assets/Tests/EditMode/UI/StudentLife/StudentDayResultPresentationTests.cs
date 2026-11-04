using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using Rootborn.UI.WorldState;
using UnityEngine;
using UnityEngine.UI;
namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class StudentDayResultPresentationTests
    {
        private GameObject _canvas;
        private StudentDayResultPanel _panel;
        [SetUp] public void Setup() { _canvas = new GameObject("ResultTestCanvas", typeof(Canvas)); _panel = StudentDayResultPanel.EnsureInScene(_canvas.GetComponent<Canvas>()); }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(_canvas); }
        private void Show() { _panel.Show(null, new StudentDaySummary(1, new string[0], new string[0], string.Empty)); }
        [Test] public void EnsureInScene_IsOwnedByRequestedCanvas()
        {
            var other = new GameObject("SecondCanvas", typeof(Canvas));
            try { var result = StudentDayResultPanel.EnsureInScene(other.GetComponent<Canvas>()); Assert.That(result, Is.Not.SameAs(_panel)); Assert.That(result.transform.IsChildOf(other.transform), Is.True); }
            finally { Object.DestroyImmediate(other); }
        }
        [Test] public void LongResults_AreInsideMaskedScrollableAutoHeightContent()
        {
            Show();
            _panel.AppendDailyPlanSummary(new DailyPlanDaySummary("route", "activity", true, Enumerable.Repeat("A long result line", 60).ToArray(), "next"));
            var scroll = _panel.GetComponentInChildren<ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null, "Long result content needs a scroll viewport");
            Assert.That(scroll.vertical, Is.True); Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            var result = _panel.GetComponentsInChildren<Text>(true).Single(t => t.name == "Results");
            Assert.That(result.transform.IsChildOf(scroll.content), Is.True);
            Assert.That(result.fontSize, Is.GreaterThanOrEqualTo(18));
            Assert.That(scroll.content.GetComponent<ContentSizeFitter>(), Is.Not.Null);
        }
        [Test] public void NextDayButton_RemainsInsidePanelOutsideScroll()
        {
            Show(); var root = (RectTransform)_panel.transform.Find("StudentDayResultRoot");
            var button = _panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "NextDayButton");
            var points = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(points);
            foreach (var point in points) Assert.That(root.rect.Contains(root.InverseTransformPoint(point)), Is.True, "Footer button crosses panel border");
            Assert.That(button.GetComponentInParent<ScrollRect>(), Is.Null);
        }
        [Test] public void ShowHideAndDestroy_LeaseGameplayInputWithoutLeaking()
        {
            Show(); Assert.That(UiGameplayInputLock.IsBlocked, Is.True);
            _panel.Hide(); Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
            Show(); Assert.That(UiGameplayInputLock.IsBlocked, Is.True);
            Object.DestroyImmediate(_panel.gameObject); Assert.That(UiGameplayInputLock.IsBlocked, Is.False);
        }
        [Test] public void ExtensionHosts_AreHiddenWithResultModal()
        {
            Show(); _panel.ShowWorldStateChanges(null); _panel.ShowLocationStates(null);
            var root = _panel.transform.Find("StudentDayResultRoot");
            foreach (string name in new[] { "WorldStateDayResultCards", "LocationStateDayResultCards" })
            {
                var host = _panel.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
                Assert.That(host.IsChildOf(root), Is.True, name + " leaks outside modal");
                Assert.That(host.GetComponentInParent<ScrollRect>(), Is.Not.Null);
            }
            _panel.Hide(); Assert.That(_panel.GetComponentsInChildren<Text>().Length, Is.Zero);
        }
    }
}
