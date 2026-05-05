using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.Game.Player
{
    /// <summary>
    /// 플레이어 인벤토리 컨테이너. Player GameObject 에 부착.
    /// GameDataRegistry 의 Items[] 와 ID 매핑.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        public Inventory Inventory { get; } = new Inventory();
        public ItemDefinition EquippedToolItem { get; private set; }
        // 자동 선택된 씨앗 (Category==Seed 첫 슬롯). EquippedSeed 가 있으면 GatherInteractor.EquippedTool 은
        // 가상 PlantingHand 도구로 임시 스왑되어 좌클릭 시 PlantSeedEffect 가 실행됨.
        public ItemDefinition EquippedSeed { get; private set; }
        // PlantingHand 가상 도구 — Inspector 에서 와이어링.
        [SerializeField] private Rootborn.Game.Tools.ToolDefinition _plantingHandTool;
        // 씨앗 미장착 시 복귀할 마지막 ToolDefinition (인벤토리 갱신 후 자동 복귀).
        private Rootborn.Game.Tools.ToolDefinition _lastEquippedToolDef;

        private Dictionary<string, ItemDefinition> _byId;

        public event System.Action OnEquipmentChanged;

        public void Bind(GameDataRegistry registry)
        {
            _byId = new Dictionary<string, ItemDefinition>(16);
            if (registry == null || registry.Items == null) return;
            for (int i = 0; i < registry.Items.Length; i++)
            {
                var it = registry.Items[i];
                if (it == null || string.IsNullOrEmpty(it.Id)) continue;
                _byId[it.Id] = it;
            }
            // 인벤토리 변경 시 EquippedSeed 자동 선택/해제.
            Inventory.OnChanged += RefreshEquippedSeed;
            RefreshEquippedSeed();
        }

        // 인벤토리에서 첫 Seed-카테고리 슬롯을 자동 선택. 씨앗 장착 시 GatherInteractor 의 도구를 PlantingHand 로 임시 스왑.
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

            // GatherInteractor 의 EquippedTool 스왑 (씨앗 우선, 해제 시 마지막 도구 복귀).
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
            if (_byId == null || string.IsNullOrEmpty(id)) return null;
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
            // GatherInteractor 의 ToolDefinition 도 같이 동기화 (Managers.Data 의 ToolById 매핑).
            var data = Rootborn.Game.Managers.Managers.Data;
            if (data != null && toolItem != null && data.ToolById.TryGetValue(toolItem.Id, out var toolDef))
            {
                var interactor = GetComponent<GatherInteractor>();
                if (interactor != null)
                {
                    // 씨앗 장착 중이 아니면 즉시 적용. 씨앗 장착 중이면 "마지막 도구" 로만 저장하고
                    // 씨앗 해제 시 자동 복귀. 사용자가 명시적으로 도구 교체 시 일관된 UX.
                    if (EquippedSeed == null || _plantingHandTool == null)
                    {
                        interactor.EquippedTool = toolDef;
                    }
                    _lastEquippedToolDef = toolDef;
                }
            }
            OnEquipmentChanged?.Invoke();
        }
    }

    /// <summary>
    /// 플레이어 근처 가장 가까운 ResourceNode를 E 키로 타격.
    /// KnowledgeProgress에 액션을 기록하여 도구 해금 트리거를 평가.
    /// </summary>
    public sealed class GatherInteractor : MonoBehaviour
    {
        [SerializeField] private float _interactRadius = 1.5f;
        [SerializeField] private ToolDefinition _equippedTool;
        [SerializeField] private PlayerInventory _inventory;

        private InputAction _interactAction;
        private KnowledgeProgress _knowledgeProgress;
        private static readonly System.Random s_dropRng = new System.Random();

        public KnowledgeProgress KnowledgeProgress => _knowledgeProgress;
        public ToolDefinition EquippedTool { get => _equippedTool; set => _equippedTool = value; }
        public PlayerInventory Inventory => _inventory;
        public void BindInventory(PlayerInventory inv) { _inventory = inv; }

        private void OnEnable()
        {
            _interactAction = new InputAction(type: InputActionType.Button);
            _interactAction.AddBinding("<Keyboard>/e");
            _interactAction.AddBinding("<Keyboard>/space");
            _interactAction.performed += OnInteract;
            _interactAction.Enable();
        }

        private void OnDisable()
        {
            if (_interactAction != null)
            {
                _interactAction.performed -= OnInteract;
                _interactAction.Disable();
                _interactAction.Dispose();
                _interactAction = null;
            }
        }

        public void Bind(KnowledgeProgress progress)
        {
            _knowledgeProgress = progress;
        }

        // PlayerController 의 마우스 좌클릭 휘두르기 마지막 프레임에서 호출 (E 키와 동일 흐름).
        public void TriggerInteract() => DoInteract();

        private void OnInteract(InputAction.CallbackContext ctx) => DoInteract();

        private void DoInteract()
        {
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

                // 자원 노드가 이번 타격으로 부서졌으면 drop 을 인벤토리에 적립.
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
                    // 부서진 노드는 시각적으로 사라지게 (collider/렌더러 비활성).
                    node.gameObject.SetActive(false);
                }
                return;
            }

            // ResourceNode 미발견 → 농사 타일 dispatch 시도.
            if (_equippedTool == null) return;
            var effects = _equippedTool.Effects;
            if (effects == null || effects.Length == 0) return;

            var grid = Rootborn.Game.Farming.FarmGrid.Instance;
            if (grid == null || grid.GroundTilemap == null) return;

            // Player facing 으로 인접 cell 결정. PlayerController 의 LastFacing 사용.
            var pc = GetComponent<PlayerController>();
            Vector2 facing = pc != null ? pc.LastFacing : new Vector2(0f, -1f);
            if (facing.sqrMagnitude < 0.0001f) facing = new Vector2(0f, -1f);
            Vector3 targetWorld = transform.position + (Vector3)(facing.normalized * 0.75f);
            Vector3Int cell = grid.WorldToCell(targetWorld);

            var clock = Rootborn.Game.Time.GameClock.Instance;
            var ctx = new ToolUseContext(
                _equippedTool, gameObject, "Soil",
                cell, grid.GroundTilemap, grid, _inventory, clock);
            _equippedTool.ApplyEffects(in ctx);
        }

        private ResourceNode FindNearestNode()
        {
            var all = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
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
