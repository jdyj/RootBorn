using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Room;
using Rootborn.Simulation.Rooms;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 자동 플레이어 여럿이 한 방(같은 프로세스 안 전송 + Rootborn.Server와 같은 방)에서 대본 없이 여러 날 플레이한다.
    /// 방 설정의 조합(시간만, 돈, 마을, 집, 전부)마다 방의 불변식(RoomChecks)과 자리마다의 불변식(PlayRunner)을 본다. 날마다 서버를 끄고 켠다.
    /// 길게: ROOTBORN_SIM_ROOM_DAYS(기본 2), ROOTBORN_SIM_ROOM_SEEDS(기본 1), ROOTBORN_SIM_ROOM_PLAYERS(기본 3), ROOTBORN_SIM_ROOM_STEPS_PER_DAY(기본 30).
    /// 기록은 output/simulation/room-&lt;이름&gt;/. 화면·입력 근거가 아니다.
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class RoomPlayTests
    {
        public static readonly (string Name, string[] Pieces)[] Combos =
        {
            ("time-only", Array.Empty<string>()),
            ("wallet", new[] { RoomWallet.PieceName }),
            ("town", new[] { RoomWorldFlags.PieceName }),
            ("house", new[] { RoomHouse.PieceName }),
            ("all", new[] { RoomWallet.PieceName, RoomWorldFlags.PieceName, RoomHouse.PieceName }),
        };

        public static RoomPlaySettings Settings(string name, string[] pieces, int seed) => new RoomPlaySettings
        {
            Name = name,
            Seed = seed,
            SharedPieces = pieces,
            Players = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_PLAYERS", 3),
            Days = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_DAYS", 2),
            StepsPerDay = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_STEPS_PER_DAY", 30),
        };

        public static RoomPlayResult Play(RoomPlaySettings settings)
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            RoomPlayResult result;
            using (var room = new RoomPlay(atlas, settings)) result = room.Run();
            RoomRecord.Write(result, atlas);
            TestContext.WriteLine(RoomRecord.Summary(result));
            return result;
        }

        public static IEnumerable<TestCaseData> Rooms()
        {
            int seeds = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_SEEDS", 1);
            for (int c = 0; c < Combos.Length; c++)
                for (int s = 1; s <= seeds; s++)
                {
                    int seed = c * 100 + s; // 조합마다 다른 씨앗 — 정책 섞기와 걸음 순서가 달라진다
                    yield return new TestCaseData(Combos[c].Name, Combos[c].Pieces, seed).SetName("Room_" + Combos[c].Name + "_seed" + seed);
                }
        }

        /// <summary>끊김(요청 직후·허락 직전·저장 도중·아무 때), 지연·흔들림·비신뢰 손실, 1일째 낮에 한 사람이 나갔다 다음 날 들어오기를 섞은 방.</summary>
        public static RoomPlaySettings Faults(int seed) => new RoomPlaySettings
        {
            Name = "faults-" + seed,
            Seed = seed,
            SharedPieces = new[] { RoomWallet.PieceName, RoomWorldFlags.PieceName, RoomHouse.PieceName },
            Players = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_PLAYERS", 3),
            Days = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_DAYS", 2),
            StepsPerDay = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_STEPS_PER_DAY", 60),
            Policies = new[] { RoomPolicies.Sharer, RoomPolicies.Sharer, PlayPolicyCatalog.MainChain }, // 요청이 많이 생기게(상자·가구·지갑)
            DisconnectChance = 0.08,
            LatencySeconds = 0.25, // 걸음 사이에 요청·답이 걸려 있게(클릭 한 번은 5프레임)
            JitterSeconds = 0.1,
            LossRate = 0.1,
            OneSitsOutADay = true,
        };

        public static IEnumerable<TestCaseData> FaultRooms()
        {
            int seeds = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_SEEDS", 1);
            for (int s = 1; s <= seeds; s++) yield return new TestCaseData(900 + s).SetName("Room_faults_seed" + (900 + s));
        }

        [TestCaseSource(nameof(FaultRooms))]
        public void Disconnects_ALaggyLine_AndARestDay_KeepEveryInvariant(int seed)
        {
            RoomPlaySettings settings = Faults(seed);
            RoomPlayResult result = Play(settings);
            PlayFailure failure = result.FirstFailure;
            Assert.That(failure, Is.Null, failure?.Message + "\n기록: " + result.OutputDir);
            Assert.That(result.ServerDays, Is.GreaterThan(settings.Days), "방이 날을 넘기며 플레이했다");
            Assert.That(result.Counts.Keys.Count(k => k.StartsWith("끊김", StringComparison.Ordinal)), Is.GreaterThan(0), "끊김을 넣었다");
            Assert.That(result.CountOf("나감(쉰 날)"), Is.EqualTo(1), "한 사람이 하루 나갔다");
            Assert.That(result.CountOf("다시 들어와 비교: 쉰 날"), Is.EqualTo(1), "쉰 날 뒤 다시 들어와 진행을 비교했다");
        }

        /// <summary>
        /// 부딪히는 방: 나누는 사람 넷이 같은 집·지갑을 쓰며 같은 칸에 가구를 놓고 같은 상자에 넣고 꺼낸다(지연이 있어 요청이 겹친다).
        /// 먼저 온 요청만 되고 나머지는 거절돼도 불변식이 그대로인지 본다.
        /// </summary>
        [Test]
        public void FourSharers_ContendForTheSameCellsAndChest_OnlyTheFirstRequestWins()
        {
            var settings = new RoomPlaySettings
            {
                Name = "contention", Seed = 31, Players = 4,
                SharedPieces = new[] { RoomWallet.PieceName, RoomHouse.PieceName },
                Policies = new[] { RoomPolicies.Sharer },
                Days = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_DAYS", 2),
                StepsPerDay = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_STEPS_PER_DAY", 60),
                LatencySeconds = 0.3, JitterSeconds = 0.1,
            };
            RoomPlayResult result = Play(settings);
            Assert.That(result.FirstFailure, Is.Null, result.FirstFailure?.Message + "\n기록: " + result.OutputDir);
            Assert.That(result.CountOf("집 요청"), Is.GreaterThanOrEqualTo(4), "가구를 놓으려 했다");
            TestContext.WriteLine("먼저 온 것만 됨: 집 " + result.CountOf("먼저 온 것만 됨: 집") + ", 상자 " + result.CountOf("먼저 온 것만 됨: 상자") + ", 지갑 " + result.CountOf("먼저 온 것만 됨: 지갑"));
        }

        [Test]
        public void SameSeed_PlaysTheSameRoom_AndTheRecordReplaysToTheSameState()
        {
            RoomPlaySettings Short(string name) => new RoomPlaySettings
            {
                Name = name, Seed = 7, SharedPieces = new[] { RoomWallet.PieceName, RoomHouse.PieceName }, Players = 2, Days = 1, StepsPerDay = 25,
                DisconnectChance = 0.1, LatencySeconds = 0.1, JitterSeconds = 0.05, LossRate = 0.1,
            };
            RoomPlayResult a = Play(Short("determinism-a"));
            RoomPlayResult b = Play(Short("determinism-b"));
            Assert.That(a.FirstFailure, Is.Null, a.FirstFailure?.Message);
            Assert.That(a.Events.Count, Is.GreaterThan(40), "판다운 판");
            Assert.That(b.Events.Select(e => e.Line), Is.EqualTo(a.Events.Select(e => e.Line)), "같은 씨앗 → 같은 일(걸음 순서, 끊김, 서버 끄고 켜기)");
            for (int i = 0; i < a.Players.Count; i++)
            {
                Assert.That(b.Players[i].Keys.ToArray(), Is.EqualTo(a.Players[i].Keys.ToArray()), "같은 씨앗 → 같은 행동 " + i);
                Assert.That(b.Players[i].FinalState, Is.EqualTo(a.Players[i].FinalState), "같은 끝 상태 " + i);
            }
            Assert.That(b.Frames, Is.EqualTo(a.Frames));

            RoomPlayResult replayed = RoomRecord.Replay(FastPlayTests.Atlas, a.OutputDir, "determinism-replay");
            RoomRecord.Write(replayed, FastPlayTests.Atlas);
            Assert.That(replayed.FirstFailure, Is.Null, replayed.FirstFailure?.Message);
            Assert.That(replayed.Events.Select(e => e.Line), Is.EqualTo(a.Events.Select(e => e.Line)), "기록의 일을 그대로 다시 했다");
            for (int i = 0; i < a.Players.Count; i++)
                Assert.That(replayed.Players[i].FinalState, Is.EqualTo(a.Players[i].FinalState), "다시 한 판의 끝 상태가 같다 " + i);
        }

        /// <summary>실패한 방 다시 보기: ROOTBORN_SIM_ROOM_REPLAY=output/simulation/room-&lt;이름&gt;(폴더). 평소에는 돌지 않는다.</summary>
        [Test, Explicit("ROOTBORN_SIM_ROOM_REPLAY에 기록 폴더를 주고 손으로 돌린다")]
        public void ReplayRoomFromEnvironment()
        {
            string dir = Environment.GetEnvironmentVariable("ROOTBORN_SIM_ROOM_REPLAY");
            Assert.That(dir, Is.Not.Null.And.Not.Empty, "ROOTBORN_SIM_ROOM_REPLAY에 방 기록 폴더를 준다");
            RoomPlayResult r = RoomRecord.Replay(FastPlayTests.Atlas, dir, "replay-" + System.IO.Path.GetFileName(System.IO.Path.GetFullPath(dir).TrimEnd('/', '\\')));
            RoomRecord.Write(r, FastPlayTests.Atlas);
            TestContext.WriteLine(RoomRecord.Summary(r));
            if (r.FirstFailure != null) Assert.Fail(r.FirstFailure.Message);
        }

        [TestCaseSource(nameof(Rooms))]
        public void SeveralPlayers_PlaySeveralDaysInOneRoom_KeepingEveryInvariant(string name, string[] pieces, int seed)
        {
            RoomPlaySettings settings = Settings(name + "-" + seed, pieces, seed);
            RoomPlayResult result = Play(settings);
            PlayFailure failure = result.FirstFailure;
            Assert.That(failure, Is.Null, failure?.Message + "\n기록: " + result.OutputDir);
            Assert.That(result.ServerDays, Is.GreaterThan(settings.Days), "방이 날을 넘기며 플레이했다");
            Assert.That(result.Restarts, Is.GreaterThanOrEqualTo(settings.Days), "날마다 서버를 끄고 켰다");
            foreach (PlayResult p in result.Players) Assert.That(p.DaysCompleted, Is.GreaterThanOrEqualTo(settings.Days), p.Name + " 날을 넘겼다");
        }
    }
}
