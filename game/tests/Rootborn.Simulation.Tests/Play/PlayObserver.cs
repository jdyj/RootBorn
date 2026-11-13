using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Rootborn.Core.Interaction;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;
using Rootborn.Core.World;
using Rootborn.Game;
using Rootborn.Screens;
using Rootborn.Screens.Career;
using Rootborn.Screens.Day;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Minigames;
using Rootborn.Screens.Objectives;
using Rootborn.Ui;

namespace Rootborn.Simulation
{
    /// <summary>
    /// "지금 할 수 있는 것"을 모은다: 열린 화면에서 그 자리의 맨 위 요소가 자기인 누를 수 있는 것(포인터 판정 그대로), 여는 키,
    /// 걸어서 닿는 상호작용 지점과 문. 화면 좌표는 요소 사각형에서, 맵 좌표는 상호작용 후보에서 온다(사람이 화면에서 보는 것).
    /// 요소 종류마다 누를 곳을 내는 방법은 <see cref="UiTargets"/>에, 줄·카드가 가리키는 데이터 id를 읽는 방법은 <see cref="ScreenSubjects"/>에
    /// 한 줄씩 등록한다. 엔티티 id·맵 id로 분기하지 않는다.
    /// </summary>
    public sealed class PlayObserver
    {
        public const string TypedName = "하늘";
        private static readonly string[] PlaceKinds = { LocationScreen.InteractionKind, "location-visit" };

        private readonly PlayAtlas _atlas;
        private readonly HashSet<string> _seenKeys = new HashSet<string>(StringComparer.Ordinal);
        private ChallengeSession _remembered;
        private int _challenge;
        private string[] _storageFurniture;

        public PlayObserver(PlayAtlas atlas)
        {
            _atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        }

        public PlayView Observe(IPlayBody body)
        {
            var view = new PlayView();
            _storageFurniture ??= _atlas.Definitions.All<Rootborn.Core.Placement.FurnitureStorageDefinition>().Select(d => d.Furniture.StableId).ToArray();
            view.StorageFurniture.AddRange(_storageFurniture);
            ScreenStack stack = body.Screens;
            view.InWorld = body.InWorld;
            view.MovementBlocked = stack.MovementBlocked;
            view.TopScreen = stack.FocusScreen?.Id;
            foreach (GameScreen s in stack.OpenScreens) view.OpenScreens.Add(s.Id);
            GameSession session = body.Session;
            if (session != null)
            {
                view.Day = session.StudentDay;
                view.Minutes = session.StudentTimeMinutes;
                ReadTracked(session, view, body);
            }
            if (body.InWorld)
            {
                view.MapId = body.Walk.Map.Id;
                int size = body.Walk.Map.Grid.CellSize;
                CellPos cell = CellPos.Containing(body.Walk.Walker.X, body.Walk.Walker.Y, size);
                view.CellX = cell.X; view.CellY = cell.Y;
            }
            // 시각과 시간 경고는 HUD 시계가 보이는 것(사람이 보는 것)에서 읽는다.
            if (stack.Find(ScreenCatalog.HudClock) is Rootborn.Screens.Hud.ClockHudScreen clock && clock.IsOpen && clock.Model.HasSession)
            {
                view.ClockMinutes = clock.Model.Hour * 60 + clock.Model.Minute;
                view.Alert = clock.Model.Alert;
                view.Money = clock.Model.Allowance; // HUD 시계 줄에 보이는 돈(없으면 음수)
            }
            ReadMinigame(body, stack, view);

            _seenKeys.Clear();
            AddScreenActions(body, stack, view);
            AddKeys(body, stack, view);
            if (body.InWorld && !stack.MovementBlocked) AddWorldActions(body, view);
            return view;
        }

        // ---------- 화면 ----------

