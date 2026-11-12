using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.NpcMovement;
using Rootborn.Screens;
using Rootborn.Screens.Start;
using Rootborn.Simulation.Headless;
using Rootborn.World;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 걷는 NPC(자동 테스트, 창 없음): 실제 데이터로 게임 한 판을 띄우고 시계를 프레임마다 흘려 보내며(AdvanceTime) NPC가 어디 있는지 본다.
    /// 시작 화면은 클릭으로 지나고, 그 뒤로는 아무것도 누르지 않는다. 화면 근거가 아니다.
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class NpcWalkingTests
    {
        private const string Teacher = "npc.teacher";

        private static HeadlessBody NewGame(PlayAtlas atlas, string saveRoot = null)
        {
            var body = new HeadlessBody(atlas, saveRoot);
            var observer = new PlayObserver(atlas);
            var memory = new PlayMemory();
            for (int i = 0; i < 5 && !body.InWorld; i++)
            {
                PlayAction a = StartRoutine.Choose(observer.Observe(body), memory);
                if (a.Kind == PlayActionKind.Type) body.TypeInto(a.X, a.Y, a.Text); else body.Click(a.X, a.Y);
            }
            Assert.That(body.InWorld, Is.True, "시작 순서로 월드에 들어갔다");
            return body;
        }

        /// <summary>한 프레임씩 흘려 보낸다. 시계가 오래 멈추면(시계를 멈추는 화면이 떴다) 실패한다.</summary>
        private static void WaitFrame(HeadlessBody body, ref int stuck, ref double last)
        {
            body.Wait(1);
            double now = body.Session.DayMinutesExact;
            stuck = now == last ? stuck + 1 : 0;
            last = now;
            Assert.That(stuck, Is.LessThan(600), "시계가 10초 넘게 멈췄다(맨 위 화면이 시계를 멈춘다?) " + now + "분");
        }

        [Test]
        public void 선생님이_정오에_학교를_떠나_마을_광장에_도착한다()
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            (float X, float Y) square = atlas.Interactions.Map("Town").NpcAnchors["location.town-square"][0];
            float cell = atlas.Maps.LoadMap("Town").World.Grid.CellSize;
            using HeadlessBody body = NewGame(atlas);
            NpcStage npcs = body.Npcs;
            int stuck = 0, schoolFramesBeforeNoon = 0;
            double last = double.NaN, leftAt = double.NaN, arrivedAt = double.NaN;
            var problems = new List<string>();
            while (body.Session.DayMinutesExact < 1019)
            {
                WaitFrame(body, ref stuck, ref last);
                double t = body.Session.DayMinutesExact;
                if (t >= 1019) break;
                bool present = npcs.TryPose(Teacher, out NpcPose pose);
                if (t < 720)
                {
                    if (!present || pose.MapId != "School") problems.Add(t.ToString("0.00") + "분: 정오 전인데 학교에 없다(" + (present ? pose.MapId : "없음") + ")");
                    else schoolFramesBeforeNoon++;
                    continue;
                }
                if (double.IsNaN(leftAt) && present && pose.MapId == "Town") leftAt = t;
                bool nearSquare = present && pose.MapId == "Town" && Math.Abs(pose.X - square.X) <= 3 * cell && Math.Abs(pose.Y - square.Y) <= 3 * cell;
                if (double.IsNaN(arrivedAt) && nearSquare) arrivedAt = t;
                else if (!double.IsNaN(arrivedAt) && !nearSquare)
                    problems.Add(t.ToString("0.00") + "분: 광장에 닿은 뒤 반지름 3칸 밖(" + (present ? pose.MapId + " " + pose.X + ", " + pose.Y : "없음") + ")");
            }
            TestContext.WriteLine("학교를 나와 마을에 " + leftAt.ToString("0.00") + "분, 광장 자리 둘레에 " + arrivedAt.ToString("0.00") + "분");
            Assert.That(schoolFramesBeforeNoon, Is.GreaterThan(0), "이 시험의 전제: 정오 전에 학교에 있던 때를 봤다");
            Assert.That(problems, Is.Empty, string.Join("\n", problems.GetRange(0, Math.Min(10, problems.Count))));
            Assert.That(leftAt, Is.GreaterThanOrEqualTo(720), "정오에 학교를 떠나 마을에 온다");
            Assert.That(arrivedAt, Is.LessThan(1019), "1019분 전에 광장 자리 둘레(반지름 3칸)에 닿는다");
        }

        [Test]
        public void 이어_해도_같은_시각에는_같은_자리다()
        {
            // 저장은 게임 1시간이 바뀔 때 한다(GameSession.AdvanceTime). 800분 앞의 정시 저장(13:00 = 780분)을 쓴다.
            const int SaveMinutes = 780;
            PlayAtlas atlas = FastPlayTests.Atlas;
            IReadOnlyList<string> ids = NpcArt.Load(atlas.RepoRoot).Order;
            string saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-sim-npc-" + Guid.NewGuid().ToString("N"));
            try
            {
                var before = new (bool Present, NpcPose Pose)[ids.Count];
                double savedAt;
                using (HeadlessBody first = NewGame(atlas, saveRoot))
                {
                    int stuck = 0;
                    double last = double.NaN;
                    while (first.Session.StudentTimeMinutes < SaveMinutes) WaitFrame(first, ref stuck, ref last);
                    Assert.That(first.Session.StudentTimeMinutes, Is.EqualTo(SaveMinutes), "이 시험의 전제: 한 프레임에 1분 넘게 가지 않는다");
                    savedAt = first.Session.DayMinutesExact;
                    for (int i = 0; i < ids.Count; i++) before[i].Present = first.Npcs.TryPose(ids[i], out before[i].Pose);
                }

                using var second = new HeadlessBody(atlas, saveRoot);
                PlayAction resume = new PlayObserver(atlas).Observe(second).Find("button:" + ScreenCatalog.SaveSlotSelect + "/" + SaveSlotScreen.ContinueId(0));
                Assert.That(resume, Is.Not.Null, "저장한 슬롯을 이어 할 수 있다");
                second.Click(resume.X, resume.Y);
                Assert.That(second.InWorld, Is.True, "이어 하기로 월드에 들어갔다");
                Assert.That(second.Session.StudentTimeMinutes, Is.EqualTo(SaveMinutes), "저장한 시각에서 이어진다");
                TestContext.WriteLine("저장 " + savedAt.ToString("0.000") + "분, 이어 한 뒤 " + second.Session.DayMinutesExact.ToString("0.000") + "분");

                int checkedPresent = 0;
                for (int i = 0; i < ids.Count; i++)
                {
                    bool present = second.Npcs.TryPose(ids[i], out NpcPose after);
                    Assert.That(present, Is.EqualTo(before[i].Present), ids[i]);
                    if (!present) continue;
                    checkedPresent++;
                    NpcPose was = before[i].Pose;
                    float cell = atlas.Maps.LoadMap(was.MapId).World.Grid.CellSize;
                    Assert.That(after.MapId, Is.EqualTo(was.MapId), ids[i]);
                    Assert.That(Math.Abs(after.X - was.X), Is.LessThanOrEqualTo(cell), ids[i] + " X (분 아래 조각은 0에서 시작하므로 1칸 안)");
                    Assert.That(Math.Abs(after.Y - was.Y), Is.LessThanOrEqualTo(cell), ids[i] + " Y");
                }
                Assert.That(checkedPresent, Is.GreaterThan(0), "이 시험의 전제: 저장한 시각에 어느 맵엔가 있는 NPC가 있다");
            }
            finally
            {
                try { Directory.Delete(saveRoot, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
