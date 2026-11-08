using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Characters;

namespace Rootborn.Core.Tests
{
    // Mirrors today's art: three growth stages, hair per stage, seven outfits each drawn for one stage,
    // two of them work uniforms. Face/hat/accessory have no art yet, so they stay empty.
    public sealed class WearableRulesTests
    {
        private static readonly string[] All = { "playground", "school", "adult" };

        private static WearableCatalog Catalog(params WearableRule[] extra)
        {
            var slots = new[]
            {
                new WearableSlotRule("face", 20, null, null),
                new WearableSlotRule("top", 10, new[] { "casual", "school_casual", "adult_casual" }, null),
                new WearableSlotRule("hair", 30, new[] { "short_brown" }, null),
                new WearableSlotRule("hat", 40, null, new[] { "hair" }),
                new WearableSlotRule("accessory", 50, null, null),
            };
            var wearables = new List<WearableRule>
            {
                new WearableRule("short_brown", "hair", All, null),
                new WearableRule("casual", "top", new[] { "playground" }, null),
                new WearableRule("school_casual", "top", new[] { "school" }, null),
                new WearableRule("school_uniform", "top", new[] { "school" }, null),
                new WearableRule("adult_casual", "top", new[] { "adult" }, null),
                new WearableRule("office", "top", new[] { "adult" }, null),
                new WearableRule("chef", "top", new[] { "adult" }, "facility.chef"),
            };
            wearables.AddRange(extra);
            var stages = new[]
            {
                new GrowthStageRule("adult", 1, true),
                new GrowthStageRule("school", 2, false),
                new GrowthStageRule("playground", 1, false),
            };
            return new WearableCatalog(slots, wearables, stages);
        }

        private static string[] Ids(IEnumerable<WearableLayer> layers) => layers.Select(l => l.SlotId + ":" + l.WearableId).ToArray();

        [Test]
        public void SelectStage_UsesTheHighestReachedStudentStageOrTheAdultStage()
        {
            var stages = Catalog().Stages;
            Assert.That(WearableRules.SelectStage(stages, 1, false), Is.EqualTo("playground"));
            Assert.That(WearableRules.SelectStage(stages, 2, false), Is.EqualTo("school"));
            Assert.That(WearableRules.SelectStage(stages, 30, false), Is.EqualTo("school"));
            Assert.That(WearableRules.SelectStage(stages, 2, true), Is.EqualTo("adult"));
            Assert.That(WearableRules.SelectStage(stages, 0, false), Is.Empty);
        }

        [Test]
        public void ResolveLayers_DrawsDefaultsInDrawOrderAndLeavesArtlessSlotsEmpty()
        {
            var layers = WearableRules.ResolveLayers(Catalog(), new WearableState(), "school", null);
            Assert.That(Ids(layers), Is.EqualTo(new[] { "top:school_casual", "hair:short_brown" }));
            Assert.That(layers.Select(l => l.DrawOrder), Is.Ordered);
        }

        [Test]
        public void Choose_StageOnlyItemsAreRememberedPerStageSoGrowingUpKeepsEachChoice()
        {
            var catalog = Catalog();
            var state = new WearableState();
            Assert.That(WearableRules.Choose(catalog, state, "top", "school_uniform", "school"), Is.True);
            Assert.That(WearableRules.Choose(catalog, state, "top", "office", "adult"), Is.True);

            Assert.That(Ids(WearableRules.ResolveLayers(catalog, state, "school", null)), Does.Contain("top:school_uniform"));
            Assert.That(Ids(WearableRules.ResolveLayers(catalog, state, "adult", null)), Does.Contain("top:office"));
            Assert.That(Ids(WearableRules.ResolveLayers(catalog, state, "playground", null)), Does.Contain("top:casual"));
        }

        [Test]
        public void Choose_AnItemDrawnForEveryStageBecomesTheGeneralChoiceAndClearsStageChoices()
        {
            var catalog = Catalog(new WearableRule("cap", "hat", All, null), new WearableRule("hoodie", "top", All, null));
            var state = new WearableState();
            WearableRules.Choose(catalog, state, "top", "school_uniform", "school");
            Assert.That(WearableRules.Choose(catalog, state, "top", "hoodie", "playground"), Is.True);

            foreach (string stage in All)
                Assert.That(Ids(WearableRules.ResolveLayers(catalog, state, stage, null)), Does.Contain("top:hoodie"), stage);
            Assert.That(state.Get(WearableState.StageKey("top", "school")), Is.Empty);
        }

