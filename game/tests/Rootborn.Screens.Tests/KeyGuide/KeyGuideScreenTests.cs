using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Screens.KeyGuide;
using Rootborn.Screens.Settings;
using Rootborn.Screens.Tests.Start;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.KeyGuide
{
    /// <summary>
    /// 단축키 안내: 줄이 등록 표·화면 쌓기·Host 이름표에서 만들어지는지, F1·설정 단추·Esc, 미니게임 중에는 열리지 않음, 시작 화면에서도 F1,
    /// 모든 언어의 문구 키, 2배 문구에서 줄 안 겹침. 창 없이 도는 자동 테스트다(화면 근거 아님).
    /// </summary>
    public sealed class KeyGuideScreenTests
    {
        /// <summary>Host의 이름표(KeyGuideNames.Create)와 같은 모양의 시험용 이름표. Screens는 Host를 참조하지 않는다.</summary>
        internal static KeyGuideKeys HostLikeKeys()
        {
            var keys = new KeyGuideKeys();
            keys.Name(ScreenKey.Escape, KeyChord.Of("Esc"))
                .Name(ScreenKey.Inventory, KeyChord.Of("I"))
                .Name(ScreenKey.Status, KeyChord.Of("C"))
                .Name(ScreenKey.Tab, KeyChord.Of("Tab"))
                .Name(ScreenKey.Enter, KeyChord.Of("Enter"))
                .Name(ScreenKey.Help, KeyChord.Of("F1"))
                .Name(ScreenKey.Interact, KeyChord.Of("E"), KeyChord.Of("Space"), KeyChord.Of("Ctrl"))
                .Name(ScreenKey.Left, KeyChord.Of("←")).Name(ScreenKey.Right, KeyChord.Of("→"))
                .Name(ScreenKey.Space, KeyChord.Of("Space")).Name(ScreenKey.Rotate, KeyChord.Of("R"))
                .Name(ScreenKey.AltLeft, KeyChord.Of("A")).Name(ScreenKey.AltRight, KeyChord.Of("D"));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Movement, "ui.keys.move", new[] { keys.Directions, KeyChord.Of("WASD") }));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Interact, "ui.keys.interact", keys.For(ScreenKey.Interact)));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.System, "ui.keys.fullscreen", new[] { KeyChord.Of("F11") }));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.System, "ui.keys.screenshot", new[] { KeyChord.Of("F12") }));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Furniture, "ui.keys.furniture.full-view", new[] { KeyChord.Of("F") }));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Furniture, "ui.keys.furniture.zoom-in", new[] { KeyChord.Of("=") }));
            keys.AddRow(new KeyGuideRow(KeyGuideGroups.Furniture, "ui.keys.furniture.zoom-out", new[] { KeyChord.Of("-") }));
            return keys;
        }

        private static TestSession Session(string language = "ko", Action<ScreenRegistry> extra = null)
        {
            var t = new TestSession(language, extra);
            t.Services.KeyGuide = HostLikeKeys();
            t.Stack.OpenHud();
            return t;
        }

        private static KeyGuideScreen Open(TestSession t)
        {
            Assert.That(t.Stack.Key(ScreenKey.Help, false), Is.True, "F1을 화면 쌓기가 가져간다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.True, "F1로 열린다");
            return t.Screen<KeyGuideScreen>(ScreenCatalog.KeyGuide);
        }

        [Test]
        public void F1_OpensAModalThatBlocksMovement_EscapeOrF1Closes()
        {
            using TestSession t = Session();
            KeyGuideScreen screen = Open(t);
            Assert.That(screen.Layer, Is.EqualTo(ScreenLayer.Modal));
            Assert.That(t.Stack.MovementBlocked, Is.True, "읽는 화면이라 걷지 않는다");
            Assert.That(t.Stack.Key(ScreenKey.Escape, false), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.False, "Esc로 닫힌다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Settings), Is.False, "닫는 Esc가 설정을 열지 않는다");

            Open(t);
            t.Stack.Key(ScreenKey.Help, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.False, "F1을 다시 누르면 닫힌다");

            Open(t);
            t.Click(KeyGuideScreen.CloseId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.False, "닫기 단추");
        }

        [Test]
        public void ScreenRows_ComeFromTheRegistry_InRegistrationOrder()
        {
            using TestSession t = Session();
            KeyGuideScreen screen = Open(t);
            var expected = ScreenCatalog.Create().KeyBindings.Select(b => KeyGuideModel.ScreenActionKey(b.Value)).ToArray();
            Assert.That(expected, Is.SupersetOf(new[] { "ui.keys.settings", "ui.keys.inventory", "ui.keys.status", "ui.keys.objective-journal", "ui.keys.key-guide" }));
            var rows = screen.Model.Rows.Where(r => r.Group == KeyGuideGroups.Screens).ToArray();
            Assert.That(rows.Select(r => r.ActionKey), Is.EqualTo(expected));
            Assert.That(rows.Single(r => r.ActionKey == "ui.keys.inventory").Alternatives.Single().ToString(), Is.EqualTo("I"));
            Assert.That(rows.Single(r => r.ActionKey == "ui.keys.key-guide").Alternatives.Single().ToString(), Is.EqualTo("F1"), "안내 자신도 안내에 나온다");
        }

        [Test]
        public void RegisteringAScreenWithAKey_AddsOneLine()
        {
            const string probe = "probe-log";
            int before;
            using (TestSession plain = Session()) before = Open(plain).Model.Rows.Count;

            using TestSession t = Session(extra: r => r.Add(probe, ScreenLayer.Panel, false, s => new LocationProbe(s)).OpenedBy(ScreenKey.Rotate));
            t.Services.KeyGuide.Name(ScreenKey.Rotate, KeyChord.Of("R"));
            KeyGuideScreen screen = Open(t);
            Assert.That(screen.Model.Rows.Count, Is.EqualTo(before + 1));
            KeyGuideRow row = screen.Model.Rows.Single(r => r.ActionKey == "ui.keys." + probe);
            Assert.That(row.Group, Is.EqualTo(KeyGuideGroups.Screens));
            Assert.That(row.Alternatives.Single().ToString(), Is.EqualTo("R"));
            Assert.That(screen.Lines.Any(l => !l.IsHeader && l.Label == "ui.keys." + probe), Is.True, "문구가 없으면 키 그대로 보인다(빠졌다는 것이 보인다)");
        }

        private sealed class LocationProbe : GameScreen
        {
            public LocationProbe(ScreenServices s) : base(s) { }
            protected override UiNode Build() => new UiPanel { Id = "probe-log" }.Centered(0, 0, 100, 100);
        }

        [Test]
        public void KeyWithoutAHostName_ShowsTheScreenKeyName()
        {
            var keys = new KeyGuideKeys();
            Assert.That(keys.For(ScreenKey.Help).Single().ToString(), Is.EqualTo("Help"));
        }

        [Test]
        public void Groups_AreInOrder_WithHostRowsAndTheScreenStackNavigationRows()
        {
            using TestSession t = Session();
            KeyGuideScreen screen = Open(t);
            var groups = screen.Model.Rows.Select(r => r.Group).Distinct().ToArray();
            Assert.That(groups, Is.EqualTo(KeyGuideGroups.Order));
            Assert.That(screen.Model.Rows.Where(r => r.Group == KeyGuideGroups.Navigation).Select(r => r.ToString()), Is.EqualTo(new[]
            {
                "navigation | ui.keys.navigate-blocking | [ui.keys.name.arrows]",
                "navigation | ui.keys.navigate-walking | Shift+[ui.keys.name.arrows]",
                "navigation | ui.keys.press | Enter",
            }));
            Assert.That(screen.Model.Rows.Single(r => r.ActionKey == "ui.keys.interact").ToString(), Is.EqualTo("interact | ui.keys.interact | E / Space / Ctrl"));
            Assert.That(screen.Lines.Count(l => l.IsHeader), Is.EqualTo(KeyGuideGroups.Order.Length), "묶음마다 제목 줄 하나");
            Assert.That(screen.Lines.First().Label, Is.EqualTo("이동"));
            Line(screen, "걷기");
            Assert.That(Line(screen, "걷기").Chords.Select(c => string.Join("+", c)), Is.EqualTo(new[] { "방향 키", "WASD" }), "방향 키는 현지화 문구, 키 이름은 그대로");
        }

        /// <summary>미니게임 키는 그 키를 처리하는 미니게임 화면이 등록 때 넘긴 줄에서 온다(손으로 적은 목록이 아니다).</summary>
        [Test]
        public void MinigameKeys_ComeFromTheMinigameScreenRegistration()
        {
            using TestSession t = Session();
            KeyGuideScreen screen = Open(t);
            Assert.That(screen.Model.Rows.Where(r => r.Group == KeyGuideGroups.Minigame).Select(r => r.ToString()), Is.EqualTo(new[]
            {
                "minigame | ui.keys.minigame.tap | Space",
                "minigame | ui.keys.minigame.rotate | R",
                "minigame | ui.keys.minigame.lane | A / D / ← / →",
            }));
            Assert.That(screen.Model.Rows.Where(r => r.Group == KeyGuideGroups.Furniture).Select(r => r.ActionKey), Is.EqualTo(new[]
            {
                "ui.keys.furniture.full-view", "ui.keys.furniture.zoom-in", "ui.keys.furniture.zoom-out",
            }), "가구 배치 카메라 키는 Host 이름표의 줄");
        }

        [Test]
        public void ARegisteredScreenCanGuideItsOwnKeys()
        {
            using TestSession t = Session(extra: r => r.Add("probe", ScreenLayer.Panel, false, s => new SettingsScreen(s))
                .GuidesKeys(keys => new[] { new KeyGuideRow("probe-group", "ui.keys.title", keys.For(ScreenKey.Rotate)) }));
            KeyGuideScreen screen = Open(t);
            Assert.That(screen.Model.Rows.Last().ToString(), Is.EqualTo("probe-group | ui.keys.title | R"), "Order에 없는 묶음은 맨 뒤");
        }

        private static KeyGuideLine Line(KeyGuideScreen screen, string label) => screen.Lines.Single(l => !l.IsHeader && l.Label == label);

        [Test]
        public void KeysOfOtherScreens_DoNothingWhileTheGuideIsOpen()
        {
            using TestSession t = Session();
            Open(t);
            t.Stack.Key(ScreenKey.Inventory, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.False, "읽는 동안 다른 화면이 열리지 않는다(이동을 막는 화면의 규칙)");
        }

        [Test]
        public void F1_DoesNotOpenDuringAMinigame_AndEscapeStillForfeits()
        {
            using TestSession t = Session();
            t.Stack.EnterMap("Town");
            t.Stack.Interaction(new InteractionEvent("Town", "part-time-work", "work.shift.corner-store"));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Minigame), Is.True, "알바 미니게임이 열렸다");
            t.Stack.Key(ScreenKey.Help, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.False, "미니게임 중에는 열지 않는다(다른 화면 키와 같은 규칙)");
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Minigame), Is.False, "Esc는 그대로 미니게임 포기·닫기");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.False);
        }

        [Test]
        public void SettingsButton_OpensTheGuideOverSettings_EscapeClosesOnlyTheGuide()
        {
            using TestSession t = Session();
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Settings), Is.True);
            t.Emit();
            Assert.That(((UiButton)t.Node(SettingsScreen.KeyGuideButtonId)).Text, Is.EqualTo("단축키"));
            t.Click(SettingsScreen.KeyGuideButtonId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.True, "설정의 단축키 단추로 열린다");
            Assert.That(t.Stack.Top.Id, Is.EqualTo(ScreenCatalog.KeyGuide));
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.False);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Settings), Is.True, "설정은 그대로");
        }

        [Test]
        public void SettingsButton_SitsInsideThePanel_WithoutTouchingTheOtherControls()
        {
            using TestSession t = Session();
            t.Stack.Key(ScreenKey.Escape, false);
            t.Emit();
            UiRect panel = t.Node("settings").Rect;
            UiRect button = t.Node(SettingsScreen.KeyGuideButtonId).Rect;
            Assert.That(panel.Contains(button.X, button.Y) && panel.Contains(button.Right - 0.01f, button.Bottom - 0.01f), Is.True, "패널 안");
            var others = new[] { SettingsScreen.LanguageButtonId, SettingsScreen.LanguageLabelId }
                .Concat(Enumerable.Range(0, SettingsModel.ToggleCount).Select(SettingsScreen.ToggleId));
            foreach (string id in others)
                Assert.That(UiSurfaces.Overlap(button, t.Node(id).Rect), Is.False, id + "와 겹치지 않는다");
        }

        [Test]
        public void F1_OnTheStartScreen_OpensTheGuide_EscapeReturnsToTheSlots()
        {
            using var t = new StartMenuFixture();
            t.Services.KeyGuide = HostLikeKeys();
            t.OpenSlots();
            Assert.That(t.Stack.Key(ScreenKey.Help, false), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.True, "시작 화면에서도 F1");
            Assert.That(t.Stack.Top.Id, Is.EqualTo(ScreenCatalog.KeyGuide));
            Assert.That(t.Screen<KeyGuideScreen>(ScreenCatalog.KeyGuide).Model.Rows, Is.Not.Empty);
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.KeyGuide), Is.False);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "슬롯 선택으로 돌아온다");

            t.Stack.Key(ScreenKey.Inventory, false);
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory) || t.Stack.IsOpen(ScreenCatalog.Settings), Is.False,
                "다른 화면 키는 시작 화면에서 그대로 막혀 있다(게임 한 판이 없다)");
        }

        private static IEnumerable<string> Languages() => LongText.MergedTables().Keys.OrderBy(k => k);

        [TestCaseSource(nameof(Languages))]
        public void EveryTextKey_IsInThisLanguage(string language)
        {
            var table = LongText.MergedTables()[language];
            using TestSession t = Session(language);
            KeyGuideScreen screen = Open(t);
            t.Stack.Close(screen);
            t.Stack.Key(ScreenKey.Escape, false);
            var settings = t.Stack.Find(ScreenCatalog.Settings);
            var used = screen.TextKeys.Concat(settings.TextKeys).Distinct().ToArray();
            Assert.That(used, Does.Contain("ui.keys.title").And.Contain("ui.keys.name.arrows").And.Contain("ui.settings.key-guide"));
            Assert.That(used.Where(k => !table.ContainsKey(k)), Is.Empty, language + "에 없는 키");
        }

        [Test]
        public void LocationLogKey_HasText_SoTheLineReadsWellWhenItsKeyArrives()
        {
            // records-and-jobs 세션이 장소 기록 화면에 L 키를 더하는 중이다. 합친 뒤 줄 이름이 바로 보이도록 문구를 미리 둔다.
            foreach (var table in LongText.MergedTables())
                Assert.That(table.Value.ContainsKey(KeyGuideModel.ScreenActionKey(ScreenCatalog.LocationStateLog)), Is.True, table.Key);
        }

        [TestCaseSource(nameof(Languages))]
        public void DoubledText_StaysInsideEachLine_AndNothingOverlaps(string language)
        {
            using var t = new TestSession(language);
            var doubled = new ScreenServices(LongText.Create(language), t.Session) { KeyGuide = HostLikeKeys() };
            var stack = new ScreenStack(ScreenCatalog.Create(), doubled);
            stack.Layout(1920, 1080);
            stack.Key(ScreenKey.Help, false);
            var screen = (KeyGuideScreen)stack.Find(ScreenCatalog.KeyGuide);
            var measurer = new WideMeasurer();
            stack.Emit(new DrawList(), measurer);
            Assert.That(screen.Lines.First().Label, Does.EndWith(TestData.Text(language).Get("ui.keys.group.movement")), "문구가 2배다");

            UiRect panel = stack.Tree.Root.Find(KeyGuideScreen.PanelId).Rect;
            UiRect list = stack.Tree.Root.Find(KeyGuideScreen.ListId).Rect;
            Assert.That(Inside(panel, new UiRect(0, 0, 1920, 1080)) && Inside(list, panel), Is.True, "패널은 화면 안, 목록은 패널 안");
            Assert.That(Inside(stack.Tree.Root.Find(KeyGuideScreen.CloseId).Rect, panel), Is.True);
            Assert.That(UiSurfaces.Overlap(stack.Tree.Root.Find(KeyGuideScreen.CloseId).Rect, list), Is.False, "닫기 단추와 목록이 겹치지 않는다");

            float width = screen.List.CellWidth;
            var rowBox = new UiRect(0, 0, width, KeyGuideScreen.Metrics.RowH);
            foreach (KeyGuideLine line in screen.Lines)
            {
                line.Layout(width, measurer);
                Assert.That(measurer.Width(line.FittedLabel, line.IsHeader ? KeyGuideScreen.Metrics.HeaderFont : KeyGuideScreen.Metrics.LabelFont),
                    Is.LessThanOrEqualTo(line.LabelRect.W + 0.01f), line.Label + ": 글자가 자리보다 길지 않다(말줄임)");
                var parts = new List<UiRect> { line.LabelRect };
                parts.AddRange(line.CapRects);
                parts.AddRange(line.SeparatorRects);
                foreach (UiRect part in parts) Assert.That(Inside(part, rowBox), Is.True, line.Label + ": 줄 밖으로 나가지 않는다");
                for (int i = 0; i < parts.Count; i++)
                    for (int j = i + 1; j < parts.Count; j++)
                        Assert.That(UiSurfaces.Overlap(parts[i], parts[j]), Is.False, line.Label + ": 겹치지 않는다");
                for (int i = 0; i < line.CapRects.Length; i++)
                    Assert.That(measurer.Width(line.CapTexts[i], KeyGuideScreen.Metrics.CapFont), Is.LessThanOrEqualTo(line.CapRects[i].W + 0.01f));
            }
        }

        private static bool Inside(UiRect inner, UiRect outer) =>
            inner.X >= outer.X - 0.5f && inner.Y >= outer.Y - 0.5f && inner.Right <= outer.Right + 0.5f && inner.Bottom <= outer.Bottom + 0.5f;

        [Test]
        public void ALongList_Scrolls_SoEveryLineCanBeReached()
        {
            using TestSession t = Session(extra: r =>
            {
                ScreenKey[] spare = { ScreenKey.Up, ScreenKey.Down, ScreenKey.Left, ScreenKey.Right, ScreenKey.Enter, ScreenKey.Interact, ScreenKey.Space, ScreenKey.Rotate, ScreenKey.AltLeft, ScreenKey.AltRight };
                for (int i = 0; i < spare.Length; i++) r.Add("probe-" + i, ScreenLayer.Hud, false, s => new LocationProbe(s)).OpenedBy(spare[i]);
            });
            KeyGuideScreen screen = Open(t);
            t.Emit();
            UiCardGrid grid = screen.List;
            Assert.That(grid.Columns, Is.EqualTo(1));
            Assert.That(grid.MaxScroll, Is.GreaterThan(0f), "줄이 목록보다 많으면 스크롤한다");
            grid.OnWheel(-1000f);
            Assert.That(grid.ScrollOffset, Is.EqualTo(grid.MaxScroll));
            Assert.That(grid.CellRect(screen.Lines.Count - 1).Bottom, Is.LessThanOrEqualTo(grid.View.Bottom + 0.5f), "마지막 줄까지 보인다");
        }

        [Test]
        public void FontSizes_AreAtLeast14()
        {
            var m = typeof(KeyGuideScreen.Metrics);
            foreach (var name in new[] { "TitleFont", "HeaderFont", "LabelFont", "CapFont" })
                Assert.That((float)m.GetField(name).GetValue(null), Is.GreaterThanOrEqualTo(14f), name);
            Assert.That(SettingsKeyGuideFont(), Is.GreaterThanOrEqualTo(14f));
        }

        private static float SettingsKeyGuideFont()
        {
            using TestSession t = Session();
            t.Stack.Key(ScreenKey.Escape, false);
            return ((UiButton)t.Node(SettingsScreen.KeyGuideButtonId)).FontSize;
        }
    }
}
