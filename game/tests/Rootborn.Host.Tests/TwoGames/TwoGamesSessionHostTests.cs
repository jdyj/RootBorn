using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Settings;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>나갔다 들어오기, 방 닫기·다시 열기, 데디케이티드 서버를 두 게임의 입력으로. 근거와 준비는 <see cref="TwoGamesHostTests"/> 머리말.</summary>
    public sealed partial class TwoGamesHostTests
    {
        private string[] GuestArgs => new[] { "--save-root", _guestSaves, "--size", "1280x720", "--evidence", _evidence };

        /// <summary>
        /// 손님이 알바로 나무·용돈을 받은 뒤 게임을 끄고(같은 프로세스에서 게임을 닫았다 다시 띄움) 다시 들어오면 이어진다. 방장이 설정의 방 닫기를 누르면
        /// 손님 화면에 끊김 알림이 뜨고 시작 화면으로 간다. 방장이 시작 화면의 방 열기로 같은 슬롯을 다시 열고 손님이 들어오면 손님 진행이 이어진다.
        /// 준비: 이야기가 있는 슬롯 파일(양쪽).
        /// </summary>
        [Test]
        public void TWO_GAMES_008_GuestQuitsAndRejoins_Continues_HostClosesTheRoom_GuestIsTold_ReopenAndRejoinContinues()
        {
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "손님", 202);
            _host = LaunchHost();
            HostOpensFromStartMenu(null);
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            string guestId = GuestId;

            _guest.GoTo("Town");
            Assert.That(_guest.Call("work-shift", ShopWork), Is.EqualTo("worked"));
            Assert.That(_guest.QueryInt("item", "Wood"), Is.EqualTo(3));
            Assert.That(_guest.QueryInt("wallet"), Is.EqualTo(5), "돈을 같이 쓰지 않는 방: 손님 지갑");

            // 손님이 게임을 끈다 → 방장 방에서 나간다.
            _guest.Call("close");
            Assert.That(Until(_host, () => _host.Game.Hosting.PlayerCount == 1), Is.True, "끄면 방에서 나간다");
            _guest.Launch(GuestArgs);
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            Assert.That(GuestId, Is.EqualTo(guestId), "같은 인물");
            Assert.That(_guest.QueryInt("item", "Wood"), Is.EqualTo(3), "다시 들어오면 이어진다(가방)");
            Assert.That(_guest.QueryInt("wallet"), Is.EqualTo(5), "다시 들어오면 이어진다(지갑)");
            TestContext.WriteLine("[다시 들어옴] 손님 맵 " + _guest.Query("map") + ", " + _guest.Query("day") + "일");
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-08-guest-rejoined"));

            // 방장이 방을 닫는다(설정 → 방 닫기).
            _host.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            _host.PressButton(SettingsScreen.RoomCloseId);
            Assert.That(_guest.Until(() => _host.Game.World == null && _host.Screens.IsOpen(ScreenCatalog.SaveSlotSelect)), Is.True, "방장은 시작 화면");
            Assert.That(_guest.UntilQuery("ui.join.reason.server-shutdown", 900, "room-notice-reason"), Is.True, "손님 화면에 방이 닫혔다는 알림");
            Assert.That(_guest.QueryBool("in-world"), Is.False);
            Assert.That(_guest.QueryBool("screen-open", ScreenCatalog.SaveSlotSelect), Is.True, "손님도 시작 화면");
            TestContext.WriteLine("스크린샷(손님): " + _guest.ScreenshotScreen(ScreenCatalog.RoomNotice, "two-08-guest-room-closed-notice"));
            _guest.PressButton(RoomNoticeScreen.OkId);

            // 방장이 같은 슬롯으로 다시 연다 → 손님이 다시 들어온다.
            _host.PressButton(SaveSlotScreen.HostId);
            _host.WaitForScreen(ScreenCatalog.HostRoom);
            _host.PressButton(HostRoomScreen.AddressId);
            _host.TypeText(Loopback);
            _host.PressButton(HostRoomScreen.OpenId);
            Assert.That(Until(_host, () => _host.Game.World != null && _host.Game.Hosting.IsOpen), Is.True);
            Assert.That(_host.Game.Hosting.Room.Server.IsNewRoom, Is.False, "같은 슬롯으로 다시 열면 그 방을 잇는다");
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            Assert.That(GuestId, Is.EqualTo(guestId));
            Assert.That(_guest.QueryInt("item", "Wood"), Is.EqualTo(3), "다시 연 방에서도 손님 진행이 이어진다");
            Assert.That(_guest.QueryInt("wallet"), Is.EqualTo(5));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-08-guest-after-reopen"));
        }

        // ---------- 데디케이티드 서버 ----------

        /// <summary>
        /// 데디케이티드 서버(Rootborn.Server 프로세스, 돈·집 공유) + 게임 둘(이 프로세스의 게임과 자식 게임, 둘 다 손님): 서버 주소로 들어가기로 들어가
        /// 돈(한 사람의 알바가 다른 사람 HUD에), 상자(한 사람이 넣고 다른 사람이 꺼냄), 잠(둘 다 자면 다음 날)을 한 번씩. 서버 프로세스는 테스트가 끝나면 끈다.
        /// 준비: 이야기가 있는 슬롯 파일(양쪽). 서버 주소는 루프백, 포트는 0(서버가 로그에 찍은 빈 포트).
        /// </summary>
        [Test]
        public void TWO_GAMES_009_DedicatedServer_TwoGuests_Money_Chest_Sleep()
        {
            string serverSaves = Path.Combine(Path.GetTempPath(), "rootborn-two-games-server-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(serverSaves);
            using var server = DedicatedServer.Start("--root", _repo, "--save-root", serverSaves, "--slot", "room", "--bind", IPAddress.Loopback.ToString(), "--port", "0",
                "--share", RoomWallet.PieceName + "," + RoomHouse.PieceName);
            try
            {
                string address = IPAddress.Loopback + ":" + server.WaitForPort(TimeSpan.FromSeconds(90));
                WriteStory(_hostSaves, "slot-1", "하나", 101);
                WriteStory(_guestSaves, "slot-1", "둘", 202);
                _host = LaunchHost();
                JoinByAddress(_host, address);
                _guest = StartGuest();
                GuestJoinsByAddress(address, null);
                string one = _host.Game.Room.PlayerId, two = GuestId;
                Assert.That(_guest.Until(() => _host.Game.Remotes.Visible.Count == 1 && _guest.Query("remotes") == one), Is.True, "서로 보인다\n" + server.Output);

                // 돈: 둘째의 알바 → 첫째 HUD의 방 지갑.
                _guest.GoTo("Town");
                Assert.That(_guest.Call("work-shift", ShopWork), Is.EqualTo("worked"));
                Assert.That(_guest.Until(() => HostHud.EndsWith(RoomBalance(5))), Is.True, "서버 방 지갑이 첫째 HUD에: " + HostHud + "\n" + server.Output);
                TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-09-server-one-hud-two-pay"));

                // 상자: 첫째가 알바로 받은 나무를 집에 놓은 상자에 넣고, 둘째가 걸어가 꺼낸다.
                var session = _host.Game.Session;
                _host.GoTo("Town");
                Assert.That(_host.WorkShift(ShopWork), Is.True);
                _host.GoTo("House");
                _host.PlaceFurniture(CrateId);
                Assert.That(_guest.Until(() => !session.WaitingForRoom && session.House.Placed.Any(f => f.Definition.StableId == CrateId)), Is.True, "서버가 상자를 허락했다\n" + server.Output);
                string chest = session.House.Placed.Single(f => f.Definition.StableId == CrateId).InstanceId;
                _host.OpenChest(chest);
                _host.MoveStack("Wood", put: true);
                Assert.That(_guest.Until(() => !session.WaitingForRoom && ChestWood(chest) == 3), Is.True, "넣었다");
                _host.CloseChest();
                _guest.GoTo("House");
                Assert.That(_guest.Until(() => _guest.QueryInt("chest", chest, "Wood") == 3), Is.True, "둘째에게도 상자의 나무가 보인다");
                _guest.Call("open-chest", chest);
                _guest.Call("move-stack", "Wood", "take");
                Assert.That(_guest.Until(() => _guest.QueryInt("item", "Wood") == 6 && ChestWood(chest) == 0), Is.True, "둘째가 꺼냈다(알바 3 + 상자 3)");
                TestContext.WriteLine("스크린샷(손님): " + _guest.ScreenshotScreen(ScreenCatalog.Storage, "two-09-server-two-took-wood"));
                _guest.Call("close-chest");

                // 잠: 둘 다 잠자리에서 E → 곧바로 다음 날.
                DayResultScreen result = _host.GoToBed();
                Assert.That(_guest.Until(() => result.Model.Sleeping && result.Model.AwakeOthers.SequenceEqual(new[] { two })), Is.True, "자는 중 — 둘째는 깨어 있다");
                _guest.Call("go-to-bed");
                var next = (UiButton)_host.Node(DayResultScreen.NextDayId);
                Assert.That(_guest.Until(() => next.Enabled && _guest.QueryBool("can-start-next-day")), Is.True, "둘 다 자면 다음 날\n" + server.Output);
                _host.PressButton(DayResultScreen.NextDayId);
                _guest.PressButton(DayResultScreen.NextDayId);
                Assert.That(_host.Game.Session.Student.CurrentDay, Is.EqualTo(2));
                Assert.That(_guest.UntilQuery("2", 60, "day"), Is.True);
                TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-09-server-one-day-two"));
                TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-09-server-two-day-two"));
            }
            finally
            {
                _guest?.Dispose();
                _guest = null;
                _host?.Dispose();
                _host = null;
                server.Stop();
                server.WaitForExit(TimeSpan.FromSeconds(20));
                server.Kill();
                try { Directory.Delete(serverSaves, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        /// <summary>이 프로세스의 게임이 시작 화면의 서버 주소로 들어가기로 들어간다.</summary>
        private static void JoinByAddress(PlayerDriver p, string address)
        {
            p.PressButton(SaveSlotScreen.JoinId);
            p.WaitForScreen(ScreenCatalog.JoinServer);
            p.PressButton(JoinServerScreen.AddressId);
            p.TypeText(address);
            p.PressButton(JoinServerScreen.ConnectId);
            Assert.That(Until(p, () => p.Game.World != null, 900), Is.True, "서버 주소로 들어간다");
            Assert.That(p.Game.Room.InRoom, Is.True);
        }

        /// <summary>빌드 출력에 복사된 Rootborn.Server.dll을 dotnet으로 띄운다(서버 프로세스 테스트와 같은 방식). 출력은 모아 둔다.</summary>
        private sealed class DedicatedServer : IDisposable
        {
            private readonly Process _process;
            private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();

            private DedicatedServer(Process process)
            {
                _process = process;
                _process.OutputDataReceived += (s, e) => { if (e.Data != null) _lines.Enqueue(e.Data); };
                _process.ErrorDataReceived += (s, e) => { if (e.Data != null) _lines.Enqueue(e.Data); };
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }

            public string Output => string.Join(Environment.NewLine, _lines);

            public static DedicatedServer Start(params string[] args)
            {
                string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                if (string.IsNullOrEmpty(dotnet) || !File.Exists(dotnet)) dotnet = "dotnet";
                var info = new ProcessStartInfo(dotnet)
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    StandardInputEncoding = new System.Text.UTF8Encoding(false),
                };
                info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Rootborn.Server.dll"));
                foreach (string arg in args) info.ArgumentList.Add(arg);
                foreach (string name in new[] { Rootborn.Server.ServerOptions.SlotVariable, Rootborn.Server.ServerOptions.BindVariable, Rootborn.Server.ServerOptions.PortVariable,
                    Rootborn.Server.ServerOptions.MaxPlayersVariable, Rootborn.Server.ServerOptions.SaveRootVariable, Rootborn.Server.ServerOptions.DataVariable, Rootborn.Server.ServerOptions.ShareVariable })
                    info.Environment.Remove(name);
                return new DedicatedServer(Process.Start(info));
            }

            /// <summary>서버가 듣기 시작한 줄에서 포트를 읽는다(벽시계 한도는 서버가 뜨지 않았을 때 끝내는 안전장치).</summary>
            public int WaitForPort(TimeSpan timeout)
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < timeout)
                {
                    foreach (string line in _lines)
                    {
                        int at = line.IndexOf(Rootborn.Server.Program.ListeningPrefix, StringComparison.Ordinal);
                        if (at < 0) continue;
                        string endpoint = line.Substring(at + Rootborn.Server.Program.ListeningPrefix.Length).Trim();
                        return int.Parse(endpoint.Substring(endpoint.LastIndexOf(':') + 1), CultureInfo.InvariantCulture);
                    }
                    if (_process.HasExited) break;
                    Thread.Sleep(20);
                }
                throw new AssertionException("서버가 듣기 시작하지 않았다\n" + Output);
            }

            public void Stop()
            {
                try
                {
                    if (_process.HasExited) return;
                    _process.StandardInput.WriteLine(Rootborn.Server.Program.StopCommand);
                    _process.StandardInput.Flush();
                }
                catch (IOException) { }
            }

            public bool WaitForExit(TimeSpan timeout) => _process.WaitForExit((int)timeout.TotalMilliseconds);

            public void Kill()
            {
                try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }

            public void Dispose()
            {
                Kill();
                _process.Dispose();
            }
        }
    }
}
