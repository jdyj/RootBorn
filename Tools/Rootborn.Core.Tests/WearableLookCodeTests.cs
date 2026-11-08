using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Characters;

namespace Rootborn.Core.Tests
{
    public sealed class WearableLookCodeTests
    {
        private static WearableCatalog Catalog()
        {
            var slots = new[]
            {
                new WearableSlotRule("slot.outfit", 1, new[] { "outfit.casual" }, null),
                new WearableSlotRule("slot.hair", 2, new[] { "hair.short" }, null),
            };
            var wearables = new[]
            {
                new WearableRule("outfit.casual", "slot.outfit", new[] { "stage.child", "stage.adult" }, null),
                new WearableRule("outfit.chef", "slot.outfit", new[] { "stage.adult" }, "facility.chef"),
                new WearableRule("hair.short", "slot.hair", new[] { "stage.child", "stage.adult" }, null),
            };
            var stages = new[] { new GrowthStageRule("stage.child", 1, false), new GrowthStageRule("stage.adult", 1, true) };
            return new WearableCatalog(slots, wearables, stages);
        }

        [Test]
        public void EncodeThenDecodeGivesTheSameLook()
        {
            string code = WearableLookCode.Encode("stage.adult", new[] { "outfit.chef", "hair.short" });
            Assert.That(WearableLookCode.TryDecode(Catalog(), code, out string stage, out List<string> ids), Is.True);
            Assert.That(stage, Is.EqualTo("stage.adult"));
            Assert.That(ids, Is.EqualTo(new[] { "outfit.chef", "hair.short" }));
        }

        [Test]
        public void AnEmptyLookIsAStageWithoutLayers()
        {
            string code = WearableLookCode.Encode("stage.child", null);
            Assert.That(WearableLookCode.TryDecode(Catalog(), code, out string stage, out List<string> ids), Is.True);
            Assert.That(stage, Is.EqualTo("stage.child"));
            Assert.That(ids, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("stage.unknown|hair.short")]
        [TestCase("stage.child|hair.unknown")]
        [TestCase("stage.child|outfit.chef")]
        [TestCase("stage.child|hair.short,hair.short")]
        public void CodesNamingUnknownOrUndrawableItemsAreRejected(string code)
        {
            // A remote player can only show items the game ships, with art for the stage, at most once.
            Assert.That(WearableLookCode.TryDecode(Catalog(), code, out _, out _), Is.False);
        }

        [Test]
        public void TheCodeFitsTheNetworkLimitForTheShippedData()
        {
            string code = WearableLookCode.Encode("stage.school", new[] { "wearable.outfit_school_uniform", "wearable.hair_short_brown" });
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(code), Is.LessThanOrEqualTo(WearableLookCode.MaxBytes));
        }
    }
}
