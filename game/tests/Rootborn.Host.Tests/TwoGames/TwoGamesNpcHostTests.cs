using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Data.Save;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 같이 하기에서 NPC는 모두에게 똑같이 움직인다(NPC 이동 설계 4절): 자리는 방의 (날, 분)에서 각자 계산하고 NPC용 메시지는 없다.
    /// 방을 연 판(이 프로세스)과 들어간 판(자식 프로세스)을 번갈아 한 프레임씩 돌리며, 같은 프레임에 두 판의 모든 NPC 자리를 견준다.
    /// 다른 점은 분 아래 조각뿐이라(각자 메운다, 1분 안) 자리 차이는 2칸 이하여야 한다. 근거와 준비는 <see cref="TwoGamesHostTests"/> 머리말.
    /// </summary>
    public sealed partial class TwoGamesHostTests
    {
        private const float NpcToleranceCells = 2f;

        /// <summary>
        /// 둘 다 집 → 손님만 마을에 간 사이 방장이 집 NPC 곁에 가구를 놓는다(같이 쓰는 집) → 손님이 집으로 돌아온다 → 둘 다 마을.
        /// 같은 맵에 있는 동안은 모든 NPC 자리가 같아야 한다. 손님이 마을에 있는 동안 손님의 집 격자에는 새 가구가 아직 없다(가구 반영은 집에 들어설 때) —
        /// 그동안 집 안 NPC 자리는 달라도 되고(손님에게 보이지 않는다) 차이를 기록만 한다. 손님이 집에 들어서면 다시 같아야 한다.
        /// 준비: 이야기가 있는 슬롯 파일(양쪽), 방의 시계 파일(15:00)과 방 설정 파일(집을 같이 씀). 나머지는 입력(문 지나기, 배치 화면 포인터)이다.
        /// </summary>
        [Test]
        public void TWO_GAMES_NPC_001_같이_하는_두_화면에서_NPC가_같은_자리에_있다()
        {
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "손님", 202);
            // 방의 시계 파일: 1일째 15:00(오후 — 집의 엄마와 마을의 NPC 여럿이 어슬렁거리는 시간대). 방은 실제 시간으로 흐르고 하루가 실제 600초라 오후를 기다리지 않는다.
            // 이어 여는 방이라 같이 쓸 것은 방 설정 파일에 둔다(집).
            var store = new FileSaveStore(_hostSaves);
            store.Write("slot-1", SaveFileNames.RoomClock, new RoomClockSaveData { Version = RoomClockSaveData.KnownVersion, Day = 1, Minutes = 15 * 60 });
            store.Write("slot-1", SaveFileNames.RoomSettings, new RoomSettingsSaveData { Version = RoomSettingsSaveData.KnownVersion, SharedPieces = new[] { RoomHouse.PieceName }, MaxPlayers = 4 });
            _host = LaunchHost();
            HostReopensFromStartMenu();
            Assert.That(_host.Game.Hosting.SharedPieces, Is.EquivalentTo(new[] { RoomHouse.PieceName }), "이어 연 방은 저장된 설정(집을 같이 쓴다)");
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            Assert.That(_host.World.Map.Id, Is.EqualTo("House"));
            Assert.That(_guest.Query("map"), Is.EqualTo("House"));
            Assert.That(_guest.QueryBool("shares-house"), Is.True);
            float cell = _host.World.Map.Grid.CellSize;

            NpcComparison inHouse = CompareNpcs(cell, 60, null);
            Report("1. 둘 다 집", inHouse);
            inHouse.AssertSame();
            Assert.That(_host.Game.World.Npcs.Count, Is.GreaterThan(0), "이 시험의 전제: 집에 NPC가 있다");
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-npc-01-host-house"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-npc-01-guest-house"));

            // 손님은 마을로. 방장은 집 NPC 곁에 가구를 놓는다(손님의 집 격자에는 손님이 집에 들어설 때 반영된다).
            _guest.GoTo("Town");
            NpcPose houseNpc = _host.Game.World.Npcs[0].Pose;
            string furniture = PeerQueries.Ask(_host, "first-free-furniture", Array.Empty<string>());
            var placed = _host.PlaceFurniture(furniture, (houseNpc.X, houseNpc.Y));
            TestContext.WriteLine("[가구] 방장이 " + furniture + "를 칸 " + placed.X + "," + placed.Y + "에 놓음 (집 NPC " + _host.Game.World.Npcs[0].NpcId
                + " 자리 " + houseNpc.X + "," + houseNpc.Y + ")");
            int hostPlaced = _host.Game.Session.House.Placed.Count;
            Assert.That(_guest.UntilQuery(hostPlaced.ToString(CultureInfo.InvariantCulture), 900, "house-placed"), Is.True, "손님 세이브에도 놓였다");

            NpcComparison split = CompareNpcs(cell, 60, "Town");
            Report("2. 방장 집, 손님 마을(마을 NPC만 견줌)", split);
            split.AssertSame();
            TestContext.WriteLine("[2. 견주지 않은 집 NPC] 두 판의 자리 차이 최대 " + split.OtherMaxCells.ToString("0.00", CultureInfo.InvariantCulture) + "칸, 다른 맵 "
                + split.OtherMapDiffers + "번 — 손님의 집 격자에 새 가구가 아직 없을 수 있다(보이지 않는 맵)");
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-npc-02-guest-town"));

            _guest.GoTo("House");
            NpcComparison back = CompareNpcs(cell, 60, null);
            Report("3. 손님이 집으로 돌아옴", back);
            back.AssertSame();
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-npc-03-host-house-furniture"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-npc-03-guest-house-furniture"));

            _host.GoTo("Town");
            _guest.GoTo("Town");
            NpcComparison town = CompareNpcs(cell, 60, null);
            Report("4. 둘 다 마을", town);
            town.AssertSame();
            Assert.That(_host.Game.World.Npcs.Count, Is.GreaterThan(0), "이 시험의 전제: 마을에 NPC가 있다");
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-npc-04-host-town"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-npc-04-guest-town"));
            Assert.That(_host.CollisionViolations, Is.EqualTo(0));
            Assert.That(_guest.QueryInt("collision-violations"), Is.EqualTo(0));
        }

        /// <summary>방장: 시작 화면의 "방 열기" → 주소를 쳐서 연다(이어 여는 방 — 같이 쓸 것은 저장된 설정이라 고르지 않는다).</summary>
        private void HostReopensFromStartMenu()
        {
            Assert.That(_host.Screens.IsOpen(Rootborn.Screens.ScreenCatalog.SaveSlotSelect), Is.True, "실행하면 슬롯 선택부터");
            _host.PressButton(Rootborn.Screens.Start.SaveSlotScreen.HostId);
            _host.WaitForScreen(Rootborn.Screens.ScreenCatalog.HostRoom);
            _host.PressButton(Rootborn.Screens.Start.HostRoomScreen.AddressId);
            _host.TypeText(Loopback);
            _host.MovePointer(-50, -50);
            _host.Frames(2);
            _host.PressButton(Rootborn.Screens.Start.HostRoomScreen.OpenId);
            Assert.That(Until(_host, () => _host.Game.World != null && _host.Game.Hosting.IsOpen), Is.True, "방을 열고 들어간다");
        }

        private static void Report(string phase, NpcComparison c) =>
            TestContext.WriteLine("[" + phase + "] " + c.Frames + "프레임(" + c.Samples + "번 견줌), 견준 자리 " + c.Compared + "번, 차이 최대 "
                + c.MaxCells.ToString("0.00", CultureInfo.InvariantCulture) + "칸, 방장 화면에서 움직인 거리 합 "
                + c.MovedCells.ToString("0.0", CultureInfo.InvariantCulture) + "칸, 어긋남 " + c.Mismatches.Count + "번");

        /// <summary>
        /// 두 판을 같이 돌리며 EveryFrames 프레임마다 두 판의 모든 NPC 자리를 견준다. 견주는 NPC가 방장 화면에서 MinMovedCells칸 넘게 걷고
        /// samples번 넘게 견줄 때까지(최대 MaxFrames). 방의 시계는 실제 시간으로 흐르고 어슬렁거림의 쉼이 실제 2~6초라, 프레임 수가 아니라 걸은 거리로 끝낸다.
        /// mapOnly가 있으면 어느 한쪽이라도 그 맵에 있는 NPC만 견주고 나머지는 차이만 센다.
        /// </summary>
        private NpcComparison CompareNpcs(float cell, int samples, string mapOnly)
        {
            const int EveryFrames = 4, MaxFrames = 12000;
            const float MinMovedCells = 3f;
            var result = new NpcComparison();
            Dictionary<string, (string Map, float X, float Y)> last = null;
            for (int f = 0; f < MaxFrames && (result.Samples < samples || result.MovedCells <= MinMovedCells); f += EveryFrames)
            {
                _host.Frames(EveryFrames);
                result.Frames = f + EveryFrames;
                result.Samples++;
                Dictionary<string, (string Map, float X, float Y)> mine = ParsePoses(PeerQueries.Ask(_host, "npc-poses", Array.Empty<string>()));
                Dictionary<string, (string Map, float X, float Y)> theirs = ParsePoses(_guest.Query("npc-poses"));
                Assert.That(theirs.Keys, Is.EquivalentTo(mine.Keys), "두 판의 NPC 목록이 같다");
                foreach (var pair in mine)
                {
                    var a = pair.Value;
                    var b = theirs[pair.Key];
                    bool watched = mapOnly == null || a.Map == mapOnly || b.Map == mapOnly;
                    bool sameMap = a.Map == b.Map;
                    float cells = sameMap && a.Map != null ? MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) / cell : 0f;
                    if (!watched)
                    {
                        if (!sameMap) result.OtherMapDiffers++;
                        result.OtherMaxCells = Math.Max(result.OtherMaxCells, cells);
                        continue;
                    }
                    result.Compared++;
                    result.MaxCells = Math.Max(result.MaxCells, cells);
                    if (!sameMap || cells > NpcToleranceCells)
                        result.Mismatches.Add(f + "프레임 " + pair.Key + ": 방장 " + Describe(a) + " / 손님 " + Describe(b));
                    if (last != null && last.TryGetValue(pair.Key, out var before) && before.Map == a.Map && a.Map != null)
                        result.MovedCells += MathF.Sqrt((a.X - before.X) * (a.X - before.X) + (a.Y - before.Y) * (a.Y - before.Y)) / cell;
                }
                last = mine;
            }
            return result;
        }

        private static string Describe((string Map, float X, float Y) p) => p.Map == null ? "없음" : p.Map + " " + p.X.ToString("0.#", CultureInfo.InvariantCulture) + "," + p.Y.ToString("0.#", CultureInfo.InvariantCulture);

        private static Dictionary<string, (string Map, float X, float Y)> ParsePoses(string text)
        {
            var poses = new Dictionary<string, (string, float, float)>(StringComparer.Ordinal);
            foreach (string part in text.Split(';'))
            {
                int eq = part.IndexOf('=');
                string id = part.Substring(0, eq), value = part.Substring(eq + 1);
                if (value == "-") { poses[id] = (null, 0f, 0f); continue; }
                string[] v = value.Split(',');
                poses[id] = (v[0], float.Parse(v[1], CultureInfo.InvariantCulture), float.Parse(v[2], CultureInfo.InvariantCulture));
            }
            return poses;
        }

        private sealed class NpcComparison
        {
            public readonly List<string> Mismatches = new List<string>();
            public int Frames, Samples, Compared, OtherMapDiffers;
            public float MaxCells, OtherMaxCells, MovedCells;

            public void AssertSame()
            {
                Assert.That(Compared, Is.GreaterThan(0), "견줄 NPC가 있다");
                Assert.That(MovedCells, Is.GreaterThan(3f), "이 시험의 전제: 견주는 동안 NPC가 걸었다");
                Assert.That(Mismatches, Is.Empty, "같은 프레임의 두 판에서 NPC 자리가 다르다(" + Mismatches.Count + "번):\n"
                    + string.Join("\n", Mismatches.GetRange(0, Math.Min(20, Mismatches.Count))));
            }
        }
    }
}
