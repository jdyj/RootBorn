using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rootborn.Screens;
using Rootborn.Screens.KeyGuide;

namespace Rootborn.Host.Input
{
    /// <summary>창 안 픽셀 좌표, 왼쪽 버튼, 지난번 읽은 뒤로 굴린 휠 칸 수(위로 굴리면 양수).</summary>
    public readonly record struct PointerState(int X, int Y, bool LeftDown, float Wheel);

    /// <summary>포인터 장치를 읽는 유일한 곳. 실제 실행은 OS 마우스, 테스트는 넣어 준 값.</summary>
    public interface IPointerSource
    {
        PointerState Read();
    }

    public sealed class OsPointer : IPointerSource
    {
        private const float WheelUnitsPerNotch = 120f;
        private int _lastWheel;
        private bool _hasWheel;

        public PointerState Read()
        {
            MouseState m = Mouse.GetState();
            float wheel = _hasWheel ? (m.ScrollWheelValue - _lastWheel) / WheelUnitsPerNotch : 0f;
            _lastWheel = m.ScrollWheelValue;
            _hasWheel = true;
            return new PointerState(m.X, m.Y, m.LeftButton == ButtonState.Pressed, wheel);
        }
    }

    /// <summary>이번 프레임에 새로 눌린 키. Shift와 Held*는 누르고 있는 상태다(걷기는 화살표나 WASD를 누르고 있는 동안).</summary>
    public struct KeyFrame
    {
        public bool Up, Down, Left, Right, Tab, Enter, Escape, Screenshot, Shift;
        /// <summary>Unity ModernUiPanelInputRouter의 패널 키: I 가방, C 상태.</summary>
        public bool Inventory, Status;
        /// <summary>상호작용 키(Unity GatherInteractor: E, Space, 왼쪽·오른쪽 Ctrl)가 이번 프레임에 눌렸다.</summary>
        public bool Interact;
        /// <summary>미니게임 키(Unity MinigamePanel): Space 탭, R 회전, A·D 줄 이동(누른 순간만).</summary>
        public bool Space, Rotate, AltLeft, AltRight;
        public bool HeldUp, HeldDown, HeldLeft, HeldRight;
        /// <summary>가구 배치 카메라(Unity HousePlacementCameraController): F 전체 보기(눌림), = 또는 키패드 + 가까이·- 멀리(누르고 있음).</summary>
        public bool FullView, ZoomInHeld, ZoomOutHeld;
        /// <summary>F11: 창 모드 ↔ 테두리 없는 전체 화면(Unity 판과 같은 키).</summary>
        public bool ToggleDisplay;
        /// <summary>L: 장소 기록(사용자 결정 2026-11-12, Unity 판에는 키가 없었다).</summary>
        public bool LocationLog;
        /// <summary>F1: 단축키 안내.</summary>
        public bool Help;
        /// <summary>화면을 여는 글자 키(KeyGuideNames.ScreenOpeners의 순서대로 한 비트씩)가 이번 프레임에 눌렸다.</summary>
        public uint Openers;

        public bool Opener(int index) => (Openers & (1u << index)) != 0;

        /// <summary>누르고 있는 방향 키로 정한 걷기 방향(-1, 0, 1). 반대 방향을 함께 누르면 0.</summary>
        public int MoveX => (HeldRight ? 1 : 0) - (HeldLeft ? 1 : 0);
        public int MoveY => (HeldDown ? 1 : 0) - (HeldUp ? 1 : 0);
    }

    /// <summary>키보드 장치를 읽는 유일한 곳. 한 프레임 안에 눌렀다 뗀 키는 보이지 않는다.</summary>
    public sealed class KeyboardDevice
    {
        private KeyboardState _previous;

