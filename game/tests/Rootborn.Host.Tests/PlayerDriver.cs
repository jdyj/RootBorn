using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Rootborn.Host.Input;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 실제 게임 루프를 한 프레임씩 돌린다. MonoGame 내부를 리플렉션으로 부르는 유일한 곳이다.
    /// MonoGame 3.8.6.1의 RunOneFrame은 SDL 이벤트를 읽지 않는다(이벤트 처리는 Run()의 루프 안에만 있다).
    /// 그래서 프레임마다 내부 메서드 SdlRunLoop를 먼저 불러 이벤트를 처리하게 한다. MonoGame 버전을 올릴 때 여기를 먼저 확인한다.
    /// </summary>
    public sealed class FrameStepper
    {
        private readonly Game _game;
        private readonly object _platform;
        private readonly MethodInfo _pump;

        public FrameStepper(Game game)
        {
            _game = game;
            _game.RunOneFrame(); // 창을 만들고 초기화한다
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _platform = typeof(Game).GetField("Platform", any)?.GetValue(game) ?? typeof(Game).GetProperty("Platform", any)?.GetValue(game)
                ?? throw new InvalidOperationException("MonoGame 내부 Platform을 찾지 못했다. MonoGame 버전이 바뀌었는지 확인한다.");
            _pump = _platform.GetType().GetMethod("SdlRunLoop", any)
                ?? throw new InvalidOperationException("MonoGame 내부 SdlRunLoop를 찾지 못했다. MonoGame 버전이 바뀌었는지 확인한다.");
        }

        public void Step(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                _pump.Invoke(_platform, null);
                _game.RunOneFrame();
            }
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

    public enum TestKey { Up, Down, Left, Right, Tab, Enter, Backspace, Escape, LeftShift }

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

        public PlayerDriver(HostOptions options)
        {
            _evidenceDir = options.EvidenceDir;
            Directory.CreateDirectory(_evidenceDir);
            Game = new HostGame(options) { Pointer = _pointer };
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
