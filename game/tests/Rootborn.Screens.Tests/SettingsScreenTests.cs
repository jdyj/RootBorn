using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Screens.Settings;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    public sealed class SettingsScreenTests
    {
        private ScreenStack _stack;
        private GameText _text;

        [SetUp]
        public void Make()
        {
            _text = TestData.Text();
            _stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(_text, null));
            _stack.Layout(1920, 1080);
        }

        private void Click(string id)
        {
            UiRect r = _stack.Tree.Root.Find(id).Rect;
            _stack.Pointer(r.CenterX, r.CenterY, false, 0);
            _stack.Pointer(r.CenterX, r.CenterY, true, 0);
            _stack.Pointer(r.CenterX, r.CenterY, false, 0);
        }

        [Test]
        public void Escape_OpensA660x510PanelInTheMiddle()
        {
            _stack.Key(ScreenKey.Escape, false);
            Assert.That(_stack.IsOpen(ScreenCatalog.Settings), Is.True);
            Assert.That(_stack.Tree.Root.Find("settings").Rect, Is.EqualTo(new UiRect(630, 285, 660, 510)));
            Assert.That(_stack.MovementBlocked, Is.False);
            _stack.Key(ScreenKey.Escape, false);
            Assert.That(_stack.IsOpen(ScreenCatalog.Settings), Is.False);
        }

        [Test]
        public void Toggles_StartLikeUnity_AndFlipWhenClicked()
        {
            _stack.Key(ScreenKey.Escape, false);
            var screen = (SettingsScreen)_stack.Find(ScreenCatalog.Settings);
            Assert.That(Enumerable.Range(0, 4).Select(screen.Model.IsOn), Is.EqualTo(new[] { true, false, false, true }));

            Click(SettingsScreen.ToggleId(1));
            Assert.That(screen.Model.IsOn(1), Is.True);
            Assert.That(((UiToggle)_stack.Tree.Root.Find(SettingsScreen.ToggleId(1))).IsOn, Is.True);

            _stack.Key(ScreenKey.Escape, false);
            _stack.Key(ScreenKey.Escape, false);
            Assert.That(screen.Model.IsOn(1), Is.True, "닫았다 열어도 값이 남는다");
            Assert.That(((UiToggle)_stack.Tree.Root.Find(SettingsScreen.ToggleId(1))).IsOn, Is.True);
        }

        [Test]
        public void TogglePosition_MatchesUnity()
        {
            _stack.Key(ScreenKey.Escape, false);
            // Unity: 묶음 가운데 (0, 58) + (-38, 25) → 패널 가운데 (960, 540)에서 x -38, y 위로 83. 크기 78x36.
            UiRect r = _stack.Tree.Root.Find(SettingsScreen.ToggleId(0)).Rect;
            Assert.That(r, Is.EqualTo(new UiRect(960 - 38 - 39, 540 - 83 - 18, 78, 36)));
        }

        [Test]
        public void Tab_ThenEnter_FlipsAToggleFromTheKeyboard()
        {
            _stack.Key(ScreenKey.Escape, false);
            var screen = (SettingsScreen)_stack.Find(ScreenCatalog.Settings);
            _stack.Key(ScreenKey.Tab, false);
            _stack.Key(ScreenKey.Enter, false);
            Assert.That(screen.Model.IsOn(0), Is.False);
        }

        [Test]
        public void AllTextKeys_ExistInBothLanguages_AndNothingWasMissing()
        {
            _stack.Key(ScreenKey.Escape, false);
            GameScreen screen = _stack.Find(ScreenCatalog.Settings);
            var list = new DrawList();
            foreach (string language in new[] { "ko", "en" })
            {
                _text.Language = language;
                _stack.Emit(list, new TestMeasurer());
            }
            foreach (string key in screen.TextKeys)
            {
                Assert.That(_text.Has(key, "ko"), Is.True, "ko: " + key);
                Assert.That(_text.Has(key, "en"), Is.True, "en: " + key);
            }
            Assert.That(_text.MissingKeys, Is.Empty);
            Assert.That(list.Commands.Where(c => c.Kind == DrawKind.Text), Is.Empty, "Unity 판 설정 화면에는 글자가 없다");
        }
    }

    public sealed class SessionCommandsTests
    {
        private sealed class Listener : GameScreen
        {
            public string Last;
            public Listener(ScreenServices s) : base(s) { }
            protected override UiNode Build() => new UiNode();
            protected override void OnChanged(string topic) => Last = topic;
        }

        [Test]
        public void Run_CallsTheSessionThenTellsOpenScreens()
        {
            string saves = Path.Combine(Path.GetTempPath(), "rootborn-screens-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saves);
            try
            {
                LoadResult loaded = GameDataLoader.Load(TestData.DataDirectory, RootbornBuilders.Register);
                loaded.ThrowIfFailed();
                GameSession session = GameSession.Start(loaded.Definitions, GameStartSettings.FromRegistry(TestData.DataDirectory),
                    new FileSaveStore(saves), "slot-test", null);
                var commands = new SessionCommands(session);
                var registry = new ScreenRegistry();
                registry.Add("listener", ScreenLayer.Hud, false, s => new Listener(s));
                var stack = new ScreenStack(registry, new ScreenServices(TestData.Text(), commands));
                var listener = (Listener)stack.Open("listener");

                int remaining = commands.Run(s => s.TimePoints.Remaining, ChangeTopics.Student);
                Assert.That(remaining, Is.EqualTo(session.Student.TimePointsRemaining));
                Assert.That(listener.Last, Is.EqualTo(ChangeTopics.Student));
            }
            finally
            {
                try { Directory.Delete(saves, true); } catch (IOException) { }
            }
        }
    }

    public sealed class NoEngineReferenceTests
    {
        [Test]
        public void RootbornScreens_DoesNotReferenceMonoGame()
        {
            string[] references = typeof(ScreenStack).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(references.Where(n => n.Contains("MonoGame") || n.Contains("Xna") || n.Contains("FNA")), Is.Empty);
        }
    }
}
