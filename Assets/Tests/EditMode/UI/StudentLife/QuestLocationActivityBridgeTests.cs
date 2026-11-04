using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class QuestLocationActivityBridgeTests
    {
        [TestCase(0, 1)]
        [TestCase(999, 0)]
        public void LocationActivityButton_PublishesOnlyCommittedSuccess_WithThePanelActor(int energyCost, int expectedEvents)
        {
            var bridge = typeof(QuestEvent).Assembly.GetType("Rootborn.Game.Quests.QuestGameplayEventBridge");
            Assert.That(bridge, Is.Not.Null, "Missing shared gameplay event bridge.");
            var recorded = bridge.GetEvent("OnRecorded", BindingFlags.Public | BindingFlags.Static);
            Assert.That(recorded, Is.Not.Null);
            string tempRoot = Path.Combine(Path.GetTempPath(), "rootborn-location-quest", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            SaveService.SetRootDirectoryForTests(tempRoot);
            var canvas = new GameObject("QuestActivityCanvas", typeof(RectTransform), typeof(Canvas));
            var panelGo = new GameObject("QuestActivityPanel");
            panelGo.transform.SetParent(canvas.transform, false);
            var panel = panelGo.AddComponent<LocationIdentityPanel>();
            var player = new GameObject("QuestActivityPlayer", typeof(StudentLifeProgressComponent));
            var student = player.GetComponent<StudentLifeProgressComponent>();
            student.ConfigureForTests("activity-slot", "activity-player", 20, 20, 0, 480);
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("location.fixture", "fixture", Vector2.zero, null);
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            activity.ConfigureForTests("activity.fixture", "fixture", location, LocationGrowthRoute.SelfStudy,
                1, energyCost, 0, 0, null, null);
            var identity = ScriptableObject.CreateInstance<LocationIdentityDefinition>();
            identity.ConfigureForTests("identity.fixture", "fixture", location, "fixture", new[] { activity }, null, 480, 1140);
            int count = 0;
            Action<QuestEvent, StudentLifeProgress> handler = (e, p) =>
            {
                if (e.Kind != QuestEventKind.LocationActivity) return;
                count++;
                Assert.That(e.Activity, Is.SameAs(activity));
                Assert.That(e.EventKey, Is.Not.Empty);
                Assert.That(p, Is.SameAs(student.Progress));
                Assert.That(p.HasAppliedRequest(e.EventKey), Is.True, "Credit requires the successful activity request.");
            };
            recorded.AddEventHandler(null, handler);
            try
            {
                panel.Show(identity, player);
                panel.ClickActivityForTests(0);
                Assert.That(count, Is.EqualTo(expectedEvents));
            }
            finally
            {
                recorded.RemoveEventHandler(null, handler);
                Object.DestroyImmediate(canvas); Object.DestroyImmediate(player);
                Object.DestroyImmediate(identity); Object.DestroyImmediate(activity); Object.DestroyImmediate(location);
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            }
        }
    }
}