        public KeyFrame Read()
        {
            KeyboardState now = Keyboard.GetState();
            var frame = new KeyFrame
            {
                Up = Pressed(now, Keys.Up),
                Down = Pressed(now, Keys.Down),
                Left = Pressed(now, Keys.Left),
                Right = Pressed(now, Keys.Right),
                Tab = Pressed(now, Keys.Tab),
                Enter = Pressed(now, Keys.Enter),
                Escape = Pressed(now, Keys.Escape),
                Inventory = Pressed(now, Keys.I),
                Status = Pressed(now, Keys.C),
                Screenshot = Pressed(now, Keys.F12),
                ToggleDisplay = Pressed(now, Keys.F11),
                LocationLog = Pressed(now, Keys.L),
                Help = Pressed(now, Keys.F1),
                Interact = Pressed(now, Keys.E) || Pressed(now, Keys.Space) || Pressed(now, Keys.LeftControl) || Pressed(now, Keys.RightControl),
                Space = Pressed(now, Keys.Space),
                Rotate = Pressed(now, Keys.R),
                AltLeft = Pressed(now, Keys.A),
                AltRight = Pressed(now, Keys.D),
                Shift = now.IsKeyDown(Keys.LeftShift) || now.IsKeyDown(Keys.RightShift),
                HeldUp = now.IsKeyDown(Keys.Up) || now.IsKeyDown(Keys.W),
                HeldDown = now.IsKeyDown(Keys.Down) || now.IsKeyDown(Keys.S),
                HeldLeft = now.IsKeyDown(Keys.Left) || now.IsKeyDown(Keys.A),
                HeldRight = now.IsKeyDown(Keys.Right) || now.IsKeyDown(Keys.D),
                FullView = Pressed(now, KeyGuideNames.FullViewKey),
                ZoomInHeld = AnyDown(now, KeyGuideNames.ZoomInKeys),
                ZoomOutHeld = AnyDown(now, KeyGuideNames.ZoomOutKeys),
            };
            for (int i = 0; i < KeyGuideNames.ScreenOpeners.Length; i++)
                if (Pressed(now, KeyGuideNames.ScreenOpeners[i].Device)) frame.Openers |= 1u << i;
            _previous = now;
            return frame;
        }

        private bool Pressed(KeyboardState now, Keys key) => now.IsKeyDown(key) && !_previous.IsKeyDown(key);

        private static bool AnyDown(KeyboardState now, Keys[] keys)
        {
            for (int i = 0; i < keys.Length; i++) if (now.IsKeyDown(keys[i])) return true;
            return false;
        }
    }

    /// <summary>
    /// 단축키 안내에 넘기는 장치 키 이름(Screens는 장치를 모른다). 위 KeyboardDevice.Read의 배정과 같아야 한다 — 배정을 바꾸거나
    /// 화면 쌓기로 넘기는 키를 더하면 여기에도 한 줄. 화면을 여는 키는 Host 테스트(KeyGuideHostTests)가 이 표대로 눌러 본다.
    /// 키 이름은 번역하지 않는 글자다. "방향 키"처럼 말로 된 것만 현지화 키.
    /// </summary>
    public static class KeyGuideNames
    {
        /// <summary>화면 쌓기가 받는 키 → 그 키를 내는 장치 키(어느 것이든). Read와 같은 배정.</summary>
        public static readonly (ScreenKey Key, Keys[] Device)[] ScreenKeys =
        {
            (ScreenKey.Up, new[] { Keys.Up }),
            (ScreenKey.Down, new[] { Keys.Down }),
            (ScreenKey.Left, new[] { Keys.Left }),
            (ScreenKey.Right, new[] { Keys.Right }),
            (ScreenKey.Tab, new[] { Keys.Tab }),
            (ScreenKey.Enter, new[] { Keys.Enter }),
            (ScreenKey.Escape, new[] { Keys.Escape }),
            (ScreenKey.Inventory, new[] { Keys.I }),
            (ScreenKey.Status, new[] { Keys.C }),
            (ScreenKey.Interact, new[] { Keys.E, Keys.Space, Keys.LeftControl }),
            (ScreenKey.Space, new[] { Keys.Space }),
            (ScreenKey.Rotate, new[] { Keys.R }),
            (ScreenKey.AltLeft, new[] { Keys.A }),
            (ScreenKey.AltRight, new[] { Keys.D }),
            (ScreenKey.Help, new[] { Keys.F1 }),
            (ScreenKey.LocationLog, new[] { Keys.L }),
            (ScreenKey.Encyclopedia, new[] { Keys.B }),
            (ScreenKey.Wardrobe, new[] { Keys.O }),
            (ScreenKey.WorldLog, new[] { Keys.T }),
            (ScreenKey.Careers, new[] { Keys.J }),
            (ScreenKey.Dream, new[] { Keys.H }),
            (ScreenKey.Placement, new[] { Keys.P }),
        };

