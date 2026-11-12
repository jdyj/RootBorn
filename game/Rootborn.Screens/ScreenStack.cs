using System;
using System.Collections.Generic;
using Rootborn.Core.Interaction;
using Rootborn.Ui;

namespace Rootborn.Screens
{
    /// <summary>
    /// 맵의 성질 이름(화면이 맵 id 대신 보는 것). Host가 맵 데이터에서 읽어 ScreenStack.EnterMap으로 넘긴다. 새 성질은 상수 하나로 더한다.
    /// </summary>
    public static class MapFeatures
    {
        /// <summary>가구를 놓는 면이 있다(game/world/interactions.json의 placementSurface).</summary>
        public const string FurniturePlacement = "furniture-placement";
    }

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
        // 화면마다 마지막으로 포커스가 있던 요소(위의 모달을 닫으면 그리로 돌아간다).
        private readonly Dictionary<GameScreen, UiNode> _lastFocus = new Dictionary<GameScreen, UiNode>();
        // Tick 전용 사본: 화면의 OnTick이 다른 화면을 열고 닫으면(그 안에서 Snapshot을 다시 쓴다) 도는 목록이 바뀌지 않게 따로 둔다. 매 프레임 새 배열을 만들지 않는다.
        private readonly List<GameScreen> _tickSnapshot = new List<GameScreen>(8);
        private float _width = 1920, _height = 1080;

        public ScreenStack(ScreenRegistry registry, ScreenServices services)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _services.Screens = this;
            for (int i = 0; i < _layers.Length; i++)
                _layers[i] = Tree.Root.Add(new UiNode { Id = "layer-" + ((ScreenLayer)i).ToString().ToLowerInvariant() });
            _modalBlocker = _layers[(int)ScreenLayer.Modal].Add(new UiNode { Id = "modal-blocker", Interactive = true, Visible = false });
            Tree.FocusChanged += RememberFocus;
            if (_services.Session != null) _services.Session.Changed += NotifyChanged;
            _services.Text.LanguageChanged += () => NotifyChanged(ChangeTopics.Language);
            if (_services.Display != null) _services.Display.Changed += _ => NotifyChanged(ChangeTopics.Display);
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

        /// <summary>
        /// 혼자 하기의 시계를 멈출 화면이 떠 있다(패널·모달·대화 — 등록 때 정한 값). Host가 창 활성 여부와 함께 GameSession.AdvanceTime에 넘긴다.
        /// </summary>
        public bool PausesClock
        {
            get
            {
                for (int i = 0; i < _open.Count; i++) if (_open[i].PausesClock) return true;
                return false;
            }
        }

