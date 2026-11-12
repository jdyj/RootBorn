using System.Linq;
using NUnit.Framework;
using Rootborn.Screens.KeyGuide;
using Rootborn.Screens.Tests.Records;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// HUD 단추로만 열리던 화면 여섯의 키(2026-11-13 임시 배정 — 장치 키는 Host Input/Devices.cs). 키를 누르면 열리고 다시 누르면 닫힌다
    /// (가방 I·상태 C와 같다). 가구 배치는 HUD 단추처럼 가구를 놓는 면이 있는 맵에서만 열린다. 창 없이 — 자동 테스트.
    /// </summary>
    public sealed class ScreenOpenerKeyTests
    {
        private static readonly (ScreenKey Key, string Screen)[] Openers =
        {
            (ScreenKey.Encyclopedia, ScreenCatalog.Encyclopedia),
            (ScreenKey.Wardrobe, ScreenCatalog.Wardrobe),
            (ScreenKey.WorldLog, ScreenCatalog.WorldStateLog),
            (ScreenKey.Careers, ScreenCatalog.CareerCandidates),
            (ScreenKey.Dream, ScreenCatalog.CareerInterest),
            (ScreenKey.Placement, ScreenCatalog.FurniturePlacement),
        };

        private static TestSession InHouse()
        {
            var t = new TestSession();
            t.Services.Wardrobe = new TestWardrobe(t.Session);
            t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            t.Stack.OpenHud();
            return t;
        }

        [Test]
        public void EachKey_OpensItsScreen_AndPressingAgainClosesIt()
        {
            foreach ((ScreenKey key, string screen) in Openers)
            {
                using TestSession t = InHouse();
                Assert.That(t.Stack.Registry.TryGetByKey(key, out string bound) && bound == screen, Is.True, key + " → " + screen + " 배정");
                Assert.That(t.Stack.Key(key, false), Is.True, key + " 키를 화면 쌓기가 받는다");
                Assert.That(t.Stack.IsOpen(screen), Is.True, key + " → " + screen + " 열림");
                t.Stack.Key(key, !t.Stack.Find(screen).BlocksMovement);
                Assert.That(t.Stack.IsOpen(screen), Is.False, key + " 다시 → " + screen + " 닫힘");
            }
        }

        [Test]
        public void PlacementKey_DoesNothing_WhereTheMapHasNoPlacementSurface_LikeTheHudButton()
        {
            using var t = new TestSession();
            t.Stack.EnterMap("Town", System.Array.Empty<string>());
            t.Stack.OpenHud();
            Assert.That(t.Stack.Find(ScreenCatalog.HudButtons).Root.Find("hud.button.placement").Visible, Is.False, "HUD 단추도 숨어 있다");
            t.Stack.Key(ScreenKey.Placement, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.FurniturePlacement), Is.False);

            t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            Assert.That(t.Stack.Find(ScreenCatalog.HudButtons).Root.Find("hud.button.placement").Visible, Is.True);
            t.Stack.Key(ScreenKey.Placement, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.FurniturePlacement), Is.True, "놓는 면이 있는 맵에서는 열린다");
        }

        [Test]
        public void OpenerKeys_DoNotOpen_OverAScreenThatBlocksMovement()
        {
            using TestSession t = InHouse();
            t.Stack.Open(ScreenCatalog.CareerCandidates);
            t.Stack.Key(ScreenKey.Encyclopedia, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Encyclopedia), Is.False, "이동을 막는 화면 위에서는 다른 화면의 키가 듣지 않는다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.CareerCandidates), Is.True);
        }

        [Test]
        public void EachOpener_HasAKeyGuideLine()
        {
            KeyGuideModel model = KeyGuideModel.From(ScreenCatalog.Create(), new KeyGuideKeys());
            foreach ((ScreenKey _, string screen) in Openers)
                Assert.That(model.Rows.Any(r => r.ActionKey == KeyGuideModel.ScreenActionKey(screen)), Is.True, screen + " 안내 줄");
        }
    }
}
