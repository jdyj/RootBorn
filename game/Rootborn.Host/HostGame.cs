using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Host.Display;
using Rootborn.Host.Net;
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
using Rootborn.Screens.Day;
using Rootborn.World;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Settings;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Host
{
    /// <summary>
    /// 창과 게임 루프. 프레임마다 장치 계층(키보드, 포인터, 글자)을 한 번씩 읽어 UI에 값으로 넘기고,
    /// UI가 낸 그리기 명령을 창 크기의 화면 버퍼에 그린 뒤 창에 옮긴다. 규칙 판단은 여기에 쓰지 않는다.
    /// 월드 화면(--world, --slot)이면 월드를 먼저 그리고 그 위에 화면 쌓기(ScreenStack)를 올린다. 키는 화면 쌓기가 먼저 받고,
    /// 화면이 가져가지 않은 상호작용 키와 누르고 있는 방향 키를 월드에 넘긴다(이동을 막는 화면이 떠 있으면 넘기지 않는다).
    /// 월드의 상호작용 이벤트는 화면 쌓기의 등록 표(상호작용 종류 → 화면)로 넘긴다. --slot이면 GameSession으로 게임 한 판을 시작한다.
    /// 세이브 폴더만 주면(--slot 없이) 시작 화면(슬롯 선택 → 캐릭터 만들기)부터 띄우고, 슬롯을 고르면 그 슬롯으로 월드와 게임을 시작한다.
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
        private readonly DrawList _worldOverlay = new DrawList();
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
        /// <summary>월드 위의 화면들(월드 화면일 때), 또는 시작 화면(슬롯을 고르기 전).</summary>
        public ScreenStack Screens { get; private set; }
        /// <summary>시작 화면이 쓰는 것(시작 화면부터 띄웠을 때만).</summary>
        public StartMenu StartMenu { get; private set; }
        /// <summary>게임 한 판(--slot으로 시작했을 때만).</summary>
        public GameSession Session { get; private set; }
        public GameText Text { get; private set; }

        /// <summary>등록 표에 화면을 더 얹는다(테스트가 시험용 화면을 더할 때). 창을 띄우기 전에 정한다.</summary>
        public Action<ScreenRegistry> ExtraScreens;
        public UiTree Ui => _tree;
        /// <summary>UI 그리기(스킨, 찾지 못한 그림 목록).</summary>
        public UiRenderer Renderer => _renderer;

        /// <summary>마지막으로 그린 화면의 UI 그리기 명령 수(무거운 화면이 보이는 것만 그리는지 보는 흔들리지 않는 기준).</summary>
        public int DrawCommandCount => _drawList.Commands.Count;
        /// <summary>마지막으로 그린 화면 버퍼(스크린샷과 같은 그림).</summary>
        public RenderTarget2D Scene => _scene;

        /// <summary>글자 칸이 선택되어 입력기가 켜져 있다.</summary>
        public bool TextInputActive => _textInput != null && _textInput.Active;

        // ---------- 방(멀티플레이) ----------

        /// <summary>서버 주소로 들어가는 쪽(시작 화면이 있을 때 만든다). 방에 들어가지 않으면 프레임마다 아무것도 하지 않는다.</summary>
        public RoomConnection Room { get; private set; }
        /// <summary>같은 맵의 다른 참가자(방에 들어갔을 때만).</summary>
        public RemoteAvatars Remotes { get; private set; }
        /// <summary>내 PC에서 방을 여는 쪽(시작 화면이 있을 때 만든다). 방을 열지 않으면 프레임마다 아무것도 하지 않는다(서버 코드가 돌지 않는다).</summary>
        public RoomHosting Hosting { get; private set; }
        /// <summary>
        /// 방 접속이 쓰는 시각(초). 네트워크는 실제 시간으로 흐르므로(상대의 살아 있다는 표시, 전송의 시간 초과) 테스트 모드에서도 실제 시간이다.
        /// 다른 참가자의 걷기 칸은 프레임 dt로 돈다.
        /// </summary>
        private readonly Rootborn.Net.Room.StopwatchRoomClock _netClock = new Rootborn.Net.Room.StopwatchRoomClock();

        public HostGame(HostOptions options)
        {
            _options = options;
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = options.Width,
                PreferredBackBufferHeight = options.Height,
                SynchronizeWithVerticalRetrace = !options.TestMode,
                HardwareModeSwitch = false, // 전체 화면은 테두리 없는 창(Unity fullscreenMode 3) — 모니터 해상도를 바꾸지 않는다
            };
            IsFixedTimeStep = false;
            if (options.TestMode) InactiveSleepTime = TimeSpan.Zero;
            IsMouseVisible = true;
            Window.AllowUserResizing = !options.TestMode;
            Window.Title = "ROOTBORN";

            _windowedSize = (options.Width, options.Height);
            _minimumSize = DisplayChoice.MinimumSize(options.Width, options.Height);
            if (Settings.Settings.TryGetToggle(DisplayChoice.FullscreenSetting, out bool saved)) SavedFullscreen = saved;
            Display = new DisplayModeSetting(Settings, DisplayChoice.StartFullscreen(options.FullscreenRequested, options.SizeGiven, options.TestMode, SavedFullscreen));
            Display.Changed += ApplyDisplayMode;
            if (Fullscreen) PrepareFullscreen();
            Window.ClientSizeChanged += OnClientSizeChanged;
        }

        // ---------- 창 모드와 전체 화면 ----------

        private (int Width, int Height) _windowedSize;
        private readonly (int Width, int Height) _minimumSize;
        private bool _applyingSize;

        /// <summary>
        /// 지금 표시 모드가 테두리 없는 전체 화면인가(F11이나 설정 화면의 표시 모드 줄로 바꾼다). 테스트 모드는 이 값만 바꾸고 실제 창은 창 모드
        /// 그대로 둔다(테스트용 이음매 — 테스트가 사용자의 화면을 덮지 않는다).
        /// </summary>
        public bool Fullscreen => Display.Fullscreen;

        /// <summary>표시 모드. 화면 쌓기에 넘겨 설정 화면이 바꾸고, 바뀌면 ApplyDisplayMode가 창에 적용한다. 저장은 DisplayModeSetting이 한다.</summary>
        public DisplayModeSetting Display { get; }

        /// <summary>시작할 때 설정 파일에서 읽은 표시 모드(없으면 null).</summary>
        public bool? SavedFullscreen { get; }

        /// <summary>창 모드로 돌아갈 때의 창 크기(전체 화면으로 가기 전 크기, 또는 마지막으로 바꾼 크기).</summary>
        public (int Width, int Height) WindowedSize => _windowedSize;

        /// <summary>창을 이보다 작게 줄이지 못한다(1280x720, 그보다 작은 --size로 띄웠으면 그 크기).</summary>
        public (int Width, int Height) MinimumWindowSize => _minimumSize;

        /// <summary>창 모드 ↔ 테두리 없는 전체 화면. 고른 모드는 설정 파일에 남겨 다음 실행이 따른다(실행 인자가 있으면 인자가 먼저).</summary>
        public void ToggleFullscreen() => Display.Fullscreen = !Display.Fullscreen;

        /// <summary>표시 모드가 바뀌었다(F11이든 설정 화면이든): 창에 바로 적용한다. 테스트 모드는 실제 창을 바꾸지 않는다.</summary>
        private void ApplyDisplayMode(bool fullscreen)
        {
            if (_options.TestMode) return;
            if (fullscreen)
            {
                _windowedSize = (GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);
                PrepareFullscreen();
            }
            else
            {
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = _windowedSize.Width;
                _graphics.PreferredBackBufferHeight = _windowedSize.Height;
            }
            ApplySize();
        }

        /// <summary>테두리 없는 전체 화면: 화면 버퍼를 모니터 크기로.</summary>
        private void PrepareFullscreen()
        {
            DisplayMode mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = mode.Width;
            _graphics.PreferredBackBufferHeight = mode.Height;
            _graphics.IsFullScreen = true;
        }

        private void ApplySize()
        {
            _applyingSize = true;
            try { _graphics.ApplyChanges(); }
            finally { _applyingSize = false; }
            EnsureScene();
        }

        /// <summary>사람이 창 가장자리를 끌어 크기를 바꿨다: 화면 버퍼를 창에 맞춘다(배치는 다음 프레임의 EnsureScene이 다시 한다).</summary>
        private void OnClientSizeChanged(object sender, EventArgs e)
        {
            if (_applyingSize || Fullscreen || GraphicsDevice == null) return;
            Rectangle bounds = Window.ClientBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            (int w, int h) = DisplayChoice.ClampToMinimum(bounds.Width, bounds.Height, _minimumSize);
            _windowedSize = (w, h);
            PresentationParameters pp = GraphicsDevice.PresentationParameters;
            if (pp.BackBufferWidth == w && pp.BackBufferHeight == h) return;
            _graphics.PreferredBackBufferWidth = w;
            _graphics.PreferredBackBufferHeight = h;
            ApplySize();
        }

        protected override void Initialize()
        {
            base.Initialize();
            Sdl2Native.SetWindowMinimumSize(Window.Handle, _minimumSize.Width, _minimumSize.Height);
        }

        protected override void LoadContent()
        {
            _batch = new SpriteBatch(GraphicsDevice);
            _text = new TextRenderer(GraphicsDevice, _options.FontPath);
            _measurer = new HostTextMeasurer(_text);
            var atlases = new SpriteAtlases(_options.RepoRoot);
            _renderer = new UiRenderer(GraphicsDevice, _text, _options.UiTheme, new ImageCatalog(atlases, _options.UiTheme, ItemIcons.Load(_options.RepoRoot)), atlases);
            _textInput = new TextInputDevice(Window);
            if (_options.HostAddress != null && _options.StartMenu) { ShowStartMenu(); return; } // --host(+--slot)는 시작 화면에서 방 열기 창으로
            if (_options.SaveSlot != null && _options.JoinAddress == null) { StartFromSlotArgument(); return; } // --join과 함께면 시작 화면에서 들어가기 창으로
            if (_options.WorldActive)
            {
                World = new WorldScreen(GraphicsDevice, _options);
                StartScreens();
                SetTree(Screens.Tree);
            }
            else if (_options.StartMenu) ShowStartMenu();
            else ShowSample();
        }

        /// <summary>
        /// 문구를 읽는다(두 현지화 폴더를 합친다 — 언어 목록은 폴더의 파일). 언어는 이미 띄운 문구가 있으면 그 언어(설정에서 바꾼 것 포함),
        /// 처음이면 LanguageChoice(--lang → 저장된 설정 → 운영체제 언어 → 기준 언어).
        /// </summary>
        private GameText LoadText()
        {
            GameText text = GameText.LoadMerged(GameText.DefaultBaseLanguage, Path.Combine(_options.RepoRoot, "data", "localization"), Path.Combine(_options.RepoRoot, "game", "localization"));
            string language = Text?.Language
                ?? LanguageChoice.Pick(_options.Language, Settings.Settings.Language, _options.SystemLanguage, text.Languages).Language;
            text.Language = language;
            return text;
        }

        private SettingsStore _settings;

        /// <summary>슬롯과 무관한 설정(언어, 켬/끔). 세이브 폴더가 있으면 그 안의 설정 파일, 없으면 메모리에만.</summary>
        public SettingsStore Settings
        {
            get
            {
                if (_settings != null) return _settings;
                _settings = new SettingsStore(string.IsNullOrWhiteSpace(_options.SaveRoot) ? null : new FileSaveStore(_options.SaveRoot));
                if (_settings.LoadError != null) Console.Error.WriteLine("[설정] 설정 파일을 읽지 못해 기본값으로 시작한다: " + _settings.LoadError);
                return _settings;
            }
        }

        /// <summary>
        /// 시작 화면: 게임 한 판이 없는 화면 쌓기에 슬롯 선택을 연다. 슬롯 파일은 세이브 폴더의 FileSaveStore로 읽고 쓴다.
        /// 미리보기 그림은 월드와 같은 캐릭터 시트에서 온다.
        /// </summary>
        private void ShowStartMenu()
        {
            Text = LoadText();
            var store = new FileSaveStore(_options.SaveRoot);
            GameContent content = GameContent.Load(_options.RepoRoot);
            StartMenu = new StartMenu(new SaveSlots(store, slot => DeleteSlotFolder(store, slot)), CreatorLooks.Load(Path.Combine(_options.RepoRoot, "data")),
                new CharacterPreviewArt(content.Characters), EnterSlot, Exit);
            ScreenRegistry registry = ScreenCatalog.Create();
            ExtraScreens?.Invoke(registry);
            var services = new ScreenServices(Text, null) { Settings = Settings, Display = Display };
            services.KeyGuide = KeyGuideNames.Create();
            Screens = new ScreenStack(registry, services);
            StartMenu.Join = Room ??= CreateRoom();
            StartMenu.Host = Hosting ??= CreateHosting();
            Screens.Open(ScreenCatalog.SaveSlotSelect, argument: StartMenu);
            if (_options.JoinAddress != null && !_joinOffered)
            {
                // --join: 들어가기 창을 그 주소로 연다. --slot도 줬으면 그 인물로 바로 접속을 시작한다.
                _joinOffered = true;
                Screens.Open(ScreenCatalog.JoinServer, argument: new JoinServerOpen(StartMenu, _options.JoinAddress, _options.SaveSlot));
                if (_options.JoinOnStart) Room.Begin(_options.JoinAddress, _options.SaveSlot);
            }
            if (_options.HostAddress != null && !_hostOffered)
            {
                // --host: 방 열기 창을 그 주소로 연다. --slot도 줬으면 그 슬롯으로 바로 방을 열고 들어간다.
                _hostOffered = true;
                Screens.Open(ScreenCatalog.HostRoom, argument: new HostRoomOpen(StartMenu, _options.HostAddress, _options.SaveSlot));
                if (_options.HostOnStart) Hosting.Begin(_options.HostAddress, _options.MaxPlayers ?? Hosting.DefaultMaxPlayers, _options.SaveSlot);
            }
            SetTree(Screens.Tree);
        }

        private bool _joinOffered;
        private bool _hostOffered;

        /// <summary>
        /// 내 PC에서 방을 여는 쪽을 만든다. 방은 데디케이티드 서버와 같은 RoomServer이고, 내 게임은 들어가기와 같은 방 접속(Room)으로 들어간다.
        /// 방의 상태는 내 세이브 폴더의 그 슬롯에, 틱은 Host의 네트워크 시계로 센다. 달력은 서버와 같은 정의(registry.json 기본값의 달력 id).
        /// </summary>
        private RoomHosting CreateHosting()
        {
            var hosting = new RoomHosting(Room, () => new FileSaveStore(_options.SaveRoot), _netClock, () =>
            {
                Rootborn.Data.GameDefinitions definitions = GameContent.Load(_options.RepoRoot).Definitions;
                return definitions.Get<Rootborn.Core.StudentLife.LifeCalendar>(GameStartSettings.FromDefinitions(definitions).CalendarId);
            }, () => ReturnToStartMenu(null));
            hosting.Changed += () => Screens?.NotifyChanged(RoomHostText.Topic);
            return hosting;
        }

        /// <summary>
        /// 방 접속을 만든다. 버전은 서버(Rootborn.Server)와 같은 셈법(RoomVersions.ForProgram + Rootborn.Data.DataFingerprint),
        /// 신원은 슬롯 정보에서(GuestProgress.RoomIdentity), 방이 받아들이면 시작 화면에서 슬롯을 고른 것과 같게 그 슬롯으로 들어간다.
        /// </summary>
        private RoomConnection CreateRoom()
        {
            var room = new RoomConnection(
                () => Rootborn.Net.Room.RoomVersions.ForProgram(typeof(GameSession).Assembly, Rootborn.Data.DataFingerprint.Compute(Path.Combine(_options.RepoRoot, "data"))),
                new Rootborn.Net.Session.SessionClientSettings(),
                slot => GuestProgress.RoomIdentity(slot, new FileSaveStore(_options.SaveRoot).TryRead(slot, SaveFileNames.Metadata, out SaveSlotMetadata m) ? m : null),
                EnterSlot);
            room.PhaseChanged += () => Screens?.NotifyChanged(Rootborn.Screens.Start.RoomJoinText.Topic);
            room.PhaseChanged += () => Screens?.NotifyChanged(RoomHostText.Topic); // 내 PC에서 연 방도 이 접속으로 들어간다
            room.Lost += ReturnToStartMenu;
            return room;
        }

        /// <summary>
        /// 방과 끊겼다: 시작 화면으로 돌아가 이유를 알린다. 게임 한 판은 명령마다, 월드 자리는 맵에 들어갈 때마다 이미 저장했으므로
        /// 따로 저장할 것이 남아 있지 않다(GameSession·WorldSlot). 같은 주소로 다시 들어가기 창을 열 수 있게 주소는 남긴다.
        /// </summary>
        private void ReturnToStartMenu(string reason)
        {
            World?.Dispose();
            World = null;
            Session = null;
            Remotes = null;
            _placement = null;
            _houseMap = null;
            _featuresMap = null;
            _sessionDay = -1;
            _options.SaveSlot = null;
            ShowStartMenu();
            if (reason == null) { SetTree(Screens.Tree); return; } // 내가 방을 닫았다: 알림 없이 시작 화면
            Screens.Open(ScreenCatalog.RoomNotice, argument: new RoomNoticeOpen(reason));
            SetTree(Screens.Tree);
        }

        /// <summary>슬롯 폴더를 통째로 지운다(Unity SaveService.DeleteSlot). ISaveStore에 슬롯 지우기가 없어 여기서 한다.</summary>
        private static void DeleteSlotFolder(FileSaveStore store, string slot)
        {
            if (!Rootborn.Core.Save.SaveSlotMetadata.IsSafeSlotId(slot)) throw new ArgumentException("슬롯 id에 쓸 수 없는 글자가 있다: " + slot, nameof(slot));
            string dir = Path.Combine(store.RootDirectory, slot);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        /// <summary>
        /// 시작 화면에서 고른 슬롯으로 들어간다: --slot으로 띄운 것과 같게 월드와 게임 한 판을 시작하고 화면 쌓기를 월드 것으로 바꾼다.
        /// 시작하지 못하면(읽을 수 없는 세이브, 맞지 않는 자리) 아무것도 바꾸지 않고 false.
        /// </summary>
        private bool EnterSlot(StartEntry entry)
        {
            string before = _options.SaveSlot;
            _options.SaveSlot = entry.SlotId;
            WorldScreen world = null;
            try
            {
                world = new WorldScreen(GraphicsDevice, _options);
                World = world;
                StartScreens();
            }
            catch (Rootborn.Game.SaveSlotDamagedException damaged)
            {
                Console.Error.WriteLine("[시작] " + damaged.Message);
                world?.Dispose();
                World = null;
                Session = null;
                _options.SaveSlot = before;
                ShowSlotDamage(damaged);
                return false;
            }
            catch (Exception e) when (e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is System.Text.Json.JsonException)
            {
                Console.Error.WriteLine("[시작] 슬롯 " + entry.SlotId + "으로 들어가지 못했다: " + e.Message);
                world?.Dispose();
                World = null;
                Session = null;
                _options.SaveSlot = before;
                return false;
            }
            StartMenu = null;
            if (Room != null && Room.Phase == Rootborn.Screens.Start.RoomJoinPhase.Joined)
                World.Remotes = Remotes = new RemoteAvatars(World.Content.Characters, World.Content.Wearables);
            SetTree(Screens.Tree);
            return true;
        }

        /// <summary>
        /// --slot으로 띄웠을 때: 그 슬롯으로 월드와 게임 한 판을 시작한다. 슬롯이 손상돼 시작을 거절하면(SaveSlotDamagedException)
        /// 슬롯 선택 화면을 띄우고 그 위에 손상 알림을 연다(돌아가기만 있다). 슬롯 파일은 바꾸지 않는다.
        /// </summary>
        private void StartFromSlotArgument()
        {
            try
            {
                World = new WorldScreen(GraphicsDevice, _options);
                StartScreens();
                SetTree(Screens.Tree);
            }
            catch (Rootborn.Game.SaveSlotDamagedException damaged)
            {
                Console.Error.WriteLine("[시작] " + damaged.Message);
                World?.Dispose();
                World = null;
                Session = null;
                _options.SaveSlot = null;
                ShowStartMenu();
                ShowSlotDamage(damaged);
            }
        }

        /// <summary>손상된 슬롯 알림을 지금 화면 쌓기(슬롯 선택) 위에 연다. 무엇이 문제인지는 예외의 Problems가 정한다.</summary>
        private void ShowSlotDamage(Rootborn.Game.SaveSlotDamagedException damaged)
        {
            SlotDamage = damaged;
            Screens?.Open(ScreenCatalog.SaveSlotDamaged, argument: damaged);
        }

        /// <summary>마지막으로 알린 손상 슬롯(테스트와 로그용). 없으면 null.</summary>
        public Rootborn.Game.SaveSlotDamagedException SlotDamage { get; private set; }

        /// <summary>
        /// 화면 쌓기를 만든다. 슬롯으로 시작했으면 정의(월드 화면이 읽은 것과 같은 data/)로 GameSession을 시작해 화면이 부를 명령으로 넘긴다.
        /// 세이브 폴더 위치는 실행 인자에서만 온다.
        /// </summary>
        private void StartScreens()
        {
            Text = LoadText();
            if (_options.SaveSlot != null) Session = StartSession();
            if (Session != null && _options.DayCopies.HasValue) Session.KeepDayCopies(_options.DayCopies.Value);
            ScreenRegistry registry = ScreenCatalog.Create();
            ExtraScreens?.Invoke(registry);
            var services = new ScreenServices(Text, Session) { Definitions = World.Content.Definitions, Settings = Settings, Display = Display, Names = DisplayNames.Load(Path.Combine(_options.RepoRoot, "data")) };
            services.KeyGuide = KeyGuideNames.Create();
            services.RoomHost = Hosting; // 내 PC에서 연 방이 열려 있으면 설정 화면에 한 줄
            if (Session != null)
                services.Wardrobe = new HostWardrobe(World, Session, new FileSaveStore(_options.SaveRoot), _options.SaveSlot, SaveFileNames.DefaultPlayerId, _options.RepoRoot);
            var screens = new ScreenStack(registry, services);
            screens.EnterMap(World.Session.Map.Id, MapFeaturesHere());
            Screens = screens;
            Screens.OpenHud();
            ConnectFurniture(Session);
            if (Session != null)
            {
                // 학생 시각·날이 NPC 자리를, 학생 날·성인 여부가 성장 단계를 정한다(Unity 판과 같음). 낮밤 빛은 HUD 시계와 같은 방의 때
                // (때의 대표 시각, GameSession.RoomShownMinutes)를 따른다 — Unity 판은 빛도 분 단위 학생 시각을 따랐다.
                World.FollowStudentTime(() => Session.StudentDay, () => Session.StudentHour, () => RoomLightHour(Session));
                DayBaseline.For(Session); // 하루 결과의 퀘스트·물건 변화 기준점(Unity: 마을 씬을 불러올 때)
                SyncSession();
            }
        }

        /// <summary>HUD 시계가 보이는 시각(방의 때의 대표 시각)을 0~24시로.</summary>
        private static float RoomLightHour(GameSession session)
        {
            const int Day = 24 * 60;
            return ((session.RoomShownMinutes % Day) + Day) % Day / 60f;
        }

        /// <summary>
        /// 게임 한 판을 시작한다. 손님 저장을 받는 방에 들어갔으면 방이 내려 준 세이브와 공유 조각, 방의 시계로(GuestProgress.RoomStoreFor),
        /// 아니면 이 PC의 그 슬롯으로. 방의 시계가 바뀌면 SyncWithRoom을 부른다(다른 사람이 때를 끝냄, 방에 없던 날).
        /// </summary>
        private GameSession StartSession()
        {
            var definitions = World.Content.Definitions;
            var settings = GameStartSettings.FromRegistry(Path.Combine(_options.RepoRoot, "data"));
            var link = Room != null && Room.Phase == Rootborn.Screens.Start.RoomJoinPhase.Joined ? Room.Link : null;
            if (link == null || !link.TakesGuestSaves)
                return GameSession.Start(definitions, settings, GuestProgress.LocalStoreFor(_options.SaveRoot), _options.SaveSlot, GuestProgress.SavePlayerId);

            var time = Room.CreateRoomTime(definitions.Get<Rootborn.Core.StudentLife.LifeCalendar>(settings.CalendarId));
            var session = GameSession.Start(definitions, settings, GuestProgress.RoomStoreFor(link), Rootborn.Net.Room.RoomGuestLink.Slot, Room.PlayerId,
                room: time, shares: link.Shares);
            time.Changed += () => session.SyncWithRoom();
            return session;
        }

        private string _featuresMap;
        private string[] _features = Array.Empty<string>();

        /// <summary>지금 맵의 성질(화면이 맵 id 대신 본다). 맵 데이터(interactions.json)에서 읽고, 맵이 바뀔 때만 다시 만든다.</summary>
        private string[] MapFeaturesHere()
        {
            string map = World.Session.Map.Id;
            if (map == _featuresMap) return _features;
            _featuresMap = map;
            _features = string.IsNullOrEmpty(World.HereData.PlacementSurface) ? Array.Empty<string>() : new[] { MapFeatures.FurniturePlacement };
            return _features;
        }

        private int _sessionDay = -1;

        /// <summary>
        /// 게임 한 판과 월드를 맞춘다(프레임마다, 바뀐 것만): 날이 바뀌면 성장 단계. 가구 방에 들어오는 일은 SyncHouse가 한다.
        /// 맵에 들어갈 때 장소 방문은 기록하지 않는다(Unity 판은 장소 지점을 눌렀을 때만 기록했다).
        /// </summary>
        private void SyncSession()
        {
            if (Session == null) return;
            if (Session.StudentDay != _sessionDay)
            {
                _sessionDay = Session.StudentDay;
                if (Session.DayCopyError != null) Console.Error.WriteLine("[세이브] 날마다 남기는 슬롯 사본을 쓰지 못했다(그날 저장은 끝났다): " + Session.DayCopyError.Message);
                World.SetGrowthStage(Session.GrowthStageId());
            }
        }

        // ---------- 가구 배치 (묶음 7) ----------

        private string _houseMap;
        private FurniturePlacementScreen _placement;

        /// <summary>
        /// 가구 배치를 잇는다: 가구 방(interactions.json의 placementSurface)에 들어오면 GameSession.EnterHouse(그 맵의 방 칸, 면 id)
        /// (Unity AuthoredHousePlacementBinding.Bind), 게임 명령이 월드를 바꾸면 놓인 가구 그림과 충돌을 다시 맞추고,
        /// 배치 화면이 열리면 배치 카메라를 켠다(Unity HousePlacementCameraRuntimeInstaller.SetPlacementMode).
        /// </summary>
        private void ConnectFurniture(GameSession session)
        {
            var view = new HostPlacementView(World, () => Fit.Scale);
            Screens.Opened += screen =>
            {
                if (!(screen is FurniturePlacementScreen placement)) return;
                _placement = placement;
                placement.Attach(World.FurnitureArt, view);
                FurnitureRoom room = World.Room;
                if (room != null)
                {
                    var grid = room.Map.Grid;
                    World.PlacementCamera.Begin(grid.PixelWidth, grid.PixelHeight, grid.CellSize, _scene == null ? 16f / 9f : _scene.Width / (float)_scene.Height);
                }
            };
            Screens.Closed += screen =>
            {
                if (screen is FurniturePlacementScreen) World.PlacementCamera.End();
            };
            if (session != null)
                session.Changed += topic => { if (topic == ChangeTopics.Furniture || topic == ChangeTopics.World) RefreshFurniture(); };
            SyncHouse();
        }

        /// <summary>맵이 바뀌었을 때만: 가구 방이면 EnterHouse(들어올 때마다 저장된 배치를 다시 읽는다). 맵 크기는 맵에서 읽는다.</summary>
        private void SyncHouse()
        {
            string map = World.Session.Map.Id;
            if (map == _houseMap) return;
            _houseMap = map;
            FurnitureRoom room = World.Room;
            string surface = World.HereData.PlacementSurface;
            if (room != null && Session != null && !string.IsNullOrEmpty(surface)) Session.EnterHouse(room.Grid, surface);
            if (room == null && World.PlacementCamera.Active) Screens.Close(ScreenCatalog.FurniturePlacement);
            RefreshFurniture();
        }

        /// <summary>놓인 가구로 월드 그림과 캐릭터 충돌을 맞춘다(바뀔 때만 부른다).</summary>
        private void RefreshFurniture()
        {
            FurnitureRoom room = World.Room;
            var house = Session?.House;
            if (room != null && house != null)
            {
                room.SetFurnitureBlocking(house.BlockingCells);
                World.SetPlacedFurniture(house.Placed);
            }
            else World.SetPlacedFurniture(null);
        }

        /// <summary>배치 중의 한 프레임: 카메라(방향 키·WASD 이동, +/- 확대, F 전체 보기, UI 밖 휠)와 UI 밖 포인터를 배치 화면에.</summary>
        private void UpdatePlacement(KeyFrame keys, PointerState pointer, float dt)
        {
            if (_placement == null || !_placement.IsOpen) return;
            float x = pointer.X / Fit.Scale, y = pointer.Y / Fit.Scale;
            bool overUi = _tree.HitTest(x, y) != null;
            PlacementCamera camera = World.PlacementCamera;
            if (camera.Active && !Screens.TextInputFocused)
            {
                camera.Pan(keys.MoveX, keys.MoveY, dt);
                camera.KeyboardZoom((keys.ZoomOutHeld ? 1 : 0) - (keys.ZoomInHeld ? 1 : 0), dt);
                if (keys.FullView) camera.FullView();
                if (!overUi) camera.Wheel(pointer.Wheel);
            }
            _placement.WorldPointer(x, y, pointer.LeftDown, overUi);
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

        /// <summary>창 크기를 바꾼다(창 모드). 최소 크기보다 작게 주면 최소 크기에서 멈춘다.</summary>
        public void Resize(int width, int height)
        {
            (width, height) = DisplayChoice.ClampToMinimum(width, height, _minimumSize);
            _windowedSize = (width, height);
            if (Fullscreen && !_options.TestMode) return; // 전체 화면이면 창 모드로 돌아갈 때 쓴다
            _graphics.PreferredBackBufferWidth = width;
            _graphics.PreferredBackBufferHeight = height;
            ApplySize();
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
            if (keys.ToggleDisplay) ToggleFullscreen();
            float frameSeconds = _options.TestMode ? TestFrameSeconds : (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (Room != null && Room.Phase == Rootborn.Screens.Start.RoomJoinPhase.Connecting)
                Room.UpdateJoining(_netClock.Now); // 들어가면 이 안에서 월드로 바뀐다
            if (Screens != null && World != null)
            {
                UpdateWorld(keys, gameTime, pointer);
                if (Room != null && Room.InRoom) UpdateRoom(frameSeconds);
            }
            else if (Screens != null) UpdateMenu(keys);
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
            Hosting?.Update(); // 내 PC에서 연 방의 틱(내 접속을 돌린 뒤). 방을 열지 않았으면 아무것도 하지 않는다
            if (keys.Screenshot) SaveScreenshot(Path.Combine(_options.EvidenceDir, "manual-" + (++_shotCount).ToString("00") + ".png"));

            SyncTextInput();
            base.Update(gameTime);
        }

        /// <summary>시작 화면의 한 프레임: 키는 화면 쌓기로만 간다(걸을 캐릭터가 없다).</summary>
        private void UpdateMenu(KeyFrame keys)
        {
            ScreenStack s = Screens;
            if (keys.Tab) s.Key(ScreenKey.Tab, keys.Shift);
            if (keys.Up) s.Key(ScreenKey.Up, keys.Shift);
            if (keys.Down) s.Key(ScreenKey.Down, keys.Shift);
            if (keys.Left) s.Key(ScreenKey.Left, keys.Shift);
            if (keys.Right) s.Key(ScreenKey.Right, keys.Shift);
            if (keys.Enter) s.Key(ScreenKey.Enter, keys.Shift);
            if (keys.Escape) s.Key(ScreenKey.Escape, keys.Shift);
            if (keys.Help) s.Key(ScreenKey.Help, keys.Shift);
        }

        /// <summary>
        /// 월드 화면의 한 프레임. 눌린 키를 화면 쌓기에 먼저 넘기고, 가져가지 않은 상호작용 키는 이동이 막히지 않았을 때 월드로.
        /// 걷기는 누르고 있는 방향 키로, 이동을 막는 화면이 떠 있거나 글자를 치는 동안은 서 있는다.
        /// </summary>
        private void UpdateWorld(KeyFrame keys, GameTime gameTime, PointerState pointer)
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
            if (keys.LocationLog) s.Key(ScreenKey.LocationLog, keys.Shift);
            if (keys.Help) s.Key(ScreenKey.Help, keys.Shift);
            // 미니게임 키는 상호작용 키보다 먼저 넘긴다(Space는 두 묶음에 다 든다).
            if (keys.Space) s.Key(ScreenKey.Space, keys.Shift);
            if (keys.Rotate) s.Key(ScreenKey.Rotate, keys.Shift);
            if (keys.AltLeft) s.Key(ScreenKey.AltLeft, keys.Shift);
            if (keys.AltRight) s.Key(ScreenKey.AltRight, keys.Shift);
            bool interact = keys.Interact && !s.Key(ScreenKey.Interact, keys.Shift) && !s.MovementBlocked;

            float dt = _options.TestMode ? TestFrameSeconds : (float)gameTime.ElapsedGameTime.TotalSeconds;
            s.Tick(dt); // 실시간 화면(미니게임)의 시간은 이 dt로만 흐른다
            bool blocked = s.MovementBlockedWhile(keys.Shift) || World.Feel.BlocksInput;
            World.Update(blocked ? 0 : keys.MoveX, blocked ? 0 : keys.MoveY, dt);
            s.EnterMap(World.Session.Map.Id, MapFeaturesHere());
            SyncSession();
            SyncHouse();
            UpdatePlacement(keys, pointer, dt);
            s.SetInteractionTarget(TargetOnScreen());
            // 마우스 왼클릭: UI 위(모달이 떠 있으면 막는 판)가 아니고 막히지 않았으면 누른 쪽을 보고 E와 같은 상호작용(WorldFeel.Click).
            bool overUi = _tree.HitTest(pointer.X / Fit.Scale, pointer.Y / Fit.Scale) != null;
            if (World.Feel.Click(pointer, overUi, s.MovementBlocked || (_placement != null && _placement.IsOpen), World)) interact = true;
            if (interact)
            {
                InteractionEvent? happened = World.Interact();
                if (happened.HasValue) s.Interaction(happened.Value);
            }
        }

        /// <summary>
        /// 방에 들어가 있을 때의 한 프레임: 내 몸·겉모습을 넘기고(보내는 횟수는 Net 설정) 같은 맵의 남을 받아 걷기 칸을 돌린다.
        /// 끊기면 RoomConnection.Lost가 시작 화면으로 돌려보낸다.
        /// </summary>
        private void UpdateRoom(float dt)
        {
            Rootborn.Core.World.Walker walker = World.Session.Walker;
            string map = World.Session.Map.Id;
            double now = _netClock.Now;
            if (!Room.UpdateInRoom(now, map, walker.X, walker.Y, walker.Facing, walker.Moving, World.PlayerLook.Code)) return;
            Remotes?.Update(Room.PlayersOn(map, now), dt);
        }

        /// <summary>
        /// 앞의 상호작용 대상을 화면 논리 좌표로(지난 프레임의 카메라 기준). 눌러도 아무 일 없는 대상(자동문)은 넘기지 않는다.
        /// </summary>
        private InteractionTargetView? TargetOnScreen()
        {
            InteractionCandidate target = World.Interactions.Target;
            if (target == null || !target.Acts) return null;
            float zoom = World.Scale / Fit.Scale;
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
            if (_placement != null && _placement.IsOpen)
            {
                // 가구 배치 미리보기는 월드의 한 층: 월드 다음, 화면들(HUD·패널) 앞에.
                _worldOverlay.Clear();
                _placement.EmitWorldOverlay(_worldOverlay);
                _renderer.Draw(_worldOverlay, Fit);
            }
            _renderer.Draw(_drawList, Fit);
            GraphicsDevice.SetRenderTarget(null);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, null, null);
            _batch.Draw(_scene, Vector2.Zero, Color.White);
            _batch.End();
            base.Draw(gameTime);
            ExitAfterFramesIfAsked();
        }

        private int _drawnFrames;

        /// <summary>--frames N: N번 그린 뒤 (--screenshot이 있으면 그 화면을 저장하고) 끝낸다.</summary>
        private void ExitAfterFramesIfAsked()
        {
            if (_options.ExitAfterFrames <= 0 || ++_drawnFrames < _options.ExitAfterFrames) return;
            if (_options.ScreenshotPath != null) SaveScreenshot(_options.ScreenshotPath);
            _options.ExitAfterFrames = 0; // 한 번만
            Exit();
        }

        /// <summary>
        /// 마지막으로 그려진 화면 버퍼를 PNG로 저장한다. 알파는 모두 불투명(255)으로 쓴다: 장면 버퍼는 반투명 그림을 NonPremultiplied로 섞으며
        /// 알파가 깎이는데, 창에는 Opaque로 복사해 보이지 않던 것이 PNG에서는 바탕이 비쳐 보인다(창과 같은 그림이 되게 한다).
        /// 저장할 때만 도는 길이라 픽셀 배열을 그때 만든다.
        /// </summary>
        public void SaveScreenshot(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var pixels = new Color[_scene.Width * _scene.Height];
            _scene.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++) pixels[i].A = 255;
            using var opaque = new Texture2D(GraphicsDevice, _scene.Width, _scene.Height, false, SurfaceFormat.Color);
            opaque.SetData(pixels);
            using FileStream stream = File.Create(path);
            opaque.SaveAsPng(stream, opaque.Width, opaque.Height);
        }

        /// <summary>게임을 끈다: 방에 들어가 있으면 나간다고 알린다(서버가 시간 초과를 기다리지 않는다).</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Room?.Dispose();
                Room = null;
                Hosting?.Dispose(); // 들어와 있던 사람에게 닫혔다고 알리고 저장한다
                Hosting = null;
            }
            base.Dispose(disposing);
        }

        protected override void UnloadContent()
        {
            Room?.Leave();
            Hosting?.ShutDownRoom();
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
