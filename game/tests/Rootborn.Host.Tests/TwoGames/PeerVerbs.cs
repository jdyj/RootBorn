using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rootborn.Core.Minigames;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 자식 게임의 동사 표(부모가 보내는 줄의 첫 칸 → 할 일). 조작 동사는 전부 <see cref="PlayerDriver"/>의 동사나 <see cref="PlayActions"/>
    /// (키·글자는 SDL 이벤트 큐, 포인터는 게임의 포인터 장치 계층 — 장치 이벤트 주입)이고, 상태는 <see cref="PeerQueries"/>로 읽기만 한다.
    /// 동사를 하는 동안 프레임을 하나 돌릴 때마다 부모에게 <see cref="PeerProtocol.Tick"/>을 보내고 답을 기다린다(부모도 한 프레임 — 번갈아 간다).
    /// "step"은 부모의 한 프레임에 맞춰 이쪽도 한 프레임 도는 것이라 묻지 않는다.
    /// 새 동사는 한 줄: <see cref="Register"/>.
    /// </summary>
    public sealed class PeerVerbs : IDisposable
    {
        private readonly TextReader _input;
        private readonly TextWriter _output;
        private readonly Dictionary<string, Func<string[], string>> _verbs = new Dictionary<string, Func<string[], string>>(StringComparer.Ordinal);
        private PlayerDriver _player;
        private bool _ticking;
        private bool _quit;

        public PeerVerbs(TextReader input, TextWriter output)
        {
            _input = input;
            _output = output;

            Register("launch", Launch);
            Register("close", a => { ClosePlayer(); return string.Empty; });
            Register("quit", a => { _quit = true; return string.Empty; });
            Register("step", a => { if (_player != null) { _ticking = false; _player.Frames(1); } return string.Empty; });
            Register("frames", a => { P.Frames(Int(a, 0)); return string.Empty; });
            Register("key", a => { P.PressKey(Key(a, 0), a.Length > 1 ? Int(a, 1) : 1); return string.Empty; });
            Register("key-down", a => { P.KeyDown(Key(a, 0)); return string.Empty; });
            Register("key-up", a => { P.KeyUp(Key(a, 0)); return string.Empty; });
            Register("type", a => { P.TypeText(a[0]); return string.Empty; });
            Register("press-button", a => { P.PressButton(a[0]); return string.Empty; });
            Register("click-at", a => { P.ClickAt(Float(a, 0), Float(a, 1)); return string.Empty; });
            Register("move-pointer", a => { P.MovePointer(Float(a, 0), Float(a, 1)); return string.Empty; });
            Register("pointer-down", a => { P.PressPointer(); return string.Empty; });
            Register("pointer-up", a => { P.ReleasePointer(); return string.Empty; });
            Register("release", a => { P.ReleaseEverything(); return string.Empty; });
            Register("wait-screen", a => P.WaitForScreen(a[0], a.Length > 1 ? Int(a, 1) : 120).ToString(CultureInfo.InvariantCulture));
            Register("wait-screen-closed", a => P.WaitForScreenClosed(a[0], a.Length > 1 ? Int(a, 1) : 120).ToString(CultureInfo.InvariantCulture));
            Register("walk-door", a => P.WalkThroughDoor(a[0]));
            Register("walk-near", a => { P.WalkNear(Float(a, 0), Float(a, 1), Float(a, 2)); return string.Empty; });
            Register("walk-away", a => { P.WalkAway(Int(a, 0)); return string.Empty; });
            Register("go-to", a => { P.GoTo(a[0]); return string.Empty; });
            Register("walk-to-thing", a => { P.WalkToThing(a[0], a.Length > 1 ? a[1] : null); return string.Empty; });
            Register("work-shift", a => P.WorkShift(a[0]) ? "worked" : "refused");
            Register("buy-first-offer", a => P.BuyFirstOffer(a[0]));
            Register("place-furniture", a => { GridPoint c = P.PlaceFurniture(a[0]); return c.X + "," + c.Y; });
            Register("open-chest", a => { P.OpenChest(a[0]); return string.Empty; });
            Register("move-stack", a => { P.MoveStack(a[0], a[1] == "put"); return string.Empty; });
            Register("aim-stack", a => { var r = P.StorageCell(a[0], a[1] == "put"); P.MovePointer(r.CenterX, r.CenterY); return string.Empty; });
            Register("close-chest", a => { P.CloseChest(); return string.Empty; });
            Register("go-to-bed", a => { P.GoToBed(); return string.Empty; });
            Register("dismiss-popups", a => { P.DismissPopups(); return string.Empty; });
            Register("screenshot", a => P.Screenshot(a[0]));
            Register("screenshot-screen", a => P.ScreenshotScreen(a[0], a[1]));
            Register("query", a => PeerQueries.Ask(_player, a[0], a.Skip(1).ToArray()));
            // 이쪽에서 조건이 될 때까지 프레임을 돈다(프레임마다 부모도 한 프레임). 칸: 조회 이름, 기다리는 값, 최대 프레임, 조회 인자…
            Register("wait-query", a =>
            {
                string[] rest = a.Skip(3).ToArray();
                int max = Int(a, 2);
                for (int i = 0; i <= max; i++)
                {
                    string value = PeerQueries.Ask(_player, a[0], rest);
                    if (value == a[1]) return i.ToString(CultureInfo.InvariantCulture);
                    P.Frames(1);
                }
                throw new InvalidOperationException(max + "프레임 안에 " + a[0] + "이(가) " + a[1] + "이 되지 않았다. 지금 " + PeerQueries.Ask(_player, a[0], rest));
            });
        }

        public void Register(string verb, Func<string[], string> run) => _verbs[verb] = run;

        private PlayerDriver P => _player ?? throw new InvalidOperationException("게임을 아직 띄우지 않았다(launch)");

        private string Launch(string[] args)
        {
            ClosePlayer();
            HostOptions options = HostOptions.Resolve(args);
            options.TestMode = true;
            _player = new PlayerDriver(options) { AfterFrame = AskParent };
            _player.Frames(3);
            return string.Empty;
        }

        /// <summary>한 프레임을 돌렸다: 부모가 자기 게임을 한 프레임 돌릴 때까지 기다린다(step 중에는 묻지 않는다).</summary>
        private void AskParent()
        {
            if (!_ticking) return;
            _output.WriteLine(PeerProtocol.Tick);
            string answer = _input.ReadLine();
            if (answer == null) throw new EndOfStreamException("부모가 끝났다");
            if (answer != PeerProtocol.Go) throw new InvalidOperationException("프레임 답 대신 " + answer);
        }

        private void ClosePlayer()
        {
            PlayerDriver player = _player;
            _player = null;
            if (player == null) return;
            player.AfterFrame = null; // 닫는 동안 도는 프레임은 묻지 않는다
            player.Dispose();
        }

        public int Run()
        {
            while (!_quit)
            {
                string line = _input.ReadLine();
                if (line == null) break; // 부모가 끝났다
                if (line.Length == 0) continue;
                string[] parts = PeerProtocol.Decode(line);
                if (!_verbs.TryGetValue(parts[0], out var run))
                {
                    _output.WriteLine(PeerProtocol.Error + "\t" + PeerProtocol.EscapeOne("모르는 동사: " + parts[0]));
                    continue;
                }
                _ticking = true;
                try
                {
                    string result = run(parts.Skip(1).ToArray()) ?? string.Empty;
                    _output.WriteLine(PeerProtocol.Ok + "\t" + PeerProtocol.EscapeOne(result));
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (Exception e)
                {
                    _output.WriteLine(PeerProtocol.Error + "\t" + PeerProtocol.EscapeOne(e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace));
                    try { _player?.ReleaseEverything(); } catch (Exception) { }
                }
                finally
                {
                    _ticking = false;
                }
            }
            return 0;
        }

        public void Dispose()
        {
            try { ClosePlayer(); } catch (Exception) { }
        }

        private static int Int(string[] a, int i) => int.Parse(a[i], CultureInfo.InvariantCulture);
        private static float Float(string[] a, int i) => float.Parse(a[i], CultureInfo.InvariantCulture);
        private static TestKey Key(string[] a, int i) => (TestKey)Enum.Parse(typeof(TestKey), a[i]);
    }

    /// <summary>
    /// 자식 게임의 상태 읽기(부모의 확인용 — 바꾸지 않는다). 값은 글자 하나로 돌려준다(참/거짓은 "True"/"False", 수는 불변 문화권).
    /// 새 조회는 표에 한 줄.
    /// </summary>
    public static class PeerQueries
    {
        private static readonly Dictionary<string, Func<PlayerDriver, string[], object>> Table = new Dictionary<string, Func<PlayerDriver, string[], object>>(StringComparer.Ordinal)
        {
            ["launched"] = (p, a) => p != null,
            ["in-world"] = (p, a) => p.Game.World != null && p.Game.Session != null,
            ["map"] = (p, a) => p.Game.World?.Session.Map.Id ?? string.Empty,
            ["pos"] = (p, a) => p.Game.World == null ? string.Empty : F(p.World.Walker.X) + "," + F(p.World.Walker.Y),
            ["in-room"] = (p, a) => p.Game.Room?.InRoom ?? false,
            ["room-player-id"] = (p, a) => p.Game.Room?.PlayerId ?? string.Empty,
            ["room-phase"] = (p, a) => p.Game.Room?.Phase.ToString() ?? string.Empty,
            ["room-failure"] = (p, a) => p.Game.Room?.FailureCode ?? string.Empty,
            ["remotes"] = (p, a) => p.Game.Remotes == null ? string.Empty : string.Join(",", p.Game.Remotes.Visible.Select(r => r.PlayerId)),
            ["remotes-drawn"] = (p, a) => p.Game.World?.RemotesDrawn ?? 0,
            ["remote-pos"] = (p, a) =>
            {
                var r = p.Game.Remotes?.Visible.FirstOrDefault(v => v.PlayerId == a[0]);
                return r == null ? string.Empty : F(r.X) + "," + F(r.Y) + "," + r.Walking;
            },
            ["screen-open"] = (p, a) => p.Game.Screens != null && p.Screens.IsOpen(a[0]),
            ["screens"] = (p, a) => p.Game.Screens == null ? string.Empty : string.Join(",", p.Screens.OpenScreens.Select(s => s.Id)),
            ["text"] = (p, a) =>
            {
                UiNode node = p.Game.Ui.Root.Find(a[0]);
                return node switch { UiLabel l => l.Text, UiTextBlock t => t.Text, UiButton b => b.Text, UiTextField f => f.Text, _ => string.Empty };
            },
            ["active"] = (p, a) => p.Game.Ui.Root.Find(a[0])?.IsActive ?? false,
            ["enabled"] = (p, a) => (p.Game.Ui.Root.Find(a[0]) as UiButton)?.Enabled ?? false,
            ["ui-text"] = (p, a) => a.Length > 1 ? p.Game.Text.Format(a[0], a.Skip(1).Select(Arg).ToArray()) : p.Game.Text.Get(a[0]),
            ["wallet"] = (p, a) => p.Game.Session.Wallet.Balance,
            ["item"] = (p, a) => p.Game.Session.Inventory.CountOf(p.Game.Session.Item(a[0])),
            ["house-placed"] = (p, a) => p.Game.Session.House?.Placed.Count ?? -1,
            ["house-placed-ids"] = (p, a) => p.Game.Session.House == null ? string.Empty : string.Join(",", p.Game.Session.House.Placed.Select(f => f.InstanceId)),
            // 이 게임 한 판이 놓을 수 있는 가구 중 한 칸짜리 막는 가구(상자 제외)의 id — 어떤 가구를 고를지 정하는 데만 쓴다.
            ["first-free-furniture"] = (p, a) =>
            {
                var session = p.Game.Session;
                var storage = new HashSet<string>(session.Definitions.All<Rootborn.Core.Placement.FurnitureStorageDefinition>().Select(s => s.Furniture.StableId));
                return session.AvailableFurniture().First(f => f.BlocksMovement && f.FootprintCells.Count == 1 && !storage.Contains(f.StableId)).StableId;
            },
            ["furniture-drawn"] =(p, a) => p.Game.World?.FurnitureDrawCount ?? 0,
            ["shares-house"] = (p, a) => p.Game.Session.SharesHouse,
            ["shares-wallet"] = (p, a) => p.Game.Session.SharesWallet,
            ["waiting-for-room"] = (p, a) => p.Game.Session.WaitingForRoom,
            ["chest"] = (p, a) =>
            {
                var contents = p.Game.Session.StorageAt(a[0]);
                if (contents == null) return -1;
                int n = 0;
                foreach (var s in contents.Stacks) if (s.Item != null && s.Item.Id == a[1]) n += s.Count;
                return n;
            },
            ["storage-notice"] = (p, a) => (p.Screens.Find(ScreenCatalog.Storage) as Rootborn.Screens.Storage.StorageScreen) is { } s && p.Screens.IsOpen(ScreenCatalog.Storage)
                ? ((UiTextBlock)p.Node(Rootborn.Screens.Storage.StorageScreen.NoticeId)).Text : string.Empty,
            ["day"] = (p, a) => p.Game.Session.Student.CurrentDay,
            ["day-state"] = (p, a) => p.Game.Session.Student.DayState.ToString(),
            ["minutes"] = (p, a) => p.Game.Session.StudentTimeMinutes,
            ["energy"] = (p, a) => p.Game.Session.Student.Energy,
            ["exhausted"] = (p, a) => p.Game.Session.IsExhausted,
            ["slept-last-night"] = (p, a) => p.Game.Session.Student.LastNight.HasSlept,
            ["busy"] = (p, a) => p.Game.Session.IsBusy,
            ["busy-ends"] = (p, a) => Rootborn.Core.Time.TimeRules.ClockMinutes(p.Game.Session.ActivityEndsAtMinutes),
            ["sleeping-in-room"] = (p, a) => p.Game.Session.IsSleepingInRoom,
            ["can-start-next-day"] = (p, a) => p.Game.Session.CanStartNextDay,
            ["awake-others"] = (p, a) =>
            {
                var list = new List<string>();
                p.Game.Session.RoomAwakeOthers(list);
                return string.Join(",", list);
            },
            ["room-day"] = (p, a) => p.Game.Session.Room.Known ? p.Game.Session.Room.Day : -1,
            ["room-clock"] = (p, a) => p.Game.Session.Room.Known ? Rootborn.Core.Time.TimeRules.ClockMinutes(p.Game.Session.Room.Minutes) : -1,
            ["refusal-key"] = (p, a) => p.Screens.IsOpen(ScreenCatalog.InteractionRefusal) ? ((InteractionRefusalNoticeScreen)p.Screens.Find(ScreenCatalog.InteractionRefusal)).MessageKey : string.Empty,
            ["room-notice-reason"] = (p, a) => p.Game.Screens != null && p.Screens.IsOpen(ScreenCatalog.RoomNotice) ? ((RoomNoticeScreen)p.Screens.Find(ScreenCatalog.RoomNotice)).ReasonKey : string.Empty,
            ["day-result-sleeping"] = (p, a) => p.Screens.Find(ScreenCatalog.DayResult) is DayResultScreen d && p.Screens.IsOpen(ScreenCatalog.DayResult) && d.Model.Sleeping,
            ["missing-images"] = (p, a) => string.Join(",", p.Game.Renderer.MissingImages),
            ["collision-violations"] = (p, a) => p.CollisionViolations,
        };

        public static string Ask(PlayerDriver player, string name, string[] args)
        {
            if (!Table.TryGetValue(name, out var read)) throw new InvalidOperationException("모르는 조회: " + name);
            if (player == null && name != "launched") throw new InvalidOperationException("게임을 아직 띄우지 않았다(launch)");
            object value = read(player, args);
            return value switch
            {
                null => string.Empty,
                bool b => b ? "True" : "False",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        private static object Arg(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : s;
    }
}
