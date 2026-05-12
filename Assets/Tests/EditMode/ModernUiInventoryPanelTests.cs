using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiInventoryPanelTests
    {
        [Test]
        public void Show_BuildsTiledInventoryWindowSlotGridScrollbarAndControls()
        {
            var fixture = CreatePanelFixture();
            var wood = MakeItem("Wood", ItemCategory.Resource, 99, "A useful material.");
            try
            {
                fixture.Inventory.Inventory.Add(wood, 12);
                Invoke(fixture.Panel, "Bind", fixture.Inventory);
                Invoke(fixture.Panel, "Show");

                Assert.IsTrue((bool)GetProperty(fixture.Panel, "IsVisible"));
                AssertChildHasTiles(fixture.Host.transform, "InventoryTitleTab");
                AssertChildHasTiles(fixture.Host.transform, "InventorySlotGrid");
                AssertChildHasTiles(fixture.Host.transform, "InventoryScrollbar");
                AssertChildHasTiles(fixture.Host.transform, "InventoryBottomControls");
                Assert.GreaterOrEqual((int)GetProperty(fixture.Panel, "TileCount"), 80);
                Assert.IsNotNull(fixture.Host.transform.Find("InventorySlotGrid/Slot_Wood"));
            }
            finally
            {
                Object.DestroyImmediate(fixture.Host);
                Object.DestroyImmediate(fixture.Player);
                Object.DestroyImmediate(wood);
            }
        }

        [Test]
        public void SlotClick_OpensItemDetailPopupAndEmptySlotClosesIt()
        {
            var fixture = CreatePanelFixture();
            var wood = MakeItem("Wood", ItemCategory.Resource, 99, "A useful material.");
            try
            {
                fixture.Inventory.Inventory.Add(wood, 12);
                Invoke(fixture.Panel, "Bind", fixture.Inventory);
                Invoke(fixture.Panel, "Show");

                fixture.Host.transform.Find("InventorySlotGrid/Slot_Wood").GetComponent<Button>().onClick.Invoke();

                var popup = fixture.Host.transform.Find("ItemDetailPopup");
                Assert.IsNotNull(popup, "Item slot click should open popup.");
                Assert.Greater(popup.GetComponent<ModernUiTileImage>().TileCount, 0);
                Assert.AreEqual("Wood", popup.Find("ItemName").GetComponent<Text>().text);
                StringAssert.Contains("A useful material.", popup.Find("ItemDescription").GetComponent<Text>().text);
                StringAssert.Contains("12", popup.Find("ItemCount").GetComponent<Text>().text);
                Assert.IsNotNull(popup.Find("ActionButton").GetComponent<ModernUiTileImage>());

                fixture.Host.transform.Find("InventorySlotGrid/Slot_Empty_1").GetComponent<Button>().onClick.Invoke();
                Assert.IsNull(fixture.Host.transform.Find("ItemDetailPopup"));
            }
            finally
            {
                Object.DestroyImmediate(fixture.Host);
                Object.DestroyImmediate(fixture.Player);
                Object.DestroyImmediate(wood);
            }
        }

        private static (GameObject Host, GameObject Player, Component Panel, PlayerInventory Inventory) CreatePanelFixture()
        {
            var type = System.Type.GetType("Rootborn.UI.Modern.ModernUiInventoryPanel, Rootborn.UI");
            Assert.IsNotNull(type, "Missing Rootborn.UI.Modern.ModernUiInventoryPanel.");

            var host = new GameObject("ModernUiInventoryPanelFixture", typeof(RectTransform));
            var player = new GameObject("Player", typeof(PlayerInventory));
            return (host, player, host.AddComponent(type), player.GetComponent<PlayerInventory>());
        }

        private static void AssertChildHasTiles(Transform root, string name)
        {
            var child = root.Find(name);
            Assert.IsNotNull(child, "Missing " + name + ".");
            var tiles = child.GetComponent<ModernUiTileImage>();
            Assert.IsNotNull(tiles, name + " must use ModernUiTileImage.");
            Assert.Greater(tiles.TileCount, 0, name + " must build 16x16 tiles.");
        }

        private static void Invoke(Component target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(method, "Missing method " + methodName + ".");
            method.Invoke(target, args);
        }

        private static object GetProperty(Component target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, "Missing property " + propertyName + ".");
            return property.GetValue(target);
        }

        private static ItemDefinition MakeItem(string id, ItemCategory category, int maxStack, string description)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_id", id);
            SetField(item, "_displayKey", id);
            SetField(item, "_category", category);
            SetField(item, "_maxStack", maxStack);
            SetField(item, "_description", description);
            return item;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing field " + name + " on " + target.GetType().Name);
            field.SetValue(target, value);
        }
    }
}
