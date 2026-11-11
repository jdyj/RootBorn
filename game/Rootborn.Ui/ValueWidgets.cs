using System;
using System.Collections.Generic;

namespace Rootborn.Ui
{
    /// <summary>
    /// 탭 줄. 탭은 같은 폭으로 나란히 놓인다. 탭 줄 전체가 포커스 하나이고, 포커스된 채 왼쪽·오른쪽 키로 탭을 바꾼다.
    /// 고른 탭은 RaiseSelected만큼 위로 튀어나오고 밝게 그린다(ui-standards: 선택은 위치 또는 색으로 표시).
    /// </summary>
    public sealed class UiTabBar : UiNode
    {
        public float FontSize = 20;
        public float RaiseSelected = 6;
        public string Skin = UiSkin.Button;

        private readonly List<string> _labels = new List<string>(8);
        private int _selected;

        /// <summary>사용자가 다른 탭을 골랐다(포인터나 키). 코드로 Selected를 바꾸면 나지 않는다.</summary>
        public event Action<int> SelectionChanged;

        public UiTabBar() { Interactive = true; Focusable = true; }

        public int Count => _labels.Count;

        public void SetTabs(IReadOnlyList<string> labels)
        {
            _labels.Clear();
            if (labels != null) _labels.AddRange(labels);
            if (_selected >= _labels.Count) _selected = Math.Max(0, _labels.Count - 1);
        }

        /// <summary>탭 글자를 바꾼다(언어가 바뀌었을 때). 개수는 그대로다.</summary>
        public void SetLabel(int index, string label) { if (index >= 0 && index < _labels.Count) _labels[index] = label; }

        public int Selected
        {
            get => _selected;
            set => _selected = _labels.Count == 0 ? 0 : Math.Max(0, Math.Min(value, _labels.Count - 1));
        }

        public UiRect TabRect(int index)
        {
            float w = _labels.Count == 0 ? Rect.W : Rect.W / _labels.Count;
            float raise = index == _selected ? RaiseSelected : 0f;
            return new UiRect(Rect.X + w * index, Rect.Y + RaiseSelected - raise, w, Rect.H - RaiseSelected + raise);
        }

        public int TabAt(float x)
        {
            if (_labels.Count == 0 || x < Rect.X || x >= Rect.Right) return -1;
            return Math.Min(_labels.Count - 1, (int)((x - Rect.X) / (Rect.W / _labels.Count)));
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= _labels.Count || index == _selected) return;
            _selected = index;
            SelectionChanged?.Invoke(index);
        }

        public override void OnClick(float x, float y) => Choose(TabAt(x));
        public override void OnSubmit() { }

        public override bool OnNavigate(UiNav nav)
        {
            if (nav == UiNav.Left && _selected > 0) { Choose(_selected - 1); return true; }
            if (nav == UiNav.Right && _selected < _labels.Count - 1) { Choose(_selected + 1); return true; }
            return false;
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            bool active = IsActive;
            for (int i = 0; i < _labels.Count; i++)
            {
                UiRect r = TabRect(i);
                bool selected = i == _selected;
                list.Panel(r, Skin, !active ? UiTheme.ButtonDisabled : selected ? UiTheme.ButtonHovered : UiTheme.ButtonPressed);
                string label = _labels[i];
                if (!string.IsNullOrEmpty(label))
                    list.Text(r.X + (r.W - text.Width(label, FontSize)) / 2f, r.Y + (r.H - text.LineHeight(FontSize)) / 2f, label, FontSize,
                        selected ? UiTheme.Ink : UiTheme.InkMuted, Skin);
            }
            if (Focused) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }

    /// <summary>게이지(진행 막대). 포인터와 포커스를 받지 않는다. 값은 0~Max로 잘라 채운다.</summary>
    public sealed class UiGauge : UiNode
    {
        public float Value, Max = 1;
        public float Padding = 3;
        public UiColor Back = new UiColor(60, 60, 100, 60);
        public UiColor Fill = UiTheme.Accent;

        public float Fraction => Max <= 0 ? 0 : Math.Max(0, Math.Min(1, Value / Max));

        public UiRect FillRect
        {
            get
            {
                UiRect inner = Rect.Inset(Padding);
                return new UiRect(inner.X, inner.Y, inner.W * Fraction, inner.H);
            }
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            list.Fill(Rect, Back);
            UiRect fill = FillRect;
            if (fill.W > 0) list.Fill(fill, Fill);
        }
    }

    /// <summary>
    /// 켬/끔. 누르거나 포커스된 채 실행 키를 누르면 바뀐다. 모양은 Unity SettingsPanel의 켬/끔(바탕 78x36, 손잡이 36,
    /// 켬이면 손잡이가 왼쪽 -18, 끔이면 오른쪽 +18)을 크기에 맞춰 그린다.
    /// </summary>
    public sealed class UiToggle : UiNode
    {
        public bool IsOn;
        public UiColor Track = new UiColor(112, 84, 77);   // PanelShadow (0.44, 0.33, 0.30)
        public UiColor OnFill = new UiColor(94, 94, 140);  // AccentBlue (0.37, 0.37, 0.55)
        public UiColor OffFill = new UiColor(150, 115, 94); // FrameMid (0.59, 0.45, 0.37)
        public UiColor Knob = new UiColor(214, 145, 71);   // AccentGold (0.84, 0.57, 0.28)
        public UiColor KnobLight = new UiColor(222, 194, 158); // PanelLight (0.87, 0.76, 0.62)

