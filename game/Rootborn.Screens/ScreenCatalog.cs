using Rootborn.Screens.Settings;

namespace Rootborn.Screens
{
    /// <summary>
    /// 화면 등록 표. 새 화면은 여기에 한 줄을 더한다: 화면 id, 층, 이동을 막는지(Unity 판과 같은 값, 설계 3.1절), 만드는 법, 여는 키·상호작용 종류.
    /// 키 배정(Unity ModernUiPanelInputRouter): I 가방, C 상태, Esc 설정, Tab 목표 수첩. 상호작용은 InteractionEvent.Kind(엔티티 id가 아니다).
    /// </summary>
    public static class ScreenCatalog
    {
        public const string Settings = "settings";

        public static void Register(ScreenRegistry r)
        {
            r.Add(Settings, ScreenLayer.Panel, blocksMovement: false, s => new SettingsScreen(s)).OpenedBy(ScreenKey.Escape);
        }

        /// <summary>표를 채운 새 등록 표.</summary>
        public static ScreenRegistry Create()
        {
            var registry = new ScreenRegistry();
            Register(registry);
            return registry;
        }
    }
}
