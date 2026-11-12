using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 같이 하기를 두 기계로: 이 PC(Windows)의 게임과 원격 Linux 장치(Strix Halo)의 데디케이티드 서버·게임이 집 안 네트워크(LAN)의 UDP로 만난다.
    /// 근거: 두 기계(Windows ↔ Linux), 실제 LAN, 장치 이벤트 주입(OS 입력 계층 미확인). 원격 게임은 원격 가상 화면(Xvfb, 소프트웨어 GL)에서 돌고
    /// 동사는 ssh 표준 입출력으로 간다(<see cref="RemoteMachine"/>). 사람이 직접 한 플레이와 인터넷을 거치는 접속은 미확인.
    /// 두 프로세스는 동사 줄로 번갈아 한 프레임씩 돌지만 네트워크는 실제 시간으로 흐르므로 같은 프레임을 가정하지 않는다 — 모든 확인은
    /// "조건이 될 때까지 기다린다(넉넉한 프레임 한도)"다.
    /// [Explicit]: 원격 장치·LAN이 필요해 평소 실행(이 PC·원격 전체 실행·CI)에는 들어가지 않는다. 돌리는 길은 <c>Scripts/remote-test/two-machines.sh</c>
    /// (원격 준비·환경 변수·뒷정리). 환경 변수가 없으면 건너뛴다(<see cref="RemoteMachine.RequireOrIgnore"/>).
    /// 주소: 원격 서버는 원격 장치의 LAN 주소 하나(ssh가 닿은 주소)에, 이 PC의 방은 이 PC의 LAN 주소 하나(원격이 본 ssh 출발 주소)에 묶는다. 포트는 0(운영체제가 준 빈 포트).
    /// 주소를 코드에 적지 않는다.
    /// </summary>
    public sealed partial class TwoGamesHostTests
    {
        private const string TwoMachines = "TwoMachines";
        private const string NeedsTwoMachines = "원격 Linux 장치와 LAN이 필요하다 — Scripts/remote-test/two-machines.sh로 돌린다";
        /// <summary>네트워크를 기다리는 확인의 프레임 한도(원격 게임과 번갈아 돌면 한 프레임이 실제 수십 ms라 넉넉히).</summary>
        private const int NetFrames = 3000;

        private RemoteMachine _remote;
        private string _remoteSlot;
        private readonly List<RemoteServer> _remoteServers = new List<RemoteServer>();

        /// <summary>원격 장치를 잡는다(없으면 건너뜀). 서버 슬롯은 테스트마다 새것이다(같은 실행 폴더의 세이브를 다른 테스트가 이어 열지 않게).</summary>
        private void BeginRemote()
        {
            _remote = RemoteMachine.RequireOrIgnore();
            _remoteSlot = "room-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private RemoteServer StartRemoteServer(string share, string folder = "server")
        {
            var s = _remote.StartServer(share, _remoteSlot, folder);
            _remoteServers.Add(s);
            return s;
        }

        private void StopRemoteServers()
        {
            foreach (RemoteServer s in _remoteServers)
            {
                try { s.Dispose(); } catch (Exception e) { TestContext.WriteLine("원격 서버 끄기: " + e.Message); }
            }
            _remoteServers.Clear();
        }

        /// <summary>원격 게임: 이야기가 있는 슬롯을 원격 세이브 폴더에 올리고(준비) 원격 가상 화면에서 띄운다.</summary>
        private PeerGame StartRemoteGuest()
        {
            _remote.Upload(_guestSaves, "guest-saves");
            var guest = _remote.StartPeer("원격 게임");
            guest.Partner = _host;
            guest.Launch("--save-root", _remote.RemotePath("guest-saves"), "--size", "1280x720", "--evidence", _remote.RemotePath("evidence"));
            return guest;
        }

        private static string Shot(PlayerDriver p, string name) => p.Screenshot("two-machines-" + name);

        // ---------- 1. 원격 서버 + 이 PC의 게임 ----------

        /// <summary>
        /// Strix Halo의 데디케이티드 서버(돈·집 공유, LAN 주소에 묶음)에 이 PC의 게임이 시작 화면의 서버 주소로 들어가기로 들어가 걷고, 알바로 돈을 벌고
        /// (방 지갑이 HUD에), 상자를 놓고 나무를 넣고, 가구를 놓고, 잔다(혼자라 바로 다음 날). 서버 세이브의 guests/에 이 사람의 진행이 남는다.
        /// 게임을 끄고 다시 들어오면 이어지고, 서버를 껐다 켠 뒤 다시 들어와도 이어진다. 준비: 이야기가 있는 슬롯 파일.
        /// </summary>
        [Test, Explicit(NeedsTwoMachines), Category(TwoMachines)]
        public void TWO_MACHINES_001_ServerOnLinux_GameOnThisPc_Walk_Work_Chest_Furniture_Sleep_Rejoin()
        {
            BeginRemote();
            try
            {
                RemoteServer server = StartRemoteServer(RoomWallet.PieceName + "," + RoomHouse.PieceName);
                string address = server.WaitForAddress(TimeSpan.FromSeconds(90));
                Assert.That(IPAddress.IsLoopback(IPAddress.Parse(address.Substring(0, address.LastIndexOf(':')))), Is.False, "서버가 LAN 주소에 묶였다: " + address);
                TestContext.WriteLine("[원격 서버] " + address);

                WriteStory(_hostSaves, "slot-1", "이쪽", 101);
                _host = LaunchHost();
                JoinByAddress(_host, address);
                _host.DismissPopups(); // 들어갈 때 뜬 하루 알림을 닫는다(장면을 가리지 않게)
                var session = _host.Game.Session;
                string me = _host.Game.Room.PlayerId;
                Assert.That(Until(_host, () => HostHud.EndsWith(RoomBalance(0)), NetFrames), Is.True, "방 지갑 0이 HUD에: " + HostHud);
                TestContext.WriteLine("스크린샷: " + Shot(_host, "01-joined"));

                // 걷기
                float x0 = _host.World.Walker.X, y0 = _host.World.Walker.Y;
                _host.WalkAway(4);
                Assert.That(Math.Abs(_host.World.Walker.X - x0) + Math.Abs(_host.World.Walker.Y - y0), Is.GreaterThan(_host.World.Map.Grid.CellSize * 2f), "걸었다");

                // 알바로 돈 → 방 지갑
                _host.GoTo("Town");
                Assert.That(_host.WorkShift(ShopWork), Is.True, "알바 미니게임을 끝까지");
                Assert.That(Until(_host, () => session.Wallet.Balance == 5 && HostHud.EndsWith(RoomBalance(5)), NetFrames), Is.True, "서버 방 지갑 5가 HUD에: " + HostHud);
                Assert.That(session.Inventory.CountOf(session.Item("Wood")), Is.EqualTo(3), "알바 보상 나무 3");
                TestContext.WriteLine("스크린샷: " + Shot(_host, "01-after-shift"));

                // 상자 놓고 넣기
                _host.GoTo("House");
                _host.PlaceFurniture(CrateId);
                Assert.That(Until(_host, () => !session.WaitingForRoom && session.House.Placed.Any(f => f.Definition.StableId == CrateId), NetFrames), Is.True,
                    "서버가 상자를 허락했다\n" + server.Output);
                string chest = session.House.Placed.Single(f => f.Definition.StableId == CrateId).InstanceId;
                _host.OpenChest(chest);
                _host.MoveStack("Wood", put: true);
                Assert.That(Until(_host, () => !session.WaitingForRoom && ChestWood(chest) == 3, NetFrames), Is.True, "상자에 나무 3");
                Assert.That(session.Inventory.CountOf(session.Item("Wood")), Is.EqualTo(0));
                TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.Storage, "two-machines-01-chest"));
                _host.CloseChest();

                // 가구 놓기(상자가 아닌 한 칸짜리)
                var storage = new HashSet<string>(session.Definitions.All<FurnitureStorageDefinition>().Select(s => s.Furniture.StableId));
                string furniture = session.AvailableFurniture().First(f => f.BlocksMovement && f.FootprintCells.Count == 1 && !storage.Contains(f.StableId)).StableId;
                int placedBefore = session.House.Placed.Count;
                _host.PlaceFurniture(furniture);
                Assert.That(Until(_host, () => !session.WaitingForRoom && session.House.Placed.Count == placedBefore + 1, NetFrames), Is.True, "서버가 가구를 허락했다");
                TestContext.WriteLine("스크린샷: " + Shot(_host, "01-furniture"));

                // 잠: 방에 혼자 → 다음 날
                DayResultScreen result = _host.GoToBed();
                var next = (UiButton)_host.Node(DayResultScreen.NextDayId);
                // 방에 혼자면 자는 순간 모두 잔 것이라 방이 날을 넘긴다(자는 중 표시는 곧 꺼진다) — 다음 날 단추가 켜지기를 기다린다.
                Assert.That(Until(_host, () => next.Enabled && session.Room.Known && session.Room.Day == 2, NetFrames), Is.True,
                    "혼자면 자면 방이 다음 날로 넘어가고 다음 날 단추가 켜진다: 단추 " + next.Enabled + ", 방의 날 " + session.Room.Day + "\n" + server.Output);
                TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.DayResult, "two-machines-01-sleep"));
                _host.PressButton(DayResultScreen.NextDayId);
                Assert.That(Until(_host, () => session.Student.CurrentDay == 2, NetFrames), Is.True, "2일째");
                int placedAfter = session.House.Placed.Count;

                // 서버 세이브에 손님 진행: guests/<이 사람>/ 아래 파일이 생겼다(서버가 받았다고 답할 때까지 기다린다).
                IReadOnlyList<string> files = null;
                Assert.That(Until(_host, () => (files = _remote.ServerSaveFiles()).Any(f => f.Contains("guests/")), 600), Is.True,
                    "서버의 guests/에 손님 진행: " + string.Join(" | ", files ?? Array.Empty<string>()));
                TestContext.WriteLine("[서버 세이브]\n  " + string.Join("\n  ", files));

                // 게임을 끄고 다시 들어온다 → 이어진다.
                _host.Dispose();
                _host = null;
                _host = LaunchHost();
                JoinByAddress(_host, address);
                AssertContinues("다시 들어옴", me, chest, placedAfter);
                TestContext.WriteLine("스크린샷: " + Shot(_host, "01-rejoined"));

                // 서버를 껐다 켠다(같은 세이브) → 다시 들어오면 이어진다.
                _host.Dispose();
                _host = null;
                Assert.That(server.Stop(TimeSpan.FromSeconds(60)), Is.EqualTo(0), "서버가 저장하고 끝남\n" + server.Output);
                Assert.That(server.Has("저장하고 닫음"), Is.True);
                RemoteServer again = StartRemoteServer(RoomWallet.PieceName + "," + RoomHouse.PieceName);
                string address2 = again.WaitForAddress(TimeSpan.FromSeconds(90));
                Assert.That(again.Has("방을 이어서 엶"), Is.True, "같은 세이브로 이어서 연다\n" + again.Output);
                _host = LaunchHost();
                JoinByAddress(_host, address2);
                AssertContinues("서버를 다시 켠 뒤", me, chest, placedAfter);
                TestContext.WriteLine("스크린샷: " + Shot(_host, "01-after-server-restart"));
            }
            finally
            {
                _host?.Dispose();
                _host = null;
                StopRemoteServers();
            }
        }

        private void AssertContinues(string when, string me, string chest, int placed)
        {
            var s = _host.Game.Session;
            Assert.That(_host.Game.Room.PlayerId, Is.EqualTo(me), when + ": 같은 인물");
            Assert.That(Until(_host, () => s.Student.CurrentDay == 2 && s.Wallet.Balance == 5 && s.House.Placed.Count == placed && ChestWood(chest) == 3, NetFrames), Is.True,
                when + ": 날 " + s.Student.CurrentDay + ", 지갑 " + s.Wallet.Balance + ", 집 가구 " + s.House.Placed.Count + "(기대 " + placed + "), 상자 나무 " + ChestWood(chest));
            Assert.That(s.Inventory.CountOf(s.Item("Wood")), Is.EqualTo(0), when + ": 가방(나무는 상자에 넣었다)");
            Assert.That(HostHud.EndsWith(RoomBalance(5)), Is.True, when + ": HUD 방 지갑 " + HostHud);
        }

        // ---------- 2. 원격 서버 + 게임 둘(이 PC, 원격) ----------

        /// <summary>
        /// Strix Halo의 서버(돈·집 공유)에 이 PC의 게임과 Strix Halo 가상 화면의 게임이 들어간다: 서로 보이고 걷는 것이 따라온다(양쪽), 원격 게임이 번 돈이
        /// 이 PC HUD에, 이 PC가 상자에 넣은 나무를 원격 게임이 꺼낸다, 1분 동안 재기(왕복 지연, 남의 몸이 튀는 프레임, 두 기계의 방 시각 차이, 보낸 바이트),
        /// 둘 다 자면 다음 날. 준비: 이야기가 있는 슬롯 파일(양쪽 — 원격 쪽은 올린다).
        /// </summary>
        [Test, Explicit(NeedsTwoMachines), Category(TwoMachines)]
        public void TWO_MACHINES_002_ServerOnLinux_ThisPcAndLinuxGames_SeeEachOther_Money_Chest_Measure_Sleep()
        {
            BeginRemote();
            try
            {
                RemoteServer server = StartRemoteServer(RoomWallet.PieceName + "," + RoomHouse.PieceName);
                string address = server.WaitForAddress(TimeSpan.FromSeconds(90));
                WriteStory(_hostSaves, "slot-1", "이쪽", 101);
                WriteStory(_guestSaves, "slot-1", "저쪽", 202);
                _host = LaunchHost();
                JoinByAddress(_host, address);
                _host.DismissPopups(); // 들어갈 때 뜬 하루 알림을 닫는다(장면을 가리지 않게)
                _guest = StartRemoteGuest();
                GuestJoinsByAddress(address, "two-machines-02-remote-join-modal");
                _guest.Call("dismiss-popups");
                string one = _host.Game.Room.PlayerId, two = GuestId;
                Assert.That(two, Is.Not.EqualTo(one));
                Assert.That(_guest.Until(() => _host.Game.Remotes.Visible.Count == 1 && _guest.Query("remotes") == one, NetFrames), Is.True, "서로 보인다\n" + server.Output);
                Assert.That(_guest.Until(() => _host.Game.World.RemotesDrawn == 1 && _guest.QueryInt("remotes-drawn") == 1, NetFrames), Is.True, "서로의 화면에 그려진다");
                var mineLook = _host.Game.World.PlayerLook;
                var theirs = _host.Game.Remotes.Visible[0];
                TestContext.WriteLine("[겉모습] 이 PC가 보내는 코드 '" + mineLook.Code + "'(몸 " + mineLook.Body?.Id + ", 옷 " + string.Join(",", mineLook.Layers.Select(l => l.Id))
                    + "), 이 PC가 받은 원격의 코드 '" + theirs.LookCode + "'(몸 " + theirs.Look?.Body?.Id + ", 옷 " + string.Join(",", theirs.Look?.Layers.Select(l => l.Id) ?? Array.Empty<string>())
                    + "), 이 PC 몸 " + _host.World.Walker.X + "," + _host.World.Walker.Y + ", 원격 몸 " + theirs.X + "," + theirs.Y);
                TestContext.WriteLine("스크린샷: " + Shot(_host, "02-this-pc-sees-linux"));
                TestContext.WriteLine("스크린샷(원격): " + _guest.Screenshot("two-machines-02-linux-sees-this-pc"));

                // 원격 게임이 걷는다 → 이 PC 화면에서 걷는 중, 멈추면 그 자리에 선다.
                var walk = new RemoteWalkSampler(_host, two);
                _guest.WhileCalling = walk.Sample;
                _guest.Call("walk-away", "4");
                _guest.WhileCalling = null;
                Assert.That(walk.SawWalking, Is.True, "원격 게임이 걷는 동안 이 PC 화면에 걷는 중");
                float[] gp = _guest.Query("pos").Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                Assert.That(_guest.Until(() => { walk.Sample(); return walk.Current is { } r && !r.Walking && Math.Abs(r.X - gp[0]) < 1f && Math.Abs(r.Y - gp[1]) < 1f; }, NetFrames),
                    Is.True, "멈춘 원격 게임이 이 PC 화면에서 그 자리에 선다");
                // 이 PC가 걷는다 → 원격 화면에서 따라온다.
                _host.WalkAway(4);
                float hx = _host.World.Walker.X, hy = _host.World.Walker.Y;
                Assert.That(_guest.Until(() =>
                {
                    string[] r = _guest.Query("remote-pos", one).Split(',');
                    return r.Length == 3 && r[2] == "False" && Math.Abs(float.Parse(r[0], CultureInfo.InvariantCulture) - hx) < 1f && Math.Abs(float.Parse(r[1], CultureInfo.InvariantCulture) - hy) < 1f;
                }, NetFrames), Is.True, "이 PC가 걸은 자리가 원격 화면에: " + _guest.Query("remote-pos", one) + " / " + hx + "," + hy);
                TestContext.WriteLine("스크린샷(원격): " + _guest.Screenshot("two-machines-02-linux-sees-this-pc-walked"));

                // 돈: 원격 게임의 알바 → 이 PC HUD의 방 지갑.
                _guest.GoTo("Town");
                Assert.That(_guest.Call("work-shift", ShopWork), Is.EqualTo("worked"), "원격 게임이 알바 미니게임을 끝까지");
                Assert.That(_guest.Until(() => HostHud.EndsWith(RoomBalance(5)) && _guest.Query("wallet") == "5", NetFrames), Is.True, "원격이 번 돈이 이 PC HUD에: " + HostHud);
                TestContext.WriteLine("스크린샷: " + Shot(_host, "02-this-pc-hud-linux-pay"));

                // 상자: 이 PC가 넣고 원격이 꺼낸다.
                var session = _host.Game.Session;
                _host.GoTo("Town");
                Assert.That(_host.WorkShift(ShopWork), Is.True);
                _host.GoTo("House");
                _host.PlaceFurniture(CrateId);
                Assert.That(_guest.Until(() => !session.WaitingForRoom && session.House.Placed.Any(f => f.Definition.StableId == CrateId), NetFrames), Is.True, "서버가 상자를 허락\n" + server.Output);
                string chest = session.House.Placed.Single(f => f.Definition.StableId == CrateId).InstanceId;
                _host.OpenChest(chest);
                _host.MoveStack("Wood", put: true);
                Assert.That(_guest.Until(() => !session.WaitingForRoom && ChestWood(chest) == 3, NetFrames), Is.True, "넣었다");
                _host.CloseChest();
                _guest.GoTo("House");
                Assert.That(_guest.Until(() => _guest.QueryInt("chest", chest, "Wood") == 3, NetFrames), Is.True, "원격 게임에도 상자의 나무가 보인다");
                _guest.Call("open-chest", chest);
                _guest.Call("move-stack", "Wood", "take");
                Assert.That(_guest.Until(() => _guest.QueryInt("item", "Wood") == 6 && ChestWood(chest) == 0, NetFrames), Is.True, "원격이 꺼냈다(알바 3 + 상자 3)");
                TestContext.WriteLine("스크린샷(원격): " + _guest.ScreenshotScreen(ScreenCatalog.Storage, "two-machines-02-linux-took-wood"));
                _guest.Call("close-chest");

                // 1분 재기: 둘이 같은 맵(집)에서 번갈아 걷는다.
                Assert.That(_guest.Until(() => _host.Game.Remotes.Visible.Count == 1, NetFrames), Is.True, "같은 맵");
                Measure(_guest, two, TimeSpan.FromSeconds(60));

                // 잠: 둘 다 자면 다음 날.
                DayResultScreen result = _host.GoToBed();
                Assert.That(_guest.Until(() => result.Model.Sleeping && result.Model.AwakeOthers.SequenceEqual(new[] { two }), NetFrames), Is.True, "자는 중 — 원격은 깨어 있다");
                _guest.Call("go-to-bed");
                var next = (UiButton)_host.Node(DayResultScreen.NextDayId);
                Assert.That(_guest.Until(() => next.Enabled && _guest.QueryBool("can-start-next-day"), NetFrames), Is.True, "둘 다 자면 다음 날\n" + server.Output);
                _host.PressButton(DayResultScreen.NextDayId);
                _guest.PressButton(DayResultScreen.NextDayId);
                Assert.That(_guest.Until(() => session.Student.CurrentDay == 2 && _guest.QueryInt("day") == 2, NetFrames), Is.True, "둘 다 2일째");
                TestContext.WriteLine("스크린샷: " + Shot(_host, "02-this-pc-day-two"));
                TestContext.WriteLine("스크린샷(원격): " + _guest.Screenshot("two-machines-02-linux-day-two"));
            }
            finally
            {
                _guest?.Dispose();
                _guest = null;
                _host?.Dispose();
                _host = null;
                StopRemoteServers();
            }
        }

        // ---------- 3. 이 PC에서 방 열기 + 원격 게임이 들어오기 ----------

        /// <summary>
        /// 이 PC가 시작 화면의 방 열기로(돈·집 공유) 이 PC의 LAN 주소에 방을 열고, Strix Halo의 게임이 그 주소로 들어온다. 서로 보이고, 원격이 번 돈이
        /// 이 PC HUD에 보인다. Windows 방화벽이 받는 UDP를 막으면 원격 게임이 들어오지 못한다 — 그때는 사용자 확인이 필요하다고 실패 이유에 적는다
        /// (방화벽 창을 조작하지 않는다). 준비: 이야기가 있는 슬롯 파일(양쪽).
        /// </summary>
        [Test, Explicit(NeedsTwoMachines), Category(TwoMachines)]
        public void TWO_MACHINES_003_ThisPcHostsOnLan_LinuxGameJoins()
        {
            BeginRemote();
            string lan = _remote.ThisPcAddress();
            Assert.That(IPAddress.IsLoopback(IPAddress.Parse(lan)), Is.False, "이 PC의 LAN 주소: " + lan);
            WriteStory(_hostSaves, "slot-1", "이쪽", 101);
            WriteStory(_guestSaves, "slot-1", "저쪽", 202);
            _host = LaunchHost();
            HostOpensAt(lan + ":0", RoomWallet.PieceName, RoomHouse.PieceName);
            string address = _host.Game.Hosting.ListenAddress;
            TestContext.WriteLine("[이 PC의 방] " + address);
            Assert.That(address, Does.StartWith(lan + ":"));
            _guest = StartRemoteGuest();
            _guest.PressButton(SaveSlotScreen.JoinId);
            _guest.WaitForScreen(ScreenCatalog.JoinServer);
            _guest.PressButton(JoinServerScreen.AddressId);
            _guest.TypeText(address);
            _guest.PressButton(JoinServerScreen.ConnectId);
            bool entered = _guest.Until(() => _guest.QueryBool("in-world") || _guest.Query("room-phase") == "Failed", NetFrames) && _guest.QueryBool("in-world");
            if (!entered)
            {
                TestContext.WriteLine("스크린샷(원격): " + _guest.ScreenshotScreen(ScreenCatalog.JoinServer, "two-machines-03-linux-could-not-join"));
                Assert.Fail("원격 게임이 이 PC의 방(" + address + ")에 들어오지 못했다: 단계 " + _guest.Query("room-phase") + ", 이유 " + _guest.Query("room-failure")
                    + ". Windows 방화벽이 이 PC로 들어오는 UDP를 막았을 수 있다 — 사용자 확인 필요(방화벽 창은 조작하지 않았다).");
            }
            string one = _host.Game.Room.PlayerId;
            Assert.That(_guest.Until(() => _host.Game.Remotes.Visible.Count == 1 && _guest.Query("remotes") == one, NetFrames), Is.True, "서로 보인다");
            TestContext.WriteLine("스크린샷: " + Shot(_host, "03-this-pc-host-sees-linux"));
            TestContext.WriteLine("스크린샷(원격): " + _guest.Screenshot("two-machines-03-linux-in-this-pc-room"));
            _guest.GoTo("Town");
            Assert.That(_guest.Call("work-shift", ShopWork), Is.EqualTo("worked"));
            Assert.That(_guest.Until(() => HostHud.EndsWith(RoomBalance(5)) && _guest.Query("wallet") == "5", NetFrames), Is.True, "원격이 번 돈이 방장 HUD에: " + HostHud);
            TestContext.WriteLine("스크린샷: " + Shot(_host, "03-this-pc-hud-linux-pay"));
        }

        /// <summary>방장: 시작 화면의 방 열기 → 같이 쓸 것을 포인터로 켬 → 주소를 쳐서 연다.</summary>
        private void HostOpensAt(string address, params string[] shares)
        {
            _host.PressButton(SaveSlotScreen.HostId);
            _host.WaitForScreen(ScreenCatalog.HostRoom);
            foreach (string piece in shares) _host.PressButton(HostRoomScreen.ShareToggleId(piece));
            _host.PressButton(HostRoomScreen.AddressId);
            _host.TypeText(address);
            _host.MovePointer(-50, -50);
            _host.Frames(2);
            TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.HostRoom, "two-machines-03-host-modal"));
            _host.PressButton(HostRoomScreen.OpenId);
            Assert.That(Until(_host, () => _host.Game.World != null && _host.Game.Hosting.IsOpen), Is.True, "방을 열고 들어간다");
        }

        // ---------- 5. 버전이 다르면 거절 ----------

        /// <summary>환경 변수: 원격 실행 폴더의 server-other(다른 커밋에서 만든 서버)의 커밋. two-machines.sh가 만든다.</summary>
        public const string OtherCommitVariable = "ROOTBORN_TWO_MACHINES_OTHER_COMMIT";

        /// <summary>
        /// 다른 커밋에서 만든 서버(Strix Halo)에 이 PC의 게임이 들어가려 하면 "게임 버전이 다르다"로 거절되고 들어가기 창에 그 이유가 보인다.
        /// 서버 로그에도 거절이 남는다. 준비: 이야기가 있는 슬롯 파일. 다른 커밋의 서버는 two-machines.sh가 그 커밋의 소스(git archive)로 게시한다.
        /// </summary>
        [Test, Explicit(NeedsTwoMachines), Category(TwoMachines)]
        public void TWO_MACHINES_004_ServerFromAnotherCommit_RejectsWithVersionReasonOnScreen()
        {
            BeginRemote();
            string other = Environment.GetEnvironmentVariable(OtherCommitVariable);
            if (string.IsNullOrEmpty(other)) Assert.Ignore(OtherCommitVariable + "가 없다(다른 커밋의 서버를 만들지 않았다)");
            try
            {
                RemoteServer server = StartRemoteServer(string.Empty, "server-other");
                string address = server.WaitForAddress(TimeSpan.FromSeconds(90));
                Assert.That(server.Has("+" + other), Is.True, "서버가 다른 커밋(" + other + ")\n" + server.Output);
                WriteStory(_hostSaves, "slot-1", "이쪽", 101);
                _host = LaunchHost();
                _host.PressButton(SaveSlotScreen.JoinId);
                _host.WaitForScreen(ScreenCatalog.JoinServer);
                _host.PressButton(JoinServerScreen.AddressId);
                _host.TypeText(address);
                _host.PressButton(JoinServerScreen.ConnectId);
                var join = (JoinServerScreen)_host.Screens.Find(ScreenCatalog.JoinServer);
                Assert.That(Until(_host, () => _host.Game.Room.Phase == RoomJoinPhase.Failed, NetFrames), Is.True, "거절된다: " + _host.Game.Room.Phase);
                Assert.That(_host.Game.Room.FailureCode, Is.EqualTo(Rootborn.Net.Room.RoomRefusal.GameVersionMismatch), "이유: 게임 버전");
                Assert.That(_host.Game.World, Is.Null, "들어가지 않았다");
                _host.Frames(2);
                string shown = ((UiTextBlock)_host.Node(JoinServerScreen.StatusId)).Text;
                Assert.That(join.StatusKey, Is.Not.EqualTo(RoomJoinText.UnknownReasonKey), "알려진 이유 문구");
                Assert.That(shown, Is.EqualTo(_host.Game.Text.Get(join.StatusKey)).And.Not.Empty, "들어가기 창에 이유가 보인다");
                TestContext.WriteLine("[거절 문구] " + join.StatusKey + " = " + shown);
                _host.MovePointer(-50, -50);
                _host.Frames(2);
                TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.JoinServer, "two-machines-04-version-refused"));
                Assert.That(Until(_host, () => server.Has("거절"), 300), Is.True, "서버 로그에 거절\n" + server.Output);
            }
            finally
            {
                _host?.Dispose();
                _host = null;
                StopRemoteServers();
            }
        }

        // ---------- 4. 재기 ----------

        /// <summary>
        /// 둘이 같은 맵에서 번갈아 걷는 동안(원격 4칸 → 이 PC 4칸 …) duration 동안: 왕복 지연(이 PC 연결의 LiteNetLib 값), 이 PC 화면에서 원격 몸이 튀는 프레임,
        /// 두 기계의 게임 시각 차이(같은 순간에 두 게임에 물은 분), 이 PC 게임 한 사람이 보내고 받은 바이트를 잰다. 표는 테스트 출력과 증거 폴더의 파일에.
        /// </summary>
        private void Measure(PeerGame guest, string guestId, TimeSpan duration)
        {
            var probe = NetProbe.Find(_host.Game.Room);
            var walk = new RemoteWalkSampler(_host, guestId);
            var rtt = new List<int>();
            int maxSkew = 0, skewSamples = 0;
            void Sample()
            {
                walk.Sample();
                if (walk.Frames % 10 != 0) return;
                int r = probe.RoundTripMs;
                if (r >= 0) rtt.Add(r);
            }
            void Skew()
            {
                int mine = _host.Game.Session.StudentTimeMinutes;
                int theirs = guest.QueryInt("minutes");
                int again = _host.Game.Session.StudentTimeMinutes; // 묻는 사이에 이쪽 분이 넘어가지 않은 표본만
                if (again != mine) return;
                skewSamples++;
                maxSkew = Math.Max(maxSkew, Math.Abs(mine - theirs));
            }
            probe.Reset();
            var watch = Stopwatch.StartNew();
            int walks = 0;
            guest.WhileCalling = Sample;
            while (watch.Elapsed < duration)
            {
                Skew();
                guest.Call("walk-away", "4");
                walks++;
                Skew();
                _host.WalkAway(4); // 이쪽 프레임마다 원격도 한 프레임(AfterFrame) — 원격 몸은 멈춰 있다
            }
            guest.WhileCalling = null;
            double seconds = watch.Elapsed.TotalSeconds;
            long sent = probe.BytesSent, received = probe.BytesReceived;
            rtt.Sort();
            string Ms(double q) => rtt.Count == 0 ? "못 잼" : rtt[Math.Min(rtt.Count - 1, (int)(q * rtt.Count))] + "ms";
            var table = new StringBuilder();
            table.AppendLine("| 잰 것 | 값 |");
            table.AppendLine("|---|---|");
            table.AppendLine("| 잰 시간(실제) | " + seconds.ToString("0.0", CultureInfo.InvariantCulture) + "초, 이 PC 게임 " + walk.Frames + "프레임(원격과 번갈아), 원격 걷기 " + walks + "번 |");
            table.AppendLine("| 왕복 지연(이 PC ↔ 서버, LiteNetLib) | 중앙 " + Ms(0.5) + ", 최소 " + (rtt.Count == 0 ? "-" : rtt[0] + "ms") + ", 최대 " + (rtt.Count == 0 ? "-" : rtt[^1] + "ms") + " (표본 " + rtt.Count + ") |");
            table.AppendLine("| 원격 몸 한 프레임 이동(걷는 중) | 중앙 " + walk.MedianStep.ToString("0.00", CultureInfo.InvariantCulture) + "px, 최대 " + walk.MaxStep.ToString("0.00", CultureInfo.InvariantCulture) + "px |");
            table.AppendLine("| 튀는 프레임(걷는 중 한 프레임 이동 > 중앙의 3배) | " + walk.Jumps + " / " + walk.WalkingFrames + " |");
            table.AppendLine("| 멈칫한 프레임(걷는 중인데 안 움직임) | " + walk.Stalls + " / " + walk.WalkingFrames + " |");
            table.AppendLine("| 두 기계의 게임 시각 차이(같은 순간에 물은 분) | 최대 " + maxSkew + "분 (표본 " + skewSamples + ") |");
            table.AppendLine("| 이 PC 게임(한 사람)이 보낸 바이트 | " + Per(sent, seconds) + " (패킷 " + probe.PacketsSent + ") |");
            table.AppendLine("| 이 PC 게임(한 사람)이 받은 바이트 | " + Per(received, seconds) + " (패킷 " + probe.PacketsReceived + ", 잃은 패킷 " + probe.PacketLoss + ") |");
            TestContext.WriteLine("[재기]\n" + table);
            Directory.CreateDirectory(_evidence);
            File.WriteAllText(Path.Combine(_evidence, "two-machines-measurements.md"), table.ToString(), new UTF8Encoding(false));
            Assert.That(probe.Found, Is.True, "NetManager를 찾아 통계를 켰다");
            Assert.That(walk.WalkingFrames, Is.GreaterThan(0), "원격 몸이 걷는 것을 봤다");
        }

        private static string Per(long bytes, double seconds)
            => bytes < 0 ? "못 잼" : bytes.ToString("N0", CultureInfo.InvariantCulture) + "바이트 / " + seconds.ToString("0", CultureInfo.InvariantCulture) + "초 = 1분에 "
                + (bytes * 60.0 / seconds).ToString("N0", CultureInfo.InvariantCulture) + "바이트";

        /// <summary>이 PC 화면의 원격 몸(메운 자리)을 프레임마다 적는다. 걷는 중인 프레임의 이동 거리로 튀는 프레임·멈칫한 프레임을 센다.</summary>
        private sealed class RemoteWalkSampler
        {
            private readonly PlayerDriver _p;
            private readonly string _id;
            private readonly List<float> _steps = new List<float>(4096);
            private float _lx, _ly;
            private bool _have;

            public RemoteWalkSampler(PlayerDriver p, string id) { _p = p; _id = id; }

            public bool SawWalking { get; private set; }
            public int Frames { get; private set; }
            public int Stalls { get; private set; }
            public int WalkingFrames => _steps.Count + Stalls;
            public Rootborn.Host.World.RemoteAvatar Current => _p.Game.Remotes?.Visible.FirstOrDefault(r => r.PlayerId == _id);

            public void Sample()
            {
                Frames++;
                var r = Current;
                if (r == null) { _have = false; return; }
                if (_have && r.Walking)
                {
                    SawWalking = true;
                    float step = MathF.Sqrt((r.X - _lx) * (r.X - _lx) + (r.Y - _ly) * (r.Y - _ly));
                    if (step < 0.001f) Stalls++;
                    else _steps.Add(step);
                }
                _lx = r.X; _ly = r.Y; _have = true;
            }

            public float MedianStep
            {
                get
                {
                    if (_steps.Count == 0) return 0;
                    var s = _steps.OrderBy(v => v).ToList();
                    return s[s.Count / 2];
                }
            }

            public float MaxStep => _steps.Count == 0 ? 0 : _steps.Max();
            public int Jumps { get { float m = MedianStep; return _steps.Count(v => v > 3 * m); } }
        }
    }
}
