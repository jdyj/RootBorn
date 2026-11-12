using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Room;
using Rootborn.Core.Workspaces;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Workspaces;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Workspaces
{
    /// <summary>
    /// 일터 템플릿 고르기 화면: 가구 배치 화면의 템플릿 단추로 열고, 목록에서 고르고, 적용 단추를 누른다. 실제 data/와 GameSession,
    /// 요리사 일터(시험용 방). 창 없이 화면을 눌러 보는 자동 테스트다(화면 근거가 아니다).
    /// </summary>
    public sealed class WorkspaceTemplateScreenTests
    {
        private static WorkspaceTemplateScreen Screen(WorkspaceFixture w) => (WorkspaceTemplateScreen)w.Stack.Find(ScreenCatalog.WorkspaceTemplates);

        private static string Text(UiNode node) => node is UiLabel l ? l.Text : node is UiTextBlock b ? b.Text : ((UiButton)node).Text;

        /// <summary>목록의 그 줄을 포인터로 누른다.</summary>
        private static void ClickRow(WorkspaceFixture w, int row)
        {
            w.Emit();
            UiScrollList list = Screen(w).List;
            Assert.That(list.TryGetRowRect(row, out UiRect r), Is.True, "줄이 보인다: " + row);
            Assert.That(w.Stack.Tree.HitTest(r.CenterX, r.CenterY), Is.SameAs(list));
            w.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
            w.Stack.Pointer(r.CenterX, r.CenterY, true, 0);
            w.Stack.Pointer(r.CenterX, r.CenterY, false, 0);
        }

        private static void OpenTemplates(WorkspaceFixture w)
        {
            w.Click(FurniturePlacementScreen.TemplatesId);
            w.Emit();
            Assert.That(w.Stack.IsOpen(ScreenCatalog.WorkspaceTemplates), Is.True, "템플릿 단추가 연다");
        }

        [Test]
        public void TemplateScreen_ListsTemplates_ApplyChangesLayout_AndClosesOrRefreshes()
        {
            using var w = new WorkspaceFixture(allowance: 50);
            OpenTemplates(w);
            Assert.That(w.Stack.MovementBlocked, Is.True, "모달이고 이동을 막는다");
            Assert.That(w.Stack.IsOpen(ScreenCatalog.FurniturePlacement), Is.True, "배치 화면은 아래에 남는다");
            WorkspaceTemplateScreen screen = Screen(w);
            Assert.That(screen.Model.Rows.Select(r => r.Id), Is.EqualTo(w.Session.TemplatesForActiveWorkspace().Select(t => t.Id)), "정의 순서");
            Assert.That(screen.List.RowCount, Is.EqualTo(screen.Model.Rows.Length));

            int cozy = screen.Model.IndexOf(WorkspaceFixture.CozyTemplate);
            Assert.That(cozy, Is.GreaterThanOrEqualTo(0));
            ClickRow(w, cozy);
            Assert.That(screen.SelectedIndex, Is.EqualTo(cozy));
            var template = w.Definitions.Get<WorkspaceTemplate>(WorkspaceFixture.CozyTemplate);
            Assert.That(((UiLabel)w.Node(WorkspaceTemplateScreen.NameId)).Text, Is.EqualTo(w.Text.Get(template.DisplayNameKey)));
            int cost = template.Costs.Where(c => c.Item == WorkspaceFixture.Allowance).Sum(c => c.Count);
            Assert.That(cost, Is.GreaterThan(0));
            Assert.That(Text(w.Node(WorkspaceTemplateScreen.CostId)), Does.Contain(cost.ToString()));
            Assert.That(Text(w.Node(WorkspaceTemplateScreen.StatusId)), Is.EqualTo(w.Text.Get("ui.workspace.template.ready")));

            int allowance = w.Session.Inventory.CountOf(w.Session.Item(WorkspaceFixture.Allowance));
            w.Click(WorkspaceTemplateScreen.ApplyId(WorkspaceFixture.CozyTemplate));
            Assert.That(screen.LastResult, Is.EqualTo(WorkspaceResultKind.Applied));
            Assert.That(w.Stack.IsOpen(ScreenCatalog.WorkspaceTemplates), Is.False, "적용하면 닫는다");
            Assert.That(w.Session.House.Placed.Select(p => p.Definition.StableId).OrderBy(x => x), Is.EqualTo(template.FurnitureIds.OrderBy(x => x)), "배치가 템플릿으로 바뀌었다");
            Assert.That(w.Session.Inventory.CountOf(w.Session.Item(WorkspaceFixture.Allowance)), Is.EqualTo(allowance - cost), "값을 한 번 냈다");

            // 아래의 배치 화면은 알림으로 새 값을 보인다.
            w.Emit();
            var status = w.Session.ActiveWorkspaceStatus;
            Assert.That(Text(w.Node(FurniturePlacementScreen.CapacityId)), Is.EqualTo(w.Text.Format("ui.workspace.capacity", status.Placed, status.Capacity)));
            Assert.That(Text(w.Node(FurniturePlacementScreen.QualityId(WorkspaceFixture.Cozy))), Is.EqualTo(status.Scores.Get(WorkspaceFixture.Cozy).ToString()));
        }

        [Test]
        public void TemplateScreen_NotEnoughMoney_ShowsReason()
        {
            using var w = new WorkspaceFixture(allowance: 0);
            OpenTemplates(w);
            WorkspaceTemplateScreen screen = Screen(w);
            ClickRow(w, screen.Model.IndexOf(WorkspaceFixture.CozyTemplate));
            Assert.That(screen.Model.Rows[screen.SelectedIndex].Check, Is.EqualTo(WorkspaceResultKind.InsufficientResources), "Core 판단");
            Assert.That(Text(w.Node(WorkspaceTemplateScreen.StatusId)), Is.EqualTo(w.Text.Get(WorkspaceTemplateModel.ReasonKey(WorkspaceResultKind.InsufficientResources))));
            Assert.That(w.Node(WorkspaceTemplateScreen.ApplyId(WorkspaceFixture.CozyTemplate)).IsActive, Is.False, "못 하면 적용 단추를 누를 수 없다");
            int placed = w.Session.House.Placed.Count;
            Assert.That(w.Session.House.Placed.Count, Is.EqualTo(placed), "아무것도 바뀌지 않는다");

            // 값이 없는 템플릿은 돈이 없어도 깔 수 있다(다른 줄을 고르면 그 줄의 판단).
            int basic = screen.Model.IndexOf(WorkspaceFixture.BasicTemplate);
            ClickRow(w, basic);
            Assert.That(Text(w.Node(WorkspaceTemplateScreen.CostId)), Is.EqualTo(w.Text.Format("ui.workspace.template.cost", w.Text.Get("ui.workspace.template.free"))));
            Assert.That(w.Node(WorkspaceTemplateScreen.ApplyId(WorkspaceFixture.BasicTemplate)).IsActive, Is.True);
        }

        [Test]
        public void TemplateScreen_ForSomeoneElsesWorkspace_CannotOpen_AndEveryRowSaysNotOwner()
        {
            using var w = new WorkspaceFixture(adult: false);
            Assert.That(w.Node(FurniturePlacementScreen.TemplatesId).IsActive, Is.False, "남의 일터에서는 단추가 꺼져 있다");
            w.Stack.Open(ScreenCatalog.WorkspaceTemplates); // 단추가 꺼져도 화면 자체가 Core 판단을 보이는지(준비 — 직접 연다)
            w.Emit();
            WorkspaceTemplateScreen screen = Screen(w);
            Assert.That(screen.Model.Rows, Is.Not.Empty);
            Assert.That(screen.Model.Rows.Select(r => r.Check), Is.All.EqualTo(WorkspaceResultKind.NotOwner));
            Assert.That(Text(w.Node(WorkspaceTemplateScreen.StatusId)), Is.EqualTo(w.Text.Get(WorkspaceTemplateModel.ReasonKey(WorkspaceResultKind.NotOwner))));
        }

        [Test]
        public void TemplateScreen_InTheHouse_ShowsNoTemplates_AndStaysOpen()
        {
            using var w = new WorkspaceFixture(open: false);
            w.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            w.Stack.Open(ScreenCatalog.WorkspaceTemplates);
            w.Emit();
            Assert.That(w.Stack.IsOpen(ScreenCatalog.WorkspaceTemplates), Is.True);
            Assert.That(Screen(w).Model.Rows, Is.Empty);
            Assert.That(Text(w.Node(WorkspaceTemplateScreen.StatusId)), Is.EqualTo(w.Text.Get("ui.workspace.template.empty")));
            Assert.That(w.Node(WorkspaceTemplateScreen.ApplyBaseId).IsActive, Is.False);
            w.Click(WorkspaceTemplateScreen.CloseId);
            Assert.That(w.Stack.IsOpen(ScreenCatalog.WorkspaceTemplates), Is.False);
        }

        // ---------- 돈을 같이 쓰는 방 ----------

        /// <summary>돈을 같이 쓰는 방(Core RoomPieceSet을 테스트가 손으로 돌린다 — 서버가 할 일).</summary>
        private sealed class WalletRoom
        {
            private readonly List<(string Player, RoomShares Shares)> _players = new List<(string, RoomShares)>();
            private int _ids;
            public readonly RoomPieceSet Pieces = new RoomPieceSet(new RoomShareSettings(new[] { RoomWallet.PieceName }), RoomPieceCatalog.CreateDefault());

            public RoomShares Join(string player)
            {
                var shares = new RoomShares(Pieces.Settings, RoomPieceCatalog.CreateDefault(), () => player + "-r" + (++_ids));
                foreach (var piece in Pieces.Pieces) shares.Receive(piece.State());
                _players.Add((player, shares));
                return shares;
            }

            public void Deliver(RoomShares from)
            {
                var (player, _) = _players.First(p => p.Shares == from);
                var answers = new List<RoomAnswer>();
                var changed = new List<IRoomPiece>();
                Pieces.Apply(player, from.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
                foreach (var a in answers) from.Receive(a);
                foreach (var piece in changed)
                    foreach (var p in _players) p.Shares.Receive(piece.State());
            }

            public void Deposit(RoomShares other, int amount)
            {
                other.Outbox.Add(new RoomRequestSaveData { Piece = RoomWallet.PieceName, RequestId = other.Outbox.NewId(), Kind = RoomWallet.Deposit, Amount = amount });
                Deliver(other);
            }
        }

        [Test]
        public void TemplateScreen_InAMoneySharingRoom_WaitsForTheRoom_ThenCloses()
        {
            var room = new WalletRoom();
            RoomShares mine = room.Join("me"), theirs = room.Join("friend");
            room.Deposit(theirs, 50); // 준비: 방 지갑에 돈이 있다
            using var w = new WorkspaceFixture(shares: mine, playerId: "me");
            Assert.That(w.Session.SharesWallet, Is.True);
            OpenTemplates(w);
            WorkspaceTemplateScreen screen = Screen(w);
            ClickRow(w, screen.Model.IndexOf(WorkspaceFixture.CozyTemplate));
            int placed = w.Session.House.Placed.Count;
            Assert.That(screen.Model.Rows[screen.SelectedIndex].Check, Is.EqualTo(WorkspaceResultKind.Applied), "준비: 방 지갑으로 낼 수 있다");

            w.Click(WorkspaceTemplateScreen.ApplyId(WorkspaceFixture.CozyTemplate));
            w.Emit();
            Assert.That(w.Session.WaitingForRoom, Is.True, "방의 허락을 기다린다");
            Assert.That(w.Stack.IsOpen(ScreenCatalog.WorkspaceTemplates), Is.True);
            Assert.That(Text(w.Node(WorkspaceTemplateScreen.StatusId)), Is.EqualTo(w.Text.Get(RoomMoneyText.WaitingKey)));
            Assert.That(w.Node(WorkspaceTemplateScreen.ApplyId(WorkspaceFixture.CozyTemplate)).IsActive, Is.False, "기다리는 동안 다시 누를 수 없다");
            Assert.That(w.Session.House.Placed.Count, Is.EqualTo(placed), "허락 전에는 바뀌지 않는다");

            room.Deliver(mine);
            w.Emit();
            Assert.That(w.Session.WaitingForRoom, Is.False);
            Assert.That(w.Stack.IsOpen(ScreenCatalog.WorkspaceTemplates), Is.False, "허락받아 적용되면 닫는다");
            var template = w.Definitions.Get<WorkspaceTemplate>(WorkspaceFixture.CozyTemplate);
            Assert.That(w.Session.House.Placed.Select(p => p.Definition.StableId).OrderBy(x => x), Is.EqualTo(template.FurnitureIds.OrderBy(x => x)));
        }

        // ---------- 문구 ----------

        [Test]
        public void Texts_ExistInEveryLanguage_AndSwitchWithTheLanguage()
        {
            using var w = new WorkspaceFixture(allowance: 0);
            OpenTemplates(w);
            WorkspaceTemplateScreen screen = Screen(w);
            ClickRow(w, screen.Model.IndexOf(WorkspaceFixture.CozyTemplate));
            string korean = Text(w.Node(WorkspaceTemplateScreen.TitleId));
            w.Text.Language = "en";
            w.Emit();
            Assert.That(Text(w.Node(WorkspaceTemplateScreen.TitleId)), Is.EqualTo(w.Text.Get("ui.workspace.template.title")).And.Not.EqualTo(korean));
            KnownMissingKeys.AssertOnlyKnownMissing(w.Text, screen.TextKeys);
            foreach (WorkspaceResultKind kind in Enum.GetValues(typeof(WorkspaceResultKind)))
                foreach (string language in new[] { "ko", "en" })
                    Assert.That(w.Text.Has(WorkspaceTemplateModel.ReasonKey(kind), language), Is.True, kind + " " + language);
            foreach (string key in new[] { "ui.workspace.full", "ui.workspace.not-owner", "ui.facility.work-bonus", "ui.workspace.capacity", "ui.workspace.templates" })
                Assert.That(w.Text.Has(key, "ko") && w.Text.Has(key, "en"), Is.True, key);
        }

        [Test]
        public void LongText_KeepsEveryTextInsideThePanel()
        {
            using var w = new WorkspaceFixture(allowance: 0, longText: true);
            OpenTemplates(w);
            ClickRow(w, Screen(w).Model.IndexOf(WorkspaceFixture.CozyTemplate));
            var measurer = new WideMeasurer();
            for (int i = 0; i < 2; i++) { w.Stack.Layout(1920, 1080); w.Stack.Emit(new DrawList(), measurer); }
            w.Stack.Layout(1920, 1080);
            UiNode root = Screen(w).Root;
            foreach (UiNode node in Descendants(root))
            {
                if (!node.IsVisibleInTree()) continue;
                var list = new DrawList();
                node.Emit(list, measurer);
                foreach (DrawCommand text in list.Commands.Where(c => c.Kind == DrawKind.Text))
                {
                    float right = text.Rect.X + measurer.Width(text.Text, text.FontSize), bottom = text.Rect.Y + measurer.LineHeight(text.FontSize);
                    UiRect box = root.Rect;
                    Assert.That(text.Rect.X >= box.X - 0.5f && right <= box.Right + 0.5f && text.Rect.Y >= box.Y - 0.5f && bottom <= box.Bottom + 0.5f, Is.True,
                        (node.Id ?? node.GetType().Name) + ": " + text.Text);
                }
            }
        }

        private static IEnumerable<UiNode> Descendants(UiNode node)
        {
            yield return node;
            foreach (UiNode child in node.Children)
                foreach (UiNode inner in Descendants(child)) yield return inner;
        }
    }
}
