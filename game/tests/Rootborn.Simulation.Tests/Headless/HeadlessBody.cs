using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rootborn.Core.Interaction;
using Rootborn.Core.NpcMovement;
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

        private readonly IHeadlessSeat _seat;
        private bool _inSeatFrame;

        /// <param name="seat">같이 하는 방의 자리(없으면 혼자 하기). 있으면 슬롯을 고를 때 방에 들어간 게임 한 판을 받고, 프레임마다 방을 돌린다.</param>
        public HeadlessBody(PlayAtlas atlas, string saveRoot = null, IHeadlessSeat seat = null)
        {
            _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
            _data = Path.Combine(atlas.RepoRoot, "data");
            _seat = seat;
            _ownsSaves = saveRoot == null;
            SaveRoot = saveRoot ?? Path.Combine(Path.GetTempPath(), "rootborn-sim-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(SaveRoot);
            Text = GameText.LoadMerged(GameText.DefaultBaseLanguage, Path.Combine(_data, "localization"), Path.Combine(atlas.RepoRoot, "game", "localization"));
            Text.Language = GameText.DefaultBaseLanguage;
            OpenStart();
        }

        private void OpenStart()
        {
            var store = new FileSaveStore(SaveRoot);
            var menu = new StartMenu(new SaveSlots(store, DeleteSlotFolder), CreatorLooks.Load(_data), new BlankPreviewArt(), Enter, () => { }, () => FixedUtcTicks, () => FixedTickCount);
            Screens = NewStack(null);
            Screens.Open(ScreenCatalog.SaveSlotSelect, argument: menu);
            Settle();
        }

        /// <summary>
        /// 방과 끊겼으면 시작 화면으로 돌아간다(Host RoomConnection.Lost → 시작 화면). 같이 하는 방의 달리는 쪽이 걸음 사이에 부른다
        /// (걸음 도중에 몸이 사라지지 않게 — Host는 그 프레임에 돌아간다). 돌아갔으면 true.
        /// </summary>
        public bool ReturnToStartIfLost()
        {
            if (_seat == null || Session == null || _seat.Connected) return false;
            Session = null;
            _world = null;
            _slot = null;
            _placement = null;
            _houseMap = null;
            _featuresMap = null;
            _furnitureDirty = true;
            HouseEntered = null;
            HouseFrame = null;
            OpenStart();
            ScreenOpened?.Invoke(ScreenCatalog.SaveSlotSelect);
            return true;
        }

        /// <summary>다른 사람이 걷는 동안 이 몸이 가만히 선 한 프레임(같이 하는 방이 부른다 — 방을 다시 돌리지 않는다).</summary>
        public void IdleFrame()
        {
            _inSeatFrame = true;
            try { Frame(); }
            finally { _inSeatFrame = false; }
        }

        public ScreenStack Screens { get; private set; }
        public GameText Text { get; }
        public GameSession Session { get; private set; }
        public bool InWorld => _world != null;
        public WorldSession Walk => _world?.Session;
        public IReadOnlyList<InteractionCandidate> Candidates => _world?.Interactions.Candidates ?? (IReadOnlyList<InteractionCandidate>)Array.Empty<InteractionCandidate>();
        public InteractionCandidate Target => _world?.Interactions.Target;
        /// <summary>걷는 NPC(월드에 들어가기 전에는 null). 시험은 TryPose로 NPC를 찾아간다.</summary>
        public NpcStage Npcs => _world?.Npcs;
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

        /// <summary>마지막으로 들어선 짓는 집의 꾸밈 틀(이 판이 붙인 것과 같은 객체). 짓는 집에 들어선 적이 없으면 null.</summary>
        public Rootborn.Core.Building.HouseBuildFrame HouseFrame { get; private set; }

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
            Func<string, string> newKey = action => action + "-" + (++_eventCounter).ToString(CultureInfo.InvariantCulture);
            GameSession session = _seat != null ? _seat.StartSession(entry.SlotId, newKey)
                : GameSession.Start(_atlas.Definitions, GameStartSettings.FromDefinitions(_atlas.Definitions), store, entry.SlotId, SaveFileNames.DefaultPlayerId, newKey);
            if (session == null) return false; // 아직 방에 들어가 있지 않다(시작 화면에 머문다)
            session.Changed += topic => { if (topic == GameChangeTopics.Furniture || topic == GameChangeTopics.Storage) _furnitureDirty = true; };
            _slot = slot;
            SlotId = entry.SlotId;
            Session = session;
            _world = new HeadlessWorld(_atlas, slot, start.Entry.SceneName, start.Entry.SpawnId, () => session.StudentDay, () => session.DayMinutesExact);
            Screens = NewStack(session);
            Screens.EnterMap(_world.Session.Map.Id, Features());
            Screens.OpenHud();
            _placementView = new HeadlessPlacementView(() => _world.Room, Width, Height);
            Screens.Opened += s =>
            {
                if (s is FurniturePlacementScreen p) { _placement = p; p.Attach(new FurnitureArt(_data), _placementView); }
            };
            DayBaseline.For(session);
            session.Changed += topic => { if (Session == session && (topic == ChangeTopics.Furniture || topic == ChangeTopics.World)) RefreshFurniture(); }; // Host ConnectFurniture와 같다(방과 끊겨 시작 화면으로 돌아간 뒤의 옛 판 알림은 버린다)
            SyncHouse();
            _world.AttachHouseFurniture(session);
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
            SyncHouseBuild(map);
            _furnitureDirty = true; // 맵이 바뀌었다: 놓인 상자 후보를 이 맵 기준으로 다시(가구 방 밖이면 비운다 — Host RefreshFurniture는 맵이 바뀔 때마다 SetPlacedFurniture)
            RefreshFurniture();
        }

        private readonly Dictionary<string, Rootborn.Core.Building.HouseBuildFrame> _buildFrames = new Dictionary<string, Rootborn.Core.Building.HouseBuildFrame>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Host HostGame.SyncHouseBuild와 같다: 짓는 집(맵 성질 build)에 들어서면 꾸밈 틀을 붙이고, 아닌 맵이면 꾸미기를 닫는다. 틀은 맵마다 한 번만 만든다.</summary>
        private void SyncHouseBuild(string mapId)
        {
            if (!_buildFrames.TryGetValue(mapId, out Rootborn.Core.Building.HouseBuildFrame frame))
                _buildFrames[mapId] = frame = HouseBuildFrames.From(_atlas.Maps.LoadMap(mapId));
            if (frame != null) { Session.EnterHouseBuild(frame); HouseFrame = frame; }
            else Session.LeaveHouseBuild();
        }

        private bool _furnitureDirty = true;

        /// <summary>
        /// 가구가 막는 칸은 플레이어가 어느 맵에 있든 가구 방의 걷는 격자에 넣는다(Host와 같은 HouseFurnitureBlocking — 막힌 칸이 실제로 바뀐 맵만 NPC 길을 다시 만든다).
        /// 놓인 상자의 상호작용 후보는 가구·상자가 바뀌었거나 맵이 바뀌었을 때만 다시 만든다(Host WorldScreen.SetPlacedFurniture).
        /// </summary>
        private void RefreshFurniture()
        {
            _world.ApplyHouseFurniture(Session);
            if (!_furnitureDirty) return;
            _furnitureDirty = false;
            FurnitureRoom room = _world.Room;
            _world.SetPlacedStorage(room != null ? Session.House?.Placed : null, Session.IsStorage); // 놓인 상자마다 상호작용 후보(Host WorldScreen.SetPlacedFurniture)
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
                if (_seat != null && !_inSeatFrame) _seat.AfterFrame(this);
                return;
            }
            if (key.HasValue) s.Key(key.Value, false);
            interact = interact && !s.Key(ScreenKey.Interact, false) && !s.MovementBlocked;
            s.Tick(FrameSeconds);
            AdvanceClock(s);
            bool blocked = s.MovementBlockedWhile(false);
            string before = _world.Session.Map.Id;
            _world.Update(blocked ? 0 : dx, blocked ? 0 : dy, FrameSeconds);
            // 벽 뚫기는 걸음 바로 뒤에 본다. 이 프레임에 가구가 몸 밑에 놓이면(아래 RefreshFurniture) 다음 걸음 앞에 비켜난다(Core WorldSession).
            if (_world.Session.Map.Id == before && _world.Session.Map.Collision.AnyBlocked(_world.Session.Walker.Box)) CollisionViolations++;
            s.EnterMap(_world.Session.Map.Id, Features());
            SyncHouse();
            if (_furnitureDirty) RefreshFurniture(); // 가구가 바뀌었다(같이 쓰는 집이면 남이 놓았을 수도 — Host는 바뀜 알림에서)
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
            if (_seat != null && !_inSeatFrame) _seat.AfterFrame(this); // Host: 월드 프레임 뒤 방 접속(UpdateInRoom), 내 PC의 방이면 그 틱
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
        /// 어느 NPC든 어슬렁 걸음이 끝난 프레임(걷기 ↔ 쉼이 바뀐다 — Core 머무는 행동이 다음 걸음의 길을 만든다)도 빼고 센다.
        /// 그 프레임의 가장 큰 값은 <see cref="MaxBytesInNpcStepFrame"/>에 남는다.
        /// </summary>
        public bool MeasuresFrameCost => _seat == null;

        public double AllocatedBytesPerIdleFrame(int frames)
        {
            for (int i = 0; i < 5; i++) Frame(); // 처음 한 번 만드는 것(캐시)은 뺀다
            NpcStepChanged();
            long idle = 0;
            int idleFrames = 0;
            for (int i = 0; i < frames; i++)
            {
                long minuteFrames = MinuteFrames;
                long before = GC.GetAllocatedBytesForCurrentThread();
                Frame();
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                if (NpcStepChanged()) { if (bytes > MaxBytesInNpcStepFrame) MaxBytesInNpcStepFrame = bytes; continue; }
                if (MinuteFrames != minuteFrames) { if (bytes > MaxBytesInMinuteFrame) MaxBytesInMinuteFrame = bytes; continue; }
                idle += bytes;
                idleFrames++;
            }
            MeasuredIdleFrames = idleFrames;
            return idleFrames == 0 ? 0 : idle / (double)idleFrames;
        }

        /// <summary>마지막 AllocatedBytesPerIdleFrame이 실제로 잰 프레임 수(분·NPC 걸음 프레임은 뺀다). 0이면 아무것도 재지 않은 것이다.</summary>
        public int MeasuredIdleFrames { get; private set; }

        /// <summary>분이 바뀐 프레임 한 번에 새로 잡은 바이트의 최대(AllocatedBytesPerIdleFrame이 잰 것).</summary>
        public long MaxBytesInMinuteFrame { get; private set; }

        /// <summary>NPC 어슬렁 걸음이 끝난 프레임 한 번에 새로 잡은 바이트의 최대(AllocatedBytesPerIdleFrame이 잰 것).</summary>
        public long MaxBytesInNpcStepFrame { get; private set; }

        private bool[] _npcMoving;

        /// <summary>마지막으로 본 뒤 걷는 중인지가 바뀐 NPC가 있는가(어느 맵에 있든).</summary>
        private bool NpcStepChanged()
        {
            if (_world == null) return false;
            IReadOnlyList<string> ids = _world.NpcIds;
            if (_npcMoving == null) _npcMoving = new bool[ids.Count];
            bool changed = false;
            for (int i = 0; i < ids.Count; i++)
            {
                bool moving = _world.Npcs.TryPose(ids[i], out NpcPose pose) && pose.Moving;
                if (moving != _npcMoving[i]) changed = true;
                _npcMoving[i] = moving;
            }
            return changed;
        }

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

    /// <summary>
    /// 같이 하는 방의 자리(창 없는 몸 하나가 방에 들어가는 길 — Host RoomConnection과 HostGame.StartSession·UpdateRoom이 하는 일).
    /// 구현은 같은 프로세스 안 전송 위의 방(RoomPlay의 RoomSeat).
    /// </summary>
    public interface IHeadlessSeat
    {
        /// <summary>시작 화면에서 슬롯을 골랐다: 방에 들어간 게임 한 판(방의 시계·공유 조각·손님 저장). 아직 방에 들어가 있지 않으면 null.</summary>
        GameSession StartSession(string slotId, Func<string, string> newEventKey);
        /// <summary>몸이 한 프레임을 돈 뒤(자기 자리 보내기, 받은 것 처리, 방의 틱, 다른 사람의 프레임).</summary>
        void AfterFrame(HeadlessBody body);
        /// <summary>방과 이어져 있다.</summary>
        bool Connected { get; }
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
