using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 슬롯 지우기 확인(Unity SaveSlotSelectPanel.ShowDeleteConfirmation). 화면 전체를 어둡게 가리고 가운데 720x360 창:
    /// 묻는 글, 슬롯 이름, 되돌릴 수 없다는 글, 기록 삭제·취소. 열면 취소에 포커스가 간다. 모달이라 아래 슬롯 버튼은 눌리지 않는다.
    /// Unity 판은 세 문구를 줄바꿈으로 이어 한 글자 칸에 넣었다. 여기서는 문장을 잇지 않도록 줄마다 따로 둔다.
    /// </summary>
    public sealed class DeleteSlotScreen : GameScreen
    {
        internal static class Metrics
        {
            public const float BoxW = 720f, BoxH = 360f;
            public const float LineW = 600f, LineH = 44f, LineFont = 24f;
            public const float PromptY = 106f, NameY = 62f, WarningY = 18f;
            public const float ButtonX = 145f, ButtonY = -114f, ButtonW = 250f, ButtonH = 58f, ButtonFont = 22f;
            public static readonly UiColor Shade = UnityLayout.Color(0f, 0f, 0f, 0.45f);
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
        }

        public const string RootId = "delete-slot";
        public const string BoxId = "delete-slot.box";
        public const string PromptId = "delete-slot.prompt";
        public const string NameId = "delete-slot.name";
        public const string WarningId = "delete-slot.warning";
        public const string ConfirmId = "delete-slot.confirm";
        public const string CancelId = "delete-slot.cancel";

        private UiLabel _prompt, _name, _warning;
        private UiButton _confirm, _cancel;
        private DeleteSlotOpen _open;

        public DeleteSlotScreen(ScreenServices services) : base(services) { }

        protected override UiNode Build()
        {
            var root = new UiBox { Id = RootId, Color = Metrics.Shade, Interactive = true };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel }).Centered(0f, 0f, Metrics.BoxW, Metrics.BoxH);
            _prompt = Line(box, PromptId, Metrics.PromptY);
            _name = Line(box, NameId, Metrics.NameY);
            _warning = Line(box, WarningId, Metrics.WarningY);
            _confirm = box.Add(new UiButton { Id = ConfirmId, FontSize = Metrics.ButtonFont }).Centered(-Metrics.ButtonX, Metrics.ButtonY, Metrics.ButtonW, Metrics.ButtonH);
            _cancel = box.Add(new UiButton { Id = CancelId, FontSize = Metrics.ButtonFont }).Centered(Metrics.ButtonX, Metrics.ButtonY, Metrics.ButtonW, Metrics.ButtonH);
            _confirm.Clicked += ConfirmDelete;
            _cancel.Clicked += Close;
            return root;
        }

        private static UiLabel Line(UiNode box, string id, float y) =>
            box.Add(new UiLabel { Id = id, FontSize = Metrics.LineFont, Color = Metrics.Ink, Align = UiAlign.Center }).Centered(0f, y, Metrics.LineW, Metrics.LineH);

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            _open = context.Argument as DeleteSlotOpen;
            Refresh();
            Services.Screens?.Tree.SetFocus(_cancel);
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        private void Refresh()
        {
            _prompt.Text = T("ui.save.delete-prompt");
            _name.Text = _open?.Slot?.Metadata?.DisplayName ?? string.Empty;
            _warning.Text = T("ui.save.delete-warning");
            _confirm.Text = T("ui.save.confirm-delete");
            _cancel.Text = T("ui.save.cancel");
        }

        private void ConfirmDelete()
        {
            DeleteSlotOpen open = _open;
            if (open?.Slot == null || !open.Slot.Exists) { Close(); return; }
            open.Menu.Slots.Delete(open.Slot.SlotId);
            Close();
            open.Deleted?.Invoke();
        }
    }
}
