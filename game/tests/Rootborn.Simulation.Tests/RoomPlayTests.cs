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
            if (seed != 901) return; // 지킴 검사는 곧은 씨앗에만(보조 씨앗은 불변식만)
            Guard(result, WalletBalance, "방 지갑 잔액을 알려진 지출과 견줬다");
            Guard(result, ForwardAfterDay, "쉰 날 뒤 날이 바뀐 길에서 퀘스트·체인을 앞으로만 견줬다");
            Guard(result, ForwardAfterPending, "걸린 요청이 있던 다시 들어옴에서 퀘스트·체인을 앞으로만 견줬다");
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

        /// <summary>
        /// 꾸미는 사람이 있는 방(같이 쓰는 집의 꾸밈): 집을 같이 쓰면 꾸밈 조각이 따라 켜진다. 꾸미는 사람 둘과 메인 체인 하나가 집에서 벽지·바닥·띠를 고른다.
        /// 돈을 나눠 쓰는 방(build-wallet)과 각자 쓰는 방(build). 새 이름이라 앞의 조합의 씨앗과 기록은 그대로다.
        /// </summary>
        public static readonly (string Name, string[] Pieces)[] BuildCombos =
        {
            ("build", new[] { RoomHouse.PieceName }),
            ("build-wallet", new[] { RoomWallet.PieceName, RoomHouse.PieceName }),
        };

        public static RoomPlaySettings BuildSettings(string name, string[] pieces, int seed) => new RoomPlaySettings
        {
            Name = name,
            Seed = seed,
            SharedPieces = pieces,
            Players = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_PLAYERS", 3),
            Days = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_DAYS", 3),
            StepsPerDay = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_STEPS_PER_DAY", 60),
            Policies = new[] { RoomPolicies.Decorator, RoomPolicies.Decorator, PlayPolicyCatalog.MainChain },
        };

        public static IEnumerable<TestCaseData> BuildRooms()
        {
            int seeds = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_SEEDS", 1);
            for (int c = 0; c < BuildCombos.Length; c++)
                for (int s = 1; s <= seeds; s++)
                {
                    int seed = 500 + c * 100 + s;
                    yield return new TestCaseData(BuildCombos[c].Name, BuildCombos[c].Pieces, seed).SetName("Room_" + BuildCombos[c].Name + "_seed" + seed);
                }
        }

        [TestCaseSource(nameof(BuildRooms))]
        public void Decorators_BuildTheSharedHouse_EveryoneSeesTheSame_AndTheMoneyAddsUp(string name, string[] pieces, int seed)
        {
            RoomPlaySettings settings = BuildSettings(name + "-" + seed, pieces, seed);
            RoomPlayResult result = Play(settings);
            PlayFailure failure = result.FirstFailure;
            Assert.That(failure, Is.Null, failure?.Message + "\n기록: " + result.OutputDir);
            Assert.That(result.ServerDays, Is.GreaterThan(settings.Days), "방이 날을 넘기며 플레이했다");
            foreach (PlayResult p in result.Players) Assert.That(p.DaysCompleted, Is.GreaterThanOrEqualTo(settings.Days), p.Name + " 날을 넘겼다");
            TestContext.WriteLine(BuildLine(result));
            Assert.That(result.CountOf("꾸밈 잡기 요청"), Is.GreaterThan(0), "꾸미려 했다(정책이 꾸미기 분류까지 갔다)");
            Assert.That(result.CountOf("꾸밈 쓰기가 방에 적용됨(돈 낸 것)"), Is.GreaterThan(0), "돈을 내고 꾸밈이 방에 적용됐다(지킴 검사 — 0이면 이 시드는 꾸밈을 지나지 않았다)");
            Assert.That(result.CountOf("꾸밈 비추기 비교"), Is.GreaterThan(0), "모두가 보는 꾸밈을 방의 기록과 견줬다");
            Assert.That(result.CountOf("끝난 뒤 가만히: 꾸밈 잡기 0, 돈 낸 표시 0"), Is.EqualTo(1), "끝난 뒤 모두 다시 들어와 가만히 있을 때 잡기·표시가 0이었다");
            if (seed % 100 != 1) return; // 지킴 검사는 곧은 씨앗에만
            GuardMoney(result, pieces.Contains(RoomWallet.PieceName));
        }

        /// <summary>한 줄 요약: 요청·잡힘·거절·쓰기·풀기·끊김 종류·동시 요청.</summary>
        public static string BuildLine(RoomPlayResult r) => RoomRecord.BuildLine(r);

        /// <summary>끊김·지연·순서 바뀜·손실·쉰 날 + 꾸미는 사람. 잡기 허락 뒤 끊김, 돈 낸 직후 끊김, 다시 들어옴에서도 돈과 꾸밈이 어긋나지 않는다.</summary>
        public static RoomPlaySettings BuildFaults(int seed, bool sharedWallet) => new RoomPlaySettings
        {
            Name = "build-faults-" + seed,
            Seed = seed,
            SharedPieces = sharedWallet ? new[] { RoomWallet.PieceName, RoomWorldFlags.PieceName, RoomHouse.PieceName } : new[] { RoomWorldFlags.PieceName, RoomHouse.PieceName },
            Players = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_PLAYERS", 3),
            Days = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_DAYS", 2),
            StepsPerDay = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_STEPS_PER_DAY", 60),
            Policies = new[] { RoomPolicies.Decorator, RoomPolicies.Decorator, PlayPolicyCatalog.MainChain },
            DisconnectChance = 0.08,
            LatencySeconds = 0.25,
            JitterSeconds = 0.1,
            LossRate = 0.1,
            OneSitsOutADay = true,
        };

        /// <summary>
        /// 꾸미는 도중 끊김이 요청 직후·허락 뒤·돈 낸 뒤 셋 다 지나가는 것으로 골라 둔 씨앗(각자 쓰는 지갑 951·954, 같이 쓰는 지갑 967·968). 셋 다 지나갔는지 세어 0이면 실패한다 —
        /// 코드가 바뀌어 이 씨앗이 그 경우를 더 지나지 않게 되면 다른 씨앗을 고른다. 그 밖의 씨앗(ROOTBORN_SIM_ROOM_SEEDS=N이면 N-1개 더, 971부터)은 지나간 횟수를 따지지 않고 불변식만 본다.
        /// </summary>
        public static readonly (int Seed, bool Wallet)[] CuratedBuildFaults = { (951, false), (954, false), (967, true), (968, true) };

        public static IEnumerable<TestCaseData> BuildFaultRooms()
        {
            foreach (var c in CuratedBuildFaults)
                yield return new TestCaseData(c.Seed, c.Wallet, true).SetName("Room_buildfaults" + (c.Wallet ? "-wallet" : string.Empty) + "_seed" + c.Seed);
            int extra = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_SEEDS", 1) - 1;
            for (int s = 0; s < extra; s++)
            {
                yield return new TestCaseData(971 + s, false, false).SetName("Room_buildfaults_seed" + (971 + s));
                yield return new TestCaseData(981 + s, true, false).SetName("Room_buildfaults-wallet_seed" + (981 + s));
            }
        }

        [TestCaseSource(nameof(BuildFaultRooms))]
        public void Decorating_WithDisconnectsALaggyLineAndARestDay_NeverLosesMoneyOrLeavesACellHeld(int seed, bool sharedWallet, bool curated)
        {
            RoomPlaySettings settings = BuildFaults(seed, sharedWallet);
            RoomPlayResult result = Play(settings);
            PlayFailure failure = result.FirstFailure;
            Assert.That(failure, Is.Null, failure?.Message + "\n기록: " + result.OutputDir);
            Assert.That(result.ServerDays, Is.GreaterThan(settings.Days), "방이 날을 넘기며 플레이했다");
            TestContext.WriteLine(BuildLine(result));
            if (!curated) { if (result.CountOf("꾸밈 잡기 요청") > 0) Assert.That(result.CountOf("끝난 뒤 가만히: 꾸밈 잡기 0, 돈 낸 표시 0"), Is.EqualTo(1)); return; }
            Assert.That(result.CountOf("꾸밈 잡기 요청"), Is.GreaterThan(0), "꾸미려 했다");
            Assert.That(result.CountOf("꾸밈 쓰기가 방에 적용됨(돈 낸 것)"), Is.GreaterThan(0), "돈을 내고 꾸밈이 방에 적용됐다");
            Assert.That(result.CountOf("끝난 뒤 가만히: 꾸밈 잡기 0, 돈 낸 표시 0"), Is.EqualTo(1), "끝난 뒤 가만히 있을 때 잡기·표시가 0이었다");
            Assert.That(result.CountOf("끊김: 꾸밈 잡기 요청 직후"), Is.GreaterThan(0), "꾸밈 요청 직후에 끊겼다 — 0이면 이 씨앗은 그 경우를 지나지 않았다");
            Assert.That(result.CountOf("끊김: 꾸밈 잡기 허락 뒤(돈 내기 전)"), Is.GreaterThan(0), "방이 칸을 잡아 준 뒤 돈을 내기 전에 끊겼다 — 0이면 이 씨앗은 그 경우를 지나지 않았다");
            Assert.That(result.CountOf("끊김: 꾸밈 돈 낸 뒤 쓰기가 방에 닿기 전"), Is.GreaterThan(0), "돈을 낸 직후(쓰기가 방에 닿기 전)에 끊겼다 — 0이면 이 씨앗은 그 경우를 지나지 않았다");
            Assert.That(result.CountOf("다시 들어옴: 풀리지 않은 꾸밈 잡기가 있었다(돈 내기 전)") + result.CountOf("다시 들어옴: 풀리지 않은 꾸밈 잡기가 있었다(돈 낸 것)"), Is.GreaterThan(0), "끊긴 뒤 다시 들어왔을 때 풀리지 않은 잡기가 있었다");
            GuardMoney(result, sharedWallet);
            Guard(result, ForwardAfterPending, "꾸밈 요청이 걸린 채 끊겼다 다시 들어와 퀘스트·체인을 앞으로만 견줬다");
            if (CuratedSkillGrowth.Contains(seed)) Guard(result, SkillGrown, "걸린 꾸밈 요청으로 인테리어 기술이 자란 것을 허용량과 견줬다");
        }

        /// <summary>부딪히는 꾸미기: 꾸미는 사람 넷이 같은 집·같은 돈을 쓰며 같은 탭·같은 카드·같은 칸을 노린다(지연이 있어 요청이 겹친다). 먼저 온 잡기만 되고 나머지는 거절된다.</summary>
        [Test]
        public void FourDecorators_ContendForTheSameCells_OnlyTheFirstHoldWins_AndNoCellStaysHeld()
        {
            var settings = new RoomPlaySettings
            {
                Name = "build-contention", Seed = 41, Players = 4,
                SharedPieces = new[] { RoomWallet.PieceName, RoomHouse.PieceName },
                Policies = new[] { RoomPolicies.Decorator },
                Days = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_DAYS", 2),
                StepsPerDay = PlaySettings.EnvInt("ROOTBORN_SIM_ROOM_STEPS_PER_DAY", 60),
                LatencySeconds = 0.3, JitterSeconds = 0.1,
            };
            RoomPlayResult result = Play(settings);
            Assert.That(result.FirstFailure, Is.Null, result.FirstFailure?.Message + "\n기록: " + result.OutputDir);
            TestContext.WriteLine(BuildLine(result));
            Assert.That(result.CountOf("꾸밈 잡기 요청"), Is.GreaterThanOrEqualTo(4), "꾸미려 했다");
            Assert.That(result.CountOf("꾸밈 동시 요청: 두 사람 이상이 한꺼번에 잡기를 가짐"), Is.GreaterThan(0), "두 사람 이상이 같은 방을 동시에 꾸미려 했다");
            GuardMoney(result, sharedWallet: true);
            Assert.That(result.CountOf("꾸밈 쓰기가 방에 적용됨(돈 낸 것)"), Is.GreaterThan(0), "먼저 온 잡기는 돈을 내고 적용됐다");
            Assert.That(result.CountOf("끝난 뒤 가만히: 꾸밈 잡기 0, 돈 낸 표시 0"), Is.EqualTo(1));
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
            if (seed % 100 == 1 && pieces.Contains(RoomWallet.PieceName)) Guard(result, WalletBalance, "방 지갑 잔액을 알려진 지출과 견줬다(곧은 씨앗)");
        }

        // ---------- 지킴 검사: 새 불변식이 그 시드에서 실제로 견준 횟수가 0이면 실패(검사가 조용히 비지 않게). 보조 씨앗에는 걸지 않는다. ----------

        public const string SharedWalletMoney = "꾸밈 돈 보존: 방 지갑이 잡은 돈 = 방의 기록으로 다시 계산한 비용";
        public const string OwnWalletMoney = "꾸밈 돈 보존: 지갑 줄어듦 = 방의 기록으로 다시 계산한 비용";
        public const string WalletBalance = "방 지갑 잔액 = 넣기 − 잡기 + 풀기(꾸밈은 방의 기록으로 다시 계산)";
        public const string ForwardAfterDay = "다시 들어와 비교: 날이 바뀌어 진행은 앞으로만";
        public const string ForwardAfterPending = "다시 들어와 비교: 걸린 요청이 있어 진행은 앞으로만";
        public const string SkillGrown = "다시 들어와 비교: 걸린 요청으로 기술이 실제로 자람(허용량과 견줌)";

        /// <summary>걸린 꾸밈 요청으로 기술이 실제로 자라는 곧은 씨앗(지난 것을 세어 골랐다 — 바뀌면 다른 씨앗을 고른다).</summary>
        public static readonly int[] CuratedSkillGrowth = { 951, 968 };

        private static void Guard(RoomPlayResult r, string count, string what)
            => Assert.That(r.CountOf(count), Is.GreaterThan(0), what + " — 0이면 이 씨앗은 그 검사를 지나지 않았다(" + count + ")");

        private static void GuardMoney(RoomPlayResult r, bool sharedWallet)
        {
            if (sharedWallet)
            {
                Guard(r, SharedWalletMoney, "같이 쓰는 지갑에서 꾸밈 돈을 방의 기록으로 다시 계산한 비용과 견줬다");
                Guard(r, WalletBalance, "방 지갑 잔액을 알려진 지출과 견줬다");
            }
            else Guard(r, OwnWalletMoney, "각자 쓰는 지갑에서 꾸밈 돈을 방의 기록으로 다시 계산한 비용과 견줬다");
        }
    }
}