        /// <summary>사용자가 바꿨다. 코드로 IsOn을 바꾸면 나지 않는다.</summary>
        public event Action<bool> Toggled;

        public UiToggle() { Interactive = true; Focusable = true; }

        /// <summary>손잡이: 높이만큼의 정사각형. 켬이면 가운데에서 왼쪽으로 (폭/2 - 높이/2), 끔이면 오른쪽으로.</summary>
        public UiRect KnobRect
        {
            get
            {
                float size = Rect.H, shift = Rect.W / 2f - size / 2f - 3f * Rect.W / 78f;
                float cx = Rect.CenterX + (IsOn ? -shift : shift);
                return new UiRect(cx - size / 2f, Rect.Y, size, size);
            }
        }

        public override void OnClick(float x, float y)
        {
            IsOn = !IsOn;
            Toggled?.Invoke(IsOn);
            base.OnClick(x, y);
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            list.Fill(Rect, IsActive ? Track : UiTheme.ButtonDisabled);
            float fx = Rect.W * 10f / 78f, fy = Rect.H * 7f / 36f; // 바탕 안쪽 칠: 58x22
            list.Fill(new UiRect(Rect.X + fx, Rect.Y + fy, Rect.W - fx * 2, Rect.H - fy * 2), IsOn ? OnFill : OffFill);
            UiRect knob = KnobRect;
            list.Fill(knob, Hovered ? KnobLight : Knob);
            float lw = knob.W * 22f / 36f, lh = knob.H * 14f / 36f; // 손잡이 위 밝은 칸 22x14, 가운데보다 5 위
            list.Fill(new UiRect(knob.CenterX - lw / 2f, knob.CenterY - lh / 2f - knob.H * 5f / 36f, lw, lh), KnobLight);
            if (Focused) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }

    /// <summary>
    /// 미끄럼 막대. 누른 곳으로 손잡이가 가고 끄는 동안 따라간다. 포커스된 채 왼쪽·오른쪽 키로 Step씩 움직인다.
    /// 값은 Min~Max, Step이 0보다 크면 Step 단위로 맞춘다.
    /// </summary>
    public sealed class UiSlider : UiNode
    {
        public float Min, Max = 1, Step;
        public float KnobWidth = 24;
        public UiColor Track = new UiColor(112, 84, 77);
        public UiColor Fill = new UiColor(94, 94, 140);
        public UiColor Knob = new UiColor(214, 145, 71);

        private float _value;

        /// <summary>사용자가 바꿨다. 코드로 Value를 바꾸면 나지 않는다.</summary>
        public event Action<float> ValueChanged;

        public UiSlider() { Interactive = true; Focusable = true; }

        public float Value
        {
            get => _value;
            set => _value = Snap(value);
        }

        private float Snap(float v)
        {
            if (Step > 0) v = Min + MathF.Round((v - Min) / Step) * Step;
            return Math.Max(Min, Math.Min(Max, v));
        }

        private float Travel => Math.Max(1f, Rect.W - KnobWidth);
        private float Fraction => Max > Min ? (_value - Min) / (Max - Min) : 0f;

        public UiRect KnobRect => new UiRect(Rect.X + Travel * Fraction, Rect.Y, KnobWidth, Rect.H);

        private void Set(float v)
        {
            float snapped = Snap(v);
            if (snapped == _value) return;
            _value = snapped;
            ValueChanged?.Invoke(_value);
        }

        private void SetFromPointer(float x) => Set(Min + (x - Rect.X - KnobWidth / 2f) / Travel * (Max - Min));

        public override void OnPress(float x, float y) => SetFromPointer(x);

        public override bool OnDrag(float x, float y, float dx, float dy)
        {
            SetFromPointer(x);
            return true;
        }

        public override void OnClick(float x, float y) { }
        public override void OnSubmit() { }

        public override bool OnNavigate(UiNav nav)
        {
            if (nav != UiNav.Left && nav != UiNav.Right) return false;
            float step = Step > 0 ? Step : (Max - Min) / 10f;
            Set(_value + (nav == UiNav.Right ? step : -step));
            return true;
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            float trackH = Rect.H / 3f;
            var track = new UiRect(Rect.X + KnobWidth / 2f, Rect.CenterY - trackH / 2f, Travel, trackH);
            list.Fill(track, IsActive ? Track : UiTheme.ButtonDisabled);
            list.Fill(new UiRect(track.X, track.Y, track.W * Fraction, track.H), Fill);
            list.Fill(KnobRect, Knob);
            if (Focused) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }
}