        private void AddScreenActions(IPlayBody body, ScreenStack stack, PlayView view)
        {
            var open = stack.OpenScreens;
            for (int i = open.Count - 1; i >= 0; i--)
            {
                GameScreen screen = open[i];
                Walk(body, stack, screen, screen.Root, view);
            }
            if (body.Room != null && body.PlacementView != null && stack.Find(ScreenCatalog.FurniturePlacement) is FurniturePlacementScreen placement && placement.IsOpen && placement.Active != null)
                AddPlacementCells(body, stack, placement, view);
        }

        private void Walk(IPlayBody body, ScreenStack stack, GameScreen screen, UiNode node, PlayView view)
        {
            if (!node.Visible || !node.Enabled) return;
            if (UiTargets.TryGet(node, out UiTargets.Enumerate enumerate))
            {
                enumerate(node, (category, index, rect) => AddUi(body, stack, screen, node, category, index, rect, view));
                if (UiTargets.Opaque(node)) return;
            }
            for (int i = 0; i < node.Children.Count; i++) Walk(body, stack, screen, node.Children[i], view);
        }

        private void AddUi(IPlayBody body, ScreenStack stack, GameScreen screen, UiNode node, string category, int index, UiRect rect, PlayView view)
        {
            if (rect.W <= 0 || rect.H <= 0) return;
            float x = rect.CenterX, y = rect.CenterY;
            if (x < 0 || y < 0 || x > body.Width || y > body.Height) return;
            if (stack.Tree.HitTest(x, y) != node) return; // 가려졌다(위층 화면, 모달 막이)
            string element = node.Id ?? "(no-id)";
            string key = category + ":" + screen.Id + "/" + element + (index >= 0 ? "#" + index.ToString(CultureInfo.InvariantCulture) : string.Empty);
            if (!_seenKeys.Add(key)) return;
            var action = new PlayAction
            {
                Kind = category == "text" ? PlayActionKind.Type : PlayActionKind.Click,
                Key = key,
                Category = category,
                Screen = screen.Id,
                Element = element,
                X = x,
                Y = y,
                Text = category == "text" ? TypedName : null,
                Subject = ScreenSubjects.Resolve(screen, element, index),
            };
            view.Actions.Add(action);
        }

        private void AddPlacementCells(IPlayBody body, ScreenStack stack, FurniturePlacementScreen placement, PlayView view)
        {
            FurnitureRoom room = body.Room;
            var house = body.Session?.House;
            int valid = 0, other = 0;
            foreach (GridPoint cell in room.FloorCells)
            {
                UiRect r = body.PlacementView.CellRect(cell);
                if (r.X < 0 || r.Y < 0 || r.Right > body.Width || r.Bottom > body.Height) continue;
                if (stack.Tree.HitTest(r.CenterX, r.CenterY) != null) continue;
                bool ok = house != null && house.CanPlace(placement.Active, cell, FurniturePlacementDirection.North);
                if (ok ? valid >= 12 : other >= 4) continue;
                if (ok) valid++; else other++;
                view.Actions.Add(new PlayAction
                {
                    Kind = PlayActionKind.Click,
                    Key = "cell:" + cell.X.ToString(CultureInfo.InvariantCulture) + "," + cell.Y.ToString(CultureInfo.InvariantCulture),
                    Category = ok ? "cell" : "cell-blocked",
                    Screen = placement.Id,
                    Subject = placement.Active.StableId,
                    X = r.CenterX,
                    Y = r.CenterY,
                });
            }
        }

        private void AddKeys(IPlayBody body, ScreenStack stack, PlayView view)
        {
            if (!body.InWorld) return;
            void K(ScreenKey key)
            {
                view.Actions.Add(new PlayAction { Kind = PlayActionKind.Key, Key = "key:" + key, Category = "key", ScreenKey = key });
            }
            K(ScreenKey.Tab);
            K(ScreenKey.Inventory);
            K(ScreenKey.Status);
            K(ScreenKey.Escape);
            if (stack.IsOpen(ScreenCatalog.Minigame))
            {
                K(ScreenKey.Space);
                K(ScreenKey.Rotate);
                K(ScreenKey.AltLeft);
                K(ScreenKey.AltRight);
                view.Actions.Add(new PlayAction { Kind = PlayActionKind.Wait, Key = "wait", Category = "wait", Frames = 30 });
            }
        }

