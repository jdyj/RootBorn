using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Core.Characters;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Interaction;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Core.World;
using Rootborn.Data.Save;
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
        private Texture2D _white;

        public LoadedMap Current => Maps.LoadMap(Session.Map.Id);
        public int Zoom { get; private set; } = 1;
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
            Session = new WorldSession(Maps, Maps.Catalog.Walker);
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
            PlayerLook = ResolvePlayerLook(options);
            UpdateLight(0f);
            RefreshInteractions();
        }

        /// <summary>시간대(Core LocationStateTimeSlots: 오전·오후·저녁·밤).</summary>
        public string TimeSlot => LocationStateTimeSlots.FromMinutes((int)(Clock.CurrentHour * 60f));

        /// <summary>맵이나 시간대가 바뀌었을 때만 NPC 자리와 후보를 다시 만든다(프레임마다 다시 만들지 않는다).</summary>
        private void RefreshInteractions()
        {
            string map = Session.Map.Id, slot = TimeSlot;
            int day = Clock.Day;
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

        /// <summary>상호작용 키: 대상이 무언가 하면 Core 이벤트가 나온다.</summary>
        public InteractionEvent? Interact() => Interactions.Press();

        /// <summary>
        /// 겉모습: 슬롯의 옷 선택(없으면 칸 기본값)과 성장 단계. 단계는 학생 진행 저장을 아직 읽지 않아 새 게임과 같은 1일차·학생으로 정한다.
        /// </summary>
        private CharacterLook ResolvePlayerLook(HostOptions options)
        {
            WearableState state = Slot != null
                ? PlayerWearables.Load(new FileSaveStore(options.SaveRoot), Slot.SlotId, SaveFileNames.DefaultPlayerId, SlotStart.Metadata, Content.Wearables, Content.Characters)
                : new WearableState();
            string stage = WearableRules.SelectStage(Content.Wearables.Stages, NewGameStudentDay, false);
            return CharacterLook.Resolve(Content.Characters, Content.Wearables, state, stage, null)
                ?? throw new InvalidOperationException("성장 단계 " + stage + "의 몸 그림이 없다");
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
            Clock.Tick(dt * _timeScale);
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
            if (!_lightSnapped) { _light.Snap(Clock.CurrentHour); _lightSnapped = true; }
            else _light.Update(Clock.CurrentHour, dt);
            Tint = _light.Tint;
        }

        public void Draw(SpriteBatch batch, int screenWidth, int screenHeight)
        {
            LoadedMap map = Current;
            MapGrid grid = map.World.Grid;
            Zoom = WorldCamera.Zoom(screenHeight, map.Entry.ViewCells, grid.CellSize);
            Walker walker = Session.Walker;
            float viewW = screenWidth / (float)Zoom, viewH = screenHeight / (float)Zoom;
            var offset = new Vector2(WorldCamera.Offset(walker.X, grid.PixelWidth, viewW), WorldCamera.Offset(walker.Y, grid.PixelHeight, viewH));
            CameraOffset = offset;

            _device.Clear(_background);
            Matrix view = Matrix.CreateTranslation(-offset.X, -offset.Y, 0f) * Matrix.CreateScale(Zoom, Zoom, 1f);
            _tint = new Color(Math.Clamp(Tint.R, 0f, 1f), Math.Clamp(Tint.G, 0f, 1f), Math.Clamp(Tint.B, 0f, 1f));
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, null, null, null, view);
            foreach (SpriteDraw s in map.Picture.Under) DrawSprite(batch, s);
            List<SpriteDraw> sorted = map.Picture.Sorted;
            // 플레이어와 NPC를 발 위치(몸 가운데 Y)로 정렬해 가구 사이에 끼운다.
            var actors = new List<(float sortY, int npc)>(_npcs.Count + 1) { (walker.Y, -1) };
            for (int i = 0; i < _npcs.Count; i++) actors.Add((_npcs[i].Y, i));
            actors.Sort((a, b) => a.sortY != b.sortY ? a.sortY.CompareTo(b.sortY) : a.npc.CompareTo(b.npc));
            int drawn = 0;
            foreach (var actor in actors)
            {
                int upTo = map.Picture.ActorIndex(actor.sortY);
                for (; drawn < upTo; drawn++) DrawSprite(batch, sorted[drawn]);
                if (actor.npc < 0) DrawCharacter(batch, walker); else DrawNpc(batch, _npcs[actor.npc]);
            }
            for (; drawn < sorted.Count; drawn++) DrawSprite(batch, sorted[drawn]);
            DrawTargetMarker(batch, grid.CellSize);
            batch.End();
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

        /// <summary>
        /// 잡힌 대상 위의 작은 표시(Unity 판은 대상 위 1.1칸쯤에 "[E] ..." 글자를 띄웠다. 글자 안내는 화면 옮기기 때 넣는다).
        /// 자동문처럼 눌러도 아무 일 없는 대상은 표시하지 않는다(Unity 판도 빈 안내였다).
        /// </summary>
        private void DrawTargetMarker(SpriteBatch batch, int cell)
        {
            InteractionCandidate target = Interactions.Target;
            if (target == null || !target.Acts) return;
            if (_white == null) { _white = new Texture2D(_device, 1, 1); _white.SetData(new[] { Color.White }); }
            int cx = (int)MathF.Round(target.X), top = (int)MathF.Round(target.Y - MarkerCellsAbove * cell);
            var outline = new Color(30, 24, 40);
            var fill = new Color(255, 245, 173); // Unity 안내 글자 색 (1, 0.96, 0.68)
            for (int row = 0; row < 5; row++)
            {
                int half = 4 - row; // 아래를 가리키는 삼각형
                batch.Draw(_white, new Rectangle(cx - half - 1, top + row, half * 2 + 3, 1), outline);
                if (half > 0) batch.Draw(_white, new Rectangle(cx - half + 0, top + row, half * 2 + 1, 1), fill);
            }
        }

        /// <summary>표시를 대상 가운데에서 몇 칸 위에 그릴지(Unity NpcInteractor 안내 높이 1.15).</summary>
        public const float MarkerCellsAbove = 1.15f;

        private Color _tint = Color.White;

        /// <summary>섞기는 알파를 곱하지 않은 색(NonPremultiplied)이라 색은 그대로 두고 알파만 바꾼다.</summary>
        private static Color WithAlpha(Color c, float alpha) => new Color(c.R, c.G, c.B, (byte)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255));

        public void Dispose()
        {
            _white?.Dispose();
            foreach (Texture2D texture in _textures.Values) texture.Dispose();
            _textures.Clear();
        }
    }
}
