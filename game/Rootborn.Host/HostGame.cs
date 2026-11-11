using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Host.Input;
using Rootborn.Host.Rendering;
using Rootborn.Host.Sample;
using Rootborn.Host.Text;
using Rootborn.Core.Interaction;
using Rootborn.Core.Save;
using Rootborn.Data.Save;
using GameSession = Rootborn.Game.GameSession;
using GameStartSettings = Rootborn.Game.GameStartSettings;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Ui;

namespace Rootborn.Host
{
    /// <summary>
    /// 창과 게임 루프. 프레임마다 장치 계층(키보드, 포인터, 글자)을 한 번씩 읽어 UI에 값으로 넘기고,
    /// UI가 낸 그리기 명령을 창 크기의 화면 버퍼에 그린 뒤 창에 옮긴다. 규칙 판단은 여기에 쓰지 않는다.
    /// 월드 화면(--world, --slot)이면 월드를 먼저 그리고 그 위에 화면 쌓기(ScreenStack)를 올린다. 키는 화면 쌓기가 먼저 받고,
    /// 화면이 가져가지 않은 상호작용 키와 누르고 있는 방향 키를 월드에 넘긴다(이동을 막는 화면이 떠 있으면 넘기지 않는다).
    /// 월드의 상호작용 이벤트는 화면 쌓기의 등록 표(상호작용 종류 → 화면)로 넘긴다. --slot이면 GameSession으로 게임 한 판을 시작한다.
    /// 테스트 모드는 프레임마다 1/60초씩 흐른다.
    /// </summary>
    public sealed class HostGame : Microsoft.Xna.Framework.Game
    {
        private static readonly Color Background = new Color(24, 22, 30);
        private const float TestFrameSeconds = 1f / 60f;

        private readonly HostOptions _options;
        private readonly GraphicsDeviceManager _graphics;
        private readonly KeyboardDevice _keyboard = new KeyboardDevice();
        private readonly DrawList _drawList = new DrawList();
        private SpriteBatch _batch;
        private RenderTarget2D _scene;
        private TextRenderer _text;
        private HostTextMeasurer _measurer;
        private UiRenderer _renderer;
        private TextInputDevice _textInput;
        private UiTree _tree;
        private UiTextField _textTarget;
        private int _shotCount;

        /// <summary>포인터를 읽는 곳. 테스트는 값을 넣어 주는 것으로 바꾼다.</summary>
        public IPointerSource Pointer = new OsPointer();

        public ScreenFit Fit { get; private set; }
        public SampleScreen Sample { get; private set; }
        public WorldScreen World { get; private set; }
        /// <summary>월드 위의 화면들(월드 화면일 때만).</summary>
        public ScreenStack Screens { get; private set; }
        /// <summary>게임 한 판(--slot으로 시작했을 때만).</summary>
        public GameSession Session { get; private set; }
        public GameText Text { get; private set; }

        /// <summary>등록 표에 화면을 더 얹는다(테스트가 시험용 화면을 더할 때). 창을 띄우기 전에 정한다.</summary>
        public Action<ScreenRegistry> ExtraScreens;
        public UiTree Ui => _tree;
        /// <summary>UI 그리기(스킨, 찾지 못한 그림 목록).</summary>
        public UiRenderer Renderer => _renderer;
        /// <summary>마지막으로 그린 화면 버퍼(스크린샷과 같은 그림).</summary>
        public RenderTarget2D Scene => _scene;

        /// <summary>글자 칸이 선택되어 입력기가 켜져 있다.</summary>
        public bool TextInputActive => _textInput != null && _textInput.Active;

        public HostGame(HostOptions options)
        {
            _options = options;
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = options.Width,
                PreferredBackBufferHeight = options.Height,
                SynchronizeWithVerticalRetrace = !options.TestMode,
            };
            IsFixedTimeStep = false;
            if (options.TestMode) InactiveSleepTime = TimeSpan.Zero;
            IsMouseVisible = true;
            Window.AllowUserResizing = !options.TestMode;
            Window.Title = "ROOTBORN";
        }

