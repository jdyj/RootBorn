using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Data.Save;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Start
{
    /// <summary>시트 id마다 정해 둔 칸을 주는 그림(실제 PNG를 읽지 않는다). 몸은 32x64, 다른 층은 32x40, 기준점은 아래 가운데.</summary>
    internal sealed class FakePreviewArt : ICharacterPreviewArt
    {
        public readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);

        public bool TryStandingFrame(string sheetId, out PreviewFrame frame)
        {
            frame = default;
            if (string.IsNullOrEmpty(sheetId) || Missing.Contains(sheetId)) return false;
            bool body = sheetId.StartsWith("body_", StringComparison.Ordinal);
            frame = new PreviewFrame("sheet:" + sheetId, new PixelRect(0, 0, 32, body ? 64 : 40), 0.5f, 1f);
            return true;
        }
    }

    /// <summary>
    /// 게임 한 판이 없는 시작 화면 쌓기(Host가 --slot 없이 띄운 것과 같다): 실제 등록 표, 실제 현지화, 실제 data/의 겉모습,
    /// 임시 폴더 세이브(FileSaveStore). 들어가기 요청은 기록만 하고 결과는 테스트가 정한다.
    /// </summary>
    internal sealed class StartMenuFixture : IDisposable
    {
        public readonly string Saves;
        public readonly FileSaveStore Store;
        public readonly List<StartEntry> Entries = new List<StartEntry>();
        public bool EnterSucceeds = true;
        public int QuitCount;
        public long Now = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc).Ticks;
        public int Ticks = 123456;
        public readonly FakePreviewArt Art = new FakePreviewArt();

        public StartMenuFixture(string language = "ko")
        {
            Saves = Path.Combine(Path.GetTempPath(), "rootborn-start-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Saves);
            Store = new FileSaveStore(Saves);
            Text = TestData.Text(language);
            Looks = CreatorLooks.Load(TestData.DataDirectory);
            Menu = new StartMenu(new SaveSlots(Store, DeleteFolder), Looks, Art, e => { Entries.Add(e); return EnterSucceeds; },
                () => QuitCount++, () => Now, () => Ticks);
            Services = new ScreenServices(Text, null) { Names = DisplayNames.Load(TestData.DataDirectory) };
            Stack = new ScreenStack(ScreenCatalog.Create(), Services);
            Stack.Layout(1920, 1080);
        }

        public GameText Text { get; }
        public CreatorLooks Looks { get; }
        public StartMenu Menu { get; }
        public ScreenServices Services { get; }
        public ScreenStack Stack { get; }

        private void DeleteFolder(string slot)
        {
            string dir = Path.Combine(Saves, slot);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        public SaveSlotScreen OpenSlots()
        {
            Stack.Open(ScreenCatalog.SaveSlotSelect, argument: Menu);
            Emit();
            return Screen<SaveSlotScreen>(ScreenCatalog.SaveSlotSelect);
        }

        public T Screen<T>(string id) where T : GameScreen => (T)Stack.Find(id);
        public UiNode Node(string id) => Stack.Tree.Root.Find(id) ?? throw new InvalidOperationException("UI 요소가 없다: " + id);
        public string LabelText(string id) => ((UiLabel)Node(id)).Text;

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
            Emit();
        }

        public DrawList Emit()
        {
            var list = new DrawList();
            Stack.Emit(list, new TestMeasurer());
            Stack.Layout(1920, 1080);
            return list;
        }

        /// <summary>새 이야기 하나를 슬롯에 바로 써 둔다(이미 있는 슬롯 준비용).</summary>
        public SaveSlotMetadata WriteStory(string slot, string name, string schoolLook = null)
        {
            var model = new CharacterCreatorModel(Looks);
            if (schoolLook != null)
                while (model.ChoiceAt(1).Id != schoolLook) model.Step(1, 1);
            SaveSlotMetadata metadata = SaveSlots.CreateMetadata(slot, name, model.Snapshot(), Now, Ticks);
            Store.Write(slot, SaveFileNames.Metadata, metadata);
            return metadata;
        }

        public IEnumerable<string> UsedKeys() => Stack.OpenScreens.Concat(new GameScreen[]
            {
                Stack.Find(ScreenCatalog.SaveSlotSelect), Stack.Find(ScreenCatalog.CharacterCreator), Stack.Find(ScreenCatalog.DeleteSlot),
            }).Where(s => s != null).SelectMany(s => s.TextKeys).Distinct();

        public void Dispose()
        {
            try { Directory.Delete(Saves, true); } catch (IOException) { }
        }
    }
}
