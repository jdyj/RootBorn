using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Characters;
using Rootborn.Game.Characters.Wearables;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using UnityEditor;

namespace Rootborn.Tests.EditMode.Characters
{
    /// <summary>Resolving looks from the real wearable data, and saving choices with the one-time legacy move.</summary>
    public sealed class WearableLookTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-wearables-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static GameDataRegistry Registry() => AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

        private static string[] Names(WearableLook look)
        {
            var names = new List<string> { look.Body.name };
            foreach (var layer in look.Layers) names.Add(layer.name);
            return names.ToArray();
        }

        [Test]
        public void WEAR_LOOK_001_DefaultsFollowTheStageAndUniformsFollowTheWorkplace()
        {
            var resolver = new WearableLookResolver(Registry());
            Assert.That(resolver.HasData, Is.True);
            Assert.That(Names(resolver.Resolve(null, 1, false, null)),
                Is.EqualTo(new[] { "Sheet_body_playground", "Sheet_outfit_playground_casual", "Sheet_head_playground_short_brown" }));
            Assert.That(Names(resolver.Resolve(null, 2, false, null)),
                Is.EqualTo(new[] { "Sheet_body_school", "Sheet_outfit_school_school_uniform", "Sheet_head_school_short_brown" }));
            Assert.That(Names(resolver.Resolve(null, 9, true, "facility.chef")),
                Is.EqualTo(new[] { "Sheet_body_adult", "Sheet_outfit_adult_chef", "Sheet_head_adult_short_brown" }));
        }

        [Test]
        public void WEAR_LOOK_002_AChoiceForOneStageIsKeptAfterGrowingUp()
        {
            var resolver = new WearableLookResolver(Registry());
            var state = new WearableState();
            string school = resolver.StageFor(2, false);
            var uniform = resolver.FindWearable("wearable.outfit_school_uniform");
            Assert.That(WearableRules.Choose(resolver.Catalog, state, uniform.Slot.Id, uniform.Id, school), Is.True);

            Assert.That(Names(resolver.Resolve(state, 3, false, null))[1], Is.EqualTo("Sheet_outfit_school_school_uniform"));
            Assert.That(Names(resolver.Resolve(state, 3, true, null))[1], Is.EqualTo("Sheet_outfit_adult_office"), "no school uniform art for adults: the adult default");
            Assert.That(Names(resolver.Resolve(state, 3, false, null))[1], Is.EqualTo("Sheet_outfit_school_school_uniform"));
        }

        [Test]
        public void WEAR_LOOK_003_EveryLegacyAppearanceMapsToTheWearableThatDrawsItsOutfit()
        {
            var registry = Registry();
            var map = new WearableLookResolver(registry).LegacyAppearanceMap(registry.CharacterAppearances);
            Assert.That(map.Count, Is.EqualTo(7));
            Assert.That(map["school_short_brown_school_uniform"], Is.EqualTo("wearable.outfit_school_uniform"));
            Assert.That(map["adult_short_brown_chef"], Is.EqualTo("wearable.outfit_chef"));
            Assert.That(map["playground_short_brown_casual"], Is.EqualTo("wearable.outfit_casual"));
        }

        [Test]
        public void WEAR_SAVE_001_LegacyChoicesMoveInOnceAndLaterChoicesPersist()
        {
            var registry = Registry();
            var resolver = new WearableLookResolver(registry);
            var map = resolver.LegacyAppearanceMap(registry.CharacterAppearances);
            var storage = new SaveService("slot-a", _root);
            // The adult default is office wear, so the old casual choice shows that the import ran.
            var legacy = new[] { "adult_short_brown_adult_casual" };

            var state = WearableWardrobeStore.Load(storage, "player-1", resolver.Catalog, legacy, map);
            Assert.That(Names(resolver.Resolve(state, 1, true, null))[1], Is.EqualTo("Sheet_outfit_adult_adult_casual"));
            Assert.That(File.Exists(Path.Combine(storage.DirectoryPath, WearableWardrobeStore.FileNameFor("player-1"))), Is.True,
                "the import is recorded right away");

            string adult = resolver.StageFor(1, true);
            var office = resolver.FindWearable("wearable.outfit_office");
            Assert.That(WearableRules.Choose(resolver.Catalog, state, office.Slot.Id, office.Id, adult), Is.True);
            Assert.That(WearableWardrobeStore.Save(storage, "player-1", state), Is.True);

            var reloaded = WearableWardrobeStore.Load(storage, "player-1", resolver.Catalog, legacy, map);
            Assert.That(Names(resolver.Resolve(reloaded, 1, true, null))[1], Is.EqualTo("Sheet_outfit_adult_office"),
                "the old look does not come back after the import");
        }

        [Test]
        public void WEAR_SAVE_002_ABrokenFileFallsBackToDefaults()
        {
            var resolver = new WearableLookResolver(Registry());
            var storage = new SaveService("slot-a", _root);
            Directory.CreateDirectory(storage.DirectoryPath);
            File.WriteAllText(Path.Combine(storage.DirectoryPath, WearableWardrobeStore.FileNameFor("player-1")), "{ not json");

            var state = WearableWardrobeStore.Load(storage, "player-1", resolver.Catalog, null, null);
            Assert.That(Names(resolver.Resolve(state, 2, false, null))[1], Is.EqualTo("Sheet_outfit_school_school_uniform"));
        }
    }
}
