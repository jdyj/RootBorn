using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Minigames;
using Rootborn.Core.World;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Hud;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>
    /// HOUSE-001 화면: 가구 배치 화면의 "꾸미기" 분류(벽지·바닥 카드, 방 채우기·칸 붓, 월드 위 미리 보기, 누르면 바뀜).
    /// 실제 data/와 실제 집 맵(game/world/maps.json의 House — 출처표는 World 로더), 임시 폴더 세이브, 창 없음.
    /// 돈 주기는 준비 단계(GameSession 가방 명령 직접). 자동 테스트다(화면에 보인다는 근거 아님 — 그것은 Host HouseFinishHostTests).
    /// </summary>
    public sealed class FinishPaletteScreenTests
    {
        private const string Allowance = "item.allowance";

        private static readonly Lazy<LoadedMap> _house = new Lazy<LoadedMap>(() => WorldMapLoader.FromRepo(TestData.RepoRoot).LoadMap("House"));
        private static LoadedMap House => _house.Value;

        private static HouseBuildFrame Frame()
        {
            var build = House.Entry.Build;
            Assert.That(build, Is.Not.Null, "전제: 집 맵은 짓는 집");
            Assert.That(House.Tiles, Is.Not.Null, "전제: 출처표가 있다");
            return new HouseBuildFrame(build.FrameId, House.Tiles, build.DefaultWall, build.DefaultFloor, build.BandColors);
        }

        /// <summary>시험 뷰: 칸 24px로 집 전체가 화면 가운데(팔레트·오른쪽 패널 사이)에 들어간다. 받은 미리 보기를 센다.</summary>
        internal sealed class FinishView : IPlacementView
        {
            public const float OriginX = 480, OriginY = 120, Cell = 24;
            public FinishView(FurnitureRoom room) { Room = room; }
            public FurnitureRoom Room { get; }
            public readonly List<HouseFinishPreview> Shown = new List<HouseFinishPreview>();
            public GridPoint? Facing;

            public bool TryCellAt(float x, float y, out GridPoint cell)
            {
                int mx = (int)Math.Floor((x - OriginX) / Cell), my = (int)Math.Floor((y - OriginY) / Cell);
                cell = Room.ToPlacement(new CellPos(mx, my));
                return Room.Map.Grid.Contains(mx, my);
            }

            public UiRect CellRect(GridPoint cell)
            {
                CellPos c = Room.ToMap(cell);
                return new UiRect(OriginX + c.X * Cell, OriginY + c.Y * Cell, Cell, Cell);
            }

            public bool TryFacingCell(out GridPoint cell)
            {
                cell = Facing ?? default;
                return Facing.HasValue;
            }

            public void ShowFinishPreview(HouseFinishPreview preview) => Shown.Add(preview);

            public (float X, float Y) Center(CellPos mapCell)
            {
                UiRect r = CellRect(Room.ToPlacement(mapCell));
                return (r.CenterX, r.CenterY);
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly TestSession T;
            public readonly FurnitureRoom Room = new FurnitureRoom(House.World);
            public readonly FinishView View;
            public readonly FurniturePlacementScreen Screen;
            public readonly HouseBuildFrame Frame = FinishPaletteScreenTests.Frame();
            public readonly RoomFill Fill;

            public Fixture(string language = "ko", GameText text = null, Rootborn.Core.Room.RoomShares shares = null)
            {
                T = new TestSession(language, playerId: shares == null ? null : "guest", shares: shares, text: text);
                View = new FinishView(Room);
                Fill = RoomFill.Build(Frame.Tiles.Grid);
                T.Session.EnterHouse(Room.Grid, "house");
                T.Session.EnterHouseBuild(Frame);
                T.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
                T.Stack.OpenHud();
                T.Click("hud.button.placement");
                Screen = T.Screen<FurniturePlacementScreen>(ScreenCatalog.FurniturePlacement);
                Screen.Attach(new FurnitureArt(TestData.DataDirectory), View);
                T.Emit();
            }

            public void Pointer(float x, float y, bool down)
            {
                T.Stack.Pointer(x, y, down, 0);
                Screen.WorldPointer(x, y, down, T.Stack.Tree.HitTest(x, y) != null);
            }

            public void Hover(CellPos cell)
            {
                (float x, float y) = View.Center(cell);
                Assert.That(T.Stack.Tree.HitTest(x, y), Is.Null, "준비: 그 칸 위에 UI가 없다 " + cell);
                Pointer(x, y, false);
            }

            public void ClickCell(CellPos cell)
            {
                Hover(cell);
                (float x, float y) = View.Center(cell);
                Pointer(x, y, true);
                Pointer(x, y, false);
            }

            public void Decorate() => T.Click(FurniturePlacementScreen.CategoryFinishId);

            /// <summary>그 꾸밈의 카드를 누른다(지금 탭의 쪽에서 찾는다).</summary>
            public void PickCard(string finishId)
            {
                int n = Enumerable.Range(0, FurniturePlacementScreen.Metrics.PageSize).First(i => Screen.Finishes.At(i)?.Definition.Id == finishId);
                T.Click(FurniturePlacementScreen.FinishCardId(n));
            }

            public int Money => T.Session.Inventory.CountOf(T.Session.Item(Allowance));
            public void GiveMoney(int amount) => T.Session.Inventory.Add(T.Session.Item(Allowance), amount); // 준비 단계

            public string Text(string id) => T.Node(id) switch
            {
                UiTextBlock b => b.Text,
                UiLabel l => l.Text,
                _ => throw new InvalidOperationException(id),
            };

            /// <summary>행 순서로 첫 바닥 칸의 방(왼쪽 위 방, 문간 포함)과 그 방에서 문간 아닌 칸 둘.</summary>
            public (IReadOnlyList<CellPos> Room, CellPos A, CellPos B) FirstRoom() => RoomFrom(FirstFloor(c => true));

            /// <summary>첫 방과 다른 방의 문간 아닌 칸.</summary>
            public CellPos OtherRoomCell()
            {
                var first = new HashSet<CellPos>(FirstRoom().Room);
                return FirstFloor(c => !first.Contains(c) && !Fill.IsDoorway(c));
            }

            private CellPos FirstFloor(Func<CellPos, bool> want)
            {
                RoomGrid grid = Frame.Tiles.Grid;
                for (int y = 0; y < grid.Rows; y++)
                    for (int x = 0; x < grid.Columns; x++)
                        if (grid.At(x, y) == RoomCells.Floor && want(new CellPos(x, y))) return new CellPos(x, y);
                throw new InvalidOperationException("바닥이 없다");
            }

            private (IReadOnlyList<CellPos>, CellPos, CellPos) RoomFrom(CellPos start)
            {
                IReadOnlyList<CellPos> room = Fill.RoomOf(start);
                var inner = room.Where(c => !Fill.IsDoorway(c)).ToList();
                return (room, inner[inner.Count / 2], inner[inner.Count / 2 + 1]);
            }

            /// <summary>첫 방의 벽 정면 칸(출처표에 그 방이 주인인 정면 부분이 있는 벽 칸).</summary>
            public CellPos FirstRoomWallFace()
            {
                var room = new HashSet<CellPos>(FirstRoom().Room);
                return Frame.Tiles.Cells.Where(c => Frame.Tiles.Grid.At(c.Cell.X, c.Cell.Y) == RoomCells.Wall)
                    .First(c => c.Parts.Any(p => p.HasOwner && p.Source == TileSources.Face && room.Contains(new CellPos(c.Cell.X + p.Owner.X, c.Cell.Y + p.Owner.Y)))).Cell;
            }

            public void Dispose() => T.Dispose();
        }

        private static RoomFinishDefinition FreeNonDefault(Fixture f, string slot)
        {
            string fallback = slot == RoomFinishSlots.Wall ? f.Frame.DefaultWall : f.Frame.DefaultFloor;
            return f.T.Session.HouseFinishOptions(slot).First(d => d.UnlockToken.Length == 0 && d.Id != fallback);
        }

        private static RoomFinishDefinition Locked(Fixture f, string slot) => f.T.Session.HouseFinishOptions(slot).First(d => d.UnlockToken.Length > 0);

        private static int PerCell(RoomFinishDefinition d) => d.Cost.Where(c => c.Item == Allowance).Sum(c => c.Count);

        [Test]
        public void Decorate_TitleIsDecorate_InfoStartsUnderTheTitle_FurnitureToolbarsHidden()
        {
            using var f = new Fixture();
            var title = (UiLabel)f.T.Node("furniture.settings.title");
            Assert.That(title.Text, Is.EqualTo(f.T.Text.Get("ui.furniture.title")));
            Assert.That(f.T.Node(FurniturePlacementScreen.ToolbarId).IsActive && f.T.Node(FurniturePlacementScreen.BottomBarId).IsActive, Is.True);

            f.Decorate();
            f.T.Emit();
            Assert.That(title.Text, Is.EqualTo(f.T.Text.Get("ui.furniture.category.finish")), "꾸미기면 오른쪽 패널 제목도 꾸미기");
            UiRect name = f.T.Node(FurniturePlacementScreen.FinishNameId).Rect;
            Assert.That(name.Y, Is.GreaterThanOrEqualTo(title.Rect.Bottom), "이름은 제목 아래");
            Assert.That(name.Y - title.Rect.Bottom, Is.LessThanOrEqualTo(FurniturePlacementScreen.Metrics.FinishInfoGap + 0.5f), "제목 바로 아래부터");
            string[] lines = { FurniturePlacementScreen.FinishNameId, FurniturePlacementScreen.FinishPriceId, FurniturePlacementScreen.FinishCellsId,
                FurniturePlacementScreen.FinishCostId, FurniturePlacementScreen.FinishMoneyId, FurniturePlacementScreen.FinishReasonId };
            for (int i = 1; i < lines.Length; i++)
                Assert.That(f.T.Node(lines[i]).Rect.Y, Is.GreaterThanOrEqualTo(f.T.Node(lines[i - 1]).Rect.Bottom), lines[i] + "는 앞 줄 아래");
            Assert.That(f.T.Node(FurniturePlacementScreen.ToolbarId).IsActive, Is.False, "가구 단추(삭제·회전)는 숨는다");
            Assert.That(f.T.Node(FurniturePlacementScreen.BottomBarId).IsActive, Is.False, "가구 단추(이동)는 숨는다");
            Assert.That(f.T.Node(FurniturePlacementScreen.RoomStatusId).IsActive, Is.False, "같이 쓰는 집의 가구 요청 줄도 숨는다");

            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            Assert.That(title.Text, Is.EqualTo(f.T.Text.Get("ui.furniture.title")));
            Assert.That(f.T.Node(FurniturePlacementScreen.ToolbarId).IsActive && f.T.Node(FurniturePlacementScreen.BottomBarId).IsActive, Is.True, "돌아가면 다시 보인다");
            f.T.Click(FurniturePlacementScreen.CategoryFurnitureId);
            Assert.That(f.T.Node(FurniturePlacementScreen.ToolbarId).IsActive && f.T.Node(FurniturePlacementScreen.BottomBarId).IsActive, Is.True);
        }

        [Test]
        public void HiddenToolbars_UncoverTheHud_AndCoverItAgainOnTheWayBack()
        {
            // 가구 단추 줄이 숨으면 그 밑의 HUD(시계)가 덮이지 않고, 돌아가면 그 자리에서 다시 덮인다(분류를 바꾼 그 누름에서 — 다음 크기 바꿈을 기다리지 않는다).
            using var f = new Fixture();
            List<UiNode> Covered() => Walk(f.T.Stack.Tree.Root).Where(n => n.Covered).ToList();
            var before = Covered();
            UiRect toolbar = f.T.Node(FurniturePlacementScreen.ToolbarId).Rect;
            var underToolbar = before.Where(n => n.Rect.X < toolbar.Right && toolbar.X < n.Rect.Right && n.Rect.Y < toolbar.Bottom && toolbar.Y < n.Rect.Bottom).ToList();
            Assume.That(underToolbar, Is.Not.Empty, "가구 단추 줄 밑에 덮인 HUD가 있다");

            f.Decorate();
            Assert.That(underToolbar.Any(n => n.Covered), Is.False, "꾸미기: 단추 줄이 숨었으니 그 밑 HUD는 덮이지 않는다");
            f.T.Click(FurniturePlacementScreen.CategoryFurnitureId);
            Assert.That(Covered(), Is.EquivalentTo(before), "가구 분류로 돌아가면 다시 덮인다");
        }

        [Test]
        public void LockedCard_HasADarkShade_AndStillClicks()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Emit();
            RoomFinishDefinition locked = Locked(f, RoomFinishSlots.Wall), free = FreeNonDefault(f, RoomFinishSlots.Wall);
            int lockedCard = Enumerable.Range(0, 20).First(i => f.Screen.Finishes.At(i)?.Definition == locked);
            int freeCard = Enumerable.Range(0, 20).First(i => f.Screen.Finishes.At(i)?.Definition == free);
            UiNode shade = f.T.Node(FurniturePlacementScreen.FinishCardId(lockedCard) + ".lock");
            Assert.That(shade.IsVisibleInTree(), Is.True, "잠긴 카드는 어둡게 덮는다");
            Assert.That(f.T.Node(FurniturePlacementScreen.FinishCardId(freeCard) + ".lock").IsVisibleInTree(), Is.False);
            var list = new DrawList();
            shade.Emit(list, new TestMeasurer());
            Assert.That(list.Commands.Single().Color, Is.EqualTo(FurniturePlacementScreen.Metrics.FinishLockedShade));
            Assert.That(shade.Rect, Is.EqualTo(f.T.Node(FurniturePlacementScreen.FinishCardId(lockedCard)).Rect), "카드 전체를 덮는다");
            f.PickCard(locked.Id); // 덮개 위를 눌러도 카드가 눌린다(TestSession.Click이 맨 위 요소를 본다)
            Assert.That(f.Screen.PickedFinish, Is.SameAs(locked));
        }

        [Test]
        public void CardPieces_AreWholeMultiplesOfThePiece_AndFitTheCard()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Emit();
            const float piece = FurniturePlacementScreen.Metrics.FinishPieceSource;
            UiRect card = f.T.Node(FurniturePlacementScreen.FinishCardId(0)).Rect;
            foreach (string id in new[] { ".icon", ".icon2" })
            {
                UiRect r = ((UiFittedImage)f.T.Node(FurniturePlacementScreen.FinishCardId(0) + id)).FittedRect;
                Assert.That(r.W % piece, Is.EqualTo(0f), "벽 조각은 정수 배율 " + id);
                Assert.That(r.W, Is.GreaterThanOrEqualTo(piece), "조각 원래 크기 이상");
                Assert.That(r.X >= card.X && r.Right <= card.Right && r.Y >= card.Y && r.Bottom <= card.Bottom, Is.True, "카드 안 " + id);
            }
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            f.T.Emit();
            UiRect floor = ((UiFittedImage)f.T.Node(FurniturePlacementScreen.FinishCardId(0) + ".icon")).FittedRect;
            Assert.That(floor.W % piece, Is.EqualTo(0f));
            Assert.That(floor.W, Is.GreaterThanOrEqualTo(piece * 2), "바닥 조각은 두 배 이상");
            Assert.That(floor.X >= card.X && floor.Right <= card.Right && floor.Y >= card.Y && floor.Bottom <= card.Bottom, Is.True);
        }

        [Test]
        public void Categories_ToggleTheirOwnNodes()
        {
            using var f = new Fixture();
            Assert.That(f.Screen.FinishShown, Is.False);
            Assert.That(f.T.Node(FurniturePlacementScreen.FinishGroupId).IsActive, Is.False, "가구 분류에서는 꾸미기 묶음이 없다");
            Assert.That(f.T.Node(FurniturePlacementScreen.FinishInfoId).IsActive, Is.False);

            f.Decorate();
            Assert.That(f.Screen.FinishShown, Is.True);
            Assert.That(f.Screen.StoredShown, Is.False);
            Assert.That(f.T.Node(FurniturePlacementScreen.FinishGroupId).IsActive, Is.True);
            Assert.That(f.T.Node(FurniturePlacementScreen.FinishInfoId).IsActive, Is.True);
            Assert.That(f.T.Node(FurniturePlacementScreen.SlotId(0)).IsActive, Is.False, "가구 칸은 숨는다");
            Assert.That(f.T.Node(FurniturePlacementScreen.StoredListId).IsActive, Is.False);
            Assert.That(f.T.Node(FurniturePlacementScreen.SelectedNameId).IsActive, Is.False, "오른쪽 패널의 가구 정보는 숨는다");
            var buttons = new[] { FurniturePlacementScreen.CategoryFurnitureId, FurniturePlacementScreen.CategoryStoredId, FurniturePlacementScreen.CategoryFinishId }
                .Select(id => ((FurnitureCategoryButton)f.T.Node(id)).Selected).ToArray();
            Assert.That(buttons, Is.EqualTo(new[] { false, false, true }), "고른 분류 표시");

            f.T.Click(FurniturePlacementScreen.CategoryStoredId);
            Assert.That((f.Screen.FinishShown, f.Screen.StoredShown), Is.EqualTo((false, true)));
            Assert.That(f.T.Node(FurniturePlacementScreen.FinishGroupId).IsActive, Is.False);
            Assert.That(f.T.Node(FurniturePlacementScreen.StoredListId).IsActive || f.T.Node(FurniturePlacementScreen.StoredEmptyId).IsActive, Is.True);

            f.T.Click(FurniturePlacementScreen.CategoryFurnitureId);
            Assert.That((f.Screen.FinishShown, f.Screen.StoredShown), Is.EqualTo((false, false)));
            Assert.That(f.T.Node(FurniturePlacementScreen.SlotId(0)).IsActive, Is.True);
            Assert.That(f.T.Node(FurniturePlacementScreen.SelectedNameId).IsActive, Is.True);
            Assert.That(f.T.Node(FurniturePlacementScreen.FinishInfoId).IsActive, Is.False);
        }

        [Test]
        public void WallTab_ShowsWallFinishes_FloorTab_FloorFinishes()
        {
            using var f = new Fixture();
            f.Decorate();
            int walls = f.T.Session.HouseFinishOptions(RoomFinishSlots.Wall).Count, floors = f.T.Session.HouseFinishOptions(RoomFinishSlots.Floor).Count;
            Assert.That((walls, floors), Is.Not.EqualTo((0, 0)));

            Assert.That(f.Screen.Finishes.Slot, Is.EqualTo(RoomFinishSlots.Wall), "벽지 탭에서 시작");
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishTabWallId)).Selected, Is.True);
            Assert.That(Visible(f), Is.EqualTo(walls), "벽 꾸밈 수만큼 카드");
            for (int i = 0; i < walls; i++)
            {
                var upper = (UiFittedImage)f.T.Node(FurniturePlacementScreen.FinishCardId(i) + ".icon");
                var lower = (UiFittedImage)f.T.Node(FurniturePlacementScreen.FinishCardId(i) + ".icon2");
                RoomFinishDefinition d = f.Screen.Finishes.At(i).Definition;
                Assert.That(d.Slot, Is.EqualTo(RoomFinishSlots.Wall));
                Assert.That(upper.Image, Is.EqualTo(d.Sheet + "#" + d.Art + "/" + RoomFinishPieces.FaceUpper), "벽 카드 위는 정면 윗조각");
                Assert.That(lower.Image, Is.EqualTo(d.Sheet + "#" + d.Art + "/" + RoomFinishPieces.FaceLower), "아래는 정면 아랫조각");
                Assert.That(upper.Rect.CenterY, Is.LessThan(lower.Rect.CenterY), "위아래로");
            }

            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            Assert.That(f.Screen.Finishes.Slot, Is.EqualTo(RoomFinishSlots.Floor));
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishTabFloorId)).Selected, Is.True);
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishTabWallId)).Selected, Is.False);
            Assert.That(Visible(f), Is.EqualTo(floors));
            for (int i = 0; i < floors; i++)
            {
                RoomFinishDefinition d = f.Screen.Finishes.At(i).Definition;
                Assert.That(d.Slot, Is.EqualTo(RoomFinishSlots.Floor));
                Assert.That(((UiFittedImage)f.T.Node(FurniturePlacementScreen.FinishCardId(i) + ".icon")).Image, Is.EqualTo(d.Sheet + "#" + d.Art + "/" + RoomFinishPieces.Floor));
                Assert.That(f.T.Node(FurniturePlacementScreen.FinishCardId(i) + ".icon2").IsActive, Is.False, "바닥 카드는 그림 하나");
            }
            Assert.That(f.Screen.Root.Find(FurniturePlacementScreen.FinishCardId(FurniturePlacementScreen.Metrics.FinishPageSize)), Is.Null, "카드는 한 쪽만큼만 만든다");
        }

        private static int Visible(Fixture f) => Enumerable.Range(0, FurniturePlacementScreen.Metrics.FinishPageSize).Count(i => f.T.Node(FurniturePlacementScreen.FinishCardId(i)).IsActive);

        [Test]
        public void LockedCard_IsDimmed_AndPickingShowsLockedText()
        {
            using var f = new Fixture();
            f.Decorate();
            RoomFinishDefinition locked = Locked(f, RoomFinishSlots.Wall), free = FreeNonDefault(f, RoomFinishSlots.Wall);
            int lockedCard = Enumerable.Range(0, 20).First(i => f.Screen.Finishes.At(i)?.Definition == locked);
            int freeCard = Enumerable.Range(0, 20).First(i => f.Screen.Finishes.At(i)?.Definition == free);
            Assert.That(((UiSlot)f.T.Node(FurniturePlacementScreen.FinishCardId(lockedCard))).Dimmed, Is.True, "잠긴 카드는 흐리다");
            Assert.That(((UiSlot)f.T.Node(FurniturePlacementScreen.FinishCardId(freeCard))).Dimmed, Is.False);

            f.PickCard(locked.Id);
            Assert.That(f.Screen.PickedFinish, Is.SameAs(locked), "잠겨도 골라 볼 수 있다");
            Assert.That(f.Text(FurniturePlacementScreen.FinishNameId), Is.EqualTo(f.T.Text.Get(locked.NameKey)));
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(HouseFinishReasons.Locked)));
            Assert.That(((UiSlot)f.T.Node(FurniturePlacementScreen.FinishCardId(lockedCard))).Selected, Is.True);

            // 방을 가리키면 Core의 잠김 미리 보기: 월드 그림은 덮지 않고 바뀔 칸만 안 되는 색으로.
            var (room, a, _) = f.FirstRoom();
            f.Hover(a);
            Assert.That(f.Screen.FinishPreview.ReasonKey, Is.EqualTo(HouseFinishReasons.Locked));
            Assert.That(f.View.Shown.Where(p => p != null), Is.Empty, "거절된 미리 보기는 월드에 그리지 않는다");
            var list = new DrawList();
            f.Screen.EmitWorldOverlay(list);
            Assert.That(list.Commands.Count(c => c.Kind == DrawKind.Fill && c.Color.Equals(FurniturePlacementScreen.Metrics.InvalidFootprint)), Is.EqualTo(f.Screen.FinishPreview.HighlightCells.Count).And.GreaterThan(0), "벽지는 Core가 정한 정면 벽 칸을");
            Assert.That(f.Screen.FinishPreview.ChangedCells.Count, Is.GreaterThan(0));
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(HouseFinishReasons.Locked)));

            f.PickCard(free.Id);
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.Not.EqualTo(f.T.Text.Get(HouseFinishReasons.Locked)));
        }

        [Test]
        public void HoverRoom_ShowsCellsCostMoney()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            var (room, a, _) = f.FirstRoom();
            int cost = PerCell(floor) * room.Count;
            f.GiveMoney(cost + 3);
            f.PickCard(floor.Id);
            string itemName = f.T.Text.Get(f.T.Session.Item(Allowance).DisplayKey);
            Assert.That(f.Text(FurniturePlacementScreen.FinishPriceId), Is.EqualTo(PerCell(floor) == 1
                ? f.T.Text.Format("ui.house-build.price.one", 1, itemName) : f.T.Text.Format("ui.house-build.price", PerCell(floor), itemName)));
            Assert.That(f.Text(FurniturePlacementScreen.FinishCellsId), Is.Empty, "가리키기 전에는 바뀌는 칸이 없다");

            f.Hover(a);
            HouseFinishPreview preview = f.Screen.FinishPreview;
            Assert.That(preview.CanApply, Is.True, preview.ReasonKey);
            Assert.That(preview.ChangedCells.Count, Is.EqualTo(room.Count), "방 채우기는 방 + 맞닿은 문간");
            Assert.That(f.Text(FurniturePlacementScreen.FinishCellsId), Is.EqualTo(f.T.Text.Format("ui.house-build.cells", room.Count)));
            Assert.That(f.Text(FurniturePlacementScreen.FinishCostId), Is.EqualTo(f.T.Text.Format("ui.house-build.cost", cost, itemName)));
            Assert.That(f.Text(FurniturePlacementScreen.FinishMoneyId), Is.EqualTo(f.T.Text.Format("ui.house-build.money", f.Money, itemName)));
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.Empty);
            Assert.That(f.View.Shown.Last(), Is.SameAs(preview), "적용할 수 있는 미리 보기는 월드에 그린다");
            var list = new DrawList();
            f.Screen.EmitWorldOverlay(list);
            Assert.That(list.Commands.Count(c => c.Kind == DrawKind.Fill && c.Color.Equals(FurniturePlacementScreen.Metrics.FinishValidCell)), Is.EqualTo(room.Count), "바뀔 칸을 옅게 밝힌다(미리 보기 그림이 보이게)");

            // 돈이 모자라면 Core의 이유. 월드에는 그리지 않고, 바뀔 칸만 안 되는 색으로. (가방 명령을 직접 부르는 준비 단계는 알림이 없어
            // 같은 칸에서는 다시 묻지 않는다 — 다른 방을 가리켜 묻게 한다.)
            f.T.Session.Inventory.Remove(f.T.Session.Item(Allowance), f.Money);
            Assert.That(f.Money, Is.EqualTo(0), "준비: 돈을 다 뺐다");
            f.Hover(f.OtherRoomCell());
            Assert.That(f.Screen.FinishPreview.ReasonKey, Is.EqualTo(HouseFinishReasons.NotEnoughMoney));
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(HouseFinishReasons.NotEnoughMoney)));
            Assert.That(f.View.Shown.Last(), Is.Null, "거절된 미리 보기는 월드 그림을 지운다");
            Assert.That(f.Text(FurniturePlacementScreen.FinishMoneyId), Is.EqualTo(f.T.Text.Format("ui.house-build.money", 0, itemName)));
            list = new DrawList();
            f.Screen.EmitWorldOverlay(list);
            Assert.That(list.Commands.Count(c => c.Kind == DrawKind.Fill && c.Color.Equals(FurniturePlacementScreen.Metrics.InvalidFootprint)),
                Is.EqualTo(f.Screen.FinishPreview.ChangedCells.Count).And.GreaterThan(0));
        }

        [Test]
        public void UnrelatedNotices_SameCell_DoNotAskAgain()
        {
            // 미리 보기 결과와 무관한 알림(흐르는 시간은 게임 1분마다 time을 알린다)은 같은 칸에서 다시 묻게 하지 않는다.
            using var f = new Fixture();
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            var (room, a, _) = f.FirstRoom();
            f.GiveMoney(PerCell(floor) * room.Count);
            f.PickCard(floor.Id);
            f.Hover(a);
            int queries = f.Screen.FinishPreviewQueries;
            foreach (string topic in new[] { Rootborn.Game.GameChangeTopics.Time, ChangeTopics.Student, ChangeTopics.Quests, ChangeTopics.Day,
                         ChangeTopics.Dialogue, ChangeTopics.Objectives, ChangeTopics.Display })
                f.T.Stack.NotifyChanged(topic);
            f.Hover(a);
            Assert.That(f.Screen.FinishPreviewQueries, Is.EqualTo(queries), "무관한 알림 뒤 같은 칸이면 다시 묻지 않는다");
        }

        [Test]
        public void NoTarget_NoHighlight_NothingOnTheWorld()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            f.GiveMoney(PerCell(floor) * 200);
            f.PickCard(floor.Id);
            // 바닥도 벽 정면·좁은 부분도 아닌 칸(벽 윗면 띠만 있는 칸, 또는 밖).
            CellPos none = f.Frame.Tiles.Cells.Where(c => f.Frame.Tiles.Grid.At(c.Cell.X, c.Cell.Y) == RoomCells.Wall
                    && !c.Parts.Any(p => p.HasOwner && (p.Source == TileSources.Face || p.Source == TileSources.Narrow)))
                .Select(c => c.Cell).First();
            f.Hover(none);
            Assert.That(f.Screen.FinishPreview.ReasonKey, Is.EqualTo(HouseFinishReasons.NoTarget));
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(HouseFinishReasons.NoTarget)));
            var list = new DrawList();
            f.Screen.EmitWorldOverlay(list);
            Assert.That(list.Commands.Count(c => c.Kind == DrawKind.Fill), Is.EqualTo(0), "대상이 없으면 밝히지 않는다");
            Assert.That(f.View.Shown.Where(p => p != null), Is.Empty, "월드에 넘기지 않는다");
        }

        [Test]
        public void MoneyNotice_WhileHovering_AsksAgainOnce()
        {
            // 돈이 GameSession 명령으로 바뀌면(알림) 같은 칸에 있어도 다음 포인터 프레임에 한 번 다시 묻는다.
            using var f = new Fixture();
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            var (room, a, _) = f.FirstRoom();
            f.GiveMoney(PerCell(floor) * room.Count);
            f.PickCard(floor.Id);
            f.Hover(a);
            Assert.That(f.Screen.FinishPreview.CanApply, Is.True);
            int queries = f.Screen.FinishPreviewQueries;
            f.T.Stack.NotifyChanged(ChangeTopics.Inventory); // 가방이 바뀌었다는 알림(GameSession.Changed와 같은 길)
            f.Hover(a);
            f.Hover(a);
            Assert.That(f.Screen.FinishPreviewQueries, Is.EqualTo(queries + 1), "알림 뒤 한 번만");
        }

        [Test]
        public void Click_Applies_ThenNothingToChange()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            var (room, a, _) = f.FirstRoom();
            int cost = PerCell(floor) * room.Count;
            f.GiveMoney(cost);
            int money = f.Money;
            f.PickCard(floor.Id);
            f.Hover(a);
            int shown = f.View.Shown.Count;
            Assert.That(f.View.Shown.Last(), Is.Not.Null);

            f.ClickCell(a);
            Assert.That(f.T.Session.HouseFloorAt(a), Is.EqualTo(floor.Id), "누르면 바뀐다");
            Assert.That(room.All(c => f.T.Session.HouseFloorAt(c) == floor.Id), Is.True, "방 전체");
            Assert.That(f.Money, Is.EqualTo(money - cost));
            Assert.That(f.View.Shown.Count, Is.EqualTo(shown + 1), "적용한 그 업데이트에서 미리 보기를 한 번 지운다");
            Assert.That(f.View.Shown.Last(), Is.Null);
            Assert.That(f.Screen.FinishPreview.ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(HouseFinishReasons.NothingToChange)));
            var list = new DrawList();
            f.Screen.EmitWorldOverlay(list);
            Assert.That(list.Commands.Count(c => c.Kind == DrawKind.Fill), Is.EqualTo(0), "바뀔 칸이 없으면 밝히지 않는다");

            f.ClickCell(a);
            Assert.That(f.Money, Is.EqualTo(money - cost), "같은 요청을 다시 눌러도 돈이 들지 않는다");
            Assert.That(f.View.Shown.Count, Is.EqualTo(shown + 1), "같은 미리 보기면 월드에 다시 넘기지 않는다");
        }

        [Test]
        public void BrushTool_OneCell()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            f.T.Click(FurniturePlacementScreen.FinishToolBrushId);
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishToolBrushId)).Selected, Is.True);
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishToolRoomId)).Selected, Is.False);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            f.GiveMoney(PerCell(floor));
            f.PickCard(floor.Id);
            var (_, a, b) = f.FirstRoom();
            f.Hover(a);
            Assert.That(f.Screen.FinishPreview.ChangedCells, Is.EqualTo(new[] { a }));
            Assert.That(f.Text(FurniturePlacementScreen.FinishCellsId), Is.EqualTo(f.T.Text.Get("ui.house-build.cells.one")));
            f.ClickCell(a);
            Assert.That(f.T.Session.HouseFloorAt(a), Is.EqualTo(floor.Id));
            Assert.That(f.T.Session.HouseFloorAt(b), Is.EqualTo(f.Frame.DefaultFloor), "옆 칸은 그대로");
        }

        [Test]
        public void PreviewChangesOnlyWhenCellsChange()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            f.GiveMoney(PerCell(floor) * 400);
            f.PickCard(floor.Id);
            var (_, a, b) = f.FirstRoom();

            f.Hover(a);
            HouseFinishPreview first = f.Screen.FinishPreview;
            int queries = f.Screen.FinishPreviewQueries;
            Assert.That(f.View.Shown.Count, Is.EqualTo(1));
            for (int i = 0; i < 5; i++)
            {
                (float x, float y) = f.View.Center(a);
                f.Pointer(x + (i - 2) * 2, y, false); // 같은 칸 안에서 움직이기
            }
            Assert.That(f.Screen.FinishPreviewQueries, Is.EqualTo(queries), "같은 칸이면 미리 보기를 다시 묻지 않는다");

            f.Hover(b);
            Assert.That(f.Screen.FinishPreviewQueries, Is.EqualTo(queries + 1), "칸이 바뀌면 한 번 묻는다");
            Assert.That(f.Screen.FinishPreview, Is.SameAs(first), "같은 방 안이면 같은 미리 보기");
            Assert.That(f.View.Shown.Count, Is.EqualTo(1), "월드에 다시 넘기지 않는다");

            f.Hover(f.OtherRoomCell());
            Assert.That(f.Screen.FinishPreview, Is.Not.SameAs(first), "다른 방이면 바뀐다");
            Assert.That(f.View.Shown.Count, Is.EqualTo(2));

            // 포인터가 UI 위로 가면 미리 보기를 지운다.
            UiRect palette = f.T.Node(FurniturePlacementScreen.PaletteId).Rect;
            f.Pointer(palette.CenterX, palette.CenterY, false);
            Assert.That(f.View.Shown.Last(), Is.Null);
            Assert.That(f.Screen.FinishPreview, Is.Null);
        }

        [Test]
        public void WallFace_PointsAtItsRoom_AndChangesTheWall()
        {
            using var f = new Fixture();
            f.Decorate();
            RoomFinishDefinition wall = FreeNonDefault(f, RoomFinishSlots.Wall);
            f.GiveMoney(PerCell(wall) * 400);
            f.PickCard(wall.Id);
            CellPos face = f.FirstRoomWallFace();
            var room = new HashSet<CellPos>(f.FirstRoom().Room);
            f.Hover(face);
            HouseFinishPreview preview = f.Screen.FinishPreview;
            Assert.That(preview.CanApply, Is.True, preview.ReasonKey);
            Assert.That(room.Contains(preview.Target), Is.True, "벽 정면을 가리키면 그 정면 주인 방");
            f.ClickCell(face);
            Assert.That(preview.ChangedCells.All(c => f.T.Session.HouseWallAt(c) == wall.Id), Is.True);
        }

        [Test]
        public void WallPreview_HighlightsTheWallFaces_NotTheFloorBelow()
        {
            using var f = new Fixture();
            f.Decorate();
            RoomFinishDefinition wall = FreeNonDefault(f, RoomFinishSlots.Wall);
            f.GiveMoney(PerCell(wall) * 400);
            f.PickCard(wall.Id);
            f.Hover(f.FirstRoomWallFace());
            HouseFinishPreview preview = f.Screen.FinishPreview;
            Assert.That(preview.CanApply, Is.True, preview.ReasonKey);
            var list = new DrawList();
            f.Screen.EmitWorldOverlay(list);
            var filled = list.Commands.Where(c => c.Kind == DrawKind.Fill).Select(c => c.Rect).ToList();
            var faces = preview.HighlightCells.Select(c => f.View.CellRect(f.Room.ToPlacement(c))).ToList();
            var owners = preview.ChangedCells.Select(c => f.View.CellRect(f.Room.ToPlacement(c))).ToList();
            Assert.That(filled, Is.EquivalentTo(faces), "Core가 정한 밝힐 칸(정면 벽 칸)만");
            Assert.That(filled.Intersect(owners), Is.Empty, "아래 바닥 줄(주인 칸)은 밝히지 않는다");
        }

        [Test]
        public void InteractKey_PressesTheFacingCell()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            var (room, a, _) = f.FirstRoom();
            f.GiveMoney(PerCell(floor) * room.Count);
            f.PickCard(floor.Id);
            f.View.Facing = f.Room.ToPlacement(a);
            Assert.That(f.T.Stack.Key(ScreenKey.Interact, false), Is.True, "상호작용 키를 꾸미기가 가져간다");
            Assert.That(f.T.Session.HouseFloorAt(a), Is.EqualTo(floor.Id));
        }

        /// <summary>
        /// 돈만 같이 쓰는 방(집은 각자): 미리 보기는 방 지갑으로 판단하고(붉지 않다), 누르면 방의 허락을 기다린다는 문구가 이유 줄에 보이며
        /// 기다리는 동안 다시 눌러도 요청이 겹치지 않는다. 허락이 오면 문구가 사라지고 꾸밈이 바뀐다. 방 쪽은 Core RoomPieceSet을 테스트가 손으로 돌린다.
        /// </summary>
        [Test]
        public void MoneySharingRoom_PreviewFollowsTheRoomWallet_PressWaitsForTheRoom()
        {
            var probe = new Fixture();
            RoomFinishDefinition floor = FreeNonDefault(probe, RoomFinishSlots.Floor);
            var (room, a, b) = probe.FirstRoom();
            probe.Dispose();
            int cost = PerCell(floor) * room.Count;
            Assert.That(cost, Is.GreaterThan(0), "전제: 값이 있는 꾸밈");

            var pieces = new Rootborn.Core.Room.RoomPieceSet(new Rootborn.Core.Room.RoomShareSettings(new[] { Rootborn.Core.Room.RoomWallet.PieceName }),
                Rootborn.Core.Room.RoomPieceCatalog.CreateDefault());
            var wallet = (Rootborn.Core.Room.RoomWallet)pieces.Pieces.First(p => p.Name == Rootborn.Core.Room.RoomWallet.PieceName);
            int ids = 0;
            var shares = new Rootborn.Core.Room.RoomShares(pieces.Settings, Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(), () => "guest-r" + (++ids));
            void Deliver(string player)
            {
                var answers = new List<Rootborn.Core.Room.RoomAnswer>();
                var changed = new List<Rootborn.Core.Room.IRoomPiece>();
                pieces.Apply(player, shares.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
                foreach (var answer in answers) shares.Receive(answer);
                foreach (var piece in changed) shares.Receive(piece.State());
            }
            // 준비: 다른 사람이 방 지갑에 비용 + 3을 넣었다(요청만).
            var friend = new Rootborn.Core.Room.RoomShares(pieces.Settings, Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(), () => "friend-r1");
            friend.Outbox.Add(new Rootborn.Core.Room.RoomRequestSaveData { Piece = Rootborn.Core.Room.RoomWallet.PieceName, RequestId = "friend-r1", Kind = Rootborn.Core.Room.RoomWallet.Deposit, Amount = cost + 3 });
            pieces.Apply("friend", friend.Outbox.Requests.ToList(), 1, new List<Rootborn.Core.Room.RoomAnswer>(), new List<Rootborn.Core.Room.IRoomPiece>());
            foreach (var p in pieces.Pieces) shares.Receive(p.State());

            using var f = new Fixture(shares: shares);
            Assert.That(f.T.Session.SharesWallet, Is.True, "준비: 돈을 같이 쓰는 방");
            Assert.That(f.T.Session.SharesHouse, Is.False, "준비: 집은 자기 것");
            Assert.That(f.Money, Is.EqualTo(cost + 3), "돈은 방 지갑");
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            f.PickCard(floor.Id);

            f.Hover(a);
            Assert.That(f.Screen.FinishPreview.CanApply, Is.True, f.Screen.FinishPreview.ReasonKey);
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.Empty, "돈이 있으면 이유가 없다");
            Assert.That(f.View.Shown.Last(), Is.SameAs(f.Screen.FinishPreview), "월드 미리 보기가 나온다");

            f.ClickCell(a);
            Assert.That(f.Screen.FinishLastAction, Is.EqualTo("waiting"));
            Assert.That(f.T.Session.WaitingForRoom, Is.True);
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(RoomMoneyText.WaitingKey)), "방의 허락을 기다린다");
            int requests = shares.Outbox.Requests.Count;
            f.ClickCell(b);
            f.ClickCell(a);
            Assert.That(shares.Outbox.Requests.Count, Is.EqualTo(requests), "기다리는 동안 다시 눌러도 요청이 겹치지 않는다");
            Assert.That(f.T.Session.HouseFloorAt(a), Is.EqualTo(f.Frame.DefaultFloor), "허락 전에는 바뀌지 않는다");

            Deliver("guest");
            Assert.That(f.T.Session.WaitingForRoom, Is.False);
            Assert.That(f.T.Session.LastRoomCommandApplied, Is.True);
            Assert.That(f.T.Session.HouseFloorAt(a), Is.EqualTo(floor.Id), "허락이 오면 바뀐다");
            f.Hover(a);
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(HouseFinishReasons.NothingToChange)), "기다림 문구가 사라지고 지금 미리 보기의 이유");
            Deliver("guest");
            Assert.That(wallet.Balance, Is.EqualTo(3), "비용만큼 한 번");
            KnownMissingKeys.AssertOnlyKnownMissing(f.T.Text, new[] { RoomMoneyText.WaitingKey, RoomMoneyText.RefusedKey });
        }

        // ---------- 띠 색 탭 ----------

        private static UiScrollList BandList(Fixture f) => (UiScrollList)f.T.Node(FurniturePlacementScreen.FinishBandListId);

        private static void ClickBandRow(Fixture f, int row)
        {
            f.T.Emit();
            UiScrollList list = BandList(f);
            Assert.That(list.TryGetRowRect(row, out UiRect r), Is.True, "줄이 보인다: " + row);
            Assert.That(f.T.Stack.Tree.HitTest(r.CenterX, r.CenterY), Is.SameAs(list));
            f.T.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
            f.T.Stack.Pointer(r.CenterX, r.CenterY, true, 0);
            f.T.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
        }

        [Test]
        public void BandTab_RowsAndApply()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Emit();
            var tabs = new[] { FurniturePlacementScreen.FinishTabWallId, FurniturePlacementScreen.FinishTabFloorId, FurniturePlacementScreen.FinishTabBandId }
                .Select(id => f.T.Node(id).Rect).ToArray();
            Assert.That(tabs.Select(r => r.Y).Distinct().Count(), Is.EqualTo(1), "탭 셋이 한 줄");
            UiRect palette = f.T.Node(FurniturePlacementScreen.PaletteId).Rect;
            Assert.That(tabs.All(r => r.X >= palette.X && r.Right <= palette.Right), Is.True, "팔레트 안");
            Assert.That(BandList(f).IsActive, Is.False, "벽지 탭에서는 띠 목록이 없다");

            f.T.Click(FurniturePlacementScreen.FinishTabBandId);
            f.T.Emit();
            Assert.That(f.Screen.Finishes.Slot, Is.EqualTo(RoomFinishSlots.Band));
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishTabBandId)).Selected, Is.True);
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishTabWallId)).Selected, Is.False);
            Assert.That(Visible(f), Is.EqualTo(0), "띠 탭은 카드 대신 줄");
            Assert.That(f.T.Node(FurniturePlacementScreen.FinishToolRoomId).IsActive || f.T.Node(FurniturePlacementScreen.FinishToolBrushId).IsActive, Is.False, "집 하나에 하나라 도구가 없다");

            IReadOnlyList<RoomFinishDefinition> bands = f.T.Session.HouseFinishOptions(RoomFinishSlots.Band);
            UiScrollList list = BandList(f);
            Assert.That(list.IsActive, Is.True);
            Assert.That(list.RowCount, Is.EqualTo(bands.Count).And.GreaterThan(1));
            int current = bands.ToList().FindIndex(b => b.Art == f.T.Session.HouseBandColors());
            Assert.That(current, Is.GreaterThanOrEqualTo(0), "전제: 지금 색이 목록에 있다");
            for (int i = 0; i < bands.Count; i++)
                Assert.That(list.RowText(i), Is.EqualTo(i == current
                    ? f.T.Text.Format("ui.house-build.band.current", f.T.Text.Get(bands[i].NameKey)) : f.T.Text.Get(bands[i].NameKey)), "줄 " + i);
            Assert.That(list.SelectedIndex, Is.EqualTo(current), "지금 색 줄을 고른 것으로 보인다");
            Assert.That(f.Text(FurniturePlacementScreen.FinishNameId), Is.EqualTo(f.T.Text.Get(bands[current].NameKey)));
            Assert.That(f.Text(FurniturePlacementScreen.FinishPriceId), Is.EqualTo(f.T.Text.Get("ui.house-build.free")));

            int other = current == 0 ? 1 : 0;
            int money = f.Money;
            ClickBandRow(f, other);
            Assert.That(f.T.Session.HouseBandColors(), Is.EqualTo(bands[other].Art), "줄을 누르면 바로 바뀐다(무료 — 미리 보기 없이)");
            Assert.That(f.Money, Is.EqualTo(money));
            Assert.That(f.Screen.FinishLastAction, Is.EqualTo("applied"));
            Assert.That(list.RowText(other), Is.EqualTo(f.T.Text.Format("ui.house-build.band.current", f.T.Text.Get(bands[other].NameKey))), "지금 표시가 옮겨 간다");
            Assert.That(list.RowText(current), Is.EqualTo(f.T.Text.Get(bands[current].NameKey)));
            Assert.That(list.SelectedIndex, Is.EqualTo(other));
            Assert.That(f.Text(FurniturePlacementScreen.FinishNameId), Is.EqualTo(f.T.Text.Get(bands[other].NameKey)));

            ClickBandRow(f, other);
            Assert.That(f.Screen.FinishLastAction, Is.EqualTo("refused"), "지금 색을 다시 누르면 바뀌는 것 없음");
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(HouseFinishReasons.NothingToChange)));

            // 월드를 가리켜도 미리 보기를 묻지 않는다(고른 카드가 없다)
            var (_, a, _) = f.FirstRoom();
            f.Hover(a);
            Assert.That(f.Screen.FinishPreview, Is.Null);
            Assert.That(f.View.Shown.Where(p => p != null), Is.Empty);

            f.T.Click(FurniturePlacementScreen.FinishTabWallId);
            Assert.That(BandList(f).IsActive, Is.False, "다른 탭으로 가면 띠 목록이 숨는다");
            Assert.That(Visible(f), Is.GreaterThan(0));
            Assert.That(f.T.Node(FurniturePlacementScreen.FinishToolRoomId).IsActive, Is.True);
        }

        private static IEnumerable<string> Languages() => LongText.MergedTables().Keys.OrderBy(k => k);

        [Test]
        public void LongText_StaysInsidePanels([ValueSource(nameof(Languages))] string language)
        {
            using var f = new Fixture(language, LongText.Create(language));
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            f.GiveMoney(5);
            f.PickCard(floor.Id);
            var (_, a, _) = f.FirstRoom();
            f.Hover(a); // 돈 모자람 — 이유 줄까지 채운다
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.Not.Empty);
            var measurer = new WideMeasurer();
            CheckPanels(f, language, measurer);
            f.T.Click(FurniturePlacementScreen.FinishTabBandId); // 띠 탭(줄 목록)도 같은 검사
            CheckPanels(f, language, measurer);
        }

        private static void CheckPanels(Fixture f, string language, WideMeasurer measurer)
        {
            for (int i = 0; i < 2; i++) { f.T.Stack.Layout(1920, 1080); f.T.Stack.Emit(new DrawList(), measurer); }
            f.T.Stack.Layout(1920, 1080);
            foreach (string panelId in new[] { FurniturePlacementScreen.PaletteId, "furniture.settings" })
            {
                UiNode panel = f.T.Node(panelId);
                foreach (UiNode node in Walk(panel).Where(n => n.IsVisibleInTree()))
                {
                    var list = new DrawList();
                    node.Emit(list, measurer);
                    // 요소가 스스로 자른(PushClip) 글은 잘린 안쪽만 보인다(띠 색 줄 목록) — LongTextScreenTests와 같이 보이는 부분만 본다.
                    UiRect? clip = null;
                    foreach (DrawCommand text in list.Commands)
                    {
                        if (text.Kind == DrawKind.PushClip) { clip = text.Rect; continue; }
                        if (text.Kind == DrawKind.PopClip) { clip = null; continue; }
                        if (text.Kind != DrawKind.Text) continue;
                        float left = text.Rect.X, top = text.Rect.Y;
                        float right = left + measurer.Width(text.Text, text.FontSize), bottom = top + measurer.LineHeight(text.FontSize);
                        if (clip is UiRect c)
                        {
                            Assert.That(c.X >= panel.Rect.X - 0.5f && c.Right <= panel.Rect.Right + 0.5f && c.Y >= panel.Rect.Y - 0.5f && c.Bottom <= panel.Rect.Bottom + 0.5f,
                                Is.True, language + " " + node.Id + ": 자르는 영역이 패널 안");
                            left = Math.Max(left, c.X); top = Math.Max(top, c.Y); right = Math.Min(right, c.Right); bottom = Math.Min(bottom, c.Bottom);
                        }
                        Assert.That(left >= panel.Rect.X - 0.5f && right <= panel.Rect.Right + 0.5f && top >= panel.Rect.Y - 0.5f && bottom <= panel.Rect.Bottom + 0.5f,
                            Is.True, language + " " + (node.Id ?? node.GetType().Name) + ": " + text.Text);
                        Assert.That(text.FontSize, Is.GreaterThanOrEqualTo(14f), node.Id);
                    }
                }
            }
            UiNode paletteNode = f.T.Node(FurniturePlacementScreen.PaletteId);
            var buttons = Walk(paletteNode).Where(n => n != paletteNode && n.IsVisibleInTree() && n.Interactive).ToList();
            for (int i = 0; i < buttons.Count; i++)
                for (int j = i + 1; j < buttons.Count; j++)
                {
                    UiRect p = buttons[i].Rect, q = buttons[j].Rect;
                    bool overlap = p.X < q.Right - 0.5f && q.X < p.Right - 0.5f && p.Y < q.Bottom - 0.5f && q.Y < p.Bottom - 0.5f;
                    Assert.That(overlap, Is.False, buttons[i].Id + " + " + buttons[j].Id);
                }
        }

        private static IEnumerable<UiNode> Walk(UiNode root)
        {
            yield return root;
            foreach (UiNode child in root.Children)
                foreach (UiNode node in Walk(child)) yield return node;
        }

        [Test]
        public void LanguageSwitch_UpdatesTexts()
        {
            using var f = new Fixture();
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            f.PickCard(floor.Id);
            var (_, a, _) = f.FirstRoom();
            f.Hover(a);
            string koName = f.Text(FurniturePlacementScreen.FinishNameId), koTab = ((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishTabFloorId)).Text;
            string koReason = f.Text(FurniturePlacementScreen.FinishReasonId);
            f.T.Text.Language = "en";
            Assert.That(f.Text(FurniturePlacementScreen.FinishNameId), Is.EqualTo(f.T.Text.Get(floor.NameKey)).And.Not.EqualTo(koName));
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishTabFloorId)).Text, Is.EqualTo("Floor").And.Not.EqualTo(koTab));
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.CategoryFinishId)).Text, Is.EqualTo("Decorate"));
            Assert.That(((FurnitureCategoryButton)f.T.Node(FurniturePlacementScreen.FinishTabBandId)).Text, Is.EqualTo("Wall top color"));
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(f.Screen.FinishPreview.ReasonKey)).And.Not.EqualTo(koReason));
        }
    }
}