        // ---------- 월드 ----------

        private void AddWorldActions(IPlayBody body, PlayView view)
        {
            WorldSession walk = body.Walk;
            WorldMap map = walk.Map;
            int size = map.Grid.CellSize;
            HashSet<CellPos> reach = Reachable(map, CellPos.Containing(walk.Walker.X, walk.Walker.Y, size));
            foreach (InteractionCandidate c in body.Candidates)
            {
                if (!c.Acts || !c.Active || Shadowed(c, body.Candidates)) continue;
                string key = "walk:" + c.Kind + "/" + c.Id;
                if (!_seenKeys.Add(key)) continue;
                if (!AnyNear(reach, c.X, c.Y, size * 1.25f, size)) continue;
                view.Actions.Add(new PlayAction
                {
                    Kind = PlayActionKind.WalkInteract, Key = key, Category = "interaction", InteractionKind = c.Kind, Subject = c.Id, X = c.X, Y = c.Y,
                });
            }
            foreach (DoorArea door in map.Doors)
            {
                if (!door.Destination.HasValue) continue;
                string key = "door:" + door.Target;
                if (!_seenKeys.Add(key)) continue;
                if (!TouchesDoor(reach, door, size)) continue;
                view.Actions.Add(new PlayAction
                {
                    Kind = PlayActionKind.Door, Key = key, Category = "door", InteractionKind = "door", Subject = door.Target,
                    DestinationMap = door.Destination.Value.MapId, X = door.Area.CenterX, Y = door.Area.CenterY,
                });
            }
        }

        /// <summary>같은 자리에 우선순위가 더 높은 후보가 있어 E의 대상이 될 수 없는 후보(예: 같은 자리의 장소 방문 지점 — game/world/README.md).</summary>
        private static bool Shadowed(InteractionCandidate c, IReadOnlyList<InteractionCandidate> all)
        {
            foreach (InteractionCandidate d in all)
                if (d != c && d.Acts && d.Active && d.Priority > c.Priority && Math.Abs(d.X - c.X) < 0.5f && Math.Abs(d.Y - c.Y) < 0.5f) return true;
            return false;
        }

        public static HashSet<CellPos> Reachable(WorldMap map, CellPos start)
        {
            var seen = new HashSet<CellPos> { start };
            var queue = new Queue<CellPos>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                CellPos c = queue.Dequeue();
                Try(new CellPos(c.X + 1, c.Y)); Try(new CellPos(c.X - 1, c.Y)); Try(new CellPos(c.X, c.Y + 1)); Try(new CellPos(c.X, c.Y - 1));
            }
            return seen;

            void Try(CellPos n)
            {
                if (!map.Grid.Contains(n.X, n.Y) || map.Collision.IsBlocked(n) || !seen.Add(n)) return;
                queue.Enqueue(n);
            }
        }

        private static bool AnyNear(HashSet<CellPos> reach, float x, float y, float within, int size)
        {
            foreach (CellPos c in reach)
            {
                var center = CellPos.Center(c, size);
                float dx = center.X - x, dy = center.Y - y;
                if (dx * dx + dy * dy <= within * within) return true;
            }
            return false;
        }

        private static bool TouchesDoor(HashSet<CellPos> reach, DoorArea door, int size)
        {
            foreach (CellPos d in CollisionInput.CellsUnder(door.Area, size))
                if (reach.Contains(d) || reach.Contains(new CellPos(d.X + 1, d.Y)) || reach.Contains(new CellPos(d.X - 1, d.Y))
                    || reach.Contains(new CellPos(d.X, d.Y + 1)) || reach.Contains(new CellPos(d.X, d.Y - 1))) return true;
            return false;
        }

        // ---------- 추적 목표와 미니게임 ----------

