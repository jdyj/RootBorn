using UnityEngine;
namespace Rootborn.UI.StudentLife
{
    internal static class LifeUiFont
    {
        private static Font _font;
        public static Font Shared
        {
            get
            {
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 20);
                return _font;
            }
        }
    }
}
