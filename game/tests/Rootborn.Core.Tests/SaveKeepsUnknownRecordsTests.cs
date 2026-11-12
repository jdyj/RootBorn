using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 데이터가 바뀐 뒤(아이템·퀘스트·체인이 빠짐, 묶음 크기가 줄어듦) 옛 세이브를 읽고 다시 써도 기록이 사라지지 않는다.
    /// 게임이 풀 수 없는 기록은 보이지 않게 들고 있다가 그대로 다시 쓰고, 데이터가 돌아오면 다시 살아난다(가구 배치가 이미 하던 방식).
    /// 전에는 읽을 때 버려서 다음 저장에 사라졌고, 퀘스트 id가 돌아오면 받은 보상을 다시 받을 수 있었다.
    /// </summary>
    public sealed class SaveKeepsUnknownRecordsTests
    {
        private static ItemDefinition Item(string id, int stack) => new ItemDefinition(id, stack);

        private static Func<string, ItemDefinition> Finder(params ItemDefinition[] items)
        {
            var byId = items.ToDictionary(i => i.Id);
            return id => byId.TryGetValue(id ?? string.Empty, out var item) ? item : null;
        }

        private static int Saved(PlayerInventorySaveData data, string id) => data.Items.Where(i => i.ItemId == id).Sum(i => i.Count);

        private static PlayerInventorySaveData Save(string id, int count)
            => new PlayerInventorySaveData { Items = new[] { new PlayerInventoryItemSaveData { ItemId = id, Count = count } } };

        [Test]
        public void Inventory_KeepsItemsTheDataNoLongerHas_AndBringsThemBackWhenItDoes()
        {
            var wood = Item("item.wood", 99);
            var save = new PlayerInventorySaveData { Items = new[]
            {
                new PlayerInventoryItemSaveData { ItemId = "item.removed", Count = 3 },
                new PlayerInventoryItemSaveData { ItemId = "item.wood", Count = 2 },
            } };

            var inventory = new ItemInventory();
            inventory.LoadFromSaveData(save, Finder(wood));
            Assert.That(inventory.Slots.Count, Is.EqualTo(1), "풀 수 없는 아이템은 칸을 차지하지 않는다");
            var written = inventory.ToSaveData(string.Empty);
            Assert.That(Saved(written, "item.removed"), Is.EqualTo(3), "다시 쓸 때 그대로 남는다");
            Assert.That(Saved(written, "item.wood"), Is.EqualTo(2));

            inventory.Add(wood, 5);
            written = inventory.ToSaveData(string.Empty);
            Assert.That(Saved(written, "item.removed"), Is.EqualTo(3), "가방이 바뀌어도 남는다");

            var removed = Item("item.removed", 99);
            var later = new ItemInventory();
            later.LoadFromSaveData(written, Finder(wood, removed));
            Assert.That(later.CountOf(removed), Is.EqualTo(3), "데이터가 돌아오면 가방에 다시 든다");
            Assert.That(Saved(later.ToSaveData(string.Empty), "item.removed"), Is.EqualTo(3), "두 번 세지 않는다");
        }

        [Test]
        public void Inventory_KeepsWhatNoLongerFits_WhenTheStackSizeShrinks()
        {
            // 묶음 99로 저장한 80개. 데이터가 묶음을 1로 줄이면 32칸에 32개만 들어간다.
            var small = Item("item.ore", 1);
            var inventory = new ItemInventory();
            inventory.LoadFromSaveData(Save("item.ore", 80), Finder(small));
            Assert.That(inventory.CountOf(small), Is.EqualTo(ItemInventory.MaxSlots));
            Assert.That(Saved(inventory.ToSaveData(string.Empty), "item.ore"), Is.EqualTo(80), "안 들어간 48개를 버리지 않는다");

            Assert.That(inventory.Remove(small, 10), Is.True);
            Assert.That(Saved(inventory.ToSaveData(string.Empty), "item.ore"), Is.EqualTo(70), "쓴 만큼만 준다");

            var again = new ItemInventory();
            again.LoadFromSaveData(inventory.ToSaveData(string.Empty), Finder(small));
            Assert.That(again.CountOf(small), Is.EqualTo(ItemInventory.MaxSlots), "자리가 나면 다시 읽을 때 채워진다");
            Assert.That(Saved(again.ToSaveData(string.Empty), "item.ore"), Is.EqualTo(70));

            var big = Item("item.ore", 99);
            var restored = new ItemInventory();
            restored.LoadFromSaveData(again.ToSaveData(string.Empty), Finder(big));
            Assert.That(restored.CountOf(big), Is.EqualTo(70), "묶음이 다시 커지면 전부 가방에 든다");
            Assert.That(restored.ToSaveData(string.Empty).Items.Sum(i => i.Count), Is.EqualTo(70));
        }

        [Test]
        public void Inventory_LoadingAgain_ReplacesWhatThePreviousLoadKept_AndClearKeepsIt()
        {
            var inventory = new ItemInventory();
            inventory.LoadFromSaveData(Save("item.removed", 3), Finder());
            inventory.LoadFromSaveData(new PlayerInventorySaveData(), Finder());
            Assert.That(inventory.ToSaveData(string.Empty).Items, Is.Empty, "다시 읽으면 앞서 읽은 것은 잊는다");

            inventory.LoadFromSaveData(Save("item.removed", 3), Finder());
            inventory.Clear();
            Assert.That(Saved(inventory.ToSaveData(string.Empty), "item.removed"), Is.EqualTo(3), "가방 칸을 비워도 풀 수 없는 기록은 남는다");
        }

        private static QuestDefinition Quest(string id)
            => new QuestDefinition(id, id + ".name", id + ".desc", new QuestObjective[] { new AnyEventObjective(1) }, null, null);

        [Test]
        public void QuestLog_KeepsRecordsOfQuestsTheDataNoLongerHas_SoAReturningQuestCannotPayAgain()
        {
            var kept = Quest("quest.kept");
            var gone = new QuestProgressSaveData
            {
                QuestId = "quest.gone", State = QuestStage.RewardClaimed, StateName = "RewardClaimed",
                ObjectiveCounts = new[] { 1 }, ProcessedEventKeys = new[] { "k1" },
            };
            var log = new QuestLog(new[] { kept });
            log.LoadFromSaveData(new QuestLogSaveData { Quests = new[] { gone } });
            log.Accept(kept);

            var written = log.ToSaveData();
            var record = written.Quests.Single(q => q.QuestId == "quest.gone");
            Assert.That(record.StateName, Is.EqualTo("RewardClaimed"));
            Assert.That(record.ObjectiveCounts, Is.EqualTo(new[] { 1 }));
            Assert.That(record.ProcessedEventKeys, Is.EqualTo(new[] { "k1" }));
            Assert.That(written.Quests.Count(q => q.QuestId == "quest.kept"), Is.EqualTo(1));

            var back = Quest("quest.gone");
            var later = new QuestLog(new[] { kept, back });
            later.LoadFromSaveData(written);
            Assert.That(later.GetState(back), Is.EqualTo(QuestStage.RewardClaimed), "퀘스트가 돌아와도 받은 것으로 남는다");
            Assert.That(later.ToSaveData().Quests.Count(q => q.QuestId == "quest.gone"), Is.EqualTo(1), "두 번 쓰지 않는다");
        }

        private static QuestChainDefinition ChainOf(string id, string stepId)
            => new QuestChainDefinition(id, id, id + ".desc", "",
                new[] { new QuestStepDefinition(stepId, stepId, stepId + ".desc", new QuestObjective[] { new AnyEventObjective(1) }) });

        [Test]
        public void QuestChainLog_KeepsRecordsOfChainsTheDataNoLongerHas()
        {
            var kept = ChainOf("chain.kept", "only");
            var gone = new QuestChainProgressSaveData
            {
                ChainId = "chain.gone", ChainState = "Completed", CurrentStepId = "last", RewardClaimedIds = new[] { "completion", "step:last" },
            };
            var log = new QuestChainLog(new[] { kept });
            log.LoadFromSaveData(new QuestChainLogSaveData { Chains = new[] { gone } });

            var written = log.ToSaveData();
            var record = written.Chains.Single(c => c.ChainId == "chain.gone");
            Assert.That(record.ChainState, Is.EqualTo("Completed"));
            Assert.That(record.RewardClaimedIds, Is.EqualTo(new[] { "completion", "step:last" }));

            var back = ChainOf("chain.gone", "last");
            var later = new QuestChainLog(new[] { kept, back });
            later.LoadFromSaveData(written);
            Assert.That(later.GetState(back), Is.EqualTo(QuestChainState.Completed));
            Assert.That(later.ToSaveData().Chains.Count(c => c.ChainId == "chain.gone"), Is.EqualTo(1));
        }
    }
}
