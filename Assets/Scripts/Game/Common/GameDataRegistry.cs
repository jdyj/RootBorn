using System;
using System.Collections.Generic;
using Rootborn.Game.Crops;
using Rootborn.Game.Generation;
using Rootborn.Game.Heir;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Resources;
using Rootborn.Game.Status;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Common
{
    public enum ItemCategory { Resource, Tool, Misc, Seed }

    // ItemDefinition 은 ItemDefinition.cs 로 분리됨 — Unity 가 파일명과 같은 단일 MonoScript 만 생성하므로
    // .asset (m_Script 참조) 가 작동하려면 별도 파일 필수.

    /// <summary>
    /// 런타임 인벤토리. 슬롯 리스트 + Add/Remove + OnChanged 이벤트.
    /// 직렬화 안 함 (저장은 후속 작업).
    /// </summary>
    public sealed class Inventory
    {
        public sealed class Slot
        {
            public ItemDefinition Item;
            public int Count;
        }

        private readonly List<Slot> _slots = new List<Slot>(32);

        public IReadOnlyList<Slot> Slots => _slots;
        public event Action OnChanged;

        public void Add(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return;
            int remaining = count;
            int max = item.MaxStack;

            // 1. 기존 같은 아이템 슬롯 채우기
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var s = _slots[i];
                if (s.Item != item) continue;
                int room = max - s.Count;
                if (room <= 0) continue;
                int put = Mathf.Min(room, remaining);
                s.Count += put;
                remaining -= put;
            }
            // 2. 남으면 신규 슬롯 (필요 시 분할)
            while (remaining > 0)
            {
                int put = Mathf.Min(max, remaining);
                _slots.Add(new Slot { Item = item, Count = put });
                remaining -= put;
            }
            OnChanged?.Invoke();
        }

        public bool Remove(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return false;
            if (CountOf(item) < count) return false;
            int remaining = count;
            for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var s = _slots[i];
                if (s.Item != item) continue;
                int take = Mathf.Min(s.Count, remaining);
                s.Count -= take;
                remaining -= take;
                if (s.Count == 0) _slots.RemoveAt(i);
            }
            OnChanged?.Invoke();
            return true;
        }

        public int CountOf(ItemDefinition item)
        {
            if (item == null) return 0;
            int sum = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Item == item) sum += _slots[i].Count;
            }
            return sum;
        }
    }

    /// <summary>
    /// UI sprite Addressables 주소 상수. 헌법 §addressables — 모든 동적 에셋은 Addressables 일원화.
    /// FarmHudController 가 ResourceManager.GetCached 로 sprite 조회. PreLoad 라벨로 부팅 시 일괄 로드.
    /// 신규 sprite 추가 시: AddressablesSetup.cs 의 UiSpriteEntries 에 등록 + 여기에 상수 추가.
    /// </summary>
    public static class UISpriteAddresses
    {
        // Book pages — Page1=정지, Page1~9 페이지 넘김 9프레임 애니메이션.
        public const string BookPage1 = "sprites/ui/book/page-1";
        public const string BookPage2 = "sprites/ui/book/page-2";
        public const string BookPage3 = "sprites/ui/book/page-3";
        public const string BookPage4 = "sprites/ui/book/page-4";
        public const string BookPage5 = "sprites/ui/book/page-5";
        public const string BookPage6 = "sprites/ui/book/page-6";
        public const string BookPage7 = "sprites/ui/book/page-7";
        public const string BookPage8 = "sprites/ui/book/page-8";
        public const string BookPage9 = "sprites/ui/book/page-9";
        public const string BookSpine = "sprites/ui/book/spine";

        public static readonly string[] BookFlipFrames = new[]
        {
            BookPage1, BookPage2, BookPage3, BookPage4, BookPage5,
            BookPage6, BookPage7, BookPage8, BookPage9,
        };

        // backward-compat alias
        public const string BookLeftPage = BookPage1;

        // Panels (9-sliced)
        public const string HudPanel = "sprites/ui/panel/hud";
        public const string HintPanel = "sprites/ui/panel/hint";
        public const string SmallButton = "sprites/ui/button/small";

        // Slots
        public const string ItemSlot = "sprites/ui/slot/item";
        public const string EquipmentSlot = "sprites/ui/slot/equipment";

        // Ribbons & Decoration
        public const string ItemsRibbon = "sprites/ui/ribbon/items";
        public const string DescriptionRibbon = "sprites/ui/ribbon/description";
        public const string EquipmentRibbon = "sprites/ui/ribbon/equipment";
        public const string CutterShort = "sprites/ui/decor/cutter-short";
        public const string CutterLong = "sprites/ui/decor/cutter-long";
        public const string InscriptionPlus = "sprites/ui/decor/inscription-plus";

        // Bookmark sheet (sliced 5 sub-sprites: Bookmark_0 ~ _4)
        public const string BookmarkSheet = "sprites/ui/sheet/bookmark";

        // Character silhouette
        public const string Character = "sprites/ui/character";

        // Bookmark sheet sub-sprite names (PixelwoodSliceSetup.SliceBookmarkSheet 가 명시 rect 로 슬라이스).
        // Bookmark_0 = 가장 위(빨강), Bookmark_4 = 가장 아래(보라).
        public const string SubBookmark0 = "Bookmark_0";
        public const string SubBookmark1 = "Bookmark_1";
        public const string SubBookmark2 = "Bookmark_2";
        public const string SubBookmark3 = "Bookmark_3";
        public const string SubBookmark4 = "Bookmark_4";

        public const string LabelPreLoad = "PreLoad";

        // 부팅 시 사전 로드할 모든 single sprite 주소 (PreLoad 라벨과 별개로 명시 enumerate 가능).
        public static readonly string[] AllSingleSprites = new[]
        {
            BookPage1, BookPage2, BookPage3, BookPage4, BookPage5,
            BookPage6, BookPage7, BookPage8, BookPage9, BookSpine,
            HudPanel, HintPanel, SmallButton,
            ItemSlot, EquipmentSlot,
            ItemsRibbon, DescriptionRibbon, EquipmentRibbon,
            CutterShort, CutterLong, InscriptionPlus,
            Character,
        };

        // 부팅 시 사전 로드할 sub-sprite sheet (address, sub names[]).
        public static readonly (string sheetAddress, string[] subNames)[] AllSheets = new[]
        {
            (BookmarkSheet, new[] { SubBookmark0, SubBookmark1, SubBookmark2, SubBookmark3, SubBookmark4 }),
        };
    }

    /// <summary>
    /// 도구 장착 시 캐릭터 sprite sheet 주소. Axe/Hoe/Pickaxe/Pickup × Down/Side/Up = 12개.
    /// PlayerController.UpdateToolSprite() 가 EquippedTool 에 따라 sheet+frame 선택.
    /// </summary>
    public static class PlayerToolSpriteAddresses
    {
        // Axe
        public const string AxeDown = "sprites/player/tool/axe-down";
        public const string AxeSide = "sprites/player/tool/axe-side";
        public const string AxeUp   = "sprites/player/tool/axe-up";
        // Hoe
        public const string HoeDown = "sprites/player/tool/hoe-down";
        public const string HoeSide = "sprites/player/tool/hoe-side";
        public const string HoeUp   = "sprites/player/tool/hoe-up";
        // Pickaxe
        public const string PickaxeDown = "sprites/player/tool/pickaxe-down";
        public const string PickaxeSide = "sprites/player/tool/pickaxe-side";
        public const string PickaxeUp   = "sprites/player/tool/pickaxe-up";
        // Pickup
        public const string PickupDown = "sprites/player/tool/pickup-down";
        public const string PickupSide = "sprites/player/tool/pickup-side";
        public const string PickupUp   = "sprites/player/tool/pickup-up";

        public static readonly string[] AllSheets = new[]
        {
            AxeDown, AxeSide, AxeUp,
            HoeDown, HoeSide, HoeUp,
            PickaxeDown, PickaxeSide, PickaxeUp,
            PickupDown, PickupSide, PickupUp,
        };
    }

    [CreateAssetMenu(fileName = "GameDataRegistry", menuName = "Rootborn/Common/Game Data Registry")]
    public sealed class GameDataRegistry : ScriptableObject
    {
        [SerializeField] private CropDefinition[] _crops = System.Array.Empty<CropDefinition>();
        [SerializeField] private ToolDefinition[] _tools = System.Array.Empty<ToolDefinition>();
        [SerializeField] private ResourceNodeDefinition[] _resources = System.Array.Empty<ResourceNodeDefinition>();
        [SerializeField] private KnowledgeNode[] _knowledge = System.Array.Empty<KnowledgeNode>();
        [SerializeField] private HeirTrait[] _traits = System.Array.Empty<HeirTrait>();
        [SerializeField] private StatusEffectDefinition[] _statuses = System.Array.Empty<StatusEffectDefinition>();
        [SerializeField] private GenerationProfile[] _generations = System.Array.Empty<GenerationProfile>();
        [SerializeField] private ItemDefinition[] _items = System.Array.Empty<ItemDefinition>();
        [SerializeField] private Sprite _groundSprite;
        [SerializeField] private Sprite _playerSprite;

        public CropDefinition[] Crops => _crops;
        public ToolDefinition[] Tools => _tools;
        public ResourceNodeDefinition[] Resources => _resources;
        public KnowledgeNode[] Knowledge => _knowledge;
        public HeirTrait[] Traits => _traits;
        public StatusEffectDefinition[] Statuses => _statuses;
        public GenerationProfile[] Generations => _generations;
        public ItemDefinition[] Items => _items;
        public Sprite GroundSprite => _groundSprite;
        public Sprite PlayerSprite => _playerSprite;
    }
}
