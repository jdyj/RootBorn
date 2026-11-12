using System.Globalization;
using System.Text;

namespace Rootborn.Net.Messages
{
    /// <summary>
    /// 방에서 보이는 사람 이름(캐릭터 만들기에서 정한 이름). 화면 표시용일 뿐이다 — 신원(세이브 폴더, 방장 판정)은 PlayerId가 정한다.
    /// 보내기 전과 받은 뒤에 <see cref="Clean"/>으로 다듬는다: 줄바꿈·제어 문자·보이지 않는 서식 문자를 빼고, 앞뒤 공백을 지우고,
    /// 보이는 글자 <see cref="MaxTextElements"/>개와 <see cref="MaxBytes"/>바이트 안으로 자른다(글자 중간에서 자르지 않는다).
    /// 빈 이름은 "이름 없음"이다 — 화면이 현지화된 기본 말을 쓴다.
    /// </summary>
    public static class PlayerName
    {
        /// <summary>캐릭터 만들기의 이름 한도(Screens CharacterCreatorModel.MaxNameLength)와 같다. Host 테스트가 둘을 맞춰 본다.</summary>
        public const int MaxTextElements = 12;

        /// <summary>메시지에 싣는 이름의 최대 UTF-8 바이트(그림 글자처럼 한 글자가 긴 것도 12글자 안팎은 들어간다).</summary>
        public const int MaxBytes = 96;

        private const char Joiner = (char)0x200D; // 너비 없는 잇기(ZWJ)

        public static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var kept = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                switch (char.GetUnicodeCategory(c))
                {
                    case UnicodeCategory.Control:
                    case UnicodeCategory.Format when c != Joiner: // 글자 잇기(그림 글자 묶음)만 남긴다
                    case UnicodeCategory.LineSeparator:
                    case UnicodeCategory.ParagraphSeparator:
                        continue;
                }

                kept.Append(c);
            }

            string trimmed = kept.ToString().Trim();
            if (trimmed.Length == 0) return string.Empty;
            var info = new StringInfo(trimmed);
            int count = info.LengthInTextElements > MaxTextElements ? MaxTextElements : info.LengthInTextElements;
            string cut = count == info.LengthInTextElements ? trimmed : info.SubstringByTextElements(0, count);
            while (count > 0 && Encoding.UTF8.GetByteCount(cut) > MaxBytes)
            {
                count--;
                cut = count == 0 ? string.Empty : info.SubstringByTextElements(0, count);
            }

            return cut.Trim();
        }
    }
}
