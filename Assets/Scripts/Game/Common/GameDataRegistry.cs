using System;
using System.Collections.Generic;
using Rootborn.Game.Crops;
using Rootborn.Game.Generation;
using Rootborn.Game.Heir;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Resources;
using Rootborn.Game.Status;
using Rootborn.Game.Tools;
using Rootborn.Game.WorldGeneration;
using UnityEngine;

namespace Rootborn.Game.Common
{
    public enum ItemCategory { Resource, Tool, Misc, Seed }

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
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (slot.Item != item) continue;
                int room = max - slot.Count;
                if (room <= 0) continue;
                int put = Mathf.Min(room, remaining);
                slot.Count += put;
                remaining -= put;
            }

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
                var slot = _slots[i];
                if (slot.Item != item) continue;
                int take = Mathf.Min(slot.Count, remaining);
                slot.Count -= take;
                remaining -= take;
                if (slot.Count == 0) _slots.RemoveAt(i);
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

    public static class UISpriteAddresses
    {
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

        public const string BookLeftPage = BookPage1;
        public const string HudPanel = "sprites/ui/panel/hud";
        public const string HintPanel = "sprites/ui/panel/hint";
        public const string SmallButton = "sprites/ui/button/small";
        public const string ItemSlot = "sprites/ui/slot/item";
        public const string EquipmentSlot = "sprites/ui/slot/equipment";
        public const string ItemsRibbon = "sprites/ui/ribbon/items";
        public const string DescriptionRibbon = "sprites/ui/ribbon/description";
        public const string EquipmentRibbon = "sprites/ui/ribbon/equipment";
        public const string CutterShort = "sprites/ui/decor/cutter-short";
        public const string CutterLong = "sprites/ui/decor/cutter-long";
        public const string InscriptionPlus = "sprites/ui/decor/inscription-plus";
        public const string BookmarkSheet = "sprites/ui/sheet/bookmark";
        public const string Character = "sprites/ui/character";
        public const string SubBookmark0 = "Bookmark_0";
        public const string SubBookmark1 = "Bookmark_1";
        public const string SubBookmark2 = "Bookmark_2";
        public const string SubBookmark3 = "Bookmark_3";
        public const string SubBookmark4 = "Bookmark_4";
        public const string LabelPreLoad = "PreLoad";

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

        public static readonly (string sheetAddress, string[] subNames)[] AllSheets = new[]
        {
            (BookmarkSheet, new[] { SubBookmark0, SubBookmark1, SubBookmark2, SubBookmark3, SubBookmark4 }),
        };
    }

    public static class PlayerToolSpriteAddresses
    {
        public const string AxeDown = "sprites/player/tool/axe-down";
        public const string AxeSide = "sprites/player/tool/axe-side";
        public const string AxeUp = "sprites/player/tool/axe-up";
        public const string HoeDown = "sprites/player/tool/hoe-down";
        public const string HoeSide = "sprites/player/tool/hoe-side";
        public const string HoeUp = "sprites/player/tool/hoe-up";
        public const string PickaxeDown = "sprites/player/tool/pickaxe-down";
        public const string PickaxeSide = "sprites/player/tool/pickaxe-side";
        public const string PickaxeUp = "sprites/player/tool/pickaxe-up";
        public const string PickupDown = "sprites/player/tool/pickup-down";
        public const string PickupSide = "sprites/player/tool/pickup-side";
        public const string PickupUp = "sprites/player/tool/pickup-up";

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
        [SerializeField] private CropDefinition[] _crops = Array.Empty<CropDefinition>();
        [SerializeField] private ToolDefinition[] _tools = Array.Empty<ToolDefinition>();
        [SerializeField] private ResourceNodeDefinition[] _resources = Array.Empty<ResourceNodeDefinition>();
        [SerializeField] private KnowledgeNode[] _knowledge = Array.Empty<KnowledgeNode>();
        [SerializeField] private HeirTrait[] _traits = Array.Empty<HeirTrait>();
        [SerializeField] private StatusEffectDefinition[] _statuses = Array.Empty<StatusEffectDefinition>();
        [SerializeField] private GenerationProfile[] _generations = Array.Empty<GenerationProfile>();
        [SerializeField] private ItemDefinition[] _items = Array.Empty<ItemDefinition>();
        [SerializeField] private Sprite _groundSprite;
        [SerializeField] private Sprite _playerSprite;
        [SerializeField] private TerrainGenerationDefinition _defaultFarmTerrainGeneration;

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
        public TerrainGenerationDefinition DefaultFarmTerrainGeneration => _defaultFarmTerrainGeneration;
    }
}
