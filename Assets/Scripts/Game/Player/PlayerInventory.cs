using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Resources;
using UnityEngine;

namespace Rootborn.Game.Player
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        private readonly Inventory _localInventory = new Inventory();
        private Inventory _inventory;

        public Inventory Inventory
        {
            get
            {
                EnsureInventoryStorage();
                return _inventory;
            }
        }

        public ItemDefinition EquippedToolItem { get; private set; }
        [SerializeField] private Rootborn.Game.Tools.ToolDefinition _plantingHandTool;
        private Rootborn.Game.Tools.ToolDefinition _lastEquippedToolDef;

        public ItemDefinition EquippedSeed { get; private set; }
        private Dictionary<string, ItemDefinition> _byId;
        private bool _inventoryEventsBound;

        public event System.Action OnEquipmentChanged;

        public void Bind(GameDataRegistry registry)
        {
            BindInventoryStorage();
            _byId = new Dictionary<string, ItemDefinition>(16);
            if (registry != null && registry.Items != null)
            {
                for (int i = 0; i < registry.Items.Length; i++)
                {
                    var it = registry.Items[i];
                    if (it == null || string.IsNullOrEmpty(it.Id)) continue;
                    _byId[it.Id] = it;
                }
            }
            RefreshEquippedSeed();
        }

        public void RefreshEquippedSeed()
        {
            ItemDefinition newSeed = null;
            var slots = Inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.Item != null && s.Item.Category == ItemCategory.Seed && s.Count > 0)
                {
                    newSeed = s.Item;
                    break;
                }
            }
            bool changed = newSeed != EquippedSeed;
            EquippedSeed = newSeed;
            if (!changed) return;

            var interactor = GetComponent<GatherInteractor>();
            if (interactor == null) return;
            if (newSeed != null && _plantingHandTool != null)
            {
                if (interactor.EquippedTool != _plantingHandTool)
                {
                    _lastEquippedToolDef = interactor.EquippedTool;
                }
                interactor.EquippedTool = _plantingHandTool;
            }
            else if (newSeed == null && interactor.EquippedTool == _plantingHandTool)
            {
                interactor.EquippedTool = _lastEquippedToolDef;
            }
            OnEquipmentChanged?.Invoke();
        }

        public ItemDefinition FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureBoundToRuntimeRegistry(id);
            if (_byId == null) return null;
            _byId.TryGetValue(id, out var def);
            return def;
        }

        public bool TryAddById(string id, int count)
        {
            var def = FindById(id);
            if (def == null) return false;
            Inventory.Add(def, count);
            return true;
        }

        public void EquipTool(ItemDefinition toolItem)
        {
            EquippedToolItem = toolItem;
            var data = Rootborn.Game.Managers.Managers.Data;
            if (data != null && toolItem != null && data.ToolById.TryGetValue(toolItem.Id, out var toolDef))
            {
                var interactor = GetComponent<GatherInteractor>();
                if (interactor != null)
                {
                    if (EquippedSeed == null || _plantingHandTool == null)
                    {
                        interactor.EquippedTool = toolDef;
                    }
                    _lastEquippedToolDef = toolDef;
                }
            }
            OnEquipmentChanged?.Invoke();
        }

        public PlayerInventorySaveData ToSaveData()
        {
            var slots = Inventory.Slots;
            var items = new List<PlayerInventoryItemSaveData>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.Item == null || slot.Count <= 0 || string.IsNullOrEmpty(slot.Item.Id))
                {
                    continue;
                }

                items.Add(new PlayerInventoryItemSaveData { ItemId = slot.Item.Id, Count = slot.Count });
            }

            return new PlayerInventorySaveData
            {
                Items = items.ToArray(),
                EquippedToolItemId = EquippedToolItem != null ? EquippedToolItem.Id : string.Empty,
            };
        }

        public void LoadFromSaveData(PlayerInventorySaveData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            Inventory.Clear();
            if (saveData.Items != null)
            {
                for (int i = 0; i < saveData.Items.Length; i++)
                {
                    var item = saveData.Items[i];
                    TryAddById(item.ItemId, item.Count);
                }
            }

            EquipTool(FindById(saveData.EquippedToolItemId));
            RefreshEquippedSeed();
        }

        private void OnDestroy()
        {
            UnbindInventoryEvents();
        }

        private void EnsureBoundToRuntimeRegistry(string requiredId)
        {
            if (_byId != null && (string.IsNullOrEmpty(requiredId) || _byId.ContainsKey(requiredId))) return;
            var registry = Rootborn.Game.Managers.Managers.Data?.Registry;
            if (registry != null)
            {
                Bind(registry);
            }
        }

        private void EnsureInventoryStorage()
        {
            if (_inventory != null)
            {
                return;
            }

            _inventory = PlayerGlobalState.TryGetInventoryForActiveSaveSlot(ResolvePlayerId()) ?? _localInventory;
        }

        private void BindInventoryStorage()
        {
            var nextInventory = PlayerGlobalState.TryGetInventoryForActiveSaveSlot(ResolvePlayerId()) ?? _localInventory;
            if (_inventory == nextInventory && _inventoryEventsBound)
            {
                return;
            }

            if (_inventory != nextInventory)
            {
                UnbindInventoryEvents();
                _inventory = nextInventory;
            }

            if (!_inventoryEventsBound)
            {
                _inventory.OnChanged += RefreshEquippedSeed;
                _inventoryEventsBound = true;
            }
        }

        private void UnbindInventoryEvents()
        {
            if (!_inventoryEventsBound || _inventory == null)
            {
                return;
            }

            _inventory.OnChanged -= RefreshEquippedSeed;
            _inventoryEventsBound = false;
        }

        private string ResolvePlayerId()
        {
            var identity = GetComponent<PlayerIdentity>();
            return identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }
    }
}
