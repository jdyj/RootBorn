using System;
using System.Collections.Generic;
using Rootborn.Game.Crops;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Family;
using Rootborn.Game.Generation;
using Rootborn.Game.Heir;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Quests;
using Rootborn.Game.Resources;
using Rootborn.Game.Status;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Tiles;
using Rootborn.Game.Tools;
using Rootborn.Game.WorldGeneration;
using UnityEngine;

namespace Rootborn.Game.Common
{
    public enum ItemCategory { Resource, Tool, Misc, Seed }

    public sealed class Inventory
    {
        public const int MaxSlots = 32;

        public sealed class Slot
        {
            public ItemDefinition Item;
            public int Count;
        }

        private readonly List<Slot> _slots = new List<Slot>(MaxSlots);

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

            while (remaining > 0 && _slots.Count < MaxSlots)
            {
                int put = Mathf.Min(max, remaining);
                _slots.Add(new Slot { Item = item, Count = put });
                remaining -= put;
            }

            OnChanged?.Invoke();
        }

        public bool CanAdd(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return false;
            int remaining = count;
            int max = item.MaxStack;
            int occupiedSlots = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Item != null && slot.Count > 0) occupiedSlots++;
                if (slot.Item != item) continue;
                int room = max - slot.Count;
                if (room <= 0) continue;
                int put = Mathf.Min(room, remaining);
                remaining -= put;
                if (remaining <= 0) return true;
            }

            int freeSlots = MaxSlots - occupiedSlots;
            while (remaining > 0 && freeSlots > 0)
            {
                remaining -= Mathf.Min(max, remaining);
                freeSlots--;
            }

            return remaining <= 0;
        }

        public bool CanAddAll(IReadOnlyList<InventoryGrant> grants)
        {
            if (grants == null) return true;
            var clone = new Inventory();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Item != null && slot.Count > 0) clone.Add(slot.Item, slot.Count);
            }

            for (int i = 0; i < grants.Count; i++)
            {
                var grant = grants[i];
                if (!clone.CanAdd(grant.Item, grant.Count)) return false;
                clone.Add(grant.Item, grant.Count);
            }

            return true;
        }

        public bool TryMoveSlot(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || targetIndex < 0) return false;
            EnsureSlotCount(Mathf.Max(sourceIndex, targetIndex) + 1);
            var source = _slots[sourceIndex];
            if (source.Item == null || source.Count <= 0) return false;
            if (sourceIndex == targetIndex) return true;
            var target = _slots[targetIndex];
            if (target.Item != null && target.Count > 0)
            {
                if (target.Item != source.Item) return false;
                int room = source.Item.MaxStack - target.Count;
                if (room <= 0) return false;
                int moved = Mathf.Min(room, source.Count);
                target.Count += moved;
                source.Count -= moved;
                if (source.Count <= 0)
                {
                    source.Item = null;
                    source.Count = 0;
                }

                OnChanged?.Invoke();
                return true;
            }

            target.Item = source.Item;
            target.Count = source.Count;
            source.Item = null;
            source.Count = 0;
            OnChanged?.Invoke();
            return true;
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
            for (int i = 0; i < _slots.Count; i++) if (_slots[i].Item == item) sum += _slots[i].Count;
            return sum;
        }

        public void Clear()
        {
            if (_slots.Count == 0) return;
            _slots.Clear();
            OnChanged?.Invoke();
        }

        private void EnsureSlotCount(int count)
        {
            while (_slots.Count < count && _slots.Count < MaxSlots) _slots.Add(new Slot());
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
        public static readonly string[] BookFlipFrames = new[] { BookPage1, BookPage2, BookPage3, BookPage4, BookPage5, BookPage6, BookPage7, BookPage8, BookPage9 };
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
        public static readonly string[] AllSingleSprites = new[] { BookPage1, BookPage2, BookPage3, BookPage4, BookPage5, BookPage6, BookPage7, BookPage8, BookPage9, BookSpine, HudPanel, HintPanel, SmallButton, ItemSlot, EquipmentSlot, ItemsRibbon, DescriptionRibbon, EquipmentRibbon, CutterShort, CutterLong, InscriptionPlus, Character };
        public static readonly (string sheetAddress, string[] subNames)[] AllSheets = new[] { (BookmarkSheet, new[] { SubBookmark0, SubBookmark1, SubBookmark2, SubBookmark3, SubBookmark4 }) };
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
        public static readonly string[] AllSheets = new[] { AxeDown, AxeSide, AxeUp, HoeDown, HoeSide, HoeUp, PickaxeDown, PickaxeSide, PickaxeUp, PickupDown, PickupSide, PickupUp };
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
        [SerializeField] private QuestDefinition[] _quests = Array.Empty<QuestDefinition>();
        [SerializeField] private QuestObjectiveBase[] _questObjectives = Array.Empty<QuestObjectiveBase>();
        [SerializeField] private QuestRewardBase[] _questRewards = Array.Empty<QuestRewardBase>();
        [SerializeField] private QuestCompletionEffectBase[] _questCompletionEffects = Array.Empty<QuestCompletionEffectBase>();
        [SerializeField] private QuestConditionBase[] _questConditions = Array.Empty<QuestConditionBase>();
        [SerializeField] private NpcDefinition[] _npcs = Array.Empty<NpcDefinition>();
        [SerializeField] private DialogueDefinition[] _dialogues = Array.Empty<DialogueDefinition>();
        [SerializeField] private StoryFlagDefinition[] _storyFlags = Array.Empty<StoryFlagDefinition>();
        [SerializeField] private TutorialStageDefinition[] _tutorialStages = Array.Empty<TutorialStageDefinition>();
        [SerializeField] private TutorialStageDefinition _defaultTutorialStage;
        [SerializeField] private CharacterPartDefinition[] _characterParts = Array.Empty<CharacterPartDefinition>();
        [SerializeField] private CharacterPartAnimationClipDefinition[] _characterPartAnimationClips = Array.Empty<CharacterPartAnimationClipDefinition>();
        [SerializeField] private FishingAnimationDefinition[] _fishingAnimations = Array.Empty<FishingAnimationDefinition>();
        [SerializeField] private LifeActivityDefinition[] _lifeActivities = Array.Empty<LifeActivityDefinition>();
        [SerializeField] private LifeChoiceDefinition[] _studentLifeChoices = Array.Empty<LifeChoiceDefinition>();
        [SerializeField] private TraitDefinition[] _studentLifeTraits = Array.Empty<TraitDefinition>();
        [SerializeField] private SkillDefinition[] _studentLifeSkills = Array.Empty<SkillDefinition>();
        [SerializeField] private CareerDefinition[] _careers = Array.Empty<CareerDefinition>();
        [SerializeField] private CareerPracticeDefinition[] _careerPractices = Array.Empty<CareerPracticeDefinition>();
        [SerializeField] private PracticeStepDefinition[] _practiceSteps = Array.Empty<PracticeStepDefinition>();
        [SerializeField] private DayEndRuleDefinition[] _dayEndRules = Array.Empty<DayEndRuleDefinition>();
        [SerializeField] private DayEndRuleDefinition _defaultDayEndRule;
        [SerializeField] private RelationshipDefinition[] _relationships = Array.Empty<RelationshipDefinition>();
        [SerializeField] private StatusDefinition[] _studentConditionStatuses = Array.Empty<StatusDefinition>();
        [SerializeField] private OutsideSchoolActivityCategoryDefinition[] _outsideSchoolActivityCategories = Array.Empty<OutsideSchoolActivityCategoryDefinition>();
        [SerializeField] private OutsideSchoolActivityDefinition[] _outsideSchoolActivities = Array.Empty<OutsideSchoolActivityDefinition>();
        [SerializeField] private TownHelpActionDefinition[] _townHelpActions = Array.Empty<TownHelpActionDefinition>();
        [SerializeField] private WorkplaceDefinition[] _workplaces = Array.Empty<WorkplaceDefinition>();
        [SerializeField] private PartTimeWorkDefinition[] _partTimeWorks = Array.Empty<PartTimeWorkDefinition>();
        [SerializeField] private LocationDefinition[] _locations = Array.Empty<LocationDefinition>();
        [SerializeField] private DiscoveryDefinition[] _discoveries = Array.Empty<DiscoveryDefinition>();
        [SerializeField] private PlaceableTileDefinition[] _placeableTiles = Array.Empty<PlaceableTileDefinition>();
        [SerializeField] private MilestoneDefinition[] _milestones = Array.Empty<MilestoneDefinition>();
        [SerializeField] private MilestoneObjectiveBase[] _milestoneObjectives = Array.Empty<MilestoneObjectiveBase>();
        [SerializeField] private MilestoneRouteDefinition[] _milestoneRoutes = Array.Empty<MilestoneRouteDefinition>();
        [SerializeField] private MilestoneRewardBase[] _milestoneRewards = Array.Empty<MilestoneRewardBase>();
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
        public QuestDefinition[] Quests => _quests;
        public QuestObjectiveBase[] QuestObjectives => _questObjectives;
        public QuestRewardBase[] QuestRewards => _questRewards;
        public QuestCompletionEffectBase[] QuestCompletionEffects => _questCompletionEffects;
        public QuestConditionBase[] QuestConditions => _questConditions;
        public NpcDefinition[] Npcs => _npcs;
        public DialogueDefinition[] Dialogues => _dialogues;
        public StoryFlagDefinition[] StoryFlags => _storyFlags;
        public TutorialStageDefinition[] TutorialStages => _tutorialStages;
        public TutorialStageDefinition DefaultTutorialStage => _defaultTutorialStage;
        public CharacterPartDefinition[] CharacterParts => _characterParts;
        public CharacterPartAnimationClipDefinition[] CharacterPartAnimationClips => _characterPartAnimationClips;
        public FishingAnimationDefinition[] FishingAnimations => _fishingAnimations;
        public LifeActivityDefinition[] LifeActivities => _lifeActivities;
        public LifeChoiceDefinition[] StudentLifeChoices => _studentLifeChoices;
        public TraitDefinition[] StudentLifeTraits => _studentLifeTraits;
        public SkillDefinition[] StudentLifeSkills => _studentLifeSkills;
        public CareerDefinition[] Careers => _careers;
        public CareerPracticeDefinition[] CareerPractices => _careerPractices;
        public PracticeStepDefinition[] PracticeSteps => _practiceSteps;
        public DayEndRuleDefinition[] DayEndRules => _dayEndRules;
        public DayEndRuleDefinition DefaultDayEndRule => _defaultDayEndRule;
        public RelationshipDefinition[] Relationships => _relationships;
        public StatusDefinition[] StudentConditionStatuses => _studentConditionStatuses;
        public OutsideSchoolActivityCategoryDefinition[] OutsideSchoolActivityCategories => _outsideSchoolActivityCategories;
        public OutsideSchoolActivityDefinition[] OutsideSchoolActivities => _outsideSchoolActivities;
        public TownHelpActionDefinition[] TownHelpActions => _townHelpActions;
        public WorkplaceDefinition[] Workplaces => _workplaces;
        public PartTimeWorkDefinition[] PartTimeWorks => _partTimeWorks;
        public LocationDefinition[] Locations => _locations;
        public DiscoveryDefinition[] Discoveries => _discoveries;
        public PlaceableTileDefinition[] PlaceableTiles => _placeableTiles;
        public MilestoneDefinition[] Milestones => _milestones;
        public MilestoneObjectiveBase[] MilestoneObjectives => _milestoneObjectives;
        public MilestoneRouteDefinition[] MilestoneRoutes => _milestoneRoutes;
        public MilestoneRewardBase[] MilestoneRewards => _milestoneRewards;
        public Sprite GroundSprite => _groundSprite;
        public Sprite PlayerSprite => _playerSprite;
        public TerrainGenerationDefinition DefaultFarmTerrainGeneration => _defaultFarmTerrainGeneration;
    }
}