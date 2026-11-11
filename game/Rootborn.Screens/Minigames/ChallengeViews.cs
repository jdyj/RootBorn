using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Ui;

namespace Rootborn.Screens.Minigames
{
    /// <summary>
    /// 도전 하나를 그리는 부분. 도전 종류(Core MinigameMechanic)마다 하나를 ChallengeViewCatalog에 등록한다.
    /// 판정은 하지 않는다: 입력을 Core MinigameSession에 넘기고, 진행 값(남은 시간·실수·진행·힌트)을 읽어 그릴 뿐이다.
    /// 새 도전 종류 = 이 인터페이스를 구현한 클래스 하나 + 등록 한 줄.
    /// </summary>
    public interface IChallengeView
    {
        /// <summary>새 도전이 시작됐다: context.Stage 아래에 노드를 만든다(도전마다 한 번).</summary>
        void Build(ChallengeViewContext context, ChallengeSession challenge);

        /// <summary>값이 바뀌었다(진행, 실수, 남은 초, 보여 주기 끝남, 입력 뒤). 글자와 색을 고친다.</summary>
        void Refresh(ChallengeSession challenge);

        /// <summary>프레임마다(움직이는 것: 박자 커서, 과녁, 장애물). 배치를 바꿨으면 true.</summary>
        bool Animate(ChallengeSession challenge);

        /// <summary>이 도전이 쓰는 키(Unity: Space 탭, R 회전, A/D·←/→ 줄 이동). 가져갔으면 true.</summary>
        bool OnKey(ScreenKey key, ChallengeSession challenge);
    }

    /// <summary>그리는 부분이 쓰는 것: 노드를 달 자리, 문구, 판 입력, 안내 글.</summary>
    public sealed class ChallengeViewContext
    {
        private readonly MinigameScreen _screen;

        internal ChallengeViewContext(MinigameScreen screen) { _screen = screen; }

        /// <summary>도전 노드를 다는 자리(Unity "Challenge" 800x290). 도전이 바뀌면 새 자리가 된다.</summary>
        public UiNode Stage { get; internal set; }

        /// <summary>지금 판. 끝났으면 입력을 넘기지 않는다.</summary>
        public MinigameSession Session => _screen.Session;
        public bool Active => _screen.IsOpen && Session != null && !Session.IsComplete;

        public string T(string key) => _screen.Text(key);
        public string F(string key, params object[] args) => _screen.Format(key, args);
        public string Shown(string keyOrText) => _screen.ShownText(keyOrText);

        /// <summary>판 아래 안내 글(Unity Feedback).</summary>
        public void SetFeedback(string text) => _screen.SetFeedback(text);

        /// <summary>판에 입력을 넣었다. 화면이 값을 다시 읽어 그린다.</summary>
        public void Changed() => _screen.RefreshView();
    }

    /// <summary>도전 종류 → 그리는 부분. 등록하지 않은 종류는 안내 글만 보인다.</summary>
    public sealed class ChallengeViewCatalog
    {
        private readonly Dictionary<MinigameMechanic, Func<IChallengeView>> _views = new Dictionary<MinigameMechanic, Func<IChallengeView>>();

        public static ChallengeViewCatalog Default()
        {
            var catalog = new ChallengeViewCatalog();
            catalog.Add(MinigameMechanic.Sequence, () => new SymbolChoiceView(showPreview: true));
            catalog.Add(MinigameMechanic.Choice, () => new SymbolChoiceView(showPreview: false));
            catalog.Add(MinigameMechanic.Timing, () => new TimingView());
            catalog.Add(MinigameMechanic.Placement, () => new PlacementView());
            catalog.Add(MinigameMechanic.TargetDodge, () => new TargetDodgeView());
            return catalog;
        }

        public void Add(MinigameMechanic mechanic, Func<IChallengeView> create)
        {
            if (_views.ContainsKey(mechanic)) throw new InvalidOperationException("이미 등록된 도전 종류: " + mechanic);
            _views[mechanic] = create ?? throw new ArgumentNullException(nameof(create));
        }

