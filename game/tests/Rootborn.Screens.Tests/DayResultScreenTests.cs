using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Time;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Screens.Day;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>하루 결과 화면(Unity StudentDayEndInteractor + StudentDayResultPanel, 하루 마감 지점 상호작용으로 열림). 자동 테스트, 창 없음.</summary>
    public sealed class DayResultScreenTests
    {
        private DayScreensFixture _f;

        [SetUp] public void Make() => _f = new DayScreensFixture();
        [TearDown] public void Clean() => _f.Dispose();

        private DayResultScreen Open()
        {
            Assert.That(_f.Interact("House", DayResultScreen.InteractionKind, "house.day-end"), Is.True);
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.DayResult), Is.True);
            return (DayResultScreen)_f.Stack.Find(ScreenCatalog.DayResult);
        }

        private string Body(DayResultScreen screen) => string.Join("\n\n", screen.Body.Sections);

        [Test]
        public void Interaction_EndsTheDay_AndOpensAModal1160x900ThatBlocksWalking()
        {
            Assert.That(_f.Session.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
            Open();
            Assert.That(_f.Session.Student.DayState, Is.EqualTo(StudentDayState.ResultReady), "하루를 끝냈다");
            Assert.That(_f.Node(DayResultScreen.PanelId).Rect, Is.EqualTo(new UiRect(380, 90, 1160, 900)));
            Assert.That(_f.Stack.MovementBlocked, Is.True);
            Assert.That(((UiLabel)_f.Node(DayResultScreen.TitleId)).Text, Is.EqualTo(string.Format(_f.Text.Get("day-result.title"), 1)));
        }

        [Test]
        public void Sections_FollowTheUnityOrder()
        {
            DayResultScreen screen = Open();
            string[] heads = screen.Body.Sections.Select(s => s.Split('\n')[0]).ToArray();
            string[] expected = { "activities", "growth", "quests", "relationships", "conditions", "rewards", "inventory" };
            Assert.That(heads.Take(expected.Length), Is.EqualTo(expected.Select(k => _f.Text.Get("day-result." + k))));
            Assert.That(heads[expected.Length], Does.StartWith(_f.Text.Get("day-result.next-day")).Or.StartWith(_f.Text.Get("day-result.next-prefix")));
        }

        [Test]
        public void ActivitiesDoneToday_AreListed_AndRelationshipOrConditionChangesShow()
        {
            Assert.That(_f.Interact("School", LocationScreen.InteractionKind, "location.school"), Is.True);
            var location = (LocationScreen)_f.Stack.Find(ScreenCatalog.Location);
            int index = location.Model.Rows.ToList().FindIndex(r => r.Enabled);
            UiRect r = location.RowRect(index);
            _f.ClickAt(r.CenterX, r.CenterY);
            string done = location.Model.Rows[index].Id;
            _f.Click(LocationScreen.CloseId);
            _f.Stack.Close(ScreenCatalog.DailyEvent);

            DayResultScreen screen = Open();
            Assert.That(screen.Model.Summary.CompletedActivityIds, Does.Contain(done));
            Assert.That(screen.Body.Sections[0], Does.Not.Contain(_f.Text.Get("day-result.empty-activities")));
            Assert.That(screen.Body.Sections[0], Does.Not.Contain(done), "id가 아니라 이름으로 보인다");
        }

        [Test]
        public void NextDay_StartsDayTwo_AndCloses()
        {
            Open();
            Assert.That(_f.Node(DayResultScreen.NextDayId).Enabled, Is.True);
            _f.Click(DayResultScreen.NextDayId);
            Assert.That(_f.Session.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(_f.Session.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(_f.Stack.IsOpen(ScreenCatalog.DayResult), Is.False);
            Assert.That(_f.Stack.MovementBlocked, Is.False);
        }

        [Test]
        public void Together_TheSleeperWaits_ShowsWhoIsAwake_UntilEveryoneSleeps()
        {
            var clock = new RoomClock(TestSession.Definitions.TimeRules);
            clock.Join("me");
            clock.Join("friend");
            using var shared = new DayScreensFixture(room: clock, playerId: "me");
            Assert.That(shared.Interact("House", DayResultScreen.InteractionKind, "house.day-end"), Is.True);
            var next = (UiButton)shared.Node(DayResultScreen.NextDayId);
            var screen = (DayResultScreen)shared.Stack.Find(ScreenCatalog.DayResult);
            Assert.That(screen.Model.Sleeping, Is.True, "자는 중 화면");
            Assert.That(screen.Model.AwakeOthers, Is.EqualTo(new[] { "friend" }), "아직 깨어 있는 사람");
            Assert.That(next.Enabled, Is.False, "자면 07:00까지 깨어날 수 없다");
            Assert.That(next.Text, Is.EqualTo(shared.Text.Get("day-result.sleeping")));

            clock.Sleep("friend");
            shared.Session.SyncWithRoom();
            Assert.That(next.Enabled, Is.True, "방이 다음 날로 넘어갔다");
            Assert.That(next.Text, Is.EqualTo(shared.Text.Get("day-result.next-day")));
            shared.Click(DayResultScreen.NextDayId);
            Assert.That(shared.Session.Student.CurrentDay, Is.EqualTo(2));
        }

        [Test]
        public void Together_AwakePeople_AreShownByName_AndNamelessAsTheGuestWord()
        {
            var clock = new RoomClock(TestSession.Definitions.TimeRules);
            clock.Join("me");
            clock.Join("slot-f85f0535da6d0a64");
            clock.Join("slot-0000000000000002");
            var names = new FixedRoomNames { ["slot-f85f0535da6d0a64"] = "민지" };
            using var shared = new DayScreensFixture(room: clock, playerId: "me", names: names);
            Assert.That(shared.Interact("House", DayResultScreen.InteractionKind, "house.day-end"), Is.True);
            var screen = (DayResultScreen)shared.Stack.Find(ScreenCatalog.DayResult);
            Assert.That(screen.Model.AwakeOthers, Is.EquivalentTo(new[] { "slot-f85f0535da6d0a64", "slot-0000000000000002" }), "신원은 id 그대로");
            string sleeping = screen.Body.Sections.Single(s => s.StartsWith(shared.Text.Get("day-result.sleeping-awake-others").Split('{')[0], StringComparison.Ordinal));
            Assert.That(sleeping, Does.Contain("민지"));
            Assert.That(sleeping, Does.Contain(shared.Text.Get("ui.room.guest-name")), "이름이 없으면 기본 말");
            Assert.That(sleeping, Does.Not.Contain("slot-"), "내부 id를 보이지 않는다");
        }

        [Test]
        public void Together_LongNames_StayInsideTheBody()
        {
            var clock = new RoomClock(TestSession.Definitions.TimeRules);
            clock.Join("me");
            var names = new FixedRoomNames();
            for (int i = 0; i < 3; i++)
            {
                clock.Join("p" + i);
                names["p" + i] = new string((char)('가' + i), Rootborn.Screens.Start.CharacterCreatorModel.MaxNameLength);
            }

            using var shared = new DayScreensFixture(room: clock, playerId: "me", names: names);
            Assert.That(shared.Interact("House", DayResultScreen.InteractionKind, "house.day-end"), Is.True);
            var screen = (DayResultScreen)shared.Stack.Find(ScreenCatalog.DayResult);
            string body = string.Join(" ", screen.Body.Sections);
            foreach (string name in names.Values) Assert.That(body, Does.Contain(name), "이름을 화면이 자르지 않는다");
            // 하루 결과 본문은 여러 줄 글자(줄바꿈)라 긴 이름도 패널 폭 안에서 줄을 바꾼다 — 그리기가 예외 없이 끝나는지 본다.
            shared.Stack.Emit(new DrawList(), shared.Measurer);
        }

        private sealed class FixedRoomNames : Dictionary<string, string>, IRoomNames
        {
            public string NameOf(string participantId) => TryGetValue(participantId, out var name) ? name : string.Empty;
        }

        [Test]
        public void Reopening_TheEndedDay_DoesNotEndItAgain_AndShowsTheSameDay()
        {
            Open();
            _f.Stack.Close(ScreenCatalog.DayResult);
            int day = _f.Session.Student.CurrentDay;
            DayResultScreen screen = Open();
            Assert.That(_f.Session.Student.CurrentDay, Is.EqualTo(day));
            Assert.That(screen.Model.Summary.DayNumber, Is.EqualTo(day));
            Assert.That(_f.Session.Student.DayState, Is.EqualTo(StudentDayState.ResultReady));
        }

        [Test]
        public void Body_ScrollsWithTheWheel_WhenItIsLongerThanTheView()
        {
            DayResultScreen screen = Open();
            _f.EmitTexts();
            UiRect body = _f.Node(DayResultScreen.BodyId).Rect;
            Assert.That(body, Is.EqualTo(new UiRect(380 + 42, 90 + 112, 1160 - 42 - 34, 900 - 224)));
            if (screen.Body.MaxScroll <= 0) Assert.Ignore("결산이 한 화면에 들어간다");
            _f.Stack.Pointer(body.CenterX, body.CenterY, false, -2);
            Assert.That(screen.Body.ScrollOffset, Is.GreaterThan(0));
        }

        [Test]
        public void AllTextKeys_ExistInBothLanguages_ExceptTheSharedKnownMissingOnes()
        {
            DayResultScreen screen = Open();
            _f.EmitTexts();
            _f.Text.Language = "en";
            _f.EmitTexts();
            KnownMissingKeys.AssertOnlyKnownMissing(_f.Text, screen.TextKeys);
        }
    }
}
