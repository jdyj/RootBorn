using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Workspaces;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Workspaces
{
    /// <summary>
    /// 템플릿 고르기 화면의 배치 수치(1920x1080, 패널 왼쪽 위 기준 — x는 오른쪽, y는 아래로). 화면 구성은 추천으로 정함(사용자 확인 전).
    /// 새 디자인에서는 여기만 고친다.
    /// </summary>
    public static class WorkspaceTemplateLayout
    {
        public const float Width = 760, Height = 600;
        public static readonly (float X, float Y, float W, float H) Title = (32, 24, 600, 44);
        public const float TitleFont = 28;
        public static readonly (float X, float Y, float W, float H) Close = (684, 24, 44, 44);
        public static readonly (float X, float Y, float W, float H) List = (32, 84, 696, 216);
        public const float RowHeight = 48, ListFont = 20, ListPadding = 12;
        public static readonly (float X, float Y, float W, float H) Name = (32, 316, 696, 36);
        public const float NameFont = 22;
        public static readonly (float X, float Y, float W, float H) Cost = (32, 360, 696, 56);
        public const float CostFont = 18;
        public static readonly (float X, float Y, float W, float H) Status = (32, 424, 696, 84);
        public const float StatusFont = 18;
        public static readonly (float X, float Y, float W, float H) Apply = (488, 524, 240, 52);
        public const float ApplyFont = 20;
        public static readonly UiColor Ink = UnityLayout.Color(0.22f, 0.19f, 0.18f);
    }

    /// <summary>
    /// 일터 템플릿 고르기(모달, 이동을 막는다). 가구 배치 화면의 템플릿 단추(workspace-templates)만 연다.
    /// 목록(UiScrollList, 보이는 줄만 그린다)에서 템플릿을 고르면 이름·값과 지금 적용할 수 있는지(못 하면 이유)가 보이고, 적용 단추
    /// (id template-apply-&lt;템플릿 id&gt;)를 누르면 GameSession.ApplyTemplate을 부른다. 적용되면 닫는다(배치 화면이 바뀐 배치를 보인다).
    /// 돈을 같이 쓰는 방이면 명령이 방의 허락을 기다린다 — 기다림 문구를 보이고 단추를 막았다가, 끝나면 닫거나 이유를 보인다(성인 시설 화면과 같다).
    /// 적용할 수 있는지는 화면이 판단하지 않는다(CanApplyTemplate).
    /// </summary>
    public sealed class WorkspaceTemplateScreen : GameScreen
    {
        public const string RootId = "workspace-template", TitleId = "workspace-template.title", CloseId = "workspace-template.close",
            ListId = "workspace-template.list", NameId = "workspace-template.name", CostId = "workspace-template.cost", StatusId = "workspace-template.status";

        /// <summary>고른 템플릿이 없을 때의 적용 단추 id.</summary>
        public const string ApplyBaseId = "template-apply";
        public static string ApplyId(string templateId) => ApplyBaseId + "-" + templateId;

        private UiLabel _title, _name;
        private UiButton _close, _apply;
        private UiScrollList _list;
        private UiTextBlock _cost, _status;
        private string[] _rowNames = Array.Empty<string>();
        private string _statusKey;
        private bool _waitingForRoom;

        public WorkspaceTemplateScreen(ScreenServices services) : base(services) { }

        public WorkspaceTemplateModel Model { get; private set; } = WorkspaceTemplateModel.Empty;
        /// <summary>고른 줄(없으면 -1).</summary>
        public int SelectedIndex { get; private set; } = -1;
        /// <summary>마지막으로 누른 적용의 결과(없으면 null). 테스트와 로그용.</summary>
        public WorkspaceResultKind? LastResult { get; private set; }
        public UiScrollList List => _list;

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = RootId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, WorkspaceTemplateLayout.Width, WorkspaceTemplateLayout.Height);
            _title = At(root.Add(new UiLabel { Id = TitleId, FontSize = WorkspaceTemplateLayout.TitleFont, Color = WorkspaceTemplateLayout.Ink }), WorkspaceTemplateLayout.Title);
            _close = At(root.Add(new UiButton { Id = CloseId, Icon = ScreenIcons.Close }), WorkspaceTemplateLayout.Close);
            _close.Clicked += Close;
            _list = At(root.Add(new UiScrollList
            {
                Id = ListId, RowHeight = WorkspaceTemplateLayout.RowHeight, FontSize = WorkspaceTemplateLayout.ListFont, Padding = WorkspaceTemplateLayout.ListPadding,
            }), WorkspaceTemplateLayout.List);
            _list.RowText = row => row >= 0 && row < _rowNames.Length ? _rowNames[row] : string.Empty;
            _list.RowClicked += Select;
            _name = At(root.Add(new UiLabel { Id = NameId, FontSize = WorkspaceTemplateLayout.NameFont, Color = WorkspaceTemplateLayout.Ink }), WorkspaceTemplateLayout.Name);
            _cost = At(root.Add(new UiTextBlock { Id = CostId, FontSize = WorkspaceTemplateLayout.CostFont, Color = WorkspaceTemplateLayout.Ink }), WorkspaceTemplateLayout.Cost);
            _status = At(root.Add(new UiTextBlock { Id = StatusId, FontSize = WorkspaceTemplateLayout.StatusFont, Color = WorkspaceTemplateLayout.Ink }), WorkspaceTemplateLayout.Status);
            _apply = At(root.Add(new UiButton { Id = ApplyBaseId, FontSize = WorkspaceTemplateLayout.ApplyFont }), WorkspaceTemplateLayout.Apply);
            _apply.Clicked += Apply;
            return root;
        }

        private static T At<T>(T node, (float X, float Y, float W, float H) r) where T : UiNode
        {
            node.Place(0f, 0f, r.X, r.Y, r.W, r.H);
            return node;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            LastResult = null;
            _statusKey = null;
            _waitingForRoom = false;
            _list.ScrollOffset = 0;
            Reload(selectFirst: true);
        }

        protected internal override void OnChanged(string topic)
        {
            var session = Services.Session;
            if (_waitingForRoom && session != null && !session.WaitingForRoom && session.LastRoomCommandApplied.HasValue)
            {
                // 기다리던 적용이 끝났다: 됐으면 닫고, 방이 거절했으면 방 지갑 문구, 허락받고도 안 됐으면 지금 이유.
                _waitingForRoom = false;
                if (session.LastRoomCommandApplied.Value)
                {
                    LastResult = WorkspaceResultKind.Applied;
                    Close();
                    return;
                }
                _statusKey = session.LastRoomRefusal ? RoomMoneyText.RefusedKey : null;
                if (_statusKey == null && Selected != null)
                {
                    WorkspaceResultKind now = session.CanApplyTemplate(Selected.Id);
                    _statusKey = WorkspaceTemplateModel.ReasonKey(now == WorkspaceResultKind.Applied ? WorkspaceResultKind.InvalidRequest : now);
                }
            }

            if (topic != ChangeTopics.Furniture && topic != ChangeTopics.Workspace && topic != ChangeTopics.Career && topic != ChangeTopics.Inventory
                && topic != ChangeTopics.Language && topic != ChangeTopics.All) return;
            Reload(selectFirst: false);
        }

        /// <summary>Esc로 닫는다.</summary>
        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key != ScreenKey.Escape) return false;
            Close();
            return true;
        }

        private WorkspaceTemplateRow Selected => SelectedIndex >= 0 && SelectedIndex < Model.Rows.Length ? Model.Rows[SelectedIndex] : null;

        private void Reload(bool selectFirst)
        {
            string selectedId = Selected?.Id;
            Model = WorkspaceTemplateModel.From(Services.Session);
            _rowNames = new string[Model.Rows.Length]; // 줄 글자는 값이 바뀔 때만 만든다
            for (int i = 0; i < _rowNames.Length; i++) _rowNames[i] = Shown(Model.Rows[i].NameKey);
            _list.RowCount = _rowNames.Length;
            int index = selectFirst || selectedId == null ? (Model.Rows.Length > 0 ? 0 : -1) : Model.IndexOf(selectedId);
            SelectedIndex = index;
            _list.SelectedIndex = index;
            Show();
        }

        private void Select(int row)
        {
            if (row < 0 || row >= Model.Rows.Length) return;
            SelectedIndex = row;
            _list.SelectedIndex = row;
            if (!_waitingForRoom) _statusKey = null;
            Show();
        }

        private void Show()
        {
            _title.Text = T("ui.workspace.template.title");
            _apply.Text = T("ui.workspace.template.apply");
            WorkspaceTemplateRow row = Selected;
            _apply.Id = row == null ? ApplyBaseId : ApplyId(row.Id);
            _name.Text = row == null ? string.Empty : Shown(row.NameKey);
            _cost.Text = row == null ? string.Empty : F("ui.workspace.template.cost", Costs(row.Costs));
            _apply.Enabled = row != null && row.CanApply && !_waitingForRoom;
            string key = _statusKey;
            if (key == null)
                key = row == null ? "ui.workspace.template.empty" : row.CanApply ? "ui.workspace.template.ready" : WorkspaceTemplateModel.ReasonKey(row.Check);
            _status.Text = T(key);
        }

        private string Costs(IReadOnlyList<ItemAmount<string>> amounts)
        {
            if (amounts == null || amounts.Count == 0) return T("ui.workspace.template.free");
            var parts = new List<string>(amounts.Count);
            foreach (ItemAmount<string> amount in amounts)
            {
                ItemDefinition item = Services.Session?.Item(amount.Item);
                string key = item == null ? amount.Item : string.IsNullOrEmpty(item.DisplayKey) ? item.Id : item.DisplayKey;
                parts.Add(F("ui.workspace.template.cost-item", Shown(key), amount.Count));
            }
            return string.Join(T("ui.workspace.template.cost-separator"), parts);
        }

        private void Apply()
        {
            var session = Services.Session;
            WorkspaceTemplateRow row = Selected;
            if (session == null || row == null || session.WaitingForRoom) return; // 방의 허락을 기다리는 동안은 다시 누르지 않는다
            string request = "workspace-template:" + row.Id + ":" + Guid.NewGuid().ToString("N");
            WorkspaceResultKind kind = session.ApplyTemplate(row.Id, request);
            LastResult = kind;
            if (kind == WorkspaceResultKind.Applied) { Close(); return; }
            _waitingForRoom = session.WaitingForRoom;
            _statusKey = _waitingForRoom ? RoomMoneyText.WaitingKey : WorkspaceTemplateModel.ReasonKey(kind);
            Reload(selectFirst: false);
        }
    }
}
