using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Status;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rootborn.UI.HUD
{
    public sealed class StatusHud : MonoBehaviour
    {
        [SerializeField] private PlayerStatus _playerStatus;
        [SerializeField] private StatusEffectDefinition _trackedStatus;
        [SerializeField] private Slider _slider;
        [SerializeField] private Text _label;

        private void Update()
        {
            if (_playerStatus == null || _trackedStatus == null) return;
            var v = _playerStatus.Get(_trackedStatus);
            if (v == null) return;
            float ratio = _trackedStatus.MaxValue > 0f ? v.Current / _trackedStatus.MaxValue : 0f;
            if (_slider != null) _slider.value = Mathf.Clamp01(ratio);
            if (_label != null) _label.text = $"{_trackedStatus.DisplayKey}: {Mathf.RoundToInt(v.Current)}";
        }
    }

    /// <summary>
    /// 좌상단 자원 카운트 + 현재 장착 도구 표시. PlayerInventory.Inventory.OnChanged 구독.
    /// FarmCanvasBuilder 가 절차적으로 생성, 또는 FarmAutoFiller 가 런타임 보강.
    /// </summary>
    public sealed class ResourceHud : MonoBehaviour
    {
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private Text _woodLabel;
        [SerializeField] private Text _stoneLabel;
        [SerializeField] private Image _equippedToolIcon;
        [SerializeField] private Text _equippedToolLabel;

        private ItemDefinition _woodItem;
        private ItemDefinition _stoneItem;

        public void Bind(PlayerInventory inv, GameDataRegistry registry)
        {
            if (_inventory != null && _inventory.Inventory != null)
            {
                _inventory.Inventory.OnChanged -= Refresh;
                _inventory.OnEquipmentChanged -= Refresh;
            }
            _inventory = inv;
            _woodItem = inv != null ? inv.FindById("Wood") : null;
            _stoneItem = inv != null ? inv.FindById("Stone") : null;
            if (_inventory != null)
            {
                _inventory.Inventory.OnChanged += Refresh;
                _inventory.OnEquipmentChanged += Refresh;
            }
            Refresh();
        }

        public void BindElements(Text wood, Text stone, Image toolIcon, Text toolLabel)
        {
            _woodLabel = wood;
            _stoneLabel = stone;
            _equippedToolIcon = toolIcon;
            _equippedToolLabel = toolLabel;
        }

        private void OnDestroy()
        {
            if (_inventory != null && _inventory.Inventory != null)
            {
                _inventory.Inventory.OnChanged -= Refresh;
                _inventory.OnEquipmentChanged -= Refresh;
            }
        }

        public void Refresh()
        {
            if (_inventory == null) return;
            if (_woodLabel != null) _woodLabel.text = $"Wood: {_inventory.Inventory.CountOf(_woodItem)}";
            if (_stoneLabel != null) _stoneLabel.text = $"Stone: {_inventory.Inventory.CountOf(_stoneItem)}";
            var tool = _inventory.EquippedToolItem;
            if (_equippedToolIcon != null)
            {
                _equippedToolIcon.sprite = tool != null ? tool.Icon : null;
                _equippedToolIcon.enabled = _equippedToolIcon.sprite != null;
            }
            if (_equippedToolLabel != null)
            {
                _equippedToolLabel.text = tool != null ? tool.Id : "(empty)";
            }
        }
    }

    /// <summary>
    /// 인벤토리 그리드 뷰. 패널 GameObject 토글로 노출/숨김.
    /// 카테고리 필터 (All/Resource/Tool) 로 슬롯 표시 분리. 슬롯 클릭 시 OnSlotSelected 발화.
    /// </summary>
    public sealed class InventoryView : MonoBehaviour
    {
        public enum Filter { All, ResourceOnly, ToolOnly }

        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private RectTransform _slotsRoot;
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private Filter _filter = Filter.All;

        // 선택된 슬롯 — 클릭 시 갱신, Refresh() 가 강조 표시 (빨간 ▶◀ 화살표 자식 GameObject + 외곽 색조).
        // ItemDefinition reference 비교로 매칭 (Slot 인스턴스는 Refresh 시 destroy → 재생성됨).
        private ItemDefinition _selectedItem;
        public ItemDefinition SelectedItem => _selectedItem;

        public event System.Action<Inventory.Slot> OnSlotSelected;

        public void Bind(PlayerInventory inv)
        {
            if (_inventory != null) _inventory.Inventory.OnChanged -= Refresh;
            _inventory = inv;
            if (_inventory != null) _inventory.Inventory.OnChanged += Refresh;
            Refresh();
        }

        public void BindElements(RectTransform slotsRoot, GameObject slotPrefab, Filter filter = Filter.All)
        {
            _slotsRoot = slotsRoot;
            _slotPrefab = slotPrefab;
            _filter = filter;
        }

        public void SetFilter(Filter filter)
        {
            if (_filter == filter) return;
            _filter = filter;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_inventory != null) _inventory.Inventory.OnChanged -= Refresh;
        }

        public void Refresh()
        {
            if (_slotsRoot == null || _inventory == null) return;
            // 기존 자식 제거
            for (int i = _slotsRoot.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_slotsRoot.GetChild(i).gameObject);
            }
            var slots = _inventory.Inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.Item == null || s.Count <= 0) continue;
                if (_filter == Filter.ToolOnly && s.Item.Category != ItemCategory.Tool) continue;
                if (_filter == Filter.ResourceOnly && s.Item.Category == ItemCategory.Tool) continue;
                CreateSlotView(s);
            }
        }

        private void CreateSlotView(Inventory.Slot s)
        {
            GameObject go;
            if (_slotPrefab != null)
            {
                go = Object.Instantiate(_slotPrefab, _slotsRoot);
                go.SetActive(true);
                go.name = $"Slot_{s.Item.Id}";
            }
            else go = MakeDefaultSlotGO(_slotsRoot);

            var icon = FindChildImage(go.transform, "Icon");
            var label = FindChildText(go.transform, "Count");
            if (icon != null) { icon.sprite = s.Item.Icon; icon.enabled = icon.sprite != null; }
            if (label != null) label.text = s.Count > 1 ? s.Count.ToString() : string.Empty;

            // 선택 강조 — 빨간 ▶◀ 화살표 + 슬롯 배경 살짝 빨간 톤. 비선택 시 화살표 비활성.
            bool isSelected = s.Item == _selectedItem;
            ApplySelectionHighlight(go, isSelected);

            // 슬롯 클릭 → 선택만 갱신 (장착은 EQUIP 버튼이 별도 처리).
            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            var captured = s;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                _selectedItem = captured.Item;
                OnSlotSelected?.Invoke(captured);
                Refresh(); // 강조 표시 갱신 — 슬롯 재빌드.
            });
        }

        // 선택된 슬롯에 빨간 ▶◀ 화살표 자식 + 배경 빨간 톤. Refresh 마다 호출.
        private static void ApplySelectionHighlight(GameObject slotGo, bool selected)
        {
            // 좌우 ▶◀ 두 개를 자식으로 만들어두고 활성/비활성 토글. 이미 만들어져 있으면 재사용.
            var arrowL = slotGo.transform.Find("ArrowL");
            var arrowR = slotGo.transform.Find("ArrowR");
            if (arrowL == null) arrowL = MakeArrow(slotGo.transform, "ArrowL", isLeft: true).transform;
            if (arrowR == null) arrowR = MakeArrow(slotGo.transform, "ArrowR", isLeft: false).transform;
            arrowL.gameObject.SetActive(selected);
            arrowR.gameObject.SetActive(selected);

            // 배경 색조 — 선택 시 살짝 빨간 톤.
            var bg = slotGo.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = selected
                    ? new Color(1.0f, 0.7f, 0.6f, 1f)   // 빨간 톤 강조
                    : new Color(0.85f, 0.75f, 0.55f, 1f); // 평소 베이지
            }
        }

        private static GameObject MakeArrow(Transform parent, string name, bool isLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            // 좌측 ▶ 는 슬롯 좌측 바깥, 우측 ◀ 는 슬롯 우측 바깥.
            if (isLeft)
            {
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-2f, 0f);
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(2f, 0f);
            }
            rt.sizeDelta = new Vector2(20f, 28f);
            var t = go.GetComponent<Text>();
            t.text = isLeft ? "▶" : "◀";
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.85f, 0.2f, 0.15f, 1f);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontStyle = FontStyle.Bold;
            t.fontSize = 22;
            t.raycastTarget = false;
            return go;
        }

        private static GameObject MakeDefaultSlotGO(Transform parent)
        {
            var go = new GameObject("Slot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.4f);
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)iconGo.transform;
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.9f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            var labelGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = new Vector2(0.5f, 0f);
            lrt.anchorMax = new Vector2(1f, 0.4f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var lbl = labelGo.GetComponent<Text>();
            lbl.alignment = TextAnchor.LowerRight;
            lbl.color = Color.white;
            lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.fontSize = 14;
            return go;
        }

        private static Image FindChildImage(Transform t, string name)
        {
            var c = t.Find(name);
            return c != null ? c.GetComponent<Image>() : null;
        }
        private static Text FindChildText(Transform t, string name)
        {
            var c = t.Find(name);
            return c != null ? c.GetComponent<Text>() : null;
        }
    }

    /// <summary>
    /// Farm 씬 UI 자동 구축. 책 펼침 레이아웃 (좌측 ITEMS 그리드 + 우측 Description/Equipment).
    /// I 키로 토글, 카테고리 북마크 5개로 좌측 페이지 필터 전환 (All/Resource/Tool/Equipment...).
    /// </summary>
    public sealed class FarmHudController : MonoBehaviour
    {
        // 카테고리 — 책갈피 색깔 매핑
        private enum BookCategory { All, Resource, Tool, Equipment, Misc }

        private GameObject _hudPanel;
        private GameObject _hintPanel;
        private GameObject _bookPanel;
        private Image _bookImage;            // BookFlipper 가 sprite 교체
        private GameObject _leftContent;     // 좌측 페이지 모든 콘텐츠 (애니메이션 시 hide)
        private GameObject _rightContent;    // 우측 페이지 모든 콘텐츠

        // 카테고리별 좌/우 페이지 콘텐츠 컨테이너 — ApplyCategory 시 해당 카테고리만 SetActive(true).
        private readonly Dictionary<BookCategory, GameObject> _leftPagesByCat = new();
        private readonly Dictionary<BookCategory, GameObject> _rightPagesByCat = new();
        // 카테고리별 북마크 RectTransform — 선택 시 우측 offset 변경 (튀어나오게).
        private readonly Dictionary<BookCategory, RectTransform> _bookmarkRectsByCat = new();
        // 북마크 — 책 panel 우측 가장자리 (anchor 1, pivot 0) 기준.
        // 샘플 이미지: 북마크 폭 절반 정도가 책 위에 겹치고, 나머지 절반이 바깥으로 튀어나옴.
        // 선택 시 추가로 더 우측으로 이동 (책에서 떨어지는 데모 효과).
        // 북마크 anchor 기준 = 베이지 페이지 우측 끝 (UV 0.907 — Page1 sprite 픽셀 263/290).
        // BookPanel 회색 프레임 (UV 1.0) 이 아니라 베이지 페이지 가장자리 기준이라야 샘플 이미지처럼 책 옆에 딱 붙음.
        // pivot=(0, 0.5) 라 anchoredPosition.x 음수 = 베이지 안쪽, 양수 = 회색 프레임/갈색 측면 위로.
        private const float BookmarkRestX = -10f;     // 평소 (북마크 좌측이 베이지 페이지 안쪽으로 10px 들어감)
        private const float BookmarkSelectedX = +14f; // 선택 (회색 프레임 위로 살짝 튀어나옴)
        private ResourceHud _resourceHud;
        private InventoryView _inventoryView;
        private Text _selectedNameLabel;
        private Text _selectedDescriptionLabel;
        private Image _selectedIcon;
        private Image _equippedSlotIcon;
        private Text _equippedSlotLabel;
        private Text _itemCountLabel;
        private GameObject _slotPrefab;
        private GameObject _equipButton;     // 우측 페이지 EQUIP 버튼 — 도구 선택 시에만 활성.
        private Inventory.Slot _currentSelected; // 우측 페이지가 보고 있는 슬롯 (EquipBtn 콜백에서 사용).
        private BookCategory _currentCategory = BookCategory.All;
        private float _flipTime = -1f;       // 페이지 넘김 애니메이션 진행 시간(초). -1 = 정지.
        private const float FlipDuration = 0.45f; // 9프레임 × 0.05s

        private InputAction _toggleInventory;

        private PlayerInventory _playerInv;

        // Addressables 캐시 동기 조회 헬퍼.
        private static Sprite Spr(string addr)
        {
            var rm = Rootborn.Game.Managers.Managers.Resource;
            return rm != null ? rm.Load<Sprite>(addr) : null;
        }
        private static Sprite SubSpr(string sheetAddr, string subName)
        {
            var rm = Rootborn.Game.Managers.Managers.Resource;
            if (rm == null) return null;
            // 신규 이름(`Bookmark_0`) 시도 후 실패 시 구 이름(`Bookmark_r0_c0`) 시도 — 사용자가 슬라이스 재실행 안 한 케이스 fallback.
            var s = rm.GetCachedSubSprite(sheetAddr, subName);
            if (s != null) return s;
            // 구 이름 추정: "Bookmark_0" → "Bookmark_r0_c0"
            if (subName.StartsWith("Bookmark_") && subName.Length == 10)
            {
                char idx = subName[9];
                var legacyName = $"Bookmark_r{idx}_c0";
                s = rm.GetCachedSubSprite(sheetAddr, legacyName);
            }
            return s;
        }

        // Awake 에서는 BuildTree 하지 않는다 — Managers.BootstrapAsync 가 같은 씬 GameBootstrap.Start 에서
        // 시작되므로 Awake 시점엔 Addressables 캐시가 비어있다 → 모든 sprite 가 null fallback (단색 사각형) 으로 빌드됨.
        // 대신 async Start 에서 BootstrapAsync 완료를 await 한 뒤 BuildTree 호출.
        private async void Start()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[ROOTBORN/UI] FarmHudController must be on a GameObject under a Canvas.");
                enabled = false;
                return;
            }

            // Bootstrap 이 아직 안 끝났을 수 있음 — UI sprite 사전 로드까지 보장된 후에 빌드.
            // BootstrapAsync 는 멱등 (IsBootstrapped 캐시) 이라 중복 await 안전.
            try
            {
                await Rootborn.Game.Managers.Managers.BootstrapAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ROOTBORN/UI] BootstrapAsync threw — UI 일부 sprite fallback. {e.Message}");
            }

            // 컴포넌트가 그 사이 destroy 됐을 수 있음.
            if (this == null || !isActiveAndEnabled || canvas == null) return;

            BuildTree(canvas.transform);

            // BuildTree 직후 카테고리=All 강제 적용 — _playerInv 가 null 이어도 페이지 토글은 발화.
            _currentCategory = BookCategory.Equipment;
            ApplyCategory(BookCategory.All);

            // PlayerInventory 와이어링.
            var playerGo = GameObject.Find("Player");
            if (playerGo != null) _playerInv = playerGo.GetComponent<PlayerInventory>();
            if (_playerInv == null)
            {
                Debug.LogWarning("[ROOTBORN/UI] PlayerInventory not found — UI will be empty.");
                return;
            }

            var registry = Rootborn.Game.Managers.Managers.Data?.Registry;
            if (_resourceHud != null) _resourceHud.Bind(_playerInv, registry);
            if (_inventoryView != null)
            {
                _inventoryView.Bind(_playerInv);
                _inventoryView.OnSlotSelected += OnSlotSelected;
            }
            _playerInv.Inventory.OnChanged += UpdateCountLabel;
            _playerInv.OnEquipmentChanged += UpdateEquippedSlot;
            UpdateEquippedSlot();
            UpdateCountLabel();
            ApplyCategory(_currentCategory);
        }

        private void OnEnable()
        {
            _toggleInventory = new InputAction(type: InputActionType.Button);
            _toggleInventory.AddBinding("<Keyboard>/i");
            _toggleInventory.AddBinding("<Keyboard>/tab");
            _toggleInventory.performed += _ => ToggleBook();
            _toggleInventory.Enable();
        }

        // 책 열고 닫기 — 책이 열리면 HUD/HotkeyHint 숨김 (집중도 ↑) + 페이지 넘김 애니메이션.
        private void ToggleBook()
        {
            if (_bookPanel == null) return;
            bool willOpen = !_bookPanel.activeSelf;
            _bookPanel.SetActive(willOpen);
            if (_hudPanel != null) _hudPanel.SetActive(!willOpen);
            if (_hintPanel != null) _hintPanel.SetActive(!willOpen);
            if (willOpen) _flipTime = 0f; // 책 펼침 애니메이션
        }

        private void OnDisable()
        {
            _toggleInventory?.Disable();
            _toggleInventory?.Dispose();
            _toggleInventory = null;
            if (_playerInv != null)
            {
                _playerInv.OnEquipmentChanged -= UpdateEquippedSlot;
                _playerInv.Inventory.OnChanged -= UpdateCountLabel;
            }
            if (_inventoryView != null) _inventoryView.OnSlotSelected -= OnSlotSelected;
        }

        private static void Toggle(GameObject panel)
        {
            if (panel == null) return;
            panel.SetActive(!panel.activeSelf);
        }

        private void UpdateEquippedSlot()
        {
            if (_playerInv == null) return;
            var tool = _playerInv.EquippedToolItem;
            if (_equippedSlotIcon != null)
            {
                _equippedSlotIcon.sprite = tool != null ? tool.Icon : null;
                _equippedSlotIcon.enabled = _equippedSlotIcon.sprite != null;
            }
            if (_equippedSlotLabel != null)
            {
                _equippedSlotLabel.text = tool != null ? tool.Id : "(none)";
            }
        }

        private void OnSlotSelected(Inventory.Slot s)
        {
            if (s == null || s.Item == null) return;
            _currentSelected = s;
            if (_selectedIcon != null)
            {
                _selectedIcon.sprite = s.Item.Icon;
                _selectedIcon.enabled = s.Item.Icon != null;
            }
            if (_selectedNameLabel != null) _selectedNameLabel.text = s.Item.Id;
            if (_selectedDescriptionLabel != null)
            {
                // 본문은 ItemDefinition.Description 우선. 없으면 메타데이터 fallback.
                string body = !string.IsNullOrEmpty(s.Item.Description)
                    ? s.Item.Description
                    : $"{s.Item.Id} ({s.Item.Category})";
                _selectedDescriptionLabel.text = $"{body}\n\nCount: {s.Count}\nMax stack: {s.Item.MaxStack}";
            }
            // EQUIP 버튼은 도구일 때만 활성화 (장착 해제는 다음 페이즈).
            if (_equipButton != null)
            {
                _equipButton.SetActive(s.Item.Category == ItemCategory.Tool);
            }
        }

        private void UpdateCountLabel()
        {
            if (_itemCountLabel == null || _playerInv == null) return;
            int total = 0;
            var slots = _playerInv.Inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.Item == null || s.Count <= 0) continue;
                if (_currentCategory == BookCategory.Tool && s.Item.Category != ItemCategory.Tool) continue;
                if (_currentCategory == BookCategory.Resource && s.Item.Category != ItemCategory.Resource) continue;
                if (_currentCategory == BookCategory.Misc && s.Item.Category != ItemCategory.Misc) continue;
                total++;
            }
            _itemCountLabel.text = total > 0 ? $"X 1/{total}" : "X 0/0";
        }

        private void ApplyCategory(BookCategory cat)
        {
            bool changed = _currentCategory != cat;
            _currentCategory = cat;
            Debug.Log($"[ROOTBORN/UI] ApplyCategory({cat}) changed={changed} leftPages={_leftPagesByCat.Count} rightPages={_rightPagesByCat.Count}");

            // 카테고리별 좌/우 페이지 콘텐츠 토글 — 선택된 카테고리만 활성.
            // 주의: 한 GameObject 가 여러 카테고리 키에 매핑될 수 있으므로 (예: ITEMS 페이지 = All/Resource/Tool/Misc 공유),
            // 단순 foreach 순회는 마지막 iteration 의 SetActive(false) 가 직전 SetActive(true) 를 덮어쓴다.
            // 두 패스 분리: (A) 모두 비활성 (B) 선택된 카테고리만 활성 — 멱등 + 공유 GameObject 안전.
            ToggleCategoryPages(_leftPagesByCat, cat);
            ToggleCategoryPages(_rightPagesByCat, cat);

            // InventoryView 필터 (All/Resource/Tool/Misc 카테고리에서만 의미). Equipment 는 별도 페이지.
            if (_inventoryView != null)
            {
                var f = cat switch
                {
                    BookCategory.Resource => InventoryView.Filter.ResourceOnly,
                    BookCategory.Tool => InventoryView.Filter.ToolOnly,
                    _ => InventoryView.Filter.All,
                };
                _inventoryView.SetFilter(f);
            }

            // 선택된 북마크는 오른쪽으로 튀어나옴 (스크린샷 데모 효과).
            foreach (var kv in _bookmarkRectsByCat)
            {
                if (kv.Value == null) continue;
                bool selected = kv.Key == cat;
                var p = kv.Value.anchoredPosition;
                p.x = selected ? BookmarkSelectedX : BookmarkRestX;
                kv.Value.anchoredPosition = p;
            }

            UpdateCountLabel();
            // 카테고리 전환 시 페이지 넘김 애니메이션 시작 (책이 열려있을 때만).
            if (changed && _bookPanel != null && _bookPanel.activeSelf) _flipTime = 0f;
        }

        // 두 패스: (A) 모든 unique GameObject 비활성, (B) 선택된 카테고리에 매핑된 GameObject 만 활성.
        // 같은 GameObject 가 여러 카테고리 키 (예: All/Resource/Tool/Misc) 에 매핑된 경우에도 멱등 동작 보장.
        private static void ToggleCategoryPages(Dictionary<BookCategory, GameObject> map, BookCategory cat)
        {
            // (A) 모두 false — HashSet 으로 중복 SetActive 호출 회피.
            var seen = new HashSet<GameObject>();
            foreach (var kv in map)
            {
                if (kv.Value == null) continue;
                if (!seen.Add(kv.Value)) continue;
                kv.Value.SetActive(false);
            }
            // (B) 선택된 카테고리만 true — Dictionary lookup 1회.
            if (map.TryGetValue(cat, out var selected) && selected != null)
            {
                selected.SetActive(true);
            }
        }

        private void Update()
        {
            // 페이지 넘김 9프레임 sprite 교체 (Page1 → Page9 → Page1 정지).
            if (_bookImage == null) return;
            if (_flipTime < 0f) return; // 비활성
            if (_flipTime >= FlipDuration)
            {
                // 애니메이션 끝 — 정지 프레임(Page1) 으로 복귀 + 콘텐츠 다시 표시.
                var rest = Spr(UISpriteAddresses.BookPage1);
                if (rest != null && _bookImage.sprite != rest) _bookImage.sprite = rest;
                _flipTime = -1f;
                SetContentVisible(true);
                return;
            }
            // 애니메이션 진행 중 — 콘텐츠는 가려져야 자연스러움.
            SetContentVisible(false);
            _flipTime += UnityEngine.Time.deltaTime;
            int frame = Mathf.Clamp(Mathf.FloorToInt(_flipTime / (FlipDuration / 9f)), 0, 8);
            var addr = UISpriteAddresses.BookFlipFrames[frame];
            var s = Spr(addr);
            if (s != null) _bookImage.sprite = s;
        }

        private void SetContentVisible(bool visible)
        {
            if (_leftContent != null && _leftContent.activeSelf != visible) _leftContent.SetActive(visible);
            if (_rightContent != null && _rightContent.activeSelf != visible) _rightContent.SetActive(visible);
        }

        // ========== UI 트리 절차 생성 ==========

        private void BuildTree(Transform canvasRoot)
        {
            // 슬롯 template — 비활성 + Canvas 자식으로 두어 destroy 시 함께 정리.
            var templates = new GameObject("[Templates]", typeof(RectTransform));
            templates.transform.SetParent(canvasRoot, false);
            templates.SetActive(false);
            _slotPrefab = MakeSlotPrefab(Spr(UISpriteAddresses.ItemSlot));
            _slotPrefab.transform.SetParent(templates.transform, false);

            // 1) 좌상단 HUD
            BuildHud(canvasRoot);

            // 2) 책 패널 (중앙)
            BuildBookPanel(canvasRoot);

            // 3) 단축키 힌트 (하단) — 760×60 (이전 440×36 너무 작음)
            var hintPanel = MakePanel(canvasRoot, "HotkeyHint",
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f),
                pos: new Vector2(0f, 30f), size: new Vector2(760f, 60f),
                sprite: Spr(UISpriteAddresses.HintPanel),
                fallbackColor: new Color(0f, 0f, 0f, 0.45f));
            var hint = MakeText(hintPanel, "Text", new Vector2(0f, 0f), new Vector2(740f, 48f),
                "I / Tab: Book   E: Interact   WASD: Move", 22, TextAnchor.MiddleCenter);
            ((RectTransform)hint.transform).anchorMin = new Vector2(0.5f, 0.5f);
            ((RectTransform)hint.transform).anchorMax = new Vector2(0.5f, 0.5f);
            ((RectTransform)hint.transform).pivot = new Vector2(0.5f, 0.5f);
            hint.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            hint.fontStyle = FontStyle.Bold;
            _hintPanel = hintPanel.gameObject;
        }

        private void BuildHud(Transform canvasRoot)
        {
            // 1920×1080 기준 좌상단 패널 — 480×200 으로 확대 (이전 300×130 너무 작음).
            var hudPanel = MakePanel(canvasRoot, "HUD",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f),
                pos: new Vector2(30f, -30f), size: new Vector2(480f, 200f),
                sprite: Spr(UISpriteAddresses.HudPanel),
                fallbackColor: new Color(0f, 0f, 0f, 0.55f));

            var woodLabel = MakeText(hudPanel, "Wood", new Vector2(30f, -22f), new Vector2(420f, 32f), "Wood: 0", 24, TextAnchor.UpperLeft);
            var stoneLabel = MakeText(hudPanel, "Stone", new Vector2(30f, -60f), new Vector2(420f, 32f), "Stone: 0", 24, TextAnchor.UpperLeft);
            var toolLabel = MakeText(hudPanel, "Tool", new Vector2(86f, -110f), new Vector2(360f, 32f), "BareHand", 22, TextAnchor.UpperLeft);
            var toolIcon = MakeImage(hudPanel, "ToolIcon", new Vector2(30f, -108f), new Vector2(48f, 48f));
            toolIcon.preserveAspect = true;
            toolIcon.color = Color.white;
            toolIcon.enabled = false;
            woodLabel.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            stoneLabel.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            toolLabel.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            woodLabel.fontStyle = FontStyle.Bold;
            stoneLabel.fontStyle = FontStyle.Bold;

            _resourceHud = hudPanel.gameObject.AddComponent<ResourceHud>();
            _resourceHud.BindElements(woodLabel, stoneLabel, toolIcon, toolLabel);
            _hudPanel = hudPanel.gameObject;
        }

        private void BuildBookPanel(Transform canvasRoot)
        {
            // Page1.png 실측 290×184 (16:10.14). 1920×1080 기준 화면 폭 65% 노출 → 1248×792 (≈ pixelScale 4.3, 비율 보존).
            // Sprite preserveAspect 로 letterboxing 거의 없음. 실 Anchor 비율 = sprite 픽셀 비율 그대로.
            const float bookW = 1248f, bookH = 792f;
            _bookPanel = new GameObject("BookPanel", typeof(RectTransform), typeof(Image));
            _bookPanel.transform.SetParent(canvasRoot, false);
            var bookRt = (RectTransform)_bookPanel.transform;
            bookRt.anchorMin = new Vector2(0.5f, 0.5f);
            bookRt.anchorMax = new Vector2(0.5f, 0.5f);
            bookRt.pivot = new Vector2(0.5f, 0.5f);
            bookRt.anchoredPosition = Vector2.zero;
            bookRt.sizeDelta = new Vector2(bookW, bookH);

            _bookImage = _bookPanel.GetComponent<Image>();
            var bookSprite = Spr(UISpriteAddresses.BookPage1);
            if (bookSprite != null)
            {
                _bookImage.sprite = bookSprite;
                _bookImage.type = Image.Type.Simple;
                _bookImage.preserveAspect = true;
                _bookImage.color = Color.white;
                _bookImage.raycastTarget = true;
            }
            else
            {
                Debug.LogWarning("[ROOTBORN/UI] BookPage1 sprite 캐시 미스 — Addressables PreLoad 확인.");
                _bookImage.color = new Color(0.94f, 0.86f, 0.68f, 1f);
            }

            // Page1 픽셀 290×184 실측 (Read 도구 시각 확인) — 페이지 안쪽 베이지 영역(테두리 inscription 안쪽 콘텐츠 안전 영역):
            //  외부 회색 모서리 + 어두운 프레임이 ~22px 좌우, ~16px 상하.
            //  좌측 베이지 페이지 콘텐츠 안전 영역: x ≈ 30~135  / y ≈ 22~162
            //  우측 베이지 페이지 콘텐츠 안전 영역: x ≈ 158~263 / y ≈ 22~162
            //  spine 가운데 ≈ 143~150
            //  → UV anchor (0,0=좌하):
            //    좌측 페이지: x 30/290~135/290 = 0.103~0.466,  y (184-162)/184~(184-22)/184 = 0.120~0.880
            //    우측 페이지: x 158/290~263/290 = 0.545~0.907, y 동일
            _leftContent = new GameObject("LeftPageContent", typeof(RectTransform));
            _leftContent.transform.SetParent(_bookPanel.transform, false);
            var leftRt = (RectTransform)_leftContent.transform;
            leftRt.anchorMin = new Vector2(0.103f, 0.120f);
            leftRt.anchorMax = new Vector2(0.466f, 0.880f);
            leftRt.offsetMin = Vector2.zero; leftRt.offsetMax = Vector2.zero;

            _rightContent = new GameObject("RightPageContent", typeof(RectTransform));
            _rightContent.transform.SetParent(_bookPanel.transform, false);
            var rightRt = (RectTransform)_rightContent.transform;
            rightRt.anchorMin = new Vector2(0.545f, 0.120f);
            rightRt.anchorMax = new Vector2(0.907f, 0.880f);
            rightRt.offsetMin = Vector2.zero; rightRt.offsetMax = Vector2.zero;

            // 카테고리별 좌/우 페이지 컨테이너 — ApplyCategory 시 해당 카테고리만 활성.
            BuildItemsCategoryPages(leftRt, rightRt);     // All / Resource / Tool / Misc 공유 (필터만 다름)
            BuildEquipmentCategoryPages(leftRt, rightRt); // Equipment 전용

            // 우측 책 가장자리 북마크 5색 (책 panel 자체에 우측 바깥쪽 배치).
            BuildBookmarks(_bookPanel.transform);

            _bookPanel.SetActive(false);
        }

        // ITEMS 카테고리 페이지 — All/Resource/Tool/Misc 4개에서 같은 페이지를 공유 (필터만 변경).
        private void BuildItemsCategoryPages(RectTransform leftRt, RectTransform rightRt)
        {
            // 좌측: 컨테이너 1개를 생성하고 4개 카테고리 모두에 매핑.
            var leftItems = new GameObject("LeftItems", typeof(RectTransform));
            leftItems.transform.SetParent(leftRt, false);
            var lirt = (RectTransform)leftItems.transform;
            lirt.anchorMin = Vector2.zero; lirt.anchorMax = Vector2.one;
            lirt.offsetMin = Vector2.zero; lirt.offsetMax = Vector2.zero;
            BuildLeftPageContent(lirt);
            _leftPagesByCat[BookCategory.All] = leftItems;
            _leftPagesByCat[BookCategory.Resource] = leftItems;
            _leftPagesByCat[BookCategory.Tool] = leftItems;
            _leftPagesByCat[BookCategory.Misc] = leftItems;

            // 우측: ITEMS 페이지 (선택 아이콘 + DESCRIPTION).
            var rightItems = new GameObject("RightItems", typeof(RectTransform));
            rightItems.transform.SetParent(rightRt, false);
            var rirt = (RectTransform)rightItems.transform;
            rirt.anchorMin = Vector2.zero; rirt.anchorMax = Vector2.one;
            rirt.offsetMin = Vector2.zero; rirt.offsetMax = Vector2.zero;
            BuildRightPageContent(rirt);
            _rightPagesByCat[BookCategory.All] = rightItems;
            _rightPagesByCat[BookCategory.Resource] = rightItems;
            _rightPagesByCat[BookCategory.Tool] = rightItems;
            _rightPagesByCat[BookCategory.Misc] = rightItems;
        }

        // EQUIPMENT 카테고리 페이지 — 좌측 캐릭터+장착 슬롯, 우측 STATS. 샘플 이미지 4번 (4/15) 레이아웃.
        private void BuildEquipmentCategoryPages(RectTransform leftRt, RectTransform rightRt)
        {
            // 좌측: EQUIPMENT 리본 + 캐릭터 실루엣(중앙) + 장착 슬롯 6개 (좌3, 우3).
            var leftEquip = new GameObject("LeftEquipment", typeof(RectTransform));
            leftEquip.transform.SetParent(leftRt, false);
            var lert = (RectTransform)leftEquip.transform;
            lert.anchorMin = Vector2.zero; lert.anchorMax = Vector2.one;
            lert.offsetMin = Vector2.zero; lert.offsetMax = Vector2.zero;

            MakeRibbonHeader(lert, UISpriteAddresses.EquipmentRibbon, "EQUIPMENT");

            // 캐릭터 실루엣 (가운데). Character.png 21×37 → ratio ≈ 1.76. 책 LeftPage 안 콘텐츠 영역에서 220×270 노출.
            var characterSprite = Spr(UISpriteAddresses.Character);
            var ch = new GameObject("Character", typeof(RectTransform), typeof(Image));
            ch.transform.SetParent(lert, false);
            var crt = (RectTransform)ch.transform;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0f, -10f);
            crt.sizeDelta = new Vector2(140f, 240f);
            var chImg = ch.GetComponent<Image>();
            if (characterSprite != null)
            {
                chImg.sprite = characterSprite;
                chImg.preserveAspect = true;
                chImg.color = Color.white;
            }
            else
            {
                chImg.color = new Color(0.85f, 0.7f, 0.55f, 0.5f);
            }

            // 장착 슬롯 6개 (좌3, 우3) — 캐릭터 양쪽 (샘플 이미지 4번 EQUIPMENT 참조).
            var equipSlotPositions = new (string name, Vector2 pos)[]
            {
                ("Slot_Head",     new Vector2(-130f,  140f)),
                ("Slot_Chest",    new Vector2(-130f,   30f)),
                ("Slot_Boots",    new Vector2(-130f, -100f)),
                ("Slot_Weapon",   new Vector2( 130f,  140f)),
                ("Slot_Shield",   new Vector2( 130f,   30f)),
                ("Slot_Trinket",  new Vector2( 130f, -100f)),
            };
            var equipSlotSprite = Spr(UISpriteAddresses.EquipmentSlot);
            for (int i = 0; i < equipSlotPositions.Length; i++)
            {
                var (slotName, slotPos) = equipSlotPositions[i];
                var go = new GameObject(slotName, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(lert, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = slotPos;
                rt.sizeDelta = new Vector2(72f, 72f);
                var img = go.GetComponent<Image>();
                if (equipSlotSprite != null)
                {
                    img.sprite = equipSlotSprite;
                    img.type = Image.Type.Sliced;
                    img.color = Color.white;
                }
                else { img.color = new Color(0.7f, 0.55f, 0.35f, 1f); }

                // 첫 슬롯(Slot_Head) 을 현재 장착 도구 표시용으로 와이어링 (placeholder — 추후 부위별 분리).
                if (i == 0)
                {
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(go.transform, false);
                    var irt = (RectTransform)iconGo.transform;
                    irt.anchorMin = new Vector2(0.18f, 0.18f);
                    irt.anchorMax = new Vector2(0.82f, 0.82f);
                    irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                    _equippedSlotIcon = iconGo.GetComponent<Image>();
                    _equippedSlotIcon.preserveAspect = true;
                    _equippedSlotIcon.raycastTarget = false;
                    _equippedSlotIcon.enabled = false;
                }
            }

            _equippedSlotLabel = MakeText(lert, "EquippedLabel",
                new Vector2(0f, 24f), new Vector2(220f, 26f), "(none)", 16, TextAnchor.MiddleCenter);
            ((RectTransform)_equippedSlotLabel.transform).anchorMin = new Vector2(0.5f, 0f);
            ((RectTransform)_equippedSlotLabel.transform).anchorMax = new Vector2(0.5f, 0f);
            ((RectTransform)_equippedSlotLabel.transform).pivot = new Vector2(0.5f, 0f);
            _equippedSlotLabel.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            _equippedSlotLabel.fontStyle = FontStyle.Bold;

            _leftPagesByCat[BookCategory.Equipment] = leftEquip;

            // 우측: STATS 페이지.
            var rightStats = new GameObject("RightStats", typeof(RectTransform));
            rightStats.transform.SetParent(rightRt, false);
            var rsrt = (RectTransform)rightStats.transform;
            rsrt.anchorMin = Vector2.zero; rsrt.anchorMax = Vector2.one;
            rsrt.offsetMin = Vector2.zero; rsrt.offsetMax = Vector2.zero;

            MakeRibbonHeader(rsrt, UISpriteAddresses.EquipmentRibbon, "STATS");

            var statsText = new GameObject("StatsText", typeof(RectTransform), typeof(Text));
            statsText.transform.SetParent(rsrt, false);
            var srt = (RectTransform)statsText.transform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(20f, 20f);
            srt.offsetMax = new Vector2(-20f, -70f);
            var t = statsText.GetComponent<Text>();
            t.text = "VITALITY\n  Health   --/--\n  Mana     --/--\n  Stamina  --/--\n\nDEFENSE\n  Armor      --\n  Resistance --\n\nDAMAGE\n  Melee   --\n  Range   --\n  Speed   --";
            t.alignment = TextAnchor.UpperLeft;
            t.fontSize = 18;
            t.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _rightPagesByCat[BookCategory.Equipment] = rightStats;
        }

        // 페이지 상단 중앙 리본 헤더 — 라벨 텍스트 + Titles sprite.
        private void MakeRibbonHeader(RectTransform parent, string ribbonAddr, string label)
        {
            var ribbon = new GameObject("RibbonHeader", typeof(RectTransform), typeof(Image));
            ribbon.transform.SetParent(parent, false);
            var rt = (RectTransform)ribbon.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -16f);
            rt.sizeDelta = new Vector2(280f, 48f);
            var img = ribbon.GetComponent<Image>();
            var sprite = Spr(ribbonAddr);
            if (sprite != null) { img.sprite = sprite; img.color = Color.white; }
            else { img.color = new Color(0.65f, 0.5f, 0.3f, 1f); }
            var txt = MakeText(rt, "Text", Vector2.zero, new Vector2(260f, 36f), label, 22, TextAnchor.MiddleCenter);
            ((RectTransform)txt.transform).anchorMin = new Vector2(0.5f, 0.5f);
            ((RectTransform)txt.transform).anchorMax = new Vector2(0.5f, 0.5f);
            ((RectTransform)txt.transform).pivot = new Vector2(0.5f, 0.5f);
            txt.color = new Color(0.4f, 0.25f, 0.1f, 1f);
            txt.fontStyle = FontStyle.Bold;
        }

        private void BuildLeftPageContent(RectTransform leftRt)
        {
            // ITEMS 리본 (상단 중앙). 책 1248×792 → LeftPage 콘텐츠 영역 ≈ 452×600. 리본 280×48.
            var itemsRibbon = new GameObject("ItemsRibbon", typeof(RectTransform), typeof(Image));
            itemsRibbon.transform.SetParent(leftRt, false);
            var rrt = (RectTransform)itemsRibbon.transform;
            rrt.anchorMin = new Vector2(0.5f, 1f);
            rrt.anchorMax = new Vector2(0.5f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -16f);
            rrt.sizeDelta = new Vector2(280f, 48f);
            var itemsImg = itemsRibbon.GetComponent<Image>();
            var itemsRibbonSprite = Spr(UISpriteAddresses.ItemsRibbon);
            if (itemsRibbonSprite != null)
            {
                itemsImg.sprite = itemsRibbonSprite;
                itemsImg.color = Color.white;
            }
            else { itemsImg.color = new Color(0.65f, 0.5f, 0.3f, 1f); }
            var itemsText = MakeText(rrt, "Text", Vector2.zero, new Vector2(260f, 36f),
                "ITEMS", 22, TextAnchor.MiddleCenter);
            ((RectTransform)itemsText.transform).anchorMin = new Vector2(0.5f, 0.5f);
            ((RectTransform)itemsText.transform).anchorMax = new Vector2(0.5f, 0.5f);
            ((RectTransform)itemsText.transform).pivot = new Vector2(0.5f, 0.5f);
            itemsText.color = new Color(0.4f, 0.25f, 0.1f, 1f);
            itemsText.fontStyle = FontStyle.Bold;

            // 슬롯 그리드 4×3 — 리본 아래. cell 84×84 + spacing 12 → 4열 = 372 + 36 = 408 (LeftPage 안쪽 fit)
            var slotsRoot = new GameObject("Slots", typeof(RectTransform), typeof(GridLayoutGroup));
            slotsRoot.transform.SetParent(leftRt, false);
            var srt = (RectTransform)slotsRoot.transform;
            srt.anchorMin = new Vector2(0.5f, 1f);
            srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -76f);
            srt.sizeDelta = new Vector2(420f, 388f);
            var grid = slotsRoot.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(84f, 84f);
            grid.spacing = new Vector2(12f, 12f);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            _inventoryView = _bookPanel.AddComponent<InventoryView>();
            _inventoryView.BindElements(srt, _slotPrefab, InventoryView.Filter.All);

            // PREV / NEXT 버튼 (하단)
            var prevBtn = MakeBookButton(leftRt, "PrevBtn",
                pos: new Vector2(8f, 8f), size: new Vector2(108f, 44f), label: "PREV");
            ((RectTransform)prevBtn.transform).anchorMin = new Vector2(0f, 0f);
            ((RectTransform)prevBtn.transform).anchorMax = new Vector2(0f, 0f);
            ((RectTransform)prevBtn.transform).pivot = new Vector2(0f, 0f);

            var nextBtn = MakeBookButton(leftRt, "NextBtn",
                pos: new Vector2(-8f, 8f), size: new Vector2(108f, 44f), label: "NEXT");
            ((RectTransform)nextBtn.transform).anchorMin = new Vector2(1f, 0f);
            ((RectTransform)nextBtn.transform).anchorMax = new Vector2(1f, 0f);
            ((RectTransform)nextBtn.transform).pivot = new Vector2(1f, 0f);
        }

        private void BuildRightPageContent(RectTransform rightRt)
        {
            // 선택 아이템 큰 아이콘 (상단 중앙)
            var selBoxGo = new GameObject("SelectedIconBox", typeof(RectTransform), typeof(Image));
            selBoxGo.transform.SetParent(rightRt, false);
            var srt = (RectTransform)selBoxGo.transform;
            srt.anchorMin = new Vector2(0.5f, 1f);
            srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -10f);
            srt.sizeDelta = new Vector2(80f, 80f);
            var selBoxImg = selBoxGo.GetComponent<Image>();
            var equipSlotSprite = Spr(UISpriteAddresses.EquipmentSlot);
            if (equipSlotSprite != null)
            {
                selBoxImg.sprite = equipSlotSprite;
                selBoxImg.type = Image.Type.Sliced;
                selBoxImg.color = Color.white;
            }
            else
            {
                // sprite 없으면 투명 — 흰 박스 방지.
                selBoxImg.color = new Color(0f, 0f, 0f, 0f);
            }

            var selIconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            selIconGo.transform.SetParent(selBoxGo.transform, false);
            var srt2 = (RectTransform)selIconGo.transform;
            srt2.anchorMin = new Vector2(0.18f, 0.18f);
            srt2.anchorMax = new Vector2(0.82f, 0.82f);
            srt2.offsetMin = Vector2.zero; srt2.offsetMax = Vector2.zero;
            _selectedIcon = selIconGo.GetComponent<Image>();
            _selectedIcon.preserveAspect = true;
            _selectedIcon.enabled = false; // sprite 없을 때 흰 박스 방지

            // DESCRIPTION 리본 + "X 1/n" — 책 우측 페이지 영역 ≈ 452×600 안쪽.
            var descRibbon = new GameObject("DescRibbon", typeof(RectTransform), typeof(Image));
            descRibbon.transform.SetParent(rightRt, false);
            var drt = (RectTransform)descRibbon.transform;
            drt.anchorMin = new Vector2(0f, 1f);
            drt.anchorMax = new Vector2(0f, 1f);
            drt.pivot = new Vector2(0f, 1f);
            drt.anchoredPosition = new Vector2(10f, -150f);
            drt.sizeDelta = new Vector2(260f, 36f);
            var dimg = descRibbon.GetComponent<Image>();
            var descRibbonSprite = Spr(UISpriteAddresses.DescriptionRibbon);
            if (descRibbonSprite != null)
            {
                dimg.sprite = descRibbonSprite;
                dimg.color = new Color(0.85f, 0.35f, 0.25f, 1f); // 빨간 톤 (샘플 이미지의 빨간 리본)
            }
            else { dimg.color = new Color(0.75f, 0.25f, 0.2f, 1f); }
            var descText = MakeText(drt, "Text", Vector2.zero, new Vector2(240f, 32f),
                "DESCRIPTION", 16, TextAnchor.MiddleCenter);
            ((RectTransform)descText.transform).anchorMin = new Vector2(0.5f, 0.5f);
            ((RectTransform)descText.transform).anchorMax = new Vector2(0.5f, 0.5f);
            ((RectTransform)descText.transform).pivot = new Vector2(0.5f, 0.5f);
            descText.color = new Color(0.98f, 0.95f, 0.85f, 1f);
            descText.fontStyle = FontStyle.Bold;

            _itemCountLabel = MakeText(rightRt, "ItemCount",
                new Vector2(-10f, -150f), new Vector2(80f, 32f), "X 0/0", 16, TextAnchor.MiddleCenter);
            ((RectTransform)_itemCountLabel.transform).anchorMin = new Vector2(1f, 1f);
            ((RectTransform)_itemCountLabel.transform).anchorMax = new Vector2(1f, 1f);
            ((RectTransform)_itemCountLabel.transform).pivot = new Vector2(1f, 1f);
            _itemCountLabel.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            _itemCountLabel.fontStyle = FontStyle.Bold;

            // 선택 이름 + 디스크립션
            _selectedNameLabel = MakeText(rightRt, "SelectedName",
                new Vector2(0f, -190f), new Vector2(0f, 26f), "Click an item",
                18, TextAnchor.UpperLeft);
            ((RectTransform)_selectedNameLabel.transform).anchorMin = new Vector2(0f, 1f);
            ((RectTransform)_selectedNameLabel.transform).anchorMax = new Vector2(1f, 1f);
            ((RectTransform)_selectedNameLabel.transform).pivot = new Vector2(0f, 1f);
            ((RectTransform)_selectedNameLabel.transform).offsetMin = new Vector2(14f, -218f);
            ((RectTransform)_selectedNameLabel.transform).offsetMax = new Vector2(-14f, -190f);
            _selectedNameLabel.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            _selectedNameLabel.fontStyle = FontStyle.Bold;

            _selectedDescriptionLabel = new GameObject("SelectedDescription", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            _selectedDescriptionLabel.transform.SetParent(rightRt, false);
            var ddrt = (RectTransform)_selectedDescriptionLabel.transform;
            ddrt.anchorMin = new Vector2(0f, 0f);
            ddrt.anchorMax = new Vector2(1f, 1f);
            ddrt.offsetMin = new Vector2(14f, 18f);
            ddrt.offsetMax = new Vector2(-14f, -200f);
            _selectedDescriptionLabel.alignment = TextAnchor.UpperLeft;
            _selectedDescriptionLabel.fontSize = 16;
            _selectedDescriptionLabel.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            _selectedDescriptionLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _selectedDescriptionLabel.text = "";

            // EQUIP 버튼 — 우측 페이지 하단 중앙. 도구 선택 시에만 OnSlotSelected 가 활성화.
            _equipButton = MakeBookButton(rightRt, "EquipBtn",
                pos: new Vector2(0f, 16f), size: new Vector2(160f, 44f), label: "EQUIP");
            ((RectTransform)_equipButton.transform).anchorMin = new Vector2(0.5f, 0f);
            ((RectTransform)_equipButton.transform).anchorMax = new Vector2(0.5f, 0f);
            ((RectTransform)_equipButton.transform).pivot = new Vector2(0.5f, 0f);
            var equipBtnComp = _equipButton.GetComponent<Button>();
            equipBtnComp.onClick.RemoveAllListeners();
            equipBtnComp.onClick.AddListener(() =>
            {
                if (_playerInv == null || _currentSelected == null || _currentSelected.Item == null) return;
                if (_currentSelected.Item.Category != ItemCategory.Tool) return;
                _playerInv.EquipTool(_currentSelected.Item);
            });
            _equipButton.SetActive(false); // 슬롯 선택 전엔 숨김.

            // _equipmentPageContent 는 더 이상 만들지 않는다 — Equipment 카테고리 우측 콘텐츠는
            // BuildEquipmentCategoryPages 가 별도 컨테이너 (_rightPagesByCat[Equipment]) 에 빌드.
            // ApplyCategory 가 좌/우 카테고리별 페이지를 토글하므로 충돌 없음.
        }

        private void BuildBookmarks(Transform bookRoot)
        {
            // 북마크 — sheet 의 5색 sub-sprite 직접 사용 (Addressables 캐시 조회).
            var entries = new (BookCategory cat, Sprite sprite, string label)[]
            {
                (BookCategory.All,       SubSpr(UISpriteAddresses.BookmarkSheet, UISpriteAddresses.SubBookmark0), "All"),
                (BookCategory.Resource,  SubSpr(UISpriteAddresses.BookmarkSheet, UISpriteAddresses.SubBookmark1), "Res"),
                (BookCategory.Tool,      SubSpr(UISpriteAddresses.BookmarkSheet, UISpriteAddresses.SubBookmark2), "Tool"),
                (BookCategory.Equipment, SubSpr(UISpriteAddresses.BookmarkSheet, UISpriteAddresses.SubBookmark3), "Eq"),
                (BookCategory.Misc,      SubSpr(UISpriteAddresses.BookmarkSheet, UISpriteAddresses.SubBookmark4), "Misc"),
            };

            // sprite cell native 22×20. 책 1248×792 기준.
            // bmW=66 (책 폭의 5.3%), bmH=96 (책 높이의 12%), spacing 116 (5×116=580 책 안 fit).
            const float bmW = 66f, bmH = 96f;
            const float spacing = 116f;
            float totalH = entries.Length * spacing;
            float startY = totalH * 0.5f - spacing * 0.5f;

            for (int i = 0; i < entries.Length; i++)
            {
                var (cat, sprite, label) = entries[i];
                var go = new GameObject($"Bookmark_{cat}", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(bookRoot, false);
                var rt = (RectTransform)go.transform;
                // anchor = 베이지 페이지 우측 끝 (UV 0.907) — BookPanel 회색 프레임 끝(1.0) 이 아닌 베이지 가장자리.
                // pivot 좌측 (0, 0.5) → anchoredPosition.x 음수 = 베이지 안쪽, 양수 = 회색 프레임/갈색 측면 위로.
                rt.anchorMin = new Vector2(0.907f, 0.5f);
                rt.anchorMax = new Vector2(0.907f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(BookmarkRestX, startY - i * spacing);
                rt.sizeDelta = new Vector2(bmW, bmH);
                _bookmarkRectsByCat[cat] = rt;

                var img = go.GetComponent<Image>();
                if (sprite != null)
                {
                    img.sprite = sprite;
                    img.color = Color.white;
                    // preserveAspect=true 로 native 22:19 비율 유지 — 깃발 좌/우 cut 픽셀이 정확히 노출되어
                    // 샘플 이미지의 들쑥날쑥 모양이 살아남. false 로 두면 강제 stretch 되어 단순 사각형처럼 보임.
                    img.preserveAspect = true;
                }
                else
                {
                    // sprite 미와이어링 fallback — 색만이라도.
                    var fallback = new[] {
                        new Color(0.85f, 0.4f, 0.3f, 1f),
                        new Color(0.4f, 0.55f, 0.85f, 1f),
                        new Color(0.55f, 0.8f, 0.4f, 1f),
                        new Color(0.95f, 0.85f, 0.4f, 1f),
                        new Color(0.7f, 0.4f, 0.85f, 1f),
                    };
                    img.color = fallback[i];
                }

                var btn = go.GetComponent<Button>();
                var captured = cat;
                btn.onClick.AddListener(() => ApplyCategory(captured));
            }
        }

        private GameObject MakeBookButton(RectTransform parent, string name, Vector2 pos, Vector2 size, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            var smallBtnSprite = Spr(UISpriteAddresses.SmallButton);
            if (smallBtnSprite != null)
            {
                img.sprite = smallBtnSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else { img.color = new Color(0.5f, 0.4f, 0.25f, 1f); }
            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)lblGo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var lbl = lblGo.GetComponent<Text>();
            lbl.text = label;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.fontSize = 18;
            lbl.color = new Color(0.3f, 0.2f, 0.1f, 1f);
            lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.fontStyle = FontStyle.Bold;
            lbl.raycastTarget = false;
            return go;
        }

        // 슬롯 prefab — 단순 GameObject 트리 (Instantiate 가능). InventoryView 가 사용.
        private static GameObject MakeSlotPrefab(Sprite slotSprite)
        {
            var go = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button));
            var bg = go.GetComponent<Image>();
            if (slotSprite != null)
            {
                bg.sprite = slotSprite;
                bg.type = Image.Type.Sliced;
                // 살짝 어두운 베이지로 책 페이지 배경과 대비.
                bg.color = new Color(0.85f, 0.75f, 0.55f, 1f);
            }
            else
            {
                bg.color = new Color(0f, 0f, 0f, 0.4f);
            }

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)iconGo.transform;
            rt.anchorMin = new Vector2(0.15f, 0.15f);
            rt.anchorMax = new Vector2(0.85f, 0.85f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var labelGo = new GameObject("Count", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = new Vector2(0.4f, 0f);
            lrt.anchorMax = new Vector2(1f, 0.4f);
            lrt.offsetMin = new Vector2(0, 2); lrt.offsetMax = new Vector2(-4, 0);
            var lbl = labelGo.GetComponent<Text>();
            lbl.alignment = TextAnchor.LowerRight;
            lbl.color = new Color(1f, 1f, 1f, 0.95f);
            lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.fontSize = 18;
            lbl.fontStyle = FontStyle.Bold;
            lbl.raycastTarget = false;

            // Disable so it doesn't render on its own (used as Instantiate template).
            go.SetActive(false);
            return go;
        }

        private static GameObject MakeSlotImageOnly(RectTransform parent, string name, Vector2 pos, Vector2 size, Sprite slotSprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var bg = go.GetComponent<Image>();
            if (slotSprite != null) { bg.sprite = slotSprite; bg.type = Image.Type.Sliced; bg.color = Color.white; }
            else bg.color = new Color(0f, 0f, 0f, 0.5f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = new Vector2(0.15f, 0.15f);
            irt.anchorMax = new Vector2(0.85f, 0.85f);
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.preserveAspect = true;
            return go;
        }

        private static Image FindChildImageStatic(Transform t, string name)
        {
            var c = t.Find(name);
            return c != null ? c.GetComponent<Image>() : null;
        }

        // ========== UGUI 헬퍼 ==========

        private static RectTransform MakePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
            Sprite sprite = null, Color? fallbackColor = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = fallbackColor ?? new Color(0f, 0f, 0f, 0.5f);
            }
            return rt;
        }

        private static Text MakeText(RectTransform parent, string name, Vector2 pos, Vector2 size,
            string text, int fontSize, TextAnchor align)
        {
            if (parent == null) return null;
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        private static Image MakeImage(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go.GetComponent<Image>();
        }

        private static RectTransform MakeGrid(RectTransform parent, string name, Vector2 pos, Vector2 size,
            Vector2 cellSize, Vector2 spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var grid = go.GetComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = new RectOffset(4, 4, 4, 4);
            return rt;
        }
    }
}
