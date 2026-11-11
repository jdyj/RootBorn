using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Host.World;
using Rootborn.World;
using Rootborn.Screens;
using Rootborn.Screens.Minigames;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 묶음 5(미니게임)를 실제 창과 게임 루프로: 새 슬롯 → 집 안 시험용 연습 지점(career-practice)까지 걷기 → E → 미니게임이 열림 →
    /// 순서 외우기는 포인터로, 박자는 Space 키로 끝까지 → 정산(예약이 지워지고 활동 기록이 남음) → Esc로 닫으면 다시 걸을 수 있음.
    /// 지금 맵에는 걸어 닿을 알바·연습 지점이 없어서(interactions.json 머리말) 시험용 상호작용 파일에 지점 하나를 더한다.
    /// 조작은 방향 키·E·Space·Esc·포인터뿐(장치 이벤트 주입, OS 입력 계층 미확인). 순간이동·내부 호출·상태 강제 설정 없음.
    /// 화면 값(보여 주는 순서, 박자 시각)은 플레이어가 화면에서 보는 것을 읽을 뿐 바꾸지 않는다.
    /// 스크린샷: output/host-screens-evidence/minigame-*.png. 한 판 동안 프레임 시간을 잰다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class MinigameHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-minigame-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        /// <summary>시험용 상호작용 파일: 실제 파일의 집(House)에 지점 하나를 더한다.</summary>
        private string InteractionsWithHousePoint(string kind, string id, float x, float y)
        {
            string text = File.ReadAllText(Path.Combine(_root, InteractionData.DefaultRelativePath));
            const string houseThings = "\"things\": [";
            int house = text.IndexOf("\"House\"", StringComparison.Ordinal);
            int things = text.IndexOf(houseThings, house, StringComparison.Ordinal);
            string point = "\n        { \"kind\": \"" + kind + "\", \"id\": \"" + id + "\", \"at\": [ " + x.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ", " + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ], \"priority\": 45 },";
            text = text.Insert(things + houseThings.Length, point);
            string path = Path.Combine(_saves, "interactions-with-" + kind + ".json");
            File.WriteAllText(path, text);
            return path;
        }

        private PlayerDriver Start(params string[] extra)
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            }.Concat(extra).ToArray());
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        [Test]
        public void MINIGAME_001_WalkToAPracticePoint_E_SequenceByPointer_TimingBySpace_Settles()
        {
            const float px = 624f, py = 560f;
            using PlayerDriver player = Start("--interactions", InteractionsWithHousePoint("career-practice", "practice.chef", px, py));
            player.WalkNear(px, py, 40f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo("career-practice"));
            int energy = player.Game.Session.Student.Energy;

            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Minigame);
            var screen = (MinigameScreen)player.Screens.Find(ScreenCatalog.Minigame);
            Assert.That(player.Screens.MovementBlocked, Is.True, "미니게임은 이동을 막는다");
            Assert.That(player.Game.Session.Student.HasPendingMinigame, Is.True, "E로 연습을 예약했다");
            Assert.That(player.Game.Session.Student.Energy, Is.LessThan(energy), "예약할 때 비용을 쓴다");
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Minigame, "minigame-01-sequence-preview");

            var frameMs = new List<double>(600);
            var clock = new Stopwatch();
            void Frame() { clock.Restart(); player.Frames(1); frameMs.Add(clock.Elapsed.TotalMilliseconds); }

            // 순서 외우기: 보여 주는 동안 화면의 순서를 보고, 끝나면 그 순서로 단추를 포인터로 누른다.
            ChallengeSession sequence = screen.Session.Current;
            Assert.That(sequence.Mechanic, Is.EqualTo(MinigameMechanic.Sequence));
            int[] order = sequence.PreviewSequence;
            while (sequence.PreviewVisible) Frame();
            player.Screenshot("minigame-02-sequence-answer");
            foreach (int answer in order) player.PressButton(SymbolChoiceView.ChoiceId(answer));

            // 박자: 박자 시각이 오면 Space.
            player.WaitForScreen(ScreenCatalog.Minigame);
            ChallengeSession timing = screen.Session.Current;
            Assert.That(timing.Mechanic, Is.EqualTo(MinigameMechanic.Timing), "두 번째 단계");
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Minigame, "minigame-03-timing");
            foreach (float beat in timing.BeatTimes)
            {
                while (timing.Elapsed < beat - 0.02f) Frame();
                player.PressKey(TestKey.Space);
            }
            for (int i = 0; i < 180 && !screen.Session.IsComplete; i++) Frame();

            Assert.That(screen.Session.IsComplete, Is.True);
            Assert.That(screen.Session.Succeeded, Is.True, "점수 " + screen.Session.Score);
            Assert.That(screen.Settlements, Is.EqualTo(1));
            Assert.That(player.Game.Session.Student.HasPendingMinigame, Is.False, "정산되어 예약이 지워졌다");
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Minigame, "minigame-04-result");

            player.PressKey(TestKey.Escape);
            player.WaitForScreenClosed(ScreenCatalog.Minigame);
            Assert.That(screen.Settlements, Is.EqualTo(1), "닫아도 다시 정산하지 않는다");
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.WalkAway(2);
            Assert.That(Math.Abs(player.World.Walker.X - x0) + Math.Abs(player.World.Walker.Y - y0), Is.GreaterThan(0f), "닫으면 다시 걷는다");

            frameMs.Sort();
            TestContext.Progress.WriteLine($"[MINIGAME_001] 프레임 {frameMs.Count}개: 중앙 {frameMs[frameMs.Count / 2]:0.00}ms, 95% {frameMs[(int)(frameMs.Count * 0.95)]:0.00}ms, 최대 {frameMs[frameMs.Count - 1]:0.00}ms");
            Assert.That(frameMs[frameMs.Count / 2], Is.LessThan(50.0), "한 판 동안 프레임 중앙값");
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, screen.TextKeys);
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty);
        }
    }
}
