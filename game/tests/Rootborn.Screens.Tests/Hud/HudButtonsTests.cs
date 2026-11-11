using System.Linq;
using NUnit.Framework;
using Rootborn.Screens.Hud;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Hud
{
    public sealed class HudButtonsTests
    {
        private sealed class Probe : GameScreen
        {
            public Probe(ScreenServices s) : base(s) { }
            protected override UiNode Build() => new UiPanel { Id = "probe" }.Centered(0, 0, 200, 100);
        }

        [Test]
        public void Buttons_SitInTheirLayoutTable_WithoutOverlapping_AndThePlacementToggleOnlyAtHome()
        {
            using var t = new TestSession();
            t.Stack.EnterMap("Town");
            t.Stack.OpenHud();
            Assert.That(t.Node("hud.button.careers").Rect, Is.EqualTo(new UiRect(24, 104, 240, 56)));
            Assert.That(t.Node("hud.button.dream").Rect, Is.EqualTo(new UiRect(24, 172, 240, 56)));
            Assert.That(t.Node("hud.button.world-log").Rect, Is.EqualTo(new UiRect(24, 252, 240, 56)));
            Assert.That(t.Node("hud.button.encyclopedia").Rect, Is.EqualTo(new UiRect(1920 - 24 - 150, 86, 150, 34)));
            Assert.That(t.Node("hud.button.wardrobe").Rect, Is.EqualTo(new UiRect(1920 - 24 - 150, 126, 150, 34)));
            Assert.That(t.Node("hud.button.placement").Visible, Is.False, "가구 배치 토글은 집에서만");

            t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            Assert.That(t.Node("hud.button.placement").Visible, Is.True);
            // Unity 판 자리(50, 28)는 진로 버튼 위쪽 8을 가렸다. 왼쪽 여백 24에 맞추고 진로 버튼과 16 띄운다.
            Assert.That(t.Node("hud.button.placement").Rect, Is.EqualTo(new UiRect(24, 24, 240, 64)));
            Assert.That(t.Node("hud.button.placement").Rect.Bottom + 16, Is.EqualTo(t.Node("hud.button.careers").Rect.Y));
            Assert.That(((UiButton)t.Node("hud.button.placement")).Text, Is.EqualTo("가구 배치 꺼짐"));
        }

        [Test]
        public void PlacementToggle_FollowsTheMapsFeature_NotItsName()
        {
            using var t = new TestSession();
            t.Stack.EnterMap("House");
            t.Stack.OpenHud();
            Assert.That(t.Node("hud.button.placement").Visible, Is.False, "이름이 House여도 가구 놓는 면이 없으면 숨는다");
            t.Stack.EnterMap("any-new-room", new[] { MapFeatures.FurniturePlacement });
            Assert.That(t.Node("hud.button.placement").Visible, Is.True, "새로 그린 맵도 가구 놓는 면이 있으면 보인다");
            Assert.That(t.Stack.HasMapFeature(MapFeatures.FurniturePlacement), Is.True);
            t.Stack.EnterMap("any-new-room");
            Assert.That(t.Node("hud.button.placement").Visible, Is.False, "같은 맵 id라도 성질이 바뀌면 다시 본다");
        }

        [Test]
        public void ButtonForAScreenNotPortedYet_DoesNothing()
        {
            // 화면이 하나씩 옮겨져도 이 시험이 흔들리지 않게, 버튼 화면만 든 등록 표로 쌓는다(여는 화면이 하나도 등록돼 있지 않다).
            using var t = new TestSession();
            var registry = new ScreenRegistry();
            registry.Add(ScreenCatalog.HudButtons, ScreenLayer.Hud, false, s => new HudButtonsScreen(s));
            var stack = new ScreenStack(registry, t.Services);
            stack.Layout(1920, 1080);
            stack.OpenHud();
            UiNode button = stack.Tree.Root.Find("hud.button.encyclopedia");
            UiRect r = button.Rect;
            stack.Pointer(r.CenterX, r.CenterY, false, 0);
            stack.Pointer(r.CenterX, r.CenterY, true, 0);
            stack.Pointer(r.CenterX, r.CenterY, false, 0);
            var hud = (HudButtonsScreen)stack.Find(ScreenCatalog.HudButtons);
            Assert.That(hud.LastRequested, Is.EqualTo(ScreenCatalog.Encyclopedia));
            Assert.That(stack.OpenScreens.All(s => s.Layer == ScreenLayer.Hud), Is.True, "아무 화면도 열리지 않는다");
            Assert.That(button.Visible, Is.True, "숨기지 않는다");
        }

        [Test]
        public void OnceTheScreenIsRegistered_TheButtonOpensIt_ByScreenId()
        {
            using var t = new TestSession(extra: r =>
            {
                // 이미 옮겨진 화면은 그 화면이 열리는 것으로 본다. 아직 없는 화면만 시험용 화면을 놓는다.
                if (!r.Screens.Any(e => e.Id == ScreenCatalog.Encyclopedia)) r.Add(ScreenCatalog.Encyclopedia, ScreenLayer.Panel, true, s => new Probe(s));
                if (!r.Screens.Any(e => e.Id == ScreenCatalog.FurniturePlacement)) r.Add(ScreenCatalog.FurniturePlacement, ScreenLayer.Panel, false, s => new Probe(s));
            });
            t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            t.Stack.OpenHud();
            t.Click("hud.button.encyclopedia");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Encyclopedia), Is.True);
            t.Stack.Close(ScreenCatalog.Encyclopedia);

            t.Click("hud.button.placement");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.FurniturePlacement), Is.True);
            Assert.That(((UiButton)t.Node("hud.button.placement")).Text, Is.EqualTo("가구 배치 켜짐"));
        }

        [Test]
        public void Labels_ExistOrAreKnownMissing()
        {
            using var t = new TestSession();
            t.Stack.EnterMap("House");
            t.Stack.OpenHud();
            t.Emit();
            t.Text.Language = "en";
            t.Emit();
            Assert.That(((UiButton)t.Node("hud.button.world-log")).Text, Is.EqualTo(t.Text.Get("ui.world-log.open")));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.Stack.Find(ScreenCatalog.HudButtons).TextKeys);
        }
    }
}
