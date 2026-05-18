using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class TownLocationNpcFoundationTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void LOCNPC_EDIT_001_003_RegistryLoadsTownLocationsCategoriesNpcsAndRoles()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.GreaterOrEqual(registry.LocationCategories.Length, 4, "LOCNPC-EDIT-001 failed: location categories are missing from registry.");
            Assert.GreaterOrEqual(registry.NpcRoles.Length, 4, "LOCNPC-EDIT-001 failed: NPC roles are missing from registry.");
            Assert.GreaterOrEqual(registry.Locations.Length, 4, "LOCNPC-EDIT-002 failed: at least four major Town locations must be registered.");
            Assert.GreaterOrEqual(registry.Npcs.Length, 4, "LOCNPC-EDIT-003 failed: at least four core NPCs must be registered.");

            for (int i = 0; i < registry.Locations.Length; i++)
            {
                var location = registry.Locations[i];
                Assert.IsNotNull(location, "LOCNPC-EDIT-002 failed: registry has a null location.");
                Assert.IsFalse(string.IsNullOrEmpty(location.Id), "LOCNPC-EDIT-002 failed: location id is missing.");
                Assert.IsFalse(string.IsNullOrEmpty(location.DisplayNameKey), location.Id + " must have a display name key.");
                Assert.IsFalse(string.IsNullOrEmpty(location.DescriptionKey), location.Id + " must have a description key.");
                Assert.IsNotNull(location.Category, location.Id + " must have a category.");
                Assert.IsFalse(string.IsNullOrEmpty(location.WorldAnchorId), location.Id + " must have a world anchor id.");
                Assert.Greater(location.RelatedRoles.Count, 0, location.Id + " must expose related role data.");
                StringAssert.StartsWith("Assets/Data/StudentLife/Locations/", AssetDatabase.GetAssetPath(location));
            }

            for (int i = 0; i < registry.Npcs.Length; i++)
            {
                var npc = registry.Npcs[i];
                Assert.IsNotNull(npc, "LOCNPC-EDIT-003 failed: registry has a null NPC.");
                Assert.IsFalse(string.IsNullOrEmpty(npc.Id), "LOCNPC-EDIT-003 failed: npc id is missing.");
                Assert.IsFalse(string.IsNullOrEmpty(npc.DisplayNameKey), npc.Id + " must have a display name key.");
                Assert.IsFalse(string.IsNullOrEmpty(npc.IntroductionKey), npc.Id + " must have an introduction key.");
                Assert.IsNotNull(npc.HomeLocation, npc.Id + " must have a default location.");
                Assert.Greater(npc.Roles.Count, 0, npc.Id + " must expose role data.");
                Assert.IsNotNull(npc.ResolveDialogue(new StudentLifeProgress("slot", "player", 8, 8)), npc.Id + " must resolve a default dialogue.");
                StringAssert.StartsWith("Assets/Data/NPCs/", AssetDatabase.GetAssetPath(npc));
            }
        }

        [Test]
        public void LOCNPC_EDIT_004_VisitAndDialogueRulesUseStrategyArraysWithoutEntityIdBranching()
        {
            var category = ScriptableObject.CreateInstance<LocationCategoryDefinition>();
            var role = ScriptableObject.CreateInstance<NpcRoleDefinition>();
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            try
            {
                category.ConfigureForTests("location.category.school", "School", 10);
                role.ConfigureForTests("npc.role.teacher", "Teacher", 10);
                location.ConfigureForTests(
                    "location.school",
                    "location.school.name",
                    "location.school.desc",
                    category,
                    "anchor.school",
                    new Vector2(1f, 2f),
                    new[] { role },
                    Array.Empty<NpcDefinition>(),
                    Array.Empty<DiscoveryDefinition>(),
                    Array.Empty<LocationVisitRuleBase>());
                npc.ConfigureForTests(
                    "npc.teacher",
                    "npc.teacher.name",
                    "npc.teacher.intro",
                    location,
                    new[] { role },
                    null,
                    Array.Empty<NpcDialogueConditionBase>());

                Assert.AreSame(category, location.Category);
                CollectionAssert.Contains((System.Collections.ICollection)location.RelatedRoles, role);
                CollectionAssert.Contains((System.Collections.ICollection)npc.Roles, role);
                Assert.IsTrue(location.CanVisit(new StudentLifeProgress("slot", "player", 8, 8)));
                Assert.IsTrue(npc.CanUseDefaultDialogue(new StudentLifeProgress("slot", "player", 8, 8)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(npc);
                UnityEngine.Object.DestroyImmediate(location);
                UnityEngine.Object.DestroyImmediate(role);
                UnityEngine.Object.DestroyImmediate(category);
            }

            string[] files =
            {
                "Assets/Scripts/Game/StudentLife/LocationDefinition.cs",
                "Assets/Scripts/Game/StudentLife/LocationVisitProgress.cs",
                "Assets/Scripts/Game/Dialogue/NpcDefinition.cs",
                "Assets/Scripts/Game/Common/GameDataRegistry.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("locationId ==", source, files[i] + " must not branch by location id.");
                StringAssert.DoesNotContain("npcId ==", source, files[i] + " must not branch by npc id.");
                StringAssert.DoesNotContain("switch (locationId", source, files[i] + " must not switch by location id.");
                StringAssert.DoesNotContain("switch (npcId", source, files[i] + " must not switch by npc id.");
            }
        }

        [Test]
        public void LOCNPC_EDIT_005_006_LocationAndNpcProgressPersistsPerSaveSlotPlayerAndDedupe()
        {
            var category = ScriptableObject.CreateInstance<LocationCategoryDefinition>();
            var role = ScriptableObject.CreateInstance<NpcRoleDefinition>();
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            try
            {
                category.ConfigureForTests("location.category.public", "Public", 1);
                role.ConfigureForTests("npc.role.guide", "Guide", 1);
                location.ConfigureForTests("location.square", "location.square.name", "location.square.desc", category, "anchor.square", Vector2.zero, new[] { role }, Array.Empty<NpcDefinition>(), Array.Empty<DiscoveryDefinition>(), Array.Empty<LocationVisitRuleBase>());
                npc.ConfigureForTests("npc.guide", "npc.guide.name", "npc.guide.intro", location, new[] { role }, null, Array.Empty<NpcDialogueConditionBase>());
                var progress = new StudentLifeProgress("slot-a", "player-a", 8, 8);
                var visitProgress = new LocationVisitProgress(progress);

                Assert.IsTrue(visitProgress.TryRecordLocationVisit(location, out var firstLocation));
                Assert.IsTrue(visitProgress.TryRecordNpcMeeting(npc, out var firstNpc));
                Assert.IsFalse(visitProgress.TryRecordLocationVisit(location, out var duplicateLocation));
                Assert.IsFalse(visitProgress.TryRecordNpcMeeting(npc, out var duplicateNpc));

                var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData(), null, null, null);
                var restoredVisits = new LocationVisitProgress(restored);

                Assert.AreEqual(LocationVisitResultKind.FirstVisit, firstLocation.Kind);
                Assert.AreEqual(LocationVisitResultKind.FirstMeeting, firstNpc.Kind);
                Assert.AreEqual(LocationVisitResultKind.DuplicateVisit, duplicateLocation.Kind);
                Assert.AreEqual(LocationVisitResultKind.DuplicateMeeting, duplicateNpc.Kind);
                Assert.IsTrue(restoredVisits.HasVisited(location));
                Assert.IsTrue(restoredVisits.HasMet(npc));
                Assert.AreEqual(1, restoredVisits.VisitedLocationIds.Length, "LOCNPC-EDIT-006 failed: duplicate location visit created extra state.");
                Assert.AreEqual(1, restoredVisits.MetNpcIds.Length, "LOCNPC-EDIT-006 failed: duplicate NPC meeting created extra state.");
                CollectionAssert.Contains(restored.GetActivityLogIds(), location.Id, "LOCNPC-EDIT-005 failed: location visit was not visible to encyclopedia/activity unlock systems.");
                CollectionAssert.Contains(restored.GetActivityLogIds(), npc.Id, "LOCNPC-EDIT-005 failed: NPC meeting was not visible to encyclopedia/activity unlock systems.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(npc);
                UnityEngine.Object.DestroyImmediate(location);
                UnityEngine.Object.DestroyImmediate(role);
                UnityEngine.Object.DestroyImmediate(category);
            }
        }

        [Test]
        public void LOCNPC_EDIT_007_GameDataLookupCacheFindsLocationsAndNpcsWithoutRepeatedRegistryScans()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            var cache = new GameDataLookupCache(registry);

            Assert.IsTrue(cache.TryGetLocation("location.town-square", out var square), "LOCNPC-EDIT-007 failed: location cache cannot resolve town square.");
            Assert.IsTrue(cache.TryGetNpc("npc.first-guide", out var guide), "LOCNPC-EDIT-007 failed: NPC cache cannot resolve first guide.");
            Assert.AreSame(square, cache.GetLocationOrNull("location.town-square"));
            Assert.AreSame(guide, cache.GetNpcOrNull("npc.first-guide"));
            Assert.AreEqual(1, cache.BuildCount, "LOCNPC-EDIT-007 failed: cache rebuilt during lookup.");
        }
    }
}
