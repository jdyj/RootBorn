using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Core.World;
using Rootborn.Host.Input;
using Rootborn.Screens;
using Rootborn.Ui;
using XnaGame = Microsoft.Xna.Framework.Game;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 실제 게임 루프를 한 프레임씩 돌린다. MonoGame 내부를 리플렉션으로 부르는 유일한 곳이다.
    /// MonoGame 3.8.6.1의 RunOneFrame은 SDL 이벤트를 읽지 않는다(이벤트 처리는 Run()의 루프 안에만 있다).
    /// 그래서 프레임마다 내부 메서드 SdlRunLoop를 먼저 불러 이벤트를 처리하게 한다. MonoGame 버전을 올릴 때 여기를 먼저 확인한다.
    /// </summary>
    public sealed class FrameStepper
    {
        private readonly XnaGame _game;
        private readonly object _platform;
        private readonly MethodInfo _pump;

        public FrameStepper(XnaGame game)
        {
            _game = game;
            _game.RunOneFrame(); // 창을 만들고 초기화한다
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _platform = typeof(XnaGame).GetField("Platform", any)?.GetValue(game) ?? typeof(XnaGame).GetProperty("Platform", any)?.GetValue(game)
                ?? throw new InvalidOperationException("MonoGame 내부 Platform을 찾지 못했다. MonoGame 버전이 바뀌었는지 확인한다.");
            _pump = _platform.GetType().GetMethod("SdlRunLoop", any)
                ?? throw new InvalidOperationException("MonoGame 내부 SdlRunLoop를 찾지 못했다. MonoGame 버전이 바뀌었는지 확인한다.");
            var graphics = game.Services.GetService(typeof(IGraphicsDeviceManager)) as GraphicsDeviceManager
                ?? throw new InvalidOperationException("GraphicsDeviceManager가 없다.");
            graphics.Disposed += (sender, e) => ReleaseClosedContexts();
        }

        public void Step(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                _pump.Invoke(_platform, null);
                _game.RunOneFrame();
            }
        }

        /// <summary>
        /// 닫은 게임의 GL 컨텍스트를 지운다. MonoGame 3.8.6.1은 GraphicsDevice를 닫을 때 컨텍스트를 정적 목록에 미뤄 두고,
        /// 그 목록은 Run()의 루프(SdlGamePlatform.RunLoop) 안에서만 GraphicsDevice.DisposeContexts()로 지운다.
        /// 테스트는 Run() 없이 한 프레임씩 돌리므로 그대로 두면 테스트마다 컨텍스트와 그 안의 텍스처가 프로세스 끝까지 남는다
        /// (2026-11-12: 130건에 10GB 넘게 쌓였다, HostMemoryTests).
        /// 때가 중요하다: Game.Dispose는 장치를 닫은 다음 플랫폼을 닫으며 SDL_Quit을 부르고, SDL이 꺼진 뒤의 SDL_GL_DeleteContext는
        /// 아무것도 하지 않는다. 그래서 장치가 닫힌 직후(GraphicsDeviceManager.Disposed, 플랫폼을 닫기 전)에 부른다.
        /// </summary>
        private static void ReleaseClosedContexts()
        {
            const BindingFlags anyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo release = typeof(GraphicsDevice).GetMethod("DisposeContexts", anyStatic)
                ?? throw new InvalidOperationException("MonoGame 내부 GraphicsDevice.DisposeContexts를 찾지 못했다. MonoGame 버전이 바뀌었는지 확인한다.");
            release.Invoke(null, null);
        }
    }

    /// <summary>테스트가 값을 넣는 포인터. 게임은 OS 마우스 대신 이것을 읽는다.</summary>
    public sealed class ScriptedPointer : IPointerSource
    {
        public int X = -100, Y = -100;
        public bool Down;
        public float PendingWheel;

        public PointerState Read()
        {
            float wheel = PendingWheel;
            PendingWheel = 0;
            return new PointerState(X, Y, Down, wheel);
        }
    }

    public enum TestKey { Up, Down, Left, Right, Tab, Enter, Backspace, Escape, LeftShift, E, Space, I, C, R, A, D, F, Equals, Minus, F11, L, F1 }

    /// <summary>
    /// 플레이어를 대신해 조작한다. 근거의 종류:
    /// - 키와 글자: SDL 이벤트 큐에 넣는다. MonoGame이 OS 이벤트와 같은 코드로 처리한다. OS 입력 계층은 거치지 않는다.
    /// - 포인터: 게임의 포인터 장치 계층(IPointerSource)에 값을 넣는다. MonoGame은 마우스를 OS에서 직접 읽어 이벤트 큐로는 넣을 수 없다.
    ///   클릭은 UI의 포인터 판정을 그대로 거친다. 요소의 메서드를 직접 부르지 않는다.
    /// </summary>
    public sealed class PlayerDriver : IDisposable
    {
        public readonly HostGame Game;
        private readonly FrameStepper _stepper;
        private readonly ScriptedPointer _pointer = new ScriptedPointer();
        private readonly HashSet<TestKey> _held = new HashSet<TestKey>();
        private readonly string _evidenceDir;

        /// <param name="extraScreens">등록 표에 시험용 화면을 더 얹는다(성능 측정용 화면 등).</param>
        public PlayerDriver(HostOptions options, Action<ScreenRegistry> extraScreens = null)
        {
            _evidenceDir = options.EvidenceDir;
            Directory.CreateDirectory(_evidenceDir);
            options.SystemLanguage = "ko"; // 운영체제 언어에 기대지 않는다(--lang도 설정도 없으면 기준 언어와 같은 ko)
            Game = new HostGame(options) { Pointer = _pointer, ExtraScreens = extraScreens };
            _stepper = new FrameStepper(Game);
        }

        public void Frames(int count = 1) => _stepper.Step(count);

        // SDL2 스캔코드와 키코드
        private static (int scancode, int sym) Codes(TestKey key)
        {
            switch (key)
            {
                case TestKey.Enter: return (40, 13);
                case TestKey.Escape: return (41, 27);
                case TestKey.Backspace: return (42, 8);
                case TestKey.Tab: return (43, 9);
                case TestKey.Right: return (79, 0x4000004F);
                case TestKey.Left: return (80, 0x40000050);
                case TestKey.Down: return (81, 0x40000051);
                case TestKey.Up: return (82, 0x40000052);
                case TestKey.LeftShift: return (225, 0x400000E1);
                case TestKey.E: return (8, 'e');
                case TestKey.Space: return (44, ' ');
                case TestKey.I: return (12, 'i');
                case TestKey.C: return (6, 'c');
                case TestKey.R: return (21, 'r');
                case TestKey.A: return (4, 'a');
                case TestKey.D: return (7, 'd');
                case TestKey.F: return (9, 'f');
                case TestKey.Equals: return (46, '=');
                case TestKey.Minus: return (45, '-');
                case TestKey.F11: return (68, 0x40000044);
                case TestKey.L: return (15, 'l');
                case TestKey.F1: return (58, 0x4000003A);
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }

        public void KeyDown(TestKey key)
        {
            if (!_held.Add(key)) return;
            (int scancode, int sym) = Codes(key);
            Sdl2Native.PushKey(Game.Window.Handle, scancode, sym, true);
            Frames(2);
        }

        public void KeyUp(TestKey key)
        {
            if (!_held.Remove(key)) return;
            (int scancode, int sym) = Codes(key);
            Sdl2Native.PushKey(Game.Window.Handle, scancode, sym, false);
            Frames(2);
        }

        /// <summary>키를 눌렀다 뗀다(누름과 뗌 사이에 프레임이 지난다).</summary>
        public void PressKey(TestKey key, int times = 1)
        {
            for (int i = 0; i < times; i++) { KeyDown(key); KeyUp(key); }
        }

        /// <summary>논리 좌표(1920x1080 기준)로 포인터를 옮긴다.</summary>
        public void MovePointer(float x, float y)
        {
            _pointer.X = (int)MathF.Round(x * Game.Fit.Scale);
            _pointer.Y = (int)MathF.Round(y * Game.Fit.Scale);
            Frames(2);
        }

        public UiNode Node(string id) => Game.Ui.Root.Find(id) ?? throw new InvalidOperationException("UI 요소가 없다: " + id);

        public void MovePointerTo(string id)
        {
            UiRect rect = Node(id).Rect;
            MovePointer(rect.CenterX, rect.CenterY);
        }

        public void PressPointer() { _pointer.Down = true; Frames(2); }
        public void ReleasePointer() { _pointer.Down = false; Frames(2); }

        public void Click(string id) { MovePointerTo(id); PressPointer(); ReleasePointer(); }
        public void ClickAt(float x, float y) { MovePointer(x, y); PressPointer(); ReleasePointer(); }

        /// <summary>휠을 굴린다. 위로 굴리면 양수.</summary>
        public void Wheel(float notches)
        {
            _pointer.PendingWheel += notches;
            Frames(2);
        }

        /// <summary>누른 채로 (dx, dy)만큼 여러 프레임에 걸쳐 옮긴다. 떼지는 않는다.</summary>
        public void DragPointerBy(float dx, float dy, int steps = 8)
        {
            float startX = _pointer.X / Game.Fit.Scale, startY = _pointer.Y / Game.Fit.Scale;
            for (int i = 1; i <= steps; i++)
            {
                _pointer.X = (int)MathF.Round((startX + dx * i / steps) * Game.Fit.Scale);
                _pointer.Y = (int)MathF.Round((startY + dy * i / steps) * Game.Fit.Scale);
                Frames(1);
            }
            Frames(1);
        }

        /// <summary>확정된 글자를 SDL 글자 입력 이벤트로 넣는다. 입력기의 조합 과정은 거치지 않는다.</summary>
        public void TypeText(string text)
        {
            foreach (char c in text)
            {
                Sdl2Native.PushText(Game.Window.Handle, c.ToString());
                Frames(1);
            }
            Frames(1);
        }

        /// <summary>조합 중 글자 이벤트를 넣는다(입력기가 조합 중일 때 보내는 것과 같은 이벤트).</summary>
        public void Compose(string text)
        {
            Sdl2Native.PushTextEditing(Game.Window.Handle, text);
            Frames(2);
        }

        // ---------- 월드: 걷기와 문 ----------
        // 어디로 갈지(칸 경로)만 Core의 GridPath로 정하고, 움직임은 언제나 방향 키를 누르고 떼는 것으로만 만든다.
        // 매 프레임 몸 상자가 막힌 칸에 들어갔는지 세어 둔다(CollisionViolations).

        private const int MaxFramesPerRun = 900;

        public WorldSession World => Game.World?.Session ?? throw new InvalidOperationException("월드 화면이 아니다(--world)");
        public int CollisionViolations { get; private set; }
        public int WorldFrames { get; private set; }

        public CellPos PlayerCell
        {
            get
            {
                Walker w = World.Walker;
                return CellPos.Containing(w.X, w.Y, World.Map.Grid.CellSize);
            }
        }

        private void WorldFrame()
        {
            Frames(1);
            WorldFrames++;
            if (World.Map.Collision.AnyBlocked(World.Walker.Box)) CollisionViolations++;
        }

        private static TestKey KeyFor(int dx, int dy) => dx > 0 ? TestKey.Right : dx < 0 ? TestKey.Left : dy > 0 ? TestKey.Down : TestKey.Up;

        /// <summary>
        /// 방향 키 하나를 누르고 있다가, 그 축으로 목표 좌표에 닿으면(한 프레임 걸음의 반 안) 뗀다. 맵이 바뀌면 바로 떼고 false.
        /// </summary>
        private bool HoldUntil(int dx, int dy, float target, string what)
        {
            string map = World.Map.Id;
            float Remaining() => dx != 0 ? (target - World.Walker.X) * dx : (target - World.Walker.Y) * dy;
            float step = World.Map.Grid.CellSize * 4f / 60f;
            TestKey key = KeyFor(dx, dy);
            KeyDown(key);
            WorldFrames += 2;
            int frames = 0;
            while (World.Map.Id == map && Remaining() > step / 2f)
            {
                if (++frames > MaxFramesPerRun) { KeyUp(key); throw new InvalidOperationException(what + ": " + MaxFramesPerRun + "프레임 안에 닿지 못했다. 지금 " + World.Walker.X + "," + World.Walker.Y); }
                WorldFrame();
            }
            KeyUp(key);
            WorldFrames += 2;
            return World.Map.Id == map;
        }

        /// <summary>칸 경로를 따라 걷는다. 같은 방향 칸은 한 번에 누르고 간다. 도중에 문을 지나 맵이 바뀌면 false.</summary>
        private bool FollowPath(Func<CellPos, bool> goal, string what)
        {
            CellPos start = PlayerCell;
            List<CellPos> path = GridPath.Find(World.Map.Collision, start, goal) ?? throw new InvalidOperationException(what + ": " + World.Map.Id + "에서 " + start + "부터 갈 길이 없다");
            int size = World.Map.Grid.CellSize;
            int i = 0;
            while (i < path.Count)
            {
                CellPos from = i == 0 ? start : path[i - 1];
                int dx = path[i].X - from.X, dy = path[i].Y - from.Y;
                int j = i;
                while (j + 1 < path.Count && path[j + 1].X - path[j].X == dx && path[j + 1].Y - path[j].Y == dy) j++;
                var center = CellPos.Center(path[j], size);
                if (!HoldUntil(dx, dy, dx != 0 ? center.X : center.Y, what + " → " + path[j])) return false;
                i = j + 1;
            }
            return true;
        }

        /// <summary>걸어가기: 칸 경로를 찾아 방향 키로 그 칸 가운데까지 걷는다.</summary>
        public void WalkTo(CellPos cell)
        {
            if (!FollowPath(c => c.Equals(cell), "걸어가기 " + cell)) throw new InvalidOperationException("걸어가는 도중 맵이 바뀌었다: " + World.Map.Id);
        }

        /// <summary>맵의 한 지점(픽셀) 가까이까지 걷는다: 가운데가 그 지점에서 within 픽셀 안인 칸 중 가장 가까운 길의 끝.</summary>
        public void WalkNear(float x, float y, float within)
        {
            int size = World.Map.Grid.CellSize;
            bool Close(CellPos c)
            {
                var center = CellPos.Center(c, size);
                float dx = center.X - x, dy = center.Y - y;
                return dx * dx + dy * dy <= within * within;
            }
            if (Close(PlayerCell)) return;
            if (!FollowPath(Close, "가까이 걷기 (" + x + "," + y + ")")) throw new InvalidOperationException("걸어가는 도중 맵이 바뀌었다: " + World.Map.Id);
        }

        /// <summary>
        /// 문 통과: 문 칸이나 문에 맞닿은 칸까지 걸어간 뒤 문 쪽으로 키를 누르고 있다가 맵이 바뀌면 뗀다.
        /// target은 문 엔티티의 목적지 값(예: town, house, shopping center). 바뀐 맵 id를 돌려준다.
        /// </summary>
        public string WalkThroughDoor(string target)
        {
            string from = World.Map.Id;
            DoorArea door = null;
            foreach (DoorArea d in World.Map.Doors)
                if (d.Destination.HasValue && DoorRoutes.Normalize(d.Target) == DoorRoutes.Normalize(target)) { door = d; break; }
            if (door == null) throw new InvalidOperationException(from + "에 '" + target + "' 문이 없다");
            int size = World.Map.Grid.CellSize;
            var doorCells = new HashSet<CellPos>(CollisionInput.CellsUnder(door.Area, size));
            bool Near(CellPos c) => doorCells.Contains(c) || doorCells.Contains(new CellPos(c.X + 1, c.Y)) || doorCells.Contains(new CellPos(c.X - 1, c.Y))
                || doorCells.Contains(new CellPos(c.X, c.Y + 1)) || doorCells.Contains(new CellPos(c.X, c.Y - 1));
            if (FollowPath(Near, "문 " + target + "까지"))
            {
                Walker w = World.Walker;
                // 문 사각형에서 가장 가까운 점 쪽으로 누른다(가운데 쪽이면 맵 가장자리를 따라 긴 문의 끝에서 옆으로만 걷는다 — SIM-002).
                float ddx = Math.Max(door.Area.X, Math.Min(w.X, door.Area.X + door.Area.W)) - w.X, ddy = Math.Max(door.Area.Y, Math.Min(w.Y, door.Area.Y + door.Area.H)) - w.Y;
                if (Math.Abs(ddx) < 0.001f && Math.Abs(ddy) < 0.001f) { ddx = door.Area.CenterX - w.X; ddy = door.Area.CenterY - w.Y; }
                int dx = Math.Abs(ddx) > Math.Abs(ddy) ? Math.Sign(ddx) : 0, dy = dx == 0 ? Math.Sign(ddy) : 0;
                TestKey key = KeyFor(dx, dy);
                KeyDown(key);
                WorldFrames += 2;
                for (int i = 0; i < 240 && World.Map.Id == from; i++) WorldFrame();
                KeyUp(key);
            }
            for (int i = 0; i < 60 && World.Map.Id == from; i++) WorldFrame(); // 문이 열리는 0.35초
            if (World.Map.Id == from) throw new InvalidOperationException("문 '" + target + "'을 지나지 못했다. 지금 " + World.Walker.X + "," + World.Walker.Y);
            Frames(2);
            return World.Map.Id;
        }

        /// <summary>한 방향 키를 정해진 프레임 동안 누르고 있는다(벽에 부딪히는지 볼 때).</summary>
        public void HoldKey(TestKey key, int frames)
        {
            KeyDown(key);
            WorldFrames += 2;
            for (int i = 0; i < frames; i++) WorldFrame();
            KeyUp(key);
        }

        // ---------- 화면 ----------

        public ScreenStack Screens => Game.Screens ?? throw new InvalidOperationException("화면 쌓기가 없다(월드 화면이 아니다)");

        /// <summary>화면이 열릴 때까지 한 프레임씩 돌린다. 지나간 프레임 수를 돌려준다.</summary>
        public int WaitForScreen(string screenId, int maxFrames = 120)
        {
            for (int i = 0; i <= maxFrames; i++)
            {
                if (Screens.IsOpen(screenId)) return i;
                Frames(1);
            }
            throw new InvalidOperationException(maxFrames + "프레임 안에 화면이 열리지 않았다: " + screenId);
        }

        public int WaitForScreenClosed(string screenId, int maxFrames = 120)
        {
            for (int i = 0; i <= maxFrames; i++)
            {
                if (!Screens.IsOpen(screenId)) return i;
                Frames(1);
            }
            throw new InvalidOperationException(maxFrames + "프레임 안에 화면이 닫히지 않았다: " + screenId);
        }

        /// <summary>키를 눌러 화면을 열고 열릴 때까지 기다린다.</summary>
        public void OpenScreenWithKey(TestKey key, string screenId)
        {
            if (Screens.IsOpen(screenId)) throw new InvalidOperationException("이미 열려 있다: " + screenId);
            PressKey(key);
            WaitForScreen(screenId);
        }

        /// <summary>키를 눌러 화면을 닫고 닫힐 때까지 기다린다.</summary>
        public void CloseScreenWithKey(TestKey key, string screenId)
        {
            if (!Screens.IsOpen(screenId)) throw new InvalidOperationException("열려 있지 않다: " + screenId);
            PressKey(key);
            WaitForScreenClosed(screenId);
        }

        /// <summary>
        /// id로 찾은 버튼을 포인터로 누른다. 요소의 메서드를 부르지 않고 포인터를 그 가운데로 옮겨 누르고 뗀다(UI 포인터 판정을 거친다).
        /// 그 자리의 맨 위 요소가 그 버튼이 아니면(다른 화면에 가려졌으면) 누르지 않고 실패한다.
        /// </summary>
        public void PressButton(string id)
        {
            UiNode node = Node(id);
            if (!node.IsActive) throw new InvalidOperationException("보이지 않거나 비활성인 요소: " + id);
            UiRect rect = node.Rect;
            UiNode top = Game.Ui.HitTest(rect.CenterX, rect.CenterY);
            if (top != node) throw new InvalidOperationException("요소 " + id + " 위에 다른 요소가 있다: " + (top?.Id ?? "(없음)"));
            Click(id);
        }

        /// <summary>
        /// 화면 스크린샷: 전체 화면(name.png)과 그 화면의 뿌리 사각형만 잘라 낸 그림(name-crop.png). 잘라 낸 그림 경로를 돌려준다.
        /// </summary>
        public string ScreenshotScreen(string screenId, string name)
        {
            Screenshot(name);
            GameScreen screen = Screens.Find(screenId) ?? throw new InvalidOperationException("만들어진 적 없는 화면: " + screenId);
            PixelRect r = Game.Fit.ToPixels(screen.Root.Rect);
            RenderTarget2D scene = Game.Scene;
            int x = Math.Max(0, r.X), y = Math.Max(0, r.Y);
            int w = Math.Min(scene.Width, r.X + r.W) - x, h = Math.Min(scene.Height, r.Y + r.H) - y;
            var pixels = new Microsoft.Xna.Framework.Color[w * h];
            scene.GetData(0, new Microsoft.Xna.Framework.Rectangle(x, y, w, h), pixels, 0, pixels.Length);
            string path = Path.Combine(_evidenceDir, name + "-crop.png");
            using (var crop = new Texture2D(Game.GraphicsDevice, w, h))
            {
                crop.SetData(pixels);
                using FileStream stream = File.Create(path);
                crop.SaveAsPng(stream, w, h);
            }
            return path;
        }

        /// <summary>지금 칸에서 칸 거리로 cells 이상 떨어진 곳까지 방향 키로 걷는다(어느 쪽이든 막히지 않은 길).</summary>
        public void WalkAway(int cells)
        {
            CellPos start = PlayerCell;
            if (!FollowPath(c => Math.Abs(c.X - start.X) + Math.Abs(c.Y - start.Y) >= cells, "걸어 나가기 " + cells + "칸"))
                throw new InvalidOperationException("걸어가는 도중 맵이 바뀌었다: " + World.Map.Id);
        }

        public string Screenshot(string name)
        {
            string path = Path.Combine(_evidenceDir, name + ".png");
            Game.SaveScreenshot(path);
            return path;
        }

        /// <summary>눌린 키와 버튼을 모두 떼고 포인터를 화면 밖으로 뺀다.</summary>
        public void ReleaseEverything()
        {
            foreach (TestKey key in new List<TestKey>(_held)) KeyUp(key);
            _pointer.Down = false;
            _pointer.X = -100; _pointer.Y = -100;
            _pointer.PendingWheel = 0;
            Frames(2);
        }

        public void Dispose()
        {
            try { ReleaseEverything(); }
            finally { Game.Dispose(); }
        }
    }
}