        /// <summary>
        /// 상호작용이 연 패널·모달(OpenedByInteraction — 상자, 대화, 장소 등)이 떠 있다. 그동안 앞의 대상 안내("[E] …")를 숨긴다:
        /// 안내는 대상 위에 떠 패널 사각형 밖에 있을 수 있어 덮임만으로는 숨지 않는다.
        /// </summary>
        public bool InteractionScreenOpen
        {
            get
            {
                for (int i = 0; i < _open.Count; i++)
                {
                    GameScreen screen = _open[i];
                    if ((screen.Layer == ScreenLayer.Panel || screen.Layer == ScreenLayer.Modal)
                        && _registry.TryGet(screen.Id, out ScreenEntry entry) && entry.OpenedByInteraction) return true;
                }
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
        private readonly HashSet<string> _mapFeatures = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>지금 상호작용 키를 누르면 반응할 대상(없으면 null).</summary>
        public InteractionTargetView? Target { get; private set; }

        /// <summary>지금 크기로 다시 배치한다(화면이 노드를 새로 단 직후).</summary>
        internal void Relayout() => LayoutTree();

        public void Layout(float width, float height)
        {
            _width = width; _height = height;
            LayoutTree();
        }

        private readonly List<UiNode> _hudSurfaces = new List<UiNode>(32);
        private readonly List<UiNode> _coverSurfaces = new List<UiNode>(64);

        /// <summary>배치하고, 열린 패널·모달이 덮는 HUD 요소를 정한다.</summary>
        private void LayoutTree()
        {
            Tree.Layout(_width, _height);
            UpdateCoveredHud();
        }

        /// <summary>
        /// 열린 패널·모달의 겉면(UiSurfaces)과 겹치는 HUD 겉면은 덮인 것으로 둔다(그리지도 누르지도 않는다). 반쯤 가려진 시계·버튼이
        /// 패널 밑에서 비어져 나오지 않게 한다. 화면의 배치 수치가 바뀌어도(새 디자인) 저절로 맞는다. HUD끼리는 덮지 않는다(배치로 피한다).
        /// </summary>
        private void UpdateCoveredHud()
        {
            _hudSurfaces.Clear();
            _coverSurfaces.Clear();
            for (int i = 0; i < _open.Count; i++)
            {
                GameScreen screen = _open[i];
                if (screen.Layer == ScreenLayer.Hud) UiSurfaces.Collect(screen.Root, _hudSurfaces, includeCovered: true);
                else if (screen.Layer == ScreenLayer.Panel || screen.Layer == ScreenLayer.Modal) UiSurfaces.Collect(screen.Root, _coverSurfaces);
            }
            for (int i = 0; i < _hudSurfaces.Count; i++)
            {
                UiNode hud = _hudSurfaces[i];
                bool covered = false;
                for (int j = 0; j < _coverSurfaces.Count && !covered; j++) covered = UiSurfaces.Overlap(hud.Rect, _coverSurfaces[j].Rect);
                hud.Covered = covered;
            }
            // 한 묶음으로 읽히는 HUD(GameScreen.CoveredAsOne)는 하나가 덮이면 모두 숨긴다.
            for (int i = 0; i < _open.Count; i++)
            {
                GameScreen screen = _open[i];
                if (screen.Layer != ScreenLayer.Hud || !screen.CoveredAsOne) continue;
                _groupSurfaces.Clear();
                UiSurfaces.Collect(screen.Root, _groupSurfaces, includeCovered: true);
                bool any = false;
                foreach (UiNode node in _groupSurfaces) any |= node.Covered;
                if (any) foreach (UiNode node in _groupSurfaces) node.Covered = true;
            }
        }

        private readonly List<UiNode> _groupSurfaces = new List<UiNode>(8);

        private GameScreen Get(string id)
        {
            if (_screens.TryGetValue(id, out GameScreen screen)) return screen;
            if (!_registry.TryGet(id, out ScreenEntry entry)) throw new InvalidOperationException("등록되지 않은 화면: " + id);
            screen = entry.Create(_services) ?? throw new InvalidOperationException("화면을 만들지 못했다: " + id);
            screen.Id = entry.Id;
            screen.Layer = entry.Layer;
            screen.BlocksMovement = entry.BlocksMovement;
            screen.PausesClock = entry.PausesClock;
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
            LayoutTree(); // 연 화면과 Opened를 들은 화면이 보이는 것을 바꿨을 수 있다(덮인 HUD를 다시 정한다)
            if (screen.IsOpen && FocusScreen == screen) GiveFocus(screen, remembered: false);
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
            LayoutTree();
            GameScreen below = FocusScreen;
            if (below != null && (Tree.Focused == null || !IsInside(Tree.Focused, below.Root))) GiveFocus(below, remembered: true);
            return true;
        }

        public bool Toggle(string id)
        {
            if (IsOpen(id)) { Close(id); return false; }
            Open(id);
            return true;
        }

        /// <summary>
        /// 키보드로 일할 수 있게 포커스를 둔다(테두리 없이 — 테두리는 키보드로 옮길 때만 보인다). 화면이 OnOpened에서 이미 자기 안에 포커스를 뒀으면
        /// 그대로 둔다. remembered면 그 화면에서 마지막으로 포커스가 있던 요소(아직 누를 수 있으면)로 돌아간다(위의 모달을 닫았을 때).
        /// 아니면 화면이 정한 처음 요소(InitialFocus), 없으면 나무 순서의 첫 요소(글자 칸은 건너뛴다).
        /// </summary>
        private void GiveFocus(GameScreen screen, bool remembered)
        {
            if (Tree.Focused != null && IsInside(Tree.Focused, screen.Root)) return;
            UiNode target = null;
            if (remembered && _lastFocus.TryGetValue(screen, out UiNode last) && last.Focusable && last.IsActive && IsInside(last, screen.Root)) target = last;
            target ??= screen.InitialFocus;
            if (target == null || !target.Focusable || !target.IsActive) target = UiTree.FirstFocusable(screen.Root);
            if (target != null) Tree.SetFocusQuietly(target);
        }

        private void RememberFocus(UiNode focused)
        {
            if (focused == null) return;
            for (int i = _open.Count - 1; i >= 0; i--)
                if (IsInside(focused, _open[i].Root)) { _lastFocus[_open[i]] = focused; return; }
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
            LayoutTree();
        }

        // ---------- 입력 ----------

        /// <summary>프레임마다 한 번. 열린 화면에 흐른 시간을 넘긴다(Host의 dt, 테스트는 고정 간격).</summary>
        public void Tick(float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            bool relayout = false;
            _tickSnapshot.Clear();
            _tickSnapshot.AddRange(_open);
            for (int i = 0; i < _tickSnapshot.Count; i++)
                if (_tickSnapshot[i].IsOpen && _tickSnapshot[i].OnTick(seconds)) relayout = true;
            _tickSnapshot.Clear();
            if (relayout) LayoutTree();
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
                // 조합 중인 글자가 있으면 방향 키·Enter·Esc는 입력기의 것이다(칸 안에서 조합을 끝내거나 지운다). 확정 글자는 Host가 키보다 먼저 넘긴다.
                bool composing = ((UiTextField)Tree.Focused).Composing;
                // 이동을 막는 화면(캐릭터 만들기, 방 열기)에서는 걸을 캐릭터가 없으니 위·아래 키만으로 칸을 나간다. 막지 않는 화면은 Shift+위·아래.
                bool leaveVertically = shift || (FocusScreen != null && FocusScreen.BlocksMovement);
                switch (key)
                {
                    case ScreenKey.Escape: if (!composing) Tree.SetFocus(null); break;
                    case ScreenKey.Enter: Tree.Submit(); break;
                    case ScreenKey.Left: Tree.Navigate(UiNav.Left); break;
                    case ScreenKey.Right: Tree.Navigate(UiNav.Right); break;
                    case ScreenKey.Up: if (leaveVertically) Tree.Navigate(UiNav.Up); break;
                    case ScreenKey.Down: if (leaveVertically) Tree.Navigate(UiNav.Down); break;
                }
                return true; // 글자 칸이 잡고 있는 동안 키 배정과 월드는 키를 보지 않는다
            }

            foreach (GameScreen screen in ReverseSnapshot())
                if (screen.IsOpen && screen.OnKey(key, shift)) return true;

            if (_registry.TryGetByKey(key, out string bound) && KeyBindingOpen(bound))
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

        /// <summary>
        /// 키 배정이 들리는가: 이동을 막는 화면이 없을 때. 단, 여는 키가 시작 화면 위에서도 듣는 화면(KeyWorksOnStartScreens)은
        /// 막는 화면이 모두 시작 화면(WithoutWorld)일 때도 연다. 미니게임처럼 월드 위에서 막는 화면이 떠 있으면 열지 않는다.
        /// </summary>
        private bool KeyBindingOpen(string screenId)
        {
            if (!ScreenBlocksMovement) return true;
            if (!_registry.TryGet(screenId, out ScreenEntry entry) || !entry.KeyWorksOnStartScreens) return false;
            for (int i = 0; i < _open.Count; i++)
                if (_open[i].BlocksMovement && (!_registry.TryGet(_open[i].Id, out ScreenEntry open) || !open.WithoutWorld)) return false;
            return true;
        }

        /// <summary>등록 표(읽기 전용 조회: 단축키 안내가 키 배정을 읽는다).</summary>
        public ScreenRegistry Registry => _registry;

        /// <summary>
        /// 화면 안에서 버튼을 옮기는 키의 안내 줄. 이 키를 처리하는 곳(Key)과 같은 규칙: 이동을 막는 화면은 방향 키, 막지 않는 화면은
        /// Shift+방향 키로 옮기고 Enter로 누른다. 장치 키 이름은 Host가 채운 이름표에서 받는다.
        /// </summary>
        public static IReadOnlyList<KeyGuide.KeyGuideRow> NavigationGuide(KeyGuide.KeyGuideKeys keys)
        {
            KeyGuide.KeyChord directions = keys.Directions;
            var withShift = directions.With(keys.Shift);
            return new[]
            {
                new KeyGuide.KeyGuideRow(KeyGuide.KeyGuideGroups.Navigation, "ui.keys.navigate-blocking", new[] { directions }),
                new KeyGuide.KeyGuideRow(KeyGuide.KeyGuideGroups.Navigation, "ui.keys.navigate-walking", new[] { withShift }),
                new KeyGuide.KeyGuideRow(KeyGuide.KeyGuideGroups.Navigation, "ui.keys.press", keys.For(ScreenKey.Enter)),
                // 글자 칸(Key의 글자 칸 갈래): 위·아래로 나온다(걸으면서 보는 화면은 Shift와 함께), Esc는 칸을 놓는다.
                new KeyGuide.KeyGuideRow(KeyGuide.KeyGuideGroups.Navigation, "ui.keys.field-leave", Concat(keys.For(ScreenKey.Up), keys.For(ScreenKey.Down))),
                new KeyGuide.KeyGuideRow(KeyGuide.KeyGuideGroups.Navigation, "ui.keys.field-release", keys.For(ScreenKey.Escape)),
            };
        }

        private static IReadOnlyList<KeyGuide.KeyChord> Concat(IReadOnlyList<KeyGuide.KeyChord> a, IReadOnlyList<KeyGuide.KeyChord> b)
        {
            var all = new List<KeyGuide.KeyChord>(a.Count + b.Count);
            all.AddRange(a);
            all.AddRange(b);
            return all;
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

        /// <summary>
        /// 플레이어가 맵에 들어섰다(처음 들어갈 때 포함). features는 그 맵의 성질(MapFeatures — 맵 데이터에서 Host가 읽는다).
        /// 화면은 맵 id가 아니라 성질을 본다(맵은 새로 그릴 예정이다). 같은 맵·같은 성질이면 아무것도 하지 않는다.
        /// </summary>
        public void EnterMap(string mapId, IReadOnlyCollection<string> features = null)
        {
            mapId ??= string.Empty;
            if (mapId == MapId && SameFeatures(features)) return;
            MapId = mapId;
            _mapFeatures.Clear();
            if (features != null) foreach (string feature in features) if (!string.IsNullOrEmpty(feature)) _mapFeatures.Add(feature);
            NotifyChanged(ChangeTopics.Map);
        }

        /// <summary>지금 맵에 그 성질이 있는가(MapFeatures).</summary>
        public bool HasMapFeature(string feature) => feature != null && _mapFeatures.Contains(feature);

        private bool SameFeatures(IReadOnlyCollection<string> features)
        {
            int count = 0;
            if (features is IReadOnlyList<string> list)
            {
                // Host가 매 프레임 같은 배열로 부른다: 열거자를 만들지 않게 번호로 돈다.
                for (int i = 0; i < list.Count; i++)
                    if (!SameFeature(list[i], ref count)) return false;
            }
            else if (features != null)
                foreach (string feature in features)
                    if (!SameFeature(feature, ref count)) return false;
            return count == _mapFeatures.Count;
        }

        private bool SameFeature(string feature, ref int count)
        {
            if (string.IsNullOrEmpty(feature)) return true;
            if (!_mapFeatures.Contains(feature)) return false;
            count++;
            return true;
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
            LayoutTree();
        }

        public void Emit(DrawList list, ITextMeasurer text) => Tree.Emit(list, text);
    }
}
