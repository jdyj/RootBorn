using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.UI.Modern
{
    public sealed class UrbanInventoryBrowseBindingTests
    {
        [Test]
        public void SameOwnerAndStorageRebindPreservesQueryPopupAndSelection()
        {
            var root = new GameObject("InventoryRebindFixture", typeof(RectTransform));
            var ownerRoot = new GameObject("InventoryRebindOwner", typeof(PlayerInventory));
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            InventoryBrowseEventSystemScope eventScope = null;
            try
            {
                eventScope = new InventoryBrowseEventSystemScope();
                var events = eventScope.Events;
                SetField(item, "_id", "fixture.binding.keep");
                SetField(item, "_displayKey", "Keep binding item");
                SetField(item, "_maxStack", 9999);
                var owner = ownerRoot.GetComponent<PlayerInventory>();
                // Never resolve or clear the active save's shared PlayerGlobalState inventory.
                SetField(owner, "_inventory", new Inventory());
                owner.Inventory.Add(item, 37);
                var storage = owner.Inventory;
                string before = JsonUtility.ToJson(owner.ToSaveData());
                var panel = root.AddComponent<ModernUiInventoryPanel>();
                panel.Bind(owner);
                panel.Show();
                var search = root.transform.Find("SearchField").GetComponent<InputField>();
                Assert.That(search, Is.Not.Null);
                search.text = "keep";
                var scroll = root.GetComponentsInChildren<ScrollRect>(true).Single();
                scroll.content.GetChild(0).GetComponent<Button>().onClick.Invoke();
                var popup = root.transform.Find("ItemDetailPopup");
                Assert.That(popup, Is.Not.Null);
                events.SetSelectedGameObject(search.gameObject);
                Assert.That(UiTextInputFocus.HasFocus, Is.True, eventScope.Describe(search));

                panel.Bind(owner);

                Assert.That(owner.Inventory, Is.SameAs(storage));
                Assert.That(object.ReferenceEquals(root.transform.Find("ItemDetailPopup"), popup), Is.True,
                    "An installer rebinding the same owner/storage must preserve the open item popup.");
                Assert.That(popup.gameObject.activeInHierarchy, Is.True);
                Assert.That(panel.IsVisible, Is.True);
                Assert.That(search.text, Is.EqualTo("keep"));
                Assert.That(events.currentSelectedGameObject, Is.SameAs(search.gameObject));
                Assert.That(UiTextInputFocus.HasFocus, Is.True, eventScope.Describe(search));
                Assert.That(JsonUtility.ToJson(owner.ToSaveData()), Is.EqualTo(before));
            }
            finally
            {
                if (eventScope != null) eventScope.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(ownerRoot);
                Object.DestroyImmediate(item);
            }
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Fixture isolation seam changed: " + name);
            field.SetValue(target, value);
        }
    }

    // UGUI EventSystem lacks ExecuteAlways. EditMode AddComponent may not deliver its
    // OnEnable registration; its current setter can only promote an already registered instance.
    // Own a fresh event system and restore the precise prior registration order and selections.
    internal sealed class InventoryBrowseEventSystemScope : IDisposable
    {
        private readonly List<EventSystem> _registered;
        private readonly EventSystem[] _previous;
        private readonly GameObject[] _previousSelections;
        private readonly GameObject _root;
        private readonly bool _manualEnable;
        public EventSystem Events { get; }

        public InventoryBrowseEventSystemScope()
        {
            var field = typeof(EventSystem).GetField("m_EventSystems", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "UGUI EventSystem registration fixture seam changed.");
            _registered = (List<EventSystem>)field.GetValue(null);
            _previous = _registered.ToArray();
            _previousSelections = _previous.Select(x => x != null ? x.currentSelectedGameObject : null).ToArray();
            _root = new GameObject("InventoryBrowseFixtureEvents", typeof(EventSystem));
            Events = _root.GetComponent<EventSystem>();
            _manualEnable = !_registered.Contains(Events);
            try
            {
                if (_manualEnable) Lifecycle("OnEnable");
                EventSystem.current = Events;
                Assert.That(EventSystem.current, Is.SameAs(Events), "Fixture must register and select its own EventSystem.");
            }
            catch { Dispose(); throw; }
        }

        public string Describe(InputField input)
        {
            return "currentIsFixture=" + (EventSystem.current == Events)
                + ", localSelection=" + Events.currentSelectedGameObject
                + ", globalSelection=" + (EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null)
                + ", expected=" + input.gameObject + ", inputActiveEnabled=" + input.isActiveAndEnabled
                + ", inputInteractable=" + input.IsInteractable();
        }

        public void Dispose()
        {
            try
            {
                Events.SetSelectedGameObject(null);
                if (_manualEnable && _registered.Contains(Events)) Lifecycle("OnDisable");
            }
            finally
            {
                Object.DestroyImmediate(_root);
                _registered.Clear();
                _registered.AddRange(_previous);
                for (int i = 0; i < _previous.Length; i++)
                    if (_previous[i] != null && _previous[i].currentSelectedGameObject != _previousSelections[i])
                        _previous[i].SetSelectedGameObject(_previousSelections[i]);
            }
        }

        private void Lifecycle(string name)
        {
            var method = typeof(EventSystem).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "UGUI EventSystem lifecycle fixture seam changed: " + name);
            method.Invoke(Events, null);
        }
    }
}