        protected override void LoadContent()
        {
            _batch = new SpriteBatch(GraphicsDevice);
            _text = new TextRenderer(GraphicsDevice, _options.FontPath);
            _measurer = new HostTextMeasurer(_text);
            var atlases = new SpriteAtlases(_options.RepoRoot);
            UrbanTheme theme = UrbanTheme.Load(_options.RepoRoot);
            _renderer = new UiRenderer(GraphicsDevice, _text, UiSkins.For(_options.UiTheme, _options.RepoRoot, atlases), new ImageCatalog(atlases, theme), atlases);
            _textInput = new TextInputDevice(Window);
            if (_options.WorldActive)
            {
                World = new WorldScreen(GraphicsDevice, _options);
                StartScreens();
                SetTree(Screens.Tree);
            }
            else ShowSample();
        }

        /// <summary>
        /// 화면 쌓기를 만든다. 슬롯으로 시작했으면 정의(월드 화면이 읽은 것과 같은 data/)로 GameSession을 시작해 화면이 부를 명령으로 넘긴다.
        /// 세이브 폴더 위치는 실행 인자에서만 온다.
        /// </summary>
        private void StartScreens()
        {
            Text = GameText.Load(Path.Combine(_options.RepoRoot, "data", "localization"), _options.Language);
            SessionCommands commands = null;
            if (_options.SaveSlot != null)
            {
                Session = GameSession.Start(World.Content.Definitions, GameStartSettings.FromRegistry(Path.Combine(_options.RepoRoot, "data")),
                    new FileSaveStore(_options.SaveRoot), _options.SaveSlot, SaveFileNames.DefaultPlayerId);
                commands = new SessionCommands(Session);
            }
            ScreenRegistry registry = ScreenCatalog.Create();
            ExtraScreens?.Invoke(registry);
            var services = new ScreenServices(Text, commands) { Names = DisplayNames.Load(Path.Combine(_options.RepoRoot, "data")) };
            Screens = new ScreenStack(registry, services);
            Screens.EnterMap(World.Session.Map.Id);
            Screens.OpenHud();
        }

        /// <summary>견본 화면을 새로 만들어 띄운다.</summary>
        public void ShowSample()
        {
            Sample = new SampleScreen();
            SetTree(Sample.Tree);
        }

        private void SetTree(UiTree tree)
        {
            _tree = tree;
            _textTarget = null;
            _textInput.Stop();
            _scene?.Dispose();
            _scene = null;
            EnsureScene();
        }

        /// <summary>창 크기를 바꾼다.</summary>
        public void Resize(int width, int height)
        {
            _graphics.PreferredBackBufferWidth = width;
            _graphics.PreferredBackBufferHeight = height;
            _graphics.ApplyChanges();
            EnsureScene();
        }

