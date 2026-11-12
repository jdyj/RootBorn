using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Screens.Tests.AdultFacility;
using Rootborn.Screens.Tests.Storage;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 키보드만으로 모든 화면: 등록된 모든 패널·모달 화면(ScreenCatalog에서 읽는다 — 목록을 손으로 적지 않는다)을 열어,
    /// 그 화면의 키 규칙(이동을 막는 화면은 방향 키, 막지 않는 화면은 Shift+방향 키)만으로 ScreenStack.Key를 눌러 포커스를 옮긴다.
    /// 본다: 처음 포커스, 모든 누를 수 있는 요소에 닿는지, 갇히는 곳, 포커스 테두리가 보이는지, 스크롤 목록이 고른 줄을 따라가는지, 닫는 길.
    /// HUD·알림 층은 키보드 포커스를 받지 않으므로(ScreenStack.FocusScreen) 표에만 적는다. 결과는 화면별 표로 출력한다. 자동 테스트다.
    /// </summary>
    public sealed class KeyboardReachTests
    {
        private static readonly ScreenKey[] Directions = { ScreenKey.Up, ScreenKey.Down, ScreenKey.Left, ScreenKey.Right };

        /// <summary>화면 하나를 연 판(TestSession 또는 성인 시설 준비). 같은 화면을 새로 열어 단추를 하나씩 눌러 볼 때 다시 만든다.</summary>
        private sealed class Harness : IDisposable
        {
            private readonly IDisposable _owner;
            public Harness(ScreenStack stack, IDisposable owner) { Stack = stack; _owner = owner; }
            public ScreenStack Stack { get; }
            public void Dispose() => _owner.Dispose();
        }

        /// <summary>그냥 Open(id)로는 내용이 없어 바로 닫히거나 비는 화면을 실제로 여는 길. 나머지는 Open(id).</summary>
        private static Harness OpenScreen(ScreenEntry entry)
        {
            if (entry.Id == ScreenCatalog.Facility)
            {
                var facility = new FacilityFixture(FacilityFixture.AlreadyAdultAtLevelOne);
                Assert.That(facility.OpenAt(), Is.True, "성인 시설 앞 상호작용");
                return new Harness(facility.Stack, facility);
            }

            var t = new TestSession();
            ScreenStack stack = t.Stack;
            stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            if (!entry.WithoutWorld) stack.OpenHud();
            stack.SetInteractionTarget(new InteractionTargetView("location-activity", "location.library", 800, 500, 64));
            if (ScreenOpeners.TryOpen(entry.Id, stack, t.Services, () => StartMenus.Create(t), () => StorageScreenTests.PlaceChest(t))) { }
            else if (entry.Id == ScreenCatalog.DailyEvent)
            {
                // 장소 화면을 닫으면 하루 이벤트가 열린다(선택지가 있는 실제 이벤트).
                stack.Interaction(new InteractionEvent("Town", "location-activity", "location.library"));
                stack.Close(ScreenCatalog.Location);
            }
            else if (entry.Id == ScreenCatalog.Exploration) stack.Interaction(new InteractionEvent("Town", "exploration", "exploration.test.alley-crate"));
            else if (entry.Id == ScreenCatalog.Wardrobe)
            {
                t.Services.Wardrobe = new Rootborn.Screens.Tests.Records.TestWardrobe(t.Session);
                stack.Open(entry.Id);
            }
            else if (!stack.IsOpen(entry.Id)) stack.Open(entry.Id);
            Settle(stack);
            return new Harness(stack, t);
        }

        private static void Settle(ScreenStack stack)
        {
            for (int i = 0; i < 2; i++)
            {
                stack.Layout(1920, 1080);
                stack.Emit(new DrawList(), new TestMeasurer());
            }
            stack.Layout(1920, 1080);
        }

        /// <summary>누를 수 있는 요소: 포커스를 받거나, 클릭·누름을 처리하는 요소(패널처럼 뒤를 가리기만 하는 것은 빼다).</summary>
        internal static bool IsPressable(UiNode node)
        {
            if (!node.Interactive) return false;
            if (node.Focusable) return true;
            if (node is UiSectionScrollView view) return view.MaxScroll > 0; // 글이 넘칠 때만 할 일이 있다(키로 스크롤)
            Type type = node.GetType();
            if (Overrides(type, nameof(UiNode.OnClick)) || Overrides(type, nameof(UiNode.OnPress))) return true;
            FieldInfo clicked = typeof(UiNode).GetField("Clicked", BindingFlags.Instance | BindingFlags.NonPublic);
            return clicked?.GetValue(node) != null;
        }

        private static bool Overrides(Type type, string method)
        {
            MethodInfo m = type.GetMethod(method, BindingFlags.Instance | BindingFlags.Public);
            return m != null && m.DeclaringType != typeof(UiNode);
        }

        private static List<UiNode> Pressables(GameScreen screen)
        {
            var into = new List<UiNode>();
            Walk(screen.Root, into);
            return into;
        }

        private static void Walk(UiNode node, List<UiNode> into)
        {
            if (!node.Visible || !node.Enabled || node.Covered) return;
            if (IsPressable(node) && node.Rect.W > 0 && node.Rect.H > 0) into.Add(node);
            foreach (UiNode child in node.Children) Walk(child, into);
        }

        private static string Name(UiNode node) => node == null ? "(없음)" : node.Id ?? node.GetType().Name;

        /// <summary>요소 안쪽 상태(고른 줄, 탭, 값)를 기억해 두었다가 방향 키 하나를 눌러 본 뒤 되돌린다.</summary>
        private readonly struct InnerState
        {
            private readonly UiNode _node;
            private readonly int _selected, _tab;
            private readonly float _scroll, _value;

            public InnerState(UiNode node)
            {
                _node = node; _selected = -1; _tab = 0; _scroll = 0; _value = 0;
                switch (node)
                {
                    case UiScrollList list: _selected = list.SelectedIndex; _scroll = list.ScrollOffset; break;
                    case UiCardScroller cards: _selected = cards.SelectedIndex; _scroll = cards.ScrollOffset; break;
                    case UiTabBar tabs: _tab = tabs.Selected; break;
                    case UiSlider slider: _value = slider.Value; break;
                    case UiSectionScrollView view: _scroll = view.ScrollOffset; break;
                }
            }

            public void Restore(ScreenStack stack, ScreenKey pressed, bool shift)
            {
                switch (_node)
                {
                    case UiScrollList list: list.SelectedIndex = _selected; list.ScrollOffset = _scroll; break;
                    case UiCardScroller cards: cards.SelectedIndex = _selected; cards.ScrollOffset = _scroll; break;
                    case UiTabBar tabs:
                        stack.Tree.SetFocus(tabs);
                        for (int i = 0; i < 64 && tabs.Selected != _tab; i++) // 탭 내용도 되돌린다(키로 고르면 화면이 탭 내용을 바꾼다)
                            stack.Key(tabs.Selected > _tab ? ScreenKey.Left : ScreenKey.Right, shift);
                        break;
                    case UiSlider slider: slider.Value = _value; break;
                    case UiSectionScrollView view: view.ScrollOffset = _scroll; break;
                }
            }
        }

        private sealed class ScreenReport
        {
            public string Id, Layer, Keys, Opener = "-", Initial = "-", Close = "-";
            public int Pressable, Reached;
            public readonly List<string> Unreached = new List<string>();
            public readonly List<string> Trapped = new List<string>();
            public readonly List<string> NoRing = new List<string>();
            public readonly List<string> ScrollLost = new List<string>();
            public readonly List<string> EnterDiffers = new List<string>();
            public bool KeyboardFocus = true;
        }

        [Test]
        public void EveryScreen_CanBeWorkedWithTheKeyboardAlone()
        {
            var reports = new List<ScreenReport>();
            foreach (ScreenEntry entry in ScreenCatalog.Create().Screens.OrderBy(e => (int)e.Layer).ThenBy(e => e.Id, StringComparer.Ordinal))
                reports.Add(Audit(entry));

            TestContext.WriteLine(Table(reports));

            var problems = new List<string>();
            foreach (ScreenReport r in reports.Where(r => r.KeyboardFocus))
            {
                if (r.Initial == "(없음)") problems.Add(r.Id + ": 처음 열었을 때 포커스가 없다");
                foreach (string u in r.Unreached) problems.Add(r.Id + ": 닿지 않음 " + u);
                foreach (string u in r.Trapped) problems.Add(r.Id + ": 갇힘 " + u);
                foreach (string u in r.NoRing) problems.Add(r.Id + ": 포커스 테두리 없음 " + u);
                foreach (string u in r.ScrollLost) problems.Add(r.Id + ": 스크롤이 따라가지 않음 " + u);
                foreach (string u in r.EnterDiffers) problems.Add(r.Id + ": Enter가 클릭과 다름 " + u);
                if (r.Close == "없음") problems.Add(r.Id + ": 키보드로 닫는 길이 없다");
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        /// <summary>
        /// 모든 화면의 모든 글자 칸: 포커스가 들어가면 글자가 먼저다 — 키 배정(I, C, L, E, Space, R, A, D, F1, Tab)이 새지 않고(화면이 열리거나 닫히지 않는다),
        /// 왼쪽·오른쪽은 칸 안, 위·아래(막는 화면) 또는 Shift+위·아래(막지 않는 화면)로 나가며, Esc는 칸을 놓고 화면은 남는다. Enter가 하는 일은 표에 적는다.
        /// </summary>
        [Test]
        public void EveryTextField_KeepsTypingFirst_AndCanBeLeftWithArrows()
        {
            var rows = new List<string> { "| 화면 | 글자 칸 | 새는 키 | 나가는 키 | Esc | Enter |", "|---|---|---|---|---|---|" };
            var problems = new List<string>();
            int fields = 0;
            ScreenKey[] bindings = { ScreenKey.Inventory, ScreenKey.Status, ScreenKey.LocationLog, ScreenKey.Interact, ScreenKey.Space, ScreenKey.Rotate,
                ScreenKey.AltLeft, ScreenKey.AltRight, ScreenKey.Help, ScreenKey.Tab };
            foreach (ScreenEntry entry in ScreenCatalog.Create().Screens.OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                if (entry.Layer == ScreenLayer.Hud || entry.Layer == ScreenLayer.Notice) continue;
                List<string> ids;
                using (Harness h = OpenScreen(entry))
                    ids = Walked(h.Stack.Find(entry.Id).Root).OfType<UiTextField>().Where(f => f.IsActive).Select(f => f.Id).ToList();
                foreach (string id in ids)
                {
                    fields++;
                    using Harness h = OpenScreen(entry);
                    ScreenStack stack = h.Stack;
                    GameScreen screen = stack.Find(entry.Id);
                    var field = (UiTextField)screen.Root.Find(id);
                    bool shift = !screen.BlocksMovement;
                    var open = stack.OpenScreens.Select(o => o.Id).ToList();

                    var leaked = new List<string>();
                    foreach (ScreenKey key in bindings)
                    {
                        stack.Tree.SetFocus(field);
                        bool taken = stack.Key(key, false);
                        if (!taken || stack.Tree.Focused != field || !stack.OpenScreens.Select(o => o.Id).SequenceEqual(open)) leaked.Add(key.ToString());
                    }
                    foreach (ScreenKey key in new[] { ScreenKey.Left, ScreenKey.Right })
                    {
                        stack.Tree.SetFocus(field);
                        stack.Key(key, shift);
                        if (stack.Tree.Focused != field) leaked.Add(key + "(칸을 나감)");
                    }

                    var leaves = new List<string>();
                    foreach (ScreenKey key in new[] { ScreenKey.Up, ScreenKey.Down })
                    {
                        stack.Tree.SetFocus(field);
                        stack.Key(key, shift);
                        if (stack.Tree.Focused != field) leaves.Add((shift ? "Shift+" : "") + key);
                    }

                    stack.Tree.SetFocus(field);
                    stack.Key(ScreenKey.Escape, false);
                    string esc = stack.Tree.Focused == field ? "칸에 남음" : !screen.IsOpen ? "화면을 닫음" : "칸을 놓음";

                    stack.Tree.SetFocus(field);
                    int submitted = 0;
                    field.Submitted += _ => submitted++;
                    stack.Key(ScreenKey.Enter, false);
                    string enter = submitted > 0 ? "보냄" + (!screen.IsOpen ? "(화면이 닫힘)" : stack.Tree.Focused != field ? "(다음으로)" : "") : "하는 일 없음";

                    rows.Add("| " + entry.Id + " | " + id + " | " + Join(leaked) + " | " + Join(leaves) + " | " + esc + " | " + enter + " |");
                    if (leaked.Count > 0) problems.Add(entry.Id + " " + id + ": 새는 키 " + Join(leaked));
                    if (leaves.Count == 0 && Pressables(screen).Count > 1) problems.Add(entry.Id + " " + id + ": 방향 키로 나갈 수 없다");
                    if (esc == "칸에 남음") problems.Add(entry.Id + " " + id + ": Esc로 칸을 놓을 수 없다");
                }
            }
            TestContext.WriteLine(string.Join("\n", rows));
            Assert.That(fields, Is.GreaterThanOrEqualTo(3), "글자 칸이 있는 화면을 열었다(캐릭터 이름, 가구 검색, 방 주소)");
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        private static IEnumerable<UiNode> Walked(UiNode node)
        {
            yield return node;
            foreach (UiNode child in node.Children)
                foreach (UiNode n in Walked(child)) yield return n;
        }

        private static ScreenReport Audit(ScreenEntry entry)
        {
            var report = new ScreenReport { Id = entry.Id, Layer = entry.Layer.ToString(), Opener = OpenerOf(entry) };
            if (entry.Layer == ScreenLayer.Hud || entry.Layer == ScreenLayer.Notice)
            {
                report.KeyboardFocus = false;
                report.Keys = "포커스 없음(" + entry.Layer + " 층)";
                return report;
            }

            using Harness h = OpenScreen(entry);
            ScreenStack stack = h.Stack;
            GameScreen screen = stack.Find(entry.Id);
            Assert.That(screen?.IsOpen, Is.True, entry.Id + "가 열려야 한다");
            bool shift = !screen.BlocksMovement;
            report.Keys = shift ? "Shift+방향 키" : "방향 키";

            List<UiNode> pressables = Pressables(screen);
            report.Pressable = pressables.Count;

            UiNode initial = stack.Tree.Focused != null && IsInside(stack.Tree.Focused, screen.Root) ? stack.Tree.Focused : null;
            report.Initial = Name(initial);
            UiNode start = initial;
            if (start == null && pressables.Count > 0)
            {
                stack.Key(ScreenKey.Down, shift);
                start = stack.Tree.Focused;
            }

            // 포커스 그래프: 닿은 요소마다 네 방향 키를 실제로 눌러 본다.
            var edges = new Dictionary<UiNode, HashSet<UiNode>>();
            var queue = new Queue<UiNode>();
            if (start != null) { queue.Enqueue(start); edges[start] = null; }
            while (queue.Count > 0)
            {
                UiNode from = queue.Dequeue();
                var outs = new HashSet<UiNode>();
                edges[from] = outs;
                foreach (ScreenKey key in Directions)
                {
                    if (!screen.IsOpen) break;
                    stack.Tree.SetFocus(from);
                    var inner = new InnerState(from);
                    UiNode to = PressUntilLeaving(stack, from, key, shift);
                    if (to == from) inner.Restore(stack, key, shift);
                    if (to == null || to == from || !IsInside(to, screen.Root)) continue;
                    outs.Add(to);
                    if (!edges.ContainsKey(to)) { edges[to] = null; queue.Enqueue(to); }
                }
                if (!screen.IsOpen) break;
            }

            var reached = new HashSet<UiNode>(edges.Keys);
            report.Reached = pressables.Count(reached.Contains);
            foreach (UiNode p in pressables) if (!reached.Contains(p)) report.Unreached.Add(Name(p));

            // 갇힘: 그 요소에서 출발해 닿은 다른 요소로 갈 수 없다.
            if (reached.Count > 1)
                foreach (UiNode node in reached)
                {
                    var seen = new HashSet<UiNode> { node };
                    var walk = new Queue<UiNode>(new[] { node });
                    while (walk.Count > 0)
                        foreach (UiNode next in edges[walk.Dequeue()] ?? new HashSet<UiNode>())
                            if (seen.Add(next)) walk.Enqueue(next);
                    if (seen.Count < reached.Count) report.Trapped.Add(Name(node) + "(" + (seen.Count - 1) + "/" + (reached.Count - 1) + "곳)");
                }

            foreach (UiNode node in reached)
            {
                if (!screen.IsOpen) break;
                if (!RingVisible(stack, node)) report.NoRing.Add(Name(node));
                string lost = ScrollFollows(stack, node, shift);
                if (lost != null) report.ScrollLost.Add(Name(node) + " " + lost);
                string enter = EnterMatchesClick(node);
                if (enter != null) report.EnterDiffers.Add(Name(node) + " " + enter);
            }

            report.Close = ClosePath(entry, reached.Select(n => n.Id).Where(id => id != null).ToList());
            return report;
        }

        /// <summary>
        /// 방향 키 하나를 누른다. 요소가 키를 안에서 썼으면(목록의 줄, 탭, 값이 바뀜) 바뀌지 않을 때까지 같은 키를 더 누른다 — 사람이 끝까지 누르는 것과 같다.
        /// 포커스가 옮겨 간 요소(또는 그대로면 그 요소)를 돌려준다.
        /// </summary>
        private static UiNode PressUntilLeaving(ScreenStack stack, UiNode from, ScreenKey key, bool shift)
        {
            for (int i = 0; i < 2000; i++)
            {
                string before = InnerSignature(from);
                stack.Key(key, shift);
                stack.Layout(1920, 1080);
                if (stack.Tree.Focused != from) return stack.Tree.Focused;
                if (InnerSignature(from) == before) return from;
            }
            return from;
        }

        private static string InnerSignature(UiNode node) => node switch
        {
            UiScrollList list => list.SelectedIndex + "/" + list.ScrollOffset,
            UiCardScroller cards => cards.SelectedIndex + "/" + cards.ScrollOffset,
            UiTabBar tabs => tabs.Selected.ToString(),
            UiSlider slider => slider.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            UiSectionScrollView view => view.ScrollOffset.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => "",
        };

        /// <summary>
        /// 화면을 여는 길(키보드로 열 수 있는가). 키 배정, 상호작용 종류(E), 시작 화면이 아니면 HUD 단추나 다른 화면의 단추로만 열린다 —
        /// HUD는 키보드 포커스를 받지 않으므로(ScreenStack.FocusScreen) 그런 화면은 키보드만으로 열 수 없다. 표에만 적는다(여는 키를 더하는 것은 구성 변경).
        /// </summary>
        private static string OpenerOf(ScreenEntry entry)
        {
            ScreenRegistry registry = ScreenCatalog.Create();
            foreach (var pair in registry.KeyBindings) if (pair.Value == entry.Id) return "키 " + pair.Key;
            if (entry.OpenedByInteraction) return "상호작용";
            if (entry.WithoutWorld) return "시작 화면";
            return "키 없음(HUD·다른 화면의 단추, 게임 진행)";
        }

        private static bool IsInside(UiNode node, UiNode root)
        {
            for (UiNode n = node; n != null; n = n.Parent) if (n == root) return true;
            return false;
        }

        /// <summary>키보드로 포커스를 옮겼을 때 그 요소가 강조색 테두리를 더 그리는가(포커스가 없을 때와 비교).</summary>
        private static bool RingVisible(ScreenStack stack, UiNode node)
        {
            stack.Tree.SetFocus(null);
            int without = AccentFills(node);
            stack.Tree.SetFocus(node);
            int with = AccentFills(node);
            return with > without;
        }

        private static int AccentFills(UiNode node)
        {
            var list = new DrawList();
            node.Emit(list, new TestMeasurer());
            return list.Commands.Count(c => c.Kind == DrawKind.Fill && c.Color.Equals(UiTheme.Accent));
        }

        /// <summary>스크롤이 있는 목록: 키로 끝까지 내려가며 고른 줄·칸이 늘 보이는지. 문제가 없으면 null.</summary>
        private static string ScrollFollows(ScreenStack stack, UiNode node, bool shift)
        {
            var inner = new InnerState(node);
            try
            {
                switch (node)
                {
                    case UiScrollList list when list.MaxScroll > 0:
                        stack.Tree.SetFocus(list);
                        for (int i = 0; i < list.RowCount + 1 && stack.Tree.Focused == list; i++)
                        {
                            stack.Key(ScreenKey.Down, shift);
                            if (stack.Tree.Focused != list || list.SelectedIndex < 0) break;
                            if (!list.TryGetRowRect(list.SelectedIndex, out UiRect r) || r.H < list.RowHeight - 0.5f)
                                return "줄 " + list.SelectedIndex + "이 보이지 않는다";
                        }
                        return list.SelectedIndex == list.RowCount - 1 || stack.Tree.Focused != list ? null : "마지막 줄에 닿지 못했다";
                    case UiCardGrid grid when grid.MaxScroll > 0:
                        stack.Tree.SetFocus(grid);
                        for (int i = 0; i < grid.ItemCount + 1 && stack.Tree.Focused == grid; i++)
                        {
                            stack.Key(ScreenKey.Down, shift);
                            if (stack.Tree.Focused != grid || grid.SelectedIndex < 0) break;
                            UiRect cell = grid.CellRect(grid.SelectedIndex), view = grid.View;
                            if (cell.Y < view.Y - 0.5f || cell.Bottom > view.Bottom + 0.5f) return "칸 " + grid.SelectedIndex + "이 보이지 않는다";
                        }
                        return null;
                    case UiCardRow row when row.MaxScroll > 0:
                        stack.Tree.SetFocus(row);
                        for (int i = 0; i < row.CardCount + 1 && stack.Tree.Focused == row; i++)
                        {
                            stack.Key(ScreenKey.Right, shift);
                            if (stack.Tree.Focused != row || row.SelectedIndex < 0) break;
                            UiRect card = row.CardRect(row.SelectedIndex), view = row.View;
                            if (card.X < view.X - 0.5f || card.Right > view.Right + 0.5f) return "카드 " + row.SelectedIndex + "이 보이지 않는다";
                        }
                        return null;
                    default:
                        return null;
                }
            }
            finally
            {
                inner.Restore(stack, ScreenKey.Down, shift);
            }
        }

        /// <summary>
        /// Enter(UiTree.Submit → OnSubmit)가 포인터 클릭(OnClick)과 같은 일을 하는가. OnSubmit을 고치지 않은 요소는 가운데를 클릭한 것과 같다.
        /// 둘 다 고친 요소는 부품마다 정해 둔 키 쓰임이 있다(목록·카드: 고른 줄을 실행, 글자 칸: 보내기, 탭·미끄럼 막대: 방향 키로 바꾼다).
        /// 그 밖의 종류가 둘 다 고쳤으면 알린다.
        /// </summary>
        private static string EnterMatchesClick(UiNode node)
        {
            Type type = node.GetType();
            if (!Overrides(type, nameof(UiNode.OnSubmit))) return null;
            if (node is UiScrollList || node is UiCardScroller || node is UiTextField || node is UiTabBar || node is UiSlider) return null;
            if (node is Rootborn.Screens.Objectives.JournalList) return null;
            if (node is UiSectionScrollView) return null; // 스크롤만 한다(클릭도 Enter도 하는 일이 없다) // 고른 줄을 실행한다(줄을 클릭한 것과 같다)
            return "(" + type.Name + "가 OnSubmit을 따로 정한다)";
        }

        /// <summary>닫는 길: Esc(두 번까지 — 첫 번은 포커스를 풀 수 있다), 아니면 닿는 요소 하나를 Enter로 눌러 닫히는 것(새로 연 판에서 하나씩).</summary>
        private static string ClosePath(ScreenEntry entry, List<string> reachedIds)
        {
            using (Harness h = OpenScreen(entry))
            {
                bool shift = !h.Stack.Find(entry.Id).BlocksMovement;
                for (int i = 0; i < 2 && h.Stack.IsOpen(entry.Id); i++) h.Stack.Key(ScreenKey.Escape, shift);
                if (!h.Stack.IsOpen(entry.Id)) return "Esc";
            }
            foreach (string id in reachedIds)
            {
                using Harness h = OpenScreen(entry);
                UiNode node = h.Stack.Find(entry.Id).Root.Find(id);
                if (node == null || !node.IsActive) continue;
                h.Stack.Tree.SetFocus(node);
                h.Stack.Key(ScreenKey.Enter, !h.Stack.Find(entry.Id).BlocksMovement);
                if (!h.Stack.IsOpen(entry.Id)) return "Enter " + id;
            }
            return "없음";
        }

        private static string Table(List<ScreenReport> reports)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| 화면 | 층 | 여는 길 | 옮기는 키 | 누를 것 | 닿음 | 처음 포커스 | 닫기 | 닿지 않음 | 갇힘 | 테두리 없음 | 스크롤 | Enter≠클릭 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (ScreenReport r in reports)
                sb.AppendLine("| " + r.Id + " | " + r.Layer + " | " + r.Opener + " | " + r.Keys + " | " + (r.KeyboardFocus ? r.Pressable.ToString() : "-") + " | "
                    + (r.KeyboardFocus ? r.Reached.ToString() : "-") + " | " + r.Initial + " | " + r.Close + " | " + Join(r.Unreached) + " | "
                    + Join(r.Trapped) + " | " + Join(r.NoRing) + " | " + Join(r.ScrollLost) + " | " + Join(r.EnterDiffers) + " |");
            return sb.ToString();
        }

        private static string Join(List<string> items) => items.Count == 0 ? "" : string.Join(", ", items);
    }
}
