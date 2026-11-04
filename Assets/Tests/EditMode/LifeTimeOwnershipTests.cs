using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Time;
using Rootborn.UI.HUD;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace Rootborn.Tests.EditMode
{
    public sealed class LifeTimeOwnershipTests
    {
        [Test]
        public void LegacyClockKeepsSameDisplayUntilDayRolls()
        {
            var clockObject = new GameObject("LegacyClock");
            var view = new GameObject("ClockView", typeof(RectTransform));
            try
            {
                var clock = clockObject.AddComponent<GameClock>();
                view.SetActive(false);
                var label = view.AddComponent<Text>();
                var hud = view.AddComponent<TimeHud>();
                typeof(TimeHud).GetField("_clock", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, clock);
                hud.BindLabelForRuntime(label);
                view.SetActive(true);
                typeof(TimeHud).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
                for (int i = 0; i < 8; i++) clock.Tick(0f);
                string before = label.text;
                for (int i = 0; i < 64; i++) clock.Tick(0f);
                Assert.That(label.text, Is.EqualTo(before));
                clock.Tick(clock.RealSecondsPerGameDay);
                StringAssert.Contains("Day 2", label.text);
            }
            finally
            {
                var hud = view.GetComponent<TimeHud>();
                if (hud != null) typeof(TimeHud).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
                Object.DestroyImmediate(view);
                Object.DestroyImmediate(clockObject);
            }
        }
    }
}

