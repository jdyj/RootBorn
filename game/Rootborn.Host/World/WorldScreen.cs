using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Core.Characters;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Interaction;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Screens.Furniture;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>
    /// 월드 화면(--world 맵). 걷기·충돌·문은 Core의 WorldSession이 계산하고, 여기서는 입력 방향을 넘기고 결과를 그리기만 한다.
    /// 그리는 순서: 타일 층(모두 캐릭터 아래) → 발 위치로 정렬된 가구와 캐릭터 → (빛 지점이 화면에 있으면) 빛 지도를 곱한다.
    /// 배율은 맵이 정한 보이는 높이에 가장 가까운 정수, 카메라는 캐릭터를 따라가되 맵 밖을 비추지 않는다(CameraFrame — 맵이 화면보다 작으면 가운데,
    /// maps.json의 cameraBounds가 있으면 맵 안의 그 상자). 밝기는 "때"로 정한다(game/world/lights.json): 바깥 맵(maps.json dayNight)은 때별 화면 색,
    /// 빛 지점은 그림 색에 더해지는 빛(화면 색 = 그림 × (바깥 밝기 + 빛)).
    /// </summary>
    public sealed class WorldScreen : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Dictionary<string, CachedTexture> _textures = new Dictionary<string, CachedTexture>(StringComparer.OrdinalIgnoreCase);
        private readonly Color _background = new Color(24, 22, 30);

        public readonly WorldMapLoader Maps;
        public readonly WorldSession Session;
        /// <summary>세이브 슬롯에서 시작했으면 그 슬롯(도착할 때마다 world-progress에 쓴다). --world로 띄웠으면 null.</summary>
        public readonly WorldSlot Slot;
        public readonly WorldSlotStart SlotStart;

        public readonly GameContent Content;
        /// <summary>플레이어 겉모습(성장 단계의 몸 + 옷 레이어)과 지금 그릴 칸.</summary>
        public CharacterLook PlayerLook { get; private set; }
        public readonly CharacterAnimator PlayerAnimator = new CharacterAnimator();
        /// <summary>게임 시계. Host가 넘긴 시간(dt × 배속)으로만 흐른다.</summary>
        public readonly DayClock Clock;
        private readonly float _timeScale;
        /// <summary>때별 바깥 밝기와 맵별 빛 지점(game/world/lights.json).</summary>
        public readonly WorldLightingData LightingData;
        private readonly AmbientLight _ambient;
        private readonly MapLights _mapLights = new MapLights();
        private readonly List<VisibleLight> _visible = new List<VisibleLight>(8);
        private readonly MapCameraBounds _cameraBounds;
        private string _lightsMap;
        private bool _lightSnapped;
        /// <summary>마지막으로 그린 화면에 그린 빛 지점(화면 안의 켜진 것만, 지금 세기를 곱한 색).</summary>
        public IReadOnlyList<VisibleLight> VisibleLights => _visible;
        /// <summary>마지막으로 그린 화면이 비춘 맵 사각형(맵 픽셀).</summary>
        public AreaRect CameraView { get; private set; }
        /// <summary>지금 화면에 입힌 빛 색(낮밤이 없는 맵은 흰색).</summary>
        public LightColor Tint { get; private set; } = LightColor.White;

        /// <summary>플레이어 앞의 상호작용 대상(Core). 상호작용 키를 누르면 Interacted 이벤트가 나온다.</summary>
        public readonly InteractionTargets Interactions;
        /// <summary>걷는 NPC(일정과 그날의 분으로 자리를 정한다. 프레임마다 Tick한다). 지금 맵에 있는 NPC는 Count·this[i], 어느 맵에 있든 TryPose.</summary>
        public NpcStage Npcs => _stage;
        /// <summary>지금까지 나온 상호작용 이벤트(대화창 등은 다음 프로젝트에서 이것을 받는다).</summary>
        public readonly List<InteractionEvent> InteractionLog = new List<InteractionEvent>();
        private readonly InteractionData _interactionData;
        private readonly NpcArt _npcArt;
        private readonly NpcScheduleLookupCache _schedules;
        private readonly NpcStage _stage;
        /// <summary>가구 방의 걷는 격자에 게임 한 판의 가구를 맞춘다(플레이어가 어느 맵에 있든 — 창 없는 판과 같은 코드).</summary>
        private readonly HouseFurnitureBlocking _houseBlocking;
        private string _placedMap;
        /// <summary>놓인 상자마다 상호작용 후보("storage", 인스턴스 id). 가구가 바뀔 때만 다시 만든다.</summary>
        private readonly List<InteractionCandidate> _storageCandidates = new List<InteractionCandidate>();

        public LoadedMap Current => Maps.LoadMap(Session.Map.Id);
        public int Zoom { get; private set; } = 1;
        /// <summary>마지막으로 그린 화면에서 맵 1픽셀이 화면 몇 픽셀인가(평소에는 Zoom, 가구 배치 중에는 배치 카메라의 배율).</summary>
        public float Scale { get; private set; } = 1f;

        /// <summary>걷는 맵(가구 방이면 가구가 막는 칸을 더한 맵).</summary>
        public readonly RoomAwareMaps Rooms;
        /// <summary>지금 맵이 가구 방이면 그 방, 아니면 null.</summary>
        public FurnitureRoom Room => Rooms.RoomOf(Session.Map.Id);
        /// <summary>가구 그림 좌표표(data/atlases).</summary>
        public readonly FurnitureArt FurnitureArt;
        /// <summary>가구 배치 중의 카메라(Active일 때 캐릭터를 따라가지 않는다).</summary>
        public readonly PlacementCamera PlacementCamera = new PlacementCamera();
        private readonly string _repoRoot;
        private readonly List<SpriteDraw> _furniture = new List<SpriteDraw>();
        private readonly List<SpriteDraw> _sorted = new List<SpriteDraw>();
        private string _sortedMap;
        private bool _sortedDirty = true;
        /// <summary>지금 방에 그리는 가구 그림 수(시험용).</summary>
        public int FurnitureDrawCount => _furniture.Count;
        /// <summary>마지막으로 그린 화면의 왼쪽 위가 맵의 어느 픽셀이었나(맵이 작으면 음수).</summary>
        public Vector2 CameraOffset { get; private set; }

        /// <summary>조작감·연출(game/world/presentation.json): 부드러운 카메라, 클릭 행동, 문 덮개·구역 페이드. 계산과 그리기는 WorldFeel에 있다.</summary>
        public readonly WorldFeel Feel;

        /// <summary>새 게임의 학생 일차(Unity StudentLifeProgress 첫날).</summary>
        public const int NewGameStudentDay = 1;

        /// <param name="game">게임 한 판(없으면 null을 돌려준다). 부지의 단계 종류 house-stage·facility-level이 읽는다(HostBuildingLevels).</param>
        public WorldScreen(GraphicsDevice device, HostOptions options, Func<Rootborn.Game.GameSession> game = null)
        {
            _device = device;
            string catalog = options.WorldMapsPath ?? Path.Combine(options.RepoRoot, WorldCatalog.DefaultRelativePath);
            Maps = new WorldMapLoader(options.RepoRoot, WorldCatalog.Load(catalog),
                BuildingCatalog.Load(options.WorldBuildingsPath ?? Path.Combine(options.RepoRoot, BuildingCatalog.DefaultRelativePath)),
                HostBuildingLevels.Create(game ?? (() => null)));
            Content = GameContent.Load(options.RepoRoot);
            Clock = Content.NewClock();
            Clock.SetCurrentHour(options.StartHour);
            _timeScale = options.TimeScale;
            LightingData = WorldLightingData.Load(Path.Combine(options.RepoRoot, WorldLightingData.DefaultRelativePath));
            _ambient = new AmbientLight(LightingData);
            _cameraBounds = MapCameraBounds.Load(catalog);
            Feel = new WorldFeel(device, WorldPresentation.Load(Path.Combine(options.RepoRoot, WorldPresentation.DefaultRelativePath)));
            _interactionData = InteractionData.Load(options.InteractionsPath ?? Path.Combine(options.RepoRoot, InteractionData.DefaultRelativePath));
            Interactions = new InteractionTargets(_interactionData.RadiusCells);
            Interactions.Interacted += e => { InteractionLog.Add(e); Console.WriteLine("[상호작용] " + e); };
            _npcArt = Content.Npcs;
            _schedules = new NpcScheduleLookupCache(Content.Definitions.All<NpcSchedule>());
            _repoRoot = options.RepoRoot;
            FurnitureArt = new FurnitureArt(Path.Combine(options.RepoRoot, "data"));
            Rooms = new RoomAwareMaps(Maps, _interactionData);
            _stage = NpcStage.Create(_schedules, _npcArt.Order, _interactionData, Maps.Catalog, Rooms, Content.Definitions.TimeRules);
            _houseBlocking = new HouseFurnitureBlocking(Rooms.RoomOf, _interactionData.PlacementMaps(Maps.Catalog), FurnitureBlockingChanged);
            Session = new WorldSession(Rooms, Maps.Catalog.Walker);
            string startMap;
            if (options.SaveSlot != null)
            {
                Slot = new WorldSlot(new FileSaveStore(options.SaveRoot), options.SaveSlot, WorldTravelData.From(Content.Definitions));
                SlotStart = Slot.Begin(DateTime.UtcNow.Ticks);
                if (!SlotStart.Ok) throw new InvalidOperationException("세이브 슬롯으로 시작하지 못했다: " + SlotStart.Problem);
                if (SlotStart.MovedFrom != null)
                    Console.Error.WriteLine("[시작] 슬롯 " + options.SaveSlot + ": 저장된 자리 " + SlotStart.MovedFrom + "가 지금 맵 목록에 없어 새 게임 자리에서 이어 간다");
                Session.Arrivals = Slot; // Unity 판처럼 처음 들어갈 때도 도착을 쓴다
                startMap = SlotStart.Entry.SceneName;
                if (!Session.Enter(startMap, SlotStart.Entry.SpawnId)) throw new InvalidOperationException(Session.LastProblem);
            }
            else
            {
                MapEntry entry = Maps.Catalog.Find(options.World) ?? throw new InvalidOperationException("맵 목록에 없는 맵: " + options.World);
                string spawn = options.WorldSpawn;
                if (string.IsNullOrEmpty(spawn)) foreach (string id in entry.Spawns.Keys) { spawn = id; break; }
                startMap = entry.Id;
                if (!Session.Enter(startMap, spawn)) throw new InvalidOperationException(Session.LastProblem);
            }
            ReportProblems();
            _look = LoadWearables(options);
            PlayerLook = ResolveLook(WearableRules.SelectStage(Content.Wearables.Stages, NewGameStudentDay, false));
            UpdateLight(0f);
            RefreshInteractions();
        }

        /// <summary>장소 상태의 시간대(Core LocationStateTimeSlots: 오전·오후·저녁·밤). 분 단위 개인 시각(CurrentHour)에서 정한다. NPC 자리는 이것이 아니라 그날의 분을 따른다.</summary>
        public string TimeSlot => LocationStateTimeSlots.FromMinutes((int)(CurrentHour * 60f), DayStartMinutes);

        /// <summary>하루 시작 시각(분, 시간 규칙 game/time.json). 그 전의 새벽은 밤 시간대다(NPC 자리·장소 상태·낮밤 빛 모두). Host가 게임 한 판의 규칙에서 넣는다.</summary>
        public int DayStartMinutes { get; set; } = LocationStateTimeSlots.DefaultDayStartMinutes;

        /// <summary>
        /// 낮밤 빛(바깥 밝기·빛 지점 세기)의 시간대. 게임 한 판이 있으면 방의 때(HUD 시계가 보이는 때의 대표 시각), 아니면 TimeSlot과 같다.
        /// NPC 자리·장소 상태는 이것이 아니라 TimeSlot(개인 시각)을 따른다 — 플레이(누가 어디 있는지)는 바꾸지 않고 HUD와 밝기만 맞춘다.
        /// </summary>
        public string LightTimeSlot => _lightHour != null ? LocationStateTimeSlots.FromMinutes((int)(_lightHour() * 60f), DayStartMinutes) : TimeSlot;

        private Func<int> _studentDay;
        private Func<float> _studentHour;
        private Func<float> _lightHour;
        private Func<double> _dayMinutes;

        /// <summary>
        /// 게임 한 판이 있으면 NPC 자리를 학생 날·시각으로 정한다(Unity 판: NPC 일정이 학생 시각을 따랐다). 낮밤 빛은 lightHour(0~24시)를 따른다 —
        /// Host는 방의 때의 대표 시각을 넘긴다. 주지 않으면 hour. 그 뒤로 게임 시계(Clock)는 흐르지 않는다(학생 시계가 흐른다).
        /// dayMinutes는 NPC가 걷는 시각(그날의 분, 하루 시작~하루 끝 축, 분 아래까지 — GameSession.DayMinutesExact). 주지 않으면 hour에서 만든다(분 단위라 걷는 NPC가 튄다).
        /// </summary>
        public void FollowStudentTime(Func<int> day, Func<float> hour, Func<float> lightHour = null, Func<double> dayMinutes = null)
        {
            _studentDay = day ?? throw new ArgumentNullException(nameof(day));
            _studentHour = hour ?? throw new ArgumentNullException(nameof(hour));
            _lightHour = lightHour;
            _dayMinutes = dayMinutes;
            _lightSnapped = false;
            _lightsMap = null;
            UpdateLight(0f);
            RefreshInteractions();
        }

        /// <summary>지금 시각(0~24). 학생 시각을 따르면 그것, 아니면 게임 시계.</summary>
        public float CurrentHour => _studentHour != null ? _studentHour() : Clock.CurrentHour;

        /// <summary>지금 날. 학생 날을 따르면 그것, 아니면 게임 시계.</summary>
        public int CurrentDay => _studentDay != null ? _studentDay() : Clock.Day;

        /// <summary>
        /// 맵이 바뀌었을 때만 후보 목록을 다시 만든다(EnterMap + 놓인 상자). 시간대·날이 바뀌어도 다시 만들지 않는다 — NPC 후보는 Tick이 옮기고 켜고 끈다.
        /// 그다음 프레임마다 NPC 자리(Tick)와 앞의 대상(Interactions.Update)을 고친다(새 객체를 만들지 않는다).
        /// </summary>
        private void RefreshInteractions()
        {
            string map = Session.Map.Id;
            if (map != _placedMap)
            {
                _placedMap = map;
                Interactions.MapId = map;
                Interactions.RadiusPixels = _interactionData.RadiusCells * Session.Map.Grid.CellSize;
                List<InteractionCandidate> candidates = _stage.EnterMap(map, _interactionData.Map(map), Session.Map.Doors);
                candidates.AddRange(_storageCandidates);
                Interactions.SetCandidates(candidates);
            }
            if (_dayMinutes != null) _stage.Tick(CurrentDay, _dayMinutes());
            else
            {
                NpcTimeWithoutGame(CurrentDay, CurrentHour, DayStartMinutes, out int npcDay, out double npcMinutes);
                _stage.Tick(npcDay, npcMinutes);
            }
            Interactions.Update(Session.Walker.X, Session.Walker.Y);
        }

        /// <summary>
        /// 게임 한 판이 없을 때(--world) NPC 자리의 (날, 하루 시작~하루 끝 축의 분). 시계의 날은 00:00에 넘어가지만 NPC의 하루는 하루 시작에 넘어간다:
        /// 하루 시작보다 이른 시각은 전날 밤의 이어짐이라 분에 하루를 더하고 날은 하나 뺀다(1 아래로는 내려가지 않는다).
        /// 그래서 23:59와 00:01이 같은 날의 이어진 분이다(자정에 다른 계획으로 건너뛰지 않는다).
        /// </summary>
        public static void NpcTimeWithoutGame(int clockDay, float hour, int dayStartMinutes, out int day, out double dayMinutes)
        {
            double minutes = hour * 60.0;
            day = clockDay;
            if (minutes < dayStartMinutes)
            {
                minutes += TimeRules.MinutesPerDay;
                if (day > 1) day--;
            }
            dayMinutes = minutes;
        }

        /// <summary>지금 맵의 상호작용 데이터(물건, NPC 자리, 가구 방이면 면 id).</summary>
        public MapInteractions HereData => _interactionData.Map(Session.Map.Id);

        /// <summary>상호작용 키: 대상이 무언가 하면 Core 이벤트가 나온다.</summary>
        public InteractionEvent? Interact() => Interactions.Press();

        private WearableState _look;

        /// <summary>지금 그리는 성장 단계 id.</summary>
        public string GrowthStage { get; private set; }

        /// <summary>슬롯의 옷 선택(없으면 칸 기본값).</summary>
        private WearableState LoadWearables(HostOptions options) => Slot != null
            ? PlayerWearables.Load(new FileSaveStore(options.SaveRoot), Slot.SlotId, SaveFileNames.DefaultPlayerId, SlotStart.Metadata, Content.Wearables, Content.Characters)
            : new WearableState();

        private CharacterLook ResolveLook(string stage)
        {
            GrowthStage = stage;
            return CharacterLook.Resolve(Content.Characters, Content.Wearables, _look, stage, null)
                ?? throw new InvalidOperationException("성장 단계 " + stage + "의 몸 그림이 없다");
        }

        /// <summary>
        /// 성장 단계를 바꾼다(게임 한 판이 있으면 Host가 GameSession.GrowthStageId()로 부른다 — Unity WearableLook.StageFor).
        /// 게임 한 판이 없으면(--world) 새 게임과 같은 1일차·학생 단계다. 같은 단계면 아무것도 하지 않는다. 빈 id는 무시한다.
        /// </summary>
        public void SetGrowthStage(string stage)
        {
            if (string.IsNullOrEmpty(stage) || stage == GrowthStage) return;
            PlayerLook = ResolveLook(stage);
        }

        /// <summary>플레이어가 고른 옷(옷장이 고치고 저장한다). 시작할 때 슬롯에서 읽는다.</summary>
        public WearableState Wearing => _look;

        /// <summary>옷장이 옷을 바꿨을 때 월드의 캐릭터 모습을 새로 입힌다.</summary>
        public void ApplyPlayerLook(CharacterLook look)
        {
            if (look != null) PlayerLook = look;
        }

        private Texture2D Load(string path)
        {
            if (!_textures.TryGetValue(path, out CachedTexture cached))
            {
                cached = new CachedTexture();
                using (FileStream stream = File.OpenRead(path)) cached.Texture = Texture2D.FromStream(_device, stream); // PNG를 그 자리에서 읽는다
                cached.Bytes = (long)cached.Texture.Width * cached.Texture.Height * 4;
                _textures[path] = cached;
                CachedTextureBytes += cached.Bytes;
            }
            cached.LastUsed = _mapTurn;
            return cached.Texture;
        }

        /// <summary>읽어 둔 그림과 그 그림을 마지막으로 그린 맵 차례(맵에 들어갈 때마다 하나씩 는다).</summary>
        private sealed class CachedTexture
        {
            public Texture2D Texture;
            public long Bytes;
            public int LastUsed;
        }

        /// <summary>
        /// 그림 캐시 기본 한도(바이트, 가로 × 세로 × 4의 합). 2026-11-12 data의 맵 10개를 다 돌았을 때 약 220MB(추정)라 지금은 풀 일이 없다.
        /// 맵을 새로 그려 그림이 늘면 넘는 만큼 오래 안 쓴 맵의 그림부터 푼다(다시 들어가면 다시 읽는다).
        /// </summary>
        public const long DefaultTextureBudgetBytes = 256L * 1048576;

        /// <summary>그림 캐시 한도(바이트). 넘으면 맵에 들어가 첫 화면을 그린 뒤 이번 맵이 쓰지 않은 그림을 오래 안 쓴 것부터 푼다.</summary>
        public long TextureBudgetBytes { get; set; } = DefaultTextureBudgetBytes;

        /// <summary>그림 캐시에 든 PNG 수(맵·캐릭터·NPC·가구 그림).</summary>
        public int CachedTextureCount => _textures.Count;

        /// <summary>그림 캐시가 잡은 GPU 메모리 추정(가로 × 세로 × 4바이트의 합).</summary>
        public long CachedTextureBytes { get; private set; }

        private int _mapTurn;
        private string _drawnMap;
        private bool _trimPending;
        private readonly List<(int lastUsed, string path)> _trimOrder = new List<(int, string)>();

        /// <summary>맵에 들어간 첫 화면을 그린 뒤(이번 맵이 쓰는 그림을 다 만진 뒤): 한도를 넘으면 이번 맵 차례에 안 쓴 그림을 오래된 것부터 푼다.</summary>
        private void TrimTextures()
        {
            _trimPending = false;
            if (CachedTextureBytes <= TextureBudgetBytes) return;
            _trimOrder.Clear();
            foreach (KeyValuePair<string, CachedTexture> pair in _textures)
                if (pair.Value.LastUsed < _mapTurn) _trimOrder.Add((pair.Value.LastUsed, pair.Key));
            _trimOrder.Sort((a, b) => a.lastUsed != b.lastUsed ? a.lastUsed.CompareTo(b.lastUsed) : string.CompareOrdinal(a.path, b.path));
            for (int i = 0; i < _trimOrder.Count && CachedTextureBytes > TextureBudgetBytes; i++)
            {
                CachedTexture cached = _textures[_trimOrder[i].path];
                _textures.Remove(_trimOrder[i].path);
                CachedTextureBytes -= cached.Bytes;
                cached.Texture.Dispose();
            }
            _trimOrder.Clear();
        }

        private void ReportProblems()
        {
            LoadedMap map = Current;
            foreach (string problem in map.Problems) Console.Error.WriteLine("[월드] " + map.Entry.Id + ": " + problem);
            foreach (string problem in map.LotProblems) Console.Error.WriteLine("[월드] " + map.Entry.Id + ": 부지: " + problem);
        }

        /// <summary>
        /// 부지가 있는 맵에서 건물 단계가 바뀌었으면(맵 읽기가 다시 만든 맵이 걷는 맵과 다르면) 서 있는 맵을 다시 읽는다.
        /// 부지가 없는 맵은 표 조회 한 번뿐이다. 가구 방에는 부지를 두지 않는다(맵 검사가 알린다).
        /// </summary>
        private void FollowBuildingLevels()
        {
            LoadedMap map = Current;
            if (map.Entry.Lots.Count == 0 || ReferenceEquals(map.World, Session.Map)) return;
            if (!Session.Reload()) { Console.Error.WriteLine("[월드] " + Session.LastProblem); return; }
            ReportProblems();
        }

        /// <summary>한 프레임: 입력 방향과 지난 시간으로 걷고, 문을 지나면 맵이 바뀐다.</summary>
        public void Update(int dx, int dy, float dt)
        {
            FollowBuildingLevels();
            string before = Session.Map.Id;
            Session.Update(dx, dy, dt);
            if (Session.Map.Id != before) ReportProblems();
            Feel.AfterWalk(Session, Current.Entry, dt);
            PlayerAnimator.Advance(Session.Walker.Moving, Session.Walker.Facing, dt);
            if (_studentHour == null) Clock.Tick(dt * _timeScale);
            UpdateLight(dt);
            RefreshInteractions();
        }

        /// <summary>
        /// 빛은 "때"(LightTimeSlot — 게임 한 판이 있으면 방의 때)를 따라간다. 맵에 들어선 순간에는 바로 맞춘다. 바깥 맵(맵 목록 dayNight — 맵 id가 아니라 맵 성질)만 때별 화면 색,
        /// 나머지는 흰색. 빛 지점은 맵마다 데이터에 있는 것.
        /// </summary>
        private void UpdateLight(float dt)
        {
            string slot = LightTimeSlot, map = Session.Map.Id;
            if (map != _lightsMap) { _lightsMap = map; _mapLights.Enter(LightingData.LightsOf(map), slot); _lightSnapped = false; }
            else _mapLights.Update(slot, dt);
            if (!Current.Entry.DayNight)
            {
                _lightSnapped = false;
                Tint = LightColor.White;
                return;
            }
            if (!_lightSnapped) { _ambient.Snap(slot); _lightSnapped = true; }
            else _ambient.Update(slot, dt);
            Tint = _ambient.Tint;
        }

        public void Draw(SpriteBatch batch, int screenWidth, int screenHeight)
        {
            LoadedMap map = Current;
            if (!string.Equals(map.Entry.Id, _drawnMap, StringComparison.Ordinal)) { _drawnMap = map.Entry.Id; _mapTurn++; _trimPending = true; }
            MapGrid grid = map.World.Grid;
            Zoom = WorldCamera.Zoom(screenHeight, map.Entry.ViewCells, grid.CellSize);
            Walker walker = Session.Walker;
            Vector2 offset;
            if (PlacementCamera.Active)
            {
                // 가구 배치 중: 배치 카메라(Unity HousePlacementCameraController — 캐릭터를 따라가지 않는다).
                PlacementCamera.SetAspect(screenWidth / (float)screenHeight);
                Scale = PlacementCamera.Scale(screenHeight);
                (float x, float y) = PlacementCamera.TopLeft(screenWidth, screenHeight);
                offset = new Vector2(x, y);
                Feel.CameraReleased();
            }
            else
            {
                Scale = Zoom;
                float viewW = screenWidth / (float)Zoom, viewH = screenHeight / (float)Zoom;
                AreaRect bounds = CameraFrame.Bounds(grid.PixelWidth, grid.PixelHeight,
                    _cameraBounds.TryGet(map.Entry.Id, out AreaRect authored) ? authored : (AreaRect?)null);
                AreaRect frame = Feel.Follow(map.Entry.Id, walker.X, walker.Y, bounds, viewW, viewH);
                offset = new Vector2(frame.X, frame.Y);
            }
            CameraOffset = offset;
            CameraView = new AreaRect(offset.X, offset.Y, screenWidth / Scale, screenHeight / Scale);

            Matrix view = Matrix.CreateTranslation(-offset.X, -offset.Y, 0f) * Matrix.CreateScale(Scale, Scale, 1f);
            _mapLights.CollectVisible(CameraView, _visible);
            bool lit = _visible.Count > 0;
            var ambient = new Color(Math.Clamp(Tint.R, 0f, 1f), Math.Clamp(Tint.G, 0f, 1f), Math.Clamp(Tint.B, 0f, 1f));
            // 빛 지점이 보이면 빛 지도(바깥 밝기 + 빛)를 먼저 만들고 월드를 흰색으로 그린 뒤 곱한다. 없으면 월드를 바깥 밝기로 바로 칠한다.
            if (lit) DrawLightMap(batch, view, ambient, screenWidth, screenHeight);
            _device.Clear(_background);
            _tint = lit ? Color.White : ambient;
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, view);
            foreach (SpriteDraw s in map.Picture.Under) DrawSprite(batch, s);
            List<SpriteDraw> sorted = SortedWithFurniture(map);
            PickDoorFrame(map, sorted);
            // 플레이어와 NPC를 발 위치(몸 가운데 Y)로 정렬해 가구 사이에 끼운다.
            _actors.Clear();
            _actors.Add((walker.Y, -1));
            for (int i = 0; i < _stage.Count; i++) _actors.Add((_stage[i].Pose.Y, i));
            IReadOnlyList<RemoteAvatar> remotes = Remotes?.Visible;
            if (remotes != null)
                for (int i = 0; i < remotes.Count; i++) _actors.Add((remotes[i].Y, RemoteActor(i)));
            _actors.Sort(ActorOrder);
            int drawn = 0, drawnRemotes = 0;
            foreach (var actor in _actors)
            {
                int upTo = ActorIndex(sorted, actor.sortY);
                for (; drawn < upTo; drawn++) DrawSorted(batch, sorted, drawn);
                if (actor.npc == -1) DrawCharacter(batch, walker);
                else if (actor.npc < -1) { DrawRemote(batch, remotes[RemoteActor(actor.npc)], walker); drawnRemotes++; }
                else DrawNpc(batch, _stage[actor.npc]);
            }
            for (; drawn < sorted.Count; drawn++) DrawSorted(batch, sorted, drawn);
            batch.End();
            RemotesDrawn = drawnRemotes;
            if (lit)
            {
                batch.Begin(SpriteSortMode.Deferred, MultiplyBlend, SamplerState.PointClamp, null, null, null, null);
                batch.Draw(_lightMap, Vector2.Zero, Color.White);
                batch.End();
            }
            Feel.DrawOverlays(batch, view, screenWidth, screenHeight, grid.CellSize, DoorFrameShown >= 0);
            if (_trimPending) TrimTextures();
        }

        private readonly List<(float sortY, int npc)> _actors = new List<(float sortY, int npc)>(8);
        private static readonly Comparison<(float sortY, int npc)> ActorOrder = (a, b) => a.sortY != b.sortY ? a.sortY.CompareTo(b.sortY) : a.npc.CompareTo(b.npc);

        // ---------- 빛 지도 ----------

        /// <summary>그린 화면 × 빛 지도(색만 곱하고 알파는 그대로). 상태는 한 장치에 묶이므로 화면마다 하나.</summary>
        private readonly BlendState MultiplyBlend = new BlendState
        {
            ColorSourceBlend = Blend.DestinationColor, ColorDestinationBlend = Blend.Zero,
            AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
        };

        /// <summary>빛 그림의 크기(픽셀). 늘려 그리므로 부드럽게 거른다.</summary>
        private const int LightTextureSize = 128;
        private RenderTarget2D _lightMap;
        private RenderTargetBinding[] _bindings = Array.Empty<RenderTargetBinding>();
        /// <summary>안 반경 비율(1/64 단위)마다 한 번 만든 빛 그림.</summary>
        private readonly Dictionary<int, Texture2D> _lightTextures = new Dictionary<int, Texture2D>();

        /// <summary>
        /// 빛 지도를 따로 그린다: 바깥 밝기로 채우고 보이는 빛을 더한다. 지금 그리던 대상(장면 버퍼)으로 돌아온다 — 돌아온 대상은 비워질 수 있으므로
        /// 월드를 그리기 전에 부른다. 화면 크기가 바뀔 때만 빛 지도를 새로 만든다.
        /// </summary>
        private void DrawLightMap(SpriteBatch batch, Matrix view, Color ambient, int width, int height)
        {
            if (_lightMap == null || _lightMap.Width != width || _lightMap.Height != height)
            {
                _lightMap?.Dispose();
                _lightMap = new RenderTarget2D(_device, width, height);
            }
            int count = _device.RenderTargetCount;
            if (_bindings.Length != count) _bindings = new RenderTargetBinding[count];
            if (count > 0) _device.GetRenderTargets(_bindings);
            _device.SetRenderTarget(_lightMap);
            _device.Clear(ambient);
            batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, null, null, null, view);
            for (int i = 0; i < _visible.Count; i++)
            {
                VisibleLight l = _visible[i];
                Texture2D texture = LightTexture(l.InnerRadius / l.Radius);
                float scale = l.Radius * 2f / LightTextureSize;
                var color = new Color(Math.Clamp(l.R, 0f, 1f), Math.Clamp(l.G, 0f, 1f), Math.Clamp(l.B, 0f, 1f));
                batch.Draw(texture, new Vector2(l.X - l.Radius, l.Y - l.Radius), null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            batch.End();
            if (count > 0) _device.SetRenderTargets(_bindings); else _device.SetRenderTarget(null);
        }

        /// <summary>
        /// 둥근 빛 그림: 안 반경까지 1, 그 바깥은 끝까지 부드럽게 0(Unity 점 조명의 안·바깥 반경). 흰색에 알파로 세기를 둔다.
        /// 안 반경 비율마다 처음 쓸 때 한 번 만든다.
        /// </summary>
        private Texture2D LightTexture(float innerRatio)
        {
            int key = (int)MathF.Round(Math.Clamp(innerRatio, 0f, 1f) * 64f);
            if (_lightTextures.TryGetValue(key, out Texture2D texture)) return texture;
            float inner = key / 64f;
            var pixels = new Color[LightTextureSize * LightTextureSize];
            float half = LightTextureSize / 2f;
            for (int y = 0; y < LightTextureSize; y++)
                for (int x = 0; x < LightTextureSize; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (d >= 1f) a = 0f;
                    else if (d <= inner) a = 1f;
                    else
                    {
                        float f = 1f - (d - inner) / (1f - inner);
                        a = f * f * (3f - 2f * f);
                    }
                    pixels[y * LightTextureSize + x] = new Color(1f, 1f, 1f, a);
                }
            texture = new Texture2D(_device, LightTextureSize, LightTextureSize);
            texture.SetData(pixels);
            _lightTextures[key] = texture;
            return texture;
        }

        /// <summary>
        /// 놓인 가구를 그릴 목록으로 바꾼다(Unity PlacedFurnitureDepthPresentation): 그림 칸마다 좌표표의 그림을 기준점이 칸 가운데에 오게,
        /// 크기는 그림 픽셀 / PPU 칸. 앞뒤는 그 칸 가운데의 높이로 캐릭터·맵 가구와 함께 정렬한다. 지금 방이 아니면 비운다.
        /// 가구가 바뀔 때만 부른다(Host가 게임 명령의 바뀜 알림에서).
        /// </summary>
        /// <summary>그릴 놓인 가구 그림 조각 수(시험·진단용).</summary>
        public int PlacedFurnitureSpriteCount => _furniture.Count;

        /// <param name="isStorage">상자인 가구(게임 한 판의 데이터 조회). 상자마다 가운데에 상호작용 후보 "storage"를 둔다 — 앞에서 누르면 상자 화면이 열린다.</param>
        public void SetPlacedFurniture(IReadOnlyList<PlacedFurniture> placed, Func<FurnitureDefinition, bool> isStorage = null)
        {
            _furniture.Clear();
            _sortedDirty = true;
            int storageBefore = _storageCandidates.Count;
            _storageCandidates.Clear();
            FurnitureRoom room = Room;
            if (room == null || placed == null)
            {
                if (storageBefore > 0) RefreshCandidatesNow();
                return;
            }
            if (isStorage != null)
                PlacedInteractionCandidates.Fill(placed, isStorage, room.CellArea, Rootborn.Screens.Storage.StorageScreen.InteractionKind, _storageCandidates);
            foreach (PlacedFurniture p in placed)
            {
                IReadOnlyList<FurnitureTilePart> parts = p.Definition.GetTileParts(p.Direction);
                foreach (FurnitureTilePart part in parts)
                {
                    if (!FurnitureArt.TryGet(part, out FurnitureSprite sprite)) continue;
                    AreaRect cell = room.CellArea(new GridPoint(p.Anchor.X + part.LocalCell.X, p.Anchor.Y + part.LocalCell.Y));
                    float w = sprite.WidthCells * cell.W, h = sprite.HeightCells * cell.H;
                    _furniture.Add(new SpriteDraw(Path.Combine(_repoRoot, sprite.Texture), sprite.X, sprite.Y, sprite.W, sprite.H,
                        cell.CenterX - w * sprite.PivotX, cell.CenterY - h * sprite.PivotY, w, h, 0, 1f, cell.CenterY));
                }
            }

            if (storageBefore > 0 || _storageCandidates.Count > 0) RefreshCandidatesNow();
        }

        /// <summary>
        /// 게임 한 판을 이을 때: 집 배치를 아직 모르면 집 밖에서 읽어 두고, 가구 방의 걷는 격자에 가구가 막는 칸을 넣는다(지금 맵과 상관없이).
        /// </summary>
        public void AttachHouseFurniture(global::Rootborn.Game.GameSession session) => _houseBlocking.Attach(session, Session.Map.Id);

        /// <summary>
        /// 가구가 바뀌었을 때(게임 한 판의 가구 알림): 플레이어가 어느 맵에 있든 그 면의 가구 방에 막힌 칸을 맞춘다. 막힌 칸이 실제로 바뀐 맵만
        /// NPC가 길과 머무름을 새 격자로 다시 만들고, 그 프레임을 그리기 전에 NPC 자리를 다시 낸다(배치 화면이 열려 있어도 프레임마다 다시 계획하지 않는다).
        /// </summary>
        public void ApplyHouseFurniture(global::Rootborn.Game.GameSession session) => _houseBlocking.Apply(session);

        private void FurnitureBlockingChanged(string mapId)
        {
            _stage.ForgetMap(mapId);
            RefreshInteractions(); // 옛 계획의 자리가 한 번 그려지지 않게 바로 다시 낸다(후보 좌표도)
        }

        /// <summary>후보를 지금 다시 만든다(놓인 상자가 바뀌었다).</summary>
        private void RefreshCandidatesNow()
        {
            _placedMap = null;
            RefreshInteractions();
        }

        private int _doorFrameAfter = -1;
        private SpriteDraw _doorFrame;

        /// <summary>마지막으로 그린 화면의 건물 문 프레임 번호(열리는 건물 문에 프레임이 있을 때만, 아니면 -1 — 시험용).</summary>
        public int DoorFrameShown { get; private set; } = -1;

        /// <summary>
        /// 열리는 문이 건물 문이고 프레임이 있으면, 그 진행도(Core가 센 값)의 프레임을 건물 그림 바로 다음에 그리도록 정한다.
        /// 닫혀 있을 때는 그리지 않는다(건물 그림에 닫힌 문이 있다). 가구를 섞은 목록에서는 번호가 달라 그리지 않는다(가구 방에는 부지가 없다).
        /// </summary>
        private void PickDoorFrame(LoadedMap map, List<SpriteDraw> sorted)
        {
            _doorFrameAfter = -1;
            DoorFrameShown = -1;
            BuildingDoorArt art = map.DoorArtFor(Session.OpeningDoor);
            if (art == null || art.SortedIndex < 0 || !ReferenceEquals(sorted, map.Picture.Sorted)) return;
            DoorFrameShown = art.FrameAt(Session.OpeningProgress);
            _doorFrame = art.Frames[DoorFrameShown];
            _doorFrameAfter = art.SortedIndex;
        }

        private void DrawSorted(SpriteBatch batch, List<SpriteDraw> sorted, int index)
        {
            DrawSprite(batch, sorted[index]);
            if (index == _doorFrameAfter) DrawSprite(batch, _doorFrame);
        }

        private List<SpriteDraw> SortedWithFurniture(LoadedMap map)
        {
            if (_furniture.Count == 0) return map.Picture.Sorted;
            if (!_sortedDirty && _sortedMap == map.Entry.Id) return _sorted;
            _sorted.Clear();
            _sorted.AddRange(map.Picture.Sorted);
            int fixedCount = _sorted.Count;
            _sorted.AddRange(_furniture);
            var order = new List<(SpriteDraw draw, int i)>(_sorted.Count);
            for (int i = 0; i < _sorted.Count; i++) order.Add((_sorted[i], i));
            order.Sort((a, b) => a.draw.SortY != b.draw.SortY ? a.draw.SortY.CompareTo(b.draw.SortY) : a.i.CompareTo(b.i));
            _sorted.Clear();
            foreach (var o in order) _sorted.Add(o.draw);
            _sortedMap = map.Entry.Id;
            _sortedDirty = false;
            return _sorted;
        }

        /// <summary>정렬된 목록에서 높이 sortY인 캐릭터보다 먼저(뒤에) 그릴 것의 수(MapPicture.ActorIndex와 같은 규칙).</summary>
        private static int ActorIndex(List<SpriteDraw> sorted, float sortY)
        {
            int lo = 0, hi = sorted.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (sorted[mid].SortY <= sortY) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        private void DrawSprite(SpriteBatch batch, SpriteDraw s)
        {
            Texture2D texture = Load(s.Texture);
            var effects = SpriteEffects.None;
            if ((s.Flip & 1) != 0) effects |= SpriteEffects.FlipHorizontally;
            if ((s.Flip & 2) != 0) effects |= SpriteEffects.FlipVertically;
            var dest = new Rectangle((int)MathF.Round(s.X), (int)MathF.Round(s.Y), (int)MathF.Round(s.W), (int)MathF.Round(s.H));
            batch.Draw(texture, dest, new Rectangle(s.SrcX, s.SrcY, s.SrcW, s.SrcH), WithAlpha(_tint, s.Alpha), 0f, Vector2.Zero, effects, 0f);
        }

        /// <summary>
        /// 레이어 캐릭터: 몸 다음 옷 레이어를 같은 칸으로 겹친다. 그림은 기준점(발)을 몸 상자 아래 가운데에 맞춘다 — Unity 판 FeetOffsetY -0.5칸.
        /// 오른쪽은 시트에 따로 그려져 있어 뒤집지 않는다.
        /// </summary>
        private void DrawCharacter(SpriteBatch batch, Walker walker)
        {
            AreaRect body = walker.Box;
            int column = Feel.Action.ColumnOr(PlayerAnimator.Column); // 클릭 행동 중이면 행동 칸
            DrawLayer(batch, PlayerLook.Body, body, PlayerAnimator, column);
            IReadOnlyList<CharacterSheet> layers = PlayerLook.Layers; // foreach는 열거자를 매 프레임 상자에 담는다
            for (int i = 0; i < layers.Count; i++) DrawLayer(batch, layers[i], body, PlayerAnimator, column);
        }

        private void DrawLayer(SpriteBatch batch, CharacterSheet sheet, AreaRect body, CharacterAnimator animator, int column = -1)
        {
            CharacterFrame f = sheet.Frame(animator.Row, column >= 0 ? column : animator.Column);
            if (f == null) return;
            var position = new Vector2(MathF.Round(body.CenterX - f.W * f.PivotX), MathF.Round(body.Bottom - f.H * f.PivotY));
            batch.Draw(Load(f.Texture), position, new Rectangle(f.X, f.Y, f.W, f.H), _tint);
        }

        /// <summary>
        /// 같은 맵의 다른 참가자들(멀티플레이, 방에 들어갔을 때만 Host가 넣는다). 없으면 그리지 않는다. 이름표는 없다(Unity 판에도 없었다).
        /// 몸은 걷기를 막지 않는다(충돌 격자에 넣지 않는다).
        /// </summary>
        public RemoteAvatars Remotes { get; set; }

        /// <summary>마지막으로 그린 화면에 그린 다른 참가자 수.</summary>
        public int RemotesDrawn { get; private set; }

        /// <summary>정렬 목록의 액터 번호: -1 나, 0 이상 NPC, -2 이하 다른 참가자(-2 - 순번). 거꾸로도 같은 식이다.</summary>
        private static int RemoteActor(int index) => -2 - index;

        /// <summary>다른 참가자: 내 몸과 같은 크기의 상자를 그 사람의 몸 가운데에 두고 같은 방법(발 기준)으로 겹쳐 그린다.</summary>
        private void DrawRemote(SpriteBatch batch, RemoteAvatar remote, Walker walker)
        {
            AreaRect mine = walker.Box;
            var body = new AreaRect(remote.X - mine.W / 2f, remote.Y - mine.H / 2f, mine.W, mine.H);
            DrawLayer(batch, remote.Look.Body, body, remote.Animator);
            IReadOnlyList<CharacterSheet> layers = remote.Look.Layers;
            for (int i = 0; i < layers.Count; i++) DrawLayer(batch, layers[i], body, remote.Animator);
        }

        /// <summary>NPC 그림: 기준점(발)을 자리에서 반 칸 아래(몸 가운데가 자리)에 맞춘다 — 플레이어와 같은 기준.</summary>
        private void DrawNpc(SpriteBatch batch, NpcOnMap npc)
        {
            CharacterFrame f = _npcArt.Frame(npc.NpcId);
            if (f == null) return;
            float feet = npc.Pose.Y + Current.World.Grid.CellSize / 2f;
            var position = new Vector2(MathF.Round(npc.Pose.X - f.W * f.PivotX), MathF.Round(feet - f.H * f.PivotY));
            batch.Draw(Load(f.Texture), position, new Rectangle(f.X, f.Y, f.W, f.H), _tint);
        }

        // 잡힌 대상 위의 "[E] …" 안내 글자는 화면 쌓기의 HUD(InteractionPromptScreen)가 그린다. Host가 대상과 그 화면 위치를 넘긴다.

        private Color _tint = Color.White;

        /// <summary>섞기는 알파를 곱하지 않은 색(NonPremultiplied)이라 색은 그대로 두고 알파만 바꾼다.</summary>
        private static Color WithAlpha(Color c, float alpha) => new Color(c.R, c.G, c.B, (byte)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255));

        public void Dispose()
        {
            foreach (CachedTexture cached in _textures.Values) cached.Texture.Dispose();
            _textures.Clear();
            CachedTextureBytes = 0;
            foreach (Texture2D texture in _lightTextures.Values) texture.Dispose();
            _lightTextures.Clear();
            _lightMap?.Dispose();
            _lightMap = null;
            MultiplyBlend.Dispose();
            Feel.Dispose();
        }
    }
}
