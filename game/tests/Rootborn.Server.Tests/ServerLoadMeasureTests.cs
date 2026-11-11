using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.Room;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Session;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 정원 8명이 빠르게 저장할 때 방의 틱이 얼마나 걸리는가(앞 세션 보고의 미확인). 손님 저장을 받는 방(데디케이티드 서버와 같은 RoomServer + GuestSaveFolder,
    /// 실제 data/, 임시 폴더)에 같은 프로세스 안 전송으로 8명이 들어가, 사람마다 초당 N번 저장한다(명령 하나 = 저장 한 번 = 바뀐 파일을 방으로).
    /// 틱마다 방의 Tick 벽시계 시간을 잰다. 재기만 하는 테스트라 [Explicit] — 손으로 돌린다(결과 표는 테스트 출력).
    /// 명령: 수첩 추적 바꾸기(작은 파일 하나 — 방은 받을 때마다 그 사람의 세이브 전체로 게임 한 판을 시작해 검사한다), 알바 한 번(큰 저장 묶음).
    /// </summary>
    [Explicit("재기만 한다 — 손으로 돌린다"), NonParallelizable]
    public sealed class ServerLoadMeasureTests
    {
        private const int Players = 8;
        private const double Seconds = 4.0;
        private static GameDefinitions _definitions;
        private static GameStartSettings _settings;
        private string _root;

        [OneTimeSetUp]
        public void LoadData()
        {
            var load = GameDataLoader.Load(Path.Combine(ServerProcessTests.RepoRoot(), "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            _definitions = load.Definitions;
            _settings = GameStartSettings.FromDefinitions(_definitions);
        }

        [SetUp] public void SetUp() { _root = Path.Combine(Path.GetTempPath(), "rootborn-load", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root); }
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        private const string MoneyOnlyWork = "work.clinic-helper";

        private sealed class Guest
        {
            public SessionClient Client;
            public RoomGuestLink Link;
            public MemorySaveSlot Store;
            public GameSession Game;
        }

        [TestCase(1), TestCase(2), TestCase(5), TestCase(10)]
        public void EightPlayers_SavingNTimesASecond_TickTimes(int perSecond)
        {
            var settings = new RoomServerSettings { MaxPlayers = Players, SharedPieces = new[] { RoomWallet.PieceName } };
            string saves = Path.Combine(_root, "saves");
            var guestsFolder = new GuestSaveFolder(GuestSaveFolder.FolderFor(saves, "room-a"), _definitions, _settings);
            using var rig = new RoomRig(new FileSaveStore(saves), settings: settings, timeRules: Program.RoomTimeRules(_definitions), guests: guestsFolder);
            var guests = new List<Guest>();
            for (int i = 0; i < Players; i++)
            {
                var client = rig.Join("p" + i);
                var g = new Guest { Client = client, Link = new RoomGuestLink(client, new SessionClientSettings().MaxMessageBytes) };
                rig.Run(0.3);
                Assert.That(g.Link.Ready, Is.True);
                g.Store = new MemorySaveSlot(RoomGuestLink.Slot, g.Link.Files.Writes);
                var link = g.Link;
                g.Store.Committed += c => link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
                g.Game = GameSession.Start(_definitions, _settings, g.Store, RoomGuestLink.Slot, "p" + i, shares: g.Link.Shares);
                // 알바 한 번: 큰 저장 묶음이 방에 가서 방에 그 사람의 세이브 전체가 생긴다(뒤의 검사는 그 전체로 게임을 시작해 본다).
                Assert.That(g.Game.BeginPartTimeWork(MoneyOnlyWork, "p" + i + ":w", 1).Applied, Is.True);
                Assert.That(g.Game.SettleMinigame("p" + i + ":w", true).Applied, Is.True);
                guests.Add(g);
            }
            rig.Run(1.0); // 처음 저장들을 다 받게
            Assert.That(guests.Sum(g => g.Link.Uploads.Count), Is.Zero, "준비한 저장을 방이 다 받았다");

            double tick = settings.TickSeconds;
            int ticks = (int)(Seconds / tick);
            int every = Math.Max(1, (int)Math.Round(1.0 / perSecond / tick));
            var times = new List<double>(ticks);
            var watch = new Stopwatch();
            int commands = 0;
            for (int t = 0; t < ticks; t++)
            {
                for (int i = 0; i < guests.Count; i++)
                {
                    if ((t + i) % every != 0) continue; // 사람마다 조금씩 어긋나게
                    guests[i].Game.TrackObjective("measure", "k" + t);
                    commands++;
                }

                rig.Clock.Now += tick;
                foreach (var c in guests) c.Client.Update(rig.Clock.Now); // 클라이언트 쪽 보내기(틱 시간에 넣지 않는다)
                watch.Restart();
                rig.Loop.RunDue();
                watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds);
            }

            for (int i = 0; i < 10; i++)
            {
                rig.Clock.Now += tick;
                foreach (var c in guests) c.Client.Update(rig.Clock.Now);
                rig.Loop.RunDue();
            }
            foreach (var c in guests) c.Client.Update(rig.Clock.Now);
            int pending = guests.Sum(g => g.Link.Uploads.Count);
            times.Sort();
            double mean = times.Average(), p95 = times[(int)(times.Count * 0.95)], max = times[times.Count - 1];
            int over = times.Count(x => x > tick * 1000.0);
            var b = new StringBuilder();
            b.AppendLine($"| 사람 | 초당 저장(1인) | 명령 수 | 틱 평균 ms | 틱 p95 ms | 틱 최대 ms | 틱 간격({tick * 1000:0.#}ms) 넘은 틱 | 끝에 남은 저장 |");
            b.AppendLine($"| {Players} | {perSecond} | {commands} | {mean:0.0} | {p95:0.0} | {max:0.0} | {over}/{times.Count} | {pending} |");
            TestContext.WriteLine(b.ToString());
            TestContext.Progress.WriteLine(b.ToString());
        }

        /// <summary>검사 한 번(GuestSaveFolder.Accept — 옛 세이브에 받은 것을 겹쳐 게임 한 판을 시작해 본다)이 얼마나 걸리는가: 작은 저장과 큰 저장.</summary>
        [Test]
        public void OneAccept_SmallAndFullSave_Times()
        {
            var folder = new GuestSaveFolder(Path.Combine(_root, "guests"), _definitions, _settings);
            var store = new MemorySaveSlot(RoomGuestLink.Slot);
            SaveFileSet last = null;
            store.Committed += c => last = SaveFileSet.From(c.Writes, c.Deletes);
            var game = GameSession.Start(_definitions, _settings, store, RoomGuestLink.Slot, "p0");
            Assert.That(game.BeginPartTimeWork(MoneyOnlyWork, "w", 1).Applied, Is.True); // 큰 저장 묶음
            Assert.That(game.SettleMinigame("w", true).Applied, Is.True);
            game.TrackObjective("measure", "k0");
            Assert.That(folder.Accept("p0", SaveFileSet.From(store.Files, null), out _), Is.Null, "첫 저장(전체)");

            var watch = Stopwatch.StartNew();
            const int Repeats = 20;
            for (int i = 0; i < Repeats; i++)
            {
                game.TrackObjective("measure", "k" + (i + 1));
                Assert.That(folder.Accept("p0", last, out _), Is.Null);
            }
            double small = watch.Elapsed.TotalMilliseconds / Repeats;

            watch.Restart();
            for (int i = 0; i < Repeats; i++) Assert.That(folder.Accept("p0", SaveFileSet.From(store.Files, null), out _), Is.Null);
            double full = watch.Elapsed.TotalMilliseconds / Repeats;
            long bytes = SaveFileSet.From(store.Files, null).ByteCount;
            string line = $"| 검사 한 번(작은 저장, 파일 {last.Writes.Count}개) | {small:0.0} ms |\n| 검사 한 번(전체 저장 {bytes / 1024} KB) | {full:0.0} ms |";
            TestContext.WriteLine(line);
            TestContext.Progress.WriteLine(line);
        }
    }
}
