using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>
    /// HOUSE-002 화면: 가구 배치 화면의 "보관" 분류. 실제 data/, 임시 폴더 세이브, 시험용 작은 방(6x5칸, 안쪽 4x3)에 한 칸 깔개 기록을 방 자리보다
    /// 많이 써 둔 슬롯으로 이어 한다 — 못 놓은 기록이 보관된다. 자동 테스트, 창 없음(화면에 보인다는 근거 아님).
    /// </summary>
    public sealed class StoredFurnitureScreenTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly string _saves;
            public readonly TestSession T;
            public readonly FurnitureRoom Room = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap(6, 5));
            public readonly FurniturePlacementScreenTests.TestView View;
            public readonly FurniturePlacementScreen Screen;
            public readonly int StoredAtStart;

            /// <param name="records">슬롯에 미리 쓸 깔개 기록 수(0이면 배치 파일 없음). 안쪽 칸 수보다 많으면 나머지가 보관된다.</param>
            public Fixture(int records, string language = "ko")
            {
                _saves = Path.Combine(Path.GetTempPath(), "rootborn-stored-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_saves);
                if (records > 0)
                {
                    string rug;
                    using (var probe = new TestSession()) rug = Rug(probe);
                    var items = Enumerable.Range(0, records).Select(i => new FurniturePlacementSaveData
                    {
                        FurnitureId = rug, SurfaceId = "house", AnchorX = 0, AnchorY = 0, Direction = FurniturePlacementDirection.North, StateJson = "{\"n\":" + i + "}",
                    }).ToArray();
                    new Rootborn.Data.Save.FileSaveStore(_saves).Write("slot-test", SaveFileNames.HouseFurnitureLayout(0),
                        new FurniturePlacementLayoutSaveData { Version = FurniturePlacementLayoutSaveData.KnownVersion, Items = items });
                }
                T = new TestSession(language, saves: _saves);
                View = new FurniturePlacementScreenTests.TestView(Room);
                T.Session.EnterHouse(Room.Grid, "house");
                T.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
                T.Stack.OpenHud();
                T.Click("hud.button.placement");
                Screen = T.Screen<FurniturePlacementScreen>(ScreenCatalog.FurniturePlacement);
                Screen.Attach(new FurnitureArt(TestData.DataDirectory), View);
                T.Emit();
                StoredAtStart = T.Session.StoredFurniture().Count;
            }

            public UiScrollList List => (UiScrollList)T.Node(FurniturePlacementScreen.StoredListId);

            public void ClickRow(int row)
            {
                Assert.That(List.TryGetRowRect(row, out UiRect r), Is.True, "줄이 보인다");
                Assert.That(T.Stack.Tree.HitTest(r.CenterX, r.CenterY), Is.SameAs(List));
                T.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
                T.Stack.Pointer(r.CenterX, r.CenterY, true, 0);
                T.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
            }

            public void Pointer(float x, float y, bool down)
            {
                T.Stack.Pointer(x, y, down, 0);
                Screen.WorldPointer(x, y, down, T.Stack.Tree.HitTest(x, y) != null);
            }

            public void ClickCell(GridPoint cell)
            {
                (float x, float y) = View.Center(cell);
                Pointer(x, y, false);
                Pointer(x, y, true);
                Pointer(x, y, false);
            }

            public void Dispose()
            {
                T.Dispose();
                try { Directory.Delete(_saves, true); } catch (IOException) { }
            }
        }

        // 해금이 필요 없고 막지 않는 한 칸짜리 가구.
        private static string Rug(TestSession t) => t.Session.AvailableFurniture()
            .First(f => !f.BlocksMovement && f.AllowsSurface("house") && f.GetFootprintCells(FurniturePlacementDirection.North).Count == 1
                && f.GetTileParts(FurniturePlacementDirection.North).Count == 1).StableId;

        [Test]
        public void StoredTab_ShowsTheStoredList_AndHidesThePalette_AndBack()
        {
            using var f = new Fixture(20);
            Assert.That(f.StoredAtStart, Is.GreaterThan(1), "준비: 못 놓은 기록이 보관됐다");
            Assert.That(f.Screen.StoredShown, Is.False);
            Assert.That(f.T.Node(FurniturePlacementScreen.SlotId(0)).IsActive, Is.True);
            Assert.That(f.T.Node(FurniturePlacementScreen.StoredListId).IsActive, Is.False, "분류마다 따로 노드 — 가구 분류에서는 보관 목록이 없다");

            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            Assert.That(f.Screen.StoredShown, Is.True);
            Assert.That(f.T.Node(FurniturePlacementScreen.StoredListId).IsActive, Is.True);
            Assert.That(f.T.Node(FurniturePlacementScreen.SlotId(0)).IsActive, Is.False);
            Assert.That(f.T.Node(FurniturePlacementScreen.SearchId).IsActive, Is.False);

            f.T.Click(FurniturePlacementScreen.CategoryFurnitureId);
            Assert.That(f.Screen.StoredShown, Is.False);
            Assert.That(f.T.Node(FurniturePlacementScreen.SlotId(0)).IsActive, Is.True);
            Assert.That(f.T.Node(FurniturePlacementScreen.StoredListId).IsActive, Is.False);
        }

        [Test]
        public void StoredTab_OneRowPerStoredPiece_NamedByTheFurniture_AndOnlyVisibleRowsAreAsked()
        {
            using var f = new Fixture(40);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            IReadOnlyList<StoredFurniture> stored = f.T.Session.StoredFurniture();
            Assert.That(f.List.RowCount, Is.EqualTo(stored.Count));
            Assert.That(stored.Count, Is.GreaterThan(f.List.LastVisibleRow + 1), "준비: 목록이 한 화면보다 길다");
            int asked = 0;
            Func<int, string> inner = f.List.RowText;
            f.List.RowText = row => { asked++; return inner(row); };
            f.T.Emit();
            Assert.That(asked, Is.LessThanOrEqualTo(f.List.LastVisibleRow - f.List.FirstVisibleRow + 2), "보이는 줄만 글자를 묻는다");
            Assert.That(inner(0), Is.Not.Empty.And.Not.EqualTo(stored[0].Definition.StableId), "화면 이름(지금 언어)");
        }

        [Test]
        public void PickingARow_ThenAFreeFloorCell_PlacesIt_AndTheRowLeavesTheList()
        {
            using var f = new Fixture(20);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            int before = f.List.RowCount;
            var free = f.T.Session.House.Placed[0]; // 자리를 하나 만든다
            GridPoint cell = free.Anchor;
            Assert.That(f.T.Session.RemoveFurniture(free.InstanceId), Is.True);
            f.T.Emit();

            string rowText = f.List.RowText(0);
            f.ClickRow(0);
            Assert.That(f.Screen.Active, Is.Not.Null, "고른 줄의 가구가 오른쪽에 보인다");
            Assert.That(((UiLabel)f.T.Node(FurniturePlacementScreen.SelectedNameId)).Text, Is.EqualTo(rowText));
            f.ClickCell(cell);
            Assert.That(f.Screen.LastAction, Is.EqualTo("placed"));
            Assert.That(f.T.Session.House.Placed.Any(p => p.Anchor.Equals(cell)), Is.True);
            Assert.That(f.List.RowCount, Is.EqualTo(before - 1), "놓은 줄은 목록에서 빠진다");
            Assert.That(f.T.Session.StoredFurniture().Count, Is.EqualTo(before - 1));
        }

        [Test]
        public void PlacingWhereItDoesNotFit_IsRejected_AndTheListStaysTheSame()
        {
            using var f = new Fixture(20);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            int before = f.List.RowCount;
            f.ClickRow(0);
            f.ClickCell(f.T.Session.House.Placed[0].Anchor); // 이미 가구가 있는 칸
            Assert.That(f.List.RowCount, Is.EqualTo(before));
            Assert.That(f.T.Session.StoredFurniture().Count, Is.EqualTo(before));
        }

        [Test]
        public void NothingStored_ShowsTheEmptyText_AndNoList()
        {
            using var f = new Fixture(0);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            Assert.That(f.List.RowCount, Is.Zero);
            Assert.That(f.T.Node(FurniturePlacementScreen.StoredListId).IsActive, Is.False);
            var empty = (UiTextBlock)f.T.Node(FurniturePlacementScreen.StoredEmptyId);
            Assert.That(empty.IsActive, Is.True);
            Assert.That(empty.Text, Is.EqualTo(f.T.Text.Get("ui.furniture.stored.empty")));
        }

        [Test]
        public void ChangingTheLanguage_ChangesTheEmptyText()
        {
            using var f = new Fixture(0);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            string ko = ((UiTextBlock)f.T.Node(FurniturePlacementScreen.StoredEmptyId)).Text;
            f.T.Text.Language = "en";
            f.T.Emit();
            string en = ((UiTextBlock)f.T.Node(FurniturePlacementScreen.StoredEmptyId)).Text;
            Assert.That(en, Is.EqualTo(f.T.Text.Get("ui.furniture.stored.empty")));
            Assert.That(en, Is.Not.EqualTo(ko));
        }

        [Test]
        public void TheSelectedCategoryButton_LooksDifferentFromTheOther()
        {
            using var f = new Fixture(0);
            var furniture = (FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.CategoryFurnitureId);
            var stored = (FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.CategoryStoredId);
            Assert.That((furniture.Selected, stored.Selected), Is.EqualTo((true, false)));
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            Assert.That((furniture.Selected, stored.Selected), Is.EqualTo((false, true)));
        }

        [Test]
        public void LongTexts_StayInsideTheirButtonAndThePalette()
        {
            using var f = new Fixture(20);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            UiRect palette = f.T.Node(FurniturePlacementScreen.PaletteId).Rect;
            foreach (string id in new[] { FurniturePlacementScreen.StoredListId, FurniturePlacementScreen.CategoryFurnitureId, FurniturePlacementScreen.CategoryStoredId })
            {
                UiRect r = f.T.Node(id).Rect;
                Assert.That(r.X >= palette.X && r.Right <= palette.Right && r.Y >= palette.Y && r.Bottom <= palette.Bottom, Is.True, id + " 패널 안");
            }
            var storedButton = (FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.CategoryStoredId);
            storedButton.Text = new string('보', 80);
            DrawList list = f.T.Emit();
            var measure = new TestMeasurer();
            var texts = list.Commands.Where(c => c.Kind == DrawKind.Text && c.Text.StartsWith("보보")).ToList();
            Assert.That(texts, Is.Not.Empty);
            foreach (DrawCommand c in texts)
                Assert.That(measure.Width(c.Text, c.FontSize), Is.LessThanOrEqualTo(storedButton.Rect.W + 0.5f), "단추보다 긴 글자는 줄인다");
        }

        [Test]
        public void Model_FromASharedHouse_CannotPick_AndGivesTheReason()
        {
            var none = new List<StoredFurniture>();
            StoredFurnitureModel shared = StoredFurnitureModel.Build(none, f => f.DisplayName, shared: true);
            Assert.That((shared.CanPick, shared.ReasonKey), Is.EqualTo((false, "ui.furniture.stored.reason.shared")));
            StoredFurnitureModel alone = StoredFurnitureModel.Build(none, f => f.DisplayName, shared: false);
            Assert.That((alone.CanPick, alone.ReasonKey, alone.IsEmpty), Is.EqualTo((true, (string)null, true)));
        }
    }
}
