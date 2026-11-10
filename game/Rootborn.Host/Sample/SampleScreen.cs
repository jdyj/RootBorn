using Rootborn.Ui;

namespace Rootborn.Host.Sample
{
    /// <summary>
    /// UI 토대를 시험하는 견본 화면(--sample). 게임 화면이 아니다.
    /// 공통 패널, 버튼 3개(하나는 비활성), 5x4 칸 격자, 200줄 목록, 이름 입력 칸, 상태 글자.
    /// </summary>
    public sealed class SampleScreen
    {
        public const int SlotColumns = 5, SlotRows = 4, ListRows = 200;

        public readonly UiTree Tree = new UiTree();
        public readonly UiButton Ok, Reset, Disabled;
        public readonly UiSlot[] Slots = new UiSlot[SlotColumns * SlotRows];
        public readonly UiScrollList List;
        public readonly UiTextField Name;
        public readonly UiLabel Status;

        public int OkClicks { get; private set; }
        public int DisabledClicks { get; private set; }
        public int LastSlot { get; private set; } = -1;
        public int LastRow { get; private set; } = -1;
        public string SubmittedName { get; private set; }

        private readonly string[] _rowTexts = new string[ListRows];

        public SampleScreen()
        {
            UiPanel panel = Tree.Root.Add(new UiPanel { Id = "panel" });
            panel.Stretch(60, 60, 60, 60);

            panel.Add(new UiLabel { Id = "title", Text = "견본 화면 — UI 토대", FontSize = 36 }).Place(0, 0, 48, 36, 800, 48);

            Ok = AddButton(panel, "ok", "확인", 120);
            Reset = AddButton(panel, "reset", "처음으로", 220);
            Disabled = AddButton(panel, "disabled", "비활성", 320);
            Disabled.Enabled = false;
            Ok.Clicked += () => { OkClicks++; SetStatus("확인 버튼을 " + OkClicks + "번 눌렀습니다."); };
            Reset.Clicked += ResetCounters;
            Disabled.Clicked += () => DisabledClicks++;

            panel.Add(new UiLabel { Id = "nameLabel", Text = "이름", FontSize = 24 }).Place(0, 0, 48, 440, 280, 36);
            Name = panel.Add(new UiTextField { Id = "name", Placeholder = "이름을 입력하세요", MaxLength = 12, FontSize = 26 });
            Name.Place(0, 0, 48, 484, 420, 80);
            Name.Submitted += text =>
            {
                SubmittedName = text;
                SetStatus("이름: " + text);
                Tree.SetFocus(null);
            };

            const float slot = 96, gap = 12, gridX = 520, gridY = 120;
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i, column = i % SlotColumns, row = i / SlotColumns;
                UiSlot node = panel.Add(new UiSlot { Id = "slot" + i });
                node.Place(0, 0, gridX + column * (slot + gap), gridY + row * (slot + gap), slot, slot);
                node.Clicked += () => SelectSlot(index);
                Slots[i] = node;
            }

            for (int i = 0; i < ListRows; i++) _rowTexts[i] = "목록 " + (i + 1) + "번째 줄";
            List = panel.Add(new UiScrollList { Id = "list", RowCount = ListRows, RowHeight = 48, FontSize = 22 });
            List.Place(0, 0, 1140, 120, 600, 760);
            List.RowText = row => _rowTexts[row];
            List.RowClicked += row => { LastRow = row; SetStatus((row + 1) + "번째 줄을 골랐습니다."); };

            Status = panel.Add(new UiLabel { Id = "status", FontSize = 24 });
            Status.Place(0, 1, 48, -88, 1000, 40);
            SetStatus("버튼, 칸, 목록, 이름 칸을 눌러 보세요.");
        }

        private static UiButton AddButton(UiNode parent, string id, string text, float y)
        {
            UiButton button = parent.Add(new UiButton { Id = id, Text = text, FontSize = 26 });
            button.Place(0, 0, 48, y, 280, 80);
            return button;
        }

        private void SelectSlot(int index)
        {
            if (LastSlot >= 0) Slots[LastSlot].Selected = false;
            LastSlot = index;
            Slots[index].Selected = true;
            SetStatus((index / SlotColumns + 1) + "행 " + (index % SlotColumns + 1) + "열 칸을 골랐습니다.");
        }

        private void ResetCounters()
        {
            OkClicks = 0;
            if (LastSlot >= 0) Slots[LastSlot].Selected = false;
            LastSlot = -1;
            LastRow = -1;
            List.SelectedIndex = -1;
            List.ScrollOffset = 0;
            SubmittedName = null;
            Name.SetText("");
            SetStatus("처음 상태로 돌렸습니다.");
        }

        private void SetStatus(string text) => Status.Text = text;
    }
}
