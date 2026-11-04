using NUnit.Framework;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;
namespace Rootborn.Tests.EditMode.UI.Modern
{
    public sealed class UrbanInventoryLayoutTests
    {
        private GameObject _go;
        private RectTransform _root;
        [SetUp] public void Setup()
        {
            _go = new GameObject("Inventory", typeof(RectTransform));
            _root = (RectTransform)_go.transform;
            _go.AddComponent<ModernUiInventoryPanel>().Show();
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(_go); }
        [Test] public void InventoryRows_AreClippedToThreeRowsAndScrollbarReachesLastCapacityRow()
        {
            var scroll = _go.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll, Is.Not.Null, "Inventory needs a functional scroll viewport.");
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.vertical, Is.True); Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.content.childCount, Is.EqualTo(Rootborn.Game.Common.Inventory.MaxSlots));
            var first = (RectTransform)scroll.content.GetChild(0);
            var last = (RectTransform)scroll.content.GetChild(Rootborn.Game.Common.Inventory.MaxSlots - 1);
            float stride = -((RectTransform)scroll.content.GetChild(5)).anchoredPosition.y;
            Assert.That(scroll.viewport.rect.height, Is.EqualTo(stride * 2f + first.rect.height).Within(0.1f));
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.verticalScrollbar.handleRect, Is.Not.Null);
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 0f;
            var corners = new Vector3[4]; last.GetWorldCorners(corners);
            foreach (var point in corners)
            {
                float y = scroll.viewport.InverseTransformPoint(point).y;
                Assert.That(y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 0.1f));
                Assert.That(y, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 0.1f));
            }
        }
        [Test] public void HeaderSearchAndViewport_StayInsideOuterMolding()
        {
            var title = _go.transform.Find("Title");
            Assert.That(title, Is.Not.Null); Assert.That(title.GetComponent<Text>().text, Is.Not.Empty);
            AssertInside((RectTransform)title);
            foreach (string name in new[] { "SearchField", "DeleteButton", "FilterButton", "InventoryInnerPanel" })
                AssertInside((RectTransform)_go.transform.Find(name));
            var scroll = _go.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(_go.transform.Find("InventoryInnerPanel").GetComponent<ModernUiTileImage>(), Is.Null,
                "Do not add a second molding frame beneath the first row.");
        }
        private void AssertInside(RectTransform child)
        {
            Assert.That(child, Is.Not.Null);
            var bounds = _root.rect; bounds.xMin += 36f; bounds.xMax -= 36f; bounds.yMin += 36f; bounds.yMax -= 36f;
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            foreach (var point in corners) Assert.That(bounds.Contains(_root.InverseTransformPoint(point)), Is.True, child.name + " crosses the molding.");
        }
    }
}
