using System.Collections.Generic;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>손상 알림의 한 줄: 문제 종류의 문구 키와 그 자리에 넣을 파일 이름.</summary>
    public readonly struct SaveSlotDamageLine
    {
        public SaveSlotDamageLine(string key, string fileName) { Key = key; FileName = fileName ?? string.Empty; }
        public string Key { get; }
        public string FileName { get; }
    }

    /// <summary>손상 알림의 표시 모델: 어느 슬롯인지와 문제 줄(읽은 순서). 시작을 거절한 예외(SaveSlotDamagedException)에서 만든다.</summary>
    public sealed class SaveSlotDamagedModel
    {
        /// <summary>문제 종류 → 문구 키. 새 종류가 생기면 한 줄 더한다. 표에 없는 종류는 UnknownKey로 보인다.</summary>
        private static readonly Dictionary<SaveSlotProblemKind, string> KindKeys = new Dictionary<SaveSlotProblemKind, string>
        {
            [SaveSlotProblemKind.Missing] = "ui.save.damaged.missing",
            [SaveSlotProblemKind.Unreadable] = "ui.save.damaged.unreadable",
        };

        public const string UnknownKey = "ui.save.damaged.unknown";

        public string SlotId { get; private set; } = string.Empty;
        public IReadOnlyList<SaveSlotDamageLine> Problems { get; private set; } = new SaveSlotDamageLine[0];

        public static SaveSlotDamagedModel From(SaveSlotDamagedException error)
        {
            if (error == null) return new SaveSlotDamagedModel();
            var lines = new List<SaveSlotDamageLine>(error.Problems.Count);
            foreach (SaveSlotProblem problem in error.Problems)
            {
                if (problem == null) continue;
                lines.Add(new SaveSlotDamageLine(KindKeys.TryGetValue(problem.Kind, out string key) ? key : UnknownKey, problem.FileName));
            }
            return new SaveSlotDamagedModel { SlotId = error.SaveSlot, Problems = lines };
        }
    }

    /// <summary>
    /// 손상된 슬롯 알림(모달, 슬롯 선택 위). 고른 슬롯의 파일이 없거나 읽을 수 없어 시작을 거절했을 때 Host가 연다(argument: SaveSlotDamagedException).
    /// 제목, 설명(슬롯 id, 파일은 바꾸지 않았다는 것), 문제 줄(앞의 MaxLines개와 나머지 개수), 돌아가기. 단추는 돌아가기 하나다 —
    /// 새 게임으로 덮을지, 사본(backups/&lt;날&gt;/)에서 되살릴지는 기획 결정이 필요해 넣지 않았다.
    /// Unity 판에는 없던 화면이다(Unity 판은 손상 슬롯을 새 게임으로 덮어 시작했다).
    /// </summary>
    public sealed class SaveSlotDamagedScreen : GameScreen
    {
        /// <summary>문제 줄을 몇 개까지 보이는가. 나머지는 "그 밖에 n개" 한 줄.</summary>
        public const int MaxLines = 5;

        internal static class Metrics
        {
            public const float BoxW = 960f, BoxH = 600f;
            public const float TextW = 840f;
            public const float TitleY = 236f, TitleH = 52f, TitleFont = 30f;
            public const float BodyY = 164f, BodyH = 72f, BodyFont = 20f;
            public const float FirstLineY = 96f, LineStep = 40f, LineH = 36f, LineFont = 18f;
            public const float ButtonY = -238f, ButtonW = 250f, ButtonH = 58f, ButtonFont = 22f;
            public static readonly UiColor Shade = UnityLayout.Color(0f, 0f, 0f, 0.45f);
            public static readonly UiColor Ink = new UiColor(48, 71, 83);
            public static readonly UiColor ProblemInk = UnityLayout.Color(0.65f, 0.24f, 0.17f);
        }

        public const string RootId = "save-slot-damaged";
        public const string BoxId = "save-slot-damaged.box";
        public const string TitleId = "save-slot-damaged.title";
        public const string BodyId = "save-slot-damaged.body";
        public const string MoreId = "save-slot-damaged.more";
        public const string BackId = "save-slot-damaged.back";
        public static string ProblemId(int i) => "save-slot-damaged.problem." + i;

        private readonly UiLabel[] _lines = new UiLabel[MaxLines];
        private UiLabel _title, _more;
        private UiTextBlock _body;
        private UiButton _back;

        public SaveSlotDamagedScreen(ScreenServices services) : base(services) { }

        public SaveSlotDamagedModel Model { get; private set; } = new SaveSlotDamagedModel();

        protected override UiNode Build()
        {
            var root = new UiBox { Id = RootId, Color = Metrics.Shade, Interactive = true };
            root.Stretch(0f, 0f, 0f, 0f);
            var box = root.Add(new UiPanel { Id = BoxId, Skin = UiSkin.Panel }).Centered(0f, 0f, Metrics.BoxW, Metrics.BoxH);
            _title = box.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.TitleY, Metrics.TextW, Metrics.TitleH);
            _body = box.Add(new UiTextBlock { Id = BodyId, FontSize = Metrics.BodyFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.BodyY, Metrics.TextW, Metrics.BodyH);
            for (int i = 0; i <= MaxLines; i++)
            {
                var line = box.Add(new UiLabel { Id = i < MaxLines ? ProblemId(i) : MoreId, FontSize = Metrics.LineFont, Color = i < MaxLines ? Metrics.ProblemInk : Metrics.Ink })
                    .Centered(0f, Metrics.FirstLineY - i * Metrics.LineStep, Metrics.TextW, Metrics.LineH);
                if (i < MaxLines) _lines[i] = line; else _more = line;
            }
            _back = box.Add(new UiButton { Id = BackId, FontSize = Metrics.ButtonFont }).Centered(0f, Metrics.ButtonY, Metrics.ButtonW, Metrics.ButtonH);
            _back.Clicked += Close;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            Model = SaveSlotDamagedModel.From(context.Argument as SaveSlotDamagedException);
            Refresh();
            Services.Screens?.Tree.SetFocus(_back);
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key != ScreenKey.Escape) return false;
            Close();
            return true;
        }

        private void Refresh()
        {
            _title.Text = T("ui.save.damaged.title");
            _body.Text = F("ui.save.damaged.body", Model.SlotId);
            IReadOnlyList<SaveSlotDamageLine> problems = Model.Problems;
            for (int i = 0; i < _lines.Length; i++)
            {
                bool shown = i < problems.Count;
                _lines[i].Visible = shown;
                _lines[i].Text = shown ? F(problems[i].Key, problems[i].FileName) : string.Empty;
            }
            int more = problems.Count - MaxLines;
            _more.Visible = more > 0;
            _more.Text = more > 0 ? F("ui.save.damaged.more", more) : string.Empty;
            _back.Text = T("ui.save.damaged.back");
        }
    }
}
