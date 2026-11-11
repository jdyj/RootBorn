using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// 하루 묶음(일과, 장소, 하루 결과) 화면 테스트의 공통 준비: 실제 data/와 임시 폴더 저장소로 GameSession을 새로 시작하고
    /// 그 위에 화면 쌓기를 올린다. 화면은 상호작용 이벤트(월드가 내는 것과 같은 값)로 열고, UiTree에 포인터를 넣어 누른다.
    /// </summary>
    internal sealed class DayScreensFixture : IDisposable
    {
        private static readonly Lazy<GameDefinitions> _definitions = new Lazy<GameDefinitions>(() =>
        {
            LoadResult loaded = GameDataLoader.Load(TestData.DataDirectory, RootbornBuilders.Register);
            loaded.ThrowIfFailed();
            return loaded.Definitions;
        });

        private readonly string _saves;

        public DayScreensFixture(string language = "ko")
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-day-screens-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            Store = new FileSaveStore(_saves);
            Session = GameSession.Start(_definitions.Value, GameStartSettings.FromRegistry(TestData.DataDirectory), Store, "slot-test", null);
            Commands = new SessionCommands(Session);
            Text = TestData.Text(language);
            Stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(Text, Commands));
            Stack.Layout(1920, 1080);
            Measurer = new TestMeasurer();
        }

        public FileSaveStore Store { get; }
        public GameSession Session { get; }
        public SessionCommands Commands { get; }
        public GameText Text { get; }
        public ScreenStack Stack { get; }
        public TestMeasurer Measurer { get; }

        public UiNode Node(string id) => Stack.Tree.Root.Find(id) ?? throw new InvalidOperationException("UI 요소가 없다: " + id);

        public bool Interact(string map, string kind, string id) => Stack.Interaction(new InteractionEvent(map, kind, id));

        /// <summary>요소 가운데를 포인터로 누르고 뗀다. 그 자리의 맨 위 요소가 그것이어야 한다.</summary>
        public void Click(string id)
        {
            UiNode node = Node(id);
            Assert.That(node.IsActive, Is.True, "보이지 않거나 비활성: " + id);
            ClickAt(node.Rect.CenterX, node.Rect.CenterY, id);
        }

        public void ClickAt(float x, float y, string expectId = null)
        {
            if (expectId != null) Assert.That(Stack.Tree.HitTest(x, y)?.Id, Is.EqualTo(expectId), "그 자리 맨 위 요소");
            Stack.Pointer(x, y, false, 0);
            Stack.Pointer(x, y, true, 0);
            Stack.Pointer(x, y, false, 0);
        }

        /// <summary>그리기 명령을 한 번 낸다(줄바꿈·글자를 계산하게). 낸 글자들을 돌려준다.</summary>
        public List<string> EmitTexts()
        {
            var list = new DrawList();
            Stack.Emit(list, Measurer);
            return list.Commands.Where(c => c.Kind == DrawKind.Text).Select(c => c.Text).ToList();
        }

        public void Dispose()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }
    }
}