        public IChallengeView Create(MinigameMechanic mechanic) => _views.TryGetValue(mechanic, out Func<IChallengeView> create) ? create() : null;
    }

    /// <summary>색을 칠한 누를 수 있는 칸(배치 판의 칸, 줄 단추). 이 묶음의 새 부품 — Rootborn.Ui로 올릴 후보.</summary>
    public sealed class MinigameCell : UiNode
    {
        public UiColor Color = UiTheme.White;
        public string Text;
        public float FontSize = 19;

        public MinigameCell() { Interactive = true; Focusable = true; }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            list.Fill(Rect, IsActive && (Hovered || Pressed) ? Lighter(Color) : Color);
            if (!string.IsNullOrEmpty(Text))
                list.Text(Rect.X + (Rect.W - text.Width(Text, FontSize)) / 2f, Rect.Y + (Rect.H - text.LineHeight(FontSize)) / 2f, Text, FontSize, UiTheme.Ink);
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }

        private static UiColor Lighter(UiColor c) =>
            new UiColor((byte)Math.Min(255, c.R + 24), (byte)Math.Min(255, c.G + 24), (byte)Math.Min(255, c.B + 24), c.A);
    }

    /// <summary>
    /// 조준 판(Unity MinigamePointerSurface): 누른 점을 판 안의 0~1 좌표로 알린다. Unity처럼 아래가 0, 위가 1이다.
    /// 이 묶음의 새 부품 — Rootborn.Ui로 올릴 후보.
    /// </summary>
    public sealed class MinigameAimSurface : UiNode
    {
        public UiColor Color = UiTheme.White;
        public event Action<PlanePoint> Aimed;

        public MinigameAimSurface() { Interactive = true; }

        public override void OnClick(float x, float y)
        {
            if (Rect.W <= 0 || Rect.H <= 0 || !Rect.Contains(x, y)) return;
            Aimed?.Invoke(new PlanePoint((x - Rect.X) / Rect.W, 1f - (y - Rect.Y) / Rect.H));
        }

        public override void OnSubmit() => OnClick(Rect.CenterX, Rect.CenterY);

        public override void Emit(DrawList list, ITextMeasurer text) => list.Fill(Rect, Color);
    }

    /// <summary>그리는 부분들이 함께 쓰는 수치(Unity MinigamePanel의 색). 새 디자인에서는 여기와 각 View의 Metrics만 고친다.</summary>
    internal static class ChallengeColors
    {
        public static readonly UiColor Track = UnityLayout.Color(.5f, .38f, .24f);
        public static readonly UiColor Beat = UnityLayout.Color(.35f, .64f, .5f);
        public static readonly UiColor Cursor = UnityLayout.Color(.97f, .82f, .25f);
        public static readonly UiColor Cell = UnityLayout.Color(.84f, .81f, .66f);
        public static readonly UiColor Blocked = UnityLayout.Color(.39f, .34f, .29f);
        public static readonly UiColor Surface = UnityLayout.Color(.78f, .82f, .68f);
        public static readonly UiColor Target = UnityLayout.Color(.83f, .38f, .24f);
        public static readonly UiColor Obstacle = UnityLayout.Color(.7f, .4f, .26f);
        public static readonly UiColor Lane = UnityLayout.Color(.78f, .68f, .49f);
        public static readonly UiColor LaneCurrent = UnityLayout.Color(.35f, .64f, .5f);

        /// <summary>놓인 조각 색(Unity Color.HSVToRGB((j*.19+.05)%1, .42, .77)).</summary>
        public static UiColor Piece(int index)
        {
            float h = (index * .19f + .05f) % 1f, s = .42f, v = .77f;
            float c = v * s, x = c * (1 - Math.Abs(h * 6 % 2 - 1)), m = v - c;
            float r, g, b;
            switch ((int)(h * 6) % 6)
            {
                case 0: r = c; g = x; b = 0; break;
                case 1: r = x; g = c; b = 0; break;
                case 2: r = 0; g = c; b = x; break;
                case 3: r = 0; g = x; b = c; break;
                case 4: r = x; g = 0; b = c; break;
                default: r = c; g = 0; b = x; break;
            }
            return UnityLayout.Color(r + m, g + m, b + m);
        }
    }

    /// <summary>
    /// 순서 외우기(Sequence)와 고르기(Choice): 기호 단추(한 줄에 최대 3개, 190x68, 간격 210·86). 힌트가 있으면 맞는 단추에 표시.
    /// 순서 외우기는 보여 주는 동안 단추를 막고 안내 글에 순서를 보인다.
    /// </summary>
    public sealed class SymbolChoiceView : IChallengeView
    {
        internal static class Metrics
        {
            public const int MaxColumns = 3;
            public const float StepX = 210f, TopY = 40f, StepY = 86f, W = 190f, H = 68f, Font = 19f;
        }

        public const string ChoicePrefix = "minigame.choice.";
        public static string ChoiceId(int index) => ChoicePrefix + index;

        private readonly bool _showPreview;
        private readonly List<UiButton> _choices = new List<UiButton>(6);
        private ChallengeViewContext _context;
        private string[] _symbols = Array.Empty<string>();
        private bool? _previewShown;

        public SymbolChoiceView(bool showPreview) { _showPreview = showPreview; }

        public void Build(ChallengeViewContext context, ChallengeSession challenge)
        {
            _context = context;
            _symbols = challenge.Symbols;
            int columns = Math.Max(1, Math.Min(Metrics.MaxColumns, _symbols.Length));
            for (int i = 0; i < _symbols.Length; i++)
            {
                int index = i;
                float x = (i % columns - (columns - 1) * .5f) * Metrics.StepX, y = Metrics.TopY - i / columns * Metrics.StepY;
                UiButton button = context.Stage.Add(new UiButton { Id = ChoiceId(i), FontSize = Metrics.Font }).Centered(x, y, Metrics.W, Metrics.H);
                button.Clicked += () => Choose(index);
                _choices.Add(button);
            }
        }

        private void Choose(int index)
        {
            if (!_context.Active) return;
            _context.Session.Choose(index);
            _context.Changed();
        }

        public void Refresh(ChallengeSession c)
        {
            for (int i = 0; i < _choices.Count; i++)
            {
                _choices[i].Enabled = !c.PreviewVisible && !c.IsComplete;
                string label = _context.T(_symbols[i]);
                _choices[i].Text = c.IsHintChoice(i) ? _context.F("ui.minigame.hint-choice", label) : label;
            }
            if (!_showPreview) return;
            if (c.PreviewVisible && _previewShown != true)
            {
                int[] sequence = c.PreviewSequence;
                var labels = new string[sequence.Length];
                for (int i = 0; i < sequence.Length; i++) labels[i] = _context.T(_symbols[sequence[i]]);
                _context.SetFeedback(_context.F("ui.minigame.preview", _context.T("minigame.memorize"), string.Join(_context.T("ui.minigame.sequence-separator"), labels)));
                _previewShown = true;
            }
            else if (!c.PreviewVisible && _previewShown == true)
            {
                _context.SetFeedback(_context.T("minigame.repeat"));
                _previewShown = false;
            }
        }

        public bool Animate(ChallengeSession c) => false;
        public bool OnKey(ScreenKey key, ChallengeSession c) => false;
    }

    /// <summary>박자(Timing): 길이 620 막대 위에 박자 칸, 지나가는 커서, 누르기 단추. Space로도 누른다.</summary>
    public sealed class TimingView : IChallengeView
    {
        internal static class Metrics
        {
            public const float TrackY = 36f, TrackW = 620f, TrackH = 24f, BeatMinW = 8f, BeatH = 46f, CursorW = 6f, CursorH = 66f;
            public const float TapY = -80f, TapW = 270f, TapH = 64f, Font = 19f;
        }

        public const string TapId = "minigame.tap";
        public const string CursorId = "minigame.cursor";

        private ChallengeViewContext _context;
        private UiBox _cursor;
        private UiButton _tap;

        public void Build(ChallengeViewContext context, ChallengeSession c)
        {
            _context = context;
            context.Stage.Add(new UiBox { Color = ChallengeColors.Track }).Centered(0f, Metrics.TrackY, Metrics.TrackW, Metrics.TrackH);
            float[] beats = c.BeatTimes;
            for (int i = 0; i < beats.Length; i++)
                context.Stage.Add(new UiBox { Id = "minigame.beat." + i, Color = ChallengeColors.Beat })
                    .Centered((beats[i] / c.Duration - .5f) * Metrics.TrackW, Metrics.TrackY, Math.Max(Metrics.BeatMinW, 2f * c.TimingWindow / c.Duration * Metrics.TrackW), Metrics.BeatH);
            _cursor = context.Stage.Add(new UiBox { Id = CursorId, Color = ChallengeColors.Cursor }).Centered(-Metrics.TrackW / 2f, Metrics.TrackY, Metrics.CursorW, Metrics.CursorH);
            _tap = context.Stage.Add(new UiButton { Id = TapId, FontSize = Metrics.Font }).Centered(0f, Metrics.TapY, Metrics.TapW, Metrics.TapH);
            _tap.Clicked += Tap;
        }

        private void Tap()
        {
            if (!_context.Active) return;
            _context.Session.Tap();
            _context.Changed();
        }

        public void Refresh(ChallengeSession c)
        {
            _tap.Text = _context.T("minigame.tap");
            _tap.Enabled = !c.IsComplete;
        }

        public bool Animate(ChallengeSession c)
        {
            _cursor.Centered((c.Elapsed / c.Duration - .5f) * Metrics.TrackW, Metrics.TrackY, Metrics.CursorW, Metrics.CursorH);
            return true;
        }

        public bool OnKey(ScreenKey key, ChallengeSession c)
        {
            if (key != ScreenKey.Space) return false;
            Tap();
            return true;
        }
    }

    /// <summary>
    /// 놓기(Placement): 판의 칸(최대 50, 판 470x260 안), 조각 단추(오른쪽), 돌리기 단추. 조각을 고르고 칸을 누르면 그 칸이 조각의 왼쪽 아래다.
    /// R로 돌린다. Unity처럼 판의 y는 위로 커진다(칸 (0,0)이 왼쪽 아래).
    /// </summary>
    public sealed class PlacementView : IChallengeView
    {
        internal static class Metrics
        {
            public const float MaxCell = 50f, BoardW = 470f, BoardH = 260f, BoardX = -95f, BoardY = 5f, CellGap = 3f;
            public const float PieceX = 285f, PieceTopY = 100f, PieceStep = 48f, PieceW = 150f, PieceH = 42f;
            public const float RotateX = 270f, RotateY = -128f, RotateW = 180f, RotateH = 48f, Font = 19f;
        }

        public static string CellId(int x, int y) => "minigame.cell." + x + "." + y;
        public static string PieceId(int index) => "minigame.piece." + index;
        public const string RotateId = "minigame.rotate";

        private ChallengeViewContext _context;
        private GridPoint _board;
        private GridPoint[] _pieces = Array.Empty<GridPoint>();
        private HashSet<GridPoint> _blocked = new HashSet<GridPoint>();
        private MinigameCell[] _cells = Array.Empty<MinigameCell>();
        private readonly List<UiButton> _pieceButtons = new List<UiButton>(4);
        private UiButton _rotate;
        private string _lastKey;

        public int SelectedPiece { get; private set; }
        public int Rotation { get; private set; }

        public void Build(ChallengeViewContext context, ChallengeSession c)
        {
            _context = context;
            _board = c.BoardSize;
            _pieces = c.PieceSizes;
            _blocked = new HashSet<GridPoint>(c.BlockedCells);
            float cell = Math.Min(Metrics.MaxCell, Math.Min(Metrics.BoardW / Math.Max(1, _board.X), Metrics.BoardH / Math.Max(1, _board.Y)));
            _cells = new MinigameCell[_board.X * _board.Y];
            for (int y = 0; y < _board.Y; y++)
                for (int x = 0; x < _board.X; x++)
                {
                    var point = new GridPoint(x, y);
                    MinigameCell node = context.Stage.Add(new MinigameCell { Id = CellId(x, y) })
                        .Centered(Metrics.BoardX + (x - (_board.X - 1) * .5f) * cell, Metrics.BoardY + (y - (_board.Y - 1) * .5f) * cell, cell - Metrics.CellGap, cell - Metrics.CellGap);
                    node.Clicked += () => Place(point);
                    _cells[y * _board.X + x] = node;
                }
            for (int i = 0; i < _pieces.Length; i++)
            {
                int index = i;
                UiButton button = context.Stage.Add(new UiButton { Id = PieceId(i), FontSize = Metrics.Font })
                    .Centered(Metrics.PieceX, Metrics.PieceTopY - i * Metrics.PieceStep, Metrics.PieceW, Metrics.PieceH);
                button.Clicked += () => Select(index);
                _pieceButtons.Add(button);
            }
            _rotate = context.Stage.Add(new UiButton { Id = RotateId, FontSize = Metrics.Font }).Centered(Metrics.RotateX, Metrics.RotateY, Metrics.RotateW, Metrics.RotateH);
            _rotate.Clicked += Rotate;
        }

        private void Select(int index)
        {
            if (!_context.Active) return;
            SelectedPiece = index;
            _lastKey = null;
            _context.Changed();
        }

        private void Rotate()
        {
            if (!_context.Active) return;
            Rotation = (Rotation + 1) % 4;
            _lastKey = null;
            _context.Changed();
        }

        private void Place(GridPoint point)
        {
            if (!_context.Active) return;
            bool placed = _context.Session.Place(SelectedPiece, point, Rotation);
            MinigameSession session = _context.Session;
            if (placed && !session.IsComplete)
                for (int i = 0; i < _pieces.Length; i++)
                    if (!session.Current.TryGetPieceRect(i, out _)) { SelectedPiece = i; Rotation = 0; break; }
            _lastKey = placed ? null : "minigame.placement-invalid";
            _context.Changed();
        }

        public void Refresh(ChallengeSession c)
        {
            for (int y = 0; y < _board.Y; y++)
                for (int x = 0; x < _board.X; x++)
                {
                    var point = new GridPoint(x, y);
                    UiColor color = _blocked.Contains(point) ? ChallengeColors.Blocked : ChallengeColors.Cell;
                    for (int j = 0; j < _pieces.Length; j++)
                        if (c.TryGetPieceRect(j, out GridRect rect) && rect.Contains(point)) color = ChallengeColors.Piece(j);
                    MinigameCell cell = _cells[y * _board.X + x];
                    cell.Color = color;
                    cell.Enabled = !c.IsComplete;
                }
            for (int i = 0; i < _pieceButtons.Count; i++)
            {
                _pieceButtons[i].Text = _context.F("minigame.piece", i + 1, _pieces[i].X, _pieces[i].Y);
                _pieceButtons[i].Enabled = !c.IsComplete;
            }
            _rotate.Text = _context.T("minigame.rotate");
            _rotate.Enabled = !c.IsComplete;
            _context.SetFeedback(_lastKey != null ? _context.T(_lastKey) : _context.F("minigame.placement-selection", SelectedPiece + 1, Rotation * 90));
        }

        public bool Animate(ChallengeSession c) => false;

        public bool OnKey(ScreenKey key, ChallengeSession c)
        {
            if (key != ScreenKey.Rotate) return false;
            Rotate();
            return true;
        }
    }

    /// <summary>
    /// 조준·피하기(TargetDodge): 왼쪽 조준 판(400x235)에서 움직이는 과녁을 누르고, 오른쪽 줄 단추로 다가오는 장애물을 피한다.
    /// A/D·←/→로 줄을 옮긴다.
    /// </summary>
    public sealed class TargetDodgeView : IChallengeView
    {
        internal static class Metrics
        {
            public const float SurfaceX = -120f, SurfaceY = 10f, SurfaceW = 400f, SurfaceH = 235f;
            public const float TargetScaleX = 800f, TargetScaleY = 470f, TargetFont = 30f;
            public const float LaneX = 160f, LaneStep = 65f, LaneY = -100f, LaneSize = 54f, LaneFont = 19f;
            public const float ObstacleTopY = 110f, ObstacleTravel = 170f, ObstacleW = 45f, ObstacleH = 26f;
            public const string TargetGlyph = "◎";
        }

        public const string SurfaceId = "minigame.aim";
        public const string TargetId = "minigame.target";
        public const string ObstacleId = "minigame.obstacle";
        public static string LaneId(int index) => "minigame.lane." + index;

        private ChallengeViewContext _context;
        private MinigameAimSurface _surface;
        private UiBox _target;
        private UiBox _obstacle;
        private readonly List<MinigameCell> _lanes = new List<MinigameCell>(4);

        public void Build(ChallengeViewContext context, ChallengeSession c)
        {
            _context = context;
            _surface = context.Stage.Add(new MinigameAimSurface { Id = SurfaceId, Color = ChallengeColors.Surface })
                .Centered(Metrics.SurfaceX, Metrics.SurfaceY, Metrics.SurfaceW, Metrics.SurfaceH);
            _surface.Aimed += Aim;
            _target = _surface.Add(new UiBox { Id = TargetId, Color = ChallengeColors.Target });
            _target.Add(new UiLabel { Text = Metrics.TargetGlyph, FontSize = Metrics.TargetFont, Align = UiAlign.Center }).Stretch(0, 0, 0, 0);
            for (int i = 0; i < c.LaneCount; i++)
            {
                int lane = i;
                MinigameCell button = context.Stage.Add(new MinigameCell { Id = LaneId(i), Text = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = Metrics.LaneFont })
                    .Centered(Metrics.LaneX + i * Metrics.LaneStep, Metrics.LaneY, Metrics.LaneSize, Metrics.LaneSize);
                button.Clicked += () => MoveLane(lane);
                _lanes.Add(button);
            }
            _obstacle = context.Stage.Add(new UiBox { Id = ObstacleId, Color = ChallengeColors.Obstacle });
            Animate(c);
        }

        private void Aim(PlanePoint point)
        {
            if (!_context.Active) return;
            _context.Session.Aim(point);
            _context.Changed();
        }

        private void MoveLane(int lane)
        {
            if (!_context.Active) return;
            _context.Session.MoveLane(lane);
            _context.Changed();
        }

        public void Refresh(ChallengeSession c)
        {
            for (int i = 0; i < _lanes.Count; i++)
            {
                _lanes[i].Color = i == c.Lane ? ChallengeColors.LaneCurrent : ChallengeColors.Lane;
                _lanes[i].Enabled = !c.IsComplete;
            }
            _surface.Enabled = !c.IsComplete;
            _context.SetFeedback(_context.T("minigame.aim-dodge"));
        }

        public bool Animate(ChallengeSession c)
        {
            _target.Visible = c.HasTarget;
            PlanePoint p = c.TargetPosition;
            _target.Centered((p.X - .5f) * Metrics.SurfaceW, (p.Y - .5f) * Metrics.SurfaceH, c.TargetRadius * Metrics.TargetScaleX, c.TargetRadius * Metrics.TargetScaleY);
            _target.Interactive = false;
            _obstacle.Visible = c.NextObstacleLane >= 0;
            _obstacle.Centered(Metrics.LaneX + c.NextObstacleLane * Metrics.LaneStep, Metrics.ObstacleTopY - c.NextObstacleFraction * Metrics.ObstacleTravel, Metrics.ObstacleW, Metrics.ObstacleH);
            return true;
        }

        public bool OnKey(ScreenKey key, ChallengeSession c)
        {
            if (key == ScreenKey.Left || key == ScreenKey.AltLeft) { MoveLane(Math.Max(0, c.Lane - 1)); return true; }
            if (key == ScreenKey.Right || key == ScreenKey.AltRight) { MoveLane(Math.Min(c.LaneCount - 1, c.Lane + 1)); return true; }
            return false;
        }
    }
}
