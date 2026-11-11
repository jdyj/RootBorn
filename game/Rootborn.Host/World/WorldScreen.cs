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
    /// 그리는 순서: 타일 층(모두 캐릭터 아래) → 발 위치로 정렬된 가구와 캐릭터. 배율은 맵이 정한 보이는 높이에 가장 가까운 정수,
    /// 카메라는 캐릭터를 따라가되 맵 밖을 비추지 않는다(맵이 화면보다 작으면 가운데).
    /// </summary>
    public sealed class WorldScreen : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
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
        private readonly DayNightLight _light;
        private bool _lightSnapped;
        /// <summary>지금 화면에 입힌 빛 색(낮밤이 없는 맵은 흰색).</summary>
        public LightColor Tint { get; private set; } = LightColor.White;

        /// <summary>플레이어 앞의 상호작용 대상(Core). 상호작용 키를 누르면 Interacted 이벤트가 나온다.</summary>
        public readonly InteractionTargets Interactions;
        /// <summary>지금 맵에 선 NPC(일정과 시간대로 정한다).</summary>
        public IReadOnlyList<PlacedNpc> Npcs => _npcs;
        /// <summary>지금까지 나온 상호작용 이벤트(대화창 등은 다음 프로젝트에서 이것을 받는다).</summary>
        public readonly List<InteractionEvent> InteractionLog = new List<InteractionEvent>();
        private readonly InteractionData _interactionData;
        private readonly NpcArt _npcArt;
        private readonly NpcScheduleLookupCache _schedules;
        private readonly NpcScheduleAccess _scheduleAccess;
        private List<PlacedNpc> _npcs = new List<PlacedNpc>();
        private string _placedMap, _placedSlot;
        private int _placedDay;

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

        /// <summary>새 게임의 학생 일차(Unity StudentLifeProgress 첫날).</summary>
        public const int NewGameStudentDay = 1;

        public WorldScreen(GraphicsDevice device, HostOptions options)
        {
            _device = device;
            string catalog = options.WorldMapsPath ?? Path.Combine(options.RepoRoot, WorldCatalog.DefaultRelativePath);
            Maps = new WorldMapLoader(options.RepoRoot, WorldCatalog.Load(catalog));
            Content = GameContent.Load(options.RepoRoot);
            Clock = Content.NewClock();
            Clock.SetCurrentHour(options.StartHour);
            _timeScale = options.TimeScale;
            _light = new DayNightLight(Content.Lighting);
            _interactionData = InteractionData.Load(options.InteractionsPath ?? Path.Combine(options.RepoRoot, InteractionData.DefaultRelativePath));
            Interactions = new InteractionTargets(_interactionData.RadiusCells);
            Interactions.Interacted += e => { InteractionLog.Add(e); Console.WriteLine("[상호작용] " + e); };
            _npcArt = Content.Npcs;
            _schedules = new NpcScheduleLookupCache(Content.Definitions.All<NpcSchedule>());
            _scheduleAccess = new NpcScheduleAccess(_schedules);
            _repoRoot = options.RepoRoot;
            FurnitureArt = new FurnitureArt(Path.Combine(options.RepoRoot, "data"));
            Rooms = new RoomAwareMaps(Maps, _interactionData);
            Session = new WorldSession(Rooms, Maps.Catalog.Walker);
            string startMap;
            if (options.SaveSlot != null)
            {
                Slot = new WorldSlot(new FileSaveStore(options.SaveRoot), options.SaveSlot, WorldTravelData.Load(options.RepoRoot));
                SlotStart = Slot.Begin(DateTime.UtcNow.Ticks);
                if (!SlotStart.Ok) throw new InvalidOperationException("세이브 슬롯으로 시작하지 못했다: " + SlotStart.Problem);
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
            foreach (string problem in Current.Problems) Console.Error.WriteLine("[월드] " + startMap + ": " + problem);
            _look = LoadWearables(options);
            PlayerLook = ResolveLook(WearableRules.SelectStage(Content.Wearables.Stages, NewGameStudentDay, false));
            UpdateLight(0f);
            RefreshInteractions();
        }

        /// <summary>시간대(Core LocationStateTimeSlots: 오전·오후·저녁·밤).</summary>
        public string TimeSlot => LocationStateTimeSlots.FromMinutes((int)(CurrentHour * 60f));

        private Func<int> _studentDay;
        private Func<float> _studentHour;

        /// <summary>
        /// 게임 한 판이 있으면 낮밤과 NPC 자리를 학생 날·시각으로 정한다(Unity 판: 마을 낮밤과 NPC 일정이 학생 시각을 따랐다).
        /// 그 뒤로 게임 시계(Clock)는 흐르지 않는다. 시각은 활동으로만 흐른다.
        /// </summary>
        public void FollowStudentTime(Func<int> day, Func<float> hour)
        {
            _studentDay = day ?? throw new ArgumentNullException(nameof(day));
            _studentHour = hour ?? throw new ArgumentNullException(nameof(hour));
            _lightSnapped = false;
            UpdateLight(0f);
            RefreshInteractions();
        }

        /// <summary>지금 시각(0~24). 학생 시각을 따르면 그것, 아니면 게임 시계.</summary>
        public float CurrentHour => _studentHour != null ? _studentHour() : Clock.CurrentHour;

        /// <summary>지금 날. 학생 날을 따르면 그것, 아니면 게임 시계.</summary>
        public int CurrentDay => _studentDay != null ? _studentDay() : Clock.Day;

        /// <summary>맵이나 시간대가 바뀌었을 때만 NPC 자리와 후보를 다시 만든다(프레임마다 다시 만들지 않는다).</summary>
        private void RefreshInteractions()
        {
            string map = Session.Map.Id, slot = TimeSlot;
            int day = CurrentDay;
            if (map == _placedMap && slot == _placedSlot && day == _placedDay) { Interactions.Update(Session.Walker.X, Session.Walker.Y); return; }
            _placedMap = map; _placedSlot = slot; _placedDay = day;
            MapInteractions here = _interactionData.Map(map);
            string LocationOf(string npcId) => _schedules.TryGetByNpcId(npcId, out NpcSchedule s) ? _scheduleAccess.ResolveLocationId(s.Id, day, slot, null) : string.Empty;
            _npcs = NpcPlacement.Place(_npcArt.Order, LocationOf, here);
            Interactions.MapId = map;
            Interactions.RadiusPixels = _interactionData.RadiusCells * Session.Map.Grid.CellSize;
            Interactions.SetCandidates(NpcPlacement.Candidates(_interactionData, here, _npcs, Session.Map.Doors));
            Interactions.Update(Session.Walker.X, Session.Walker.Y);
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

        private Texture2D Load(string path)
        {
            if (_textures.TryGetValue(path, out Texture2D texture)) return texture;
            using (FileStream stream = File.OpenRead(path)) texture = Texture2D.FromStream(_device, stream); // PNG를 그 자리에서 읽는다
            _textures[path] = texture;
            return texture;
        }

        /// <summary>한 프레임: 입력 방향과 지난 시간으로 걷고, 문을 지나면 맵이 바뀐다.</summary>
        public void Update(int dx, int dy, float dt)
        {
            string before = Session.Map.Id;
            Session.Update(dx, dy, dt);
            if (Session.Map.Id != before)
                foreach (string problem in Current.Problems) Console.Error.WriteLine("[월드] " + Session.Map.Id + ": " + problem);
            PlayerAnimator.Advance(Session.Walker.Moving, Session.Walker.Facing, dt);
            if (_studentHour == null) Clock.Tick(dt * _timeScale);
            UpdateLight(dt);
            RefreshInteractions();
        }

        /// <summary>낮밤이 있는 맵은 시각의 빛을 따라가고(들어선 순간에는 바로 맞춤), 없는 맵은 흰색.</summary>
        private void UpdateLight(float dt)
        {
            if (!Current.Entry.DayNight)
            {
                _lightSnapped = false;
                Tint = LightColor.White;
                return;
            }
            if (!_lightSnapped) { _light.Snap(CurrentHour); _lightSnapped = true; }
            else _light.Update(CurrentHour, dt);
            Tint = _light.Tint;
        }

        public void Draw(SpriteBatch batch, int screenWidth, int screenHeight)
        {
            LoadedMap map = Current;
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
            }
            else
            {
                Scale = Zoom;
                float viewW = screenWidth / (float)Zoom, viewH = screenHeight / (float)Zoom;
                offset = new Vector2(WorldCamera.Offset(walker.X, grid.PixelWidth, viewW), WorldCamera.Offset(walker.Y, grid.PixelHeight, viewH));
            }
            CameraOffset = offset;

            _device.Clear(_background);
            Matrix view = Matrix.CreateTranslation(-offset.X, -offset.Y, 0f) * Matrix.CreateScale(Scale, Scale, 1f);
            _tint = new Color(Math.Clamp(Tint.R, 0f, 1f), Math.Clamp(Tint.G, 0f, 1f), Math.Clamp(Tint.B, 0f, 1f));
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, view);
            foreach (SpriteDraw s in map.Picture.Under) DrawSprite(batch, s);
            List<SpriteDraw> sorted = SortedWithFurniture(map);
            // 플레이어와 NPC를 발 위치(몸 가운데 Y)로 정렬해 가구 사이에 끼운다.
            var actors = new List<(float sortY, int npc)>(_npcs.Count + 1) { (walker.Y, -1) };
            for (int i = 0; i < _npcs.Count; i++) actors.Add((_npcs[i].Y, i));
            actors.Sort((a, b) => a.sortY != b.sortY ? a.sortY.CompareTo(b.sortY) : a.npc.CompareTo(b.npc));
            int drawn = 0;
            foreach (var actor in actors)
            {
                int upTo = ActorIndex(sorted, actor.sortY);
                for (; drawn < upTo; drawn++) DrawSprite(batch, sorted[drawn]);
                if (actor.npc < 0) DrawCharacter(batch, walker); else DrawNpc(batch, _npcs[actor.npc]);
            }
            for (; drawn < sorted.Count; drawn++) DrawSprite(batch, sorted[drawn]);
            batch.End();
        }

        /// <summary>
        /// 놓인 가구를 그릴 목록으로 바꾼다(Unity PlacedFurnitureDepthPresentation): 그림 칸마다 좌표표의 그림을 기준점이 칸 가운데에 오게,
        /// 크기는 그림 픽셀 / PPU 칸. 앞뒤는 그 칸 가운데의 높이로 캐릭터·맵 가구와 함께 정렬한다. 지금 방이 아니면 비운다.
        /// 가구가 바뀔 때만 부른다(Host가 게임 명령의 바뀜 알림에서).
        /// </summary>
        public void SetPlacedFurniture(IReadOnlyList<PlacedFurniture> placed)
        {
            _furniture.Clear();
            _sortedDirty = true;
            FurnitureRoom room = Room;
            if (room == null || placed == null) return;
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
            DrawLayer(batch, PlayerLook.Body, body);
            foreach (CharacterSheet layer in PlayerLook.Layers) DrawLayer(batch, layer, body);
        }

        private void DrawLayer(SpriteBatch batch, CharacterSheet sheet, AreaRect body)
        {
            CharacterFrame f = sheet.Frame(PlayerAnimator.Row, PlayerAnimator.Column);
            if (f == null) return;
            var position = new Vector2(MathF.Round(body.CenterX - f.W * f.PivotX), MathF.Round(body.Bottom - f.H * f.PivotY));
            batch.Draw(Load(f.Texture), position, new Rectangle(f.X, f.Y, f.W, f.H), _tint);
        }

        /// <summary>NPC 그림: 기준점(발)을 자리에서 반 칸 아래(몸 가운데가 자리)에 맞춘다 — 플레이어와 같은 기준.</summary>
        private void DrawNpc(SpriteBatch batch, PlacedNpc npc)
        {
            CharacterFrame f = _npcArt.Frame(npc.NpcId);
            if (f == null) return;
            float feet = npc.Y + Current.World.Grid.CellSize / 2f;
            var position = new Vector2(MathF.Round(npc.X - f.W * f.PivotX), MathF.Round(feet - f.H * f.PivotY));
            batch.Draw(Load(f.Texture), position, new Rectangle(f.X, f.Y, f.W, f.H), _tint);
        }

        // 잡힌 대상 위의 "[E] …" 안내 글자는 화면 쌓기의 HUD(InteractionPromptScreen)가 그린다. Host가 대상과 그 화면 위치를 넘긴다.

        private Color _tint = Color.White;

        /// <summary>섞기는 알파를 곱하지 않은 색(NonPremultiplied)이라 색은 그대로 두고 알파만 바꾼다.</summary>
        private static Color WithAlpha(Color c, float alpha) => new Color(c.R, c.G, c.B, (byte)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255));

        public void Dispose()
        {
            foreach (Texture2D texture in _textures.Values) texture.Dispose();
            _textures.Clear();
        }
    }
}
