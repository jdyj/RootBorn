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
        public void Buttons_SitWhereUnityPutThem_AndThePlacementToggleOnlyAtHome()
        {
            using var t = new TestSession();
            t.Stack.EnterMap("Town");
            t.Stack.OpenHud();
            Assert.That(t.Node("hud.button.careers").Rect, Is.EqualTo(new UiRect(24, 84, 240, 56)));
            Assert.That(t.Node("hud.button.dream").Rect, Is.EqualTo(new UiRect(24, 152, 240, 56)));
            Assert.That(t.Node("hud.button.world-log").Rect, Is.EqualTo(new UiRect(24, 232, 240, 56)));
            Assert.That(t.Node("hud.button.encyclopedia").Rect, Is.EqualTo(new UiRect(1920 - 24 - 150, 86, 150, 34)));
            Assert.That(t.Node("hud.button.wardrobe").Rect, Is.EqualTo(new UiRect(1920 - 24 - 150, 126, 150, 34)));
            Assert.That(t.Node("hud.button.placement").Visible, Is.False, "가구 배치 토글은 집에서만");

            t.Stack.EnterMap("House");
            Assert.That(t.Node("hud.button.placement").Visible, Is.True);
            Assert.That(t.Node("hud.button.placement").Rect, Is.EqualTo(new UiRect(960 - 790 - 120, 540 - 480 - 32, 240, 64)));
            Assert.That(((UiButton)t.Node("hud.button.placement")).Text, Is.EqualTo("가구 배치 꺼짐"));
        }

        [Test]
        public void ButtonForAScreenNotPortedYet_DoesNothing()
        {
            using var t = new TestSession();
            t.Stack.OpenHud();
            t.Click("hud.button.encyclopedia");
            var hud = t.Screen<HudButtonsScreen>(ScreenCatalog.HudButtons);
            Assert.That(hud.LastRequested, Is.EqualTo(ScreenCatalog.Encyclopedia));
            Assert.That(t.Stack.OpenScreens.All(s => s.Layer == ScreenLayer.Hud), Is.True, "아무 화면도 열리지 않는다");
            Assert.That(t.Node("hud.button.encyclopedia").Visible, Is.True, "숨기지 않는다");
        }

        [Test]
        public void OnceTheScreenIsRegistered_TheButtonOpensIt_ByScreenId()
        {
            using var t = new TestSession(extra: r =>
            {
                r.Add(ScreenCatalog.Encyclopedia, ScreenLayer.Panel, true, s => new Probe(s));
            });
            t.Stack.EnterMap("House");
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