        [Test]
        public void Choose_RejectsWrongSlotMissingArtAndUnknownItemsWithoutChangingState()
        {
            var catalog = Catalog();
            var state = new WearableState();
            Assert.That(WearableRules.Choose(catalog, state, "hair", "office", "adult"), Is.False, "wrong slot");
            Assert.That(WearableRules.Choose(catalog, state, "top", "office", "school"), Is.False, "no art at this stage");
            Assert.That(WearableRules.Choose(catalog, state, "top", "missing", "adult"), Is.False, "unknown item");
            Assert.That(state.Count, Is.Zero);
        }

        [Test]
        public void Choose_AWorkUniformCanAlsoBeWornAsNormalClothes()
        {
            // The wardrobe has always offered the chef and interior outfits; the facility only adds auto-wearing at work.
            var catalog = Catalog();
            var state = new WearableState();
            Assert.That(WearableRules.Choose(catalog, state, "top", "chef", "adult"), Is.True);
            Assert.That(Ids(WearableRules.ResolveLayers(catalog, state, "adult", null)), Does.Contain("top:chef"));
        }

        [Test]
        public void ResolveLayers_WorkUniformReplacesTheTopOnlyWhileAtThatFacility()
        {
            var catalog = Catalog();
            var state = new WearableState();
            WearableRules.Choose(catalog, state, "top", "office", "adult");
            Assert.That(Ids(WearableRules.ResolveLayers(catalog, state, "adult", "facility.chef")), Does.Contain("top:chef"));
            Assert.That(Ids(WearableRules.ResolveLayers(catalog, state, "adult", "facility.interior")), Does.Contain("top:office"));
            Assert.That(Ids(WearableRules.ResolveLayers(catalog, state, "school", "facility.chef")), Does.Contain("top:school_casual"),
                "a uniform without art for the stage is not forced on");
        }

        [Test]
        public void ResolveLayers_AHatHidesTheHairSlot()
        {
            var catalog = Catalog(new WearableRule("cap", "hat", All, null));
            var state = new WearableState();
            WearableRules.Choose(catalog, state, "hat", "cap", "school");
            Assert.That(Ids(WearableRules.ResolveLayers(catalog, state, "school", null)), Is.EqualTo(new[] { "top:school_casual", "hat:cap" }));
        }

        [Test]
        public void ResolveLayers_ASavedChoiceForAnItemThatNoLongerExistsFallsBackToTheDefault()
        {
            var state = WearableState.FromEntries(new[] { new KeyValuePair<string, string>("top@school", "deleted_item") });
            Assert.That(Ids(WearableRules.ResolveLayers(Catalog(), state, "school", null)), Does.Contain("top:school_casual"));
        }

        [Test]
        public void State_EntriesRoundTripInStableOrder()
        {
            var catalog = Catalog();
            var state = new WearableState();
            WearableRules.Choose(catalog, state, "top", "office", "adult");
            WearableRules.Choose(catalog, state, "top", "school_uniform", "school");
            var entries = state.ToEntries();
            Assert.That(entries.Select(e => e.Key), Is.EqualTo(new[] { "top@adult", "top@school" }));
            var copy = WearableState.FromEntries(entries);
            Assert.That(copy.ToEntries(), Is.EqualTo(entries));
        }

        [Test]
        public void Migrate_OldPerBodyOutfitChoicesBecomeStageChoicesAndNeverOverrideNewOnes()
        {
            var catalog = Catalog();
            var map = new Dictionary<string, string>
            {
                { "school_short_brown_school_uniform", "school_uniform" },
                { "adult_short_brown_office", "office" },
            };
            var current = new WearableState();
            WearableRules.Choose(catalog, current, "top", "adult_casual", "adult");

            var migrated = WearableRules.MigrateLegacyChoices(catalog, current,
                new[] { "school_short_brown_school_uniform", "adult_short_brown_office", "unknown_look" }, map);

            Assert.That(migrated.Get("top", "school"), Is.EqualTo("school_uniform"));
            Assert.That(migrated.Get("top", "adult"), Is.EqualTo("adult_casual"), "a choice already made wins over the old one");
            Assert.That(current.Get("top", "school"), Is.Empty, "the input state is not modified");
        }

        [Test]
        public void Choices_ListEveryItemWithArtForTheStageIncludingWorkUniforms()
        {
            var ids = Catalog().Choices("top", "adult").Select(w => w.Id).ToArray();
            Assert.That(ids, Is.EqualTo(new[] { "adult_casual", "chef", "office" }), "catalog order is by id");
        }
    }
}
