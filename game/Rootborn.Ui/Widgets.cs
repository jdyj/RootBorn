using System;
using System.Text;

namespace Rootborn.Ui
{
    public enum UiAlign { Left, Center, Right }

    /// <summary>9조각 패널 배경. 포인터를 받아 뒤를 가린다.</summary>
    public sealed class UiPanel : UiNode
    {
        public string Skin = UiSkin.Panel;
        public UiColor Tint = UiTheme.White;

        public UiPanel() { Interactive = true; }

        public override bool IsSurface => true;

        public override void Emit(DrawList list, ITextMeasurer text) => list.Panel(Rect, Skin, Tint);
    }

    /// <summary>색으로 칠한 사각형. 포인터를 받지 않는다(장식, 구분선, 바탕).</summary>
    public sealed class UiBox : UiNode
    {
        public UiColor Color = UiTheme.White;

        public override bool IsSurface => true;

        public override void Emit(DrawList list, ITextMeasurer text) => list.Fill(Rect, Color);
    }

    /// <summary>한 줄 글자. 사각형 안에서 세로 가운데, 가로는 Align.</summary>
    public sealed class UiLabel : UiNode
    {
        public string Text;
        public float FontSize = 20;
        public UiColor Color = UiTheme.Ink;
        public UiAlign Align = UiAlign.Left;
        /// <summary>칸보다 길면 뒤를 말줄임(…)으로 줄인다. 기본은 켬.</summary>
        public bool Ellipsis = true;

