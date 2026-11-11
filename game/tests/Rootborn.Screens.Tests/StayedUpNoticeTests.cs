using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Screens.Day;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 잠자리와 밤샘 알림(흐르는 시간 설계 3.3·3.4): 잠자리(하루 마감 지점)는 자는 곳이고 취침 시각 전에도 재운다. 자지 않고 07:00을 넘기면 하루 결과 없이
    /// 다음 날이 열리고 밤샘 알림이 한 번 뜬다. 시간은 GameSession.AdvanceTime으로 손으로 돌린다. 자동 테스트, 창 없음.
    /// </summary>
    public sealed class StayedUpNoticeTests
    {
        private const float GameHour = 25f;
        private DayScreensFixture _f;

        [SetUp] public void Make() => _f = new DayScreensFixture();
        [TearDown] public void Clean() => _f.Dispose();

        [Test]
        public void SleepingBeforeBedtime_IsAllowed_WakesAtSeven_AndShowsNoStayedUpNotice()
        {
            _f.Session.AdvanceTime(GameHour * 3, false); // 10:00 — 취침 시각 전
            Assert.That(_f.Interact("House", DayResultScreen.InteractionKind, "house.day-end"), Is.True);
            Assert.That(_f.Session.Student.DayState, Is.Not.EqualTo(StudentDayState.InProgress), "일찍 자도 막지 않는다");
            _f.Click(DayResultScreen.NextDayId);
            Assert.That((_f.Session.Student.CurrentDay, _f.Session.ClockMinutes), Is.EqualTo((2, 7 * 60)), "다음 날 07:00에 일어난다");
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.StayedUpNotice), Is.False, "잔 날은 밤샘 알림이 없다");
        }

        [Test]
        public void StayingUpPastSeven_OpensTheNextDay_WithOneStayedUpNotice_ThatClosesByItself()
        {
            _f.Stack.OpenHud();
            for (int i = 0; i < 25 && _f.Session.Student.CurrentDay == 1; i++) _f.Session.AdvanceTime(GameHour, false);
            Assert.That(_f.Session.Student.CurrentDay, Is.EqualTo(2), "준비: 자지 않고 07:00을 넘겼다");
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.DayResult), Is.False, "하루 결과 화면 없이 다음 날이 열린다");
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.StayedUpNotice), Is.True);
            Assert.That(((UiLabel)_f.Node(StayedUpNoticeScreen.MessageId)).Text, Is.EqualTo(_f.Text.Get(StayedUpNoticeScreen.MessageKey)));
            Assert.That(_f.Stack.PausesClock, Is.False, "알림은 시계를 멈추지 않는다");
            Assert.That(_f.Stack.MovementBlocked, Is.False);

            _f.Stack.Tick(StayedUpNoticeScreen.ShowSeconds + 0.1f);
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.StayedUpNotice), Is.False, "저절로 닫힌다");
            _f.Session.AdvanceTime(GameHour, false);
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.StayedUpNotice), Is.False, "같은 날에는 다시 뜨지 않는다");
        }
    }
}
