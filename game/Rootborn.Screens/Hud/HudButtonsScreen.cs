using System;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>버튼을 누르면: 화면을 연다(이미 열려 있으면 그대로) 또는 열고 닫는다.</summary>
    public enum HudButtonAction { Open, Toggle }

    /// <summary>
    /// HUD 버튼 하나: id, 문구 키(켜졌을 때 문구가 다르면 OnKey), 여는 화면 id, 누를 때 하는 일, 자리,
    /// 보이려면 지금 맵에 있어야 하는 성질(MapFeatures, 비면 모든 맵 — 맵 id에 기대지 않는다).
    /// 자리는 기준점(Anchor: 0 왼쪽·위, 0.5 가운데, 1 오른쪽·아래)에서 왼쪽 위 모서리까지의 거리와 크기(1920x1080 논리 좌표).
    /// </summary>
    public sealed record HudButtonSpec(string Id, string LabelKey, string OnKey, string ScreenId, HudButtonAction Action,
        float AnchorX, float AnchorY, float X, float Y, float W, float H, float FontSize, string RequiredMapFeature);

    /// <summary>
    /// HUD 버튼 줄. Unity 판에서 설치 코드마다 따로 만들던 버튼을 한 표로 모았다(같은 자리·크기·문구):
    /// 진로(CareerCandidateRuntimeInstaller), 장래희망(CareerInterestRuntimeInstaller), 마을 기록(WorldStateLogRuntimeInstaller),
    /// 도감(EncyclopediaRuntimeInstaller), 옷장(GrowingWardrobeRuntimeInstaller), 가구 배치 켬/끔(HousePlacementModeToggleInstaller, 가구를 놓는 면이 있는 맵에서만).
    /// 여는 화면이 아직 등록되지 않았으면 눌러도 아무 일도 없다(숨기지 않는다). 그 화면을 등록하면 저절로 이어진다.
    /// 장소 기록 버튼(LocationStateRuntimeInstaller, 오른쪽 위 168x36)은 Unity 판에서 도감 버튼 밑에 깔려 보이지 않았다. 사용자 결정(2026-11-12)으로
    /// 오른쪽 위 열의 옷장 아래(같은 크기 150x34, 간격 40)에 둔다.
    /// </summary>
    public sealed class HudButtonsScreen : GameScreen
    {
        /// <summary>
        /// 배치와 구성 — 새 디자인에서는 이 표만 고친다. 왼쪽 위 열: 가구 배치(집에서만) → 진로 → 장래희망 → 마을 기록.
        /// Unity 판은 가구 배치(가운데 기준 (-790, 480) 240x64 → 왼쪽 위 (50, 28))가 진로(24, 84) 위쪽 8을 가렸다. 가구 배치를 왼쪽 여백 24에 맞추고
        /// 아래 세 버튼을 16씩 내려 겹치지 않게 했다(버튼 간격은 Unity 그대로 68·80).
        /// </summary>
        public static readonly HudButtonSpec[] Buttons =
        {
            new HudButtonSpec("hud.button.careers", "ui.career-candidate.open", null, ScreenCatalog.CareerCandidates, HudButtonAction.Open, 0f, 0f, 24f, 104f, 240f, 56f, 20f, null),
            new HudButtonSpec("hud.button.dream", "ui.career-interest.open", null, ScreenCatalog.CareerInterest, HudButtonAction.Open, 0f, 0f, 24f, 172f, 240f, 56f, 20f, null),
            new HudButtonSpec("hud.button.world-log", "ui.world-log.open", null, ScreenCatalog.WorldStateLog, HudButtonAction.Open, 0f, 0f, 24f, 252f, 240f, 56f, 20f, null),
            new HudButtonSpec("hud.button.encyclopedia", "ui.encyclopedia.open", null, ScreenCatalog.Encyclopedia, HudButtonAction.Toggle, 1f, 0f, -24f - 150f, 86f, 150f, 34f, 18f, null),
            new HudButtonSpec("hud.button.wardrobe", "ui.wardrobe.open", null, ScreenCatalog.Wardrobe, HudButtonAction.Toggle, 1f, 0f, -24f - 150f, 126f, 150f, 34f, 18f, null),
            new HudButtonSpec("hud.button.placement", "ui.placement.off", "ui.placement.on", ScreenCatalog.FurniturePlacement, HudButtonAction.Toggle, 0f, 0f, 24f, 24f, 240f, 64f, 20f, MapFeatures.FurniturePlacement),
            new HudButtonSpec("hud.button.location-log", "ui.location-log", null, ScreenCatalog.LocationStateLog, HudButtonAction.Toggle, 1f, 0f, -24f - 150f, 166f, 150f, 34f, 18f, null),
        };

        private readonly UiButton[] _buttons = new UiButton[Buttons.Length];
        private bool _subscribed;

        public HudButtonsScreen(ScreenServices services) : base(services) { }

        /// <summary>마지막으로 누른 버튼이 연(또는 열려다 화면이 없어 아무것도 안 한) 화면 id. 시험용.</summary>
        public string LastRequested { get; private set; }

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "hud.buttons" };
            for (int i = 0; i < Buttons.Length; i++)
            {
                HudButtonSpec spec = Buttons[i];
                var button = root.Add(new UiButton { Id = spec.Id, FontSize = spec.FontSize, Focusable = false });
                button.Place(spec.AnchorX, spec.AnchorY, spec.X, spec.Y, spec.W, spec.H);
                button.Clicked += () => Press(spec);
                _buttons[i] = button;
            }
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (!_subscribed && Services.Screens != null)
            {
                Services.Screens.Opened += _ => Refresh();
                Services.Screens.Closed += _ => Refresh();
                _subscribed = true;
            }
            Refresh();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Map || topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        private void Press(HudButtonSpec spec)
        {
            LastRequested = spec.ScreenId;
            ScreenStack screens = Services.Screens;
            if (screens == null || !screens.Has(spec.ScreenId)) return; // 아직 옮기지 않은 화면
            if (spec.Action == HudButtonAction.Toggle) screens.Toggle(spec.ScreenId);
            else screens.Open(spec.ScreenId);
        }

        private void Refresh()
        {
            for (int i = 0; i < Buttons.Length; i++)
            {
                HudButtonSpec spec = Buttons[i];
                _buttons[i].Visible = spec.RequiredMapFeature == null || (Services.Screens != null && Services.Screens.HasMapFeature(spec.RequiredMapFeature));
                bool on = spec.OnKey != null && Services.Screens != null && Services.Screens.IsOpen(spec.ScreenId);
                _buttons[i].Text = T(on ? spec.OnKey : spec.LabelKey);
            }
        }
    }
}