        private void ReadTracked(GameSession session, PlayView view, IPlayBody body)
        {
            TrackedObjectiveView tracked = session.TrackedObjective();
            if (!tracked.HasChain) return;
            view.HasTracked = true;
            view.TrackedStep = tracked.StepId;
            QuestChainDefinition chain = session.Chain(tracked.ChainId);
            var step = session.QuestChains.GetCurrentStep(chain);
            if (step == null) return;
            bool allDone = true;
            for (int i = 0; i < step.Objectives.Count; i++)
            {
                if (session.QuestChains.GetObjectiveProgress(chain, i) >= step.Objectives[i].RequiredCount) continue;
                allDone = false;
                ObjectiveIds.Collect(step.Objectives[i], view.TrackedTargets);
                foreach (string kind in ObjectiveIds.KindNames(step.Objectives[i])) view.TrackedKinds.Add(kind);
            }
            view.TrackedObjectivesDone = allDone && step.Objectives.Count > 0;
            // 활동 목표는 그 활동이 있는 장소 지점도 목표로 본다(사람은 장소 화면에서 활동을 찾는다).
            if (view.TrackedTargets.Count == 0) return;
            var places = new List<string>();
            foreach (string place in _atlas.ThingIds(PlaceKinds))
            {
                if (view.TrackedTargets.Contains(place)) continue;
                foreach (var a in session.LocationActivitiesAt(place))
                    if (view.TrackedTargets.Contains(a.Activity.Id)) { places.Add(place); break; }
            }
            foreach (string p in places) view.TrackedTargets.Add(p);
        }

        /// <summary>
        /// 미니게임 화면에서 보이는 것을 읽는다: 제목이 말하는 도전 종류, 끝났는지(결과 줄), 안내 글, 진행 줄, 도전 자리의 요소(사각형·글자·색).
        /// 판의 정답(순서, 답, 꼭 덮을 칸)은 읽지 않는다.
        /// </summary>
        private void ReadMinigame(IPlayBody body, ScreenStack stack, PlayView view)
        {
            if (!(stack.Find(ScreenCatalog.Minigame) is MinigameScreen screen) || !screen.IsOpen || screen.Session == null || screen.Session.Current == null)
            {
                _remembered = null;
                return;
            }
            ChallengeSession current = screen.Session.Current;
            if (current != _remembered) { _remembered = current; _challenge++; }
            var sight = new MinigameSight
            {
                Mechanic = current.Mechanic,
                Challenge = _challenge,
                Complete = screen.Session.IsComplete,
                Frame = body.Frames,
                Text = body.Text,
            };
            Collect(screen.Root, sight, false);
            view.Minigame = sight;
        }

