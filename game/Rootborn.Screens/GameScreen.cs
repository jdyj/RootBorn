using System;
using System.Collections.Generic;
using Rootborn.Core.Interaction;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens
{
    /// <summary>화면이 놓이는 층. 아래에서 위로: 월드(Host) → HUD → 패널 → 모달 → 알림.</summary>
    public enum ScreenLayer { Hud = 0, Panel = 1, Modal = 2, Notice = 3 }

    /// <summary>
    /// 화면 쌓기가 받는 키(장치 키가 아니라 Host가 옮겨 준 값). Interact는 Unity 상호작용 키(E·Space·Ctrl) 묶음이다.
    /// Space·Rotate(R)·AltLeft(A)·AltRight(D)는 미니게임 키(Unity MinigamePanel). Space는 같은 프레임의 Interact보다 먼저 온다.
    /// LocationLog(L)는 장소 기록(Unity 판에는 키가 없었다 — 사용자 결정 2026-11-12).
    /// Help는 단축키 안내(F1).
    /// </summary>
    public enum ScreenKey { Up, Down, Left, Right, Tab, Enter, Escape, Inventory, Status, Interact, Space, Rotate, AltLeft, AltRight, LocationLog, Help }

    /// <summary>
    /// "무엇이 바뀌었다" 알림의 이름. 게임 진행 쪽 이름은 GameSession.Changed가 알리는 GameChangeTopics 그대로이고,
    /// 화면 쪽에서만 생기는 것(언어, 맵 들어감, 전부)은 화면 쌓기가 낸다. 화면은 자기가 그리는 값과 관련된 알림만 보고 그 부분만 고친다.
    /// 새 이름은 상수 하나로 더한다.
    /// </summary>
    public static class ChangeTopics
    {
        public const string Student = GameChangeTopics.Student;
        public const string Inventory = GameChangeTopics.Inventory;
        public const string Quests = GameChangeTopics.Quests;
        public const string Dialogue = GameChangeTopics.Dialogue;
        public const string Day = GameChangeTopics.Day;
        public const string Career = GameChangeTopics.Career;
        public const string World = GameChangeTopics.World;
        public const string DailyEvents = GameChangeTopics.DailyEvents;
        public const string Discovery = GameChangeTopics.Discovery;
        public const string Furniture = GameChangeTopics.Furniture;
        public const string Objectives = GameChangeTopics.Objectives;
        public const string Language = "language";
        /// <summary>표시 모드(창 / 전체 화면)가 바뀌었다(DisplayModeSetting.Changed).</summary>
        public const string Display = "display";
        /// <summary>플레이어가 다른 맵에 들어섰다(ScreenStack.EnterMap).</summary>
        public const string Map = "map";
        /// <summary>무엇이 바뀌었는지 모를 때(이어 하기 직후 등). 화면은 전부 다시 받는다.</summary>
        public const string All = "all";
    }

    /// <summary>
    /// 화면이 쓰는 것. Session은 게임 한 판이 없을 때(견본, 슬롯 선택) null이다. 화면은 상태를 바꿀 때 Session의 명령을 부르고,
    /// 무엇이 바뀌었는지는 화면 쌓기가 Session.Changed를 받아 열린 화면들에 넘긴다(누가 부른 명령이든 같다).
    /// </summary>
    public sealed class ScreenServices
    {
        public ScreenServices(GameText text, GameSession session)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Session = session;
        }

        public GameText Text { get; }
        /// <summary>게임 한 판(없으면 null). 읽기는 알림을 받았을 때만 한다(매 프레임 읽지 않는다).</summary>
        public GameSession Session { get; }
        /// <summary>ScreenStack이 만들어질 때 채운다.</summary>
        public ScreenStack Screens { get; internal set; }
        /// <summary>
        /// 게임 정의(이름 키 등 읽기 전용 조회). 게임 한 판이 있으면 그 정의, 없으면(--world) Host가 넣어 준 정의. 둘 다 없으면 null.
        /// </summary>
        public Rootborn.Data.GameDefinitions Definitions { get => _definitions ?? Session?.Definitions; set => _definitions = value; }
        private Rootborn.Data.GameDefinitions _definitions;
        /// <summary>옷 칸·옷 id → 화면 이름 키. Core 옷 규칙에 이름 키가 없어 data/에서 읽는다. 없으면 빈 표.</summary>
        public DisplayNames Names { get; set; } = DisplayNames.Empty;
        /// <summary>슬롯과 무관한 설정(언어, 켬/끔). Host가 세이브 폴더의 설정 파일로 채운다. 없으면 저장하지 않는 기본값.</summary>
        public Settings.SettingsStore Settings { get; set; } = Rootborn.Screens.Settings.SettingsStore.InMemory();
        /// <summary>창의 표시 모드(Host가 채운다). 없으면(창 없이 도는 화면) 설정 화면의 표시 모드 단추는 누를 수 없다.</summary>
        public Settings.DisplayModeSetting Display { get; set; }
        /// <summary>옷장이 부르는 캐릭터 쪽(Host가 채운다). 없으면 옷장은 비어 있다.</summary>
        public Records.IWardrobeBackend Wardrobe { get; set; }
        /// <summary>장치 키 이름표(단축키 안내). Host가 Input/Devices.cs의 배정으로 채운다. 없으면 ScreenKey 이름으로 보인다.</summary>
        public Rootborn.Screens.KeyGuide.KeyGuideKeys KeyGuide { get; set; } = new Rootborn.Screens.KeyGuide.KeyGuideKeys();
        /// <summary>내 PC에서 연 방(Host가 채운다). 열려 있으면 설정 화면에 한 줄(열림, 주소, 사람 수, 닫기)이 보인다. 없으면 그 줄이 없다.</summary>
        public Start.IRoomHost RoomHost { get; set; }
    }

    /// <summary>화면을 연 까닭. 상호작용으로 열었으면 그 이벤트가 있다. Argument는 여는 쪽이 넘긴 값(예: 하루 이벤트 id).</summary>
    public readonly struct ScreenOpenContext
    {
        public ScreenOpenContext(InteractionEvent? interaction, object argument = null) { Interaction = interaction; Argument = argument; }
        public InteractionEvent? Interaction { get; }
        public object Argument { get; }
    }

    /// <summary>
    /// 화면 하나. 표시 모델로 UiNode 나무를 한 번만 만들고(Build), 열릴 때·닫힐 때·"무엇이 바뀌었다" 알림을 받을 때 값만 고친다.
    /// 버튼이 눌리면 Services.Session의 게임 명령을 부른다. 규칙 판단은 쓰지 않는다. 화면 인스턴스는 처음 열 때 만들고 닫아도 버리지 않는다.
    /// 문구는 T(key)로 받는다(쓴 키를 모아 두어 테스트가 두 언어에 다 있는지 본다).
    /// </summary>
    public abstract class GameScreen
    {
        private UiNode _root;
        private readonly HashSet<string> _textKeys = new HashSet<string>(StringComparer.Ordinal);

        protected GameScreen(ScreenServices services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        protected ScreenServices Services { get; }

        public string Id { get; internal set; }
        public ScreenLayer Layer { get; internal set; }
        /// <summary>떠 있는 동안 캐릭터가 걷지 못하는가(등록할 때 정한다).</summary>
        public bool BlocksMovement { get; internal set; }
        public bool IsOpen { get; internal set; }

        /// <summary>화면의 뿌리. 처음 부를 때 Build로 만든다.</summary>
        public UiNode Root => _root ??= Build() ?? throw new InvalidOperationException(GetType().Name + ".Build가 null을 돌려줬다");

        /// <summary>이 화면이 지금까지 쓴 문구 키.</summary>
        public IReadOnlyCollection<string> TextKeys => _textKeys;

        protected abstract UiNode Build();

        protected internal virtual void OnOpened(ScreenOpenContext context) { }
        protected internal virtual void OnClosed() { }
        /// <summary>게임 명령이 무언가를 바꿨다(이 화면이 열려 있을 때만 불린다). topic은 ChangeTopics.</summary>
        protected internal virtual void OnChanged(string topic) { }
        /// <summary>
        /// 시간이 흘렀다(열려 있을 때만, Host가 프레임마다 넘기는 dt). 실시간으로 도는 화면(미니게임)만 쓴다. 화면은 시계를 직접 읽지 않는다.
        /// 배치를 바꿨으면 true(화면 쌓기가 다시 배치한다).
        /// </summary>
        protected internal virtual bool OnTick(float seconds) => false;
        /// <summary>
        /// HUD 화면의 겉면 하나라도 위층 화면에 덮이면 이 화면의 겉면을 모두 숨기는가(한 묶음으로 읽히는 HUD: 체력·집중 칸).
        /// 기본은 겉면마다 따로 숨긴다(버튼 줄).
        /// </summary>
        protected internal virtual bool CoveredAsOne => false;

        /// <summary>화면이 가져갈 키. true면 다른 화면과 키 배정은 그 키를 보지 않는다.</summary>
        protected internal virtual bool OnKey(ScreenKey key, bool shift) => false;

        protected string T(string key)
        {
            _textKeys.Add(key);
            return Services.Text.Get(key);
        }

        protected string F(string key, params object[] args)
        {
            _textKeys.Add(key);
            return Services.Text.Format(key, args);
        }

        /// <summary>
        /// 데이터가 넘긴 값이 키면 문구로, 키가 아니면 그대로(Unity ObjectiveJournalSourceText.Shown과 같음). 조건 요약처럼
        /// 키일 수도 글일 수도 있는 값에만 쓴다. 키가 아니어도 빠진 키로 세지 않는다.
        /// </summary>
        protected string Shown(string keyOrText)
        {
            if (string.IsNullOrEmpty(keyOrText)) return string.Empty;
            return Services.Text.Has(keyOrText, Services.Text.Language) ? T(keyOrText) : keyOrText;
        }

        /// <summary>
        /// 정의의 화면 이름: 이름 키 → id로 만든 규칙 키(DataTextKeys.Name) 중 지금 언어에 있는 첫 문구, 없으면 데이터의 이름 글, 그것도 없으면 id.
        /// 화면이 id마다 분기하지 않고 문구를 game/localization에 더할 수 있게 한다.
        /// </summary>
        protected string NameOf(string displayNameKey, string id, string fallbackText = null)
        {
            string language = Services.Text.Language;
            if (!string.IsNullOrEmpty(displayNameKey) && Services.Text.Has(displayNameKey, language)) return T(displayNameKey);
            string ruleKey = DataTextKeys.Name(id);
            if (ruleKey != null && Services.Text.Has(ruleKey, language)) return T(ruleKey);
            if (!string.IsNullOrEmpty(fallbackText)) return fallbackText;
            return id ?? string.Empty;
        }

        protected void Close() => Services.Screens?.Close(this);
    }
}
