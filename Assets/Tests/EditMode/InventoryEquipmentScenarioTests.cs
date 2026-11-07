using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.UI.HUD;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode
{
    public sealed class InventoryEquipmentScenarioTests
    {
        [Test]
        public void EQP_002_EquipmentButton_Click_ChangesEquippedToolItem()
        {
            var axe = MakeItem("StoneAxe", ItemCategory.Tool, 1);
            var player = new GameObject("Player", typeof(PlayerInventory));
            try
            {
                var inventory = player.GetComponent<PlayerInventory>();

                inventory.EquipTool(axe);

                Assert.AreSame(axe, inventory.EquippedToolItem);
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void EQP_003_EquippingTool_UpdatesPlayerEquipmentVisualState()
        {
            var axe = MakeItem("StoneAxe", ItemCategory.Tool, 1);
            var player = new GameObject("Player", typeof(PlayerInventory), typeof(PlayerController));
            try
            {
                var inventory = player.GetComponent<PlayerInventory>();
                var controller = player.GetComponent<PlayerController>();
                SetField(controller, "_inventory", inventory);

                inventory.EquipTool(axe);
                InvokePrivate(controller, "OnEquipmentChanged");

                Assert.AreEqual("StoneAxe", GetPrivate<string>(controller, "_activeToolId"));
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void EQP_004_EquippingNull_UnequipsToolAndClearsVisualState()
        {
            var axe = MakeItem("StoneAxe", ItemCategory.Tool, 1);
            var player = new GameObject("Player", typeof(PlayerInventory), typeof(PlayerController));
            try
            {
                var inventory = player.GetComponent<PlayerInventory>();
                var controller = player.GetComponent<PlayerController>();
                SetField(controller, "_inventory", inventory);
                inventory.EquipTool(axe);
                InvokePrivate(controller, "OnEquipmentChanged");

                inventory.EquipTool(null);
                InvokePrivate(controller, "OnEquipmentChanged");

                Assert.IsNull(inventory.EquippedToolItem);
                Assert.IsNull(GetPrivate<string>(controller, "_activeToolId"));
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void STK_002_AddingStackableItem_IncrementsExistingSlotCount()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 99);
            var inventory = new Inventory();

            inventory.Add(wood, 3);
            inventory.Add(wood, 4);

            Assert.AreEqual(1, inventory.Slots.Count);
            Assert.AreEqual(7, inventory.Slots[0].Count);
        }

        [Test]
        public void STK_003_AddingBeyondMaxStack_SplitsIntoNewSlot()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 5);
            var inventory = new Inventory();

            inventory.Add(wood, 7);

            Assert.AreEqual(2, inventory.Slots.Count);
            Assert.AreEqual(5, inventory.Slots[0].Count);
            Assert.AreEqual(2, inventory.Slots[1].Count);
        }

        [Test]
        public void STK_004_EquipmentItems_DoNotMergeLikeMaterials()
        {
            var axe = MakeItem("StoneAxe", ItemCategory.Tool, 1);
            var inventory = new Inventory();

            inventory.Add(axe, 2);

            Assert.AreEqual(2, inventory.Slots.Count);
            Assert.AreEqual(1, inventory.Slots[0].Count);
            Assert.AreEqual(1, inventory.Slots[1].Count);
        }

        [Test]
        public void DRG_001_MoveSlot_ToEmptyLogicalSlot_MovesItemWithoutDuplication()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 99);
            var inventory = new Inventory();
            inventory.Add(wood, 4);

            bool moved = inventory.TryMoveSlot(0, 3);

            Assert.IsTrue(moved);
            Assert.IsNull(inventory.Slots[0].Item);
            Assert.AreEqual(0, inventory.Slots[0].Count);
            Assert.AreSame(wood, inventory.Slots[3].Item);
            Assert.AreEqual(4, inventory.Slots[3].Count);
            Assert.AreEqual(4, inventory.CountOf(wood));
        }

        [Test]
        public void DRG_002_MoveStackableSlot_ToSameItemSlot_MergesAvailableCountOnly()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 5);
            var inventory = new Inventory();
            inventory.Add(wood, 5);
            inventory.Add(wood, 3);

            bool moved = inventory.TryMoveSlot(1, 0);

            Assert.IsFalse(moved, "A full target stack cannot receive more items.");
            Assert.AreEqual(5, inventory.Slots[0].Count);
            Assert.AreEqual(3, inventory.Slots[1].Count);
        }

        [Test]
        public void DRG_003_MoveSlot_ToIncompatibleOccupiedSlot_ReturnsFalseAndRestoresSource()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 99);
            var axe = MakeItem("StoneAxe", ItemCategory.Tool, 1);
            var inventory = new Inventory();
            inventory.Add(wood, 2);
            inventory.Add(axe, 1);

            bool moved = inventory.TryMoveSlot(0, 1);

            Assert.IsFalse(moved);
            Assert.AreSame(wood, inventory.Slots[0].Item);
            Assert.AreEqual(2, inventory.Slots[0].Count);
            Assert.AreSame(axe, inventory.Slots[1].Item);
            Assert.AreEqual(1, inventory.Slots[1].Count);
        }

        [Test]
        public void DRG_004_MoveStackableSlot_OverflowRemainsInSourceSlot()
        {
            var stone = MakeItem("Stone", ItemCategory.Resource, 5);
            var inventory = new Inventory();
            // Slots are only changed through the inventory: a full stack (5) at 0, an axe at 1, a partial stack (4) at 2.
            inventory.Add(stone, 5);
            inventory.Add(MakeItem("StoneAxe", ItemCategory.Tool, 1), 1);
            inventory.Add(stone, 4);

            bool moved = inventory.TryMoveSlot(0, 2);

            Assert.IsTrue(moved);
            Assert.AreEqual(5, inventory.Slots[2].Count);
            Assert.AreSame(stone, inventory.Slots[0].Item);
            Assert.AreEqual(4, inventory.Slots[0].Count);
            Assert.AreEqual(9, inventory.CountOf(stone));
        }

        private static int FilledSlotCount(Inventory inventory)
        {
            int count = 0;
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                if (inventory.Slots[i].Item != null && inventory.Slots[i].Count > 0) count++;
            }
            return count;
        }

        private static ItemDefinition MakeItem(string id, ItemCategory category, int maxStack)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_id", id);
            SetField(item, "_displayKey", id);
            SetField(item, "_category", category);
            SetField(item, "_maxStack", maxStack);
            return item;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {name} on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {name} on {target.GetType().Name}");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method {name} on {target.GetType().Name}");
            method.Invoke(target, null);
        }
    }
}
