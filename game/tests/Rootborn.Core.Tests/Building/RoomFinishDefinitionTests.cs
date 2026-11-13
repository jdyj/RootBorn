using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Tests.Building
{
    /// <summary>꾸밈 정의의 값 약속(생성자로 만든다, 잘못된 값은 던지지 않고 IsValid로 알린다, 비용은 복사본). 자동 테스트.</summary>
    public sealed class RoomFinishDefinitionTests
    {
        private static RoomFinishDefinition Make(string id = "f", string slot = "wall", string sheet = "s.png", string art = "a",
            IReadOnlyList<ItemAmount<string>> cost = null, string token = null) =>
            new RoomFinishDefinition(id, slot, "k", sheet, art, cost ?? new[] { new ItemAmount<string>("item", 2) }, token);

        [Test]
        public void ValuesComeFromTheConstructor()
        {
            var f = Make(token: "unlock.x");
            Assert.That((f.Id, f.Slot, f.NameKey, f.Sheet, f.Art, f.UnlockToken), Is.EqualTo(("f", "wall", "k", "s.png", "a", "unlock.x")));
            Assert.That(f.Cost.Count, Is.EqualTo(1));
            Assert.That(f.IsValid, Is.True);
        }

        [Test]
        public void NullTextBecomesEmpty_AndNullCostBecomesNoCost()
        {
            var f = new RoomFinishDefinition(null, null, null, null, null, null, null);
            Assert.That((f.Id, f.Slot, f.NameKey, f.Sheet, f.Art, f.UnlockToken), Is.EqualTo(("", "", "", "", "", "")));
            Assert.That(f.Cost, Is.Empty);
            Assert.That(f.IsValid, Is.False);
        }

        [TestCase("", "wall", "s.png", "a")]
        [TestCase("f", "", "s.png", "a")]
        [TestCase("f", "wall", "", "a")]
        [TestCase("f", "wall", "s.png", "")]
        public void MissingRequiredText_IsNotValid(string id, string slot, string sheet, string art) =>
            Assert.That(Make(id, slot, sheet, art).IsValid, Is.False);

        [TestCase(0)]
        [TestCase(-3)]
        public void NonPositiveCost_IsNotValid(int count) =>
            Assert.That(Make(cost: new[] { new ItemAmount<string>("item", count) }).IsValid, Is.False);

        [Test]
        public void CostWithoutAnItem_IsNotValid() =>
            Assert.That(Make(cost: new[] { new ItemAmount<string>("", 1) }).IsValid, Is.False);

        [Test]
        public void NoCostIsValid_FreeFinish() => Assert.That(Make(cost: new ItemAmount<string>[0]).IsValid, Is.True);

        [Test]
        public void CostIsACopy_ChangingTheSourceListDoesNotChangeTheDefinition()
        {
            var source = new[] { new ItemAmount<string>("item", 2) };
            var f = Make(cost: source);
            source[0] = new ItemAmount<string>("other", 9);
            Assert.That(f.Cost[0].Item, Is.EqualTo("item"));
        }

        [Test]
        public void SlotNamesAreTheDataValues()
        {
            Assert.That((RoomFinishSlots.Wall, RoomFinishSlots.Floor), Is.EqualTo(("wall", "floor")));
        }
    }
}
