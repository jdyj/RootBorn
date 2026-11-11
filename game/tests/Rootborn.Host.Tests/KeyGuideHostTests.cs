using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Input;
using NUnit.Framework;
using Rootborn.Host.Input;
using Rootborn.Screens;
using Rootborn.Screens.KeyGuide;
using Rootborn.Screens.Settings;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 단축키 안내를 실제 창과 게임 루프로: 게임 중 F1로 열고 Esc·F1로 닫기, 설정(Esc)의 단축키 단추, 안내에 나온 화면 키를 이름표대로
    /// 눌러 그 화면이 열리는지, 시작 화면에서도 F1. 조작은 키·포인터뿐(장치 이벤트 주입, OS 입력 계층 미확인).
    /// 스크린샷: output/host-screens-evidence/key-guide-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class KeyGuideHostTests
    {
        private string _saves;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-key-guide-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Launch(bool inGame, int width = 1280, int height = 720)
        {
            var args = inGame
                ? new[] { "--save-root", _saves, "--slot", "slot-1", "--size", width + "x" + height }
                : new[] { "--save-root", _saves, "--size", width + "x" + height };
            HostOptions options = HostOptions.Resolve(args);
            options.TestMode = true;
            options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-screens-evidence");
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        /// <summary>이름표의 장치 키 → 테스트가 넣을 수 있는 키.</summary>
        private static TestKey ToTestKey(Keys key)
        {
            switch (key)
            {
                case Keys.Escape: return TestKey.Escape;
                case Keys.Tab: return TestKey.Tab;
                case Keys.I: return TestKey.I;
                case Keys.C: return TestKey.C;
                case Keys.F1: return TestKey.F1;
                case Keys.Enter: return TestKey.Enter;
                case Keys.E: return TestKey.E;
                case Keys.Space: return TestKey.Space;
                case Keys.R: return TestKey.R;
                case Keys.A: return TestKey.A;
                case Keys.D: return TestKey.D;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "PlayerDriver에 이 키를 더한다");
            }
        }

        [Test]
        public void KEY_GUIDE_001_F1InGame_OpensTheGuide_EveryListedScreenKeyOpensItsScreen_SettingsButtonToo()
        {
            using PlayerDriver player = Launch(inGame: true);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.HudClock), Is.True, "게임 중(HUD가 떠 있다)");

            player.OpenScreenWithKey(TestKey.F1, ScreenCatalog.KeyGuide);
            var guide = (KeyGuideScreen)player.Screens.Find(ScreenCatalog.KeyGuide);
            Assert.That(player.Screens.MovementBlocked, Is.True);
            player.Frames(2);
            string shot = player.ScreenshotScreen(ScreenCatalog.KeyGuide, "key-guide-01-ingame-1280x720");
            TestContext.WriteLine("스크린샷: " + shot);

            // 줄은 등록 표와 Host 이름표에서: 화면 줄 수 = 키 배정 수, 걷기·상호작용·전체 화면은 이름표의 줄.
            var bindings = player.Screens.Registry.KeyBindings;
            Assert.That(guide.Model.Rows.Count(r => r.Group == KeyGuideGroups.Screens), Is.EqualTo(bindings.Count));
            Assert.That(guide.Model.Rows.Select(r => r.ActionKey), Does.Contain("ui.keys.move").And.Contain("ui.keys.interact").And.Contain("ui.keys.fullscreen"));
            Assert.That(guide.Model.Rows.Single(r => r.ActionKey == "ui.keys.interact").Alternatives.Select(a => a.ToString()), Is.EqualTo(new[] { "E", "Space", "Ctrl" }));
            foreach (string language in player.Game.Text.Languages)
                Assert.That(guide.TextKeys.Where(k => !player.Game.Text.Has(k, language)), Is.Empty, language + "에 없는 문구 키");

            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.KeyGuide);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Settings), Is.False, "닫는 Esc가 설정을 열지 않는다");

            // 안내에 나온 키를 그대로 눌러 본다(이름표가 실제 배정과 같은지).
            foreach (var binding in bindings)
            {
                Assert.That(KeyGuideNames.ScreenKeys.Any(k => k.Key == binding.Key), Is.True, binding.Key + "의 장치 이름이 Host 이름표(KeyGuideNames)에 있다");
                Keys device = KeyGuideNames.ScreenKeys.First(k => k.Key == binding.Key).Device[0];
                TestKey key = ToTestKey(device);
                player.OpenScreenWithKey(key, binding.Value);
                player.CloseScreenWithKey(key, binding.Value);
            }

            // 설정 화면의 단축키 단추.
            player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            player.Frames(2);
            TestContext.WriteLine("스크린샷: " + player.ScreenshotScreen(ScreenCatalog.Settings, "key-guide-02a-settings-button"));
            player.PressButton(SettingsScreen.KeyGuideButtonId);
            player.WaitForScreen(ScreenCatalog.KeyGuide);
            Assert.That(player.Screens.Top.Id, Is.EqualTo(ScreenCatalog.KeyGuide));
            player.Frames(2);
            player.Screenshot("key-guide-02-over-settings");
            player.PressButton(KeyGuideScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.KeyGuide);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Settings), Is.True, "닫기 단추는 안내만 닫는다");
            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
        }

        [Test]
        public void KEY_GUIDE_002_F1OnTheStartScreen_OpensTheGuide_EscapeBackToSlots()
        {
            using PlayerDriver player = Launch(inGame: false);
            Assert.That(player.Game.World, Is.Null, "시작 화면");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
            player.OpenScreenWithKey(TestKey.F1, ScreenCatalog.KeyGuide);
            player.Frames(2);
            string shot = player.ScreenshotScreen(ScreenCatalog.KeyGuide, "key-guide-03-start-1280x720");
            TestContext.WriteLine("스크린샷: " + shot);
            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.KeyGuide);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "슬롯 선택으로 돌아온다");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Settings), Is.False);
        }

        [Test]
        public void KEY_GUIDE_003_InEnglishAt1920x1080()
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080", "--lang", "en" });
            options.TestMode = true;
            options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-screens-evidence");
            using var player = new PlayerDriver(options);
            player.Frames(3);
            player.OpenScreenWithKey(TestKey.F1, ScreenCatalog.KeyGuide);
            player.Frames(2);
            TestContext.WriteLine("스크린샷: " + player.ScreenshotScreen(ScreenCatalog.KeyGuide, "key-guide-04-english-1920x1080"));
            var guide = (KeyGuideScreen)player.Screens.Find(ScreenCatalog.KeyGuide);
            Assert.That(guide.Lines.First().Label, Is.EqualTo("Movement"));
            player.CloseScreenWithKey(TestKey.F1, ScreenCatalog.KeyGuide);
        }
    }
}
