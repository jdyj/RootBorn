using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode
{
    public sealed class LifeTimeHudTests
    {
        private GameObject _actor;
        private GameObject _view;
        private StudentLifeProgressComponent _owner;
        private TimeHud _hud;
        private Text _label;
        private GameDataRegistry _registry;
        private GameTextCatalog _catalog;
        private string _previousLanguage;

        [SetUp]
        public void SetUp()
        {
            _previousLanguage = GameText.Language;
            GameText.Language = "en";
            _catalog = ScriptableObject.CreateInstance<GameTextCatalog>();
            _catalog.ConfigureJson("{\"entries\":[{\"key\":\"life.time.hud\",\"en\":\"Day {0}  {1:00}:{2:00}\",\"ko\":\"{0}일  {1:00}:{2:00}\"}]}");
            _registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            typeof(GameDataRegistry).GetField("_textCatalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_registry, _catalog);
            GameDataRegistryProvider.SetOverrideForTests(_registry);
            _actor = new GameObject("LifeTimeHudTestActor");
            _owner = _actor.AddComponent<StudentLifeProgressComponent>();
            _owner.ConfigureForTests("clock-test", "clock-player", 12, 10, 0, 480);
            _view = new GameObject("LifeTimeHudTestView", typeof(RectTransform));
            _label = _view.AddComponent<Text>();
            _hud = _view.AddComponent<TimeHud>();
            _hud.BindLabelForRuntime(_label);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_view);
            Object.DestroyImmediate(_actor);
            GameDataRegistryProvider.ClearOverrideForTests();
            GameDataRegistryProvider.ResetCacheForTests();
            Object.DestroyImmediate(_registry);
            Object.DestroyImmediate(_catalog);
            GameText.Language = _previousLanguage;
        }

        private void Bind()
        {
            var method = typeof(TimeHud).GetMethod("BindLifeProgress", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Offline Town HUD needs the shared student/adult progress owner, not the legacy midnight clock.");
            method.Invoke(_hud, new object[] { _owner });
        }

        [Test]
        public void BindingExistingOwnerImmediatelyShowsItsDayAndTime()
        {
            Bind();
            StringAssert.Contains("Day 1", _label.text);
            StringAssert.Contains("08:00", _label.text);
        }

        [Test]
        public void SpendingTimeRefreshesWithoutAnUpdatePoll()
        {
            Bind();
            _owner.Progress.Spend(75, 0, 0, 0);
            StringAssert.Contains("09:15", _label.text);
        }

        [Test]
        public void RestoredAggregateReplacesObservedProgressAndDetachesOldInstance()
        {
            Bind();
            var previous = _owner.Progress;
            var save = previous.ToSaveData();
            save.CurrentDay = 6;
            save.TimeMinutes = 1110;
            _owner.RestoreFromSaveData(save, null, null, null);
            StringAssert.Contains("Day 6", _label.text);
            StringAssert.Contains("18:30", _label.text);
            var restoredText = _label.text;
            previous.Spend(10, 0, 0, 0);
            Assert.That(_label.text, Is.EqualTo(restoredText));
            _owner.Progress.Spend(15, 0, 0, 0);
            StringAssert.Contains("18:45", _label.text);
        }

        [Test]
        public void StartingNextDayRefreshesDayAndMorningTogether()
        {
            Bind();
            _owner.Progress.Spend(600, 0, 0, 0);
            Assert.That(_owner.Progress.TryEndDay(null, out _), Is.True);
            Assert.That(_owner.Progress.TryStartNextDay(), Is.True);
            StringAssert.Contains("Day 2", _label.text);
            StringAssert.Contains("08:00", _label.text);
        }
    }
}

