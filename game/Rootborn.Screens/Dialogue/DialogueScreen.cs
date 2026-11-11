using System;
using System.Collections.Generic;
using System.Text;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Quests;
using Rootborn.Core.WorldState;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Dialogue
{
    /// <summary>대화 선택지 한 줄: 대화 정의 안의 순번, 문구 키(비면 퀘스트 행동 이름), 고르면 대화가 닫히는지.</summary>
    public readonly record struct DialogueChoiceRow(int Index, string LabelKey, string FallbackLabel, bool ClosesDialogue);

    /// <summary>
    /// 대화 화면의 표시 모델: 대사 키(모두 한꺼번에 보인다 — Unity DialoguePanel은 줄을 넘기지 않고 줄바꿈으로 이어 보였다)와
    /// 지금 고를 수 있는 선택지(Unity 판처럼 고를 수 없는 것은 숨긴다). 고를 수 있는지는 Core DialogueChoice.IsAvailable이 판단한다.
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
            // 고를 수 있는지 보는 문맥(읽기만). GameSession에 이 조회가 없어 여기서 같은 값으로 만든다(보고서 "필요한 변경").
            var context = new DialogueChoiceContext(session.QuestLog, new RewardRuntimeContext(session.QuestLog, session.Inventory, null, null, session.Student,
                new WorldStateAccess(session.World, session.Definitions.All<WorldStateFlag>()), null));
            var choices = new List<DialogueChoiceRow>(dialogue.Choices.Count);
            for (int i = 0; i < dialogue.Choices.Count; i++)
            {
                DialogueChoice choice = dialogue.Choices[i];
                if (choice == null || !choice.IsAvailable(in context)) continue;
                choices.Add(new DialogueChoiceRow(i, choice.LabelKey, choice.QuestAction.ToString(), choice.QuestAction != DialogueQuestAction.None));
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
            public const float Inset = 42f;
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
            public const float CloseFont = 18f;
            /// <summary>닫기 버튼 기호(언어와 무관).</summary>
            public const string CloseGlyph = "X";
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

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "dialogue.root" };
            _root = root.Add(new DialogueRoot(this) { Id = PanelId });
            _lines = _root.Add(new UiTextBlock { Id = LinesId, FontSize = Metrics.LineFont, Color = Metrics.LineColor });
            _close = _root.Add(new UiButton { Id = CloseId, Text = Metrics.CloseGlyph, FontSize = Metrics.CloseFont });
            _close.Place(1f, 0f, -Metrics.CloseInset - Metrics.CloseSize, Metrics.CloseInset, Metrics.CloseSize, Metrics.CloseSize);
            _close.Clicked += Close;
            Arrange(Metrics.MinLineHeight);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            LastChoiceApplied = null;
            if (context.Interaction.HasValue && Services.Commands != null)
            {
                NpcId = context.Interaction.Value.TargetId;
                string npc = NpcId;
                Services.Commands.Run(s => s.TalkTo(npc), ChangeTopics.Dialogue);
            }
            Rebuild();
            if (Model.DialogueId == null) Close(); // 보여 줄 대화가 없다
        }

        protected internal override void OnClosed()
        {
            if (Services.Commands != null && Services.Session.CurrentDialogue != null)
                Services.Commands.Run(s => s.CloseDialogue(), ChangeTopics.Dialogue);
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
            if (visibleIndex < 0 || visibleIndex >= Model.Choices.Length || Services.Commands == null) return;
            DialogueChoiceRow row = Model.Choices[visibleIndex];
            bool applied = Services.Commands.Run(s => s.ChooseDialogueOption(row.Index), ChangeTopics.Quests);
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
            float height = Metrics.Inset * 2f + ChoicesHeight + Metrics.SectionGap + lineHeight;
            _root.LineHeight = lineHeight;
            _root.Place(0.5f, 1f, -Metrics.Width / 2f, -Metrics.BottomMargin - height, Metrics.Width, height);
            _lines.Place(0f, 0f, Metrics.Inset, Metrics.Inset, Metrics.Width - Metrics.Inset - Metrics.LineRightReserve, lineHeight);
            float top = Metrics.Inset + lineHeight + Metrics.SectionGap;
            for (int i = 0; i < _choiceButtons.Count; i++)
                _choiceButtons[i].Place(0f, 0f, Metrics.Inset, top + i * Metrics.ChoiceStep, Metrics.Width - Metrics.Inset * 2f, Metrics.ChoiceHeight);
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
