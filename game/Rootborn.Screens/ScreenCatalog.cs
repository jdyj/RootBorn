using Rootborn.Screens.AdultFacility;
using Rootborn.Screens.Career;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Day;
using Rootborn.Screens.Discoveries;
using Rootborn.Screens.Minigames;
using Rootborn.Screens.Objectives;
using Rootborn.Screens.Quests;
using Rootborn.Screens.Inventory;
using Rootborn.Screens.KeyGuide;
using Rootborn.Screens.Records;
using Rootborn.Screens.Settings;
using Rootborn.Screens.Status;
using Rootborn.Screens.Start;

namespace Rootborn.Screens
{
    /// <summary>
    /// 화면 등록 표. 새 화면은 여기에 한 줄을 더한다: 화면 id, 층, 이동을 막는지(Unity 판과 같은 값, 설계 3.1절), 만드는 법, 여는 키·상호작용 종류.
    /// 키 배정(Unity ModernUiPanelInputRouter): I 가방, C 상태, Esc 설정, Tab 목표 수첩. 상호작용은 InteractionEvent.Kind(엔티티 id가 아니다).
    /// HUD 층 화면은 월드를 띄울 때 모두 열린다(ScreenStack.OpenHud).
    /// </summary>
    public static class ScreenCatalog
    {
        public const string Settings = "settings";
        public const string Inventory = "inventory";
        public const string Status = "status";
        public const string HudClock = "hud.clock";
        public const string HudVitals = "hud.vitals";
        public const string HudTracked = "hud.tracked-objective";
        public const string HudButtons = "hud.buttons";
        public const string HudPrompt = "hud.interaction-prompt";
        public const string Dialogue = "dialogue";
        public const string DailyEvent = "daily-event";
        public const string Minigame = "minigame";
        public const string ObjectiveJournal = "objective-journal";
        public const string QuestChainLog = "quest-chain-log";
        public const string Exploration = "exploration";
        // 시작 화면(슬롯 선택, 캐릭터 만들기, 슬롯 지우기 확인). 게임 한 판이 없을 때 Host가 슬롯 선택을 연다.
        public const string SaveSlotSelect = "save-slot-select";
        public const string CharacterCreator = "character-creator";
        public const string DeleteSlot = "delete-slot";
        /// <summary>손상된 슬롯 알림(모달). 고른 슬롯의 시작이 SaveSlotDamagedException으로 거절되면 Host가 슬롯 선택 위에 연다.</summary>
        public const string SaveSlotDamaged = "save-slot-damaged";

        // HUD 버튼이 여는 화면. 아직 옮기지 않은 화면은 id만 정해 두고, 그 화면을 등록하면 버튼이 저절로 이어진다.
        public const string CareerCandidates = "career-candidates";
        public const string CareerInterest = "career-interest";
        public const string WorldStateLog = "world-state-log";
        public const string Encyclopedia = "encyclopedia";
        public const string Wardrobe = "wardrobe";
        public const string FurniturePlacement = "furniture-placement";
        public const string DailyPlan = "daily-plan";
        public const string Location = "location";
        public const string DayResult = "day-result";
        /// <summary>장소 기록(Unity LocationStateLogPanel). L 키와 HUD 버튼(오른쪽 위 열, 옷장 아래)이 열고 닫는다(사용자 결정 2026-11-12).</summary>
        public const string LocationStateLog = "location-state-log";
        /// <summary>성인 시설 관리(Unity FacilityManagementPanel). 시설 앞 상호작용으로 열린다. 이동을 막는다.</summary>
        public const string Facility = "facility";
        /// <summary>발견 알림(알림 층). 발견 지점 상호작용으로 열려 GameSession.Discover를 부르고 결과를 보인다. 이동을 막지 않는다.</summary>
        public const string DiscoveryNotice = "discovery-notice";
        /// <summary>단축키 안내(F1, 설정의 단축키 단추, 시작 화면에서도 F1). 읽는 모달이라 이동을 막는다. 줄은 이 등록 표의 키 배정에서 만든다.</summary>
        public const string KeyGuide = "key-guide";

