using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Time;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class UrbanLifeVitalsHudTests
    {
        private GameObject _host, _first, _second;
        [TearDown] public void Cleanup() { if (_host != null) UnityEngine.Object.DestroyImmediate(_host); if (_first != null) UnityEngine.Object.DestroyImmediate(_first); if (_second != null) UnityEngine.Object.DestroyImmediate(_second); }
        private static EventInfo Vitals(Type type) { var evt = type.GetEvent("VitalsChanged"); Assert.That(evt, Is.Not.Null, "Read-only vitals notification is required"); return evt; }
        [Test] public void Model_ZeroTimeSpendAndRestoresNotifyOnlyRealVitalsChanges()
        {
            var progress = new StudentLifeProgress("test", "test", 12, 10);
            int count = 0; Action onChange = () => count++;
            Vitals(typeof(StudentLifeProgress)).AddEventHandler(progress, onChange);
            progress.Spend(0, 2, 1, 0); Assert.That(count, Is.EqualTo(1));
            progress.Spend(0, 0, 0, 0); progress.Spend(5, 0, 0, 0); Assert.That(count, Is.EqualTo(1));
            progress.RestoreEnergyForDay(10); progress.RestoreFocusForDay(9); Assert.That(count, Is.EqualTo(1));
            progress.RestoreEnergyForDay(12); progress.RestoreFocusForDay(10); progress.Spend(0, 0, 0, 1); Assert.That(count, Is.EqualTo(4));
        }
        [Test] public void Component_ProgressReplacementDetachesOldModelAndNoOpDoesNotNotify()
        {
            _first = new GameObject("owner"); var owner = _first.AddComponent<StudentLifeProgressComponent>();
            owner.ConfigureForTests("test", "test", 12, 10, 0, 480);
            int count = 0; Action onChange = () => count++;
            Vitals(typeof(StudentLifeProgressComponent)).AddEventHandler(owner, onChange);
            var old = owner.Progress;
            owner.ConfigureForTests("test", "test", 12, 10, 0, 480); Assert.That(count, Is.Zero);
            old.Spend(0, 1, 1, 0); Assert.That(count, Is.Zero);
            owner.Progress.Spend(0, 1, 0, 0); Assert.That(count, Is.EqualTo(1));
            owner.ConfigureForTests("test", "test", 3, 4, 0, 480); Assert.That(count, Is.EqualTo(2));
        }
        [Test] public void Hud_UsesSameOwnerUpdatesWithoutTimeAndHidesSuspendedSource()
        {
            _host = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            var source = _host.AddComponent<LifeTimePresentation>();
            var hudType = typeof(Rootborn.UI.HUD.TownTimeHudRuntimeInstaller).Assembly.GetType("Rootborn.UI.HUD.UrbanLifeVitalsHud");
            Assert.That(hudType, Is.Not.Null, "Shared-owner vitals presenter is required");
            var hud = _host.AddComponent(hudType);
            hudType.GetMethod("Bind").Invoke(hud, new object[] { source });
            _first = new GameObject("first"); var first = _first.AddComponent<StudentLifeProgressComponent>(); first.ConfigureForTests("test", "first", 12, 10, 0, 480);
            source.Bind(first);
            AssertValues("12", "10"); first.Progress.Spend(0, 2, 3, 0); AssertValues("10", "7");
            _second = new GameObject("second"); var second = _second.AddComponent<StudentLifeProgressComponent>(); second.ConfigureForTests("test", "second", 5, 6, 0, 480); source.Bind(second);
            first.Progress.Spend(0, 1, 1, 0); AssertValues("5", "6");
            source.SetSuspended(true); Assert.That(_host.transform.Find("UrbanEnergyWidget").gameObject.activeSelf, Is.False);
            source.SetSuspended(false); AssertValues("5", "6");
            source.Bind(null); Assert.That(_host.transform.Find("UrbanFocusWidget").gameObject.activeSelf, Is.False);
        }
        private void AssertValues(string energy, string focus)
        {
            var energyRoot = _host.transform.Find("UrbanEnergyWidget"); var focusRoot = _host.transform.Find("UrbanFocusWidget");
            Assert.That(energyRoot.gameObject.activeSelf && focusRoot.gameObject.activeSelf, Is.True);
            Assert.That(energyRoot.Find("Value").GetComponent<Text>().text, Is.EqualTo(energy));
            Assert.That(focusRoot.Find("Value").GetComponent<Text>().text, Is.EqualTo(focus));
            Assert.That(((RectTransform)energyRoot).anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(((RectTransform)focusRoot).anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(((RectTransform)energyRoot).anchoredPosition.x+((RectTransform)energyRoot).sizeDelta.x,Is.LessThan(((RectTransform)focusRoot).anchoredPosition.x),"Resource panels must retain a gap");
            Assert.That(((RectTransform)energyRoot).sizeDelta, Is.EqualTo(new Vector2(140, 144)));
            Assert.That(energyRoot.GetComponent<Rootborn.UI.Modern.ModernUiTileImage>(), Is.Not.Null);
            Assert.That(energyRoot.Find("Icon").GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(_host.GetComponentsInChildren<Graphic>(true).Any(g => g.raycastTarget), Is.False);
            foreach(var text in _host.GetComponentsInChildren<Text>(true))Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+0.1f),text.name+" must fit the actual font line height");
        }
    }
}
