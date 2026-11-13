using NUnit.Framework;
using Rootborn.Screens;
using Rootborn.Simulation.Pace;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트: 걸음의 분류는 맨 위 화면의 등록 성질(층, 시계를 멈추는지, 키·상호작용으로 여는지)과 행동 종류로 정한다.
    /// 시험용 등록 표를 손으로 만든다(화면 id는 아무 이름 — 분류가 이름을 보지 않는다는 것도 함께 본다).
    /// </summary>
    public sealed class PaceClassifierTests
    {
        private const string SleepKind = "sleep-spot";

        private static PaceClassifier Classifier()
        {
            var r = new ScreenRegistry();
            r.Add("aa", ScreenLayer.Panel, false, _ => null).OpenedBy(ScreenKey.Inventory);          // 키로 여는 패널 → 살펴보기
            r.Add("bb", ScreenLayer.Modal, false, _ => null).OpenedByInteraction("talk");            // 상호작용이 여는 모달 → 읽기·고르기
            r.Add("cc", ScreenLayer.Modal, true, _ => null).KeepsClockRunning();                      // 떠 있어도 시계가 흐른다 → 미니게임
            r.Add("dd", ScreenLayer.Modal, true, _ => null).OpenedByInteraction(SleepKind);          // 잠자리 상호작용이 여는 화면 → 잠
            r.Add("ee", ScreenLayer.Panel, false, _ => null).OpenedByInteraction("board");           // 상호작용이 여는 패널 → 읽기·고르기
            r.Add("ff", ScreenLayer.Panel, true, _ => null).WithoutWorld();                           // 시작 화면 → 읽기·고르기
            return new PaceClassifier(r, SleepKind);
        }

        private static PlayAction A(PlayActionKind kind, string category) => new PlayAction { Kind = kind, Category = category, Key = category };

        [Test]
        public void InTheWorld_WalkingAndDoorsAreWalk_IdlingIsNothingToDo()
        {
            PaceClassifier c = Classifier();
            Assert.That(c.Classify(null, A(PlayActionKind.WalkInteract, "interaction"), busy: false), Is.EqualTo(PaceCategories.Walk));
            Assert.That(c.Classify(null, A(PlayActionKind.Door, "door"), false), Is.EqualTo(PaceCategories.Walk));
            Assert.That(c.Classify(null, A(PlayActionKind.Wait, "idle"), false), Is.EqualTo(PaceCategories.NothingToDo));
            Assert.That(c.Classify(null, A(PlayActionKind.Wait, "hold"), false), Is.EqualTo(PaceCategories.NothingToDo));
            Assert.That(c.Classify(null, A(PlayActionKind.Key, "key"), false), Is.EqualTo(PaceCategories.Browse), "월드에서 키를 누르는 것은 살펴보기 화면을 여는 것");
        }

        [Test]
        public void TopScreen_IsClassifiedByItsRegistration()
        {
            PaceClassifier c = Classifier();
            PlayAction click = A(PlayActionKind.Click, "button");
            Assert.That(c.Classify("aa", click, false), Is.EqualTo(PaceCategories.Browse));
            Assert.That(c.Classify("bb", click, false), Is.EqualTo(PaceCategories.ReadChoose));
            Assert.That(c.Classify("cc", click, false), Is.EqualTo(PaceCategories.Minigame));
            Assert.That(c.Classify("dd", click, false), Is.EqualTo(PaceCategories.Sleep));
            Assert.That(c.Classify("ee", click, false), Is.EqualTo(PaceCategories.ReadChoose));
            Assert.That(c.Classify("ff", click, false), Is.EqualTo(PaceCategories.ReadChoose));
            Assert.That(c.Classify("not-registered", click, false), Is.EqualTo(PaceCategories.ReadChoose), "등록에 없는 화면은 읽기·고르기");
        }

        [Test]
        public void Walking_WithANonBlockingPanelOpen_IsStillWalk_AndBusyWinsOverEverything()
        {
            PaceClassifier c = Classifier();
            Assert.That(c.Classify("aa", A(PlayActionKind.WalkInteract, "interaction"), false), Is.EqualTo(PaceCategories.Walk));
            Assert.That(c.Classify(null, A(PlayActionKind.WalkInteract, "interaction"), busy: true), Is.EqualTo(PaceCategories.Busy));
            Assert.That(c.Classify("bb", A(PlayActionKind.Click, "button"), busy: true), Is.EqualTo(PaceCategories.Busy));
        }

        [Test]
        public void Choices_AreCountedOnlyForInputsOnAScreen_NotForMinigamesOrWalking()
        {
            PaceClassifier c = Classifier();
            Assert.That(c.Choices("bb", A(PlayActionKind.Click, "button"), PaceCategories.ReadChoose), Is.EqualTo(1));
            Assert.That(c.Choices("aa", A(PlayActionKind.Key, "key"), PaceCategories.Browse), Is.EqualTo(1));
            Assert.That(c.Choices("cc", A(PlayActionKind.Click, "button"), PaceCategories.Minigame), Is.EqualTo(0), "미니게임은 제한 시간 몫이 맡는다");
            Assert.That(c.Choices(null, A(PlayActionKind.WalkInteract, "interaction"), PaceCategories.Walk), Is.EqualTo(0));
            Assert.That(c.Choices("bb", A(PlayActionKind.Wait, "wait"), PaceCategories.ReadChoose), Is.EqualTo(0), "기다리기는 고르기가 아니다");
        }

        [Test]
        public void HudAndClockRunningScreens_AreNotRead()
        {
            var r = new ScreenRegistry();
            r.Add("hud", ScreenLayer.Hud, false, _ => null);
            r.Add("game", ScreenLayer.Modal, true, _ => null).KeepsClockRunning();
            r.Add("talk", ScreenLayer.Modal, false, _ => null);
            var c = new PaceClassifier(r, SleepKind);
            Assert.That(c.CountsText("hud"), Is.False, "HUD는 흘끗 보는 것");
            Assert.That(c.CountsText("game"), Is.False, "미니게임 글은 제한 시간 몫에 든다");
            Assert.That(c.CountsText("talk"), Is.True);
        }
    }

    /// <summary>자동 테스트: 새로 나온 글자 수는 앞 관찰에 없던 글줄의 글자(공백 빼고)만 센다.</summary>
    public sealed class NewTextCounterTests
    {
        [Test]
        public void CountsOnlyLinesThatWereNotVisibleBefore_IgnoringSpaces()
        {
            var counter = new NewTextCounter();
            Assert.That(counter.Count(new[] { "안녕 하세요", "닫기" }), Is.EqualTo(7));
            Assert.That(counter.Count(new[] { "안녕 하세요", "닫기" }), Is.EqualTo(0), "그대로면 새 글이 없다");
            Assert.That(counter.Count(new[] { "닫기", "다음 말" }), Is.EqualTo(3));
            Assert.That(counter.Count(new string[0]), Is.EqualTo(0));
            Assert.That(counter.Count(new[] { "닫기" }), Is.EqualTo(2), "사라졌다 다시 나오면 다시 읽는다");
        }
    }
}
