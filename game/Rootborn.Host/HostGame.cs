using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Host.Input;
using Rootborn.Host.Rendering;
using Rootborn.Host.Sample;
using Rootborn.Host.Text;
using Rootborn.Host.World;
using Rootborn.Ui;

namespace Rootborn.Host
{
    /// <summary>
    /// 창과 게임 루프. 프레임마다 장치 계층(키보드, 포인터, 글자)을 한 번씩 읽어 UI에 값으로 넘기고,
    /// UI가 낸 그리기 명령을 창 크기의 화면 버퍼에 그린 뒤 창에 옮긴다. 규칙 판단은 여기에 쓰지 않는다.
    /// 월드 화면(--world)이면 누르고 있는 방향 키를 월드에 넘기고 월드를 먼저 그린다. 테스트 모드는 프레임마다 1/60초씩 흐른다.
    /// </summary>
    public sealed class HostGame : Game
    {
        private static readonly Color Background = new Color(24, 22, 30);
        private const float TestFrameSeconds = 1f / 60f;

        private readonly HostOptions _options;
        private readonly GraphicsDeviceManager _graphics;
        private readonly KeyboardDevice _keyboard = new KeyboardDevice();
        private readonly DrawList _drawList = new DrawList();
        private SpriteBatch _batch;
        private RenderTarget2D _scene;
        private Texture2D _sheet;
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
        public UiTree Ui => _tree;

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
            using (FileStream stream = File.OpenRead(_options.UiSheetPath))
                _sheet = Texture2D.FromStream(GraphicsDevice, stream); // PNG를 그 자리에서 읽는다. 변환 단계 없음
            _text = new TextRenderer(GraphicsDevice, _options.FontPath);
            _measurer = new HostTextMeasurer(_text);
            _renderer = new UiRenderer(GraphicsDevice, _sheet, _text);
            _textInput = new TextInputDevice(Window);
            if (_options.World != null)
            {
                World = new WorldScreen(GraphicsDevice, _options);
                SetTree(new UiTree());
            }
            else ShowSample();
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
            _tree.Layout(Fit.LogicalWidth, Fit.LogicalHeight);
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
            if (World != null)
            {
                float dt = _options.TestMode ? TestFrameSeconds : (float)gameTime.ElapsedGameTime.TotalSeconds;
                World.Update(keys.MoveX, keys.MoveY, dt);
            }
            if (keys.Screenshot) SaveScreenshot(Path.Combine(_options.EvidenceDir, "manual-" + (++_shotCount).ToString("00") + ".png"));

            SyncTextInput();
            base.Update(gameTime);
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
            _tree.Emit(_drawList, _measurer);
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
            _sheet?.Dispose();
            _batch?.Dispose();
            base.UnloadContent();
        }
    }
}
