using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.World;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 자동 테스트(화면 근거 아님): 조작감·연출 계산 — 카메라 부드러운 추적(Unity CameraFollow SmoothDamp), 클릭 행동(Unity PlayerController·
    /// GrowingCharacterVisualView), 문 열림·구역 페이드(Unity TownAutoDoorPortal·TownLevelTransitionTrigger). 수치는 game/world/presentation.json.
    /// </summary>
    public sealed class WorldFeelTests
    {
        private static WorldPresentation Real => WorldPresentation.Load(Path.Combine(Repo.Root, WorldPresentation.DefaultRelativePath));

        // ---------- 데이터 ----------

        [Test]
        public void RealData_HasUnitysValues()
        {
            WorldPresentation p = Real;
            Assert.That(p.CameraSmoothSeconds, Is.EqualTo(0.15f), "Unity CameraFollow._smoothTime(모든 씬 0.15)");
            Assert.That(p.ActionSeconds, Is.EqualTo(0.3f), "Unity GrowingCharacterVisualView.ActionSeconds");
            Assert.That((p.ActionFirstColumn, p.ActionColumnCount), Is.EqualTo((3, 2)));
            Assert.That((p.DoorWidthFrom, p.DoorWidthTo, p.DoorAlphaFrom, p.DoorAlphaTo), Is.EqualTo((0.25f, 1f, 0.55f, 1f)), "Unity TownAutoDoorPortal");
            Assert.That(p.AreaFadeAlpha, Is.EqualTo(0.82f), "Unity TownLevelTransitionTrigger 덮개");
            Assert.That(p.TransitionsBlockInput, Is.False, "Unity 판은 문이 열리는 동안 입력을 막지 않았다");
        }

        [Test]
        public void Parse_RejectsANegativeSmoothTime()
        {
            Assert.Throws<InvalidDataException>(() => WorldPresentation.Parse("{ \"camera\": { \"smoothSeconds\": -1 } }"));
        }

        // ---------- 카메라 ----------

        private static readonly AreaRect Map = new AreaRect(0, 0, 2000, 1000);
        private const float ViewW = 640, ViewH = 360;

        [Test]
        public void Camera_FirstFrameSnaps_ThenFollowsSmoothly_AndReachesTheTarget()
        {
            var camera = new SmoothCamera(0.15f);
            AreaRect first = camera.Follow(500, 500, Map, ViewW, ViewH, 1f / 60f);
            Assert.That(first.CenterX, Is.EqualTo(500).Within(1f), "처음에는 바로 맞춘다(Unity SnapToTarget)");
            AreaRect next = camera.Follow(700, 500, Map, ViewW, ViewH, 1f / 60f);
            Assert.That(next.CenterX, Is.GreaterThan(500f).And.LessThan(700f), "목표가 옮겨 가면 따라가는 중");
            for (int i = 0; i < 120; i++) next = camera.Follow(700, 500, Map, ViewW, ViewH, 1f / 60f);
            Assert.That(next.CenterX, Is.EqualTo(700).Within(1f), "2초 뒤에는 목표에 닿는다");
        }

        [Test]
        public void Camera_SnapPutsItOnTheTargetRightAway()
        {
            var camera = new SmoothCamera(0.15f);
            camera.Follow(500, 500, Map, ViewW, ViewH, 1f / 60f);
            camera.Snap();
            AreaRect r = camera.Follow(1500, 300, Map, ViewW, ViewH, 1f / 60f);
            Assert.That(r.CenterX, Is.EqualTo(1500f).Within(1f));
            Assert.That(r.CenterY, Is.EqualTo(300f).Within(1f));
        }

        [Test]
        public void Camera_StaysInsideTheBounds_AtTheEdge()
        {
            var camera = new SmoothCamera(0.15f);
            camera.Follow(1000, 500, Map, ViewW, ViewH, 1f / 60f);
            AreaRect r = default;
            for (int i = 0; i < 240; i++) r = camera.Follow(1990, 990, Map, ViewW, ViewH, 1f / 60f);
            Assert.That(r.Right, Is.LessThanOrEqualTo(Map.Right + 0.01f));
            Assert.That(r.Bottom, Is.LessThanOrEqualTo(Map.Bottom + 0.01f));
            Assert.That(r.Right, Is.EqualTo(Map.Right).Within(1f), "가장자리에서 멈춘다");
            for (int i = 0; i < 240; i++)
            {
                r = camera.Follow(i % 2 == 0 ? 5 : 1995, 5, Map, ViewW, ViewH, 1f / 60f);
                Assert.That(r.X, Is.GreaterThanOrEqualTo(Map.X - 0.01f));
                Assert.That(r.Right, Is.LessThanOrEqualTo(Map.Right + 0.01f), "흔들어도 경계 밖으로 넘치지 않는다");
            }
        }

        [Test]
        public void Camera_SmallMap_StaysCentered()
        {
            var small = new AreaRect(0, 0, 300, 200);
            var camera = new SmoothCamera(0.15f);
            AreaRect r = camera.Follow(10, 10, small, ViewW, ViewH, 1f / 60f);
            for (int i = 0; i < 30; i++) r = camera.Follow(290, 190, small, ViewW, ViewH, 1f / 60f);
            Assert.That(r.CenterX, Is.EqualTo(150).Within(1f));
            Assert.That(r.CenterY, Is.EqualTo(100).Within(1f));
        }

        /// <summary>목표가 1초 동안 초당 400px로 움직이다 선다. 그동안의 카메라 가운데(소수 픽셀 그대로).</summary>
        private static float Run(Func<int, float> dtOf, float until)
        {
            var camera = new SmoothCamera(0.15f);
            float t = 0f, target = 300f;
            camera.Follow(target, 500, Map, ViewW, ViewH, 0f);
            int frame = 0;
            while (t < until - 1e-6f)
            {
                float dt = Math.Min(dtOf(frame++), until - t);
                t += dt;
                target = 300f + 400f * Math.Min(t, 1f);
                camera.Follow(target, 500, Map, ViewW, ViewH, dt);
            }
            return camera.CenterX;
        }

        [TestCase(0.5f)]
        [TestCase(1.0f)]
        [TestCase(1.3f)]
        [TestCase(3.0f)]
        public void Camera_DoesNotDependOnTheFrameRate(float at)
        {
            float at60 = Run(_ => 1f / 60f, at);
            float at144 = Run(_ => 1f / 144f, at);
            float at30 = Run(_ => 1f / 30f, at);
            var random = new Random(7);
            float uneven = Run(_ => (float)(0.004 + random.NextDouble() * 0.04), at);
            // 움직이는 동안은 목표를 프레임마다 띄엄띄엄 읽는 만큼(초당 400px에서 30fps는 약 9ms 늦게 본다) 몇 픽셀 다르다 — Unity 판도 같다.
            // 멈춘 뒤에는 간격과 무관하게 같은 자리다.
            float tolerance = at >= 3f ? 0.5f : 5f;
            Assert.That(at144, Is.EqualTo(at60).Within(tolerance), "144 대 60");
            Assert.That(at30, Is.EqualTo(at60).Within(tolerance), "30 대 60");
            Assert.That(uneven, Is.EqualTo(at60).Within(tolerance), "고르지 않은 간격 대 60");
            if (at >= 3f) Assert.That(at60, Is.EqualTo(700f).Within(0.5f), "멈춘 뒤에는 같은 자리로 수렴한다");
        }

        [Test]
        public void SmoothDamp_MatchesUnitysFormula()
        {
            // Unity Mathf.SmoothDamp(0, 10, ref v=0, 0.15, inf, 1/60) = 0.1669...(공식을 손으로 계산한 값)
            float v = 0f;
            float x = SmoothCamera.SmoothDamp(0f, 10f, ref v, 0.15f, 1f / 60f);
            float omega = 2f / 0.15f, k = omega / 60f, exp = 1f / (1f + k + 0.48f * k * k + 0.235f * k * k * k);
            float temp = (0f + omega * -10f) / 60f;
            float expected = 10f + (-10f + temp) * exp;
            Assert.That(x, Is.EqualTo(expected).Within(1e-5f));
            Assert.That(v, Is.GreaterThan(0f));
        }

        // ---------- 클릭 행동 ----------

        [TestCase(10f, 1f, Facing.Right)]
        [TestCase(-10f, 3f, Facing.Left)]
        [TestCase(1f, 10f, Facing.Down)]
        [TestCase(2f, -10f, Facing.Up)]
        public void PointerFacing_FollowsTheLongerAxis(float dx, float dy, Facing expected)
        {
            Assert.That(PointerFacing.From(dx, dy, Facing.Down), Is.EqualTo(expected));
        }

        [Test]
        public void PointerFacing_OnTheCharacterItself_KeepsTheFacing()
        {
            Assert.That(PointerFacing.From(0f, 0f, Facing.Left), Is.EqualTo(Facing.Left));
        }

        [Test]
        public void Action_ShowsColumnsThreeThenFour_ForPointThreeSeconds_ThenGivesBackTheWalkColumn()
        {
            var action = new CharacterActionTimer(0.3f, 3, 2);
            Assert.That(action.ColumnOr(1), Is.EqualTo(1), "행동 중이 아니면 걷기 칸 그대로");
            action.Play(Facing.Left);
            Assert.That(action.Active, Is.True);
            Assert.That(action.Facing, Is.EqualTo(Facing.Left));
            action.Advance(0.05f);
            Assert.That(action.ColumnOr(1), Is.EqualTo(3));
            action.Advance(0.15f);
            Assert.That(action.ColumnOr(1), Is.EqualTo(4));
            action.Advance(0.11f);
            Assert.That(action.Active, Is.False);
            Assert.That(action.ColumnOr(2), Is.EqualTo(2));
        }

        [Test]
        public void Action_PlayingAgain_StartsOver()
        {
            var action = new CharacterActionTimer(0.3f, 3, 2);
            action.Play(Facing.Up);
            action.Advance(0.25f);
            action.Play(Facing.Down);
            Assert.That(action.Facing, Is.EqualTo(Facing.Down));
            action.Advance(0.1f);
            Assert.That(action.ColumnOr(1), Is.EqualTo(3), "다시 누르면 처음 칸부터");
        }

        // ---------- 문 열림·구역 페이드 ----------

        private static readonly DoorArea Door = new DoorArea("town", new AreaRect(100, 100, 32, 16), new TravelTarget("Town", "a"));
        private static readonly DoorArea Zone = new DoorArea("beach", new AreaRect(0, 0, 32, 400), new TravelTarget("Beach", "b"), 0.25f);

        /// <summary>한 프레임: Core DoorWatch가 문 열림 시간을 세고(한 곳), 연출은 그 진행도를 그대로 보인다.</summary>
        private static void Frame(WorldTransition t, DoorWatch watch, AreaRect body, bool isArea, float dt)
        {
            watch.Update(body, dt);
            t.Show(watch.Opening, isArea, watch.Progress);
        }

        private static readonly AreaRect Away = new AreaRect(500, 500, 32, 32);

        [Test]
        public void Door_ProgressGoesFromZeroToOne_OverItsOpenSeconds_WideningAndBrightening()
        {
            var t = new WorldTransition(Real);
            var watch = new DoorWatch(new[] { Door });
            watch.Reset(Away);
            Frame(t, watch, Away, false, 1f / 60f);
            Assert.That(t.Kind, Is.EqualTo(WorldTransitionKind.None));
            Frame(t, watch, Door.Area, false, 1f / 60f);
            Assert.That(t.Kind, Is.EqualTo(WorldTransitionKind.Door));
            Assert.That(t.Progress, Is.EqualTo(0f), "들어선 프레임은 0(Core DoorWatch가 센다)");
            Assert.That(t.DoorWidthRatio, Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(t.DoorAlpha, Is.EqualTo(0.55f).Within(1e-4f));
            Assert.That(t.FadeAlpha, Is.EqualTo(0f), "문에는 화면 덮개가 없다");
            for (int i = 0; i < 10; i++) Frame(t, watch, Door.Area, false, 1f / 60f);
            float half = 10f / 60f / DoorWatch.UnityOpeningSeconds;
            Assert.That(t.Progress, Is.EqualTo(half).Within(1e-3f));
            Assert.That(t.Progress, Is.EqualTo(watch.Progress), "따로 세지 않는다");
            Assert.That(t.DoorWidthRatio, Is.EqualTo(0.25f + 0.75f * half).Within(1e-3f));
            Assert.That(t.BlocksInput, Is.False);
            t.Show(null, false, 0f); // 지났다(맵이 바뀌었다)
            Assert.That(t.Kind, Is.EqualTo(WorldTransitionKind.None));
            Assert.That(t.Completed, Is.EqualTo(1));
        }

        [Test]
        public void Zone_FadesTheScreen_ForItsOwnSeconds()
        {
            var t = new WorldTransition(Real);
            var watch = new DoorWatch(new[] { Zone });
            watch.Reset(Away);
            Frame(t, watch, new AreaRect(0, 0, 32, 32), true, 1f / 60f);
            Assert.That(t.Kind, Is.EqualTo(WorldTransitionKind.Area));
            Assert.That(t.FadeAlpha, Is.EqualTo(0.82f), "Unity 판 덮개는 처음부터 82%");
            Assert.That(t.DoorAlpha, Is.EqualTo(0f));
            for (int i = 0; i < 6; i++) Frame(t, watch, new AreaRect(0, 0, 32, 32), true, 0.02f);
            Assert.That(t.Progress, Is.EqualTo(0.12f / 0.25f).Within(1e-3f));
        }

        [Test]
        public void ADifferentDoor_StartsOver()
        {
            var t = new WorldTransition(Real);
            t.Show(Door, false, 0f);
            t.Show(Door, false, 0.4f);
            var other = new DoorArea("house", new AreaRect(300, 100, 32, 16), new TravelTarget("House", "a"));
            t.Show(other, false, 0f);
            Assert.That(t.Door, Is.SameAs(other));
            Assert.That(t.Progress, Is.EqualTo(0f));
        }

        // ---------- 성능 ----------

        [Test]
        public void EveryFrameWork_DoesNotAllocate()
        {
            var camera = new SmoothCamera(0.15f);
            var action = new CharacterActionTimer(0.3f, 3, 2);
            var t = new WorldTransition(Real);
            void OneFrame(int i)
            {
                camera.Follow(500 + i, 500, Map, ViewW, ViewH, 1f / 60f);
                action.Advance(1f / 60f);
                action.ColumnOr(1);
                PointerFacing.From(i, -i, Facing.Down);
                t.Show(i % 40 < 20 ? Door : null, false, (i % 20) / 20f);
            }
            for (int i = 0; i < 100; i++) OneFrame(i); // 처음 쓸 때의 준비는 빼고 잰다
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) OneFrame(i);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "매 프레임 새 객체를 만들지 않는다");
        }
    }
}
