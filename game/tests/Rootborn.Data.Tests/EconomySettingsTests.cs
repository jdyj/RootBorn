using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 어느 아이템이 돈인지(지갑으로 가는 통화 아이템)는 game/economy.json이 정한다.
    /// 아이템 정의에는 통화임을 알리는 필드나 분류가 없다(분류는 Misc). 코드는 아이템 id를 모른다.
    /// </summary>
    public sealed class EconomySettingsTests
    {
        [Test]
        public void RealData_HasACurrencyItem_FromTheGameSideFile()
        {
            var result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            result.ThrowIfFailed();
            Assert.That(result.Definitions.Currency, Is.Not.Null, "game/economy.json의 currencyItem");
            Assert.That(result.Definitions.All<ItemDefinition>(), Does.Contain(result.Definitions.Currency), "정의 표의 같은 객체");
        }

        [Test]
        public void TheCurrency_IsTheItemFurnitureIsPricedIn()
        {
            // 가게(가구 가격), 알바 보상, 시설 비용이 가리키는 아이템이 지갑으로 가야 한다.
            var result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            var prices = result.Definitions.All<Rootborn.Core.Placement.FurnitureDefinition>()
                .Where(f => f.PriceItem != null).Select(f => f.PriceItem).Distinct().ToArray();
            Assert.That(prices, Is.EqualTo(new[] { result.Definitions.Currency }));
        }

        [Test]
        public void NoFile_NoCurrency_MoneyStaysAnItem()
        {
            using var dir = new TempDir();
            var data = Path.Combine(dir.Path, "data");
            Directory.CreateDirectory(data);
            File.WriteAllText(Path.Combine(data, "registry.json"), "{}");
            File.WriteAllText(Path.Combine(data, "items.json"),
                "[{\"type\":\"ItemDefinition\",\"id\":\"coin\",\"maxStack\":99,\"category\":2,\"displayKey\":\"\",\"description\":\"\",\"toolSpritePrefix\":\"\"}]");

            var result = GameDataLoader.Load(data, RootbornBuilders.Register);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Definitions.Currency, Is.Null);
        }

        [Test]
        public void TheFileNamesAnItem_ThatItemIsTheCurrency()
        {
            using var dir = WithEconomy("{\"currencyItem\":\"coin\"}");
            var result = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Definitions.Currency, Is.SameAs(result.Definitions.Get<ItemDefinition>("coin")));
        }

        [TestCase("{\"currencyItem\":\"no-such-item\"}")]
        [TestCase("{\"currencyItem\":\"\"}")]
        [TestCase("{}")]
        [TestCase("{ not json")]
        public void ABrokenFile_IsALoadError(string json)
        {
            using var dir = WithEconomy(json);
            var result = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Errors.Select(e => e.Source), Has.Some.Contains("economy.json"));
            Assert.That(result.Definitions.Currency, Is.Null);
        }

        private static TempDir WithEconomy(string json)
        {
            var dir = new TempDir();
            var data = Path.Combine(dir.Path, "data");
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(Path.Combine(dir.Path, "game"));
            File.WriteAllText(Path.Combine(data, "registry.json"), "{}");
            File.WriteAllText(Path.Combine(data, "items.json"),
                "[{\"type\":\"ItemDefinition\",\"id\":\"coin\",\"maxStack\":99,\"category\":2,\"displayKey\":\"\",\"description\":\"\",\"toolSpritePrefix\":\"\"}]");
            File.WriteAllText(Path.Combine(dir.Path, "game", "economy.json"), json);
            return dir;
        }
    }
}
