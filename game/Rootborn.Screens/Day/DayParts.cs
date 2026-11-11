using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.StudentLife;
using Rootborn.Ui;

namespace Rootborn.Screens.Day
{
    /// <summary>
    /// 학생 시간 문구(Unity TimeBudgetText). 문구는 현지화 키로만 받는다. t는 화면의 T(key)다(쓴 키가 화면 기록에 남도록).
    /// </summary>
    public static class StudentTimeText
    {
        public const string Loading = "life.tp.loading", Summary = "life.tp.summary", Free = "life.tp.free", Cost = "life.tp.cost", WeekdayPrefix = "life.weekday.";

        /// <summary>"N일째 · 요일 · 남은 TP r/b". 달력이 없으면 불러오는 중 문구.</summary>
        public static string Format(Func<string, string> t, StudentLifeProgress progress)
        {
            if (progress == null || progress.Calendar == null) return t(Loading);
            return string.Format(CultureInfo.InvariantCulture, t(Summary), progress.CurrentDay,
                t(WeekdayPrefix + progress.Calendar.GetWeekdayIndex(progress.CurrentDay)), progress.TimePointsRemaining, progress.DailyTimePointBudget);
        }

        /// <summary>활동 비용 한 줄: TP가 들지 않으면 "TP 소모 없음", 아니면 "n TP".</summary>
        public static string FormatCost(Func<string, string> t, ActivityTimePolicy policy) =>
            policy == null || policy.TimePointCost == 0 ? t(Free) : string.Format(CultureInfo.InvariantCulture, t(Cost), policy.TimePointCost);
    }

    /// <summary>
    /// 화면이 쓰는 그림 id. 화면은 아이콘 키만 말하고, 어느 PNG의 어느 칸인지는 Host(테마 데이터)가 정한다.
    /// For는 정의마다 테마가 정한 아이콘(테마의 presentations)을 먼저 찾고, 없으면 fallbackKey 아이콘을 쓴다(Unity UrbanUiIcon.ConfigureFor).
    /// </summary>
    public static class ScreenIcons
    {
        public static string Key(string iconKey) => "icon:" + iconKey;

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
