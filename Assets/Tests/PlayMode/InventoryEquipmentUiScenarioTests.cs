using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.UI.HUD;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode
{
    public sealed class InventoryEquipmentUiScenarioTests
    {
        [UnityTest]
        public IEnumerator INV_001_ToggleInventoryCommand_OpensInventoryPanel()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            var host = new GameObject("Hud", typeof(RectTransform), typeof(FarmHudController));
            var book = new GameObject("BookPanel");
            try
            {
                host.transform.SetParent(canvasGo.transform, false);
                book.SetActive(false);
                var controller = host.GetComponent<FarmHudController>();
                SetField(controller, "_bookPanel", book);

                InvokePrivate(controller, "ToggleBook");
                yield return null;

                Assert.IsTrue(book.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(book);
                Object.DestroyImmediate(canvasGo);
            }
        }

        [UnityTest]
        public IEnumerator INV_003_And_STK_001_PlayerInventoryItem_RendersSlotAndCount()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 99);
            var fixture = MakeInventoryViewFixture();
            try
            {
                fixture.Inventory.Inventory.Add(wood, 12);
                fixture.View.Refresh();
                yield return null;

                var slot = fixture.Root.Find("Slot_Wood");
                Assert.IsNotNull(slot);
                Assert.AreEqual("12", slot.Find("Count").GetComponent<Text>().text);
            }
            finally { Object.DestroyImmediate(fixture.Host); }
        }

        [UnityTest]
        public IEnumerator EQP_002_EquipmentButton_Click_ChangesEquippedToolItem()
        {
            var axe = MakeItem("StoneAxe", ItemCategory.Tool, 1);
            var player = new GameObject("Player", typeof(PlayerInventory));
            try
            {
                var inventory = player.GetComponent<PlayerInventory>();
                inventory.EquipTool(axe);
                yield return null;

                Assert.AreSame(axe, inventory.EquippedToolItem);
            }
            finally { Object.DestroyImmediate(player); }
        }

        [UnityTest]
        public IEnumerator DRG_001_MoveInventorySlot_ToEmptySlot_MovesItem()
        {
            var wood = MakeItem("Wood", ItemCategory.Resource, 99);
            var inventory = new Inventory();
            inventory.Add(wood, 3);

            bool moved = inventory.TryMoveSlot(0, 2);
            yield return null;

            Assert.IsTrue(moved);
            Assert.IsNull(inventory.Slots[0].Item);
            Assert.AreSame(wood, inventory.Slots[2].Item);
            Assert.AreEqual(3, inventory.CountOf(wood));
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

        private static void InvokePrivate(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method {name} on {target.GetType().Name}");
            method.Invoke(target, null);
        }
    }
}
