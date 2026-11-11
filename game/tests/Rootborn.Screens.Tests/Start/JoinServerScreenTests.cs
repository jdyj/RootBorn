using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Start
{
    /// <summary>들어가는 쪽 흉내: 부른 것을 적어 두고 단계는 테스트가 정한다.</summary>
    internal sealed class FakeRoomJoin : IRoomJoin
    {
        public readonly List<(string Address, string Slot)> Begun = new List<(string, string)>();
        public int Cancelled;
        public RoomJoinPhase Phase { get; set; }
        public string FailureCode { get; set; } = string.Empty;

        public void Begin(string address, string slotId)
        {
            Begun.Add((address, slotId));
            Phase = RoomJoinPhase.Connecting;
        }

        public void Cancel()
        {
            Cancelled++;
            Phase = RoomJoinPhase.Idle;
        }
    }

    /// <summary>
    /// 서버 주소로 들어가기(시작 화면 단추 → 주소 모달)와 끊김 알림. 창 없이 화면을 눌러 보는 자동 테스트다(화면 근거 아님).
    /// 접속 자체는 Host 테스트(MultiplayerJoinHostTests)가 실제 UDP 루프백으로 본다.
    /// </summary>
    public sealed class JoinServerScreenTests
    {
        private static JoinServerScreen OpenJoin(StartMenuFixture t)
        {
            t.OpenSlots();
            t.Click(SaveSlotScreen.JoinId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.JoinServer), Is.True);
            return t.Screen<JoinServerScreen>(ScreenCatalog.JoinServer);
        }

        private static string Status(StartMenuFixture t) => ((UiTextBlock)t.Node(JoinServerScreen.StatusId)).Text;

        [Test]
        public void JoinButton_ShowsOnlyWhenThereIsAJoiner()
        {
            using var t = new StartMenuFixture();
            t.OpenSlots();
            Assert.That(t.Node(SaveSlotScreen.JoinId).Visible, Is.False, "들어가는 쪽이 없으면(테스트·견본) 단추가 없다");
            t.Stack.Close(ScreenCatalog.SaveSlotSelect);

            t.Menu.Join = new FakeRoomJoin();
            t.OpenSlots();
            Assert.That(t.Node(SaveSlotScreen.JoinId).IsActive, Is.True);
            Assert.That(((UiButton)t.Node(SaveSlotScreen.JoinId)).Text, Is.EqualTo("서버 주소로 들어가기"));
        }

        [Test]
        public void Modal_ListsOnlyStories_TypingAndJoin_AsksToJoinWithTheChosenSlot()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            t.WriteStory("slot-2", "바다");
            var join = new FakeRoomJoin();
            t.Menu.Join = join;
            JoinServerScreen screen = OpenJoin(t);

            Assert.That(screen.SlotIds, Is.EqualTo(new[] { "slot-0", "slot-2" }), "이야기가 있는 슬롯만");
            Assert.That(t.Stack.Tree.Focused?.Id, Is.EqualTo(JoinServerScreen.AddressId), "주소 칸에 바로 친다");
            Assert.That(screen.StatusKey, Is.EqualTo("ui.join.temporary-save"), "임시: 진행은 이 PC에 저장된다고 알린다");
            Assert.That(t.Node(JoinServerScreen.SlotsId).Rect.Y, Is.GreaterThan(t.Node(JoinServerScreen.AddressId).Rect.Y));

            var tabs = (UiTabBar)t.Node(JoinServerScreen.SlotsId);
            UiRect second = tabs.TabRect(1);
            t.Stack.Pointer(second.CenterX, second.CenterY, false, 0);
            t.Stack.Pointer(second.CenterX, second.CenterY, true, 0);
            t.Stack.Pointer(second.CenterX, second.CenterY, false, 0);
            Assert.That(screen.SelectedSlotId, Is.EqualTo("slot-2"));

            t.Click(JoinServerScreen.AddressId);
            t.Stack.TextInput(" 10.0.0.5:7777 ");
            t.Click(JoinServerScreen.ConnectId);
            Assert.That(join.Begun, Is.EqualTo(new[] { ("10.0.0.5:7777", "slot-2") }), "앞뒤 빈칸은 뗀다");
            Assert.That(screen.StatusKey, Is.EqualTo("ui.join.connecting"));
            Assert.That(t.Node(JoinServerScreen.ConnectId).IsActive, Is.False, "접속하는 동안 다시 누르지 못한다");

            join.Phase = RoomJoinPhase.Failed;
            join.FailureCode = "game version mismatch";
            t.Stack.NotifyChanged(RoomJoinText.Topic);
            Assert.That(screen.StatusKey, Is.EqualTo("ui.join.reason.game-version-mismatch"));
            Assert.That(Status(t), Is.EqualTo("서버와 게임 버전이 달라요."));
            Assert.That(t.Node(JoinServerScreen.ConnectId).IsActive, Is.True, "실패하면 다시 시도할 수 있다");
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys.Concat(t.UsedKeys()));
        }

        [Test]
        public void UnknownReasonCode_ShowsTheGenericReason()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            var join = new FakeRoomJoin { Phase = RoomJoinPhase.Failed, FailureCode = "something new from a later server" };
            t.Menu.Join = join;
            JoinServerScreen screen = OpenJoin(t);
            Assert.That(screen.StatusKey, Is.EqualTo(RoomJoinText.UnknownReasonKey));
        }

        [Test]
        public void NoStory_CannotJoin_AndSaysWhy()
        {
            using var t = new StartMenuFixture();
            t.Menu.Join = new FakeRoomJoin();
            JoinServerScreen screen = OpenJoin(t);
            Assert.That(screen.SlotIds, Is.Empty);
            Assert.That(screen.StatusKey, Is.EqualTo("ui.join.no-character"));
            Assert.That(t.Node(JoinServerScreen.ConnectId).IsActive, Is.False);
            Assert.That(t.Node(JoinServerScreen.SlotsId).Visible, Is.False);
        }

        [Test]
        public void Cancel_WhileConnecting_StopsAndCloses()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-1", "하늘");
            var join = new FakeRoomJoin();
            t.Menu.Join = join;
            OpenJoin(t);
            t.Click(JoinServerScreen.AddressId);
            t.Stack.TextInput("127.0.0.1:1"); // 화면 테스트의 글자일 뿐 접속하지 않는다
            t.Click(JoinServerScreen.ConnectId);
            t.Click(JoinServerScreen.CancelId);
            Assert.That(join.Cancelled, Is.EqualTo(1));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.JoinServer), Is.False);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
        }

        [Test]
        public void OpenedWithAnAddressAndSlot_FillsThemIn()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            t.WriteStory("slot-1", "바다");
            t.Menu.Join = new FakeRoomJoin();
            t.OpenSlots();
            t.Stack.Open(ScreenCatalog.JoinServer, argument: new JoinServerOpen(t.Menu, "host.example:7777", "slot-1"));
            var screen = t.Screen<JoinServerScreen>(ScreenCatalog.JoinServer);
            Assert.That(((UiTextField)t.Node(JoinServerScreen.AddressId)).Text, Is.EqualTo("host.example:7777"));
            Assert.That(screen.SelectedSlotId, Is.EqualTo("slot-1"));
        }

        [Test]
        public void OpenedWithoutArgument_DoesNotThrow()
        {
            // Host의 등록 표 순회 테스트(영어 화면, 시험용 테마)는 인자 없이 연다.
            using var t = new StartMenuFixture();
            t.Stack.Open(ScreenCatalog.JoinServer);
            t.Stack.Open(ScreenCatalog.RoomNotice);
            t.Emit();
            Assert.That(t.Screen<RoomNoticeScreen>(ScreenCatalog.RoomNotice).ReasonKey, Is.EqualTo(RoomJoinText.UnknownReasonKey));
        }

        [Test]
        public void Notice_ShowsTheReason_AndOkCloses()
        {
            using var t = new StartMenuFixture();
            t.OpenSlots();
            t.Stack.Open(ScreenCatalog.RoomNotice, argument: new RoomNoticeOpen("server shutdown"));
            t.Emit();
            var notice = t.Screen<RoomNoticeScreen>(ScreenCatalog.RoomNotice);
            Assert.That(notice.ReasonKey, Is.EqualTo("ui.join.reason.server-shutdown"));
            Assert.That(((UiTextBlock)t.Node(RoomNoticeScreen.ReasonId)).Text, Is.EqualTo("서버가 닫혔어요."));
            Assert.That(t.LabelText(RoomNoticeScreen.TitleId), Is.EqualTo("서버와 연결이 끊겼어요"));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, notice.TextKeys);
            t.Click(RoomNoticeScreen.OkId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.RoomNotice), Is.False);
        }

        [Test]
        public void ReasonKeys_AreMadeFromTheCode()
        {
            Assert.That(RoomJoinText.ReasonKey("replaced by new connection"), Is.EqualTo("ui.join.reason.replaced-by-new-connection"));
            Assert.That(RoomJoinText.ReasonKey(""), Is.EqualTo(RoomJoinText.UnknownReasonKey));
            Assert.That(RoomJoinText.ReasonKey(null), Is.EqualTo(RoomJoinText.UnknownReasonKey));
        }

        /// <summary>들어가는 쪽이 스스로 정하는 이유 코드의 문구가 모든 언어에 있다(서버 코드는 Host 테스트가 Net의 상수로 본다).</summary>
        [Test]
        public void JoinerReasons_HaveTextInEveryLanguage()
        {
            GameText text = TestData.Text();
            string[] codes = { RoomJoinText.BadAddress, RoomJoinText.CannotConnect, RoomJoinText.Unreachable, RoomJoinText.EntryFailed };
            KnownMissingKeys.AssertOnlyKnownMissing(text, codes.Select(RoomJoinText.ReasonKey).Append(RoomJoinText.UnknownReasonKey));
        }

        [TestCaseSource(nameof(Languages))]
        public void DoubleLengthText_StaysInsideTheBoxes(string language)
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            t.WriteStory("slot-1", "바다");
            t.WriteStory("slot-2", "구름");
            var join = new FakeRoomJoin { Phase = RoomJoinPhase.Failed, FailureCode = "replaced by new connection" };
            t.Menu.Join = join;
            var services = new ScreenServices(LongText.Create(language), null);
            var stack = new ScreenStack(ScreenCatalog.Create(), services);
            stack.Open(ScreenCatalog.SaveSlotSelect, argument: t.Menu);
            stack.Open(ScreenCatalog.JoinServer, argument: new JoinServerOpen(t.Menu, "a-very-long-host-name.example.org:65535"));
            Settle(stack);
            var found = new List<string>();
            Inspect(stack.Find(ScreenCatalog.JoinServer), found);
            stack.Close(ScreenCatalog.JoinServer);
            stack.Open(ScreenCatalog.RoomNotice, argument: new RoomNoticeOpen("replaced by new connection"));
            Settle(stack);
            Inspect(stack.Find(ScreenCatalog.RoomNotice), found);
            Inspect(stack.Find(ScreenCatalog.SaveSlotSelect), found, onlyId: SaveSlotScreen.JoinId);
            Assert.That(found, Is.Empty);
        }

        private static IEnumerable<string> Languages() => LongText.MergedTables().Keys.OrderBy(k => k);

        private static void Settle(ScreenStack stack)
        {
            for (int i = 0; i < 2; i++)
            {
                stack.Layout(1920, 1080);
                stack.Emit(new DrawList(), new WideMeasurer());
            }
            stack.Layout(1920, 1080);
        }

        /// <summary>글자가 가장 가까운 패널(또는 단추 자신) 밖으로 나가는지, 단추끼리 겹치는지.</summary>
        private static void Inspect(GameScreen screen, List<string> found, string onlyId = null)
        {
            var measurer = new WideMeasurer();
            var buttons = new List<UiNode>();
            foreach (UiNode node in Walk(screen.Root))
            {
                if (!node.IsVisibleInTree() || (onlyId != null && node.Id != onlyId)) continue;
                if (node is UiButton) buttons.Add(node);
                var list = new DrawList();
                node.Emit(list, measurer);
                UiRect box = node is UiButton || node is UiTextField || node is UiTabBar ? node.Rect : PanelOf(node, screen.Root).Rect;
                foreach (DrawCommand c in list.Commands.Where(c => c.Kind == DrawKind.Text))
                {
                    float right = c.Rect.X + measurer.Width(c.Text, c.FontSize), bottom = c.Rect.Y + measurer.LineHeight(c.FontSize);
                    if (c.Rect.X < box.X - 0.5f || right > box.Right + 0.5f || c.Rect.Y < box.Y - 0.5f || bottom > box.Bottom + 0.5f)
                    {
                        found.Add(screen.Id + " | " + node.Id + " | 밖으로 나감: " + c.Text);
                        break;
                    }
                }
            }
            for (int i = 0; i < buttons.Count; i++)
                for (int j = i + 1; j < buttons.Count; j++)
                {
                    UiRect a = buttons[i].Rect, b = buttons[j].Rect;
                    if (a.X < b.Right - 0.5f && b.X < a.Right - 0.5f && a.Y < b.Bottom - 0.5f && b.Y < a.Bottom - 0.5f)
                        found.Add(screen.Id + " | " + buttons[i].Id + "+" + buttons[j].Id + " | 단추 겹침");
                }
        }

        private static IEnumerable<UiNode> Walk(UiNode root)
        {
            yield return root;
            foreach (UiNode child in root.Children)
                foreach (UiNode node in Walk(child)) yield return node;
        }

        private static UiNode PanelOf(UiNode node, UiNode root)
        {
            for (UiNode p = node.Parent; p != null; p = p.Parent)
                if (p is UiPanel || ReferenceEquals(p, root)) return p;
            return root;
        }
    }
}
