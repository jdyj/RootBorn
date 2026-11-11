using System;
using System.Collections.Generic;
using Rootborn.Core.Interaction;
using Rootborn.Ui;

namespace Rootborn.Screens
{
    /// <summary>
    /// 지금 떠 있는 화면들. 층(HUD → 패널 → 모달 → 알림)마다 노드를 두고 화면 뿌리를 그 아래에 단다. UiTree 하나가 포인터 판정과
    /// 그리기를 맡으므로 위층 화면이 포인터를 먼저 받는다. 모달이 떠 있으면 그 아래 층은 포인터를 받지 않는다.
    /// 큰 패널은 한 번에 하나(Unity ModernUiPanelInputRouter의 배타 규칙), 모달·알림은 위로 쌓인다.
    /// 이동을 막는지는 화면마다 등록할 때 정하고(Unity UiGameplayInputLock 대신), Host는 MovementBlocked로 묻는다.
    /// 키 처리 순서: 글자 칸 포커스 → 열린 화면이 가져가는 키(위에서부터) → 키 배정 표 → 맨 위 화면 안에서 포커스 이동.
    /// 이동을 막지 않는 화면이 맨 위면 방향 키만으로는 가져가지 않는다(캐릭터가 걷는다). 그런 화면에서 포커스 이동은 Shift+방향 키,
    /// 실행은 Enter다. Tab은 목표 수첩을 여는 키라 포커스 이동에 쓰지 않는다(설계 3.1절, 2026-11-12).
    /// Host는 월드에서 일어난 일(맵 들어감, 앞의 상호작용 대상, 상호작용)을 EnterMap·SetInteractionTarget·Interaction으로 넘긴다.
    /// </summary>
    public sealed class ScreenStack
    {
        private readonly ScreenRegistry _registry;
        private readonly ScreenServices _services;
        private readonly Dictionary<string, GameScreen> _screens = new Dictionary<string, GameScreen>(StringComparer.Ordinal);
        private readonly UiNode[] _layers = new UiNode[4];
        private readonly UiNode _modalBlocker;
        private readonly List<GameScreen> _open = new List<GameScreen>(8);
        private readonly List<GameScreen> _snapshot = new List<GameScreen>(8);
        private float _width = 1920, _height = 1080;

        public ScreenStack(ScreenRegistry registry, ScreenServices services)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _services.Screens = this;
            for (int i = 0; i < _layers.Length; i++)
                _layers[i] = Tree.Root.Add(new UiNode { Id = "layer-" + ((ScreenLayer)i).ToString().ToLowerInvariant() });
            _modalBlocker = _layers[(int)ScreenLayer.Modal].Add(new UiNode { Id = "modal-blocker", Interactive = true, Visible = false });
            if (_services.Commands != null) _services.Commands.Changed += NotifyChanged;
            _services.Text.LanguageChanged += () => NotifyChanged(ChangeTopics.Language);
            foreach (Action<ScreenServices> attach in registry.Listeners) attach(_services);
        }

        public UiTree Tree { get; } = new UiTree();

        /// <summary>열린 화면, 아래에서 위 순서(층 순서, 같은 층은 연 순서).</summary>
        public IReadOnlyList<GameScreen> OpenScreens => _open;

        public GameScreen Top => _open.Count == 0 ? null : _open[_open.Count - 1];

        /// <summary>
        /// 포커스 이동과 실행 키를 받는 화면: 맨 위의 패널·모달. HUD와 알림은 키보드 포커스를 받지 않는다.
        /// </summary>
        public GameScreen FocusScreen
        {
            get
            {
                for (int i = _open.Count - 1; i >= 0; i--)
                    if (_open[i].Layer == ScreenLayer.Panel || _open[i].Layer == ScreenLayer.Modal) return _open[i];
                return null;
            }
        }

        public bool TextInputFocused => Tree.Focused is UiTextField field && field.IsActive;

        /// <summary>캐릭터가 걷지 못한다: 이동을 막는 화면이 떠 있거나 글자를 치고 있다.</summary>
        public bool MovementBlocked => ScreenBlocksMovement || TextInputFocused;

        private bool ScreenBlocksMovement
        {
            get
            {
                for (int i = 0; i < _open.Count; i++) if (_open[i].BlocksMovement) return true;
                return false;
            }
        }

        public bool IsOpen(string id) => _screens.TryGetValue(id, out GameScreen s) && s.IsOpen;

        /// <summary>등록 표에 그 화면이 있다(아직 옮기지 않은 화면을 여는 버튼은 이것으로 본다).</summary>
        public bool Has(string id) => _registry.Has(id);

        /// <summary>
        /// 캐릭터가 걷지 못한다: MovementBlocked이거나, Shift를 누른 채 포커스를 받는 화면이 떠 있다(Shift+방향 키는 버튼 사이를 옮긴다).
        /// </summary>
        public bool MovementBlockedWhile(bool shiftHeld) => MovementBlocked || (shiftHeld && FocusScreen != null);

