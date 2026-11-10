using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

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

    /// <summary>이번 프레임에 새로 눌린 키. Shift만 누르고 있는 상태다.</summary>
    public struct KeyFrame
    {
        public bool Up, Down, Left, Right, Tab, Enter, Escape, Screenshot, Shift;
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
                Screenshot = Pressed(now, Keys.F12),
                Shift = now.IsKeyDown(Keys.LeftShift) || now.IsKeyDown(Keys.RightShift),
            };
            _previous = now;
            return frame;
        }

        private bool Pressed(KeyboardState now, Keys key) => now.IsKeyDown(key) && !_previous.IsKeyDown(key);
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
