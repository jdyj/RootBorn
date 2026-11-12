using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Screens.Furniture;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>
    /// 가구 배치를 키보드만으로(이동을 막지 않는 패널이라 Shift+방향 키와 Enter). 팔레트에서 가구 고르기와 돌리기는 키로 된다.
    /// 방의 칸 고르기(놓기, 옮길 칸, 치울 가구 고르기)는 Host가 넘기는 월드 포인터(WorldPointer)로만 들어온다 — 칸 커서는 새 키 배정이 필요한
    /// 구성 변경이라 고치지 않고 보고한다(알려진 빈 곳). 화면은 HUD 단추로만 열려 여기서는 Open으로 연다(준비). 자동 테스트다.
    /// </summary>
    public sealed class KeyboardPlacementTests
    {
        private static FurniturePlacementScreen Open(TestSession t)
        {
            t.Session.EnterHouse(new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap()).Grid, "house");
            t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            t.Stack.OpenHud();
            t.Stack.Open(ScreenCatalog.FurniturePlacement);
            t.Emit();
            return t.Screen<FurniturePlacementScreen>(ScreenCatalog.FurniturePlacement);
        }

        [Test]
        public void PaletteAndRotate_WorkWithShiftArrowsAndEnter()
        {
            using var t = new TestSession();
            FurniturePlacementScreen screen = Open(t);
            Assert.That(t.Stack.Tree.Focused, Is.SameAs(t.Node(FurniturePlacementScreen.SlotId(0))), "열면 첫 팔레트 칸에 포커스");

            KeyboardPath.Press(t.Stack, t.Node(FurniturePlacementScreen.SlotId(1)), shift: true);
            Assert.That(screen.Active, Is.Not.Null, "Enter로 가구를 고른다");
            FurniturePlacementDirection before = screen.Direction;
            KeyboardPath.Press(t.Stack, t.Node(FurniturePlacementScreen.RotateId), shift: true);
            Assert.That(screen.Direction, Is.Not.EqualTo(before), "돌리기 단추에 포커스를 두고 Enter");
            Assert.That(t.Stack.MovementBlocked, Is.False, "그동안 캐릭터는 걸을 수 있다(Shift를 놓은 방향 키)");
        }

        [Test]
        public void ChoosingARoomCell_HasNoKeyboardPath_KnownGap()
        {
            using var t = new TestSession();
            FurniturePlacementScreen screen = Open(t);
            KeyboardPath.Press(t.Stack, t.Node(FurniturePlacementScreen.SlotId(0)), shift: true);
            Assert.That(screen.Active, Is.Not.Null);
            int placed = t.Session.House.Placed.Count;

            // 화면 쌓기가 받는 모든 키를 Shift와 함께·없이 여러 번 눌러도 칸에 놓이지 않는다(칸은 포인터로만 고른다).
            foreach (ScreenKey key in Enum.GetValues(typeof(ScreenKey)).Cast<ScreenKey>().Where(k => k != ScreenKey.Escape && k != ScreenKey.Inventory
                         && k != ScreenKey.Status && k != ScreenKey.Tab && k != ScreenKey.LocationLog && k != ScreenKey.Help))
                for (int i = 0; i < 3; i++)
                {
                    t.Stack.Key(key, false);
                    t.Stack.Key(key, true);
                }
            Assert.That(screen.IsOpen, Is.True);
            Assert.That(t.Session.House.Placed.Count, Is.EqualTo(placed), "알려진 빈 곳: 키보드로는 방의 칸을 고를 수 없다(보고서 '필요한 구성 변경')");
        }
    }
}
