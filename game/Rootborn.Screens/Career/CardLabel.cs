using System;
using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Career
{
    /// <summary>
    /// 카드·목록 버튼에 "이름\n상태" 같은 여러 줄 글자를 가운데 정렬로 그린다(폭에 맞춰 줄바꿈, 영역 밖은 자른다).
    /// 보이는 카드마다 불리므로 줄 목록을 재사용한다(할당 없음).
    /// </summary>
    internal sealed class CardLabel
    {
        private readonly List<string> _lines = new List<string>(6);

        public void Draw(DrawList list, ITextMeasurer text, UiRect area, string label, float fontSize, UiColor color, float lineSpacing = 2f, bool centerVertically = false)
        {
            TextWrap.Wrap(label, area.W, text, fontSize, _lines);
            float lineHeight = text.LineHeight(fontSize);
            float total = _lines.Count * lineHeight + Math.Max(0, _lines.Count - 1) * lineSpacing;
            float y = centerVertically ? area.Y + (area.H - total) / 2f : area.Y;
            list.PushClip(area);
            foreach (string line in _lines)
            {
                if (line.Length > 0) list.Text(area.X + (area.W - text.Width(line, fontSize)) / 2f, y, line, fontSize, color);
                y += lineHeight + lineSpacing;
            }
            list.PopClip();
        }
    }
}
