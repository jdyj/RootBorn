using System.IO;
using NUnit.Framework;
using Rootborn.Game.Save;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.WorldState
{
    public sealed class WorldStatePersistenceTests
    {
        [Test]
        public void WORLD_STATE_EDIT_006_007_PersistenceSavesLoadsPersonalAndSharedWorldStatePerPlayer()
        {
            var root = Path.Combine(Application.temporaryCachePath, "rootborn-world-state-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            SaveService.SetRootDirectoryForTests(root);
            try
            {
                var shared = MakeFlag("world.library.archive-open", WorldStateScopeKind.Shared);
                var personal = MakeFlag("world.mentor.notice", WorldStateScopeKind.Personal);
                var progress = WorldStateProgressPersistence.LoadOrCreate("slot-0", "player-a");

                progress.TryActivate(shared, new WorldStateActivationSource("chain.library", "step.archive", "event.archive", 2));
                progress.TryActivate(personal, new WorldStateActivationSource("chain.mentor", "step.notice", "event.notice", 3));
                progress.MarkNotificationSeen(personal);
                WorldStateProgressPersistence.Save(progress);

                var loadedA = WorldStateProgressPersistence.LoadOrCreate("slot-0", "player-a");
                var loadedB = WorldStateProgressPersistence.LoadOrCreate("slot-0", "player-b");

                Assert.IsTrue(loadedA.IsActive(shared), "WORLD-STATE-EDIT-006 failed: shared active flag should restore for same player.");
                Assert.IsTrue(loadedA.IsActive(personal), "WORLD-STATE-EDIT-006 failed: personal active flag should restore for same player.");
                Assert.AreEqual(WorldStateScopeKind.Shared, loadedA.GetRecord(shared).Scope);
                Assert.AreEqual(WorldStateScopeKind.Personal, loadedA.GetRecord(personal).Scope);
                Assert.IsTrue(loadedA.GetRecord(personal).SeenNotification);
                Assert.IsTrue(loadedB.IsActive(shared), "WORLD-STATE-EDIT-007 failed: shared world-state flags must be visible to another player in the same save slot.");
                Assert.IsFalse(loadedB.IsActive(personal), "WORLD-STATE-EDIT-007 failed: personal world-state flags must not leak between players.");
            }
            finally
            {
                SaveService.SetRootDirectoryForTests(null);
                Directory.Delete(root, true);
            }
        }

        private static WorldStateFlagDefinition MakeFlag(string id, WorldStateScopeKind scope)
        {
            var flag = ScriptableObject.CreateInstance<WorldStateFlagDefinition>();
            flag.ConfigureForTests(
                id,
                id + ".name",
                id + ".desc",
                null,
                null,
                null,
                scope,
                WorldStateChangeKind.ObjectRevealed,
                id + ".next",
                new[] { WorldStateSummarySurface.DayResult, WorldStateSummarySurface.WorldLog },
                new[] { WorldStateBadgeKind.New },
                1,
                1);
            return flag;
        }
    }
}
