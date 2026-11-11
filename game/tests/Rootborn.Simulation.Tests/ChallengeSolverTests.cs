using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;
using Rootborn.Screens;
using Rootborn.Screens.Minigames;
using Rootborn.Simulation.Headless;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 미니게임 도전 종류마다 풀이가 있고, 풀이가 화면에 보이는 것만 보고 실제 알바 미니게임을 푼다.
    /// 판 예약만 준비 단계로 GameSession 명령을 부르고(알바 지점까지 걷는 것은 시뮬레이션 판이 본다), 판은 화면 포인터 판정과 키 배정을 거친 입력으로 한다.
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class ChallengeSolverTests
    {
        [Test]
        public void EveryChallengeKind_TheScreenCanDraw_HasASolver()
        {
            ChallengeViewCatalog views = ChallengeViewCatalog.Default();
            foreach (MinigameMechanic m in Enum.GetValues(typeof(MinigameMechanic)))
            {
                Assert.That(views.Create(m), Is.Not.Null, "화면이 그리지 못하는 도전 종류: " + m);
                Assert.That(ChallengeSolvers.Has(m), Is.True, "풀이가 없는 도전 종류: " + m + " — ChallengeSolvers.Register에 한 줄 더한다");
            }
        }

        /// <summary>알바 미니게임마다(실제 데이터). 고르기가 든 판은 답을 볼 수 없어 무작위로 고르므로 끝까지 했는지만 본다.</summary>
        public static IEnumerable<TestCaseData> Works()
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            foreach (var work in atlas.Definitions.All<IMinigamePartTimeWork>().OrderBy(w => w.Id, StringComparer.Ordinal))
                if (work.Minigame != null) yield return new TestCaseData(work.Id).SetName("Solves_" + work.Id);
        }

        [TestCaseSource(nameof(Works))]
        public void Solver_PlaysThePartTimeWorkMinigame_ToTheEnd_AndClosesIt(string workId)
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new HeadlessBody(atlas);
            var observer = new PlayObserver(atlas);
            var memory = new PlayMemory();
            for (int i = 0; i < 40 && !body.InWorld; i++) Do(body, StartRoutine.Choose(observer.Observe(body), memory));
            Assert.That(body.InWorld, Is.True, "새 게임으로 월드에 들어섰다");

            // 준비: 그 알바의 판을 예약한다(예약이 생기면 화면이 열린다 — MinigameTrigger).
            var begun = body.Session.BeginPartTimeWork(workId, "solver-test:" + workId, 7);
            Assert.That(begun.Applied, Is.True, "예약: " + begun.Kind);
            body.Wait(1);
            Assert.That(body.Screens.IsOpen(ScreenCatalog.Minigame), Is.True, "미니게임 화면이 열렸다");
            var screen = (MinigameScreen)body.Screens.Find(ScreenCatalog.Minigame);
            IMinigamePartTimeWork work = atlas.Definitions.Get<IMinigamePartTimeWork>(workId);
            bool guesses = work.Minigame.Challenges.Any(c => c is ChoiceChallenge);

            var player = new MinigamePlayer();
            var random = new Random(3);
            var kinds = new HashSet<MinigameMechanic>();
            int steps = 0;
            for (; steps < 600 && body.Screens.IsOpen(ScreenCatalog.Minigame); steps++)
            {
                PlayView view = observer.Observe(body);
                if (view.Minigame != null) kinds.Add(view.Minigame.Mechanic);
                PlayAction a = player.Choose(view, random);
                Assert.That(a, Is.Not.Null, "미니게임이 떠 있는데 고를 것이 없다");
                Do(body, a);
            }
            MinigameSession played = screen.Session;
            TestContext.WriteLine(workId + ": 도전 " + string.Join(",", kinds) + ", 점수 " + played.Score + ", 성공 " + played.Succeeded + ", 걸음 " + steps);
            Assert.That(body.Screens.IsOpen(ScreenCatalog.Minigame), Is.False, "판이 끝나면 닫는다");
            Assert.That(played.IsCancelled, Is.False, "포기하지 않고 끝까지 했다");
            Assert.That(screen.Settlements, Is.EqualTo(1), "한 번 정산했다");
            if (!guesses) Assert.That(played.Succeeded, Is.True, "답을 볼 수 있는 도전만 든 판은 풀어 낸다(점수 " + played.Score + ")");
        }

        private static void Do(HeadlessBody body, PlayAction a)
        {
            if (a == null) return;
            switch (a.Kind)
            {
                case PlayActionKind.Click: body.Click(a.X, a.Y); break;
                case PlayActionKind.Key: body.PressKey(a.ScreenKey); break;
                case PlayActionKind.Type: body.TypeInto(a.X, a.Y, a.Text); break;
                case PlayActionKind.Wait: body.Wait(a.Frames); break;
                default: Assert.Fail("이 테스트에서 쓰지 않는 행동: " + a.Key); break;
            }
        }
    }
}
