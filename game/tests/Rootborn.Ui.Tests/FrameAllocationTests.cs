using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Rootborn.Ui.Tests
{
    /// <summary>
    /// 매 프레임 도는 UI 일(배치, 포인터 판정, 그리기 명령 내기, 9조각 나누기)은 같은 나무를 두 번째 그릴 때 새 객체를 만들지 않는다
    /// (coding-standards "성능": 매 프레임 새 객체 금지). 할당은 GC.GetAllocatedBytesForCurrentThread 차이로 잰다(흔들리지 않는 값).
    /// 고친 뒤에도 그리기 결과(명령 목록, 9조각 조각)는 같아야 한다 — 픽셀이 아니라 명령으로 비교한다.
    /// </summary>
    public sealed class FrameAllocationTests
    {
        private static readonly NineSliceSource Urban = new NineSliceSource(8, 26, 86, 104, 8, 26, 86, 104);
        private static readonly NineSliceSource Uneven = new NineSliceSource(0, 5, 17, 20, 3, 4, 30, 41);

        /// <summary>고치기 전 BuildStretched(배열 두 개로 선을 잡던 것)를 그대로 옮긴 기준.</summary>
        private static void ReferenceStretched(PixelRect dest, NineSliceSource source, int scale, List<SlicePiece> into)
        {
            int left = Math.Min(source.ColW(0) * scale, dest.W / 2), right = Math.Min(source.ColW(2) * scale, dest.W - left);
            int top = Math.Min(source.RowH(0) * scale, dest.H / 2), bottom = Math.Min(source.RowH(2) * scale, dest.H - top);
            int[] xs = { dest.X, dest.X + left, dest.X + dest.W - right, dest.X + dest.W };
            int[] ys = { dest.Y, dest.Y + top, dest.Y + dest.H - bottom, dest.Y + dest.H };
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    int w = xs[col + 1] - xs[col], h = ys[row + 1] - ys[row];
                    if (w <= 0 || h <= 0) continue;
                    into.Add(new SlicePiece(new PixelRect(xs[col], ys[row], w, h),
                        new PixelRect(source.ColX(col), source.RowY(row), source.ColW(col), source.RowH(row))));
                }
        }

        [Test]
        public void StretchedNineSlice_SameAsBefore_ForManySizesAndScales()
        {
            var expected = new List<SlicePiece>();
            var actual = new List<SlicePiece>();
            int cases = 0;
            foreach (NineSliceSource source in new[] { Urban, Uneven })
                for (int scale = 1; scale <= 4; scale++)
                    for (int w = 0; w <= 260; w += 7)
                        for (int h = 0; h <= 260; h += 11)
                        {
                            var dest = new PixelRect(-13 + w, 40 - h / 3, w, h);
                            expected.Clear(); actual.Clear();
                            ReferenceStretched(dest, source, scale, expected);
                            NineSlice.BuildStretched(dest, source, scale, actual);
                            Assert.That(actual, Is.EqualTo(expected), dest + " 배율 " + scale);
                            cases++;
                        }
            Assert.That(cases, Is.GreaterThan(1000));
        }

        [Test]
        public void StretchedNineSlice_AllocatesNothing()
        {
            var pieces = new List<SlicePiece>(64);
            var dest = new PixelRect(100, 50, 660, 510);
            NineSlice.BuildStretched(dest, Urban, 2, pieces);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { pieces.Clear(); NineSlice.BuildStretched(dest, Urban, 2, pieces); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void TiledNineSlice_AllocatesNothing()
        {
            var pieces = new List<SlicePiece>(1024);
            var dest = new PixelRect(0, 0, 500, 300);
            var common = new NineSliceSource(0, 16, 32, 48, 0, 16, 32, 48);
            NineSlice.Build(dest, common, 2, pieces);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { pieces.Clear(); NineSlice.Build(dest, common, 2, pieces); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        private static readonly string[] RowTexts = { "첫째 줄", "둘째 줄", "셋째 줄", "넷째 줄", "다섯째 줄", "여섯째 줄", "일곱째 줄", "여덟째 줄", "아홉째 줄", "열째 줄" };

        private static void PaintCard(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            list.Panel(rect, UiSkin.Slot, state.Selected ? UiTheme.ButtonHovered : UiTheme.ButtonNormal);
            list.Text(rect.X + 4, rect.Y + 4, RowTexts[index % RowTexts.Length], 16, UiTheme.Ink);
        }

        /// <summary>Ui의 모든 부품을 한 나무에 담는다. 글자가 칸보다 길어 줄바꿈·말줄임·글자 줄이기가 일어나게 한다.</summary>
        private static UiTree EveryWidget()
        {
            var tree = new UiTree();
            var panel = tree.Root.Add(new UiPanel()); panel.Place(0, 0, 20, 20, 1800, 1000);
            panel.Add(new UiBox { Color = UiTheme.Accent }).Place(0, 0, 10, 10, 50, 20);
            panel.Add(new UiLabel { Text = "아주 긴 이름이라서 칸을 넘치는 한 줄 글자", Align = UiAlign.Center }).Place(0, 0, 70, 10, 120, 30);
            panel.Add(new UiButton { Text = "넘치는 단추 글자가 여기 있다", Icon = "icon:close" }).Place(0, 0, 200, 10, 100, 40);
            panel.Add(new UiSlot { Selected = true }).Place(0, 0, 310, 10, 64, 64);
            var field = panel.Add(new UiTextField { Placeholder = "이름" }); field.Place(0, 0, 380, 10, 200, 40); field.SetText("입력한 글");
            panel.Add(new UiMultilineButton { Text = "여러 줄로 나뉘는 단추 글자 하나 둘 셋 넷 다섯" }).Place(0, 0, 590, 10, 120, 80);
            panel.Add(new UiTextBlock { Text = "여러 줄 글자 묶음입니다. 폭에 맞춰 줄을 바꾼다.\n새 문단", Align = UiAlign.Right }).Place(0, 0, 720, 10, 160, 200);
            panel.Add(new UiImage { Image = "icon:journal" }).Place(0, 0, 890, 10, 32, 32);
            var tabs = panel.Add(new UiTabBar()); tabs.Place(0, 0, 10, 220, 400, 40);
            tabs.SetTabs(new[] { "하나", "둘", "셋" });
            panel.Add(new UiGauge { Value = 3, Max = 10 }).Place(0, 0, 420, 220, 200, 20);
            panel.Add(new UiToggle { IsOn = true }).Place(0, 0, 630, 220, 80, 40);
            panel.Add(new UiSlider { Value = 0.4f }).Place(0, 0, 720, 220, 200, 30);
            var list = panel.Add(new UiScrollList { RowText = i => RowTexts[i % RowTexts.Length] });
            list.Place(0, 0, 10, 280, 300, 200);
            list.RowCount = 40;
            var grid = panel.Add(new UiCardGrid { DrawCell = PaintCard });
            grid.Place(0, 0, 320, 280, 400, 300);
            grid.ItemCount = 200;
            var row = panel.Add(new UiCardRow { DrawCard = PaintCard });
            row.Place(0, 0, 730, 280, 500, 200);
            row.CardCount = 30;
            var sections = panel.Add(new UiSectionScrollView());
            sections.Place(0, 0, 10, 600, 300, 200);
            sections.SetSections(new[] { "첫 구역 글자는 길어서 여러 줄이 된다 하나 둘 셋 넷", "둘째 구역", "셋째 구역 글자도 꽤 길다 길다 길다" });
            panel.Add(new UiCenteredText { Text = "가운데 놓는 여러 줄 글자입니다 줄을 바꿔야 한다" }).Place(0, 0, 320, 600, 150, 120);
            panel.Add(new UiScrollText { Text = "스크롤 글자. 아주 길어서 넘친다. 하나 둘 셋 넷 다섯 여섯 일곱 여덟 아홉 열 열하나 열둘 열셋" }).Place(0, 0, 480, 600, 160, 80);
            panel.Add(new UiShrinkLabel { Text = "글자 크기를 줄여도 넘치는 아주 아주 긴 이름" }).Place(0, 0, 650, 600, 200, 40);
            var fitted = panel.Add(new UiFittedImage()); fitted.Place(0, 0, 860, 600, 100, 60);
            fitted.Show("icon:map", 40, 30);
            panel.Add(new UiSelectableRow { Selected = true }).Place(0, 0, 970, 600, 200, 40);
            return tree;
        }

        /// <summary>프레임 하나: 배치, 포인터 판정, 그리기 명령, 패널 명령마다 9조각 나누기(Host UiRenderer가 하는 일).</summary>
        private static void Frame(UiTree tree, DrawList list, ITextMeasurer text, List<SlicePiece> pieces, float pointerX)
        {
            tree.Layout(1920, 1080);
            tree.UpdatePointer(pointerX, 400, false);
            tree.Emit(list, text);
            for (int i = 0; i < list.Commands.Count; i++)
            {
                DrawCommand c = list.Commands[i];
                if (c.Kind != DrawKind.Panel) continue;
                var dest = new PixelRect((int)c.Rect.X, (int)c.Rect.Y, (int)c.Rect.W, (int)c.Rect.H);
                pieces.Clear();
                NineSlice.BuildStretched(dest, Urban, 2, pieces);
                pieces.Clear();
                NineSlice.Build(dest, Urban, 2, pieces);
            }
        }

        [Test]
        public void SameTreeDrawnTwice_SecondFrameAllocatesNothing_AndEmitsTheSameCommands()
        {
            UiTree tree = EveryWidget();
            var text = new FakeMeasurer();
            var list = new DrawList();
            var pieces = new List<SlicePiece>(4096);
            Frame(tree, list, text, pieces, 400);
            var first = new List<DrawCommand>(list.Commands);
            Assert.That(first.Count, Is.GreaterThan(80), "모든 부품이 그렸다");

            long before = GC.GetAllocatedBytesForCurrentThread();
            Frame(tree, list, text, pieces, 400);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(list.Commands, Is.EqualTo(first), "두 번째 프레임의 그리기 명령이 같다");
            Assert.That(bytes, Is.EqualTo(0), "같은 나무의 두 번째 프레임이 새로 잡은 바이트");

            // 포인터가 움직이는 프레임(올려 둔 요소가 바뀐다)도 같다.
            Frame(tree, list, text, pieces, 20 + 380 + 50); // 글자 칸 위
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10; i++) Frame(tree, list, text, pieces, i % 2 == 0 ? 450 : 1500);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "포인터가 오가는 프레임이 새로 잡은 바이트");
        }

        /// <summary>줄바꿈·말줄임 결과를 기억해도 글자·폭·크기가 바뀌면 새로 계산한다: 기억한 부품의 명령이 새로 만든 부품의 명령과 같다.</summary>
        [Test]
        public void CachedTextWidgets_RecomputeWhenTextWidthOrSizeChanges()
        {
            var text = new FakeMeasurer();
            Func<UiNode>[] makers =
            {
                () => new UiCenteredText { Text = "가운데 놓는 여러 줄 글자입니다 줄을 바꿔야 한다" },
                () => new UiShrinkLabel { Text = "글자 크기를 줄여도 넘치는 아주 아주 긴 이름" },
            };
            foreach (Func<UiNode> make in makers)
            {
                var tree = new UiTree();
                UiNode kept = tree.Root.Add(make()).Place(0, 0, 0, 0, 150, 120);
                var list = new DrawList();
                tree.Layout(1920, 1080);
                tree.Emit(list, text);

                void Change(Action<UiNode> change, float w)
                {
                    change(kept);
                    kept.Place(0, 0, 0, 0, w, 120);
                    tree.Layout(1920, 1080);
                    tree.Emit(list, text);
                    var freshTree = new UiTree();
                    UiNode fresh = freshTree.Root.Add(make());
                    change(fresh);
                    fresh.Place(0, 0, 0, 0, w, 120);
                    var freshList = new DrawList();
                    freshTree.Layout(1920, 1080);
                    freshTree.Emit(freshList, text);
                    Assert.That(list.Commands, Is.EqualTo(freshList.Commands), kept.GetType().Name + " 폭 " + w);
                }

                Change(n => { }, 150);
                Change(n => { }, 90);
                Change(n => { if (n is UiCenteredText c) c.Text = "짧은 글"; else ((UiShrinkLabel)n).Text = "짧은 이름"; }, 90);
                Change(n => { if (n is UiCenteredText c) { c.Text = "다시 긴 글자로 바꾸면 줄이 늘어난다 하나 둘 셋"; c.FontSize = 14; } else { var s = (UiShrinkLabel)n; s.Text = "다시 아주 아주 아주 긴 이름으로"; s.MaxFontSize = 30; s.MinFontSize = 16; } }, 200);
            }
        }
    }
}
