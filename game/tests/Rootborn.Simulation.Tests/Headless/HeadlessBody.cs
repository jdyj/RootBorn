using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rootborn.Core.Interaction;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Start;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Simulation.Headless
{
    /// <summary>
    /// 창 없는 몸(자동 테스트): 슬롯 선택 화면부터 시작해 Host HostGame.UpdateWorld와 같은 순서로 한 프레임씩 돈다 —
    /// 포인터 → 키(화면 쌓기 → 키 배정 표) → 화면 시간(Tick) → 학생 시계(AdvanceTime, 시계를 멈추는 화면이 떠 있으면 멈춤) → 걷기(이동을 막는 화면이면 서 있음) → 맵 들어감 → 가구 방 → 상호작용.
    /// 입력은 화면 포인터 판정과 키 배정을 거친다. GameSession 명령을 부르지 않는다(가구 방에 들어갈 때 Host가 부르는 EnterHouse만 같게 부른다).
    /// 시간은 고정 간격(1/60초), 시작 시각·시드는 고정값이라 같은 입력은 같은 판이 된다.
    /// </summary>
    public sealed class HeadlessBody : IPlayBody, IFrameCost, IClockWatch
    {
        public const float FrameSeconds = 1f / 60f;
        public const long FixedUtcTicks = 638_600_000_000_000_000L; // 2024-??: 고정 시각(결정적 시작). 값 자체에는 뜻이 없다.
        public const int FixedTickCount = 20261112;
        private const int MaxFramesPerRun = 900;

        private readonly PlayAtlas _atlas;
        private readonly string _data;
        private readonly bool _ownsSaves;
        private readonly TestTextMeasurer _measurer = new TestTextMeasurer();
        private readonly DrawList _draw = new DrawList();
        private HeadlessWorld _world;
        private WorldSlot _slot;
        private HeadlessPlacementView _placementView;
        private FurniturePlacementScreen _placement;
        private string _houseMap;
        private float _pointerX = -100, _pointerY = -100;
        private bool _pointerDown;
        private int _eventCounter;

        public HeadlessBody(PlayAtlas atlas, string saveRoot = null)
        {
            _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
            _data = Path.Combine(atlas.RepoRoot, "data");
            _ownsSaves = saveRoot == null;
            SaveRoot = saveRoot ?? Path.Combine(Path.GetTempPath(), "rootborn-sim-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(SaveRoot);
            Text = GameText.LoadMerged(GameText.DefaultBaseLanguage, Path.Combine(_data, "localization"), Path.Combine(atlas.RepoRoot, "game", "localization"));
            Text.Language = GameText.DefaultBaseLanguage;
            var store = new FileSaveStore(SaveRoot);
            var menu = new StartMenu(new SaveSlots(store, DeleteSlotFolder), CreatorLooks.Load(_data), new BlankPreviewArt(), Enter, () => { }, () => FixedUtcTicks, () => FixedTickCount);
            Screens = NewStack(null);
            Screens.Open(ScreenCatalog.SaveSlotSelect, argument: menu);
            Settle();
        }

        public ScreenStack Screens { get; private set; }
        public GameText Text { get; }
        public GameSession Session { get; private set; }
        public bool InWorld => _world != null;
        public WorldSession Walk => _world?.Session;
        public IReadOnlyList<InteractionCandidate> Candidates => _world?.Interactions.Candidates ?? (IReadOnlyList<InteractionCandidate>)Array.Empty<InteractionCandidate>();
        public InteractionCandidate Target => _world?.Interactions.Target;
        public FurnitureRoom Room => _world?.Room;
        public IPlacementView PlacementView => _placementView;
        public float Width => 1920f;
        public float Height => 1080f;
        public long Frames { get; private set; }
        public float ClockSecondsPerFrame => FrameSeconds;
        public int CollisionViolations { get; private set; }
        public string SaveRoot { get; }
        public string SlotId { get; private set; }
        public (IPlacementGrid Grid, string Surface)? HouseEntered { get; private set; }

        public event Action<string> ScreenOpened;
        public event Action<InteractionEvent> Interacted;

        private ScreenStack NewStack(GameSession session)
        {
            var services = new ScreenServices(Text, session) { Definitions = _atlas.Definitions, Names = DisplayNames.Load(_data) };
            var stack = new ScreenStack(ScreenCatalog.Create(), services);
            stack.Layout(Width, Height);
            stack.Opened += s => ScreenOpened?.Invoke(s.Id);
            stack.Interacted += (e, _) => Interacted?.Invoke(e);
            return stack;
        }

        private void DeleteSlotFolder(string slot)
        {
            if (!SaveSlotMetadata.IsSafeSlotId(slot)) throw new ArgumentException("슬롯 id: " + slot);
            string dir = Path.Combine(SaveRoot, slot);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        /// <summary>시작 화면에서 고른 슬롯으로 들어간다(Host HostGame.EnterSlot + StartScreens와 같은 순서).</summary>
        private bool Enter(StartEntry entry)
        {
            var store = new FileSaveStore(SaveRoot);
            var slot = new WorldSlot(store, entry.SlotId, WorldTravelData.From(_atlas.Definitions));
            WorldSlotStart start = slot.Begin(FixedUtcTicks);
            if (!start.Ok) return false;
            GameSession session = GameSession.Start(_atlas.Definitions, GameStartSettings.FromDefinitions(_atlas.Definitions), store, entry.SlotId, SaveFileNames.DefaultPlayerId,
                action => action + "-" + (++_eventCounter).ToString(CultureInfo.InvariantCulture));
            _slot = slot;
            SlotId = entry.SlotId;
            Session = session;
            _world = new HeadlessWorld(_atlas, slot, start.Entry.SceneName, start.Entry.SpawnId, () => session.StudentDay, () => session.StudentHour);
            Screens = NewStack(session);
            Screens.EnterMap(_world.Session.Map.Id, Features());
            Screens.OpenHud();
            _placementView = new HeadlessPlacementView(() => _world.Room, Width, Height);
            Screens.Opened += s =>
            {
                if (s is FurniturePlacementScreen p) { _placement = p; p.Attach(new FurnitureArt(_data), _placementView); }
            };
            DayBaseline.For(session);
            SyncHouse();
            return true;
        }

        private string _featuresMap;
        private string[] _features = Array.Empty<string>();

        /// <summary>지금 맵의 성질(맵이 바뀔 때만 다시 만든다 — Host HostGame.MapFeaturesHere와 같다).</summary>
        private string[] Features()
        {
            string map = _world.Session.Map.Id;
            if (map == _featuresMap) return _features;
            _featuresMap = map;
            _features = string.IsNullOrEmpty(_world.HereData.PlacementSurface) ? Array.Empty<string>() : new[] { MapFeatures.FurniturePlacement };
            return _features;
        }

        private void SyncHouse()
        {
            string map = _world.Session.Map.Id;
            if (map == _houseMap) return;
            _houseMap = map;
            FurnitureRoom room = _world.Room;
            string surface = _world.HereData.PlacementSurface;
            if (room != null && !string.IsNullOrEmpty(surface))
            {
                Session.EnterHouse(room.Grid, surface);
                HouseEntered = (room.Grid, surface);
            }
            if (room == null) Screens.Close(ScreenCatalog.FurniturePlacement);
            RefreshFurniture();
        }

        private void RefreshFurniture()
        {
            FurnitureRoom room = _world.Room;
            if (room != null && Session.House != null) room.SetFurnitureBlocking(Session.House.BlockingCells);
        }

        // ---------- 한 프레임 ----------

        private ScreenKey? _keyThisFrame;
        private bool _interactThisFrame;

        private void Frame(int dx = 0, int dy = 0)
        {
            ScreenStack s = Screens;
            s.Pointer(_pointerX, _pointerY, _pointerDown, 0f);
            ScreenKey? key = _keyThisFrame;
            bool interact = _interactThisFrame;
            _keyThisFrame = null; _interactThisFrame = false;
            if (_world == null)
            {
                if (key.HasValue) s.Key(key.Value, false);
                Frames++;
                return;
            }
            if (key.HasValue) s.Key(key.Value, false);
            interact = interact && !s.Key(ScreenKey.Interact, false) && !s.MovementBlocked;
            s.Tick(FrameSeconds);
            AdvanceClock(s);
            bool blocked = s.MovementBlockedWhile(false);
            string before = _world.Session.Map.Id;
            _world.Update(blocked ? 0 : dx, blocked ? 0 : dy, FrameSeconds);
            s.EnterMap(_world.Session.Map.Id, Features());
            SyncHouse();
            if (_placement != null && _placement.IsOpen)
            {
                bool overUi = s.Tree.HitTest(_pointerX, _pointerY) != null;
                _placement.WorldPointer(_pointerX, _pointerY, _pointerDown, overUi);
                RefreshFurniture();
            }
            InteractionCandidate target = _world.Interactions.Target;
            s.SetInteractionTarget(target == null || !target.Acts ? (InteractionTargetView?)null : new InteractionTargetView(target.Kind, target.Id, Width / 2f, Height / 2f, 64f));
            if (interact)
            {
                InteractionEvent? happened = _world.Interactions.Press();
                if (happened.HasValue) s.Interaction(happened.Value);
            }
            Frames++;
            if (_world.Session.Map.Id == before && _world.Session.Map.Collision.AnyBlocked(_world.Session.Walker.Box)) CollisionViolations++;
        }

        /// <summary>
        /// 학생 시계: Host HostGame.UpdateWorld처럼 프레임 시간을 넘긴다(창은 늘 활성). 혼자인 방에서 시계를 멈추는 화면이 떠 있는데 시각이 움직이면
        /// 불변식 위반으로 센다(흐르는 시간 설계 3.1 — 시계를 멈출지는 GameSession이 정하고, 여기서는 그 결과만 본다).
        /// </summary>
        private void AdvanceClock(ScreenStack s)
        {
            GameSession session = Session;
            if (session == null) return;
            bool paused = s.PausesClock;
            int day = session.StudentDay, minutes = session.StudentTimeMinutes;
            int moved = session.AdvanceTime(FrameSeconds, paused);
            if (paused && session.IsSoloRoom && (moved != 0 || session.StudentDay != day || session.StudentTimeMinutes != minutes)) ClockViolations++;
            if (session.StudentTimeMinutes != minutes || session.StudentDay != day) MinuteFrames++;
        }

        /// <summary>혼자인 방에서 시계를 멈추는 화면이 떠 있는데 시계가 움직인 프레임 수.</summary>
        public int ClockViolations { get; private set; }

        /// <summary>시계의 분이 바뀐 프레임 수(분마다 HUD가 글자를 새로 만드는 프레임 — 할당 측정이 따로 센다).</summary>
        public long MinuteFrames { get; private set; }

        /// <summary>한 번 그린다(여러 줄 글자 높이가 글자 폭에 맞춰진다) — Host가 프레임마다 그리는 것 대신 행동마다.</summary>
        private void Settle()
        {
            Screens.Emit(_draw, _measurer);
            Screens.Layout(Width, Height);
        }

        // ---------- 입력 ----------

        public void Click(float x, float y)
        {
            _pointerX = x; _pointerY = y; _pointerDown = false;
            Frame();
            _pointerDown = true; Frame(); Frame();
            _pointerDown = false; Frame(); Frame();
            Settle();
        }

        public void PressKey(ScreenKey key)
        {
            if (key == ScreenKey.Interact) _interactThisFrame = true;
            else _keyThisFrame = key;
            if (key == ScreenKey.Space) _interactThisFrame = true; // Host: Space는 미니게임 키이자 상호작용 키
            Frame(); Frame();
            Settle();
        }

        public void TypeInto(float x, float y, string text)
        {
            Click(x, y);
            Screens.TextInput(text);
            Frame();
            PressKey(ScreenKey.Escape);
        }

        public void Wait(int frames)
        {
            for (int i = 0; i < frames; i++) Frame();
            Settle();
        }

        public void Interact() => PressKey(ScreenKey.Interact);

        /// <summary>
        /// 아무것도 누르지 않은 프레임(Host가 매 프레임 도는 화면 쌓기 Tick·포인터·걷기·맵·대상 고르기·학생 시계) 한 번에 새로 잡는 바이트(이 스레드).
        /// 벽시계 대신 흔들리지 않는 기준으로 매 프레임 쓰레기가 생기는지 본다. 그리기(Emit)는 넣지 않는다(Host가 그린다).
        /// 시계의 분이 바뀐 프레임(게임 1분 = 약 25프레임에 한 번 — 알림을 받은 HUD가 글자를 새로 만들고, 게임 1시간마다 저장한다)은 빼고 센다.
        /// 그 프레임의 가장 큰 값은 <see cref="MaxBytesInMinuteFrame"/>에 남는다.
        /// </summary>
        public double AllocatedBytesPerIdleFrame(int frames)
        {
            for (int i = 0; i < 5; i++) Frame(); // 처음 한 번 만드는 것(캐시)은 뺀다
            long idle = 0;
            int idleFrames = 0;
            for (int i = 0; i < frames; i++)
            {
                long minuteFrames = MinuteFrames;
                long before = GC.GetAllocatedBytesForCurrentThread();
                Frame();
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                if (MinuteFrames != minuteFrames) { if (bytes > MaxBytesInMinuteFrame) MaxBytesInMinuteFrame = bytes; continue; }
                idle += bytes;
                idleFrames++;
            }
            return idleFrames == 0 ? 0 : idle / (double)idleFrames;
        }

        /// <summary>분이 바뀐 프레임 한 번에 새로 잡은 바이트의 최대(AllocatedBytesPerIdleFrame이 잰 것).</summary>
        public long MaxBytesInMinuteFrame { get; private set; }

        public string Screenshot(string name) => null;

        // ---------- 걷기(PlayerDriver와 같은 방식: 칸 경로를 정하고 방향 키를 누르고 있는다) ----------

        private CellPos PlayerCell => CellPos.Containing(Walk.Walker.X, Walk.Walker.Y, Walk.Map.Grid.CellSize);

        private bool HoldUntil(int dx, int dy, float target)
        {
            string map = Walk.Map.Id;
            float Remaining() => dx != 0 ? (target - Walk.Walker.X) * dx : (target - Walk.Walker.Y) * dy;
            float step = Walk.Map.Grid.CellSize * 4f / 60f;
            Frame(); // 누름
            for (int frames = 0; Walk.Map.Id == map && Remaining() > step / 2f; frames++)
            {
                if (frames > MaxFramesPerRun || Screens.MovementBlocked) return false;
                Frame(dx, dy);
            }
            Frame(); // 뗌
            return Walk.Map.Id == map;
        }

        private bool FollowPath(Func<CellPos, bool> goal)
        {
            CellPos start = PlayerCell;
            List<CellPos> path = GridPath.Find(Walk.Map.Collision, start, goal);
            if (path == null) return false;
            int size = Walk.Map.Grid.CellSize;
            int i = 0;
            while (i < path.Count)
            {
                CellPos from = i == 0 ? start : path[i - 1];
                int dx = path[i].X - from.X, dy = path[i].Y - from.Y;
                int j = i;
                while (j + 1 < path.Count && path[j + 1].X - path[j].X == dx && path[j + 1].Y - path[j].Y == dy) j++;
                var center = CellPos.Center(path[j], size);
                if (!HoldUntil(dx, dy, dx != 0 ? center.X : center.Y)) return false;
                i = j + 1;
            }
            return true;
        }

        public bool WalkNear(float x, float y, float within)
        {
            int size = Walk.Map.Grid.CellSize;
            bool Close(CellPos c)
            {
                var center = CellPos.Center(c, size);
                float dx = center.X - x, dy = center.Y - y;
                return dx * dx + dy * dy <= within * within;
            }
            bool ok = Close(PlayerCell) || FollowPath(Close);
            Settle();
            return ok;
        }

        public bool WalkThroughDoor(DoorArea door)
        {
            string from = Walk.Map.Id;
            int size = Walk.Map.Grid.CellSize;
            var doorCells = new HashSet<CellPos>(CollisionInput.CellsUnder(door.Area, size));
            bool Near(CellPos c) => doorCells.Contains(c) || doorCells.Contains(new CellPos(c.X + 1, c.Y)) || doorCells.Contains(new CellPos(c.X - 1, c.Y))
                || doorCells.Contains(new CellPos(c.X, c.Y + 1)) || doorCells.Contains(new CellPos(c.X, c.Y - 1));
            if (FollowPath(Near) && Walk.Map.Id == from)
            {
                (int dx, int dy) = DoorDirection.Toward(door.Area, Walk.Walker.X, Walk.Walker.Y);
                for (int i = 0; i < 240 && Walk.Map.Id == from && !Screens.MovementBlocked; i++) Frame(dx, dy);
            }
            for (int i = 0; i < 60 && Walk.Map.Id == from; i++) Frame(); // 문이 열리는 0.35초
            Frame(); Frame();
            Settle();
            return Walk.Map.Id != from;
        }

        public void Dispose()
        {
            if (!_ownsSaves) return;
            try { Directory.Delete(SaveRoot, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>캐릭터 미리 보기 그림이 없는 시작 화면(창 없는 몸은 그림을 그리지 않는다).</summary>
    internal sealed class BlankPreviewArt : ICharacterPreviewArt
    {
        public bool TryStandingFrame(string sheetId, out PreviewFrame frame)
        {
            frame = new PreviewFrame("sheet:" + sheetId, new PixelRect(0, 0, 32, 64), 0.5f, 1f);
            return !string.IsNullOrEmpty(sheetId);
        }
    }

    /// <summary>글자 하나의 폭을 글자 크기의 절반으로 치는 측정기(화면 테스트와 같다).</summary>
    internal sealed class TestTextMeasurer : ITextMeasurer
    {
        public float Width(string text, float fontSize) => (text?.Length ?? 0) * fontSize * 0.5f;
        public float LineHeight(float fontSize) => fontSize;
    }
}
