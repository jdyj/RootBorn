using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Day;
using Rootborn.Screens.Minigames;
using Rootborn.Screens.Objectives;
using Rootborn.Screens.Quests;
using Rootborn.Screens.Settings;

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

        public static void Register(ScreenRegistry r)
        {
            r.Add(Settings, ScreenLayer.Panel, blocksMovement: false, s => new SettingsScreen(s)).OpenedBy(ScreenKey.Escape);
            r.Add(HudClock, ScreenLayer.Hud, blocksMovement: false, s => new ClockHudScreen(s));
            r.Add(HudVitals, ScreenLayer.Hud, blocksMovement: false, s => new VitalsHudScreen(s));
            r.Add(HudTracked, ScreenLayer.Hud, blocksMovement: false, s => new TrackedObjectiveHudScreen(s));
            r.Add(HudButtons, ScreenLayer.Hud, blocksMovement: false, s => new HudButtonsScreen(s));
            r.Add(HudPrompt, ScreenLayer.Hud, blocksMovement: false, s => new InteractionPromptScreen(s));
            r.Add(Dialogue, ScreenLayer.Modal, blocksMovement: false, s => new DialogueScreen(s)).OpenedByInteraction("npc");
            r.Add(DailyEvent, ScreenLayer.Modal, blocksMovement: true, s => new DailyEventScreen(s));
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
