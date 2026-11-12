using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Storage;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 상자를 실제 창과 게임 루프로: 새 슬롯(집) → 가구 배치에서 상자를 검색해 골라 월드를 눌러 놓음 → 배치 끔 → 상자까지 걸어가 E →
    /// 상자 화면에서 가방 칸을 눌러 넣음 → 닫기 → 다시 E로 열어 그대로 있음 → 끄고 다시 시작(이어 하기) → 걸어가 E로 열어 그대로 있음.
    /// 조작은 키·글자·포인터뿐(장치 이벤트 주입, OS 입력 계층 미확인). 준비 하나만 내부 호출이다: 새 게임의 가방에 나무를 넣는다
    /// (Inventory.Add — 나무를 모으는 흐름은 이 시험의 대상이 아니다). 칸·지점을 고를 때만 Core 판정과 좌표 바꾸기를 읽는다(어디를 누를지 정하는 데만).
    /// 스크린샷: output/host-screens-evidence/storage-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class StorageChestHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-storage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start()
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        private static HostPlacementView View(PlayerDriver p) => new HostPlacementView(p.Game.World, () => p.Game.Fit.Scale);

        /// <summary>상자의 상호작용 후보(맵 픽셀)까지 걸어가 E를 눌러 상자 화면을 연다.</summary>
        private static StorageScreen WalkUpAndOpen(PlayerDriver p, string chestId)
        {
            InteractionCandidate chest = p.Game.World.Interactions.Candidates.Single(c => c.Kind == StorageScreen.InteractionKind && c.Id == chestId);
            int size = p.World.Map.Grid.CellSize;
            p.WalkNear(chest.X, chest.Y, size * 1.3f);
            p.Frames(2);
            Assert.That(p.Game.World.Interactions.Target?.Kind, Is.EqualTo(StorageScreen.InteractionKind), "앞의 대상이 상자다");
            Assert.That(((Rootborn.Screens.Hud.InteractionPromptScreen)p.Screens.Find(ScreenCatalog.HudPrompt)).Text, Does.Contain(p.Game.Text.Get("ui.prompt.open-storage")));
            p.PressKey(TestKey.E);
            p.WaitForScreen(ScreenCatalog.Storage);
            p.Frames(2);
            return (StorageScreen)p.Screens.Find(ScreenCatalog.Storage);
        }

        /// <summary>
        /// 다른 화면 시험(시험용 테마, 영어)의 준비: 집에 상자를 놓고 나무 둘을 넣는다(GameSession 명령 — 준비만). 그 상자를 여는 상호작용을 돌려준다.
        /// </summary>
        internal static InteractionEvent PrepareChest(PlayerDriver p)
        {
            var session = p.Game.Session;
            var crate = session.Definitions.Get<FurnitureDefinition>(session.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId);
            GridPoint cell = p.Game.World.Room.FloorCells.First(c => session.House.CanPlace(crate, c, FurniturePlacementDirection.North));
            session.PlaceFurniture(crate.StableId, cell, FurniturePlacementDirection.North);
            string id = session.House.Placed.Last().InstanceId;
            session.Inventory.Add(session.Item("Wood"), 2);
            session.PutIntoStorage(id, "Wood", 2);
            p.Frames(2);
            return new InteractionEvent(p.World.Map.Id, StorageScreen.InteractionKind, id);
        }

        private static void ClickGridCell(PlayerDriver p, string gridId, int index)
        {
            UiRect r = ((UiCardGrid)p.Node(gridId)).CellRect(index);
            float s = p.Game.Fit.Scale;
            p.ClickAt(r.CenterX * s, r.CenterY * s);
        }

        [Test]
        public void STORAGE_HOST_001_PlaceAChest_WalkUp_Open_PutWood_Close_Reopen_AndAfterContinuing()
        {
            string chestId;
            GridPoint chestCell;
            using (PlayerDriver player = Start())
            {
                var session = player.Game.Session;
                Assert.That(player.World.Map.Id, Is.EqualTo("House"));
                string crate = session.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId;
                FurnitureDefinition crateDef = session.Definitions.Get<FurnitureDefinition>(crate);
                session.Inventory.Add(session.Item("Wood"), 5); // 준비(내부 호출)

                // 가구 배치: HUD 버튼 → 검색 → 칸 → 월드 클릭.
                player.PressButton("hud.button.placement");
                player.WaitForScreen(ScreenCatalog.FurniturePlacement);
                var placement = (FurniturePlacementScreen)player.Screens.Find(ScreenCatalog.FurniturePlacement);
                player.PressButton(FurniturePlacementScreen.SearchId);
                player.TypeText(crate);
                int slot = Enumerable.Range(0, 20).First(i => placement.Palette.At(i)?.StableId == crate);
                player.PressButton(FurniturePlacementScreen.SlotId(slot));
                Assert.That(placement.Active, Is.SameAs(crateDef));

                FurnitureRoom room = player.Game.World.Room;
                var others = player.Game.World.Interactions.Candidates.Where(c => c.Acts).ToList();
                int cell = player.World.Map.Grid.CellSize;
                HostPlacementView view = View(player);
                chestCell = room.FloorCells.First(c =>
                {
                    UiRect r = view.CellRect(c);
                    if (r.X < 0 || r.Y < 0 || r.Right > 1920 || r.Bottom > 1080 || player.Game.Ui.HitTest(r.CenterX, r.CenterY) != null) return false;
                    if (!session.House.CanPlace(crateDef, c, FurniturePlacementDirection.North)) return false;
                    AreaRect a = room.CellArea(c);
                    return others.All(o => Math.Abs(o.X - a.CenterX) + Math.Abs(o.Y - a.CenterY) > cell * 4.5f);
                });
                UiRect target = view.CellRect(chestCell);
                player.ClickAt(target.CenterX, target.CenterY);
                Assert.That(placement.LastAction, Is.EqualTo("placed"));
                chestId = session.House.Placed.Single().InstanceId;
                player.PressButton("hud.button.placement");
                player.WaitForScreenClosed(ScreenCatalog.FurniturePlacement);
                player.MovePointer(-50, -50);
                player.Frames(2);
                Assert.That(player.Game.World.Interactions.Candidates.Count(c => c.Kind == StorageScreen.InteractionKind), Is.EqualTo(1), "놓은 상자가 상호작용 대상이 된다");

                // 걸어가 E로 연다.
                StorageScreen screen = WalkUpAndOpen(player, chestId);
                Assert.That(player.Screens.MovementBlocked, Is.False);
                Assert.That(screen.Model.Chest, Is.Empty);
                player.ScreenshotScreen(ScreenCatalog.Storage, "storage-01-open-empty");

                // 가방의 나무 칸을 눌러 넣는다.
                int wood = screen.Model.Bag.ToList().FindIndex(r => r.Item.Id == "Wood");
                Assert.That(wood, Is.GreaterThanOrEqualTo(0));
                ClickGridCell(player, StorageScreen.BagGridId, wood);
                player.Frames(2);
                Assert.That(session.Inventory.CountOf(session.Item("Wood")), Is.EqualTo(0));
                Assert.That(screen.Model.Chest.Single().Count, Is.EqualTo(5));
                player.ScreenshotScreen(ScreenCatalog.Storage, "storage-02-put-wood");

                // 닫고 다시 연다.
                player.PressButton(StorageScreen.CloseId);
                player.WaitForScreenClosed(ScreenCatalog.Storage);
                player.PressKey(TestKey.E);
                player.WaitForScreen(ScreenCatalog.Storage);
                player.Frames(2);
                screen = (StorageScreen)player.Screens.Find(ScreenCatalog.Storage);
                Assert.That(screen.Model.Chest.Single(r => r.Item.Id == "Wood").Count, Is.EqualTo(5), "다시 열어도 그대로");
                Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, screen.TextKeys);
                Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
            }

            // 이어 하기: 끄고 다시 시작해 걸어가 연다.
            using (PlayerDriver again = Start())
            {
                Assert.That(again.World.Map.Id, Is.EqualTo("House"));
                string id = again.Game.Session.House.Placed.Single().InstanceId;
                Assert.That(again.Game.Session.House.Placed.Single().Anchor, Is.EqualTo(chestCell));
                StorageScreen screen = WalkUpAndOpen(again, id);
                Assert.That(screen.Model.Chest.Single(r => r.Item.Id == "Wood").Count, Is.EqualTo(5), "이어 하기 뒤에도 상자에 있다");
                Assert.That(again.Game.Session.Inventory.CountOf(again.Game.Session.Item("Wood")), Is.EqualTo(0), "가방에서는 빠진 채");
                again.ScreenshotScreen(ScreenCatalog.Storage, "storage-03-after-continue");
            }
        }
    }
}
