using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Core.Placement;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Storage;
using Rootborn.Screens.Tests.Furniture;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Storage
{
    /// <summary>
    /// 상자 화면을 창 없이: 실제 data/와 GameSession, 실제 등록 표. 상자는 시험용 방(10x8칸)에 GameSession 명령으로 놓고(준비),
    /// 화면은 상호작용 "storage"로 열어 칸을 포인터로 누르거나 Shift+방향 키·Enter로 옮긴다. 자동 테스트다(화면에 보인다는 근거 아님).
    /// </summary>
    public sealed class StorageScreenTests
    {
        private static string ChestFurniture => TestSession.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId;

        /// <summary>준비: 시험용 방에 들어가(아직이면) 상자를 놓는다(GameSession 명령). 다른 화면 시험(겹침)도 쓴다.</summary>
        internal static string PlaceChest(TestSession t)
        {
            if (t.Session.House == null) t.Session.EnterHouse(new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap()).Grid, "house");
            var furniture = t.Session.Definitions.Get<FurnitureDefinition>(ChestFurniture);
            for (int x = 1; x < 9; x++)
                for (int y = 1; y < 7; y++)
                {
                    var cell = new Rootborn.Core.Minigames.GridPoint(x, y);
                    if (t.Session.House.CanPlace(furniture, cell, FurniturePlacementDirection.North)
                        && t.Session.PlaceFurniture(ChestFurniture, cell, FurniturePlacementDirection.North))
                        return t.Session.House.Placed.Last().InstanceId;
                }

            throw new InvalidOperationException("상자를 놓을 칸이 없다");
        }

        private static StorageScreen Open(TestSession t, string chest)
        {
            Assert.That(t.Stack.Interaction(new InteractionEvent("House", StorageScreen.InteractionKind, chest)), Is.True, "상자 앞 상호작용이 화면을 연다");
            return t.Screen<StorageScreen>(ScreenCatalog.Storage);
        }

        private static void ClickCell(TestSession t, string gridId, int index)
        {
            var grid = (UiCardGrid)t.Node(gridId);
            UiRect r = grid.CellRect(index);
            t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
            t.Stack.Pointer(r.CenterX, r.CenterY, true, 0);
            t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
        }

        [Test]
        public void TheChestOpensFromItsInteraction_BagLeftChestRight_WithoutBlockingMovement()
        {
            using var t = new TestSession();
            string chest = PlaceChest(t);
            var screen = Open(t, chest);
            Assert.That(screen.ChestId, Is.EqualTo(chest));
            Assert.That(t.Stack.MovementBlocked, Is.False, "가방처럼 이동을 막지 않는다");
            Assert.That(t.Node(StorageScreen.BagGridId).Rect.X, Is.LessThan(t.Node(StorageScreen.ChestGridId).Rect.X));
            Assert.That(screen.Model.ChestSlots, Is.EqualTo(64));
            Assert.That(t.Texts(t.Emit()), Does.Contain(t.Text.Format("ui.storage.chest", 0, 64)));
        }

        [Test]
        public void ClickingABagSlot_PutsTheStack_ClickingAChestSlot_TakesItBack()
        {
            using var t = new TestSession();
            string chest = PlaceChest(t);
            t.Session.Inventory.Clear();
            t.Session.Inventory.Add(t.Session.Item("Wood"), 7);
            t.Session.Inventory.Add(t.Session.Item("Stone"), 3);
            var screen = Open(t, chest);

            ClickCell(t, StorageScreen.BagGridId, 0);
            Assert.That(t.Session.Inventory.CountOf(t.Session.Item("Wood")), Is.EqualTo(0));
            Assert.That(screen.Model.Chest.Single().Count, Is.EqualTo(7));
            Assert.That(screen.Model.Bag.Single().Item.Id, Is.EqualTo("Stone"));
            Assert.That(screen.NoticeKey, Is.EqualTo("ui.storage.moved"));

            ClickCell(t, StorageScreen.ChestGridId, 0);
            Assert.That(t.Session.Inventory.CountOf(t.Session.Item("Wood")), Is.EqualTo(7));
            Assert.That(screen.Model.Chest, Is.Empty);

            ClickCell(t, StorageScreen.ChestGridId, 5); // 빈 칸
            Assert.That(t.Session.Inventory.CountOf(t.Session.Item("Wood")), Is.EqualTo(7));
        }

        [Test]
        public void TheKeyboard_ShiftArrowsChooseASlot_AndEnterMovesIt()
        {
            using var t = new TestSession();
            string chest = PlaceChest(t);
            t.Session.Inventory.Clear();
            t.Session.Inventory.Add(t.Session.Item("Wood"), 4);
            t.Session.Inventory.Add(t.Session.Item("Stone"), 2);
            Open(t, chest);
            t.Stack.Tree.SetFocus(t.Node(StorageScreen.BagGridId));
            Assert.That(t.Stack.Key(ScreenKey.Right, true), Is.True, "Shift+→ 로 칸을 고른다(이동을 막지 않는 화면)");
            Assert.That(t.Stack.Key(ScreenKey.Right, true), Is.True);
            Assert.That(t.Stack.Key(ScreenKey.Enter, false), Is.True, "Enter로 옮긴다");
            Assert.That(t.Session.Inventory.CountOf(t.Session.Item("Stone")), Is.EqualTo(0), "두 번째 칸(돌)을 넣었다");
            Assert.That(t.Session.Inventory.CountOf(t.Session.Item("Wood")), Is.EqualTo(4));
        }

        [Test]
        public void AFullChest_RefusesAndChangesNothing_AndSaysWhy()
        {
            using var t = new TestSession();
            string chest = PlaceChest(t);
            t.Session.Inventory.Clear();
            // 준비: 상자 64칸을 서로 다른 칸으로 채운다(쌓기 1인 도구는 칸 하나씩).
            var tool = t.Session.Item("StoneAxe");
            for (int i = 0; i < 64; i++)
            {
                t.Session.Inventory.Add(tool, 1);
                Assert.That(t.Session.PutIntoStorage(chest, tool.Id, 1), Is.EqualTo(StorageTransferResult.Moved), "준비 " + i);
            }

            t.Session.Inventory.Add(t.Session.Item("Wood"), 2);
            var screen = Open(t, chest);
            Assert.That(screen.Model.ChestUsed, Is.EqualTo(64));
            ClickCell(t, StorageScreen.BagGridId, 0);
            Assert.That(t.Session.Inventory.CountOf(t.Session.Item("Wood")), Is.EqualTo(2), "가방 그대로");
            Assert.That(screen.NoticeKey, Is.EqualTo("ui.storage.no-room"));
            Assert.That(t.Texts(t.Emit()), Does.Contain(t.Text.Get("ui.storage.no-room")));
        }

        [Test]
        public void Money_IsNotShownInTheBagColumn()
        {
            using var t = new TestSession();
            string chest = PlaceChest(t);
            t.Session.Inventory.Clear();
            t.Session.Inventory.Add(t.Session.Inventory.Currency, 30);
            var screen = Open(t, chest);
            Assert.That(screen.Model.Bag, Is.Empty, "돈은 지갑에 있고 상자에 넣지 않는다");
        }

        [Test]
        public void ANonChestTarget_DoesNotStayOpen()
        {
            using var t = new TestSession();
            PlaceChest(t);
            t.Stack.Interaction(new InteractionEvent("House", StorageScreen.InteractionKind, "no-such-instance"));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Storage), Is.False);
        }

        [Test]
        public void ReopeningTheGame_ShowsTheSameChest()
        {
            using var t = new TestSession();
            string chest = PlaceChest(t);
            t.Session.Inventory.Add(t.Session.Item("Wood"), 5);
            Open(t, chest);
            int woodSlot = t.Screen<StorageScreen>(ScreenCatalog.Storage).Model.Bag.ToList().FindIndex(r => r.Item.Id == "Wood");
            ClickCell(t, StorageScreen.BagGridId, woodSlot);

            using var again = t.Reopen();
            string id = PlaceChestOrFind(again);
            var screen = Open(again, id);
            Assert.That(screen.Model.Chest.Single(r => r.Item.Id == "Wood").Count, Is.EqualTo(5));
        }

        private static string PlaceChestOrFind(TestSession t)
        {
            t.Session.EnterHouse(new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap()).Grid, "house");
            return t.Session.House.Placed.Single(p => t.Session.IsStorage(p.Definition)).InstanceId;
        }

        [Test]
        public void EveryKeyTheScreenUses_IsInEveryLanguage()
        {
            using var t = new TestSession();
            string chest = PlaceChest(t);
            var screen = Open(t, chest);
            t.Emit();
            var keys = screen.TextKeys.Concat(StorageScreen.NoticeKeys).Append("ui.furniture.storage-not-empty").Distinct().ToList();
            foreach (string language in LongText.MergedTables().Keys)
                foreach (string key in keys)
                    Assert.That(t.Text.Has(key, language), Is.True, language + ": " + key);
        }

        [Test]
        public void DoubleLengthText_StaysInsideThePanel_InEveryLanguage()
        {
            foreach (string language in LongText.MergedTables().Keys)
            {
                var saves = Path.Combine(Path.GetTempPath(), "rootborn-storage-long-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(saves);
                try
                {
                    TestSession.EmptyHouse(new FileSaveStore(saves), "slot"); // 준비: 처음 배치 가구 없는 빈 집(놓은 상자 하나만 보려고)
                    var session = GameSession.Start(TestSession.Definitions, GameStartSettings.FromDefinitions(TestSession.Definitions), new FileSaveStore(saves), "slot", null);
                    var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(LongText.Create(language), session));
                    stack.Layout(1920, 1080);
                    session.EnterHouse(new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap()).Grid, "house");
                    session.PlaceFurniture(ChestFurniture, new Rootborn.Core.Minigames.GridPoint(2, 3), FurniturePlacementDirection.North);
                    string chest = session.House.Placed.Single().InstanceId;
                    session.Inventory.Add(session.Item("Wood"), 1);
                    stack.Interaction(new InteractionEvent("House", StorageScreen.InteractionKind, chest));
                    var screen = (StorageScreen)stack.Find(ScreenCatalog.Storage);
                    session.PutIntoStorage(chest, "Wood", 1); // 안내 줄도 채운다
                    for (int i = 0; i < 2; i++) { stack.Layout(1920, 1080); stack.Emit(new DrawList(), new WideMeasurer()); }

                    UiRect panel = screen.Root.Rect;
                    var measurer = new WideMeasurer();
                    var list = new DrawList();
                    screen.Root.Emit(list, measurer);
                    foreach (UiNode node in Walk(screen.Root))
                    {
                        var own = new DrawList();
                        node.Emit(own, measurer);
                        foreach (DrawCommand c in own.Commands.Where(c => c.Kind == DrawKind.Text))
                        {
                            float right = c.Rect.X + measurer.Width(c.Text, c.FontSize), bottom = c.Rect.Y + measurer.LineHeight(c.FontSize);
                            Assert.That(c.Rect.X >= panel.X - 0.5f && right <= panel.Right + 0.5f && c.Rect.Y >= panel.Y - 0.5f && bottom <= panel.Bottom + 0.5f, Is.True,
                                language + " " + (node.Id ?? node.GetType().Name) + ": " + c.Text);
                        }
                    }
                }
                finally { try { Directory.Delete(saves, true); } catch (IOException) { } }
            }
        }

        private static IEnumerable<UiNode> Walk(UiNode root)
        {
            yield return root;
            foreach (UiNode child in root.Children)
                foreach (UiNode node in Walk(child)) yield return node;
        }
    }
}
