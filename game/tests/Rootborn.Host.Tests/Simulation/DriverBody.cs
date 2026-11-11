using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Game;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Furniture;
using Rootborn.Simulation;

namespace Rootborn.Host.Tests.Simulation
{
    /// <summary>
    /// 실제 창 몸(장치 이벤트 주입, OS 입력 계층 미확인): 창 없는 몸과 같은 판단·관찰·불변식 코드를 실제 게임 루프에서 돌린다.
    /// 입력은 PlayerDriver의 동사만 쓴다(키·글자는 SDL 이벤트 큐, 포인터는 게임의 포인터 장치 계층). 읽기는 관찰과 불변식 검사에만 쓴다.
    /// 문 지나기는 PlayerDriver.WalkThroughDoor 대신 여기서 한다 — 그쪽은 문 가운데 쪽으로 키를 눌러 맵 가장자리를 따라 긴 문(해변)에 닿지 못한다
    /// (DoorDirection). 걷기 자체는 PlayerDriver.WalkNear(방향 키를 누르고 뗀다)다.
    /// </summary>
    public sealed class DriverBody : IPlayBody
    {
        private readonly string _saves;
        private readonly FrameCounter _counter;
        private ScreenStack _hooked;
        private HostPlacementView _placementView;
        private WorldScreen _placementWorld;

        public DriverBody(string repoRoot, string evidenceDir)
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-sim-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            HostOptions options = HostOptions.Resolve(new[] { "--root", repoRoot, "--save-root", _saves, "--size", "1920x1080", "--evidence", evidenceDir });
            options.TestMode = true;
            Player = new PlayerDriver(options);
            _counter = new FrameCounter(Player.Game);
            Player.Game.Components.Add(_counter);
            Player.Frames(3);
            Hook();
        }

        public PlayerDriver Player { get; }
        private HostGame Game => Player.Game;

        public ScreenStack Screens => Game.Screens;
        public GameText Text => Game.Text;
        public GameSession Session => Game.Session;
        public bool InWorld => Game.World != null && Game.Session != null;
        public WorldSession Walk => Game.World?.Session;
        public IReadOnlyList<InteractionCandidate> Candidates => Game.World?.Interactions.Candidates ?? (IReadOnlyList<InteractionCandidate>)Array.Empty<InteractionCandidate>();
        public InteractionCandidate Target => Game.World?.Interactions.Target;
        public FurnitureRoom Room => Game.World?.Room;
        public IPlacementView PlacementView
        {
            get
            {
                if (Game.World == null) return null;
                if (_placementWorld != Game.World) { _placementWorld = Game.World; _placementView = new HostPlacementView(Game.World, () => Game.Fit.Scale); }
                return _placementView;
            }
        }
        public float Width => Game.Fit.LogicalWidth;
        public float Height => Game.Fit.LogicalHeight;
        public long Frames => _counter.Count;
        public int CollisionViolations => Player.CollisionViolations;
        public string SaveRoot => _saves;
        public string SlotId => Game.Session?.SaveSlot;
        public (IPlacementGrid Grid, string Surface)? HouseEntered { get; private set; }

        public event Action<string> ScreenOpened;
        public event Action<InteractionEvent> Interacted;

        /// <summary>화면 쌓기가 바뀌었으면(슬롯을 고르면 월드 것으로) 알림을 다시 잇고, 가구 방이면 그 방을 기억한다(Host가 EnterHouse에 넘긴 것).</summary>
        private void Hook()
        {
            ScreenStack s = Game.Screens;
            if (s != null && s != _hooked)
            {
                _hooked = s;
                foreach (GameScreen open in s.OpenScreens) ScreenOpened?.Invoke(open.Id);
                s.Opened += screen => ScreenOpened?.Invoke(screen.Id);
                s.Interacted += (e, _) => Interacted?.Invoke(e);
            }
            if (Game.World != null && Game.World.Room != null && !string.IsNullOrEmpty(Game.World.HereData.PlacementSurface))
                HouseEntered = (Game.World.Room.Grid, Game.World.HereData.PlacementSurface);
        }

        public void Click(float x, float y) { Player.ClickAt(x, y); Hook(); }

        public void PressKey(ScreenKey key) { Player.PressKey(KeyOf(key)); Hook(); }

