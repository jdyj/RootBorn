using System;
using System.Collections.Generic;
using Rootborn.Core.Save;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 캐릭터 만들기 화면의 상태(Unity SpumCharacterCreatorPanel): 단계마다 고른 번호, 미리 보는 단계, 이름.
    /// 고르기는 단계 안에서 돌아간다(마지막 다음은 처음). 이름은 앞뒤 공백을 지우고 12자까지.
    /// </summary>
    public sealed class CharacterCreatorModel
    {
        /// <summary>Unity SpumCharacterCreatorPanel.MaxNameLength.</summary>
        public const int MaxNameLength = 12;

        private readonly int[] _choices;

        public CharacterCreatorModel(CreatorLooks looks)
        {
            Looks = looks ?? throw new ArgumentNullException(nameof(looks));
            _choices = new int[looks.Stages.Count];
        }

        public CreatorLooks Looks { get; }
        public int StageCount => _choices.Length;
        public int PreviewStage { get; private set; }
        public IReadOnlyList<int> Choices => _choices;

        public CreatorLook ChoiceAt(int stage) => Looks.Choice(stage, _choices);
        public bool CanStep(int stage) => stage >= 0 && stage < StageCount && Looks.Stages[stage].Count > 1;

        public void Step(int stage, int offset)
        {
            if (stage < 0 || stage >= StageCount || Looks.Stages[stage].Count == 0) return;
            int count = Looks.Stages[stage].Count;
            _choices[stage] = ((_choices[stage] + offset) % count + count) % count;
            PreviewStage = stage;
        }

        public void Preview(int stage)
        {
            if (stage >= 0 && stage < StageCount) PreviewStage = stage;
        }

        /// <summary>Unity RandomizeSelection(seed): System.Random(seed)로 단계마다 하나씩.</summary>
        public void Randomize(int seed)
        {
            var random = new Random(seed);
            for (int i = 0; i < _choices.Length; i++) _choices[i] = random.Next(Looks.Stages[i].Count);
        }

        public CharacterAppearanceSnapshotSaveData Snapshot() => Looks.Snapshot(_choices);

        public static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string trimmed = value.Trim();
            var info = new System.Globalization.StringInfo(trimmed);
            return info.LengthInTextElements > MaxNameLength ? info.SubstringByTextElements(0, MaxNameLength) : trimmed;
        }
    }

    /// <summary>
    /// 캐릭터 만들기(Unity SpumCharacterCreatorPanel). 어두운 바탕 위 가운데 1296x864 패널: 머리줄(제목, 뒤로, 무작위, 시작하기),
    /// 왼쪽 미리보기(겹친 캐릭터 그림과 단계 이름), 오른쪽 고르기(이름 칸, 머리(한 가지라 화살표 없음), 단계마다 ◀ 겉모습 ▶ 줄),
    /// 아래 안내. 단계 줄을 누르면 그 단계를 미리 본다. 시작하기는 새 이야기를 슬롯에 쓰고 게임에 들어간다(실패하면 슬롯 선택으로
    /// 돌아가 실패 문구를 보인다). 뒤로는 아무것도 쓰지 않고 슬롯 선택으로 돌아간다.
    /// </summary>
    public sealed class CharacterCreatorScreen : GameScreen
    {
        /// <summary>배치 — Unity SpumCharacterCreatorPanel 수치를 패널 가운데 기준(y 위로)으로 옮긴 값. 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float PanelW = 1296f, PanelH = 864f;
            public const float HeaderY = 360f, HeaderButtonH = 52f, HeaderButtonFont = 20f;
            public const float TitleX = -380f, TitleW = 420f, TitleH = 52f, TitleFont = 32f;
            public const float BackX = 170f, BackW = 120f, RandomX = 318f, RandomW = 140f, ConfirmX = 478f, ConfirmW = 150f;
            public const float PreviewX = -400f, PreviewW = 380f, PreviewH = 560f;
            public const float CharacterY = 30f, CharacterSize = 300f;
            public const float CaptionY = -230f, CaptionW = 340f, CaptionH = 48f, CaptionFont = 22f;
            public const float ChoicesX = 210f, FirstRowY = 230f, RowStep = 90f;
            public const float LabelX = -50f, LabelW = 170f, LabelH = 48f, LabelFont = 22f;
            public const float FieldX = 300f, FieldW = 480f, FieldH = 56f, FieldFont = 22f;
            public const float ArrowPreviousX = 90f, ArrowNextX = 510f, ArrowW = 56f, ArrowH = 52f, ArrowFont = 20f;
            public const float ChoiceTextX = 300f, ChoiceTextW = 340f, ChoiceTextH = 52f, ChoiceFont = 22f;
            public const float RowW = 690f, RowH = 72f;
            public const float FooterY = -350f, HintW = 1120f, HintH = 40f, HintFont = 20f;
            public const string PreviousGlyph = "<", NextGlyph = ">";
            public static readonly UiColor Backdrop = UnityLayout.Color(0.06f, 0.08f, 0.12f);
            public static readonly UiColor PreviewBackdrop = UnityLayout.Color(0.13f, 0.16f, 0.22f);
            public static readonly UiColor Ink = UnityLayout.Color(0.10f, 0.13f, 0.20f);
            public static readonly UiColor CaptionInk = UiTheme.White;
            public static readonly UiColor SelectedRow = UnityLayout.Color(0.35f, 0.45f, 0.70f, 0.25f);
        }

        public const string RootId = "creator";
        public const string PanelId = "creator.panel";
        public const string TitleId = "creator.title";
        public const string BackId = "creator.back";
        public const string RandomId = "creator.random";
        public const string ConfirmId = "creator.confirm";
        public const string PreviewId = "creator.preview";
        public const string CharacterId = "creator.preview.character";
        public const string CaptionId = "creator.preview.caption";
        public const string NameLabelId = "creator.name.label";
        public const string NameFieldId = "creator.name";
        public const string HairLabelId = "creator.hair.label";
        public const string HairChoiceId = "creator.hair.choice";
        public const string HintId = "creator.hint";
        public static string StageRowId(int stage) => "creator.stage." + stage;
        public static string StageLabelId(int stage) => StageRowId(stage) + ".label";
        public static string StageChoiceId(int stage) => StageRowId(stage) + ".choice";
        public static string StagePreviousId(int stage) => StageRowId(stage) + ".previous";
        public static string StageNextId(int stage) => StageRowId(stage) + ".next";

        /// <summary>단계 줄 수. 데이터의 단계가 이보다 많으면 앞의 것만 보인다(Unity 판 패널에 들어가는 줄 수).</summary>
        public const int MaxStages = 3;

        private sealed class StageRow
        {
            public UiSelectableRow Background;
            public UiLabel Label, Choice;
            public UiButton Previous, Next;
        }

        private readonly StageRow[] _rows = new StageRow[MaxStages];
        private UiLabel _title, _caption, _nameLabel, _hairLabel, _hairChoice, _hint;
        private UiButton _back, _random, _confirm;
        private UiTextField _name;
        private CharacterPreview _character;
        private StartMenu _menu;
        private string _slotId;

        public CharacterCreatorScreen(ScreenServices services) : base(services) { }

        public CharacterCreatorModel Model { get; private set; }
        public string SlotId => _slotId;

        /// <summary>이름 칸에 지금 들어 있는 이름(정리한 값).</summary>
        public string PlayerName => CharacterCreatorModel.CleanName(_name?.Text);

        protected override UiNode Build()
        {
            var root = new UiBox { Id = RootId, Color = Metrics.Backdrop, Interactive = true };
            root.Stretch(0f, 0f, 0f, 0f);
            var panel = root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel }).Centered(0f, 0f, Metrics.PanelW, Metrics.PanelH);

            _title = panel.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink }).Centered(Metrics.TitleX, Metrics.HeaderY, Metrics.TitleW, Metrics.TitleH);
            _back = HeaderButton(panel, BackId, Metrics.BackX, Metrics.BackW);
            _random = HeaderButton(panel, RandomId, Metrics.RandomX, Metrics.RandomW);
            _confirm = HeaderButton(panel, ConfirmId, Metrics.ConfirmX, Metrics.ConfirmW);
            _back.Clicked += Back;
            _random.Clicked += Randomize;
            _confirm.Clicked += Confirm;

            var preview = panel.Add(new UiBox { Id = PreviewId, Color = Metrics.PreviewBackdrop }).Centered(Metrics.PreviewX, 0f, Metrics.PreviewW, Metrics.PreviewH);
            _character = preview.Add(new CharacterPreview { Id = CharacterId }).Centered(0f, Metrics.CharacterY, Metrics.CharacterSize, Metrics.CharacterSize);
            _caption = preview.Add(new UiLabel { Id = CaptionId, FontSize = Metrics.CaptionFont, Color = Metrics.CaptionInk, Align = UiAlign.Center })
                .Centered(0f, Metrics.CaptionY, Metrics.CaptionW, Metrics.CaptionH);

            float y = Metrics.FirstRowY;
            _nameLabel = Label(panel, NameLabelId, y);
            _name = panel.Add(new UiTextField { Id = NameFieldId, MaxLength = CharacterCreatorModel.MaxNameLength, FontSize = Metrics.FieldFont })
                .Centered(Metrics.FieldX, y, Metrics.FieldW, Metrics.FieldH);
            y -= Metrics.RowStep;
            _hairLabel = Label(panel, HairLabelId, y);
            _hairChoice = ChoiceText(panel, HairChoiceId, y);
            y -= Metrics.RowStep;

            for (int i = 0; i < _rows.Length; i++, y -= Metrics.RowStep)
            {
                int stage = i;
                var row = new StageRow();
                row.Background = panel.Add(new UiSelectableRow { Id = StageRowId(i), SelectedColor = Metrics.SelectedRow })
                    .Centered(Metrics.ChoicesX, y, Metrics.RowW, Metrics.RowH);
                row.Background.Clicked += () => PreviewStage(stage);
                row.Label = Label(panel, StageLabelId(i), y);
                row.Previous = panel.Add(new UiButton { Id = StagePreviousId(i), Text = Metrics.PreviousGlyph, FontSize = Metrics.ArrowFont })
                    .Centered(Metrics.ArrowPreviousX, y, Metrics.ArrowW, Metrics.ArrowH);
                row.Next = panel.Add(new UiButton { Id = StageNextId(i), Text = Metrics.NextGlyph, FontSize = Metrics.ArrowFont })
                    .Centered(Metrics.ArrowNextX, y, Metrics.ArrowW, Metrics.ArrowH);
                row.Choice = ChoiceText(panel, StageChoiceId(i), y);
                row.Previous.Clicked += () => Step(stage, -1);
                row.Next.Clicked += () => Step(stage, 1);
                _rows[i] = row;
            }

            _hint = panel.Add(new UiLabel { Id = HintId, FontSize = Metrics.HintFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.FooterY, Metrics.HintW, Metrics.HintH);
            return root;
        }

        private static UiButton HeaderButton(UiNode panel, string id, float x, float w) =>
            panel.Add(new UiButton { Id = id, FontSize = Metrics.HeaderButtonFont }).Centered(x, Metrics.HeaderY, w, Metrics.HeaderButtonH);

        private static UiLabel Label(UiNode panel, string id, float y) =>
            panel.Add(new UiLabel { Id = id, FontSize = Metrics.LabelFont, Color = Metrics.Ink }).Centered(Metrics.LabelX, y, Metrics.LabelW, Metrics.LabelH);

        private static UiLabel ChoiceText(UiNode panel, string id, float y) =>
            panel.Add(new UiLabel { Id = id, FontSize = Metrics.ChoiceFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(Metrics.ChoiceTextX, y, Metrics.ChoiceTextW, Metrics.ChoiceTextH);

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            var open = context.Argument as CreatorOpen;
            _menu = open?.Menu;
            _slotId = open?.SlotId;
            Model = new CharacterCreatorModel(_menu?.Looks ?? new CreatorLooks(Array.Empty<CreatorLook>()));
            _name.SetText(string.Empty); // Unity: 기본 이름은 빈 글자
            RefreshAll();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) RefreshAll();
        }

        private void RefreshAll()
        {
            if (Model == null) return;
            _title.Text = T("ui.creator.title");
            _back.Text = T("ui.creator.back");
            _random.Text = T("ui.creator.random");
            _confirm.Text = T("ui.creator.confirm");
            _nameLabel.Text = T("ui.creator.name");
            _name.Placeholder = T("ui.creator.name.placeholder");
            _hairLabel.Text = T("ui.creator.hair");
            _hairChoice.Text = T("ui.creator.hair.short_brown");
            _hint.Text = T("ui.creator.hint");
            for (int i = 0; i < _rows.Length; i++)
            {
                StageRow row = _rows[i];
                bool shown = i < Model.StageCount;
                row.Background.Visible = row.Label.Visible = row.Choice.Visible = shown;
                row.Previous.Visible = row.Next.Visible = shown && Model.CanStep(i); // 하나뿐이면 화살표를 숨긴다(Unity)
                if (!shown) continue;
                CreatorLook look = Model.ChoiceAt(i);
                row.Label.Text = T(CreatorLooks.StageLabelKey(Model.Looks.Stages[i][0]));
                row.Choice.Text = look != null ? Shown(look.NameKey) : string.Empty;
                row.Background.Selected = i == Model.PreviewStage;
            }
            CreatorLook shownLook = Model.ChoiceAt(Model.PreviewStage);
            _caption.Text = shownLook != null ? F("ui.creator.preview", T(CreatorLooks.StageLabelKey(shownLook))) : string.Empty;
            _character.Show(shownLook, _menu?.Art);
        }

        private void Step(int stage, int offset)
        {
            Model?.Step(stage, offset);
            RefreshAll();
        }

        private void PreviewStage(int stage)
        {
            Model?.Preview(stage);
            RefreshAll();
        }

        private void Randomize()
        {
            if (Model == null || _menu == null) return;
            Model.Randomize(_menu.TickCount());
            RefreshAll();
        }

        private void Back()
        {
            if (_menu == null) { Close(); return; }
            Services.Screens?.Open(ScreenCatalog.SaveSlotSelect, argument: _menu);
        }

        /// <summary>
        /// 시작하기(Unity ConfirmSpumCreator): 새 슬롯 정보(시드는 지금 값, 이름, 고른 겉모습)를 이야기가 없는 슬롯에 쓰고 들어간다.
        /// 슬롯에 읽히는 이야기가 있거나 들어가지 못하면 슬롯 선택으로 돌아가 실패 문구를 보인다.
        /// </summary>
        private void Confirm()
        {
            if (_menu == null || Model == null || string.IsNullOrEmpty(_slotId)) return;
            SaveSlotMetadata metadata = SaveSlots.CreateMetadata(_slotId, PlayerName, Model.Snapshot(), _menu.UtcNow(), _menu.TickCount());
            if (!_menu.Slots.StartNewSlot(metadata) || !_menu.Enter(new StartEntry(_slotId, true)))
                Services.Screens?.Open(ScreenCatalog.SaveSlotSelect, argument: new SlotSelectReturn(_menu, SaveSlotScreen.EntryFailedKey));
        }
    }
}
