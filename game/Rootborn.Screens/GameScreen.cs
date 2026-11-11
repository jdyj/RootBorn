using System;
using System.Collections.Generic;
using Rootborn.Core.Interaction;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens
{
    /// <summary>화면이 놓이는 층. 아래에서 위로: 월드(Host) → HUD → 패널 → 모달 → 알림.</summary>
    public enum ScreenLayer { Hud = 0, Panel = 1, Modal = 2, Notice = 3 }

    /// <summary>화면 쌓기가 받는 키(장치 키가 아니라 Host가 옮겨 준 값). E는 Unity 상호작용 키(E·Space·Ctrl) 묶음이다.</summary>
    public enum ScreenKey { Up, Down, Left, Right, Tab, Enter, Escape, Inventory, Status, Interact }

    /// <summary>"무엇이 바뀌었다" 알림의 이름. 화면은 자기가 그리는 값과 관련된 알림만 보고 그 부분만 고친다. 새 이름은 상수 하나로 더한다.</summary>
    public static class ChangeTopics
    {
        public const string Student = "student";
        public const string Inventory = "inventory";
        public const string Quests = "quests";
        public const string Dialogue = "dialogue";
        public const string Day = "day";
        public const string Career = "career";
        public const string World = "world";
        public const string Language = "language";
        /// <summary>무엇이 바뀌었는지 모를 때(이어 하기 직후 등). 화면은 전부 다시 받는다.</summary>
        public const string All = "all";
    }

    /// <summary>
    /// 게임 명령을 부르는 얇은 감싸개. GameSession에는 변경 알림이 없어서, 명령을 부른 뒤 열린 화면들에 "바뀜"을 알린다.
    /// 화면은 상태를 바꿀 때 이것만 부른다. 읽기는 Session의 조회를 쓰되, 알림을 받았을 때만 읽는다(매 프레임 읽지 않는다).
    /// </summary>
    public sealed class SessionCommands
    {
        public SessionCommands(GameSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public GameSession Session { get; }

        public event Action<string> Changed;

        public T Run<T>(Func<GameSession, T> command, string topic)
        {
            T result = command(Session);
            Changed?.Invoke(topic ?? ChangeTopics.All);
            return result;
        }

        public void Run(Action<GameSession> command, string topic)
        {
            command(Session);
            Changed?.Invoke(topic ?? ChangeTopics.All);
        }
    }

    /// <summary>화면이 쓰는 것. Commands는 게임 한 판이 없을 때(견본 등) null이다.</summary>
    public sealed class ScreenServices
    {
        public ScreenServices(GameText text, SessionCommands commands)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Commands = commands;
        }

        public GameText Text { get; }
        public SessionCommands Commands { get; }
        /// <summary>ScreenStack이 만들어질 때 채운다.</summary>
        public ScreenStack Screens { get; internal set; }
    }

    /// <summary>화면을 연 까닭. 상호작용으로 열었으면 그 이벤트가 있다.</summary>
    public readonly struct ScreenOpenContext
    {
        public ScreenOpenContext(InteractionEvent? interaction) { Interaction = interaction; }
        public InteractionEvent? Interaction { get; }
    }

    /// <summary>
    /// 화면 하나. 표시 모델로 UiNode 나무를 한 번만 만들고(Build), 열릴 때·닫힐 때·"무엇이 바뀌었다" 알림을 받을 때 값만 고친다.
    /// 버튼이 눌리면 Services.Commands로 게임 명령을 부른다. 규칙 판단은 쓰지 않는다. 화면 인스턴스는 처음 열 때 만들고 닫아도 버리지 않는다.
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
        /// <summary>화면이 가져갈 키. true면 다른 화면과 키 배정은 그 키를 보지 않는다.</summary>
        protected internal virtual bool OnKey(ScreenKey key, bool shift) => false;

        protected string T(string key)
        {
            _textKeys.Add(key);
            return Services.Text.Get(key);
        }

        protected void Close() => Services.Screens?.Close(this);
    }
}