        public static void Register(ScreenRegistry r)
        {
            r.Add(Settings, ScreenLayer.Panel, blocksMovement: false, s => new SettingsScreen(s)).OpenedBy(ScreenKey.Escape);
            r.Add(Inventory, ScreenLayer.Panel, blocksMovement: false, s => new InventoryScreen(s)).OpenedBy(ScreenKey.Inventory);
            r.Add(Status, ScreenLayer.Panel, blocksMovement: false, s => new StatusScreen(s)).OpenedBy(ScreenKey.Status);
            r.Add(HudClock, ScreenLayer.Hud, blocksMovement: false, s => new ClockHudScreen(s));
            r.Add(HudVitals, ScreenLayer.Hud, blocksMovement: false, s => new VitalsHudScreen(s));
            r.Add(HudTracked, ScreenLayer.Hud, blocksMovement: false, s => new TrackedObjectiveHudScreen(s));
            r.Add(HudButtons, ScreenLayer.Hud, blocksMovement: false, s => new HudButtonsScreen(s));
            r.Add(HudPrompt, ScreenLayer.Hud, blocksMovement: false, s => new InteractionPromptScreen(s));
            r.Add(Dialogue, ScreenLayer.Modal, blocksMovement: false, s => new DialogueScreen(s)).OpenedByInteraction("npc");
            r.Add(DailyEvent, ScreenLayer.Modal, blocksMovement: true, s => new DailyEventScreen(s));
            // 진로 묶음: 장래희망·진로 후보는 HUD 버튼(화면 id)이 열고, 탐색 선택은 탐색 지점 상호작용 종류로 열린다. 셋 다 Unity 판에서 이동을 막는다.
            r.Add(CareerInterest, ScreenLayer.Modal, blocksMovement: true, s => new CareerInterestScreen(s));
            r.Add(CareerCandidates, ScreenLayer.Modal, blocksMovement: true, s => new CareerCandidateScreen(s));
            r.Add(Exploration, ScreenLayer.Modal, blocksMovement: true, s => new ExplorationScreen(s)).OpenedByInteraction("exploration");
            // 하루 이벤트는 장소 지점 상호작용 뒤에 연다(Unity DailyEventRuntimeInstaller).
            r.AddListener(s => new DailyEventTrigger(s, DailyEvent, "location-activity"));
            r.Add(DailyPlan, ScreenLayer.Panel, blocksMovement: false, s => new DailyPlanScreen(s)).OpenedByInteraction(DailyPlanScreen.InteractionKind);
            r.Add(Location, ScreenLayer.Modal, blocksMovement: true, s => new LocationScreen(s)).OpenedByInteraction(LocationScreen.InteractionKind);
            r.Add(DayResult, ScreenLayer.Modal, blocksMovement: true, s => new DayResultScreen(s)).OpenedByInteraction(DayResultScreen.InteractionKind);
            // 미니게임은 예약이 생기면 연다(Unity MinigamePresentationBridge). 일터·연습 지점은 상호작용 종류로 등록한다.
            r.Add(Minigame, ScreenLayer.Modal, blocksMovement: true, s => new MinigameScreen(s));
            r.AddListener(s => new MinigameTrigger(s, Minigame));
            // 목표 수첩(Tab, 큰 패널, 이동을 막지 않음). 목표성 UI는 수첩이 소유한다(objective-journal-ui 규칙).
            r.Add(ObjectiveJournal, ScreenLayer.Panel, blocksMovement: false, s => new ObjectiveJournalScreen(s)).OpenedBy(ScreenKey.Tab);
            // 퀘스트 체인 기록: Unity 판에도 여는 키·상호작용이 없었다(등록만, Open(id)로만 연다).
            r.Add(QuestChainLog, ScreenLayer.Panel, blocksMovement: false, s => new QuestChainLogScreen(s));
            r.AddListener(s => new MinigameWorkPoints(s, new[] { "part-time-work" }, new[] { "career-practice" }));
            // 시작 화면은 월드가 없을 때 뜬다. 캐릭터가 없으니 방향 키가 버튼 사이를 옮기도록 이동을 막는 화면으로 둔다.
            r.Add(SaveSlotSelect, ScreenLayer.Panel, blocksMovement: true, s => new SaveSlotScreen(s)).WithoutWorld();
            r.Add(CharacterCreator, ScreenLayer.Panel, blocksMovement: true, s => new CharacterCreatorScreen(s)).WithoutWorld();
            r.Add(DeleteSlot, ScreenLayer.Modal, blocksMovement: true, s => new DeleteSlotScreen(s)).WithoutWorld();
            r.Add(SaveSlotDamaged, ScreenLayer.Modal, blocksMovement: true, s => new SaveSlotDamagedScreen(s)).WithoutWorld();
            // 가구 배치: 집 HUD 버튼(가구 배치 켬/끔)이 연다. 이동을 막지 않는다(Unity 판 패널은 입력을 잠그지 않았다).
            r.Add(FurniturePlacement, ScreenLayer.Panel, blocksMovement: false, s => new FurniturePlacementScreen(s));
            r.Add(Encyclopedia, ScreenLayer.Panel, blocksMovement: true, s => new EncyclopediaScreen(s));
            r.Add(Wardrobe, ScreenLayer.Panel, blocksMovement: true, s => new WardrobeScreen(s));
            r.Add(WorldStateLog, ScreenLayer.Panel, blocksMovement: true, s => new WorldStateLogScreen(s));
            r.Add(LocationStateLog, ScreenLayer.Panel, blocksMovement: false, s => new LocationStateLogScreen(s)).OpenedBy(ScreenKey.LocationLog);
            r.Add(Facility, ScreenLayer.Modal, blocksMovement: true, s => new FacilityScreen(s)).OpenedByInteraction(FacilityScreen.InteractionKind);
            r.Add(DiscoveryNotice, ScreenLayer.Notice, blocksMovement: false, s => new DiscoveryNoticeScreen(s)).OpenedByInteraction(DiscoveryNoticeScreen.InteractionKind);
            r.Add(KeyGuide, ScreenLayer.Modal, blocksMovement: true, s => new KeyGuideScreen(s)).OpenedBy(ScreenKey.Help).KeyWorksOnStartScreens();
        }

        /// <summary>표를 채운 새 등록 표.</summary>
        public static ScreenRegistry Create()
        {
            var registry = new ScreenRegistry();
            Register(registry);
            return registry;
        }
    }
}