        /// <summary>화면 버퍼와 배율, 배치를 지금 창 크기에 맞춘다.</summary>
        private void EnsureScene()
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth, height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            if (_scene != null && _scene.Width == width && _scene.Height == height) return;
            _scene?.Dispose();
            _scene = new RenderTarget2D(GraphicsDevice, width, height);
            Fit = ScreenFit.For(width, height);
            _measurer.Scale = Fit.Scale;
            if (Screens != null) Screens.Layout(Fit.LogicalWidth, Fit.LogicalHeight);
            else _tree.Layout(Fit.LogicalWidth, Fit.LogicalHeight);
        }

        protected override void Update(GameTime gameTime)
        {
            EnsureScene();

            // 글자가 먼저다: 입력기가 Enter로 조합을 끝내면 확정 글자와 Enter가 같은 프레임에 온다.
            foreach (TextEvent e in _textInput.Drain())
            {
                if (e.Kind == TextEventKind.Text) _tree.TextInput(e.Text);
                else if (e.Kind == TextEventKind.Backspace) _tree.TextBackspace();
                else _tree.TextComposition(e.Text);
            }

            PointerState pointer = Pointer.Read();
            _tree.UpdatePointer(pointer.X / Fit.Scale, pointer.Y / Fit.Scale, pointer.LeftDown, pointer.Wheel);

            KeyFrame keys = _keyboard.Read();
            if (Screens != null) UpdateWorld(keys, gameTime);
            else
            {
                if (keys.Tab) _tree.Navigate(keys.Shift ? UiNav.Previous : UiNav.Next);
                if (keys.Up) _tree.Navigate(UiNav.Up);
                if (keys.Down) _tree.Navigate(UiNav.Down);
                if (keys.Left) _tree.Navigate(UiNav.Left);
                if (keys.Right) _tree.Navigate(UiNav.Right);
                if (keys.Enter) _tree.Submit();
                if (keys.Escape)
                {
                    if (_tree.Focused != null) _tree.SetFocus(null);
                    else if (!_options.TestMode) Exit();
                }
            }
            if (keys.Screenshot) SaveScreenshot(Path.Combine(_options.EvidenceDir, "manual-" + (++_shotCount).ToString("00") + ".png"));

            SyncTextInput();
            base.Update(gameTime);
        }

        /// <summary>
        /// 월드 화면의 한 프레임. 눌린 키를 화면 쌓기에 먼저 넘기고, 가져가지 않은 상호작용 키는 이동이 막히지 않았을 때 월드로.
        /// 걷기는 누르고 있는 방향 키로, 이동을 막는 화면이 떠 있거나 글자를 치는 동안은 서 있는다.
        /// </summary>
        private void UpdateWorld(KeyFrame keys, GameTime gameTime)
        {
            ScreenStack s = Screens;
            if (keys.Tab) s.Key(ScreenKey.Tab, keys.Shift);
            if (keys.Up) s.Key(ScreenKey.Up, keys.Shift);
            if (keys.Down) s.Key(ScreenKey.Down, keys.Shift);
            if (keys.Left) s.Key(ScreenKey.Left, keys.Shift);
            if (keys.Right) s.Key(ScreenKey.Right, keys.Shift);
            if (keys.Enter) s.Key(ScreenKey.Enter, keys.Shift);
            if (keys.Escape) s.Key(ScreenKey.Escape, keys.Shift);
            if (keys.Inventory) s.Key(ScreenKey.Inventory, keys.Shift);
            if (keys.Status) s.Key(ScreenKey.Status, keys.Shift);
            bool interact = keys.Interact && !s.Key(ScreenKey.Interact, keys.Shift) && !s.MovementBlocked;

            float dt = _options.TestMode ? TestFrameSeconds : (float)gameTime.ElapsedGameTime.TotalSeconds;
            bool blocked = s.MovementBlockedWhile(keys.Shift);
            World.Update(blocked ? 0 : keys.MoveX, blocked ? 0 : keys.MoveY, dt);
            s.EnterMap(World.Session.Map.Id);
            s.SetInteractionTarget(TargetOnScreen());
            if (interact)
            {
                InteractionEvent? happened = World.Interact();
                if (happened.HasValue) s.Interaction(happened.Value);
            }
        }

        /// <summary>
        /// 앞의 상호작용 대상을 화면 논리 좌표로(지난 프레임의 카메라 기준). 눌러도 아무 일 없는 대상(자동문)은 넘기지 않는다.
        /// </summary>
        private InteractionTargetView? TargetOnScreen()
        {
            InteractionCandidate target = World.Interactions.Target;
            if (target == null || !target.Acts) return null;
            float zoom = World.Zoom / Fit.Scale;
            return new InteractionTargetView(target.Kind, target.Id,
                (target.X - World.CameraOffset.X) * zoom, (target.Y - World.CameraOffset.Y) * zoom, World.Current.World.Grid.CellSize * zoom);
        }

        /// <summary>글자 칸이 선택된 동안에만 입력기를 켠다.</summary>
        private void SyncTextInput()
        {
            var target = _tree.Focused as UiTextField;
            if (target == _textTarget) return;
            _textTarget = target;
            if (target == null) { _textInput.Stop(); return; }
            PixelRect rect = Fit.ToPixels(target.Rect);
            _textInput.Start(new Rectangle(rect.X, rect.Y, rect.W, rect.H));
        }

        protected override void Draw(GameTime gameTime)
        {
            if (Screens != null) Screens.Emit(_drawList, _measurer);
            else _tree.Emit(_drawList, _measurer);
            GraphicsDevice.SetRenderTarget(_scene);
            GraphicsDevice.Clear(Background);
            World?.Draw(_batch, _scene.Width, _scene.Height);
            _renderer.Draw(_drawList, Fit);
            GraphicsDevice.SetRenderTarget(null);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, null, null);
            _batch.Draw(_scene, Vector2.Zero, Color.White);
            _batch.End();
            base.Draw(gameTime);
        }

        /// <summary>마지막으로 그려진 화면 버퍼를 PNG로 저장한다.</summary>
        public void SaveScreenshot(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using FileStream stream = File.Create(path);
            _scene.SaveAsPng(stream, _scene.Width, _scene.Height);
        }

        protected override void UnloadContent()
        {
            World?.Dispose();
            _textInput?.Dispose();
            _renderer?.Dispose();
            _text?.Dispose();
            _scene?.Dispose();
            _batch?.Dispose();
            base.UnloadContent();
        }
    }
}
