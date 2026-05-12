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
        public void INV_001_ToggleInventoryCommand_OpensInventoryPanel()
        {
            var host = new GameObject("Hud", typeof(RectTransform), typeof(FarmHudController));
            var book = new GameObject("BookPanel");
            try
            {
                book.SetActive(false);
                var controller = host.GetComponent<FarmHudController>();
                SetField(controller, "_bookPanel", book);

                InvokePrivate(controller, "ToggleBook");

                Assert.IsTrue(book.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(book);
            }
        }

        [Test]
        public void INV_002_ClosingInventory_RestoresHudAndMovementCapabilitySurface()
        {
            var host = new GameObject("Hud", typeof(RectTransform), typeof(FarmHudController));
            var hud = new GameObject("HudPanel");
            var book = new GameObject("BookPanel");
            try
            {
                hud.SetActive(true);
                book.SetActive(true);
                var controller = host.GetComponent<FarmHudController>();
                SetField(controller, "_hudPanel", hud);
                SetField(controller, "_bookPanel", book);

                InvokePrivate(controller, "ToggleBook");

                Assert.IsFalse(book.activeSelf);
                Assert.IsTrue(hud.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(hud);
                Object.DestroyImmediate(book);
            }
        }

        [Test]
        public void INV_003_PlayerInventoryItem_RendersInventorySlotUi()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 99);
            var fixture = MakeInventoryViewFixture();
            try
            {
                fixture.Inventory.Inventory.Add(wood, 1);
                fixture.View.Refresh();

                Assert.IsNotNull(fixture.Root.Find("Slot_Wood"));
            }
            finally { Object.DestroyImmediate(fixture.Host); }
        }

        [Test]
        public void INV_004_EmptyInventory_RendersStableEmptySlotsWithoutException()
        {
            var fixture = MakeInventoryViewFixture();
            try
            {
                fixture.View.Refresh();

                Assert.GreaterOrEqual(fixture.Root.childCount, 16);
                Assert.IsNotNull(fixture.Root.Find("Slot_Empty_0"));
            }
            finally { Object.DestroyImmediate(fixture.Host); }
        }

        [Test]
        public void STK_001_ResourceItemSlot_DisplaysOwnedCount()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 99);
            var fixture = MakeInventoryViewFixture();
            try
            {
                fixture.Inventory.Inventory.Add(wood, 12);
                fixture.View.Refresh();

                var slot = fixture.Root.Find("Slot_Wood");
                Assert.IsNotNull(slot);
                var count = slot.Find("Count")?.GetComponent<Text>();
                Assert.IsNotNull(count);
                Assert.AreEqual("12", count.text);
            }
            finally { Object.DestroyImmediate(fixture.Host); }
        }

        [Test]
        public void EQP_001_ToolSlotSelection_UpdatesSelectedItemAndRaisesSelection()
        {
            var axe = MakeItem("StoneAxe", ItemCategory.Tool, 1);
            var fixture = MakeInventoryViewFixture();
            try
            {
                Inventory.Slot selected = null;
                fixture.View.OnSlotSelected += slot => selected = slot;
                fixture.Inventory.Inventory.Add(axe, 1);
                fixture.View.Refresh();

                var slotGo = fixture.Root.Find("Slot_StoneAxe");
                Assert.IsNotNull(slotGo);
                slotGo.GetComponent<Button>().onClick.Invoke();

                Assert.AreSame(axe, fixture.View.SelectedItem);
                Assert.IsNotNull(selected);
                Assert.AreSame(axe, selected.Item);
            }
            finally { Object.DestroyImmediate(fixture.Host); }
        }

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
        public void EQP_005_EquipmentChange_RefreshesHudCurrentToolLabel()
        {
            var axe = MakeItem("StoneAxe", ItemCategory.Tool, 1);
            var host = new GameObject("Hud", typeof(RectTransform), typeof(FarmHudController));
            var labelGo = new GameObject("EquippedLabel", typeof(RectTransform), typeof(Text));
            try
            {
                labelGo.transform.SetParent(host.transform, false);
                var inventory = host.AddComponent<PlayerInventory>();
                var controller = host.GetComponent<FarmHudController>();
                SetField(controller, "_playerInv", inventory);
                SetField(controller, "_equippedSlotLabel", labelGo.GetComponent<Text>());

                inventory.EquipTool(axe);
                InvokePrivate(controller, "UpdateEquippedSlot");

                Assert.AreEqual("StoneAxe", labelGo.GetComponent<Text>().text);
            }
            finally { Object.DestroyImmediate(host); }
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
        public void DRG_001_InventorySlotDragHandler_DropOnEmptySlotMovesItem()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 99);
            var fixture = MakeInventoryViewFixture();
            try
            {
                fixture.Inventory.Inventory.Add(wood, 3);
                fixture.View.Refresh();

                var source = fixture.Root.Find("Slot_Wood");
                var target = fixture.Root.Find("Slot_Empty_1");
                Assert.IsNotNull(source);
                Assert.IsNotNull(target);
                var beginDrag = source.GetComponent<IBeginDragHandler>();
                var drop = target.GetComponent<IDropHandler>();
                Assert.IsNotNull(beginDrag, "Filled inventory slots must expose a drag source handler." );
                Assert.IsNotNull(drop, "Empty inventory slots must expose a drop target handler." );

                var eventData = new PointerEventData(EventSystem.current);
                beginDrag.OnBeginDrag(eventData);
                drop.OnDrop(eventData);

                Assert.IsNull(fixture.Inventory.Inventory.Slots[0].Item);
                Assert.AreSame(wood, fixture.Inventory.Inventory.Slots[1].Item);
                Assert.AreEqual(3, fixture.Inventory.Inventory.CountOf(wood));
            }
            finally { Object.DestroyImmediate(fixture.Host); }
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
            inventory.Add(stone, 4);
            inventory.Add(MakeItem("StoneAxe", ItemCategory.Tool, 1), 1);
            inventory.Add(stone, 4);
            inventory.Slots[0].Count = 4;
            inventory.Slots[2].Count = 4;

            bool moved = inventory.TryMoveSlot(2, 0);

            Assert.IsTrue(moved);
            Assert.AreEqual(5, inventory.Slots[0].Count);
            Assert.AreSame(stone, inventory.Slots[2].Item);
            Assert.AreEqual(3, inventory.Slots[2].Count);
            Assert.AreEqual(8, inventory.CountOf(stone));
        }

        [Test]
        public void DRG_005_RefreshingInventoryDuringSelection_DoesNotLoseOrDuplicateItems()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 99);
            var fixture = MakeInventoryViewFixture();
            try
            {
                fixture.Inventory.Inventory.Add(wood, 6);
                fixture.View.Refresh();
                fixture.Root.Find("Slot_Wood").GetComponent<Button>().onClick.Invoke();

                fixture.View.Refresh();

                Assert.AreEqual(6, fixture.Inventory.Inventory.CountOf(wood));
                Assert.AreEqual(1, FilledSlotCount(fixture.Inventory.Inventory));
            }
            finally { Object.DestroyImmediate(fixture.Host); }
        }

        private static (GameObject Host, RectTransform Root, InventoryView View, PlayerInventory Inventory) MakeInventoryViewFixture()
        {
            var host = new GameObject("InventoryViewFixture", typeof(RectTransform));
            var rootGo = new GameObject("Slots", typeof(RectTransform));
            rootGo.transform.SetParent(host.transform, false);
            var inventory = host.AddComponent<PlayerInventory>();
            var view = host.AddComponent<InventoryView>();
            view.BindElements((RectTransform)rootGo.transform, null, InventoryView.Filter.All);
            view.Bind(inventory);
            return (host, (RectTransform)rootGo.transform, view, inventory);
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
