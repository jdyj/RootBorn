using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 실제 data/와 임시 폴더 세이브로 시작한 새 게임 한 판, 그 위의 화면 쌓기(실제 등록 표). 정의는 한 번만 읽는다.
    /// </summary>
    internal sealed class TestSession : IDisposable
    {
        private static readonly Lazy<GameDefinitions> _definitions = new Lazy<GameDefinitions>(() =>
        {
            LoadResult loaded = GameDataLoader.Load(TestData.DataDirectory, RootbornBuilders.Register);
            loaded.ThrowIfFailed();
            return loaded.Definitions;
        });

        private readonly string _saves;

        public TestSession(string language = "ko", Action<ScreenRegistry> extra = null)
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-screens-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            Session = GameSession.Start(_definitions.Value, GameStartSettings.FromRegistry(TestData.DataDirectory), new FileSaveStore(_saves), "slot-test", null);
            Commands = new SessionCommands(Session);
            Text = TestData.Text(language);
            ScreenRegistry registry = ScreenCatalog.Create();
            extra?.Invoke(registry);
            Services = new ScreenServices(Text, Commands) { Names = DisplayNames.Load(TestData.DataDirectory) };
            Stack = new ScreenStack(registry, Services);
            Stack.Layout(1920, 1080);
        }

        public GameSession Session { get; }
        public SessionCommands Commands { get; }
        public GameText Text { get; }
        public ScreenServices Services { get; }
        public ScreenStack Stack { get; }

        public T Screen<T>(string id) where T : GameScreen => (T)Stack.Find(id);
        public UiNode Node(string id) => Stack.Tree.Root.Find(id) ?? throw new InvalidOperationException("UI 요소가 없다: " + id);

        /// <summary>포인터로 누른다(UiTree 포인터 판정을 거친다). 그 자리 맨 위 요소가 그 요소가 아니면 실패.</summary>
        public void Click(string id)
        {
            UiNode node = Node(id);
            Assert.That(node.IsActive, Is.True, "보이지 않거나 비활성: " + id);
            UiRect r = node.Rect;
            Assert.That(Stack.Tree.HitTest(r.CenterX, r.CenterY), Is.SameAs(node), "요소 " + id + " 위에 다른 요소가 있다");
            Stack.Pointer(r.CenterX, r.CenterY, false, 0);
            Stack.Pointer(r.CenterX, r.CenterY, true, 0);
            Stack.Pointer(r.CenterX, r.CenterY, false, 0);
        }

        /// <summary>한 번 그린다(줄바꿈과 높이가 글자 폭에 맞춰진다).</summary>
        public DrawList Emit()
        {
            var list = new DrawList();
            Stack.Emit(list, new TestMeasurer());
            Stack.Layout(1920, 1080);
            return list;
        }

        public IEnumerable<string> Texts(DrawList list) => list.Commands.Where(c => c.Kind == DrawKind.Text).Select(c => c.Text);

        public void Dispose()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }
    }
}
