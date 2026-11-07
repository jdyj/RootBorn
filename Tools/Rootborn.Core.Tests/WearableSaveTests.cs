using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Characters;

namespace Rootborn.Core.Tests
{
    public sealed class WearableSaveTests
    {
        private static readonly string[] All = { "playground", "school", "adult" };

        private static WearableCatalog Catalog()
        {
            return new WearableCatalog(
                new[]
                {
                    new WearableSlotRule("top", 10, new[] { "casual", "school_casual", "adult_casual" }, null),
                    new WearableSlotRule("hair", 30, new[] { "short_brown" }, null),
                },
                new[]
                {
                    new WearableRule("short_brown", "hair", All, null),
                    new WearableRule("casual", "top", new[] { "playground" }, null),
                    new WearableRule("school_casual", "top", new[] { "school" }, null),
                    new WearableRule("school_uniform", "top", new[] { "school" }, null),
                    new WearableRule("adult_casual", "top", new[] { "adult" }, null),
                    new WearableRule("office", "top", new[] { "adult" }, null),
                },
                new[]
                {
                    new GrowthStageRule("adult", 1, true),
                    new GrowthStageRule("school", 2, false),
                    new GrowthStageRule("playground", 1, false),
                });
        }

        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { "school_short_brown_school_uniform", "school_uniform" },
            { "adult_short_brown_office", "office" },
            { "adult_short_brown_casual", "adult_casual" },
        };

        private static KeyValuePair<string, string> Pair(string key, string value) => new KeyValuePair<string, string>(key, value);

        [Test]
        public void SaveData_RoundTripsTheStateAndSkipsBlankEntries()
        {
            var catalog = Catalog();
            var state = new WearableState();
            WearableRules.Choose(catalog, state, "top", "office", "adult");
            WearableRules.Choose(catalog, state, "top", "school_uniform", "school");

            var data = WearableSaveData.From(state, legacyImported: true);
            Assert.That(data.version, Is.EqualTo(WearableSaveData.CurrentVersion));
            Assert.That(data.legacyImported, Is.True);
            Assert.That(data.entries.Select(e => e.key), Is.EqualTo(new[] { "top@adult", "top@school" }));

            data.entries = data.entries.Concat(new[] { new WearableSaveEntry { key = "", wearableId = "x" }, null,
                new WearableSaveEntry { key = "hat", wearableId = "" } }).ToArray();
            Assert.That(data.ToState().ToEntries(), Is.EqualTo(state.ToEntries()));
        }

        [Test]
        public void LegacyLookIds_PutWardrobeChoicesBeforeTheCreatorOutfitAndIgnoreOtherCreatorParts()
        {
            var ids = LegacyWardrobe.LookIds(
                new[] { Pair("adult_body", "adult_short_brown_office"), Pair("school_body", "") },
                new[] { Pair("outfit:adult_body", "adult_short_brown_casual"), Pair("hair", "short_brown"),
                        Pair("outfit:school_body", "school_short_brown_school_uniform") },
                "outfit:");
            Assert.That(ids, Is.EqualTo(new[] { "adult_short_brown_office", "adult_short_brown_casual", "school_short_brown_school_uniform" }));
        }

        [Test]
        public void Load_WithoutSaveImportsLegacyChoicesOnceAndAsksToSave()
        {
            var legacy = new[] { "adult_short_brown_office", "adult_short_brown_casual", "school_short_brown_school_uniform" };
            var result = WearableSaveRules.Load(Catalog(), null, legacy, Map);

            Assert.That(result.NeedsSave, Is.True);
            Assert.That(result.State.Get("top", "adult"), Is.EqualTo("office"), "the wardrobe choice wins over the creator outfit");
            Assert.That(result.State.Get("top", "school"), Is.EqualTo("school_uniform"));
            Assert.That(result.ToSaveData().legacyImported, Is.True);
        }

        [Test]
        public void Load_AfterImportIgnoresLegacyFilesSoChangedChoicesAreNotOverwritten()
        {
            var catalog = Catalog();
            var state = new WearableState();
            WearableRules.Choose(catalog, state, "top", "adult_casual", "adult");
            var saved = WearableSaveData.From(state, legacyImported: true);

            var result = WearableSaveRules.Load(catalog, saved, new[] { "adult_short_brown_office" }, Map);

            Assert.That(result.NeedsSave, Is.False);
            Assert.That(result.State.Get("top", "adult"), Is.EqualTo("adult_casual"));
        }

        [Test]
        public void Load_ASaveWrittenBeforeImportKeepsItsChoicesAndFillsOnlyMissingStages()
        {
            var catalog = Catalog();
            var state = new WearableState();
            WearableRules.Choose(catalog, state, "top", "adult_casual", "adult");
            var saved = WearableSaveData.From(state, legacyImported: false);

            var result = WearableSaveRules.Load(catalog, saved,
                new[] { "adult_short_brown_office", "school_short_brown_school_uniform" }, Map);

            Assert.That(result.NeedsSave, Is.True);
            Assert.That(result.State.Get("top", "adult"), Is.EqualTo("adult_casual"));
            Assert.That(result.State.Get("top", "school"), Is.EqualTo("school_uniform"));
        }

        [Test]
        public void Load_WithNothingSavedAndNoLegacyFilesStartsEmpty()
        {
            var result = WearableSaveRules.Load(Catalog(), null, null, null);
            Assert.That(result.State.Count, Is.Zero);
            Assert.That(result.NeedsSave, Is.True, "marking the import as done avoids rereading old files every load");
        }
    }
}
