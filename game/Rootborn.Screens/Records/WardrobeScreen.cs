using System;
using System.Collections.Generic;
using Rootborn.Core.Characters;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Records
{
    /// <summary>미리보기 그림 한 장: PNG 경로(그림 id)와 그 안의 칸. 몸 그림 다음에 옷 레이어가 그리는 순서로 온다.</summary>
    public readonly record struct WardrobeSprite(string Image, PixelRect Source);

    /// <summary>
    /// 옷장이 부르는 캐릭터 쪽. 어느 옷을 입는지와 저장, 월드 캐릭터에 바로 입히기, 미리보기 그림은 엔진·월드 그림과 이어지는 일이라
    /// Host가 구현한다(화면은 월드 프로젝트를 모른다). 규칙(무엇을 고를 수 있는지, 같은 칸의 다른 옷을 벗기는 것)은 Core WearableRules가 정한다.
    /// </summary>
    public interface IWardrobeBackend
    {
        /// <summary>칸·옷·성장 단계 규칙.</summary>
        WearableCatalog Catalog { get; }

        /// <summary>지금 캐릭터의 성장 단계 id.</summary>
        string StageId { get; }

        /// <summary>지금 입고 있는 옷 id들(고르지 않은 칸의 기본 옷 포함).</summary>
        IReadOnlyCollection<string> WornIds { get; }

        /// <summary>그 옷을 입었을 때의 모습(몸 + 옷 레이어). 입히지는 않는다.</summary>
        IReadOnlyList<WardrobeSprite> Preview(string wearableId);

        /// <summary>그 옷을 입고 저장한다. 입을 수 없으면 false(아무것도 바뀌지 않는다).</summary>
        bool Choose(string wearableId);
    }

    /// <summary>옷 카드 하나: 옷 id, 이름 문구 키, 입고 있는지, 미리보기 그림.</summary>
    public readonly record struct WardrobeCardModel(string WearableId, string LabelKey, bool Selected, IReadOnlyList<WardrobeSprite> Sprites);

    /// <summary>한 칸(머리, 옷 …)의 줄: 칸 이름 키와 카드들.</summary>
    public sealed class WardrobeSectionModel
    {
        public WardrobeSectionModel(string titleKey) { TitleKey = titleKey ?? string.Empty; }
        public string TitleKey { get; }
        public List<WardrobeCardModel> Cards { get; } = new List<WardrobeCardModel>();
    }

    /// <summary>
    /// 옷장의 표시 모델(Unity GrowingCharacterStageDriver.WardrobeSections와 같은 규칙): 지금 단계에서 고를 수 있는 옷이 둘 이상인 칸마다 한 줄,
    /// 그런 칸이 하나도 없으면(첫 단계) 하나뿐인 칸도 보여서 옷장이 비지 않게 한다. 카드 순서는 데이터 등록 순서.
    /// </summary>
    public static class WardrobeModel
    {
        public static List<WardrobeSectionModel> From(GameSession session, DisplayNames names, IWardrobeBackend backend)
        {
            var sections = new List<WardrobeSectionModel>();
            if (backend == null || session == null) return sections;
            WearableCatalog catalog = backend.Catalog;
            string stage = backend.StageId;
            int minimumChoices = 1;
            foreach (WearableSlotRule slot in catalog.Slots)
                if (catalog.Choices(slot.Id, stage).Count >= 2) minimumChoices = 2;
            var worn = new HashSet<string>(backend.WornIds, StringComparer.Ordinal);
            IReadOnlyList<WearableRule> registryOrder = session.Definitions.All<WearableRule>();
            foreach (WearableSlotRule slot in catalog.Slots)
            {
                var choices = new HashSet<string>(StringComparer.Ordinal);
                foreach (WearableRule choice in catalog.Choices(slot.Id, stage)) choices.Add(choice.Id);
                if (choices.Count < minimumChoices) continue;
                string title = names.Get("WearableSlotDefinition", slot.Id);
                var section = new WardrobeSectionModel(string.IsNullOrEmpty(title) ? slot.Id : title);
                foreach (WearableRule wearable in registryOrder)
                {
                    if (wearable == null || !choices.Contains(wearable.Id)) continue;
                    string label = names.Get("WearableDefinition", wearable.Id);
                    section.Cards.Add(new WardrobeCardModel(wearable.Id, string.IsNullOrEmpty(label) ? wearable.Id : label, worn.Contains(wearable.Id), backend.Preview(wearable.Id)));
                }
                sections.Add(section);
            }
            return sections;
        }
    }

    /// <summary>
    /// 옷장(HUD 버튼). Unity GrowingWardrobePanel을 같은 수치로 옮겼다: 가운데 패널(폭 1040, 높이는 줄 수에 맞춰 최소 420), 위 가운데 제목과
    /// 오른쪽 위 닫기, 칸마다 이름 줄 아래에 카드(170x230, 간격 16)를 가운데로 늘어놓는다. 카드 = 미리보기 그림(128x180 안에 비율 유지) + 옷 이름.
    /// 입고 있는 카드는 금색 바탕. 카드를 누르면 그 옷을 입고 저장하고(IWardrobeBackend) 옷장을 다시 그린다. 카드와 줄 제목은 한 번 만들어 다시 쓴다.
    /// 이동을 막는다(Unity: UiGameplayInputLock). 닫기는 × 버튼, HUD 옷장 버튼, Esc(Unity 판에는 없던 키).
    /// </summary>
    public sealed class WardrobeScreen : GameScreen
    {
        /// <summary>배치 — 새 디자인에서는 여기만 고친다. 가운데 기준(y는 위로 +)이거나 패널 위 기준.</summary>
        internal static class Metrics
        {
            public const float PanelW = 1040f, PanelMinH = 420f, PanelChrome = 120f;
            public const float CardW = 170f, CardH = 230f, CardGap = 16f;
            public const float RowTitleH = 32f, RowTitleW = 400f, RowTitleFont = 20f, RowGap = 20f;
            /// <summary>카드들이 놓이는 곳의 가운데에서 아래로 내린 거리.</summary>
            public const float CardsOffsetY = -24f;
            public const float TitleFont = 28f, TitleW = 400f, TitleH = 44f, TitleCenterFromTop = 36f;
            public const float CloseSize = 48f, CloseFont = 28f, CloseFromRight = 40f, CloseFromTop = 36f;
            public const float PreviewW = 128f, PreviewH = 180f, PreviewLift = 16f;
            public const float LabelFont = 18f, LabelW = 162f, LabelH = 36f, LabelDrop = -94f;
            public const string CloseGlyph = "×";
            public static readonly UiColor Selected = UnityLayout.Color(0.95f, 0.72f, 0.28f);
            public static readonly UiColor Normal = UnityLayout.Color(0.93f, 0.87f, 0.74f);
            public static readonly UiColor Ink = UnityLayout.Color(0.28f, 0.19f, 0.12f);
        }

        public const string PanelId = "wardrobe";
        public const string TitleId = "wardrobe.title";
        public const string CloseId = "wardrobe.close";
        public static string CardId(int index) => "wardrobe.card." + index;
        public static string RowTitleId(int index) => "wardrobe.row." + index;

        private UiPanel _panel;
        private UiLabel _title;
        private readonly List<CardNode> _cards = new List<CardNode>();
        private readonly List<UiLabel> _rowTitles = new List<UiLabel>();

        public WardrobeScreen(ScreenServices services) : base(services) { }

        public IReadOnlyList<WardrobeSectionModel> Sections { get; private set; } = Array.Empty<WardrobeSectionModel>();

        /// <summary>지금 보이는 카드 수.</summary>
        public int VisibleCardCount { get; private set; }

        /// <summary>옷 id로 카드 노드 id를 찾는다(시험용).</summary>
        public string CardIdOf(string wearableId)
        {
            for (int i = 0; i < VisibleCardCount; i++) if (_cards[i].WearableId == wearableId) return CardId(i);
            return null;
        }

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "wardrobe.root" };
            _panel = root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel });
            _panel.Centered(0f, 0f, Metrics.PanelW, Metrics.PanelMinH);
            _title = _panel.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Align = UiAlign.Center, Color = Metrics.Ink });
            _title.Place(0.5f, 0f, -Metrics.TitleW / 2f, Metrics.TitleCenterFromTop - Metrics.TitleH / 2f, Metrics.TitleW, Metrics.TitleH);
            var close = _panel.Add(new UiButton { Id = CloseId, Text = Metrics.CloseGlyph, FontSize = Metrics.CloseFont });
            close.Place(1f, 0f, -Metrics.CloseFromRight - Metrics.CloseSize / 2f, Metrics.CloseFromTop - Metrics.CloseSize / 2f, Metrics.CloseSize, Metrics.CloseSize);
            close.Clicked += Close;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context) => Refresh();

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

        private void Choose(string wearableId)
        {
            if (Services.Wardrobe == null || !Services.Wardrobe.Choose(wearableId)) return;
            Refresh();
        }

        /// <summary>표시 모델을 다시 받아 카드와 줄 제목의 값과 자리를 고친다. 새로 만드는 것은 처음 필요해진 만큼뿐이다.</summary>
        private void Refresh()
        {
            _ = Root;
            _title.Text = T("ui.wardrobe.title");
            Sections = WardrobeModel.From(Services.Session, Services.Names, Services.Wardrobe);
            int rows = Sections.Count;
            float rowHeight = Metrics.CardH + Metrics.RowTitleH + Metrics.RowGap;
            float height = Math.Max(Metrics.PanelMinH, Metrics.PanelChrome + rows * rowHeight);
            _panel.Centered(0f, 0f, Metrics.PanelW, height);
            float top = rows * rowHeight / 2f;
            int used = 0;
            for (int row = 0; row < rows; row++)
            {
                WardrobeSectionModel section = Sections[row];
                float rowCenter = top - row * rowHeight - Metrics.RowTitleH - Metrics.CardH / 2f;
                while (_rowTitles.Count <= row)
                    _rowTitles.Add(_panel.Add(new UiLabel { Id = RowTitleId(_rowTitles.Count), FontSize = Metrics.RowTitleFont, Align = UiAlign.Center, Color = Metrics.Ink }));
                UiLabel title = _rowTitles[row];
                title.Visible = true;
                title.Text = Shown(section.TitleKey);
                title.Centered(0f, rowCenter + Metrics.CardH / 2f + Metrics.RowTitleH / 2f + Metrics.CardsOffsetY, Metrics.RowTitleW, Metrics.RowTitleH);
                int count = section.Cards.Count;
                float start = -(count * Metrics.CardW + Math.Max(0, count - 1) * Metrics.CardGap) / 2f;
                for (int i = 0; i < count; i++)
                {
                    while (_cards.Count <= used) _cards.Add(MakeCard(_cards.Count));
                    CardNode card = _cards[used++];
                    card.Visible = true;
                    card.Fill(section.Cards[i], this);
                    card.Centered(start + i * (Metrics.CardW + Metrics.CardGap) + Metrics.CardW / 2f, rowCenter + Metrics.CardsOffsetY, Metrics.CardW, Metrics.CardH);
                }
            }
            for (int i = used; i < _cards.Count; i++) _cards[i].Visible = false;
            for (int i = rows; i < _rowTitles.Count; i++) _rowTitles[i].Visible = false;
            VisibleCardCount = used;
            if (Root.Parent != null && Root.Parent.Rect.W > 0) Root.Layout(Root.Parent.Rect);
        }

        private CardNode MakeCard(int index)
        {
            var card = _panel.Add(new CardNode { Id = CardId(index) });
            card.Clicked += () => Choose(card.WearableId);
            return card;
        }

        /// <summary>카드 하나: 바탕, 미리보기 그림 레이어들, 옷 이름. 그림 레이어와 이름 글자는 다시 쓴다.</summary>
        private sealed class CardNode : UiNode
        {
            private readonly List<UiImage> _layers = new List<UiImage>(4);
            private readonly UiLabel _label = new UiLabel { FontSize = Metrics.LabelFont, Align = UiAlign.Center, Color = Metrics.Ink };
            private UiColor _color = Metrics.Normal;

            public CardNode() { Interactive = true; Focusable = true; Add(_label); _label.Centered(0f, Metrics.LabelDrop, Metrics.LabelW, Metrics.LabelH); }

            public string WearableId { get; private set; }

            public void Fill(WardrobeCardModel model, WardrobeScreen owner)
            {
                WearableId = model.WearableId;
                _color = model.Selected ? Metrics.Selected : Metrics.Normal;
                _label.Text = owner.Shown(model.LabelKey);
                for (int i = 0; i < model.Sprites.Count; i++)
                {
                    while (_layers.Count <= i)
                    {
                        var image = Add(new UiImage());
                        Children.Remove(_label);
                        Children.Add(_label); // 이름 글자는 그림 위에 그린다
                        _layers.Add(image);
                    }
                    WardrobeSprite sprite = model.Sprites[i];
                    UiImage layer = _layers[i];
                    layer.Visible = true;
                    layer.Image = sprite.Image;
                    layer.Source = sprite.Source;
                    FitInto(layer, sprite.Source);
                }
                for (int i = model.Sprites.Count; i < _layers.Count; i++) _layers[i].Visible = false;
            }

            /// <summary>128x180 칸 안에 비율을 지켜 꽉 채운다(Unity Image.preserveAspect).</summary>
            private static void FitInto(UiImage image, PixelRect source)
            {
                if (source.W <= 0 || source.H <= 0) { image.Centered(0f, Metrics.PreviewLift, Metrics.PreviewW, Metrics.PreviewH); return; }
                float scale = Math.Min(Metrics.PreviewW / source.W, Metrics.PreviewH / source.H);
                image.Centered(0f, Metrics.PreviewLift, source.W * scale, source.H * scale);
            }

            public override void Emit(DrawList list, ITextMeasurer text)
            {
                list.Fill(Rect, IsActive && Hovered ? new UiColor((byte)Math.Min(255, _color.R + 12), (byte)Math.Min(255, _color.G + 12), (byte)Math.Min(255, _color.B + 12)) : _color);
                if (Focused) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
            }
        }
    }
}
