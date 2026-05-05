using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Tools;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    /// <summary>
    /// Phase E — 인벤토리 UI 정상화 + 도구 장착 시각화 회귀 방지 테스트.
    /// </summary>
    public class InventoryUiTests
    {
        // 1. ItemDefinition 에 description 필드가 추가되었는지 reflection 검증.
        [Test]
        public void ItemDefinition_HasDescriptionField()
        {
            var t = typeof(ItemDefinition);
            var prop = t.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(prop, "ItemDefinition 에 public Description getter 가 있어야 함.");
            Assert.AreEqual(typeof(string), prop.PropertyType, "Description 은 string 타입이어야 함.");

            var fld = t.GetField("_description", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fld, "ItemDefinition 에 [SerializeField] private string _description; 가 있어야 함.");
        }

        // 2. ToolDefinition 에도 동일하게 description 필드 확인.
        [Test]
        public void ToolDefinition_HasDescriptionField()
        {
            var t = typeof(ToolDefinition);
            var prop = t.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(prop, "ToolDefinition 에 public Description getter 가 있어야 함.");
            Assert.AreEqual(typeof(string), prop.PropertyType);
        }

        // 3. PlayerController 가 PlayerInventory.OnEquipmentChanged 를 구독하는 메서드 존재 + Awake/Enable 시 자동 등록.
        //    Reflection 으로 OnEquipmentChanged 핸들러 메서드 존재 확인.
        [Test]
        public void PlayerController_HasEquipmentChangedHandler()
        {
            var t = typeof(PlayerController);
            var method = t.GetMethod("OnEquipmentChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "PlayerController 에 private void OnEquipmentChanged() 핸들러가 있어야 함.");

            // _activeToolId 필드 — 도구 장착 상태 추적 변수.
            var f = t.GetField("_activeToolId", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "PlayerController 에 _activeToolId 필드가 있어야 함.");
            Assert.AreEqual(typeof(string), f.FieldType);

            // _inventory 필드 — PlayerInventory 참조.
            var inv = t.GetField("_inventory", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(inv, "PlayerController 에 _inventory 필드 (PlayerInventory) 가 있어야 함.");
            Assert.AreEqual(typeof(PlayerInventory), inv.FieldType);
        }

        // 4. PlayerToolSpriteAddresses 상수 클래스 — 12 sheet 모두 등록되어 있는지.
        [Test]
        public void PlayerToolSpriteAddresses_AllSheets_HasTwelveEntries()
        {
            Assert.AreEqual(12, PlayerToolSpriteAddresses.AllSheets.Length,
                "Axe/Hoe/Pickaxe/Pickup × Down/Side/Up = 12 sheet 가 모두 등록되어야 함.");

            // 명명 규칙 검증 — 모두 "sprites/player/tool/<tool>-<dir>" 형식.
            foreach (var addr in PlayerToolSpriteAddresses.AllSheets)
            {
                Assert.IsTrue(addr.StartsWith("sprites/player/tool/"),
                    $"도구 sheet 주소는 'sprites/player/tool/' 으로 시작해야 함: {addr}");
                Assert.IsTrue(addr.EndsWith("-down") || addr.EndsWith("-side") || addr.EndsWith("-up"),
                    $"도구 sheet 주소는 -down/-side/-up 으로 끝나야 함: {addr}");
            }
        }

        // 5. InventoryView 에 SelectedItem 프로퍼티 존재 — 선택 상태 추적 회귀 방지.
        [Test]
        public void InventoryView_HasSelectedItemProperty()
        {
            // InventoryView 는 StatusHud.cs 의 internal 클래스. Rootborn.UI.HUD 네임스페이스.
            var t = System.Type.GetType("Rootborn.UI.HUD.InventoryView, Rootborn.UI");
            Assert.IsNotNull(t, "InventoryView 타입을 찾을 수 없음 — Rootborn.UI 어셈블리 확인.");

            var prop = t.GetProperty("SelectedItem", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(prop, "InventoryView 에 public SelectedItem getter 가 있어야 함.");
            Assert.AreEqual(typeof(ItemDefinition), prop.PropertyType);
        }
    }
}
