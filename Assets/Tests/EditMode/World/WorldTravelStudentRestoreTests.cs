using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Network.Session;
using Rootborn.UI.Careers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.World
{
    public sealed class WorldTravelStudentRestoreTests
    {
        [Test] public void WORLD_TRANSFER_RESTORE_001_ExistingAggregateStudentIsNotReplacedBySceneLegacyLoader()
        {
            string root = Path.Combine(Path.GetTempPath(), "rb-student-view-" + Guid.NewGuid().ToString("N"));
            string slot = "restore-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var oldMetadata = ActiveSaveContext.Metadata; var oldConfig = GameBootstrap.Config; var oldSession = NetworkBootstrap.ActiveSession;
            GameDataRegistryProvider.TryGet(out var oldRegistry); var objects = new List<Object>();
            try
            {
                SaveService.SetRootDirectoryForTests(root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = slot });
                typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = slot }); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
                var calendar = ScriptableObject.CreateInstance<LifeCalendarDefinition>(); objects.Add(calendar); calendar.ConfigureForTests(3, 3);
                var registry = ScriptableObject.CreateInstance<GameDataRegistry>(); objects.Add(registry); typeof(GameDataRegistry).GetField("_studentLifeCalendar", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(registry, calendar); GameDataRegistryProvider.SetOverrideForTests(registry);
                var actor = new GameObject("CarriedPlayer"); objects.Add(actor); actor.AddComponent<PlayerIdentity>().Configure("actor");
                var component = actor.AddComponent<StudentLifeProgressComponent>(); component.ConfigureForTests(slot, "actor", 12, 10, 0, 480); component.Progress.ConfigureCalendar(calendar); actor.AddComponent<PlayerInventory>();
                var owner = AdultCareerRuntimeComponent.Ensure(actor, registry); Assert.That(owner.SaveCurrent(), Is.True); var model = component.Progress;
                Assert.That(StudentLifeProgressPersistence.TryLoad(component, null, null, null), Is.True);
                Assert.That(component.Progress, Is.SameAs(model), "Scene re-entry must retain the current aggregate's student model and its event subscriptions.");
            }
            finally
            {
                for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
                SaveService.SetRootDirectoryForTests(null); ActiveSaveContext.Set(oldMetadata); GameDataRegistryProvider.SetOverrideForTests(oldRegistry); typeof(GameBootstrap).GetProperty("Config").SetValue(null, oldConfig); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, oldSession);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
