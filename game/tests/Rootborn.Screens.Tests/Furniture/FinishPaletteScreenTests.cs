using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Minigames;
using Rootborn.Core.World;
using Rootborn.Screens.Furniture;
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
            Assert.That(f.Screen.Root.Find(FurniturePlacementScreen.FinishCardId(FurniturePlacementScreen.Metrics.PageSize)), Is.Null, "카드는 한 쪽만큼만 만든다");
        }

        private static int Visible(Fixture f) => Enumerable.Range(0, FurniturePlacementScreen.Metrics.PageSize).Count(i => f.T.Node(FurniturePlacementScreen.FinishCardId(i)).IsActive);

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
            Assert.That(list.Commands.Count(c => c.Kind == DrawKind.Fill && c.Color.Equals(FurniturePlacementScreen.Metrics.InvalidFootprint)), Is.EqualTo(f.Screen.FinishPreview.ChangedCells.Count));
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

        [Test]
        public void SharedHouse_ShowsReason()
        {
            var rules = new Rootborn.Core.Room.RoomHouseRules(TestSession.Definitions.All<Rootborn.Core.Placement.FurnitureDefinition>(),
                s => s == "house" ? new FurnitureRoom(House.World).Grid : null);
            var pieces = new Rootborn.Core.Room.RoomPieceSet(new Rootborn.Core.Room.RoomShareSettings(new[] { Rootborn.Core.Room.RoomHouse.PieceName }),
                Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(rules));
            var shares = new Rootborn.Core.Room.RoomShares(pieces.Settings, Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(), () => "guest-r1");
            foreach (var p in pieces.Pieces) shares.Receive(p.State());
            using var f = new Fixture(shares: shares);
            Assert.That(f.T.Session.SharesHouse, Is.True, "준비: 같이 쓰는 집");
            f.Decorate();
            f.T.Click(FurniturePlacementScreen.FinishTabFloorId);
            RoomFinishDefinition floor = FreeNonDefault(f, RoomFinishSlots.Floor);
            f.PickCard(floor.Id);
            var (_, a, _) = f.FirstRoom();
            f.Hover(a);
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(HouseFinishReasons.SharedHouse)));
            Assert.That(f.View.Shown.Where(p => p != null), Is.Empty);
            f.ClickCell(a);
            Assert.That(f.T.Session.HouseFloorAt(a), Is.EqualTo(f.Frame.DefaultFloor), "바뀌지 않는다");
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
            for (int i = 0; i < 2; i++) { f.T.Stack.Layout(1920, 1080); f.T.Stack.Emit(new DrawList(), measurer); }
            f.T.Stack.Layout(1920, 1080);
            foreach (string panelId in new[] { FurniturePlacementScreen.PaletteId, "furniture.settings" })
            {
                UiNode panel = f.T.Node(panelId);
                foreach (UiNode node in Walk(panel).Where(n => n.IsVisibleInTree()))
                {
                    var list = new DrawList();
                    node.Emit(list, measurer);
                    foreach (DrawCommand text in list.Commands.Where(c => c.Kind == DrawKind.Text))
                    {
                        float right = text.Rect.X + measurer.Width(text.Text, text.FontSize), bottom = text.Rect.Y + measurer.LineHeight(text.FontSize);
                        Assert.That(text.Rect.X >= panel.Rect.X - 0.5f && right <= panel.Rect.Right + 0.5f && text.Rect.Y >= panel.Rect.Y - 0.5f && bottom <= panel.Rect.Bottom + 0.5f,
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
            Assert.That(f.Text(FurniturePlacementScreen.FinishReasonId), Is.EqualTo(f.T.Text.Get(f.Screen.FinishPreview.ReasonKey)).And.Not.EqualTo(koReason));
        }
    }
}