        /// <summary>
        /// HUD 단추로만 열리던 화면의 키(2026-11-13 임시 배정 — 사용자 확인 전): B 도감, O 옷장, T 마을 기록, J 진로 후보, H 장래희망, P 가구 배치.
        /// 추천은 마을 기록 R, 가구 배치 F였지만 R은 미니게임 조각 돌리기, F는 가구 배치의 집 전체 보기라 T·P로 바꿨다(WASD는 걷기).
        /// Read가 이 표대로 KeyFrame.Openers를 채우고 HostGame이 같은 순서로 화면 쌓기에 넘긴다. 위 ScreenKeys에도 같은 줄(안내 이름).
        /// </summary>
        public static readonly (ScreenKey Key, Keys Device)[] ScreenOpeners =
        {
            (ScreenKey.Encyclopedia, Keys.B),
            (ScreenKey.Wardrobe, Keys.O),
            (ScreenKey.WorldLog, Keys.T),
            (ScreenKey.Careers, Keys.J),
            (ScreenKey.Dream, Keys.H),
            (ScreenKey.Placement, Keys.P),
        };

        /// <summary>걷기: 방향 키나 WASD를 누르고 있는 동안(Read의 Held*).</summary>
        public static readonly Keys[] WalkLetters = { Keys.W, Keys.A, Keys.S, Keys.D };
        public const Keys FullscreenKey = Keys.F11;
        public const Keys ScreenshotKey = Keys.F12;
        /// <summary>가구 배치 카메라(Read의 FullView·Zoom*Held가 이 값을 읽는다): F 전체 보기, = 또는 키패드 + 가까이, - 또는 키패드 - 멀리.</summary>
        public const Keys FullViewKey = Keys.F;
        public static readonly Keys[] ZoomInKeys = { Keys.OemPlus, Keys.Add };
        public static readonly Keys[] ZoomOutKeys = { Keys.OemMinus, Keys.Subtract };

