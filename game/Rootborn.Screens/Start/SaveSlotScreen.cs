using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>슬롯 칸 하나의 표시 값. 이름은 플레이어가 쓴 글이라 키가 아니다(없으면 TitleKey).</summary>
    public sealed class SaveSlotRow
    {
        public string SlotId;
        public bool Exists;
        /// <summary>플레이어가 지은 이름(있는 슬롯만). 비면 TitleKey를 보인다.</summary>
        public string DisplayName;
        public string TitleKey;
        public string DescriptionKey;
        /// <summary>겉모습 줄의 문구 키(있는 슬롯은 "저장된 인물", 빈 슬롯은 "시작할 때 고를 수 있다").</summary>
        public string CharacterKey;
        /// <summary>테마 아이콘 키(Unity UrbanUiIcon: 있는 슬롯 journal, 빈 슬롯 plan).</summary>
        public string IconKey;
        /// <summary>칸에 그릴 겉모습(있는 슬롯만).</summary>
        public CreatorLook Look;
        /// <summary>마지막으로 저장한 시각(UTC 틱). Unity 판 화면은 보이지 않아 여기서도 그리지 않는다.</summary>
        public long UpdatedAtUtcTicks;
    }

    /// <summary>슬롯 선택 화면의 표시 모델(Unity SaveSlotSelectPanel.BuildSlotCard).</summary>
    public sealed class SaveSlotSelectModel
    {
        public IReadOnlyList<SaveSlotRow> Rows { get; private set; } = new SaveSlotRow[0];

        public static SaveSlotSelectModel From(StartMenu menu)
        {
            var rows = new List<SaveSlotRow>(SaveSlots.MaxUiSlots);
            foreach (SaveSlotSummary slot in menu.Slots.List())
            {
                var row = new SaveSlotRow { SlotId = slot.SlotId, Exists = slot.Exists };
                if (slot.Exists)
                {
                    row.DisplayName = string.IsNullOrWhiteSpace(slot.Metadata.DisplayName) ? null : slot.Metadata.DisplayName;
                    row.TitleKey = "ui.save.empty";
                    row.DescriptionKey = "ui.save.continue-description";
                    row.CharacterKey = "ui.save.saved-character";
                    row.IconKey = "journal";
                    row.Look = menu.Looks.SlotLook(slot.Metadata);
                    row.UpdatedAtUtcTicks = slot.Metadata.UpdatedAtUtcTicks;
                }
                else
                {
                    row.TitleKey = "ui.save.empty";
                    row.DescriptionKey = "ui.save.empty-description";
                    row.CharacterKey = "ui.save.choose-character";
                    row.IconKey = "plan";
                }
                rows.Add(row);
            }
            return new SaveSlotSelectModel { Rows = rows };
        }
    }

    /// <summary>
    /// 슬롯 선택(Unity SaveSlotSelectPanel). 화면 전체 바탕 위 가운데 1440x840 틀: 제목, 슬롯 칸 셋(아이콘, 이름, 설명, 겉모습,
    /// 이어서 시작·삭제 또는 새 이야기), 돌아가기, 삭제 안내, 들어가기 실패 문구. 새 이야기는 캐릭터 만들기 화면을 연다.
    /// 지우기는 확인 창을 거친다. 여는 쪽(Host)이 StartMenu를 넘긴다.
    /// </summary>
    public sealed class SaveSlotScreen : GameScreen
    {
        /// <summary>배치 — Unity SaveSlotSelectPanel 수치를 가운데 기준(y 위로)으로 옮긴 값. 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float FrameW = 1440f, FrameH = 840f;
            public const float TitleY = 352f, TitleW = 1250f, TitleH = 64f, TitleFont = 34f;
            public const float CardStepX = 445f, CardY = -27f, CardW = 414f, CardH = 470f;
            public const float IconY = 131f, IconSize = 144f;
            public const float CardTextW = 342f;
            public const float CardTitleY = 20f, CardTitleH = 52f, CardTitleFont = 25f;
            public const float CardDescriptionY = -37f, CardDescriptionH = 54f, CardDescriptionFont = 20f;
            public const float PreviewY = -96f, PreviewSize = 48f;
            public const float SavedCharacterY = -139f, SavedCharacterH = 28f, SavedCharacterFont = 16f;
            public const float ChooseCharacterY = -114f, ChooseCharacterH = 58f, ChooseCharacterFont = 18f;
            public const float CardButtonY = -190f, CardButtonH = 58f, PairButtonX = 87f, PairButtonW = 152f, NewButtonW = 278f, CardButtonFont = 22f;
            // 아래 줄: 돌아가기 · 서버 주소로 들어가기 · 방 열기 · 삭제 안내(오른쪽 맞춤). 틀 안쪽 -625~625.
            public const float BackX = -525f, BottomY = -350f, BackW = 200f, BackH = 58f, BackFont = 22f;
            public const float JoinX = -275f, JoinW = 280f;
            public const float HostX = -5f, HostW = 240f;
            public const float HintX = 380f, HintW = 490f, HintH = 44f, HintFont = 18f;
            public const float ErrorY = -304f, ErrorW = 1100f, ErrorH = 36f, ErrorFont = 18f;
            public static readonly UiColor Backdrop = new UiColor(211, 216, 212);
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
            public static readonly UiColor ErrorInk = UnityLayout.Color(0.65f, 0.24f, 0.17f);
        }

        public const string RootId = "save-slots";
        public const string FrameId = "save-slots.frame";
        public const string TitleId = "save-slots.title";
        public const string BackId = "save-slots.back";
        public const string JoinId = "save-slots.join";
        public const string HostId = "save-slots.host";
        public const string HintId = "save-slots.delete-hint";
        public const string ErrorId = "save-slots.error";
        public static string CardId(int i) => "save-slots.card." + i;
        public static string CardTitleId(int i) => CardId(i) + ".title";
        public static string CardDescriptionId(int i) => CardId(i) + ".description";
        public static string CardIconId(int i) => CardId(i) + ".icon";
        public static string CardPreviewId(int i) => CardId(i) + ".preview";
        public static string CardCharacterId(int i) => CardId(i) + ".character";
        public static string ContinueId(int i) => CardId(i) + ".continue";
        public static string DeleteId(int i) => CardId(i) + ".delete";
        public static string NewGameId(int i) => CardId(i) + ".new";

        private sealed class Card
        {
            public UiImage Icon;
            public UiLabel Title;
            public UiTextBlock Description, Choose;
            public CharacterPreview Preview;
            public UiLabel Saved;
            public UiButton Continue, Delete, NewGame;
        }

        private readonly Card[] _cards = new Card[SaveSlots.MaxUiSlots];
        private UiLabel _title, _hint, _error;
        private UiButton _back, _join, _host;
        private StartMenu _menu;
        private string _errorKey;

        public SaveSlotScreen(ScreenServices services) : base(services) { }

        public SaveSlotSelectModel Model { get; private set; } = new SaveSlotSelectModel();
        public StartMenu Menu => _menu;

        protected override UiNode Build()
        {
            var root = new UiBox { Id = RootId, Color = Metrics.Backdrop, Interactive = true };
            root.Stretch(0f, 0f, 0f, 0f);
            var frame = root.Add(new UiPanel { Id = FrameId, Skin = UiSkin.Panel }).Centered(0f, 0f, Metrics.FrameW, Metrics.FrameH);
            _title = frame.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink })
                .Centered(0f, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            for (int i = 0; i < _cards.Length; i++) _cards[i] = BuildCard(frame, i);
            _back = frame.Add(new UiButton { Id = BackId, FontSize = Metrics.BackFont }).Centered(Metrics.BackX, Metrics.BottomY, Metrics.BackW, Metrics.BackH);
            _back.Clicked += () => _menu?.Quit?.Invoke();
            _join = frame.Add(new UiButton { Id = JoinId, FontSize = Metrics.BackFont }).Centered(Metrics.JoinX, Metrics.BottomY, Metrics.JoinW, Metrics.BackH);
            _join.Clicked += OpenJoin;
            _host = frame.Add(new UiButton { Id = HostId, FontSize = Metrics.BackFont }).Centered(Metrics.HostX, Metrics.BottomY, Metrics.HostW, Metrics.BackH);
            _host.Clicked += OpenHost;
            _hint = frame.Add(new UiLabel { Id = HintId, FontSize = Metrics.HintFont, Color = Metrics.Ink, Align = UiAlign.Right })
                .Centered(Metrics.HintX, Metrics.BottomY, Metrics.HintW, Metrics.HintH);
            _error = frame.Add(new UiLabel { Id = ErrorId, FontSize = Metrics.ErrorFont, Color = Metrics.ErrorInk, Align = UiAlign.Center, Visible = false })
                .Centered(0f, Metrics.ErrorY, Metrics.ErrorW, Metrics.ErrorH);
            return root;
        }

        private Card BuildCard(UiNode frame, int index)
        {
            var c = new Card();
            var card = frame.Add(new UiPanel { Id = CardId(index), Skin = UiSkin.Slot, Interactive = false })
                .Centered((index - (SaveSlots.MaxUiSlots - 1) / 2f) * Metrics.CardStepX, Metrics.CardY, Metrics.CardW, Metrics.CardH);
            c.Icon = card.Add(new UiImage { Id = CardIconId(index) }).Centered(0f, Metrics.IconY, Metrics.IconSize, Metrics.IconSize);
            c.Title = card.Add(new UiLabel { Id = CardTitleId(index), FontSize = Metrics.CardTitleFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.CardTitleY, Metrics.CardTextW, Metrics.CardTitleH);
            c.Description = card.Add(new UiTextBlock { Id = CardDescriptionId(index), FontSize = Metrics.CardDescriptionFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.CardDescriptionY, Metrics.CardTextW, Metrics.CardDescriptionH);
            c.Preview = card.Add(new CharacterPreview { Id = CardPreviewId(index) }).Centered(0f, Metrics.PreviewY, Metrics.PreviewSize, Metrics.PreviewSize);
            c.Saved = card.Add(new UiLabel { Id = CardCharacterId(index), FontSize = Metrics.SavedCharacterFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.SavedCharacterY, Metrics.CardTextW, Metrics.SavedCharacterH);
            c.Choose = card.Add(new UiTextBlock { Id = CardCharacterId(index) + ".empty", FontSize = Metrics.ChooseCharacterFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.ChooseCharacterY, Metrics.CardTextW, Metrics.ChooseCharacterH);
            c.Continue = card.Add(new UiButton { Id = ContinueId(index), FontSize = Metrics.CardButtonFont })
                .Centered(-Metrics.PairButtonX, Metrics.CardButtonY, Metrics.PairButtonW, Metrics.CardButtonH);
            c.Delete = card.Add(new UiButton { Id = DeleteId(index), FontSize = Metrics.CardButtonFont })
                .Centered(Metrics.PairButtonX, Metrics.CardButtonY, Metrics.PairButtonW, Metrics.CardButtonH);
            c.NewGame = card.Add(new UiButton { Id = NewGameId(index), FontSize = Metrics.CardButtonFont })
                .Centered(0f, Metrics.CardButtonY, Metrics.NewButtonW, Metrics.CardButtonH);
            c.Continue.Clicked += () => Continue(index);
            c.Delete.Clicked += () => AskDelete(index);
            c.NewGame.Clicked += () => NewGame(index);
            return c;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (context.Argument is StartMenu menu) _menu = menu;
            else if (context.Argument is SlotSelectReturn back) { _menu = back.Menu; _errorKey = back.ErrorKey; }
            if (!(context.Argument is SlotSelectReturn)) _errorKey = null;
            Refresh();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        /// <summary>슬롯을 다시 읽어 칸을 고친다(열 때, 지운 뒤, 언어가 바뀔 때).</summary>
        public void Refresh()
        {
            if (_menu == null) return;
            Model = SaveSlotSelectModel.From(_menu);
            _title.Text = T("ui.save.title");
            _back.Text = T("ui.save.back");
            _back.Visible = _menu.Quit != null;
            _join.Text = T("ui.save.join-server");
            _join.Visible = _menu.Join != null;
            _host.Text = T("ui.save.host-room");
            _host.Visible = _menu.Host != null;
            _hint.Text = T("ui.save.delete-hint");
            for (int i = 0; i < _cards.Length; i++)
            {
                Card c = _cards[i];
                SaveSlotRow row = i < Model.Rows.Count ? Model.Rows[i] : null;
                if (row == null) continue;
                c.Icon.Image = "icon:" + row.IconKey;
                c.Title.Text = row.DisplayName ?? T(row.TitleKey);
                c.Description.Text = T(row.DescriptionKey);
                c.Preview.Visible = c.Saved.Visible = c.Continue.Visible = c.Delete.Visible = row.Exists;
                c.Choose.Visible = c.NewGame.Visible = !row.Exists;
                if (row.Exists)
                {
                    c.Preview.Show(row.Look, _menu.Art);
                    c.Saved.Text = T(row.CharacterKey);
                    c.Continue.Text = T("ui.save.load");
                    c.Delete.Text = T("ui.save.delete");
                }
                else
                {
                    c.Choose.Text = T(row.CharacterKey);
                    c.NewGame.Text = T("ui.save.new");
                }
            }
            ShowError(_errorKey);
        }

        private void ShowError(string key)
        {
            _errorKey = key;
            _error.Visible = !string.IsNullOrEmpty(key);
            _error.Text = _error.Visible ? T(key) : string.Empty;
        }

        /// <summary>마지막으로 보인 실패 문구 키(없으면 null).</summary>
        public string ErrorKey => _errorKey;

        private SaveSlotRow Row(int index) => index < Model.Rows.Count ? Model.Rows[index] : null;

        private void Continue(int index)
        {
            SaveSlotRow row = Row(index);
            if (_menu == null || row == null || !row.Exists) return;
            if (!_menu.Enter(new StartEntry(row.SlotId, false))) ShowError(EntryFailedKey);
            if (Services.Screens != null && Services.Screens.IsOpen(ScreenCatalog.SaveSlotDamaged)) ShowError(null); // 손상 알림이 문제를 말한다
        }

        /// <summary>Unity SaveSlotSelectPanel.EntryFailure의 문구.</summary>
        public const string EntryFailedKey = "world.entry.failed";

        private void AskDelete(int index)
        {
            SaveSlotRow row = Row(index);
            if (_menu == null || row == null || !row.Exists) return;
            Services.Screens?.Open(ScreenCatalog.DeleteSlot, argument: new DeleteSlotOpen(_menu, _menu.Slots.Read(row.SlotId), Refresh));
        }

        /// <summary>서버 주소로 들어가기 창을 연다(들어가는 쪽이 있을 때만 보이는 단추).</summary>
        private void OpenJoin()
        {
            if (_menu?.Join == null) return;
            Services.Screens?.Open(ScreenCatalog.JoinServer, argument: new JoinServerOpen(_menu));
        }

        /// <summary>내 PC에서 방 열기 창을 연다(여는 쪽이 있을 때만 보이는 단추).</summary>
        private void OpenHost()
        {
            if (_menu?.Host == null) return;
            Services.Screens?.Open(ScreenCatalog.HostRoom, argument: new HostRoomOpen(_menu));
        }

        private void NewGame(int index)
        {
            SaveSlotRow row = Row(index);
            if (_menu == null || row == null || row.Exists) return;
            Services.Screens?.Open(ScreenCatalog.CharacterCreator, argument: new CreatorOpen(_menu, row.SlotId));
        }
    }

    /// <summary>만들기 화면에서 슬롯 선택으로 돌아올 때 넘기는 값(실패했으면 그 문구 키).</summary>
    public sealed record SlotSelectReturn(StartMenu Menu, string ErrorKey);
}