        private FittedText _fitted;

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            if (string.IsNullOrEmpty(Text)) return;
            string shown = Ellipsis ? _fitted.Get(Text, Rect.W, FontSize, text) : Text;
            if (shown.Length == 0) return;
            float x = Rect.X;
            if (Align != UiAlign.Left)
            {
                float width = text.Width(shown, FontSize);
                x = Align == UiAlign.Center ? Rect.X + (Rect.W - width) / 2f : Rect.Right - width;
            }
            list.Text(x, Rect.Y + (Rect.H - text.LineHeight(FontSize)) / 2f, shown, FontSize, Color);
        }
    }

    public sealed class UiButton : UiNode
    {
        public string Text;
        public float FontSize = 20;
        public string Skin = UiSkin.Button;
        /// <summary>칸보다 길면 뒤를 말줄임(…)으로 줄인다. 기본은 켬.</summary>
        public bool Ellipsis = true;
        /// <summary>글자 대신(또는 글자 없이) 가운데에 그리는 그림 id(예: "icon:close"). 크기는 단추의 짧은 변 × IconRatio.</summary>
        public string Icon;
        public float IconRatio = 0.6f;

        private FittedText _fitted;

        public UiButton() { Interactive = true; Focusable = true; }

        public override bool IsSurface => true;

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            bool active = IsActive;
            UiColor tint = !active ? UiTheme.ButtonDisabled : Pressed ? UiTheme.ButtonPressed : Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal;
            list.Panel(Rect, Skin, tint);
            if (!string.IsNullOrEmpty(Icon))
            {
                float size = MathF.Min(Rect.W, Rect.H) * IconRatio;
                float offset = Pressed ? UiTheme.PressedOffset : 0f;
                list.Image(new UiRect(Rect.CenterX - size / 2f, Rect.CenterY - size / 2f + offset, size, size), Icon, default,
                    active ? UiTheme.White : UiTheme.ButtonDisabled);
            }
            string shown = string.IsNullOrEmpty(Text) ? string.Empty : Ellipsis ? _fitted.Get(Text, Rect.W, FontSize, text) : Text;
            if (shown.Length > 0)
            {
                float x = Rect.X + (Rect.W - text.Width(shown, FontSize)) / 2f;
                float y = Rect.Y + (Rect.H - text.LineHeight(FontSize)) / 2f + (Pressed ? UiTheme.PressedOffset : 0f);
                list.Text(x, y, shown, FontSize, active ? UiTheme.Ink : UiTheme.InkDisabled, Skin);
            }
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }

    /// <summary>가방 칸 하나. 내용물(아이콘, 수량)은 화면을 옮길 때 붙인다.</summary>
    public sealed class UiSlot : UiNode
    {
        public string Skin = UiSkin.Slot;
        public bool Selected;

        /// <summary>고른 칸 표시는 테두리 그림(논리 좌표로 15~18) 안쪽에 그린다.</summary>
        private const float SelectedInset = 21;

        public UiSlot() { Interactive = true; Focusable = true; }

        public override bool IsSurface => true;

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            UiColor tint = !IsActive ? UiTheme.ButtonDisabled : Pressed ? UiTheme.ButtonPressed : Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal;
            list.Panel(Rect, Skin, tint);
            if (Selected) list.Outline(Rect.Inset(SelectedInset), UiTheme.FocusOutline, UiTheme.InkOnDark);
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }

    /// <summary>
    /// 한 줄 글자 입력 칸. 글자는 끝에만 붙고 끝에서만 지워진다(이름 입력용).
    /// 조합 중인 글자는 Composition에 따로 두고 다른 색과 밑줄로 보여 준다.
    /// </summary>
    public sealed class UiTextField : UiNode
    {
        private string _text = "";

        /// <summary>확정된 글자(조합 중인 글자는 Composition). 바뀌면 TextChanged.</summary>
        public string Text
        {
            get => _text;
            private set
            {
                value ??= "";
                if (string.Equals(value, _text, StringComparison.Ordinal)) return;
                _text = value;
                TextChanged?.Invoke(_text);
            }
        }

        public string Composition { get; private set; } = "";
        public string Placeholder;
        public int MaxLength = 12;
        public float FontSize = 24;
        public float PaddingX = 20;
        public string Skin = UiSkin.Field;

        /// <summary>포커스된 채 실행 키(Enter)를 눌렀다.</summary>
        public event Action<string> Submitted;

        /// <summary>확정된 글자가 바뀌었다(입력, 지우기, SetText). 조합 중인 글자와 같은 글자로 바꾸기는 알리지 않는다.</summary>
        public event Action<string> TextChanged;

        public UiTextField() { Interactive = true; Focusable = true; }

        public override bool IsSurface => true;

        public void SetText(string text) { Composition = ""; Text = text ?? ""; }

        /// <summary>입력기가 글자를 조합하고 있다. 그동안 방향 키·Enter는 입력기의 것이다(조합을 깨지 않게 칸에서 나가거나 보내지 않는다).</summary>
        public bool Composing => Composition.Length > 0;

        public override void OnClick(float x, float y) { }
        public override void OnSubmit() { if (!Composing) Submitted?.Invoke(Text); }
        /// <summary>왼쪽·오른쪽은 칸 안의 것(글자는 끝에만 붙는다), 위·아래는 칸을 나간다. 조합 중에는 모두 칸 안에 둔다.</summary>
        public override bool OnNavigate(UiNav nav) => Composing || nav == UiNav.Left || nav == UiNav.Right;
        public override void OnFocusChanged(bool focused) { if (!focused) Composition = ""; }

        public override void OnTextInput(string text)
        {
            Composition = "";
            if (string.IsNullOrEmpty(text)) return;
            var result = new StringBuilder(Text);
            int length = CountCharacters(Text);
            for (int i = 0; i < text.Length && length < MaxLength; i++)
            {
                char c = text[i];
                if (char.IsControl(c)) continue;
                result.Append(c);
                if (char.IsHighSurrogate(c) && i + 1 < text.Length) result.Append(text[++i]);
                length++;
            }
            Text = result.ToString();
        }

        public override void OnTextBackspace()
        {
            if (Composition.Length > 0 || Text.Length == 0) return; // 조합 중에는 입력기가 지운다
            int remove = Text.Length >= 2 && char.IsLowSurrogate(Text[Text.Length - 1]) && char.IsHighSurrogate(Text[Text.Length - 2]) ? 2 : 1;
            Text = Text.Substring(0, Text.Length - remove);
        }

        public override void OnTextComposition(string text) => Composition = text ?? "";

        private static int CountCharacters(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
                if (!char.IsLowSurrogate(text[i])) count++;
            return count;
        }

        // 안내 문구를 칸 폭에 맞춘 것(말줄임). 문구·폭·크기가 같으면 다시 재지 않는다(매 프레임 새 글자를 만들지 않게).
        private string _fittedSource, _fitted;
        private float _fittedWidth, _fittedSize;
        private ITextMeasurer _fittedMeasurer;

        private string FittedPlaceholder(float width, ITextMeasurer text)
        {
            if (!ReferenceEquals(_fittedSource, Placeholder) || _fittedWidth != width || _fittedSize != FontSize || !ReferenceEquals(_fittedMeasurer, text))
            {
                _fittedSource = Placeholder; _fittedWidth = width; _fittedSize = FontSize; _fittedMeasurer = text;
                _fitted = TextFit.Ellipsize(Placeholder, width, text, FontSize);
            }
            return _fitted;
        }

        private string _tailSource, _tail;
        private float _tailWidth, _tailSize;
        private ITextMeasurer _tailMeasurer;

        private string FittedText(float width, ITextMeasurer text)
        {
            if (!ReferenceEquals(_tailSource, Text) || _tailWidth != width || _tailSize != FontSize || !ReferenceEquals(_tailMeasurer, text))
            {
                _tailSource = Text; _tailWidth = width; _tailSize = FontSize; _tailMeasurer = text;
                _tail = TextFit.Tail(Text, width, text, FontSize);
            }
            return _tail;
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            bool active = IsActive;
            list.Panel(Rect, Skin, !active ? UiTheme.ButtonDisabled : Focused ? UiTheme.ButtonHovered : UiTheme.ButtonNormal);
            float lineHeight = text.LineHeight(FontSize);
            float x = Rect.X + PaddingX, y = Rect.Y + (Rect.H - lineHeight) / 2f;
            if (Text.Length == 0 && Composition.Length == 0)
            {
                if (!Focused) list.Text(x, y, FittedPlaceholder(Rect.W - PaddingX * 2f, text), FontSize, UiTheme.InkMuted);
            }
            else
            {
                // 칸보다 길면 끝 쪽만(조합 중인 글자와 입력 위치 자리를 남기고). 같은 글자·폭이면 다시 재지 않는다.
                float room = Rect.W - PaddingX * 2f - 4f - (Composition.Length > 0 ? text.Width(Composition, FontSize) : 0f);
                string shown = FittedText(room, text);
                list.Text(x, y, shown, FontSize, active ? UiTheme.Ink : UiTheme.InkDisabled);
                x += text.Width(shown, FontSize);
                if (Composition.Length > 0)
                {
                    float width = text.Width(Composition, FontSize);
                    list.Text(x, y, Composition, FontSize, UiTheme.Accent);
                    list.Fill(new UiRect(x, y + lineHeight + 2, width, 2), UiTheme.Accent);
                    x += width;
                }
            }
            if (Focused)
            {
                list.Fill(new UiRect(x + 2, y, 2, lineHeight), UiTheme.Ink); // 입력 위치
                list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
            }
        }
    }
}
