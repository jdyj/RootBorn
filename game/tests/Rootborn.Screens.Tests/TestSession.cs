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

        /// <summary>실제 data/의 정의(한 번만 읽는다).</summary>
        public static GameDefinitions Definitions => _definitions.Value;

        private readonly string _saves;
        private readonly bool _ownsSaves;

        /// <param name="saves">이어 할 세이브 폴더(Reopen). 비면 새 임시 폴더를 만들고 Dispose에서 지운다.</param>
        /// <param name="definitions">쓸 정의(비면 실제 data/).</param>
        /// <param name="room">게임 한 판이 따를 방의 시계(비면 혼자인 방). playerId는 그 방에서의 신원(비면 기본 플레이어).</param>
        public TestSession(string language = "ko", Action<ScreenRegistry> extra = null, string saves = null, GameDefinitions definitions = null,
            Rootborn.Core.Time.IRoomTime room = null, string playerId = null)
        {
            _ownsSaves = saves == null;
            _saves = saves ?? Path.Combine(Path.GetTempPath(), "rootborn-screens-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            Session = GameSession.Start(definitions ?? _definitions.Value, GameStartSettings.FromDefinitions(definitions ?? _definitions.Value), new FileSaveStore(_saves), "slot-test", playerId, room: room);
            Text = TestData.Text(language);
            ScreenRegistry registry = ScreenCatalog.Create();
            extra?.Invoke(registry);
            Services = new ScreenServices(Text, Session) { Names = DisplayNames.Load(TestData.DataDirectory) };
            Stack = new ScreenStack(registry, Services);
            Stack.Layout(1920, 1080);
        }

        /// <summary>같은 세이브 폴더로 게임을 다시 불러온다(새 화면 쌓기). 폴더는 이 세션이 지운다.</summary>
        public TestSession Reopen(Action<ScreenRegistry> extra = null) => new TestSession(Text.Language, extra, _saves, Session.Definitions);

        public GameSession Session { get; }
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
            if (!_ownsSaves) return;
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }
    }
}
