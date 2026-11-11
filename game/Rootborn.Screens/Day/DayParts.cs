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

    /// <summary>
    /// 세로로 스크롤하는 글자 묶음(Unity의 ScrollRect + VerticalLayoutGroup + 줄바꿈 Text 구역들). 구역마다 줄바꿈해 쌓고,
    /// 보이는 줄만 그린다. 휠·끌기로 스크롤한다. 줄바꿈 결과는 글자나 폭이 바뀔 때만 다시 계산한다.
    /// Rootborn.Ui에 없는 부품이라 이 화면 폴더에 둔다(보고 대상).
    /// </summary>
    public sealed class ScrollingTextView : UiNode
    {
        public float FontSize = 20;
        public float LineSpacing = 4;
        public float SectionSpacing = 22;
        public float PaddingLeft = 14, PaddingTop = 10, PaddingRight = 14, PaddingBottom = 20;
        public float WheelStep = 48;
        public float DragThreshold = 8;
        public UiColor Color = UiTheme.Ink;

        private readonly List<string> _sections = new List<string>(16);
        private readonly List<List<string>> _wrapped = new List<List<string>>(16);
        private float _wrappedWidth = -1, _wrappedSize;
        private bool _dirty = true;
        private float _scroll, _pressScroll, _contentHeight;
        private bool _dragging;

        public const float ScrollbarWidth = 18;
        private static readonly UiColor Track = new UiColor(64, 46, 31, 38);
        private static readonly UiColor Thumb = new UiColor(117, 87, 54);

        public ScrollingTextView() { Interactive = true; }

        public IReadOnlyList<string> Sections => _sections;

        public void SetSections(IEnumerable<string> sections)
        {
            _sections.Clear();
            foreach (string s in sections) if (!string.IsNullOrEmpty(s)) _sections.Add(s);
            _dirty = true;
        }

        public float ScrollOffset { get => _scroll; set => _scroll = MathF.Max(0, MathF.Min(value, MaxScroll)); }
        public float MaxScroll => MathF.Max(0, _contentHeight - TextArea.H);
        public float ContentHeight => _contentHeight;

        private UiRect TextArea => new UiRect(Rect.X + PaddingLeft, Rect.Y + PaddingTop, MathF.Max(1, Rect.W - PaddingLeft - PaddingRight - ScrollbarWidth), MathF.Max(1, Rect.H - PaddingTop - PaddingBottom));

        /// <summary>줄바꿈한 줄 전부(테스트와 결함 확인용). 측정기가 필요하다.</summary>
        public IReadOnlyList<string> Lines(ITextMeasurer text)
        {
            Ensure(text);
            var all = new List<string>();
            foreach (List<string> s in _wrapped) all.AddRange(s);
            return all;
        }

        private void Ensure(ITextMeasurer text)
        {
            UiRect area = TextArea;
            if (!_dirty && _wrappedWidth == area.W && _wrappedSize == FontSize) return;
            while (_wrapped.Count < _sections.Count) _wrapped.Add(new List<string>(8));
            float lineHeight = text.LineHeight(FontSize), height = 0;
            for (int i = 0; i < _sections.Count; i++)
            {
                TextWrap.Wrap(_sections[i], area.W, text, FontSize, _wrapped[i]);
                int n = _wrapped[i].Count;
                if (i > 0) height += SectionSpacing;
                height += n * lineHeight + Math.Max(0, n - 1) * LineSpacing;
            }
            for (int i = _sections.Count; i < _wrapped.Count; i++) _wrapped[i].Clear();
            _contentHeight = height;
            _wrappedWidth = area.W; _wrappedSize = FontSize; _dirty = false;
            ScrollOffset = _scroll;
        }

        public override bool OnWheel(float notches)
        {
            ScrollOffset = _scroll - notches * WheelStep;
            return true;
        }

        public override void OnPress(float x, float y) { _pressScroll = _scroll; _dragging = false; }

        public override bool OnDrag(float x, float y, float dx, float dy)
        {
            if (!_dragging && MathF.Abs(dy) > DragThreshold) _dragging = true;
            if (_dragging) ScrollOffset = _pressScroll - dy;
            return _dragging;
        }

        public override void OnDragEnd() => _dragging = false;

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            Ensure(text);
            UiRect area = TextArea;
            float lineHeight = text.LineHeight(FontSize);
            list.PushClip(new UiRect(Rect.X, Rect.Y, Rect.W - ScrollbarWidth, Rect.H));
            float y = area.Y - _scroll;
            for (int s = 0; s < _sections.Count; s++)
            {
                if (s > 0) y += SectionSpacing;
                List<string> lines = _wrapped[s];
                for (int i = 0; i < lines.Count; i++)
                {
                    if (y + lineHeight >= Rect.Y && y <= Rect.Bottom) list.Text(area.X, y, lines[i], FontSize, Color);
                    y += lineHeight + (i < lines.Count - 1 ? LineSpacing : 0);
                }
            }
            list.PopClip();
            var track = new UiRect(Rect.Right - ScrollbarWidth, Rect.Y, ScrollbarWidth, Rect.H);
            list.Fill(track, Track);
            float max = MaxScroll;
            float thumbH = max <= 0 ? track.H : MathF.Max(24, track.H * area.H / MathF.Max(area.H, _contentHeight));
            float thumbY = max <= 0 ? track.Y : track.Y + (track.H - thumbH) * (_scroll / max);
            list.Fill(new UiRect(track.X, thumbY, track.W, thumbH), Thumb);
        }
    }

    /// <summary>
    /// 사각형 가운데에 놓는 여러 줄 글자(Unity Text의 MiddleCenter·MiddleLeft + 줄바꿈). 버튼·카드 안 글자에 쓴다. 포인터를 받지 않는다.
    /// Rootborn.Ui에 없는 부품이라 이 화면 폴더에 둔다(보고 대상).
    /// </summary>
    public sealed class CenteredText : UiNode
    {
        public string Text;
        public float FontSize = 20;
        public float LineSpacing = 2;
        public UiColor Color = UiTheme.Ink;
        public UiAlign Align = UiAlign.Center;
        /// <summary>글자가 놓이는 바탕 스킨(버튼 위면 UiSkin.Button). 스킨이 정한 글자색을 쓰게 Host에 넘긴다.</summary>
        public string OnSkin;

        private readonly List<string> _lines = new List<string>(4);

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            if (string.IsNullOrEmpty(Text)) return;
            TextWrap.Wrap(Text, Rect.W, text, FontSize, _lines);
            float lineHeight = text.LineHeight(FontSize);
            float total = _lines.Count * lineHeight + Math.Max(0, _lines.Count - 1) * LineSpacing;
            float y = Rect.Y + (Rect.H - total) / 2f;
            foreach (string line in _lines)
            {
                float w = text.Width(line, FontSize);
                float x = Align == UiAlign.Left ? Rect.X : Align == UiAlign.Right ? Rect.Right - w : Rect.X + (Rect.W - w) / 2f;
                list.Text(x, y, line, FontSize, Color, OnSkin);
                y += lineHeight + LineSpacing;
            }
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
