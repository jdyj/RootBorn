using System;
using System.Collections.Generic;
using System.Text;
using Rootborn.Core.Dialogue;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Dialogue
{
    /// <summary>
    /// 대화 선택지 한 줄: 대화 정의 안의 순번, 문구 키(비면 퀘스트 행동 이름), 고르면 대화가 닫히는지,
    /// 지금 못 고르는 이유(None이면 고를 수 있다 — GameSession.DialogueChoices 판정 그대로).
    /// </summary>
    public readonly record struct DialogueChoiceRow(int Index, string LabelKey, string FallbackLabel, bool ClosesDialogue, DialogueChoiceBlock Block);

    /// <summary>
    /// 대화 화면의 표시 모델: 대사 키(모두 한꺼번에 보인다 — Unity DialoguePanel은 줄을 넘기지 않고 줄바꿈으로 이어 보였다)와
    /// 보이는 선택지(Unity 판처럼 숨김 규칙에 걸린 것은 뺀다). 보이는지·고를 수 있는지는 GameSession.DialogueChoices가 판단한다.
    /// </summary>
    public sealed class DialogueModel
    {
        public static readonly DialogueModel Empty = new DialogueModel(null, Array.Empty<string>(), Array.Empty<DialogueChoiceRow>());

        public DialogueModel(string dialogueId, string[] lineKeys, DialogueChoiceRow[] choices)
        {
            DialogueId = dialogueId; LineKeys = lineKeys; Choices = choices;
        }

        public string DialogueId { get; }
        public string[] LineKeys { get; }
        public DialogueChoiceRow[] Choices { get; }

        public static DialogueModel From(GameSession session)
        {
            DialogueDefinition dialogue = session?.CurrentDialogue;
            if (dialogue == null) return Empty;
            var lines = new List<string>(dialogue.LineKeys.Count);
            foreach (string key in dialogue.LineKeys) if (!string.IsNullOrEmpty(key)) lines.Add(key);
            IReadOnlyList<DialogueChoiceView> judged = session.DialogueChoices();
            var choices = new List<DialogueChoiceRow>(judged.Count);
            foreach (DialogueChoiceView view in judged)
            {
                if (!view.Evaluation.Visible) continue;
                DialogueChoice choice = view.Choice;
                choices.Add(new DialogueChoiceRow(view.Index, choice.LabelKey, choice.QuestAction.ToString(), choice.QuestAction != DialogueQuestAction.None, view.Evaluation.Block));
            }
            return new DialogueModel(dialogue.Id, lines.ToArray(), choices.ToArray());
        }
    }

    /// <summary>
    /// 대화(Unity DialoguePanel + NpcDialoguePresenter). NPC 앞에서 상호작용하면 열린다(상호작용 종류 "npc"로 등록).
    /// 열 때 GameSession.TalkTo로 말을 건다(퀘스트·체인·캠페인 알림은 GameSession이 한다). 아래 가운데의 폭 760 대화 패널,
    /// 대사, 고를 수 있는 선택지 버튼(위에서 아래로), 오른쪽 위 닫기 버튼. 패널 높이는 대사 길이와 선택지 수에 맞춘다.
    /// 키: E·Space·Enter는 첫 번째 고를 수 있는 선택지를 고른다(Unity 판과 같음). 고를 것이 없으면 키를 가져가지 않는다.
    /// 선택지를 골라 퀘스트를 받거나 보상을 받으면 닫힌다. 이동을 막지 않는다(Unity 판: 패널도 부르는 쪽도 입력을 잠그지 않았다).
    /// </summary>
    public sealed class DialogueScreen : GameScreen
    {
        /// <summary>배치 — Unity LocationNpcRuntimeInstaller.EnsureDialoguePanel과 DialoguePanel의 수치. 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float Width = 760f;
            public const float BottomMargin = 36f;
            // 대화창 안 여백은 테마의 대화창 스킨 여백(ScreenServices.PaddingOf(dialogue)). 높이는 여백 + 대사 + 간격 + 선택지.
            public const float SectionGap = 24f;
            public const float MinLineHeight = 84f;
            /// <summary>대사 오른쪽에 닫기 버튼과 띄울 자리.</summary>
            public const float LineRightReserve = 102f;
            public const float LineFont = 20f;
            public const float ChoiceHeight = 48f;
            public const float ChoiceStep = 60f;
            public const float ChoiceFont = 18f;
            public const float CloseSize = 48f;
            public const float CloseInset = 42f;
            /// <summary>닫기 버튼 기호(언어와 무관).</summary>
            public static readonly UiColor LineColor = UnityLayout.Color(0.12f, 0.18f, 0.25f);
        }

        public const string PanelId = "dialogue";
        public const string LinesId = "dialogue.lines";
        public const string CloseId = "dialogue.close";
        public static string ChoiceId(int visibleIndex) => "dialogue.choice." + visibleIndex;

        private DialogueRoot _root;
        private UiTextBlock _lines;
        private UiButton _close;
        private readonly List<UiButton> _choiceButtons = new List<UiButton>(4);

        public DialogueScreen(ScreenServices services) : base(services) { }

        public DialogueModel Model { get; private set; } = DialogueModel.Empty;
        /// <summary>대화를 건 NPC id.</summary>
        public string NpcId { get; private set; }
        /// <summary>마지막으로 고른 선택지가 적용됐는지.</summary>
        public bool? LastChoiceApplied { get; private set; }

        /// <summary>키보드 처음 포커스: 선택지가 있으면 첫 선택지, 없으면 닫기. 닫기 단추가 나무의 앞에 있다.</summary>
        protected internal override UiNode InitialFocus => UiTree.FirstFocusable(Root, _close);

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "dialogue.root" };
            _root = root.Add(new DialogueRoot(this) { Id = PanelId });
            _lines = _root.Add(new UiTextBlock { Id = LinesId, FontSize = Metrics.LineFont, Color = Metrics.LineColor });
            _close = _root.Add(new UiButton { Id = CloseId, Icon = Rootborn.Screens.Day.ScreenIcons.Close });
            _close.Place(1f, 0f, -Metrics.CloseInset - Metrics.CloseSize, Metrics.CloseInset, Metrics.CloseSize, Metrics.CloseSize);
            _close.Clicked += Close;
            Arrange(Metrics.MinLineHeight);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            LastChoiceApplied = null;
            if (context.Interaction.HasValue && Services.Session != null)
            {
                NpcId = context.Interaction.Value.TargetId;
                string npc = NpcId;
                Services.Session.TalkTo(npc);
            }
            Rebuild();
            if (Model.DialogueId == null) Close(); // 보여 줄 대화가 없다
        }

        protected internal override void OnClosed()
        {
            if (Services.Session != null && Services.Session.CurrentDialogue != null)
                Services.Session.CloseDialogue();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language) Rebuild();
        }

        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            bool choiceKey = key == ScreenKey.Interact || (key == ScreenKey.Enter && Services.Screens?.Tree.Focused == null);
            if (!choiceKey || Model.Choices.Length == 0) return false;
            Choose(0);
            return true;
        }

        /// <summary>보이는 선택지 하나를 고른다.</summary>
        private void Choose(int visibleIndex)
        {
            if (visibleIndex < 0 || visibleIndex >= Model.Choices.Length || Services.Session == null) return;
            DialogueChoiceRow row = Model.Choices[visibleIndex];
            bool applied = Services.Session.ChooseDialogueOption(row.Index);
            LastChoiceApplied = applied;
            if (applied && row.ClosesDialogue) { Close(); return; }
            Rebuild();
        }

        private void Rebuild()
        {
            Model = DialogueModel.From(Services.Session);
            var text = new StringBuilder(128);
            foreach (string key in Model.LineKeys)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(T(key));
            }
            _lines.Text = text.ToString();

            while (_choiceButtons.Count < Model.Choices.Length)
            {
                int visible = _choiceButtons.Count;
                var button = _root.Add(new UiButton { Id = ChoiceId(visible), FontSize = Metrics.ChoiceFont });
                button.Clicked += () => Choose(visible);
                _choiceButtons.Add(button);
            }
            for (int i = 0; i < _choiceButtons.Count; i++)
            {
                bool used = i < Model.Choices.Length;
                _choiceButtons[i].Visible = used;
                if (used)
                {
                    DialogueChoiceRow row = Model.Choices[i];
                    _choiceButtons[i].Text = string.IsNullOrEmpty(row.LabelKey) ? row.FallbackLabel : T(row.LabelKey);
                }
            }
            Arrange(_root.LineHeight);
        }

        private float ChoicesHeight => Model.Choices.Length > 0 ? (Model.Choices.Length - 1) * Metrics.ChoiceStep + Metrics.ChoiceHeight : 0f;

        /// <summary>대사 높이가 정해지면 패널 높이와 줄·버튼 자리를 다시 정한다(Unity RefreshContentLayout).</summary>
        internal void Arrange(float lineHeight)
        {
            UiInsets pad = Services.PaddingOf(UiSkin.Dialogue);
            float height = pad.Vertical + ChoicesHeight + Metrics.SectionGap + lineHeight;
            _root.LineHeight = lineHeight;
            _root.Place(0.5f, 1f, -Metrics.Width / 2f, -Metrics.BottomMargin - height, Metrics.Width, height);
            _lines.Place(0f, 0f, pad.Left, pad.Top, Metrics.Width - pad.Left - Metrics.LineRightReserve, lineHeight);
            float top = pad.Top + lineHeight + Metrics.SectionGap;
            for (int i = 0; i < _choiceButtons.Count; i++)
                _choiceButtons[i].Place(0f, 0f, pad.Left, top + i * Metrics.ChoiceStep, Metrics.Width - pad.Horizontal, Metrics.ChoiceHeight);
            if (_root.Parent != null && _root.Parent.Rect.W > 0) _root.Layout(_root.Parent.Rect);
        }

        /// <summary>대화 패널 뿌리: 그릴 때 대사의 실제 높이(글자 폭으로 줄바꿈한 결과)를 재서 패널 높이를 맞춘다.</summary>
        private sealed class DialogueRoot : UiNode
        {
            private readonly DialogueScreen _owner;

            public DialogueRoot(DialogueScreen owner) { _owner = owner; Interactive = true; }

            public float LineHeight = Metrics.MinLineHeight;

            public override void Emit(DrawList list, ITextMeasurer text)
            {
                float measured = Math.Max(Metrics.MinLineHeight, _owner._lines.ContentHeight(text));
                if (Math.Abs(measured - LineHeight) > 0.5f) _owner.Arrange(measured);
                list.Panel(Rect, UiSkin.Dialogue, UiTheme.White);
            }
        }
    }
}
