using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 꾸밈(벽지·바닥) 정의 data/building/RoomFinishDefinition.json: 실제 데이터 10종이 읽히고, 잘못된 정의는 읽기 오류다(자동 테스트).
    /// 값(종류, 비용, 처음부터 열린 것)은 시험용이라 값 자체가 아니라 모양과 연결을 본다.
    /// </summary>
    public sealed class RoomFinishDefinitionTests
    {
        private static LoadResult LoadReal() => GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);

        private static string One(string id = "finish.wall.x", string slot = "wall", string sheet = "art/s.png", string art = "paper", string counts = "[2]") =>
            @"[{ ""type"": ""RoomFinishDefinition"", ""id"": """ + id + @""", ""slot"": """ + slot + @""", ""nameKey"": ""finish.x.name"",
                ""sheet"": """ + sheet + @""", ""art"": """ + art + @""",
                ""costItems"": [{ ""ref"": ""item.allowance"", ""type"": ""ItemDefinition"" }], ""costCounts"": " + counts + @", ""unlockToken"": """" }]";

        private static LoadResult LoadOne(string json)
        {
            using var dir = new TempDir();
            dir.Write("data/a/ItemDefinition.json", @"[{ ""type"": ""ItemDefinition"", ""id"": ""item.allowance"" }]");
            dir.Write("data/b/RoomFinishDefinition.json", json);
            return GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);
        }

        [Test]
        public void RealData_LoadsTenFinishes_WithoutErrors()
        {
            var result = LoadReal();
            Assert.That(result.Errors.Select(e => e.ToString()), Is.Empty);
            Assert.That(result.UnclassifiedTypes, Is.Empty);
            Assert.That(result.Definitions.All<RoomFinishDefinition>().Count, Is.EqualTo(10));
        }

        [Test]
        public void RealData_SlotsAreWallOrFloor_AndEveryFinishIsValid()
        {
            var finishes = LoadReal().Definitions.All<RoomFinishDefinition>();
            Assert.That(finishes.Select(f => f.Slot).Distinct().OrderBy(s => s), Is.EqualTo(new[] { RoomFinishSlots.Floor, RoomFinishSlots.Wall }));
            Assert.That(finishes.Count(f => f.Slot == RoomFinishSlots.Wall), Is.EqualTo(5));
            Assert.That(finishes.Count(f => f.Slot == RoomFinishSlots.Floor), Is.EqualTo(5));
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
            var finishes = LoadReal().Definitions.All<RoomFinishDefinition>();
            Assert.That(finishes.Count(f => f.UnlockToken.Length == 0), Is.EqualTo(6));
            Assert.That(finishes.Where(f => f.UnlockToken.Length > 0).Select(f => f.UnlockToken), Is.All.StartWith("unlock.finish."));
            Assert.That(finishes.Select(f => f.UnlockToken).Where(t => t.Length > 0), Is.Unique);
        }

        [Test]
        public void RealData_CostItemsExist_AndCountsArePositive_PerCellValuesOfTheTrial()
        {
            var result = LoadReal();
            result.ThrowIfFailed();
            foreach (var f in result.Definitions.All<RoomFinishDefinition>())
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
        public void EmptyRequiredField_IsLoadError(string field)
        {
            var json = field switch
            {
                "slot" => One(slot: ""),
                "sheet" => One(sheet: ""),
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
    }
}
