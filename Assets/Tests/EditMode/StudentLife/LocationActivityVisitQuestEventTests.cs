using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class LocationActivityVisitQuestEventTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void ActualLocationActivityInteractor_EmitsVisitOnlyWhenAllowed_AndRevisitsRemainCreditable(bool bound)
        {
            var bridge = typeof(QuestEvent).Assembly.GetType("Rootborn.Game.Quests.QuestGameplayEventBridge");
            Assert.That(bridge, Is.Not.Null);
            var recorded = bridge.GetEvent("OnRecorded", BindingFlags.Static | BindingFlags.Public);
            Assert.That(recorded, Is.Not.Null);
            string temp = Path.Combine(Path.GetTempPath(), "rootborn-location-actual", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp); SaveService.SetRootDirectoryForTests(temp);
            var player = new GameObject("ActualVisitPlayer", typeof(StudentLifeProgressComponent));
            var student = player.GetComponent<StudentLifeProgressComponent>();
            student.ConfigureForTests("visit-slot", "visit-player", 20, 20, 0, 480);
            var point = new GameObject("ActualLocationActivityPoint", typeof(LocationActivityInteractor));
            var interactor = point.GetComponent<LocationActivityInteractor>();
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("location.fixture", "fixture", Vector2.zero, null);
            var identity = ScriptableObject.CreateInstance<LocationIdentityDefinition>();
            identity.ConfigureForTests("identity.fixture", "fixture", location, "fixture", null, null, 480, 1140);
            if (bound) interactor.Bind(identity);
            var events = new List<QuestEvent>();
            Action<QuestEvent, StudentLifeProgress> handler = (e, p) =>
            {
                Assert.That(p, Is.SameAs(student.Progress));
                Assert.That(e.Kind.ToString(), Is.EqualTo("LocationVisit"));
                Assert.That(typeof(QuestEvent).GetField("Location").GetValue(e), Is.SameAs(location));
                events.Add(e);
            };
            recorded.AddEventHandler(null, handler);
            try
            {
                Assert.That(interactor.TryInteract(player), Is.EqualTo(bound));
                Assert.That(events.Count, Is.EqualTo(bound ? 1 : 0));
                Assert.That(interactor.TryInteract(player), Is.EqualTo(bound));
                Assert.That(events.Count, Is.EqualTo(bound ? 2 : 0));
                if (bound) Assert.That(events[0].EventKey, Is.Not.EqualTo(events[1].EventKey));
            }
            finally
            {
                recorded.RemoveEventHandler(null, handler);
                Object.DestroyImmediate(point); Object.DestroyImmediate(player); Object.DestroyImmediate(identity); Object.DestroyImmediate(location);
                SaveService.SetRootDirectoryForTests(null); if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }
    }
}
