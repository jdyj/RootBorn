using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 꾸밈(벽지·바닥·띠 색) 정의 data/building/RoomFinishDefinition.json: 실제 데이터 15종이 읽히고, 잘못된 정의는 읽기 오류다(자동 테스트).
    /// 값(종류, 비용, 처음부터 열린 것)은 시험용이라 값 자체가 아니라 모양과 연결을 본다.
    /// </summary>
    public sealed class RoomFinishDefinitionTests
    {
        private static LoadResult LoadReal() => GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);

        private static string One(string id = "finish.wall.x", string slot = "wall", string sheet = "art/s.png", string art = "paper", string counts = "[2]", string nameKey = "finish.x.name") =>
            @"[{ ""type"": ""RoomFinishDefinition"", ""id"": """ + id + @""", ""slot"": """ + slot + @""", ""nameKey"": """ + nameKey + @""",
                ""sheet"": """ + sheet + @""", ""art"": """ + art + @""",
                ""costItems"": [{ ""ref"": ""item.allowance"", ""type"": ""ItemDefinition"" }], ""costCounts"": " + counts + @", ""unlockToken"": """" }]";

        private static LoadResult LoadOne(string json)
        {
            using var dir = new TempDir();
            dir.Write("data/a/ItemDefinition.json", @"[{ ""type"": ""ItemDefinition"", ""id"": ""item.allowance"" }]");
            dir.Write("data/b/RoomFinishDefinition.json", json);
            return GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);
        }

        // 벽지·바닥(칸마다, 칸당 비용). 띠 색(집 하나에 하나, 무료)은 따로 본다(BandFinishes_AreFree_AndResolve).
        private static IEnumerable<RoomFinishDefinition> PerCell(IEnumerable<RoomFinishDefinition> all) => all.Where(f => f.Slot != RoomFinishSlots.Band);

        [Test]
        public void RealData_LoadsFifteenFinishes_WithoutErrors()
        {
            var result = LoadReal();
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(result.UnclassifiedTypes, Is.Empty);
            Assert.That(result.Definitions.All<RoomFinishDefinition>().Count, Is.EqualTo(15), "벽지 5 + 바닥 5 + 띠 색 5");
        }

        [Test]
        public void RealData_SlotsAreWallFloorOrBand_AndEveryFinishIsValid()
        {
            var finishes = LoadReal().Definitions.All<RoomFinishDefinition>();
            Assert.That(finishes.Select(f => f.Slot).Distinct().OrderBy(s => s, StringComparer.Ordinal),
                Is.EqualTo(new[] { RoomFinishSlots.Band, RoomFinishSlots.Floor, RoomFinishSlots.Wall }));
            Assert.That(finishes.Count(f => f.Slot == RoomFinishSlots.Wall), Is.EqualTo(5));
            Assert.That(finishes.Count(f => f.Slot == RoomFinishSlots.Floor), Is.EqualTo(5));
            Assert.That(finishes.Count(f => f.Slot == RoomFinishSlots.Band), Is.EqualTo(5));
            foreach (var f in finishes)
            {
                Assert.That(f.IsValid, Is.True, f.Id);
                Assert.That(f.NameKey, Is.Not.Empty, f.Id);
                Assert.That(f.Sheet, Does.EndWith(".png"), f.Id);
            }
        }

        [Test]
        public void RealData_SixFinishesAreOpenFromTheStart_FourNeedAToken()
        {
            var finishes = PerCell(LoadReal().Definitions.All<RoomFinishDefinition>()).ToList();
            Assert.That(finishes.Count(f => f.UnlockToken.Length == 0), Is.EqualTo(6));
            Assert.That(finishes.Where(f => f.UnlockToken.Length > 0).Select(f => f.UnlockToken), Is.All.StartWith("unlock.finish."));
            Assert.That(finishes.Select(f => f.UnlockToken).Where(t => t.Length > 0), Is.Unique);
        }

        [Test]
        public void RealData_CostItemsExist_AndCountsArePositive_PerCellValuesOfTheTrial()
        {
            var result = LoadReal();
            result.ThrowIfFailed();
            foreach (var f in PerCell(result.Definitions.All<RoomFinishDefinition>()))
            {
                Assert.That(f.Cost, Is.Not.Empty, f.Id);
                foreach (var amount in f.Cost)
                {
                    Assert.That(result.Definitions.TryGet<ItemDefinition>(amount.Item, out _), Is.True, f.Id + ": 모르는 아이템 " + amount.Item);
                    Assert.That(amount.Count, Is.GreaterThan(0), f.Id);
                }
            }

            // 시험용 값(결정 대기 1·9번): 벽 칸당 2, 바닥 칸당 1. 사용자가 정하면 데이터와 함께 고친다.
            Assert.That(result.Definitions.Get<RoomFinishDefinition>("finish.wall.paper").Cost.Single().Count, Is.EqualTo(2));
            Assert.That(result.Definitions.Get<RoomFinishDefinition>("finish.floor.plank").Cost.Single().Count, Is.EqualTo(1));
        }

        [Test]
        public void BandFinishes_AreFree_AndResolve()
        {
            // 띠 색 다섯(값은 시험용, 결정 대기 26번): 무료, 처음부터 열림, 그림(Art)은 그 시트 좌표표 bands의 줄 이름.
            var result = LoadReal();
            result.ThrowIfFailed();
            var bands = result.Definitions.All<RoomFinishDefinition>().Where(f => f.Slot == RoomFinishSlots.Band).ToList();
            Assert.That(bands.Select(b => b.Id), Is.EqualTo(new[] { "finish.band.cream", "finish.band.brown", "finish.band.light", "finish.band.slate", "finish.band.plaster" }));
            Assert.That(bands.Select(b => b.Art), Is.EqualTo(new[] { "wall.top.cream", "wall.top.brown", "wall.top.light", "wall.top.slate", "wall.plaster.top" }));
            foreach (var b in bands)
            {
                Assert.That(b.Cost, Is.Empty, b.Id + ": 무료");
                Assert.That(b.UnlockToken, Is.Empty, b.Id + ": 처음부터 열림");
                Assert.That(b.NameKey, Is.EqualTo(b.Id + ".name"), b.Id);
                string atlas = Path.Combine(RepoPaths.DataDirectory, "atlases", b.Sheet + ".atlas.json");
                Assert.That(File.Exists(atlas), Is.True, b.Id + ": 좌표표 " + atlas);
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(atlas));
                Assert.That(doc.RootElement.GetProperty("bands").TryGetProperty(b.Art, out _), Is.True, b.Id + ": bands에 " + b.Art);
            }
        }

        [Test]
        public void FreeBandDefinition_Loads_ButEmptyNameKeyIsLoadError()
        {
            const string free = @"[{ ""type"": ""RoomFinishDefinition"", ""id"": ""finish.band.x"", ""slot"": ""band"", ""nameKey"": ""finish.band.x.name"",
                ""sheet"": ""art/s.png"", ""art"": ""wall.top.x"", ""costItems"": [], ""costCounts"": [], ""unlockToken"": """" }]";
            var ok = LoadOne(free);
            Assert.That(ok.Errors.Any(e => e.Type == "RoomFinishDefinition"), Is.False, string.Join("\n", ok.Errors));
            Assert.That(ok.Definitions.Get<RoomFinishDefinition>("finish.band.x").Cost, Is.Empty);
            var noName = LoadOne(free.Replace("\"finish.band.x.name\"", "\"\""));
            Assert.That(noName.Errors.Any(e => e.Type == "RoomFinishDefinition"), Is.True, "이름 키가 비면 읽기 오류");
        }

        [Test]
        public void RealData_EveryFinishNameKeyIsInEveryLanguage()
        {
            var keys = LoadReal().Definitions.All<RoomFinishDefinition>().Select(f => f.NameKey).ToArray();
            var dirs = new[] { Path.Combine(RepoPaths.Root, "game", "localization"), Path.Combine(RepoPaths.Root, "data", "localization") };
            foreach (var file in Directory.GetFiles(dirs[0], "*.json"))
            {
                var text = File.ReadAllText(file);
                foreach (var key in keys) Assert.That(text, Does.Contain("\"" + key + "\""), Path.GetFileName(file) + ": " + key);
            }
        }

        [Test]
        public void ValidDefinition_Loads()
        {
            var result = LoadOne(One());
            Assert.That(result.Errors.Any(e => e.Type == "RoomFinishDefinition"), Is.False, string.Join("\n", result.Errors));
            Assert.That(result.Definitions.Get<RoomFinishDefinition>("finish.wall.x").Art, Is.EqualTo("paper"));
        }

        [TestCase("[0]", TestName = "ZeroCost")]
        [TestCase("[-1]", TestName = "NegativeCost")]
        [TestCase("[]", TestName = "CostCountsLengthDiffers")]
        public void BadCost_IsLoadError(string counts)
        {
            var result = LoadOne(One(counts: counts));
            Assert.That(result.Errors.Any(e => e.Type == "RoomFinishDefinition"), Is.True, string.Join("\n", result.Errors));
        }

        [TestCase("slot", TestName = "EmptySlot")]
        [TestCase("sheet", TestName = "EmptySheet")]
        [TestCase("art", TestName = "EmptyArt")]
        [TestCase("nameKey", TestName = "EmptyNameKey")]
        public void EmptyRequiredField_IsLoadError(string field)
        {
            var json = field switch
            {
                "slot" => One(slot: ""),
                "sheet" => One(sheet: ""),
                "nameKey" => One(nameKey: ""),
                _ => One(art: ""),
            };
            var result = LoadOne(json);
            Assert.That(result.Errors.Any(e => e.Type == "RoomFinishDefinition"), Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void DuplicateId_IsLoadError()
        {
            var two = One().TrimEnd(']') + "," + One().TrimStart('[');
            var result = LoadOne(two);
            Assert.That(result.Errors.Any(e => e.Kind == "duplicate" && e.Type == "RoomFinishDefinition"), Is.True, string.Join("\n", result.Errors));
        }

        // --- 가게 판매(나중에 더한 필드, 가구와 같은 꼴) ---

        [Test]
        public void WithoutSaleFields_ReadsAsNotForSale_OlderDataStillLoads()
        {
            var result = LoadOne(One());
            Assert.That(result.Errors.Where(e => e.Type == "RoomFinishDefinition").Select(e => e.ToString()), Is.Empty, "시험 아이템의 빠진 필드는 보지 않는다");
            var f = result.Definitions.Get<RoomFinishDefinition>("finish.wall.x");
            Assert.That((f.PriceItemId, f.Price, f.PurchaseLocationId, f.IsForSale), Is.EqualTo(("", 0, "", false)));
        }

        [Test]
        public void SaleFields_AreRead()
        {
            var json = One().Replace(@"""unlockToken"": """"",
                @"""unlockToken"": ""unlock.finish.x"", ""priceItem"": { ""ref"": ""item.allowance"", ""type"": ""ItemDefinition"" }, ""price"": 7, ""purchaseLocationId"": ""location.shop""");
            var result = LoadOne(json);
            Assert.That(result.Errors.Where(e => e.Type == "RoomFinishDefinition").Select(e => e.ToString()), Is.Empty, "시험 아이템의 빠진 필드는 보지 않는다");
            var f = result.Definitions.Get<RoomFinishDefinition>("finish.wall.x");
            Assert.That((f.PriceItemId, f.Price, f.PurchaseLocationId, f.IsForSale), Is.EqualTo(("item.allowance", 7, "location.shop", true)));
        }
    }
}
