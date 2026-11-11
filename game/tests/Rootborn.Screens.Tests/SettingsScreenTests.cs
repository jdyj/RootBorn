using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Core.Settings;
using Rootborn.Screens.Settings;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    public sealed class SettingsScreenTests
    {
        private ScreenStack _stack;
        private GameText _text;
        private string _saves;
        private FileSaveStore _store;

        [SetUp]
        public void Make()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _store = new FileSaveStore(_saves);
            _text = TestData.Text();
            _stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(_text, null) { Settings = new SettingsStore(_store) });
            _stack.Layout(1920, 1080);
        }

        [TearDown]
        public void Clean()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
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
            Assert.That(new SettingsStore(_store).Settings.GetToggle(GameSettingNames.Muted), Is.True, "Core 설정 파일에 저장된다");
        }

        [Test]
        public void SavedSettings_AreWhatTheTogglesShow()
        {
            var saved = new SettingsStore(_store);
            saved.Settings.SetToggle(GameSettingNames.Speaker, false);
            saved.Settings.SetToggle(GameSettingNames.Music, true);
            saved.Save();
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(_text, null) { Settings = new SettingsStore(_store) });
            stack.Layout(1920, 1080);
            stack.Key(ScreenKey.Escape, false);
            var screen = (SettingsScreen)stack.Find(ScreenCatalog.Settings);
            Assert.That(Enumerable.Range(0, 4).Select(screen.Model.IsOn), Is.EqualTo(new[] { false, false, true, true }));
            Assert.That(((UiToggle)stack.Tree.Root.Find(SettingsScreen.ToggleId(0))).IsOn, Is.False);
        }

        [Test]
        public void LanguageRow_CyclesThroughTheLanguageFiles_ChangesOpenTextAtOnce_AndSaves()
        {
            _stack.Key(ScreenKey.Escape, false);
            var label = (UiLabel)_stack.Tree.Root.Find(SettingsScreen.LanguageLabelId);
            var value = (UiButton)_stack.Tree.Root.Find(SettingsScreen.LanguageButtonId);
            Assert.That(_text.Language, Is.EqualTo("ko"));
            Assert.That(label.Text, Is.EqualTo(_text.Get("ui.settings.language")));
            Assert.That(value.Text, Is.EqualTo(_text.Get("ui.settings.language-name")));
            string koLabel = label.Text;

            var order = _text.Languages.OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList();
            string next = order[(order.IndexOf("ko") + 1) % order.Count];
            Click(SettingsScreen.LanguageButtonId);
            Assert.That(_text.Language, Is.EqualTo(next), "언어 목록은 언어 파일 폴더에서 온다(사전 순으로 다음)");
            Assert.That(label.Text, Is.Not.EqualTo(koLabel), "열린 화면 글자가 바로 바뀐다");
            Assert.That(label.Text, Is.EqualTo(_text.Get("ui.settings.language")));
            Assert.That(new SettingsStore(_store).Settings.Language, Is.EqualTo(next), "고른 언어는 설정 파일에 저장된다");

            for (int i = 1; i < order.Count; i++) Click(SettingsScreen.LanguageButtonId);
            Assert.That(_text.Language, Is.EqualTo("ko"), "끝에서 처음으로 돈다");
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
        public void ShiftArrow_ThenEnter_FlipsAToggleFromTheKeyboard()
        {
            _stack.Key(ScreenKey.Escape, false);
            var screen = (SettingsScreen)_stack.Find(ScreenCatalog.Settings);
            _stack.Key(ScreenKey.Down, true);
            Assert.That(_stack.Tree.Focused?.Id, Is.EqualTo(SettingsScreen.ToggleId(0)));
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
            Assert.That(screen.TextKeys, Does.Contain("ui.settings.language"), "Unity 판 설정 화면의 글자는 더한 언어 줄뿐이다");
        }
    }

    /// <summary>화면은 GameSession.Changed만 듣는다: 화면이 아닌 곳에서 부른 명령도 알림이 오고, 적용되지 않은 명령은 알림이 없다.</summary>
    public sealed class SessionChangeTests
    {
        private sealed class Listener : GameScreen
        {
            public readonly System.Collections.Generic.List<string> Topics = new System.Collections.Generic.List<string>();
            public Listener(ScreenServices s) : base(s) { }
            protected override UiNode Build() => new UiNode();
            protected override void OnChanged(string topic) => Topics.Add(topic);
        }

        private static void WithStack(Action<GameSession, Listener> body)
        {
            string saves = Path.Combine(Path.GetTempPath(), "rootborn-screens-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saves);
            try
            {
                LoadResult loaded = GameDataLoader.Load(TestData.DataDirectory, RootbornBuilders.Register);
                loaded.ThrowIfFailed();
                GameSession session = GameSession.Start(loaded.Definitions, GameStartSettings.FromRegistry(TestData.DataDirectory),
                    new FileSaveStore(saves), "slot-test", null);
                var registry = new ScreenRegistry();
                registry.Add("listener", ScreenLayer.Hud, false, s => new Listener(s));
                var stack = new ScreenStack(registry, new ScreenServices(TestData.Text(), session));
                body(session, (Listener)stack.Open("listener"));
            }
            finally
            {
                try { Directory.Delete(saves, true); } catch (IOException) { }
            }
        }

        [Test]
        public void ACommandCalledOnTheSession_ReachesOpenScreens()
        {
            WithStack((session, listener) =>
            {
                var result = session.PerformLocationActivity("location.activity.library-hard-book", "req-1");
                Assert.That(result.Kind, Is.EqualTo(Rootborn.Core.StudentLife.LifeActivityResultKind.Applied));
                Assert.That(listener.Topics, Does.Contain(ChangeTopics.Student));
                Assert.That(listener.Topics, Is.Unique, "주제마다 한 번");
            });
        }

        [Test]
        public void ACommandThatChangesNothing_TellsNoScreen()
        {
            WithStack((session, listener) =>
            {
                Assert.That(session.AcceptQuest("no-such-quest"), Is.False);
                Assert.That(listener.Topics, Is.Empty);
            });
        }

        [Test]
        public void ScreenTopics_AreTheSessionTopics()
        {
            Assert.That(ChangeTopics.Student, Is.EqualTo(GameChangeTopics.Student));
            Assert.That(ChangeTopics.Inventory, Is.EqualTo(GameChangeTopics.Inventory));
            Assert.That(ChangeTopics.Quests, Is.EqualTo(GameChangeTopics.Quests));
            Assert.That(ChangeTopics.Dialogue, Is.EqualTo(GameChangeTopics.Dialogue));
            Assert.That(ChangeTopics.Day, Is.EqualTo(GameChangeTopics.Day));
            Assert.That(ChangeTopics.Career, Is.EqualTo(GameChangeTopics.Career));
            Assert.That(ChangeTopics.World, Is.EqualTo(GameChangeTopics.World));
            Assert.That(ChangeTopics.DailyEvents, Is.EqualTo(GameChangeTopics.DailyEvents));
            Assert.That(ChangeTopics.Discovery, Is.EqualTo(GameChangeTopics.Discovery));
            Assert.That(ChangeTopics.Furniture, Is.EqualTo(GameChangeTopics.Furniture));
            Assert.That(ChangeTopics.Objectives, Is.EqualTo(GameChangeTopics.Objectives));
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
