using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Quests;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using Rootborn.Game.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.Game.Player
{
    [Serializable]
    public sealed class PlayerInventorySaveData
    {
        public PlayerInventoryItemSaveData[] Items = Array.Empty<PlayerInventoryItemSaveData>();
        public string EquippedToolItemId;
    }

    [Serializable]
    public struct PlayerInventoryItemSaveData
    {
        public string ItemId;
        public int Count;
    }

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

    public sealed class GatherInteractor : MonoBehaviour
    {
        private const string DefaultSurface = "Soil";

        [SerializeField] private float _interactRadius = 1.5f;
        [SerializeField] private ToolDefinition _equippedTool;
        [SerializeField] private PlayerInventory _inventory;

        private InputAction _interactAction;
        private KnowledgeProgress _knowledgeProgress;
        private IQuestEventSink _questEvents;
        private bool _wasKeyboardInteractPressed;
        private static readonly System.Random s_dropRng = new System.Random();

        public KnowledgeProgress KnowledgeProgress => _knowledgeProgress;
        public ToolDefinition EquippedTool { get => _equippedTool; set => _equippedTool = value; }
        public PlayerInventory Inventory => _inventory;
        public void BindInventory(PlayerInventory inv) { _inventory = inv; }

        private void OnEnable()
        {
            if (GetComponent<PlayerInteractionRouter>() == null)
            {
                gameObject.AddComponent<PlayerInteractionRouter>();
            }

            _wasKeyboardInteractPressed = false;
            _interactAction = new InputAction(type: InputActionType.Button);
            _interactAction.AddBinding("<Keyboard>/e");
            _interactAction.AddBinding("<Keyboard>/space");
            _interactAction.AddBinding("<Keyboard>/leftCtrl");
            _interactAction.AddBinding("<Keyboard>/rightCtrl");
        }

        private void OnDisable()
        {
            if (_interactAction != null)
            {
                _interactAction.Dispose();
                _interactAction = null;
            }
        }

        private void Update()
        {
            bool pressed = IsKeyboardInteractPressed();
            if (pressed && !_wasKeyboardInteractPressed)
            {
                DoInteract();
            }
            _wasKeyboardInteractPressed = pressed;
        }

        public void Bind(KnowledgeProgress progress)
        {
            _knowledgeProgress = progress;
        }

        public void BindQuestEvents(IQuestEventSink sink)
        {
            _questEvents = sink;
        }

        public void TriggerInteract() => DoInteract();

        private void OnInteract(InputAction.CallbackContext ctx) => DoInteract();

        private static bool IsKeyboardInteractPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return keyboard.eKey.isPressed ||
                keyboard.spaceKey.isPressed ||
                keyboard.leftCtrlKey.isPressed ||
                keyboard.rightCtrlKey.isPressed;
        }

        private void DoInteract()
        {
            var interactionRouter = GetComponent<PlayerInteractionRouter>();
            if (interactionRouter != null && interactionRouter.TryInteractWithNearest())
            {
                return;
            }

            var node = FindNearestNode();
            if (node != null && !node.IsBroken)
            {
                bool wasBroken = node.IsBroken;
                node.Hit(_equippedTool);

                if (_knowledgeProgress != null && node.Definition != null)
                {
                    _knowledgeProgress.RecordAction(
                        KnowledgeAction.HitGround,
                        _equippedTool,
                        node.Definition.Id,
                        node.Definition.SurfaceTag);
                }

                if (node.Definition != null)
                {
                    _questEvents?.Record(new QuestEvent(
                        QuestEventKind.Gather,
                        $"{node.GetInstanceID()}:{UnityEngine.Time.frameCount}",
                        resource: node.Definition,
                        tool: _equippedTool));
                }

                if (!wasBroken && node.IsBroken && _inventory != null && node.Definition != null)
                {
                    var drops = node.Definition.Drops;
                    if (drops != null)
                    {
                        for (int i = 0; i < drops.Length; i++)
                        {
                            var d = drops[i];
                            int count = d.Roll(s_dropRng);
                            if (count <= 0 || string.IsNullOrEmpty(d.ResourceId)) continue;
                            _inventory.TryAddById(d.ResourceId, count);
                        }
                    }
                    node.gameObject.SetActive(false);
                }
                return;
            }

            if (_equippedTool == null) return;
            var effects = _equippedTool.Effects;
            if (effects == null || effects.Length == 0) return;

            var clock = Rootborn.Game.Time.GameClock.Instance;
            var grid = Rootborn.Game.Farming.FarmGrid.Instance;
            Vector2 facing = ResolveFacing();
            Vector3 targetWorld = transform.position + (Vector3)(facing.normalized * 0.75f);
            string surface = ResolveSurface(targetWorld);

            if (grid == null || grid.GroundTilemap == null)
            {
                var targetOnlyCtx = new ToolUseContext(
                    _equippedTool, gameObject, surface,
                    default, null, null, _inventory, clock, _questEvents);
                _equippedTool.ApplyEffects(in targetOnlyCtx);
                return;
            }

            Vector3Int cell = grid.WorldToCell(targetWorld);

            var ctx = new ToolUseContext(
                _equippedTool, gameObject, surface,
                cell, grid.GroundTilemap, grid, _inventory, clock, _questEvents);
            _equippedTool.ApplyEffects(in ctx);
        }

        private Vector2 ResolveFacing()
        {
            var pc = GetComponent<PlayerController>();
            Vector2 facing = pc != null ? pc.LastFacing : new Vector2(0f, -1f);
            return facing.sqrMagnitude < 0.0001f ? new Vector2(0f, -1f) : facing;
        }

        private static string ResolveSurface(Vector3 targetWorld)
        {
            var colliders = Physics2D.OverlapPointAll(targetWorld);
            for (int i = 0; i < colliders.Length; i++)
            {
                var zone = colliders[i] != null ? colliders[i].GetComponent<SurfaceTagZone>() : null;
                if (zone != null)
                {
                    return zone.Surface;
                }
            }

            return DefaultSurface;
        }

        private ResourceNode FindNearestNode()
        {
            var all = UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode nearest = null;
            float bestSqr = _interactRadius * _interactRadius;
            for (int i = 0; i < all.Length; i++)
            {
                var n = all[i];
                if (n == null || n.IsBroken) continue;
                float sqr = (n.transform.position - transform.position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    nearest = n;
                }
            }
            return nearest;
        }
    }
}
