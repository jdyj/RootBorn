using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Core.Building;
using Rootborn.Core.Interaction;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Minigames;
using Rootborn.Screens.Storage;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 두 게임 테스트가 양쪽 게임에서 같이 쓰는 조작 묶음. 조작은 전부 <see cref="PlayerDriver"/>의 동사(방향 키·E·Esc·글자·포인터)다.
    /// 게임 상태는 어디를 누를지 고르는 데에만 읽는다(지점 좌표, 칸, 화면의 줄). 명령을 직접 부르지 않는다.
    /// 실패는 <see cref="InvalidOperationException"/>(자식 게임에서는 그 글이 부모의 실패 이유로 간다 — NUnit 밖에서도 같게).
    /// </summary>
    public static class PlayActions
    {
        private static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException(why);
        }

        // ---------- 걷기 ----------

        /// <summary>맵 사이 이동에 지나는 문(지금 맵 기준 문 목적지 값 — 맵이 바뀌면 이 표만 고친다. MainChainPlayer와 같은 표).</summary>
        private static readonly Dictionary<(string From, string To), string> Doors = new Dictionary<(string, string), string>
        {
            { ("House", "Town"), "town" }, { ("Town", "House"), "house" }, { ("Town", "School"), "north" }, { ("School", "Town"), "town" },
        };

        private static readonly Dictionary<(string From, string To), string> Via = new Dictionary<(string, string), string>
        {
            { ("House", "School"), "Town" }, { ("School", "House"), "Town" },
        };

        /// <summary>문을 걸어 지나 그 맵까지 간다. 저절로 뜬 하루 이벤트는 닫는다.</summary>
        public static void GoTo(this PlayerDriver p, string map)
        {
            for (int hop = 0; hop < 4 && p.World.Map.Id != map; hop++)
            {
                string from = p.World.Map.Id;
                string next = Via.TryGetValue((from, map), out string via) ? via : map;
                Check(Doors.TryGetValue((from, next), out string door), from + "에서 " + next + "로 가는 문을 모른다");
                string reached = p.WalkThroughDoor(door);
                Check(reached == next, "문 " + door + "을 지나 " + next + "가 아니라 " + reached);
                p.DismissPopups();
            }
            Check(p.World.Map.Id == map, "맵 " + map + "에 닿지 못했다: " + p.World.Map.Id);
        }

        /// <summary>저절로 뜬 하루 이벤트가 있으면 닫기 단추로 닫는다.</summary>
        public static void DismissPopups(this PlayerDriver p)
        {
            p.Frames(2);
            if (p.Screens.IsOpen(ScreenCatalog.DailyEvent))
            {
                p.PressButton(DailyEventScreen.CloseId);
                p.WaitForScreenClosed(ScreenCatalog.DailyEvent);
            }
        }

        public static InteractionThing Thing(this PlayerDriver p, string kind, string id)
        {
            InteractionThing thing = p.Game.World.HereData.Things.FirstOrDefault(t => t.Kind == kind && (string.IsNullOrEmpty(id) || t.Id == id));
            Check(thing != null, p.World.Map.Id + "에 " + kind + "/" + id + " 지점이 없다");
            return thing;
        }

        /// <summary>지점까지 걸어가 그것이 E의 대상인지 본다.</summary>
        public static void WalkToThing(this PlayerDriver p, string kind, string id)
        {
            InteractionThing thing = p.Thing(kind, id);
            p.WalkNear(thing.X, thing.Y, p.World.Map.Grid.CellSize * 1.25f);
            p.Frames(2);
            var target = p.Game.World.Interactions.Target;
            Check(target?.Kind == thing.Kind && target?.Id == thing.Id, "걸어간 자리에서 E의 대상이 " + target?.Kind + "/" + target?.Id);
        }

        // ---------- 알바(미니게임) ----------

        /// <summary>
        /// 알바 자리까지 걸어가 E → 미니게임(순서 외우기)을 화면에 보인 순서대로 포인터로 → 끝나면 Esc. 정산까지 했으면 true.
        /// 미니게임이 열리지 않으면(거절) false — 거절 안내는 부른 쪽이 본다.
        /// </summary>
        public static bool WorkShift(this PlayerDriver p, string workId)
        {
            p.WalkToThing("part-time-work", workId);
            p.PressKey(TestKey.E);
            for (int i = 0; i < 20 && !p.Screens.IsOpen(ScreenCatalog.Minigame); i++) p.Frames(1);
            if (!p.Screens.IsOpen(ScreenCatalog.Minigame)) return false;
            var screen = (MinigameScreen)p.Screens.Find(ScreenCatalog.Minigame);
            for (int guard = 0; guard < 8 && !screen.Session.IsComplete; guard++)
            {
                ChallengeSession challenge = screen.Session.Current;
                Check(challenge.Mechanic == MinigameMechanic.Sequence, "이 도우미는 순서 외우기 알바만 한다: " + challenge.Mechanic);
                int[] order = challenge.PreviewSequence; // 미리 보기 동안 화면에 보이는 순서
                for (int i = 0; i < 600 && challenge.PreviewVisible; i++) p.Frames(1);
                foreach (int answer in order) p.PressButton(SymbolChoiceView.ChoiceId(answer));
                for (int i = 0; i < 120 && screen.Session.Current == challenge && !screen.Session.IsComplete; i++) p.Frames(1);
            }
            Check(screen.Session.IsComplete && screen.Session.Succeeded, "알바 미니게임을 끝내지 못했다");
            p.Frames(2);
            p.PressKey(TestKey.Escape);
            p.WaitForScreenClosed(ScreenCatalog.Minigame);
            p.DismissPopups();
            return true;
        }

        // ---------- 가게 ----------

        /// <summary>장소 지점까지 걸어가 E → 물건 줄(가구) 중 살 수 있는 첫 줄을 포인터로 누른다. 그 물건 id. 화면은 열어 둔다.</summary>
        public static string BuyFirstOffer(this PlayerDriver p, string locationId)
        {
            p.WalkToThing(LocationScreen.InteractionKind, locationId);
            p.PressKey(TestKey.E);
            p.WaitForScreen(ScreenCatalog.Location);
            var screen = (LocationScreen)p.Screens.Find(ScreenCatalog.Location);
            int row = screen.Model.Rows.ToList().FindIndex(r => r.Kind == LocationRowKind.Offer && r.Enabled);
            Check(row >= 0, locationId + "에 살 수 있는 물건 줄이 없다");
            string id = screen.Model.Rows[row].Id;
            UiNode list = p.Node(LocationScreen.RowsId);
            for (int guard = 0; guard < 40; guard++)
            {
                UiRect r = screen.RowRect(row);
                if (r.Y >= list.Rect.Y && r.Bottom <= list.Rect.Bottom) break;
                p.MovePointer(list.Rect.CenterX, list.Rect.CenterY);
                p.Wheel(r.Y < list.Rect.Y ? 1 : -1);
            }
            UiRect rect = screen.RowRect(row);
            Check(p.Game.Ui.HitTest(rect.CenterX, rect.CenterY)?.Id == LocationScreen.RowsId, "물건 줄이 가려졌다");
            p.ClickAt(rect.CenterX, rect.CenterY);
            return id;
        }

        // ---------- 가구와 상자 ----------

        /// <summary>
        /// 집 HUD 버튼으로 배치를 켜고 검색 칸에 가구 id를 쳐 고른 뒤, 화면 안의 놓을 수 있는 빈 칸(다른 상호작용 지점에서 4칸 넘게 떨어진)을 포인터로 누른다.
        /// 배치를 끄고 놓은 칸(가구 방 칸)을 돌려준다. 방이 허락할 때까지 기다리는 것은 부른 쪽이 한다.
        /// near(맵 픽셀)를 주면 그 자리에 가까운 칸부터 고르고, 지점과는 1.5칸만 떨어지면 되며, 걷는 NPC의 후보(움직인다)는 떨어질 지점으로 세지 않는다.
        /// </summary>
        public static GridPoint PlaceFurniture(this PlayerDriver p, string stableId, (float X, float Y)? near = null)
        {
            var session = p.Game.Session;
            FurnitureDefinition definition = session.Definitions.Get<FurnitureDefinition>(stableId);
            p.PressButton("hud.button.placement");
            p.WaitForScreen(ScreenCatalog.FurniturePlacement);
            var placement = (FurniturePlacementScreen)p.Screens.Find(ScreenCatalog.FurniturePlacement);
            if (placement.Palette.Query.Length > 0)
            {
                p.PressButton(FurniturePlacementScreen.SearchId);
                p.PressKey(TestKey.Backspace, placement.Palette.Query.Length + 2);
                p.PressKey(TestKey.Escape);
            }
            p.PressButton(FurniturePlacementScreen.SearchId);
            p.TypeText(stableId);
            p.PressKey(TestKey.Escape);
            int slot = Enumerable.Range(0, 20).FirstOrDefault(i => placement.Palette.At(i)?.StableId == stableId, -1);
            Check(slot >= 0, "팔레트에서 " + stableId + "를 찾지 못했다");
            p.PressButton(FurniturePlacementScreen.SlotId(slot));
            Check(placement.Active?.StableId == stableId, "고른 가구가 " + placement.Active?.StableId);

            FurnitureRoom room = p.Game.World.Room;
            var others = p.Game.World.Interactions.Candidates.Where(c => c.Acts && (near == null || c.Kind != "npc")).ToList();
            int size = p.World.Map.Grid.CellSize;
            var view = new HostPlacementView(p.Game.World, () => p.Game.Fit.Scale);
            float w = p.Game.Fit.LogicalWidth, h = p.Game.Fit.LogicalHeight;
            GridPoint target = default;
            bool found = false;
            float keepAway = near.HasValue ? 1.5f : 4.5f; // 자리를 고르면 지점 칸만 비켜 둔다(지점 곁의 자리도 고를 수 있게)
            IEnumerable<GridPoint> cells = room.FloorCells;
            if (near.HasValue)
                cells = cells.OrderBy(c => { AreaRect a = room.CellArea(c); return Math.Abs(a.CenterX - near.Value.X) + Math.Abs(a.CenterY - near.Value.Y); }).ToList();
            foreach (GridPoint c in cells)
            {
                UiRect r = view.CellRect(c);
                if (r.X < 0 || r.Y < 0 || r.Right > w || r.Bottom > h || p.Game.Ui.HitTest(r.CenterX, r.CenterY) != null) continue;
                if (Math.Abs(c.X - room.Grid.Spawn.X) <= 2 && Math.Abs(c.Y - room.Grid.Spawn.Y) <= 2) continue;
                if (!session.House.CanPlace(definition, c, FurniturePlacementDirection.North)) continue;
                AreaRect a = room.CellArea(c);
                if (!others.All(o => Math.Abs(o.X - a.CenterX) + Math.Abs(o.Y - a.CenterY) > size * keepAway)) continue;
                target = c; found = true; break;
            }
            Check(found, stableId + "를 놓을 칸이 화면에 없다");
            UiRect at = view.CellRect(target);
            p.ClickAt(at.CenterX, at.CenterY);
            // 같이 쓰는 집이면 방의 답을 기다린다(waiting → placed / refused). 배치 화면이 답을 보인다.
            for (int i = 0; i < 900 && placement.LastAction == "waiting"; i++) p.Frames(1);
            Check(placement.LastAction == "placed", stableId + "를 " + target.X + "," + target.Y + "에 놓지 못했다: " + placement.LastAction
                + " (방 안내: " + (p.Game.Ui.Root.Find("furniture.room-status") is UiLabel l ? l.Text : "?") + ")");
            p.PressButton("hud.button.placement");
            p.WaitForScreenClosed(ScreenCatalog.FurniturePlacement);
            p.MovePointer(-50, -50);
            p.Frames(2);
            return target;
        }

        /// <summary>상자의 상호작용 후보(맵 픽셀)까지 걸어가 E를 눌러 상자 화면을 연다.</summary>
        public static StorageScreen OpenChest(this PlayerDriver p, string chestId)
        {
            InteractionCandidate chest = p.Game.World.Interactions.Candidates.FirstOrDefault(c => c.Kind == StorageScreen.InteractionKind && c.Id == chestId);
            Check(chest != null, p.World.Map.Id + "에 상자 " + chestId + "가 없다");
            int size = p.World.Map.Grid.CellSize;
            p.WalkNear(chest.X, chest.Y, size * 1.3f);
            p.Frames(2);
            Check(p.Game.World.Interactions.Target?.Kind == StorageScreen.InteractionKind && p.Game.World.Interactions.Target?.Id == chestId,
                "앞의 대상이 상자가 아니다: " + p.Game.World.Interactions.Target?.Kind);
            p.PressKey(TestKey.E);
            p.WaitForScreen(ScreenCatalog.Storage);
            p.Frames(2);
            return (StorageScreen)p.Screens.Find(ScreenCatalog.Storage);
        }

        /// <summary>열린 상자 화면에서 그 물건의 칸(가방 쪽이면 put) 가운데 논리 좌표. 그 칸이 없으면 실패.</summary>
        public static UiRect StorageCell(this PlayerDriver p, string itemId, bool put)
        {
            var screen = (StorageScreen)p.Screens.Find(ScreenCatalog.Storage);
            Check(screen != null && p.Screens.IsOpen(ScreenCatalog.Storage), "상자 화면이 열려 있지 않다");
            var rows = put ? screen.Model.Bag : screen.Model.Chest;
            int index = rows.ToList().FindIndex(r => r.Item.Id == itemId);
            Check(index >= 0, (put ? "가방" : "상자") + "에 " + itemId + " 칸이 없다");
            UiRect r = ((UiCardGrid)p.Node(put ? StorageScreen.BagGridId : StorageScreen.ChestGridId)).CellRect(index);
            Check(p.Game.Ui.HitTest(r.CenterX, r.CenterY)?.Id == (put ? StorageScreen.BagGridId : StorageScreen.ChestGridId), "칸이 가려졌다");
            return r;
        }

        /// <summary>열린 상자 화면에서 그 물건 칸을 눌러 옮긴다(가방 → 상자면 put).</summary>
        public static void MoveStack(this PlayerDriver p, string itemId, bool put)
        {
            UiRect r = p.StorageCell(itemId, put);
            p.ClickAt(r.CenterX, r.CenterY);
        }

        public static void CloseChest(this PlayerDriver p)
        {
            p.PressButton(StorageScreen.CloseId);
            p.WaitForScreenClosed(ScreenCatalog.Storage);
        }

        // ---------- 꾸미기(가구 배치 화면의 꾸미기 분류) ----------

        /// <summary>탭 이름(꾸밈 자리) → 탭 단추 id.</summary>
        private static readonly Dictionary<string, string> FinishTabs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { RoomFinishSlots.Wall, FurniturePlacementScreen.FinishTabWallId },
            { RoomFinishSlots.Floor, FurniturePlacementScreen.FinishTabFloorId },
            { RoomFinishSlots.Band, FurniturePlacementScreen.FinishTabBandId },
        };

        /// <summary>도구 이름 → 도구 단추 id.</summary>
        private static readonly Dictionary<string, string> FinishTools = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { HouseFinishTools.Room, FurniturePlacementScreen.FinishToolRoomId },
            { HouseFinishTools.Brush, FurniturePlacementScreen.FinishToolBrushId },
        };

        private static FurniturePlacementScreen Placement(PlayerDriver p) => (FurniturePlacementScreen)p.Screens.Find(ScreenCatalog.FurniturePlacement);

        /// <summary>집 HUD 버튼으로 가구 배치를 켜고(이미 켜져 있으면 그대로) 꾸미기 분류 → 그 탭을 누른다.</summary>
        public static FurniturePlacementScreen OpenFinishTab(this PlayerDriver p, string tab)
        {
            Check(FinishTabs.TryGetValue(tab, out string tabId), "모르는 꾸밈 탭: " + tab);
            if (!p.Screens.IsOpen(ScreenCatalog.FurniturePlacement))
            {
                p.PressButton("hud.button.placement");
                p.WaitForScreen(ScreenCatalog.FurniturePlacement);
            }
            FurniturePlacementScreen screen = Placement(p);
            p.PressButton(FurniturePlacementScreen.CategoryFinishId);
            Check(screen.FinishShown, "꾸미기 분류가 열리지 않았다");
            p.PressButton(tabId);
            Check(screen.Finishes.Slot == tab, "탭이 " + screen.Finishes.Slot);
            return screen;
        }

        /// <summary>가구 배치 화면을 HUD 버튼으로 닫고 포인터를 화면 밖으로 뺀다.</summary>
        public static void ClosePlacement(this PlayerDriver p)
        {
            if (!p.Screens.IsOpen(ScreenCatalog.FurniturePlacement)) return;
            p.PressButton("hud.button.placement");
            p.WaitForScreenClosed(ScreenCatalog.FurniturePlacement);
            p.MovePointer(-50, -50);
            p.Frames(2);
        }

        /// <summary>
        /// 꾸미기: 탭 → 도구 → 그 꾸밈의 카드(쪽을 넘겨 찾는다) → 주어진 맵 칸 중 화면에 보이고 UI가 덮지 않은 첫 칸에 포인터를 올리고,
        /// 미리 보기가 적용할 수 있게 될 때까지 기다린다(같이 쓰는 집은 방의 첫 꾸밈 상태가 오기 전 RoomTooOld). 누르지 않는다.
        /// 바뀔 칸 수(미리 보기의 ChangedCells)를 돌려준다. 게임 상태는 어디를 가리킬지·언제 누를지 고르는 데에만 읽는다.
        /// </summary>
        public static int AimFinish(this PlayerDriver p, string tab, string finishId, string tool, IEnumerable<CellPos> mapCells)
        {
            FurniturePlacementScreen screen = p.OpenFinishTab(tab);
            Check(FinishTools.TryGetValue(tool, out string toolId), "모르는 꾸밈 도구: " + tool);
            p.PressButton(toolId);
            int card = -1;
            for (int page = 0; page < 20 && card < 0; page++)
            {
                card = Enumerable.Range(0, FurniturePlacementScreen.Metrics.FinishPageSize).FirstOrDefault(i => screen.Finishes.At(i)?.Definition.Id == finishId, -1);
                if (card < 0)
                {
                    Check(screen.Finishes.HasNext, "카드에서 " + finishId + "를 찾지 못했다");
                    p.PressButton(FurniturePlacementScreen.FinishNextId);
                }
            }
            p.PressButton(FurniturePlacementScreen.FinishCardId(card));
            Check(screen.PickedFinish?.Id == finishId, "고른 꾸밈이 " + screen.PickedFinish?.Id);

            var view = new HostPlacementView(p.Game.World, () => p.Game.Fit.Scale);
            FurnitureRoom room = p.Game.World.Room;
            float w = p.Game.Fit.LogicalWidth, h = p.Game.Fit.LogicalHeight;
            UiRect? at = null;
            foreach (CellPos c in mapCells)
            {
                UiRect r = view.CellRect(room.ToPlacement(c));
                if (r.X < 0 || r.Y < 0 || r.Right > w || r.Bottom > h || p.Game.Ui.HitTest(r.CenterX, r.CenterY) != null) continue;
                at = r;
                break;
            }
            Check(at.HasValue, "가리킬 칸이 화면에 없다");
            p.MovePointer(at.Value.CenterX, at.Value.CenterY);
            p.Frames(1);
            for (int i = 0; i < 900 && screen.FinishPreview?.CanApply != true; i++) p.Frames(1);
            Check(screen.FinishPreview?.CanApply == true, finishId + " 미리 보기가 적용할 수 없다: " + screen.FinishPreview?.ReasonKey);
            return screen.FinishPreview.ChangedCells.Count;
        }

        /// <summary>
        /// <see cref="AimFinish"/> 뒤 포인터를 눌렀다 뗀다. (바뀔 칸 수, 누름이 한 일(applied/waiting/refused), 누른 직후의 이유 줄 글)을 돌려준다.
        /// 같이 쓰는 집이면 방의 허락을 기다린다("waiting") — 끝나기를 기다리는 것은 부른 쪽이 한다. Changed: 누르기 전 미리 보기의 바뀔 칸(맵 칸).
        /// </summary>
        public static (int Cells, string Action, string Reason, IReadOnlyList<CellPos> Changed) Decorate(this PlayerDriver p, string tab, string finishId, string tool, IEnumerable<CellPos> mapCells)
        {
            int cells = p.AimFinish(tab, finishId, tool, mapCells);
            FurniturePlacementScreen screen = Placement(p);
            IReadOnlyList<CellPos> changed = screen.FinishPreview.ChangedCells;
            // 누른 그 프레임 뒤에 읽는다(내 PC의 방은 같은 프로세스라 다음 프레임이면 답할 수 있다).
            p.SetPointerDown(true);
            p.Frames(1);
            string action = screen.FinishLastAction;
            string reason = ((UiTextBlock)p.Node(FurniturePlacementScreen.FinishReasonId)).Text;
            p.Frames(1);
            p.ReleasePointer();
            Check(action == "waiting" || action == "applied", finishId + " 누름이 " + action + " (이유: " + reason + ")");
            return (cells, action, reason, changed);
        }

        /// <summary>띠 색 탭 → 그 띠 꾸밈의 줄을 누른다(무료라 미리 보기 없이). 누름이 한 일(applied/waiting/refused)을 돌려준다.</summary>
        public static string DecorateBand(this PlayerDriver p, string finishId)
        {
            FurniturePlacementScreen screen = p.OpenFinishTab(RoomFinishSlots.Band);
            var session = p.Game.Session;
            int row = session.HouseFinishOptions(RoomFinishSlots.Band).ToList().FindIndex(b => b.Id == finishId);
            Check(row >= 0, "띠 줄에 " + finishId + "가 없다");
            // 같이 쓰는 집: 방의 첫 꾸밈 상태가 오기 전에는 누르지 않는다(RoomTooOld).
            for (int i = 0; i < 900 && !session.PreviewHouseBand(finishId).CanApply; i++) p.Frames(1);
            Check(session.PreviewHouseBand(finishId).CanApply, finishId + " 띠 색을 바꿀 수 없다: " + session.PreviewHouseBand(finishId).ReasonKey);
            var list = (UiScrollList)p.Node(FurniturePlacementScreen.FinishBandListId);
            Check(list.TryGetRowRect(row, out UiRect r), "띠 줄이 보이지 않는다");
            Check(p.Game.Ui.HitTest(r.CenterX, r.CenterY) == list, "띠 줄이 가려졌다");
            p.ClickAt(r.CenterX, r.CenterY);
            return screen.FinishLastAction;
        }

        // ---------- 잠 ----------

        /// <summary>잠자리(하루 마감 지점)까지 걸어가 E → 하루 결과 화면.</summary>
        public static DayResultScreen GoToBed(this PlayerDriver p)
        {
            p.WalkToThing(DayResultScreen.InteractionKind, null);
            p.PressKey(TestKey.E);
            p.WaitForScreen(ScreenCatalog.DayResult);
            return (DayResultScreen)p.Screens.Find(ScreenCatalog.DayResult);
        }
    }
}
