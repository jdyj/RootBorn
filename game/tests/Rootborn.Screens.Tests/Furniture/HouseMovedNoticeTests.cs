using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>
    /// HOUSE-002 알림: 집 맵이 바뀐 뒤 옛 세이브로 처음 집에 들어서면 "가구 N개를 옮겼고 M개를 보관했다" 알림이 한 번 뜬다(알림 층,
    /// 이동을 막지 않음, 5초 뒤 저절로 닫힘). 자동 테스트, 창 없음.
    /// </summary>
    public sealed class HouseMovedNoticeTests
    {
        private string _saves;

        [SetUp] public void Make() { _saves = Path.Combine(Path.GetTempPath(), "rootborn-moved-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_saves); }
        [TearDown] public void Clean() { try { Directory.Delete(_saves, true); } catch (IOException) { } }

        private void WriteRecords(int count)
        {
            string rug;
            using (var probe = new TestSession())
                rug = probe.Session.AvailableFurniture().First(f => !f.BlocksMovement && f.AllowsSurface("house")
                    && f.GetFootprintCells(FurniturePlacementDirection.North).Count == 1 && f.GetTileParts(FurniturePlacementDirection.North).Count == 1).StableId;
            var items = Enumerable.Range(0, count).Select(i => new FurniturePlacementSaveData
            {
                FurnitureId = rug, SurfaceId = "house", AnchorX = 0, AnchorY = 0, Direction = FurniturePlacementDirection.North, StateJson = "{\"n\":" + i + "}",
            }).ToArray();
            new Rootborn.Data.Save.FileSaveStore(_saves).Write("slot-test", SaveFileNames.HouseFurnitureLayout(0),
                new FurniturePlacementLayoutSaveData { Version = FurniturePlacementLayoutSaveData.KnownVersion, Items = items });
        }

        private static void EnterHouse(TestSession t)
        {
            var room = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap(6, 5));
            t.Session.EnterHouse(room.Grid, "house");
            t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            t.Stack.OpenHud();
        }

        [Test]
        public void EnteringTheHouseWithMovedPieces_OpensOneNotice_WithBothCounts_AndItClosesByItself()
        {
            WriteRecords(20);
            using var t = new TestSession(saves: _saves);
            EnterHouse(t);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.HouseMovedNotice), Is.True);
            int stored = t.Session.StoredFurniture().Count, moved = t.Session.House.Placed.Count;
            Assert.That(moved + stored, Is.EqualTo(20), "준비: 하나도 잃지 않았다");
            Assert.That(((UiTextBlock)t.Node(HouseMovedNoticeScreen.MessageId)).Text, Is.EqualTo(t.Text.Format(HouseMovedNoticeScreen.MessageKey, moved, stored)));
            Assert.That(t.Stack.PausesClock, Is.False, "알림은 시계를 멈추지 않는다");
            Assert.That(t.Stack.MovementBlocked, Is.False);

            t.Stack.Tick(HouseMovedNoticeScreen.ShowSeconds + 0.1f);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.HouseMovedNotice), Is.False, "저절로 닫힌다");
            EnterHouse(t);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.HouseMovedNotice), Is.False, "다시 들어서도 이 판에서는 다시 뜨지 않는다");
        }

        [Test]
        public void ChangingTheLanguage_ChangesTheNoticeText()
        {
            WriteRecords(20);
            using var t = new TestSession(saves: _saves);
            EnterHouse(t);
            string ko = ((UiTextBlock)t.Node(HouseMovedNoticeScreen.MessageId)).Text;
            t.Text.Language = "en";
            Assert.That(((UiTextBlock)t.Node(HouseMovedNoticeScreen.MessageId)).Text, Is.Not.EqualTo(ko));
        }

        [Test]
        public void NothingMovedOrStored_NoNotice()
        {
            using var t = new TestSession(saves: _saves, emptyHouse: true); // 준비: 처음 배치 가구 없이(시험용 작은 방에는 처음 배치 자리가 없어 옮겨진다)
            EnterHouse(t);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.HouseMovedNotice), Is.False);
        }

        // ---------- 검토 지적 3: 경우별 문구 ----------

        [TestCase(5, 0, HouseMovedNoticeScreen.MovedOnlyKey)]
        [TestCase(0, 4, HouseMovedNoticeScreen.StoredOnlyKey)]
        [TestCase(3, 4, HouseMovedNoticeScreen.MessageKey)]
        public void TheNotice_PicksTheKeyByWhichCountsAreNonZero(int moved, int stored, string key)
        {
            using var t = new TestSession(saves: _saves);
            Assert.That(HouseMovedNoticeScreen.KeyFor(moved, stored), Is.EqualTo(key));
            t.Stack.Open(ScreenCatalog.HouseMovedNotice, null, (moved, stored));
            string text = ((UiTextBlock)t.Node(HouseMovedNoticeScreen.MessageId)).Text;
            Assert.That(text, Is.EqualTo(t.Text.Format(key, moved, stored)));
            Assert.That(text.Contains("0개") || text.Contains(" 0 "), Is.False, "0개를 말하지 않는다");
        }

        [Test]
        public void AllThreeKeys_ExistInEveryLanguage()
        {
            foreach (var table in LongText.MergedTables())
                foreach (string key in new[] { HouseMovedNoticeScreen.MessageKey, HouseMovedNoticeScreen.MovedOnlyKey, HouseMovedNoticeScreen.StoredOnlyKey })
                    Assert.That(table.Value.ContainsKey(key), Is.True, table.Key + ": " + key);
        }

        [Test]
        public void TheNotice_WithTwiceAsLongText_StaysInsideItsBox([ValueSource(nameof(Languages))] string language, [Values(0, 1, 2)] int which)
        {
            using var t = new TestSession(language, saves: _saves);
            var (moved, stored) = which == 0 ? (5, 0) : which == 1 ? (0, 4) : (3, 4);
            t.Stack.Open(ScreenCatalog.HouseMovedNotice, null, (moved, stored));
            var message = (UiTextBlock)t.Node(HouseMovedNoticeScreen.MessageId);
            message.Text = message.Text + message.Text;
            var measurer = new WideMeasurer();
            for (int i = 0; i < 2; i++)
            {
                t.Stack.Layout(1920, 1080);
                t.Stack.Emit(new DrawList(), measurer);
            }
            t.Stack.Layout(1920, 1080);
            var list = new DrawList();
            t.Stack.Emit(list, measurer);
            UiRect box = t.Node(HouseMovedNoticeScreen.BoxId).Rect;
            var texts = list.Commands.Where(c => c.Kind == DrawKind.Text).ToList();
            Assert.That(texts, Is.Not.Empty);
            foreach (DrawCommand c in texts)
            {
                Assert.That(c.Rect.X, Is.GreaterThanOrEqualTo(box.X - 0.5f));
                Assert.That(c.Rect.X + measurer.Width(c.Text, c.FontSize), Is.LessThanOrEqualTo(box.Right + 0.5f), language + " 가로");
                Assert.That(c.Rect.Y, Is.GreaterThanOrEqualTo(box.Y - 0.5f));
                Assert.That(c.Rect.Y + measurer.LineHeight(c.FontSize), Is.LessThanOrEqualTo(box.Bottom + 0.5f), language + " 세로");
            }
        }

        private static System.Collections.Generic.IEnumerable<string> Languages() => LongText.MergedTables().Keys.OrderBy(k => k);
    }
}
