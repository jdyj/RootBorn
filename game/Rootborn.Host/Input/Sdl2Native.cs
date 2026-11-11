using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Rootborn.Host.Input
{
    /// <summary>
    /// SDL2를 직접 부르는 유일한 곳. MonoGame(DesktopGL)이 공개하지 않는 기능만 쓴다:
    /// 조합 중 글자 이벤트 받기, 글자 입력 켜고 끄기, 창 최소 크기, 그리고 테스트용 이벤트 넣기.
    /// MonoGame이 함께 배포하는 SDL2 라이브러리를 그대로 쓴다. MonoGame 버전을 올릴 때 이 파일을 먼저 확인한다.
    /// 구조체 자리는 SDL 2.x의 SDL_TextEditingEvent, SDL_TextInputEvent, SDL_KeyboardEvent 기준이다.
    /// </summary>
    public static unsafe class Sdl2Native
    {
        private const string Lib = "SDL2";
        private const uint KeyDownEvent = 0x300, KeyUpEvent = 0x301, TextEditingEvent = 0x302, TextInputEvent = 0x303;
        private const int EventSize = 56, TextOffset = 12, TextCapacity = 32;

        static Sdl2Native()
        {
            NativeLibrary.SetDllImportResolver(typeof(Sdl2Native).Assembly, (name, assembly, path) =>
            {
                if (name != Lib) return IntPtr.Zero;
                foreach (string candidate in new[] { "SDL2.dll", "libSDL2-2.0.so.0", "libSDL2-2.0.0.dylib", "SDL2" })
                    if (NativeLibrary.TryLoad(candidate, typeof(Microsoft.Xna.Framework.Game).Assembly, null, out IntPtr handle)) return handle;
                return IntPtr.Zero;
            });
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int X, Y, W, H; }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EventFilter(IntPtr userdata, byte* evt);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_AddEventWatch(EventFilter filter, IntPtr userdata);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_DelEventWatch(EventFilter filter, IntPtr userdata);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_StartTextInput();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_StopTextInput();
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_SetTextInputRect(ref Rect rect);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_PushEvent(byte* evt);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern uint SDL_GetWindowID(IntPtr window);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_SetWindowMinimumSize(IntPtr window, int minW, int minH);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_GetWindowMinimumSize(IntPtr window, out int w, out int h);

        private static EventFilter _filter; // 가비지 수집되지 않게 잡아 둔다
        private static Action<string> _onEditing;

        /// <summary>조합 중인 글자가 바뀔 때마다 불린다. 조합이 끝나면 빈 글자로 불린다.</summary>
        public static void WatchTextEditing(Action<string> onEditing)
        {
            UnwatchTextEditing();
            _onEditing = onEditing;
            _filter = (userdata, evt) =>
            {
                if (*(uint*)evt == TextEditingEvent)
                {
                    int bytes = 0;
                    while (bytes < TextCapacity && evt[TextOffset + bytes] != 0) bytes++;
                    _onEditing?.Invoke(Encoding.UTF8.GetString(evt + TextOffset, bytes));
                }
                return 1;
            };
            SDL_AddEventWatch(_filter, IntPtr.Zero);
        }

        public static void UnwatchTextEditing()
        {
            if (_filter != null) SDL_DelEventWatch(_filter, IntPtr.Zero);
            _filter = null;
            _onEditing = null;
        }

        /// <summary>창을 이보다 작게 줄이지 못하게 한다(MonoGame이 창 최소 크기를 공개하지 않는다).</summary>
        public static void SetWindowMinimumSize(IntPtr window, int width, int height) => SDL_SetWindowMinimumSize(window, width, height);

        public static (int Width, int Height) GetWindowMinimumSize(IntPtr window)
        {
            SDL_GetWindowMinimumSize(window, out int w, out int h);
            return (w, h);
        }

        public static void StartTextInput() => SDL_StartTextInput();
        public static void StopTextInput() => SDL_StopTextInput();

        /// <summary>입력기 후보 창이 뜰 자리(창 안 픽셀).</summary>
        public static void SetTextInputRect(int x, int y, int w, int h)
        {
            var rect = new Rect { X = x, Y = y, W = w, H = h };
            SDL_SetTextInputRect(ref rect);
        }

        /// <summary>테스트용: 키 이벤트를 SDL 큐에 넣는다. state(12) repeat(13) scancode(16) sym(20) mod(24).</summary>
        public static void PushKey(IntPtr window, int scancode, int sym, bool down)
        {
            byte* evt = stackalloc byte[EventSize];
            new Span<byte>(evt, EventSize).Clear();
            *(uint*)evt = down ? KeyDownEvent : KeyUpEvent;
            *(uint*)(evt + 8) = SDL_GetWindowID(window);
            evt[12] = (byte)(down ? 1 : 0);
            *(int*)(evt + 16) = scancode;
            *(int*)(evt + 20) = sym;
            SDL_PushEvent(evt);
        }

        /// <summary>테스트용: 확정 글자 이벤트를 SDL 큐에 넣는다. 한 번에 UTF-8 31바이트까지.</summary>
        public static void PushText(IntPtr window, string text) => PushTextEvent(window, TextInputEvent, text);

        /// <summary>테스트용: 조합 중 글자 이벤트를 SDL 큐에 넣는다.</summary>
        public static void PushTextEditing(IntPtr window, string text) => PushTextEvent(window, TextEditingEvent, text);

        private static void PushTextEvent(IntPtr window, uint type, string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            if (utf8.Length >= TextCapacity) throw new ArgumentException("한 번에 UTF-8 31바이트를 넘길 수 없다: " + text);
            byte* evt = stackalloc byte[EventSize];
            new Span<byte>(evt, EventSize).Clear();
            *(uint*)evt = type;
            *(uint*)(evt + 8) = SDL_GetWindowID(window);
            for (int i = 0; i < utf8.Length; i++) evt[TextOffset + i] = utf8[i];
            SDL_PushEvent(evt);
        }
    }
}