        /// <summary>안내 이름표: 화면 쌓기 키의 장치 이름 + 화면이 아닌 키의 줄(걷기, 상호작용, 전체 화면, 화면 저장, 가구 배치 카메라).</summary>
        public static KeyGuideKeys Create()
        {
            var keys = new KeyGuideKeys { Shift = KeyCap.Name("Shift") };
            foreach ((ScreenKey key, Keys[] device) in ScreenKeys) keys.Name(key, Chords(device));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Movement, "ui.keys.move", new[] { keys.Directions, new KeyChord(KeyCap.Name(string.Concat(Names(WalkLetters)))) }));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Interact, "ui.keys.interact", keys.For(ScreenKey.Interact)));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.System, "ui.keys.fullscreen", new[] { KeyChord.Of(Name(FullscreenKey)) }));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.System, "ui.keys.screenshot", new[] { KeyChord.Of(Name(ScreenshotKey)) }));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Furniture, "ui.keys.furniture.full-view", new[] { KeyChord.Of(Name(FullViewKey)) }));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Furniture, "ui.keys.furniture.zoom-in", Chords(ZoomInKeys)));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Furniture, "ui.keys.furniture.zoom-out", Chords(ZoomOutKeys)));
            return keys;
        }

        private static KeyChord[] Chords(Keys[] device)
        {
            var chords = new KeyChord[device.Length];
            for (int i = 0; i < device.Length; i++) chords[i] = KeyChord.Of(Name(device[i]));
            return chords;
        }

        private static string[] Names(Keys[] device)
        {
            var names = new string[device.Length];
            for (int i = 0; i < device.Length; i++) names[i] = Name(device[i]);
            return names;
        }

        /// <summary>키 모양에 쓰는 이름(번역하지 않는다). 따로 적지 않은 키는 MonoGame 키 이름 그대로.</summary>
        public static string Name(Keys key)
        {
            switch (key)
            {
                case Keys.Escape: return "Esc";
                case Keys.LeftControl: case Keys.RightControl: return "Ctrl";
                case Keys.LeftShift: case Keys.RightShift: return "Shift";
                case Keys.Up: return "↑";
                case Keys.Down: return "↓";
                case Keys.Left: return "←";
                case Keys.Right: return "→";
                case Keys.OemPlus: return "=";
                case Keys.OemMinus: return "-";
                case Keys.Add: return "Num +";
                case Keys.Subtract: return "Num -";
                default: return key.ToString();
            }
        }
    }

    public enum TextEventKind { Text, Backspace, Composition }

    public readonly record struct TextEvent(TextEventKind Kind, string Text);

    /// <summary>
    /// 글자 입력을 읽는 유일한 곳. 확정된 글자와 백스페이스는 MonoGame의 창 글자 이벤트로,
    /// 조합 중인 글자는 SDL2 이벤트(Sdl2Native)로 받아 순서대로 쌓아 둔다. 입력기는 글자 칸이 선택된 동안에만 켠다.
    /// </summary>
    public sealed class TextInputDevice : IDisposable
    {
        private readonly GameWindow _window;
        private readonly List<TextEvent> _pending = new List<TextEvent>(16);
        private readonly List<TextEvent> _drained = new List<TextEvent>(16);

        public bool Active { get; private set; }

        public TextInputDevice(GameWindow window)
        {
            _window = window;
            _window.TextInput += OnTextInput;
            Sdl2Native.WatchTextEditing(text => { if (Active) _pending.Add(new TextEvent(TextEventKind.Composition, text)); });
            Sdl2Native.StopTextInput(); // SDL은 데스크톱에서 글자 입력을 켠 채 시작한다
        }

        private void OnTextInput(object sender, TextInputEventArgs e)
        {
            if (!Active) return;
            if (e.Character == '\b') _pending.Add(new TextEvent(TextEventKind.Backspace, null));
            else if (!char.IsControl(e.Character)) _pending.Add(new TextEvent(TextEventKind.Text, e.Character.ToString()));
        }

        /// <summary>글자 칸이 선택됐다. rect는 창 안 픽셀(입력기 후보 창 자리).</summary>
        public void Start(Rectangle rect)
        {
            Sdl2Native.SetTextInputRect(rect.X, rect.Y, rect.Width, rect.Height);
            if (!Active) Sdl2Native.StartTextInput();
            Active = true;
        }

        public void Stop()
        {
            if (Active) Sdl2Native.StopTextInput();
            Active = false;
            _pending.Clear();
        }

        /// <summary>지난번 뒤로 들어온 것을 순서대로 돌려준다. 돌려준 목록은 다음 호출 때 다시 쓰인다.</summary>
        public List<TextEvent> Drain()
        {
            _drained.Clear();
            _drained.AddRange(_pending);
            _pending.Clear();
            return _drained;
        }

        public void Dispose()
        {
            _window.TextInput -= OnTextInput;
            Sdl2Native.UnwatchTextEditing();
        }
    }
}
