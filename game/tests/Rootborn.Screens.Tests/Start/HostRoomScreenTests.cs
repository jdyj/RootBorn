using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Screens.Settings;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Start
{
    /// <summary>여는 쪽 흉내: 부른 것을 적어 두고 단계·사람 수는 테스트가 정한다.</summary>
    internal sealed class FakeRoomHost : IRoomHost
    {
        public readonly List<(string Address, int MaxPlayers, string Slot)> Begun = new List<(string, int, string)>();
        public readonly List<string[]> BegunShares = new List<string[]>();
        public readonly Dictionary<string, SavedRoom> Saved = new Dictionary<string, SavedRoom>();
        public IReadOnlyList<string> ShareablePieces { get; set; } = new[] { "wallet", "town" };
        public IReadOnlyList<string> DefaultSharedPieces { get; set; } = new string[0];
        public IReadOnlyList<string> SharedPieces { get; set; } = new string[0];
        public SavedRoom SavedRoomIn(string slotId) => slotId != null && Saved.TryGetValue(slotId, out var saved) ? saved : null;
        public int Cancelled, Closed;
        public RoomJoinPhase Phase { get; set; }
        public string FailureCode { get; set; } = string.Empty;
        public int DefaultMaxPlayers => 4;
        public int MaxPlayersLimit => 8;
        public bool IsOpen => Phase == RoomJoinPhase.Joined;
        public string ListenAddress { get; set; } = "192.168.0.10:7777";
        public int PlayerCount { get; set; } = 1;
        public int MaxPlayers { get; set; } = 4;

        public void Begin(string listenAddress, int maxPlayers, string slotId, IReadOnlyList<string> sharedPieces)
        {
            Begun.Add((listenAddress, maxPlayers, slotId));
            BegunShares.Add(sharedPieces.ToArray());
            Phase = RoomJoinPhase.Connecting;
        }

        public void Cancel()
        {
            Cancelled++;
            Phase = RoomJoinPhase.Idle;
        }

        public void Close()
        {
            Closed++;
            Phase = RoomJoinPhase.Idle;
        }
    }

    /// <summary>
    /// 내 PC에서 방 열기(시작 화면 단추 → 듣는 주소·정원·인물 모달)와 설정 화면의 방 줄. 창 없이 화면을 눌러 보는 자동 테스트다(화면 근거 아님).
    /// 방을 실제로 여는 것은 Host 테스트(MultiplayerHostModeHostTests)가 본다.
    /// </summary>
    public sealed class HostRoomScreenTests
    {
        private static HostRoomScreen OpenHost(StartMenuFixture t)
        {
            t.OpenSlots();
            t.Click(SaveSlotScreen.HostId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.HostRoom), Is.True);
            return t.Screen<HostRoomScreen>(ScreenCatalog.HostRoom);
        }

        private static string Status(StartMenuFixture t) => ((UiTextBlock)t.Node(HostRoomScreen.StatusId)).Text;

        private static void ClickTab(StartMenuFixture t, string id, int index)
        {
            UiRect r = ((UiTabBar)t.Node(id)).TabRect(index);
            t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
            t.Stack.Pointer(r.CenterX, r.CenterY, true, 0);
            t.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
        }

        [Test]
        public void HostButton_ShowsOnlyWhenThereIsAHost_BesideJoin()
        {
            using var t = new StartMenuFixture();
            t.OpenSlots();
            Assert.That(t.Node(SaveSlotScreen.HostId).Visible, Is.False, "여는 쪽이 없으면(테스트·견본) 단추가 없다");
            t.Stack.Close(ScreenCatalog.SaveSlotSelect);

            t.Menu.Host = new FakeRoomHost();
            t.Menu.Join = new FakeRoomJoin();
            t.OpenSlots();
            Assert.That(t.Node(SaveSlotScreen.HostId).IsActive, Is.True);
            Assert.That(((UiButton)t.Node(SaveSlotScreen.HostId)).Text, Is.EqualTo("방 열기"));
            UiRect host = t.Node(SaveSlotScreen.HostId).Rect, join = t.Node(SaveSlotScreen.JoinId).Rect, back = t.Node(SaveSlotScreen.BackId).Rect;
            UiRect hint = t.Node(SaveSlotScreen.HintId).Rect;
            Assert.That(host.Y, Is.EqualTo(join.Y), "들어가기와 같은 줄");
            Assert.That(back.Right <= join.X && join.Right <= host.X && host.Right <= hint.X, Is.True, "돌아가기 · 들어가기 · 방 열기 · 안내가 겹치지 않는다");
        }

        [Test]
        public void Modal_ListsOnlyStories_CapacityStartsAtTheDefault_OpenAsksWithAddressCapacityAndSlot()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            t.WriteStory("slot-2", "바다");
            var host = new FakeRoomHost();
            t.Menu.Host = host;
            HostRoomScreen screen = OpenHost(t);

            Assert.That(screen.SlotIds, Is.EqualTo(new[] { "slot-0", "slot-2" }), "이야기가 있는 슬롯만");
            Assert.That(t.Stack.Tree.Focused?.Id, Is.EqualTo(HostRoomScreen.AddressId), "주소 칸에 바로 친다");
            Assert.That(screen.MaxPlayers, Is.EqualTo(4), "정원은 방 설정의 기본값에서 시작한다");
            var capacity = (UiTabBar)t.Node(HostRoomScreen.CapacityId);
            Assert.That(capacity.Count, Is.EqualTo(8), "1~정원 상한");
            Assert.That(screen.StatusKey, Is.EqualTo("ui.host.saved-in-slot"));
            Assert.That(t.LabelText(HostRoomScreen.TitleId), Is.EqualTo("방 열기"));
            Assert.That(t.Node(HostRoomScreen.CapacityId).Rect.Y, Is.GreaterThan(t.Node(HostRoomScreen.AddressId).Rect.Y));
            Assert.That(t.Node(HostRoomScreen.SlotsId).Rect.Y, Is.GreaterThan(t.Node(HostRoomScreen.CapacityId).Rect.Y));

            ClickTab(t, HostRoomScreen.CapacityId, 5);
            Assert.That(screen.MaxPlayers, Is.EqualTo(6));
            ClickTab(t, HostRoomScreen.SlotsId, 1);
            Assert.That(screen.SelectedSlotId, Is.EqualTo("slot-2"));

            t.Click(HostRoomScreen.AddressId);
            t.Stack.TextInput(" 10.0.0.5:7777 ");
            t.Click(HostRoomScreen.OpenId);
            Assert.That(host.Begun, Is.EqualTo(new[] { ("10.0.0.5:7777", 6, "slot-2") }), "앞뒤 빈칸은 뗀다");
            Assert.That(screen.StatusKey, Is.EqualTo("ui.host.opening"));
            Assert.That(t.Node(HostRoomScreen.OpenId).IsActive, Is.False, "여는 동안 다시 누르지 못한다");

            host.Phase = RoomJoinPhase.Failed;
            host.FailureCode = RoomHostText.CannotListen;
            t.Stack.NotifyChanged(RoomHostText.Topic);
            Assert.That(screen.StatusKey, Is.EqualTo("ui.join.reason.cannot-listen"));
            Assert.That(Status(t), Is.EqualTo(t.Text.Get("ui.join.reason.cannot-listen")));
            Assert.That(t.Node(HostRoomScreen.OpenId).IsActive, Is.True, "실패하면 다시 시도할 수 있다");
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys.Concat(t.UsedKeys()));
        }

        private static UiToggle ShareToggle(StartMenuFixture t, string piece) => (UiToggle)t.Node(HostRoomScreen.ShareToggleId(piece));

        [Test]
        public void ShareRows_OnePerPiece_PlusTheFixedLine_OpenSendsWhatWasChosen()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            var host = new FakeRoomHost { DefaultSharedPieces = new[] { "town" } };
            t.Menu.Host = host;
            HostRoomScreen screen = OpenHost(t);

            Assert.That(ShareToggle(t, "wallet").IsOn, Is.False);
            Assert.That(ShareToggle(t, "town").IsOn, Is.True, "--share로 준 것이 처음 켜져 있다");
            Assert.That(t.LabelText(HostRoomScreen.ShareLabelId("wallet")), Is.EqualTo(t.Text.Get("ui.room.share.wallet")));
            string always = ((UiTextBlock)t.Node(HostRoomScreen.ShareAlwaysId)).Text;
            Assert.That(always, Is.EqualTo(t.Text.Get("ui.host.share-always")), "시간은 항상 같이, 가방·개인 성장은 항상 각자 — 고를 수 없는 줄");
            Assert.That(t.Node(HostRoomScreen.ShareAlwaysId).Rect.Y, Is.GreaterThan(t.Node(HostRoomScreen.ShareToggleId("wallet")).Rect.Y));
            Assert.That(t.Node(HostRoomScreen.ShareToggleId("wallet")).Rect.Y, Is.GreaterThan(t.Node(HostRoomScreen.SlotsId).Rect.Y), "인물 줄 아래");

            t.Click(HostRoomScreen.ShareToggleId("wallet"));
            t.Click(HostRoomScreen.ShareToggleId("town"));
            Assert.That(screen.ChosenSharedPieces, Is.EqualTo(new[] { "wallet" }));
            t.Click(HostRoomScreen.AddressId);
            t.Stack.TextInput("10.0.0.5:7777");
            t.Click(HostRoomScreen.OpenId);
            Assert.That(host.BegunShares.Single(), Is.EqualTo(new[] { "wallet" }));
            Assert.That(ShareToggle(t, "wallet").IsActive, Is.False, "여는 동안은 바꾸지 못한다");
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys.Concat(t.UsedKeys()));
        }

        [Test]
        public void ExistingRoom_ShowsItsSavedSettings_Locked_AndOpensWithThem()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            t.WriteStory("slot-1", "바다");
            var host = new FakeRoomHost { DefaultSharedPieces = new[] { "wallet" } };
            host.Saved["slot-1"] = new SavedRoom(new[] { "town" }, 3);
            t.Menu.Host = host;
            HostRoomScreen screen = OpenHost(t);
            Assert.That(ShareToggle(t, "wallet").IsActive, Is.True, "새 방이면 고른다");

            ClickTab(t, HostRoomScreen.SlotsId, 1);
            Assert.That(ShareToggle(t, "wallet").IsOn, Is.False);
            Assert.That(ShareToggle(t, "town").IsOn, Is.True, "저장된 설정을 보인다");
            Assert.That(ShareToggle(t, "town").IsActive || ShareToggle(t, "wallet").IsActive, Is.False, "이미 만든 방은 바꿀 수 없다(설계 10절 3번)");
            Assert.That(screen.MaxPlayers, Is.EqualTo(3), "정원도 방 설정");
            Assert.That(t.Node(HostRoomScreen.CapacityId).IsActive, Is.False);
            Assert.That(screen.StatusKey, Is.EqualTo("ui.host.saved-room"));
            t.Click(HostRoomScreen.AddressId);
            t.Stack.TextInput("10.0.0.5:7777");
            t.Click(HostRoomScreen.OpenId);
            Assert.That(host.Begun.Single(), Is.EqualTo(("10.0.0.5:7777", 3, "slot-1")));
            Assert.That(host.BegunShares.Single(), Is.EqualTo(new[] { "town" }));

            host.Phase = RoomJoinPhase.Idle;
            t.Stack.NotifyChanged(RoomHostText.Topic);
            ClickTab(t, HostRoomScreen.SlotsId, 0);
            Assert.That(ShareToggle(t, "wallet").IsActive, Is.True, "새 방 슬롯으로 돌아가면 다시 고른다");
            Assert.That(ShareToggle(t, "wallet").IsOn, Is.True, "그 슬롯에서 고른 값(처음 값)으로 돌아온다");
            Assert.That(t.Node(HostRoomScreen.CapacityId).IsActive, Is.True);
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys.Concat(t.UsedKeys()));
        }

        [Test]
        public void ExistingRoomWithoutACapacity_KeepsTheCapacityChoosable()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            var host = new FakeRoomHost();
            host.Saved["slot-0"] = new SavedRoom(new string[0], 0); // 방 설정 파일보다 먼저 만든 방
            t.Menu.Host = host;
            OpenHost(t);
            Assert.That(ShareToggle(t, "wallet").IsActive, Is.False);
            Assert.That(t.Node(HostRoomScreen.CapacityId).IsActive, Is.True, "정원을 적지 않은 방은 열 때 받은 정원을 쓴다");
        }

        /// <summary>등록된 모든 공유 조각의 이름 문구가 모든 언어에 있다(조각을 더하면 문구도 더한다).</summary>
        [Test]
        public void EveryRoomPiece_HasANameInEveryLanguage()
        {
            GameText text = TestData.Text();
            var keys = Rootborn.Core.Room.RoomPieceCatalog.CreateDefault().Names.Select(RoomShareText.PieceKey).ToList();
            keys.AddRange(new[] { "ui.room.shares", "ui.room.shares-nothing", "ui.host.share-always", "ui.host.share", "ui.host.saved-room" });
            KnownMissingKeys.AssertOnlyKnownMissing(text, keys);
            Assert.That(RoomShareText.Describe(text, new[] { "wallet" }), Does.Contain(text.Get("ui.room.share.wallet")));
            Assert.That(RoomShareText.Describe(text, new string[0]), Is.EqualTo(text.Get("ui.room.shares-nothing")));
        }

        [Test]
        public void NoStory_CannotOpen_AndSaysWhy()
        {
            using var t = new StartMenuFixture();
            t.Menu.Host = new FakeRoomHost();
            HostRoomScreen screen = OpenHost(t);
            Assert.That(screen.SlotIds, Is.Empty);
            Assert.That(screen.StatusKey, Is.EqualTo("ui.host.no-character"));
            Assert.That(t.Node(HostRoomScreen.OpenId).IsActive, Is.False);
            Assert.That(t.Node(HostRoomScreen.SlotsId).Visible, Is.False);
        }

        [Test]
        public void Cancel_WhileOpening_StopsAndCloses()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-1", "하늘");
            var host = new FakeRoomHost();
            t.Menu.Host = host;
            OpenHost(t);
            t.Click(HostRoomScreen.AddressId);
            t.Stack.TextInput("127.0.0.1:1"); // 화면 테스트의 글자일 뿐 듣지 않는다
            t.Click(HostRoomScreen.OpenId);
            t.Click(HostRoomScreen.CancelId);
            Assert.That(host.Cancelled, Is.EqualTo(1));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.HostRoom), Is.False);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
        }

        [Test]
        public void OpenedWithAnAddressAndSlot_FillsThemIn()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            t.WriteStory("slot-1", "바다");
            t.Menu.Host = new FakeRoomHost();
            t.OpenSlots();
            t.Stack.Open(ScreenCatalog.HostRoom, argument: new HostRoomOpen(t.Menu, "10.1.2.3:0", "slot-1"));
            var screen = t.Screen<HostRoomScreen>(ScreenCatalog.HostRoom);
            Assert.That(((UiTextField)t.Node(HostRoomScreen.AddressId)).Text, Is.EqualTo("10.1.2.3:0"));
            Assert.That(screen.SelectedSlotId, Is.EqualTo("slot-1"));
        }

        [Test]
        public void OpenedWithoutArgument_DoesNotThrow()
        {
            // Host의 등록 표 순회 테스트(영어 화면, 시험용 테마)는 인자 없이 연다.
            using var t = new StartMenuFixture();
            t.Stack.Open(ScreenCatalog.HostRoom);
            t.Emit();
            Assert.That(t.Node(HostRoomScreen.OpenId).IsActive, Is.False);
        }

        /// <summary>여는 쪽이 스스로 정하는 이유 코드의 문구가 모든 언어에 있다.</summary>
        [Test]
        public void HostReasons_HaveTextInEveryLanguage()
        {
            GameText text = TestData.Text();
            string[] codes = { RoomHostText.CannotListen, RoomHostText.RoomSaveUnreadable, RoomJoinText.BadAddress, RoomJoinText.EntryFailed };
            KnownMissingKeys.AssertOnlyKnownMissing(text, codes.Select(RoomJoinText.ReasonKey));
        }

        // ---------- 설정 화면의 방 줄 ----------

        private static (ScreenStack Stack, GameText Text) Settings(IRoomHost host, GameText text = null)
        {
            text ??= TestData.Text();
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(text, null) { RoomHost = host });
            stack.Layout(1920, 1080);
            stack.Open(ScreenCatalog.Settings);
            stack.Emit(new DrawList(), new TestMeasurer());
            stack.Layout(1920, 1080);
            return (stack, text);
        }

        private static UiNode Find(ScreenStack stack, string id) => stack.Tree.Root.Find(id);

        [Test]
        public void SettingsRoomLine_HiddenWithoutAnOpenRoom()
        {
            var (stack, _) = Settings(null);
            Assert.That(Find(stack, SettingsScreen.RoomLineId).IsVisibleInTree(), Is.False, "혼자 하기: 방 줄이 없고 설정 화면은 그대로");
            var (closed, _) = Settings(new FakeRoomHost { Phase = RoomJoinPhase.Idle });
            Assert.That(Find(closed, SettingsScreen.RoomLineId).IsVisibleInTree(), Is.False);
        }

        [Test]
        public void SettingsRoomLine_ShowsAddressAndPeople_UpdatesOnTheTopic_AndClosesTheRoom()
        {
            var host = new FakeRoomHost { Phase = RoomJoinPhase.Joined, ListenAddress = "192.168.0.10:7777", PlayerCount = 1, MaxPlayers = 4 };
            var (stack, text) = Settings(host);
            Assert.That(Find(stack, SettingsScreen.RoomLineId).IsVisibleInTree(), Is.True);
            var line = (UiTextBlock)Find(stack, SettingsScreen.RoomTextId);
            Assert.That(line.Text, Is.EqualTo(text.Format("ui.settings.room-open", "192.168.0.10:7777", 1, 4)));
            Assert.That(line.Text, Does.Contain("192.168.0.10:7777").And.Contain("1").And.Contain("4"));

            host.PlayerCount = 3;
            stack.NotifyChanged(RoomHostText.Topic);
            Assert.That(line.Text, Is.EqualTo(text.Format("ui.settings.room-open", "192.168.0.10:7777", 3, 4)), "들어온 사람 수가 바뀌면 고친다");

            host.SharedPieces = new[] { "wallet" };
            stack.NotifyChanged(RoomHostText.Topic);
            Assert.That(((UiLabel)Find(stack, SettingsScreen.RoomSharesId)).Text, Is.EqualTo(RoomShareText.Describe(text, new[] { "wallet" })), "같이 쓰는 것도 보인다");

            UiRect r = Find(stack, SettingsScreen.RoomCloseId).Rect;
            Assert.That(((UiButton)Find(stack, SettingsScreen.RoomCloseId)).Text, Is.EqualTo(text.Get("ui.settings.room-close")));
            stack.Pointer(r.CenterX, r.CenterY, false, 0);
            stack.Pointer(r.CenterX, r.CenterY, true, 0);
            stack.Pointer(r.CenterX, r.CenterY, false, 0);
            Assert.That(host.Closed, Is.EqualTo(1), "방 닫기");

            stack.NotifyChanged(RoomHostText.Topic);
            Assert.That(Find(stack, SettingsScreen.RoomLineId).IsVisibleInTree(), Is.False, "닫히면 줄이 숨는다");
            KnownMissingKeys.AssertOnlyKnownMissing(text, stack.Find(ScreenCatalog.Settings).TextKeys);
        }

        // ---------- 들어간 직후 알림 ----------

        [Test]
        public void JoinedNotice_SaysWhetherThisRoomStartsANewCharacterOrContinues()
        {
            GameText text = TestData.Text();
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(text, null));
            stack.Layout(1920, 1080);
            string Progress() => ((UiTextBlock)Find(stack, RoomSharesNoticeScreen.ProgressId)).Text;

            stack.Open(ScreenCatalog.RoomSharesNotice, argument: new RoomSharesNoticeOpen(new[] { "wallet" }, continuesHere: false));
            Assert.That(Progress(), Is.EqualTo(text.Get("ui.room.joined-progress.new")), "처음 온 방: 새 캐릭터로 시작한다(진행은 방에)");
            KnownMissingKeys.AssertOnlyKnownMissing(text, stack.Find(ScreenCatalog.RoomSharesNotice).TextKeys);
            stack.Close(ScreenCatalog.RoomSharesNotice);

            stack.Open(ScreenCatalog.RoomSharesNotice, argument: new RoomSharesNoticeOpen(new[] { "wallet" }, continuesHere: true));
            Assert.That(Progress(), Is.EqualTo(text.Get("ui.room.joined-progress.continue")), "방에 내 진행이 있다: 이어 한다");
            stack.Close(ScreenCatalog.RoomSharesNotice);

            stack.Open(ScreenCatalog.RoomSharesNotice, argument: new RoomSharesNoticeOpen(new[] { "wallet" }));
            Assert.That(Progress(), Is.Empty, "진행이 이 PC에 남는 방이면 알리지 않는다");
        }

        [Test]
        public void JoinedNotice_SaysWhatTheRoomShares_ThenClosesByItself_WithoutBlockingWalking()
        {
            GameText text = TestData.Text();
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(text, null));
            stack.Layout(1920, 1080);
            stack.Open(ScreenCatalog.RoomSharesNotice, argument: new RoomSharesNoticeOpen(new[] { "wallet", "town" }));
            Assert.That(stack.IsOpen(ScreenCatalog.RoomSharesNotice), Is.True);
            Assert.That(stack.MovementBlocked, Is.False, "걸으면서 본다");
            string shown = ((UiTextBlock)Find(stack, RoomSharesNoticeScreen.MessageId)).Text;
            Assert.That(shown, Is.EqualTo(text.Format("ui.room.joined-notice", RoomShareText.Describe(text, new[] { "wallet", "town" }))));
            Assert.That(shown, Does.Contain(text.Get("ui.room.share.wallet")).And.Contain(text.Get("ui.room.share.town")));
            stack.Tick(RoomSharesNoticeScreen.ShowSeconds - 0.5f);
            Assert.That(stack.IsOpen(ScreenCatalog.RoomSharesNotice), Is.True);
            stack.Tick(1f);
            Assert.That(stack.IsOpen(ScreenCatalog.RoomSharesNotice), Is.False, "몇 초 뒤 저절로 닫힌다");
            KnownMissingKeys.AssertOnlyKnownMissing(text, stack.Find(ScreenCatalog.RoomSharesNotice).TextKeys);

            stack.Open(ScreenCatalog.RoomSharesNotice);
            Assert.That(stack.IsOpen(ScreenCatalog.RoomSharesNotice), Is.False, "열 값이 없으면(등록 표 순회) 바로 닫는다");

            stack.Open(ScreenCatalog.RoomSharesNotice, argument: new RoomSharesNoticeOpen(new[] { "wallet" }));
            stack.Open(ScreenCatalog.Settings);
            stack.Tick(0.016f);
            Assert.That(stack.IsOpen(ScreenCatalog.RoomSharesNotice), Is.False, "패널이 열리면 닫는다(방 줄을 가리지 않게)");
        }

        /// <summary>
        /// 글자가 상자 테두리 위에 걸리지 않는다: 위 여백이 옆 여백보다 좁지 않다(위 여백 14가 도시형 몰딩 테두리 36 안에 들어 글자가 테두리 선 위에 그려졌다, 2026-11-12 훑기).
        /// </summary>
        [Test]
        public void JoinedNotice_TheMessage_SitsAsFarFromTheTopAsFromTheSides()
        {
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(TestData.Text(), null));
            stack.Layout(1920, 1080);
            stack.Open(ScreenCatalog.RoomSharesNotice, argument: new RoomSharesNoticeOpen(new[] { "wallet" }));
            UiRect box = Find(stack, RoomSharesNoticeScreen.BoxId).Rect, message = Find(stack, RoomSharesNoticeScreen.MessageId).Rect;
            Assert.That(message.Y - box.Y, Is.GreaterThanOrEqualTo(message.X - box.X));
        }

        [TestCaseSource(nameof(Languages))]
        public void JoinedNotice_DoubleLengthText_StaysInsideTheBox(string language)
        {
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(LongText.Create(language), null));
            // 같이 쓰는 것 전부 + 진행 줄(두 문구 중 긴 쪽 — 새 캐릭터로 시작)
            stack.Open(ScreenCatalog.RoomSharesNotice, argument: new RoomSharesNoticeOpen(Rootborn.Core.Room.RoomPieceCatalog.CreateDefault().Names, continuesHere: false));
            TextBounds.Settle(stack);
            var found = new List<string>();
            TextBounds.Inspect(stack.Find(ScreenCatalog.RoomSharesNotice), found);
            Assert.That(found, Is.Empty);
        }

        // ---------- 긴 문구 ----------

        [TestCaseSource(nameof(Languages))]
        public void DoubleLengthText_StaysInsideTheBoxes(string language)
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            t.WriteStory("slot-1", "바다");
            t.WriteStory("slot-2", "구름");
            var host = new FakeRoomHost { Phase = RoomJoinPhase.Failed, FailureCode = RoomHostText.RoomSaveUnreadable };
            t.Menu.Host = host;
            t.Menu.Join = new FakeRoomJoin();
            GameText text = LongText.Create(language);
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(text, null));
            stack.Open(ScreenCatalog.SaveSlotSelect, argument: t.Menu);
            stack.Open(ScreenCatalog.HostRoom, argument: new HostRoomOpen(t.Menu, "192.168.100.200:65535"));
            TextBounds.Settle(stack);
            var found = new List<string>();
            TextBounds.Inspect(stack.Find(ScreenCatalog.HostRoom), found);
            stack.Close(ScreenCatalog.HostRoom);
            TextBounds.Settle(stack);
            TextBounds.Inspect(stack.Find(ScreenCatalog.SaveSlotSelect), found, SaveSlotScreen.HostId, SaveSlotScreen.JoinId, SaveSlotScreen.BackId, SaveSlotScreen.HintId);

            var open = new FakeRoomHost { Phase = RoomJoinPhase.Joined, ListenAddress = "192.168.100.200:65535", PlayerCount = 8, MaxPlayers = 8, SharedPieces = new[] { "wallet", "town" } };
            var settings = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(text, null) { RoomHost = open });
            settings.Open(ScreenCatalog.Settings);
            TextBounds.Settle(settings);
            TextBounds.Inspect(settings.Find(ScreenCatalog.Settings), found, SettingsScreen.RoomTextId, SettingsScreen.RoomSharesId, SettingsScreen.RoomCloseId);
            Assert.That(found, Is.Empty);
        }

        private static IEnumerable<string> Languages() => LongText.MergedTables().Keys.OrderBy(k => k);
    }

    /// <summary>글자가 가장 가까운 패널(또는 단추·칸 자신) 밖으로 나가는지, 단추끼리 겹치는지(들어가기 창 테스트와 같은 셈).</summary>
    internal static class TextBounds
    {
        public static void Settle(ScreenStack stack)
        {
            for (int i = 0; i < 2; i++)
            {
                stack.Layout(1920, 1080);
                stack.Emit(new DrawList(), new WideMeasurer());
            }
            stack.Layout(1920, 1080);
        }

        /// <param name="onlyIds">주면 그 요소만 본다(남이 맡은 화면의 다른 요소는 보지 않는다).</param>
        public static void Inspect(GameScreen screen, List<string> found, params string[] onlyIds)
        {
            var measurer = new WideMeasurer();
            var buttons = new List<UiNode>();
            foreach (UiNode node in Walk(screen.Root))
            {
                if (!node.IsVisibleInTree() || (onlyIds.Length > 0 && !onlyIds.Contains(node.Id))) continue;
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
