using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 긴 문구 시험 도구(테스트 전용): 실제 두 현지화 폴더를 합친 표에서 모든 문구를 기준 문구의 2배 길이(같은 문구 두 번)로 바꾼 GameText.
    /// 언어 목록은 폴더에서 읽는다.
    /// </summary>
    internal static class LongText
    {
        public static Dictionary<string, Dictionary<string, string>> MergedTables()
        {
            var tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string directory in new[] { TestData.LocalizationDirectory, TestData.ExtraLocalizationDirectory })
            {
                if (!Directory.Exists(directory)) continue;
                foreach (string file in Directory.GetFiles(directory, "*.json"))
                {
                    string code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (!tables.TryGetValue(code, out var merged)) tables[code] = merged = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))) merged[pair.Key] = pair.Value;
                }
            }
            return tables;
        }

        /// <summary>모든 문구를 2배 길이로. 기준 문구(BaseLanguage)의 길이를 기준으로 하므로 기준 문구를 두 번 이어 쓴다(자리 표시도 두 번).</summary>
        public static GameText Create(string language = GameText.DefaultBaseLanguage)
        {
            var doubled = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in MergedTables())
                doubled[table.Key] = table.Value.ToDictionary(p => p.Key, p => p.Value + p.Value, StringComparer.Ordinal);
            return new GameText(doubled, language);
        }
    }

    /// <summary>
    /// 지금 main에 있는 화면(설정, HUD 5종, 대화, 하루 이벤트)을 2배 길이 문구로 띄워 글자가 패널 밖으로 나가지 않는지,
    /// 버튼끼리 겹치지 않는지 본다. 화면 폴더는 다른 세션이 고치고 있어서 넘치는 곳은 고치지 않고 KnownOverflow에 고정한다
    /// (보고서 목록). 목록 밖의 넘침이 생기거나 목록의 넘침이 사라지면 실패한다. 자동 테스트다.
    /// </summary>
    public sealed class LongTextScreenTests
    {
        /// <summary>알려진 넘침: "화면 id | 노드 id | 종류". 화면을 고치면 여기서 지운다.</summary>
        private static readonly string[] KnownOverflow =
        {
        };

        private static readonly Lazy<GameDefinitions> Definitions = new Lazy<GameDefinitions>(() =>
        {
            LoadResult loaded = GameDataLoader.Load(TestData.DataDirectory, RootbornBuilders.Register);
            loaded.ThrowIfFailed();
            return loaded.Definitions;
        });

        private sealed class LongSession : IDisposable
        {
            private readonly string _saves;

            public LongSession(string language, Action<ScreenRegistry> extra = null)
            {
                _saves = Path.Combine(Path.GetTempPath(), "rootborn-longtext-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_saves);
                Session = GameSession.Start(Definitions.Value, GameStartSettings.FromRegistry(TestData.DataDirectory), new FileSaveStore(_saves), "slot-test", null);
                Text = LongText.Create(language);
                var services = new ScreenServices(Text, new SessionCommands(Session)) { Names = DisplayNames.Load(TestData.DataDirectory) };
                ScreenRegistry registry = ScreenCatalog.Create();
                extra?.Invoke(registry);
                Stack = new ScreenStack(registry, services);
                Stack.Layout(1920, 1080);
            }

            public GameSession Session { get; }
            public GameText Text { get; }
            public ScreenStack Stack { get; }

            public void Settle()
            {
                // 두 번: 첫 그리기에서 글자 폭에 맞춘 높이가 다음 배치에 들어간다.
                for (int i = 0; i < 2; i++)
                {
                    Stack.Layout(1920, 1080);
                    Stack.Emit(new DrawList(), new WideMeasurer());
                }
                Stack.Layout(1920, 1080);
            }

            public void Dispose()
            {
                try { Directory.Delete(_saves, true); } catch (IOException) { }
            }
        }

        private static readonly string[] Screens =
        {
            ScreenCatalog.Settings, ScreenCatalog.HudClock, ScreenCatalog.HudVitals, ScreenCatalog.HudTracked, ScreenCatalog.HudButtons,
            ScreenCatalog.HudPrompt, ScreenCatalog.Dialogue, ScreenCatalog.DailyEvent,
        };

        private static IEnumerable<string> Languages() => LongText.MergedTables().Keys.OrderBy(k => k);

        [TestCaseSource(nameof(Languages))]
        public void LongText_StaysInsideItsPanel_AndButtonsDoNotOverlap(string language)
        {
            var found = new List<string>();
            var checkedScreens = new List<string>();

            using (var t = new LongSession(language))
            {
                Assert.That(t.Text.Get("ui.hud.energy"), Is.EqualTo(TestData.Text(language).Get("ui.hud.energy") + TestData.Text(language).Get("ui.hud.energy")), "문구가 2배다");
                t.Stack.OpenHud();
                t.Stack.SetInteractionTarget(new InteractionTargetView("location-activity", "location.library", 800, 500, 64));
                t.Stack.Open(ScreenCatalog.Settings);
                t.Settle();
                foreach (string id in new[] { ScreenCatalog.Settings, ScreenCatalog.HudClock, ScreenCatalog.HudVitals, ScreenCatalog.HudButtons, ScreenCatalog.HudPrompt })
                    Inspect(t, id, found, checkedScreens);
                t.Stack.Close(ScreenCatalog.Settings);

                // 대화와 추적 목표: 엄마에게 말을 걸면 대화가 열리고 메인 체인이 추적된다.
                Assert.That(t.Stack.Interaction(new InteractionEvent("House", "npc", "npc.first-guide")), Is.True);
                t.Settle();
                Inspect(t, ScreenCatalog.Dialogue, found, checkedScreens);
                Inspect(t, ScreenCatalog.HudTracked, found, checkedScreens);
            }

            using (var t = new LongSession(language))
            {
                t.Stack.OpenHud();
                t.Stack.Interaction(new InteractionEvent("Town", "location-activity", "location.library"));
                // 장소 화면이 먼저 뜨고, 그것을 닫으면 하루 이벤트가 열린다(Unity 판 순서).
                t.Stack.Close(ScreenCatalog.Location);
                Assert.That(t.Stack.IsOpen(ScreenCatalog.DailyEvent), Is.True);
                t.Settle();
                Inspect(t, ScreenCatalog.DailyEvent, found, checkedScreens);
            }

            Assert.That(checkedScreens.Distinct().OrderBy(s => s), Is.EqualTo(Screens.OrderBy(s => s)), "모든 화면을 봤다");
            TestContext.WriteLine(language + " 넘침: " + (found.Count == 0 ? "없음" : string.Join("\n  ", found)));
            Assert.That(found.Except(KnownOverflow), Is.Empty, "알려진 목록 밖의 넘침");
            Assert.That(KnownOverflow.Where(k => k.StartsWith(language + " |", StringComparison.Ordinal) || !k.Contains(" |")).Except(found), Is.Empty,
                "고쳐진 넘침은 목록에서 지운다");
        }

        private sealed class Overflowing : GameScreen
        {
            public Overflowing(ScreenServices s) : base(s) { }

            protected override UiNode Build()
            {
                var panel = new UiPanel { Id = "probe.panel" }.Centered(0, 0, 100, 60);
                panel.Add(new UiLabel { Id = "probe.label", Text = "abcdefghijklmnopqrstuvwxyz", Ellipsis = false }).Place(0, 0, 0, 0, 100, 20);
                panel.Add(new UiTextBlock { Id = "probe.block", Text = "가나다라마바사아자차카타파하가나다라마바사아자차카타파하" }).Place(0, 0, 0, 20, 100, 20);
                panel.Add(new UiButton { Id = "probe.a" }).Place(0, 0, 0, 40, 60, 20);
                panel.Add(new UiButton { Id = "probe.b" }).Place(0, 0, 40, 40, 60, 20);
                panel.Add(new UiLabel { Id = "probe.fits", Text = "abc" }).Place(0, 0, 0, 10, 100, 30);
                return panel;
            }
        }

        [Test]
        public void TheChecker_FindsTextOutsideThePanel_AndOverlappingButtons()
        {
            using var t = new LongSession(GameText.DefaultBaseLanguage, r => r.Add("probe", ScreenLayer.Modal, false, s => new Overflowing(s)));
            t.Stack.Open("probe");
            t.Settle();
            var found = new List<string>();
            Inspect(t, "probe", found, new List<string>());
            string ko = GameText.DefaultBaseLanguage;
            Assert.That(found, Is.EquivalentTo(new[]
            {
                ko + " | probe | probe.label | 패널 밖",
                ko + " | probe | probe.block | 패널 밖",
                ko + " | probe | probe.a+probe.b | 버튼 겹침",
            }));
        }

        private static void Inspect(LongSession t, string screenId, List<string> found, List<string> checkedScreens)
        {
            GameScreen screen = t.Stack.Find(screenId);
            Assert.That(screen, Is.Not.Null, screenId);
            Assert.That(screen.IsOpen, Is.True, screenId + "가 열려 있어야 한다");
            checkedScreens.Add(screenId);
            var measurer = new WideMeasurer();
            var buttons = new List<UiNode>();

            foreach (UiNode node in Walk(screen.Root))
            {
                if (!node.IsVisibleInTree()) continue;
                if (node is UiButton || node is UiMultilineButton) buttons.Add(node);

                var list = new DrawList();
                node.Emit(list, measurer);
                UiRect container = ContainerOf(node, screen.Root).Rect;
                foreach (DrawCommand text in list.Commands.Where(c => c.Kind == DrawKind.Text))
                {
                    float left = text.Rect.X, top = text.Rect.Y;
                    float right = left + measurer.Width(text.Text, text.FontSize), bottom = top + measurer.LineHeight(text.FontSize);
                    const float slack = 0.5f;
                    if (left < container.X - slack || right > container.Right + slack || top < container.Y - slack || bottom > container.Bottom + slack)
                    {
                        found.Add(t.Text.Language + " | " + screenId + " | " + (node.Id ?? node.GetType().Name) + " | 패널 밖");
                        break;
                    }
                }

                // 보고용(실패시키지 않음): 2배 문구에서 말줄임으로 줄어든 한 줄 글자.
                string source = node is UiLabel label ? label.Text : node is UiButton button ? button.Text : null;
                if (!string.IsNullOrEmpty(source) && list.Commands.Any(c => c.Kind == DrawKind.Text && c.Text != source && c.Text.EndsWith(TextFit.Ellipsis, StringComparison.Ordinal)))
                    TestContext.WriteLine("말줄임: " + t.Text.Language + " | " + screenId + " | " + (node.Id ?? node.GetType().Name));
            }

            for (int i = 0; i < buttons.Count; i++)
                for (int j = i + 1; j < buttons.Count; j++)
                    if (Overlaps(buttons[i].Rect, buttons[j].Rect))
                        found.Add(t.Text.Language + " | " + screenId + " | " + buttons[i].Id + "+" + buttons[j].Id + " | 버튼 겹침");
        }

        private static IEnumerable<UiNode> Walk(UiNode root)
        {
            yield return root;
            foreach (UiNode child in root.Children)
                foreach (UiNode node in Walk(child)) yield return node;
        }

        /// <summary>가장 가까운 패널 조상(자기는 빼고). 없으면 화면 뿌리.</summary>
        private static UiNode ContainerOf(UiNode node, UiNode root)
        {
            for (UiNode p = node.Parent; p != null; p = p.Parent)
            {
                if (p is UiPanel) return p;
                if (ReferenceEquals(p, root)) return root;
            }
            return root;
        }

        private static bool Overlaps(UiRect a, UiRect b)
            => a.X < b.Right - 0.5f && b.X < a.Right - 0.5f && a.Y < b.Bottom - 0.5f && b.Y < a.Bottom - 0.5f;
    }

    /// <summary>
    /// 실제 글꼴에 가까운 측정기: 한글·한자·가나(전각)는 글자 크기 1배, 나머지는 0.55배. TestMeasurer(모두 0.5배)는 한글 폭을 반으로 본다.
    /// </summary>
    internal sealed class WideMeasurer : ITextMeasurer
    {
        public float Width(string text, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            float w = 0;
            foreach (char c in text)
            {
                if (char.IsLowSurrogate(c)) continue;
                bool wide = (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '　' && c <= '鿿') || (c >= '가' && c <= '힣') || (c >= '＀' && c <= '￯');
                w += fontSize * (wide ? 1f : 0.55f);
            }
            return w;
        }

        public float LineHeight(float fontSize) => fontSize * 1.25f;
    }

    internal static class UiNodeVisibility
    {
        public static bool IsVisibleInTree(this UiNode node)
        {
            for (UiNode n = node; n != null; n = n.Parent) if (!n.Visible) return false;
            return true;
        }
    }
}
