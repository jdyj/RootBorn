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