        /// <summary>만들어진 화면(열린 적이 있으면). 없으면 null.</summary>
        public GameScreen Find(string id) => _screens.TryGetValue(id, out GameScreen s) ? s : null;

        public event Action<GameScreen> Opened;
        public event Action<GameScreen> Closed;
        /// <summary>월드의 상호작용이 있었다. 두 번째 값은 그 종류로 열린 화면(없으면 null).</summary>
        public event Action<InteractionEvent, GameScreen> Interacted;
        /// <summary>앞의 상호작용 대상이 바뀌거나 화면에서 움직였다.</summary>
        public event Action TargetChanged;

        /// <summary>지금 맵 id(Host가 EnterMap으로 넘긴다). 모르면 빈 글자.</summary>
        public string MapId { get; private set; } = string.Empty;

        /// <summary>지금 상호작용 키를 누르면 반응할 대상(없으면 null).</summary>
        public InteractionTargetView? Target { get; private set; }

        /// <summary>지금 크기로 다시 배치한다(화면이 노드를 새로 단 직후).</summary>
        internal void Relayout() => Tree.Layout(_width, _height);

        public void Layout(float width, float height)
        {
            _width = width; _height = height;
            Tree.Layout(width, height);
        }

        private GameScreen Get(string id)
        {
            if (_screens.TryGetValue(id, out GameScreen screen)) return screen;
            if (!_registry.TryGet(id, out ScreenEntry entry)) throw new InvalidOperationException("등록되지 않은 화면: " + id);
            screen = entry.Create(_services) ?? throw new InvalidOperationException("화면을 만들지 못했다: " + id);
            screen.Id = entry.Id;
            screen.Layer = entry.Layer;
            screen.BlocksMovement = entry.BlocksMovement;
            UiNode root = screen.Root;
            root.Visible = false;
            _layers[(int)entry.Layer].Add(root);
            _screens[id] = screen;
            return screen;
        }

        /// <summary>등록된 HUD 층 화면을 모두 연다(Host가 월드를 띄울 때 한 번).</summary>
        public void OpenHud()
        {
            foreach (ScreenEntry entry in _registry.Screens)
                if (entry.Layer == ScreenLayer.Hud && !IsOpen(entry.Id)) Open(entry.Id);
        }

        /// <summary>화면을 연다. 이미 열려 있으면 그 층의 맨 위로 올리고 다시 알린다. 큰 패널은 다른 패널을 닫는다.</summary>
        public GameScreen Open(string id, InteractionEvent? interaction = null, object argument = null)
        {
            GameScreen screen = Get(id);
            if (screen.Layer == ScreenLayer.Panel)
                foreach (GameScreen other in Snapshot())
                    if (other != screen && other.Layer == ScreenLayer.Panel) Close(other);

            UiNode layer = _layers[(int)screen.Layer];
            layer.Children.Remove(screen.Root);
            layer.Children.Add(screen.Root); // 같은 층에서 맨 위(Add는 부모가 이미 이 층이다)
            _open.Remove(screen);
            _open.Insert(InsertIndex(screen.Layer), screen);
            screen.Root.Visible = true;
            screen.IsOpen = true;
            Refresh();
            screen.OnOpened(new ScreenOpenContext(interaction, argument));
            Opened?.Invoke(screen);
            return screen;
        }

        private int InsertIndex(ScreenLayer layer)
        {
            int index = _open.Count;
            while (index > 0 && _open[index - 1].Layer > layer) index--;
            return index;
        }

        public bool Close(string id) => _screens.TryGetValue(id, out GameScreen screen) && Close(screen);

        public bool Close(GameScreen screen)
        {
            if (screen == null || !screen.IsOpen) return false;
            screen.IsOpen = false;
            screen.Root.Visible = false;
            _open.Remove(screen);
            if (Tree.Focused != null && IsInside(Tree.Focused, screen.Root)) Tree.SetFocus(null);
            Refresh();
            screen.OnClosed();
            Closed?.Invoke(screen);
            return true;
        }

        public bool Toggle(string id)
        {
            if (IsOpen(id)) { Close(id); return false; }
            Open(id);
            return true;
        }

        private static bool IsInside(UiNode node, UiNode root)
        {
            for (UiNode n = node; n != null; n = n.Parent) if (n == root) return true;
            return false;
        }

        private List<GameScreen> Snapshot()
        {
            _snapshot.Clear();
            _snapshot.AddRange(_open);
            return _snapshot;
        }

        private void Refresh()
        {
            bool anyModal = false;
            for (int i = 0; i < _open.Count; i++) if (_open[i].Layer == ScreenLayer.Modal) anyModal = true;
            _modalBlocker.Visible = anyModal;
            Tree.FocusScope = FocusScreen?.Root ?? _layers[(int)ScreenLayer.Panel];
            Tree.Layout(_width, _height);
        }

