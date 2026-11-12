using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.StudentLife;
using Rootborn.Ui;

namespace Rootborn.Screens.Day
{
    /// <summary>
    /// 학생 시간 문구(Unity TimeBudgetText를 흐르는 시간에 맞춰 고침 — 시간 포인트는 보이지 않는다, 흐르는 시간 설계 4절).
    /// 문구는 현지화 키로만 받는다. t는 화면의 T(key)다(쓴 키가 화면 기록에 남도록).
    /// </summary>
    public static class StudentTimeText
    {
        public const string Loading = "life.tp.loading", Summary = "life.time.summary", Cost = "life.time.cost", Instant = "life.time.instant",
            CostEnds = "life.time.cost-ends", CostEndsLate = "life.time.cost-ends-late", WeekdayPrefix = "life.weekday.";

        /// <summary>"N일차 · 요일 · 14:05"(보이는 시각은 24시간으로 감는다). 달력이 없으면 불러오는 중 문구.</summary>
        public static string Format(Func<string, string> t, StudentLifeProgress progress)
        {
            if (progress == null || progress.Calendar == null) return t(Loading);
            int clock = ClockMinutes(progress.TimeMinutes);
            return string.Format(CultureInfo.InvariantCulture, t(Summary), progress.CurrentDay,
                t(WeekdayPrefix + progress.Calendar.GetWeekdayIndex(progress.CurrentDay)), clock / 60, clock % 60);
        }

        /// <summary>활동이 걸리는 시간 한 줄: 0분이면 "금방", 아니면 "n분".</summary>
        public static string FormatCost(Func<string, string> t, int minutes) =>
            minutes <= 0 ? t(Instant) : string.Format(CultureInfo.InvariantCulture, t(Cost), minutes);

        /// <summary>
        /// 지금 시작하면 끝나는 시각을 붙인 시간 줄: "n분 · 21:30까지". 끝나는 시각이 취침 시각을 넘기면(밤에는 체력이 준다) 다른 문구다.
        /// 0분이면 "금방", 시간 규칙이 없으면(시험 데이터) 걸리는 시간만. 판단에 쓰는 값은 GameSession 조회(MinutesUntilBedtime)다.
        /// </summary>
        public static string FormatCostAndEnd(Func<string, string> t, Rootborn.Game.GameSession session, int minutes)
        {
            string cost = FormatCost(t, minutes);
            if (minutes <= 0 || session == null || session.TimeRules == null) return cost;
            int end = ClockMinutes(session.Student.TimeMinutes + minutes);
            string key = minutes > session.MinutesUntilBedtime ? CostEndsLate : CostEnds;
            return string.Format(CultureInfo.InvariantCulture, t(key), cost, end / 60, end % 60);
        }

        /// <summary>그날 0시부터 센 분을 보이는 시각(0~1439)으로 감는다(26:00 → 02:00).</summary>
        public static int ClockMinutes(int dayMinutes) => ((dayMinutes % (24 * 60)) + 24 * 60) % (24 * 60);
    }

    /// <summary>
    /// 화면이 쓰는 그림 id. 화면은 아이콘 키만 말하고, 어느 PNG의 어느 칸인지는 Host(테마 데이터)가 정한다.
    /// For는 정의마다 테마가 정한 아이콘(테마의 presentations)을 먼저 찾고, 없으면 fallbackKey 아이콘을 쓴다(Unity UrbanUiIcon.ConfigureFor).
    /// </summary>
    public static class ScreenIcons
    {
        public static string Key(string iconKey) => "icon:" + iconKey;

        /// <summary>닫기 단추의 그림(테마의 close 아이콘). 글자 "×"를 화면마다 적지 않는다.</summary>
        public static readonly string Close = Key("close");

        public static string For(string definitionId, string fallbackKey) =>
            string.IsNullOrEmpty(definitionId) ? Key(fallbackKey) : Key(fallbackKey) + "@" + definitionId;
    }

    /// <summary>카드 목록 칸에 여러 줄 글자를 그리는 도움(줄바꿈은 TextWrap).</summary>
    internal static class CardText
    {
        [ThreadStatic] private static List<string> _lines;

        /// <summary>사각형 안에 문단들을 위에서부터 쌓아 그린다. 넘치면 잘린다(잘라 내기는 카드 목록이 건다). 그린 높이를 돌려준다.</summary>
        public static float Draw(DrawList list, ITextMeasurer text, UiRect rect, string value, float fontSize, UiColor color, bool center, float lineSpacing = 2f)
        {
            if (string.IsNullOrEmpty(value)) return 0f;
            List<string> lines = _lines ??= new List<string>(4);
            TextWrap.Wrap(value, rect.W, text, fontSize, lines);
            float lineHeight = text.LineHeight(fontSize), y = rect.Y;
            foreach (string line in lines)
            {
                if (y + lineHeight > rect.Bottom + 0.5f) break;
                float x = center ? rect.X + (rect.W - text.Width(line, fontSize)) / 2f : rect.X;
                list.Text(x, y, line, fontSize, color);
                y += lineHeight + lineSpacing;
            }
            return y - rect.Y;
        }
    }

    internal static class ScreenLayoutExtensions
    {
        /// <summary>
        /// 화면 안의 배치를 바꾼 뒤 그 화면만 다시 배치한다. ScreenStack은 화면을 연 뒤에야 OnOpened를 부르므로,
        /// 그 안에서 줄 수에 따라 자리를 바꾸면 이것으로 바로 맞춘다.
        /// </summary>
        public static void Relayout(this UiNode root)
        {
            if (root?.Parent != null) root.Layout(root.Parent.Rect);
        }
    }
}
