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
            public Fixture(int records, string language = "ko", bool mixed = false, bool chest = false)
            {
                _saves = Path.Combine(Path.GetTempPath(), "rootborn-stored-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_saves);
                if (records > 0)
                {
                    string[] rugs;
                    using (var probe = new TestSession()) rugs = Rugs(probe);
                    var items = Enumerable.Range(0, records).Select(i => new FurniturePlacementSaveData
                    {
                        FurnitureId = mixed ? rugs[i % 2] : rugs[0], SurfaceId = "house", AnchorX = 0, AnchorY = 0, Direction = FurniturePlacementDirection.North, StateJson = "{\"n\":" + i + "}",
                    }).ToList();
                    if (chest)
                        items.Add(new FurniturePlacementSaveData
                        {
                            FurnitureId = TestSession.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId, SurfaceId = "house", AnchorX = 0, AnchorY = 0,
                            Direction = FurniturePlacementDirection.North, StateJson = "{}",
                            Storage = new FurnitureStorageSaveData { Items = new[] { new Rootborn.Core.Inventory.PlayerInventoryItemSaveData { ItemId = "item.test-thing", Count = 2 } } },
                        });
                    new Rootborn.Data.Save.FileSaveStore(_saves).Write("slot-test", SaveFileNames.HouseFurnitureLayout(0),
                        new FurniturePlacementLayoutSaveData { Version = FurniturePlacementLayoutSaveData.KnownVersion, Items = items.ToArray() });
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

        // 첫째는 한 칸 깔개, 둘째는 이름이 다른 다른 가구(집에 놓을 수 있는 것 아무거나).
        private static string[] Rugs(TestSession t)
        {
            string rug = Rug(t);
            string other = t.Session.AvailableFurniture().First(f => f.StableId != rug && f.AllowsSurface("house")).StableId;
            return new[] { rug, other };
        }

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

        // ---------- 작업 11: 줄 그림과 방향, 줄 글자는 바뀔 때만 ----------

        [Test]
        public void StoredRows_ShowIconAndDirection()
        {
            using var f = new Fixture(13, chest: true);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            IReadOnlyList<StoredFurniture> stored = f.T.Session.StoredFurniture();
            Assert.That(stored.Count, Is.GreaterThan(1), "준비: 보관된 기록이 있다");
            var art = new FurnitureArt(TestData.DataDirectory);
            for (int row = 0; row < stored.Count; row++)
            {
                StoredFurniture s = stored[row];
                string direction = f.T.Text.Get("ui.furniture.direction." + s.Direction.ToString().ToLowerInvariant());
                string line = f.T.Text.Format("ui.furniture.stored.row", NameShown(f, row), direction);
                string expected = s.HoldsItems ? f.T.Text.Format("ui.furniture.stored.row-with-items", line) : line;
                Assert.That(f.List.RowText(row), Is.EqualTo(expected), "줄 " + row + ": 이름 · 방향(+ 물건이 든 상자)");
                Assert.That(art.TryPrimary(s.Definition, out FurnitureTilePart part, out FurnitureSprite sprite), Is.True, "준비: 대표 그림이 있다");
                Assert.That(f.List.RowIcon(row), Is.EqualTo(FurnitureArt.ImageId(part)), "줄 그림 = 가구 대표 그림");
                Assert.That(f.List.RowIconSource(row), Is.EqualTo(((float)sprite.W, (float)sprite.H)), "비율을 지키려고 그림 크기를 넘긴다");
            }
            Assert.That(stored.Any(s => s.HoldsItems), Is.True, "준비: 물건이 든 상자 줄이 있다");

            DrawList list = f.T.Emit();
            UiRect listRect = f.List.Rect;
            List<DrawCommand> icons = list.Commands.Where(c => c.Kind == DrawKind.Image && listRect.Contains(c.Rect.CenterX, c.Rect.CenterY)).ToList();
            Assert.That(icons.Count, Is.EqualTo(Math.Min(stored.Count, f.List.LastVisibleRow - f.List.FirstVisibleRow + 1)), "보이는 줄마다 그림 하나");
            foreach (DrawCommand icon in icons)
                Assert.That(icon.Rect.W <= f.List.IconSize + 0.01f && icon.Rect.H <= f.List.IconSize + 0.01f, Is.True, "그림은 그림 칸 안");
        }

        private static string NameShown(Fixture f, int row)
        {
            // 화면 이름 = 고르면 오른쪽 패널에 보이는 이름(같은 Name 길). 고르지 않고 줄 글의 앞부분과 견줄 값으로 쓴다.
            FurnitureDefinition d = f.Screen.Stored.Rows[row].Definition;
            string key = d.DisplayNameKey;
            if (!string.IsNullOrEmpty(key) && f.T.Text.Has(key, f.T.Text.Language)) return f.T.Text.Get(key);
            string rule = Rootborn.Screens.DataTextKeys.Name(d.StableId);
            if (rule != null && f.T.Text.Has(rule, f.T.Text.Language)) return f.T.Text.Get(rule);
            return string.IsNullOrEmpty(d.DisplayName) ? d.StableId : d.DisplayName;
        }

        [Test]
        public void StoredRowText_BuiltOnceNotPerFrame()
        {
            using var f = new Fixture(40);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            var free = f.T.Session.House.Placed[0];
            int before = f.Screen.StoredRowTextBuilds;
            Assert.That(f.T.Session.RemoveFurniture(free.InstanceId), Is.True, "준비: 가구 변경 알림 한 번(목록을 다시 읽는다)");
            for (int frame = 0; frame < 100; frame++) f.T.Emit();
            Assert.That(f.List.RowCount, Is.GreaterThan(f.List.LastVisibleRow + 1), "준비: 목록이 한 화면보다 길다");
            Assert.That(f.Screen.StoredRowTextBuilds - before, Is.EqualTo(f.List.RowCount),
                "그리기 100번 동안 줄 글을 만든 횟수 = 줄 수(목록을 다시 읽을 때 한 번씩, 그리기마다 만들지 않는다)");
        }

        [Test]
        public void LanguageSwitch_RebuildsRows()
        {
            using var f = new Fixture(20);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            StoredFurniture first = f.T.Session.StoredFurniture()[0];
            string ko = f.List.RowText(0);
            string koDirection = f.T.Text.Get("ui.furniture.direction." + first.Direction.ToString().ToLowerInvariant());
            Assert.That(ko, Does.Contain(koDirection));
            f.T.Text.Language = "en";
            f.T.Emit();
            string en = f.List.RowText(0);
            string enDirection = f.T.Text.Get("ui.furniture.direction." + first.Direction.ToString().ToLowerInvariant());
            Assert.That(enDirection, Is.Not.EqualTo(koDirection), "준비: 방향 문구가 언어마다 다르다");
            Assert.That(en, Is.EqualTo(f.T.Text.Format("ui.furniture.stored.row", NameShown(f, 0), enDirection)), "언어를 바꾸면 줄 글을 다시 만든다");
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
            string shownName = ((UiLabel)f.T.Node(FurniturePlacementScreen.SelectedNameId)).Text;
            Assert.That(shownName, Is.Not.Empty);
            Assert.That(rowText, Does.StartWith(shownName), "줄 글은 그 가구 이름으로 시작한다(뒤에 방향)");
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

        // ---------- 검토 지적 1: 다시 열면 고른 것을 지운다, 번호가 바뀐 줄은 놓지 않는다 ----------

        [Test]
        public void PickedRow_ClosedThenReopenedAfterReenteringTheHouse_IsGone_AndClickingTheMapPlacesNothing()
        {
            using var f = new Fixture(20, mixed: true);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            var free = f.T.Session.House.Placed[0];
            GridPoint cell = free.Anchor;
            Assert.That(f.T.Session.RemoveFurniture(free.InstanceId), Is.True);
            f.T.Emit();
            f.ClickRow(1);
            Assert.That(f.Screen.Active, Is.Not.Null, "준비: 보관 줄을 골랐다");
            f.T.Stack.Close(ScreenCatalog.FurniturePlacement);
            f.T.Session.EnterHouse(f.Room.Grid, "house"); // 배치를 다시 읽는다 — 기록 자리 번호가 다시 매겨진다
            f.T.Click("hud.button.placement");
            f.T.Emit();

            Assert.That(f.Screen.PickedStoredIndex, Is.EqualTo(-1), "다시 열면 보관 선택이 없다");
            Assert.That(f.Screen.Active, Is.Null, "오른쪽 패널과 미리 보기도 비어 있다");
            Assert.That(f.Screen.StoredShown, Is.False, "열 때마다 같은 분류(가구)에서 시작한다");
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            int before = f.T.Session.StoredFurniture().Count;
            f.ClickCell(cell);
            Assert.That(f.T.Session.StoredFurniture().Count, Is.EqualTo(before), "맵을 눌러도 아무것도 놓이지 않는다");
        }

        [Test]
        public void PickedRow_WhenTheListChangesWhileOpen_NeverPointsAtAnotherPiece()
        {
            using var f = new Fixture(20, mixed: true);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            var free = f.T.Session.House.Placed[0];
            Assert.That(f.T.Session.RemoveFurniture(free.InstanceId), Is.True);
            f.T.Emit();
            IReadOnlyList<StoredFurniture> list = f.T.Session.StoredFurniture();
            int other = Enumerable.Range(1, list.Count - 1).First(i => list[i].Definition != list[0].Definition);
            f.ClickRow(other);
            FurnitureDefinition picked = f.Screen.Active;
            Assert.That(picked, Is.Not.Null);

            // 다른 줄(0번)을 화면 밖 명령으로 놓아 목록을 바꾼다 — 가구 변경 알림이 화면에 간다.
            Assert.That(f.T.Session.PlaceStoredFurniture(list[0].Index, free.Anchor, list[0].Direction), Is.True, "준비: 다른 줄을 놓았다");
            f.T.Emit();

            if (f.Screen.PickedStoredIndex >= 0)
            {
                int row = f.Screen.Stored.RowOf(f.Screen.PickedStoredIndex);
                Assert.That(row, Is.GreaterThanOrEqualTo(0));
                Assert.That(f.Screen.Stored.Rows[row].Definition, Is.SameAs(f.Screen.Active), "고른 줄은 오른쪽 패널의 그 가구를 가리킨다");
                Assert.That(f.Screen.Active, Is.SameAs(picked));
            }
            else Assert.That(f.Screen.Active, Is.Null, "고른 줄을 잃었으면 고른 가구도 없다");
        }

        // ---------- 검토 지적 4: 물건이 든 상자 안내 ----------

        [Test]
        public void PickingAStoredChestWithItems_ShowsTheNoteThatItCanBeEmptiedOnceItIsPlaced()
        {
            using var f = new Fixture(13, chest: true);
            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            var note = (UiTextBlock)f.T.Node(FurniturePlacementScreen.StoredReasonId);
            Assert.That(note.IsActive, Is.False, "고른 것이 없으면 안내가 없다");
            int chest = f.T.Session.StoredFurniture().ToList().FindIndex(s => s.HoldsItems);
            Assert.That(chest, Is.GreaterThanOrEqualTo(0), "준비: 물건이 든 상자가 보관됐다");
            f.ClickRow(chest);
            Assert.That(note.IsActive, Is.True);
            Assert.That(note.Text, Is.EqualTo(f.T.Text.Get(FurniturePlacementScreen.HoldsItemsNoteKey)));
        }

        // ---------- 검토 지적 2: 같이 쓰는 집의 이유 문구 ----------

        [Test]
        public void SharedHouse_StoredTab_ShowsTheReason_AndRowsCannotBePicked()
        {
            string saves = Path.Combine(Path.GetTempPath(), "rootborn-stored-shared-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saves);
            try
            {
                var room = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap(6, 5));
                var rules = new Rootborn.Core.Room.RoomHouseRules(TestSession.Definitions.All<FurnitureDefinition>(), s => s == "house" ? room.Grid : null);
                var pieces = new Rootborn.Core.Room.RoomPieceSet(new Rootborn.Core.Room.RoomShareSettings(new[] { Rootborn.Core.Room.RoomHouse.PieceName }),
                    Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(rules));
                Assert.That(pieces.TryGet(Rootborn.Core.Room.RoomHouse.PieceName, out var piece), Is.True);
                string rug;
                using (var probe = new TestSession()) rug = Rug(probe);
                var store = new Rootborn.Data.Save.FileSaveStore(saves);
                store.Write("room", SaveFileNames.RoomPiece(Rootborn.Core.Room.RoomHouse.PieceName), new Rootborn.Core.Room.RoomHouseSaveData
                {
                    Version = Rootborn.Core.Room.RoomHouseSaveData.KnownVersion, NextNumber = 40,
                    Placed = Enumerable.Range(0, 30).Select(i => new Rootborn.Core.Room.RoomHousePlacementSaveData
                    {
                        InstanceId = (i + 1).ToString(), FurnitureId = rug, SurfaceId = "house", AnchorX = 0, AnchorY = 0,
                        Direction = FurniturePlacementDirection.North, PlacedBy = "a", Day = 1,
                    }).ToArray(),
                });
                ((Rootborn.Core.Room.RoomHouse)piece).Load(store, "room");
                var shares = new Rootborn.Core.Room.RoomShares(pieces.Settings, Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(), () => "guest-r1");
                foreach (var p in pieces.Pieces) shares.Receive(p.State());

                using var t = new TestSession(saves: saves, playerId: "guest", shares: shares);
                var view = new FurniturePlacementScreenTests.TestView(room);
                t.Session.EnterHouse(room.Grid, "house");
                t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
                t.Stack.OpenHud();
                t.Click("hud.button.placement");
                var screen = t.Screen<FurniturePlacementScreen>(ScreenCatalog.FurniturePlacement);
                screen.Attach(new FurnitureArt(TestData.DataDirectory), view);
                t.Emit();
                Assert.That(t.Session.SharesOpenLayout, Is.True, "준비: 같이 쓰는 집");
                Assert.That(t.Session.StoredFurniture().Count, Is.GreaterThan(0), "준비: 방의 기록 중 보관된 것이 있다");

                t.Click(FurniturePlacementScreen.CategoryStoredId);
                var reason = (UiTextBlock)t.Node(FurniturePlacementScreen.StoredReasonId);
                Assert.That(reason.IsActive, Is.True, "이유 문구가 보인다");
                Assert.That(reason.Text, Is.EqualTo(t.Text.Get(StoredFurnitureModel.SharedReasonKey)));
                Assert.That(reason.Text, Is.Not.Empty);

                var list = (UiScrollList)t.Node(FurniturePlacementScreen.StoredListId);
                Assert.That(list.TryGetRowRect(0, out UiRect r), Is.True);
                t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
                t.Stack.Pointer(r.CenterX, r.CenterY, true, 0);
                t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
                Assert.That(screen.Active, Is.Null, "줄을 눌러도 고를 수 없다");
                Assert.That(screen.PickedStoredIndex, Is.EqualTo(-1));
                Assert.That(list.SelectedIndex, Is.EqualTo(-1));
            }
            finally { try { Directory.Delete(saves, true); } catch (IOException) { } }
        }
    }
}