        private static void Collect(UiNode node, MinigameSight sight, bool inBody)
        {
            if (node.Id == MinigameScreen.FeedbackId && node is UiTextBlock feedback) sight.Feedback = feedback.Text ?? string.Empty;
            else if (node.Id == MinigameScreen.StatusId && node is UiLabel status) sight.Status = status.Text ?? string.Empty;
            else if (inBody && node.Id != null)
            {
                var e = new SightElement { Id = node.Id, Rect = node.Rect, Visible = node.Visible, Enabled = node.IsActive };
                switch (node)
                {
                    case UiButton b: e.Text = b.Text; break;
                    case MinigameCell c: e.Text = c.Text; e.Color = c.Color; break;
                    case UiBox x: e.Color = x.Color; break;
                    case UiLabel l: e.Text = l.Text; break;
                }
                sight.Elements.Add(e);
            }
            bool body = inBody || node.Id == MinigameScreen.BodyId;
            for (int i = 0; i < node.Children.Count; i++) Collect(node.Children[i], sight, body);
        }
    }

    /// <summary>
    /// UI 요소 종류마다 누를 곳을 내는 방법. 한 요소가 여러 곳(줄, 카드, 탭)을 가지면 index를 붙인다. 새 요소 종류는 Register에 한 줄.
    /// 등록된 종류가 아니어도 포인터와 포커스를 받는 요소는 가운데를 누르는 버튼으로 본다.
    /// </summary>
    public static class UiTargets
    {
        public delegate void Emit(string category, int index, UiRect rect);
        public delegate void Enumerate(UiNode node, Emit emit);

        private static readonly List<(Type Type, Enumerate Enumerate, bool Opaque)> Kinds = new List<(Type, Enumerate, bool)>();

        static UiTargets()
        {
            Register(typeof(UiScrollList), (n, e) =>
            {
                var list = (UiScrollList)n;
                for (int i = list.FirstVisibleRow; i <= list.LastVisibleRow; i++)
                    if (list.TryGetRowRect(i, out UiRect r) && Inside(r, list.View)) e("row", i, r);
            });
            Register(typeof(JournalList), (n, e) =>
            {
                var list = (JournalList)n;
                for (int i = 0; i < list.RowCount; i++) { UiRect r = list.RowRect(i); if (Inside(r, list.View)) e("row", i, r); }
            });
            Register(typeof(UiCardRow), (n, e) =>
            {
                var cards = (UiCardRow)n;
                for (int i = 0; i < cards.CardCount; i++) { UiRect r = cards.CardRect(i); if (Inside(r, cards.View)) e("card", i, r); }
            });
            Register(typeof(UiCardGrid), (n, e) =>
            {
                var grid = (UiCardGrid)n;
                for (int i = 0; i < grid.ItemCount; i++) { UiRect r = grid.CellRect(i); if (Inside(r, grid.View)) e("card", i, r); }
            });
            Register(typeof(UiTabBar), (n, e) =>
            {
                var tabs = (UiTabBar)n;
                for (int i = 0; i < tabs.Count; i++) e("tab", i, tabs.TabRect(i));
            });
            Register(typeof(UiTextField), (n, e) => { if (n.IsActive) e("text", -1, n.Rect); });
            Register(typeof(UiButton), Button, opaque: false);
            Register(typeof(UiSlot), Button, opaque: false);
            Register(typeof(UiToggle), Button, opaque: false);
            Register(typeof(MinigameAimSurface), (n, e) => { if (n.IsActive) e("button", -1, n.Rect); }, opaque: false);
        }

        public static void Register(Type type, Enumerate enumerate, bool opaque = true) => Kinds.Add((type, enumerate, opaque));

        public static bool TryGet(UiNode node, out Enumerate enumerate)
        {
            Type t = node.GetType();
            foreach (var k in Kinds) if (k.Type.IsAssignableFrom(t)) { enumerate = k.Enumerate; return true; }
            if (node.Interactive && node.Focusable) { enumerate = Button; return true; }
            enumerate = null;
            return false;
        }

        /// <summary>그 요소가 자식 대신 누를 곳을 다 내는가(줄 목록 안의 노드를 따로 보지 않는다).</summary>
        public static bool Opaque(UiNode node)
        {
            Type t = node.GetType();
            foreach (var k in Kinds) if (k.Type.IsAssignableFrom(t)) return k.Opaque;
            return false;
        }

        private static void Button(UiNode node, Emit emit) { if (node.IsActive) emit("button", -1, node.Rect); }

        private static bool Inside(UiRect r, UiRect view) => r.Y >= view.Y - 0.5f && r.Bottom <= view.Bottom + 0.5f && r.X >= view.X - 0.5f && r.Right <= view.Right + 0.5f;
    }

    /// <summary>
    /// 줄·카드·칸이 가리키는 데이터 id 읽기(사람이 줄의 글을 읽고 무엇인지 아는 것 대신). 화면 종류마다 한 줄로 등록한다.
    /// 정책은 이 값이 추적 목표의 id 묶음에 있는지만 본다.
    /// </summary>
    public static class ScreenSubjects
    {
        public delegate string Read(GameScreen screen, string element, int index);

        private static readonly List<(Type Screen, Read Read)> Readers = new List<(Type, Read)>();

        static ScreenSubjects()
        {
            Register(typeof(LocationScreen), (s, e, i) =>
            {
                var model = ((LocationScreen)s).Model;
                return e == LocationScreen.RowsId && model != null && i >= 0 && i < model.Rows.Count ? model.Rows[i].Id : null;
            });
            Register(typeof(DailyPlanScreen), (s, e, i) =>
            {
                var model = ((DailyPlanScreen)s).Model;
                return e == DailyPlanScreen.RouteGridId && model != null && i >= 0 && i < model.Routes.Count ? model.Routes[i].RouteId : null;
            });
            Register(typeof(CareerInterestScreen), (s, e, i) =>
            {
                var model = ((CareerInterestScreen)s).Model;
                return e == CareerInterestScreen.CardsId && i >= 0 && i < model.Cards.Length ? model.Cards[i].InterestId : null;
            });
            Register(typeof(ObjectiveJournalScreen), (s, e, i) =>
            {
                var items = ((ObjectiveJournalScreen)s).Items;
                return e == ObjectiveJournalScreen.ListId && i >= 0 && i < items.Count ? items[i].StableKey : null;
            });
            Register(typeof(Rootborn.Screens.Storage.StorageScreen), (s, e, i) =>
            {
                // 상자 화면의 칸: 그 칸에 든 아이템(빈 칸이면 null — 눌러도 아무 일도 없다).
                var model = ((Rootborn.Screens.Storage.StorageScreen)s).Model;
                var rows = e == Rootborn.Screens.Storage.StorageScreen.BagGridId ? model.Bag : e == Rootborn.Screens.Storage.StorageScreen.ChestGridId ? model.Chest : null;
                return rows != null && i >= 0 && i < rows.Count ? rows[i].Item.Id : null;
            });
            Register(typeof(FurniturePlacementScreen), (s, e, i) =>
            {
                const string prefix = "furniture.slot.";
                if (e == null || !e.StartsWith(prefix, StringComparison.Ordinal)) return null;
                return int.TryParse(e.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot)
                    ? ((FurniturePlacementScreen)s).Palette.At(slot)?.StableId : null;
            });
        }

        public static void Register(Type screen, Read read) => Readers.Add((screen, read));

        public static string Resolve(GameScreen screen, string element, int index)
        {
            Type t = screen.GetType();
            foreach (var r in Readers) if (r.Screen.IsAssignableFrom(t)) return r.Read(screen, element, index);
            return null;
        }
    }

    /// <summary>
    /// Core 목표가 가리키는 id 묶음: 목표 클래스의 공개 속성 중 이름이 Target으로 시작하는 글자 하나, Accepted로 시작하는 글자 목록.
    /// 여러 갈래 목표(Alternatives)는 갈래마다 모은다. 목표 종류를 하나하나 적지 않는다(새 목표 종류도 이 이름 약속이면 저절로 읽힌다).
    /// </summary>
    public static class ObjectiveIds
    {
        public static void Collect(object objective, HashSet<string> into)
        {
            if (objective == null) return;
            foreach (PropertyInfo p in objective.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                if (p.PropertyType == typeof(string) && p.Name.StartsWith("Target", StringComparison.Ordinal))
                {
                    if (p.GetValue(objective) is string id && id.Length > 0) into.Add(id);
                }
                else if (p.Name.StartsWith("Accepted", StringComparison.Ordinal) && p.GetValue(objective) is IEnumerable<string> ids)
                {
                    foreach (string id in ids) if (!string.IsNullOrEmpty(id)) into.Add(id);
                }
                else if (p.Name == "Alternatives" && p.GetValue(objective) is IEnumerable alternatives)
                {
                    foreach (object a in alternatives) Collect(a, into);
                }
            }
        }

        /// <summary>그 단계 목표의 클래스 이름들(정책의 목표 종류 힌트용).</summary>
        public static IEnumerable<string> KindNames(object objective)
        {
            if (objective == null) yield break;
            yield return objective.GetType().Name;
            if (objective.GetType().GetProperty("Alternatives")?.GetValue(objective) is IEnumerable alternatives)
                foreach (object a in alternatives)
                    foreach (string n in KindNames(a)) yield return n;
        }
    }
}
