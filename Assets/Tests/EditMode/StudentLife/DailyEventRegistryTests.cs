using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyEventRegistryTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void DAILYEVENT_EDIT_001_007_RegistryLoadsDailyEventDefinitionsAndCacheResolvesWithoutRepeatedScans()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.GreaterOrEqual(registry.DailyEventKinds.Length, 2, "DAILYEVENT-EDIT-001 failed: daily event kinds must be registered.");
            Assert.GreaterOrEqual(registry.DailyEvents.Length, 2, "DAILYEVENT-EDIT-001 failed: at least two MVP daily events must be registered.");

            int eventsWithChoices = 0;
            bool hasDeferrable = false;
            bool hasOptional = false;
            for (int i = 0; i < registry.DailyEvents.Length; i++)
            {
                var dailyEvent = registry.DailyEvents[i];
                Assert.IsNotNull(dailyEvent, "DAILYEVENT-EDIT-001 failed: registry contains null daily event.");
                Assert.IsNotNull(dailyEvent.Kind, dailyEvent.Id + " must reference a kind definition.");
                Assert.IsNotNull(dailyEvent.Location, dailyEvent.Id + " must reference a location definition.");
                Assert.GreaterOrEqual(dailyEvent.Choices.Count, 2, dailyEvent.Id + " must expose player choices.");
                StringAssert.StartsWith("Assets/Data/StudentLife/DailyEvents/", AssetDatabase.GetAssetPath(dailyEvent));
                eventsWithChoices++;
                hasDeferrable |= dailyEvent.Kind.IsDeferrable;
                hasOptional |= dailyEvent.Kind.IsOptional;
            }

            Assert.GreaterOrEqual(eventsWithChoices, 2, "DAILYEVENT-EDIT-001 failed: two playable daily events must have choices.");
            Assert.IsTrue(hasDeferrable, "DAILYEVENT-EDIT-004 failed: at least one MVP daily event must support deferral.");
            Assert.IsTrue(hasOptional, "DAILYEVENT-EDIT-001 failed: MVP events should be optional-centered.");

            var cache = new GameDataLookupCache(registry);
            Assert.IsTrue(cache.TryGetDailyEvent(registry.DailyEvents[0].Id, out var cachedEvent), "DAILYEVENT-EDIT-007 failed: cache cannot resolve daily event.");
            Assert.AreSame(registry.DailyEvents[0], cachedEvent);
            Assert.AreEqual(1, cache.BuildCount, "DAILYEVENT-EDIT-007 failed: daily event lookup should reuse the built cache.");
        }

        [Test]
        public void DAILYEVENT_EDIT_002_SourceDoesNotBranchByDailyEventEntityIds()
        {
            string[] files =
            {
                "Assets/Scripts/Game/StudentLife/DailyEventDefinition.cs",
                "Assets/Scripts/Game/StudentLife/DailyEventProgress.cs",
                "Assets/Scripts/Game/StudentLife/DailyEventRunner.cs",
                "Assets/Scripts/Game/StudentLife/DailyEventOutcomes.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("eventId ==", source, files[i] + " must not branch by event id.");
                StringAssert.DoesNotContain("choiceId ==", source, files[i] + " must not branch by choice id.");
                StringAssert.DoesNotContain("locationId ==", source, files[i] + " must not branch by location id.");
                StringAssert.DoesNotContain("switch (eventId", source, files[i] + " must not switch by event id.");
                StringAssert.DoesNotContain("switch (choiceId", source, files[i] + " must not switch by choice id.");
            }
        }
    }
}
