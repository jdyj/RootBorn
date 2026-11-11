using System;

namespace Rootborn.Screens.Settings
{
    /// <summary>
    /// 표시 모드(창 / 테두리 없는 전체 화면). 화면은 MonoGame을 모르므로 언어(GameText.Language → LanguageChanged)와 같은 길로 잇는다:
    /// 설정 화면이나 F11이 값을 바꾸면 설정 파일에 저장하고 Changed로 알린다. Host가 Changed를 받아 창에 바로 적용하고,
    /// 화면 쌓기는 열린 화면에 ChangeTopics.Display를 넘긴다. 처음 값은 Host가 실제로 시작한 모드다(실행 인자·테스트 모드가 저장값보다 먼저).
    /// </summary>
    public sealed class DisplayModeSetting
    {
        /// <summary>설정 파일(Core GameSettings 켬/끔 목록)에 적는 이름. 켬 = 테두리 없는 전체 화면.</summary>
        public const string SettingName = "Fullscreen";

        private bool _fullscreen;

        public DisplayModeSetting(SettingsStore store, bool fullscreen)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            _fullscreen = fullscreen;
        }

        /// <summary>고른 모드를 저장하는 설정.</summary>
        public SettingsStore Store { get; }

        /// <summary>지금 모드. 바꾸면 저장하고 알린다(같은 값이면 아무것도 하지 않는다).</summary>
        public bool Fullscreen
        {
            get => _fullscreen;
            set
            {
                if (_fullscreen == value) return;
                _fullscreen = value;
                Store.Settings.SetToggle(SettingName, value);
                Store.Save();
                Changed?.Invoke(value);
            }
        }

        /// <summary>모드가 바뀌었다(새 값: 전체 화면인가).</summary>
        public event Action<bool> Changed;
    }
}