        public void TypeInto(float x, float y, string text)
        {
            Player.ClickAt(x, y);
            Player.TypeText(text);
            Player.PressKey(TestKey.Escape);
            Hook();
        }

        public void Wait(int frames) { Player.Frames(frames); Hook(); }

        public void Interact() { Player.PressKey(TestKey.E); Hook(); }

        public bool WalkNear(float x, float y, float within)
        {
            string map = Walk.Map.Id;
            try { Player.WalkNear(x, y, within); }
            catch (InvalidOperationException) { Player.ReleaseEverything(); Hook(); return false; }
            Hook();
            return Walk.Map.Id == map;
        }

        public bool WalkThroughDoor(DoorArea door)
        {
            string from = Walk.Map.Id;
            int size = Walk.Map.Grid.CellSize;
            HashSet<CellPos> reach = PlayObserver.Reachable(Walk.Map, CellPos.Containing(Walk.Walker.X, Walk.Walker.Y, size));
            // 문에 맞닿은 칸 중 지금 자리에서 가장 가까운(칸 거리) 칸 가운데까지 걷는다. 도중에 문을 지나면 그대로 성공.
            CellPos? best = null;
            int bestDistance = int.MaxValue;
            CellPos here = CellPos.Containing(Walk.Walker.X, Walk.Walker.Y, size);
            foreach (CellPos d in CollisionInput.CellsUnder(door.Area, size))
                foreach (CellPos c in new[] { d, new CellPos(d.X + 1, d.Y), new CellPos(d.X - 1, d.Y), new CellPos(d.X, d.Y + 1), new CellPos(d.X, d.Y - 1) })
                {
                    if (!reach.Contains(c)) continue;
                    int distance = Math.Abs(c.X - here.X) + Math.Abs(c.Y - here.Y);
                    if (distance < bestDistance) { bestDistance = distance; best = c; }
                }
            if (best.HasValue)
            {
                var center = CellPos.Center(best.Value, size);
                try { Player.WalkNear(center.X, center.Y, size * 0.5f); }
                catch (InvalidOperationException) { Player.ReleaseEverything(); }
            }
            if (Walk.Map.Id == from)
            {
                (int dx, int dy) = DoorDirection.Toward(door.Area, Walk.Walker.X, Walk.Walker.Y);
                TestKey key = dx > 0 ? TestKey.Right : dx < 0 ? TestKey.Left : dy > 0 ? TestKey.Down : TestKey.Up;
                Player.KeyDown(key);
                for (int i = 0; i < 240 && Walk.Map.Id == from && !Screens.MovementBlocked; i++) Player.Frames(1);
                Player.KeyUp(key);
            }
            for (int i = 0; i < 60 && Walk.Map.Id == from; i++) Player.Frames(1); // 문이 열리는 0.35초
            Player.Frames(2);
            Hook();
            return Walk.Map.Id != from;
        }

        public string Screenshot(string name) => Player.Screenshot(name);

        private static TestKey KeyOf(ScreenKey key)
        {
            switch (key)
            {
                case ScreenKey.Up: return TestKey.Up;
                case ScreenKey.Down: return TestKey.Down;
                case ScreenKey.Left: return TestKey.Left;
                case ScreenKey.Right: return TestKey.Right;
                case ScreenKey.Tab: return TestKey.Tab;
                case ScreenKey.Enter: return TestKey.Enter;
                case ScreenKey.Escape: return TestKey.Escape;
                case ScreenKey.Inventory: return TestKey.I;
                case ScreenKey.Status: return TestKey.C;
                case ScreenKey.Interact: return TestKey.E;
                case ScreenKey.Space: return TestKey.Space;
                case ScreenKey.Rotate: return TestKey.R;
                case ScreenKey.AltLeft: return TestKey.A;
                case ScreenKey.AltRight: return TestKey.D;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "장치 키가 없는 화면 키");
            }
        }

        public void Dispose()
        {
            try { Player.Dispose(); }
            finally { try { Directory.Delete(_saves, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }

    /// <summary>게임 루프의 Update 횟수(프레임 수). PlayerDriver를 고치지 않고 게임 구성 요소로 센다.</summary>
    internal sealed class FrameCounter : GameComponent
    {
        public FrameCounter(Microsoft.Xna.Framework.Game game) : base(game) { }
        public long Count { get; private set; }
        public override void Update(GameTime gameTime) => Count++;
    }
}
