using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Time;
using Rootborn.UI.HUD;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class UrbanHudPresentationTests
    {
        private Scene _scene;
        private GameObject _trackedHost;

        [SetUp]
        public void SetUp() => _scene = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown()
        {
            if (_trackedHost != null) Object.DestroyImmediate(_trackedHost);
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
        }

        private LifeTimePresentation InstallClock()
        {
            return (LifeTimePresentation)typeof(TownTimeHudRuntimeInstaller)
                .GetMethod("EnsureInScene", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { _scene });
        }

        [Test]
        public void ClockInstaller_UsesSharedSkinCompactBoundsAndOneDecorativeIcon()
        {
            var source = InstallClock();
            var root = (RectTransform)source.transform;
            Assert.That(root.GetComponent<ModernUiTileImage>(), Is.Not.Null, "The real Town clock must use the approved shared molding renderer.");
            Assert.That(root.rect.width, Is.LessThanOrEqualTo(440f));
            Assert.That(root.rect.height, Is.GreaterThanOrEqualTo(144f));
            var icon = root.Find("UrbanTimeIcon");
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.GetComponents<Component>().Any(c => c != null && c.GetType().Name == "UrbanUiIcon"), Is.True);
            Assert.That(icon.GetComponent<Image>().raycastTarget, Is.False);
            var label = root.Find("WorldTimeLabel").GetComponent<Text>();
            AssertInside(root, label.rectTransform, 36f);
            Assert.That(label.color.r, Is.LessThan(0.4f), "Ivory panels need navy text rather than the old yellow HUD label.");
        }

        [Test]
        public void ClockInstaller_ReinstallationKeepsExistingOwnerAndDecorativeIcon()
        {
            var first = InstallClock();
            var actor = new GameObject("ClockOwner");
            SceneManager.MoveGameObjectToScene(actor, _scene);
            var owner = actor.AddComponent<StudentLifeProgressComponent>();
            owner.ConfigureForTests("urban-hud", "hud-player", 12, 10, 0, 555);
            first.Bind(owner);
            var progress = owner.Progress;
            var icon = first.transform.Find("UrbanTimeIcon");
            Assert.That(icon, Is.Not.Null);
            var second = InstallClock();
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.Owner, Is.SameAs(owner));
            Assert.That(owner.Progress, Is.SameAs(progress));
            Assert.That(second.Minutes, Is.EqualTo(555));
            Assert.That(second.transform.Find("UrbanTimeIcon"), Is.SameAs(icon));
            Assert.That(second.GetComponentsInChildren<Transform>(true).Count(t => t.name == "UrbanTimeIcon"), Is.EqualTo(1));
        }

        [Test]
        public void TrackedObjective_ReopenKeepsOneIconAndAllThreeRowsInsideMolding()
        {
            _trackedHost = new GameObject("TrackedHost", typeof(RectTransform), typeof(TrackedObjectiveHud));
            ((RectTransform)_trackedHost.transform).sizeDelta = new Vector2(360f, 96f);
            var hud = _trackedHost.GetComponent<TrackedObjectiveHud>();
            var item = new ObjectiveJournalItem("test.goal", "Goals", "A helpful action in the town", "Fallback detail", "Active", "1 / 3", "Choose an activity in the journal", false, 0);
            hud.Show(item);
            var root = (RectTransform)_trackedHost.transform.Find("TrackedObjectiveHudRoot");
            Assert.That(root.rect.width, Is.GreaterThanOrEqualTo(600f));
            Assert.That(root.rect.height, Is.GreaterThanOrEqualTo(200f));
            var icon = root.Find("TrackedObjectiveIcon");
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.GetComponent<Image>().raycastTarget, Is.False);
            foreach (var label in root.GetComponentsInChildren<Text>(true)) AssertInside(root, label.rectTransform, 36f);
            Assert.That(hud.VisibleText, Does.Contain(item.Title).And.Contain(item.ProgressText).And.Contain(item.NextHintText));
            hud.Hide();
            hud.Show(new ObjectiveJournalItem("test.other", "Hints", "A different tracked objective", "", "Available", "2 / 3", "Follow the new hint", false, 0));
            Assert.That(root.Find("TrackedObjectiveIcon"), Is.SameAs(icon));
            Assert.That(_trackedHost.GetComponentsInChildren<Transform>(true).Count(t => t.name == "TrackedObjectiveHudRoot"), Is.EqualTo(1));
            Assert.That(hud.VisibleText, Does.Contain("A different tracked objective").And.Not.Contain(item.Title));
        }

        private static void AssertInside(RectTransform outer, RectTransform child, float inset)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var p = outer.InverseTransformPoint(corner);
                Assert.That(p.x, Is.InRange(outer.rect.xMin + inset - 0.01f, outer.rect.xMax - inset + 0.01f), child.name + " horizontal molding inset");
                Assert.That(p.y, Is.InRange(outer.rect.yMin + inset - 0.01f, outer.rect.yMax - inset + 0.01f), child.name + " vertical molding inset");
            }
        }
    }
}