        // ---------- 입력 ----------

        /// <summary>프레임마다 한 번. 열린 화면에 흐른 시간을 넘긴다(Host의 dt, 테스트는 고정 간격).</summary>
        public void Tick(float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            bool relayout = false;
            foreach (GameScreen screen in Snapshot().ToArray())
                if (screen.IsOpen && screen.OnTick(seconds)) relayout = true;
            if (relayout) Tree.Layout(_width, _height);
        }

        /// <summary>프레임마다 한 번. 논리 좌표.</summary>
        public void Pointer(float x, float y, bool down, float wheel) => Tree.UpdatePointer(x, y, down, wheel);

        public void TextInput(string text) => Tree.TextInput(text);
        public void TextBackspace() => Tree.TextBackspace();
        public void TextComposition(string text) => Tree.TextComposition(text);

        /// <summary>이번 프레임에 눌린 키 하나. 화면 쪽에서 가져갔으면 true(Host는 그 키를 월드에 넘기지 않는다).</summary>
        public bool Key(ScreenKey key, bool shift)
        {
            if (TextInputFocused)
            {
                switch (key)
                {
                    case ScreenKey.Escape: Tree.SetFocus(null); break;
                    case ScreenKey.Enter: Tree.Submit(); break;
                    case ScreenKey.Left: Tree.Navigate(UiNav.Left); break;
                    case ScreenKey.Right: Tree.Navigate(UiNav.Right); break;
                    case ScreenKey.Up: if (shift) Tree.Navigate(UiNav.Up); break;
                    case ScreenKey.Down: if (shift) Tree.Navigate(UiNav.Down); break;
                }
                return true; // 글자 칸이 잡고 있는 동안 키 배정과 월드는 키를 보지 않는다
            }

            foreach (GameScreen screen in ReverseSnapshot())
                if (screen.IsOpen && screen.OnKey(key, shift)) return true;

            if (!ScreenBlocksMovement && _registry.TryGetByKey(key, out string bound))
            {
                Toggle(bound);
                return true;
            }

            GameScreen focus = FocusScreen;
            if (focus != null)
            {
                switch (key)
                {
                    case ScreenKey.Enter:
                        if (Tree.Focused == null) return false;
                        Tree.Submit();
                        return true;
                    case ScreenKey.Up: case ScreenKey.Down: case ScreenKey.Left: case ScreenKey.Right:
                        if (!focus.BlocksMovement && !shift) return false;
                        Tree.Navigate(key == ScreenKey.Up ? UiNav.Up : key == ScreenKey.Down ? UiNav.Down : key == ScreenKey.Left ? UiNav.Left : UiNav.Right);
                        return true;
                }
            }

            if (key == ScreenKey.Escape && Tree.Focused != null)
            {
                Tree.SetFocus(null);
                return true;
            }
            return false;
        }

        private IEnumerable<GameScreen> ReverseSnapshot()
        {
            List<GameScreen> snapshot = Snapshot();
            var reversed = new GameScreen[snapshot.Count];
            for (int i = 0; i < snapshot.Count; i++) reversed[i] = snapshot[snapshot.Count - 1 - i];
            return reversed;
        }

        /// <summary>월드의 상호작용: 그 종류에 등록된 화면을 연다. 열었으면 true. 열었든 아니든 Interacted로 알린다.</summary>
        public bool Interaction(InteractionEvent interaction)
        {
            GameScreen opened = _registry.TryGetByInteraction(interaction.Kind, out string id) ? Open(id, interaction) : null;
            Interacted?.Invoke(interaction, opened);
            return opened != null;
        }

        /// <summary>플레이어가 맵에 들어섰다(처음 들어갈 때 포함). 같은 맵이면 아무것도 하지 않는다.</summary>
        public void EnterMap(string mapId)
        {
            mapId ??= string.Empty;
            if (mapId == MapId) return;
            MapId = mapId;
            NotifyChanged(ChangeTopics.Map);
        }

        /// <summary>앞의 상호작용 대상(없으면 null). 바뀐 것이 있을 때만 TargetChanged를 낸다.</summary>
        public void SetInteractionTarget(InteractionTargetView? target)
        {
            if (Nullable.Equals(Target, target)) return;
            Target = target;
            TargetChanged?.Invoke();
        }

        /// <summary>열린 화면들에 무엇이 바뀌었는지 알린다.</summary>
        public void NotifyChanged(string topic)
        {
            foreach (GameScreen screen in Snapshot().ToArray())
                if (screen.IsOpen) screen.OnChanged(topic);
            Tree.Layout(_width, _height);
        }

        public void Emit(DrawList list, ITextMeasurer text) => Tree.Emit(list, text);
    }
}
