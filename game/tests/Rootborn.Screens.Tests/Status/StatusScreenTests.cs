using NUnit.Framework;
using Rootborn.Screens.Status;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Status
{
    public sealed class StatusScreenTests
    {
        private static StatusScreen Open(TestSession t)
        {
            Assert.That(t.Stack.Key(ScreenKey.Status, false), Is.True);
            return t.Screen<StatusScreen>(ScreenCatalog.Status);
        }

        [Test]
        public void C_OpensA560x540PanelInTheMiddle_WithoutBlockingMovement_AndCCloses()
        {
            using var t = new TestSession();
            Open(t);
            Assert.That(t.Stack.MovementBlocked, Is.False);
            Assert.That(t.Node(StatusScreen.RootId).Rect, Is.EqualTo(new UiRect(680, 270, 560, 540)));
            t.Stack.Key(ScreenKey.Status, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Status), Is.False);
        }

        [Test]
        public void NewGame_ShowsDayPointsAndTheThreeVitals_InUnityOrder()
        {
            using var t = new TestSession();
            var screen = Open(t);
            var s = t.Session.Student;
            Assert.That((screen.Model.Day, screen.Model.Energy, screen.Model.Focus, screen.Model.Stress),
                Is.EqualTo((t.Session.StudentDay, s.Energy, s.Focus, s.Stress)));
            Assert.That(((UiLabel)t.Node(StatusScreen.DayPointsId)).Text,
                Is.EqualTo(t.Text.Format("ui.status.day-points", t.Session.StudentDay, t.Session.TimePoints.Remaining)));
            Assert.That(((UiLabel)t.Node(StatusScreen.ValueId(0))).Text, Is.EqualTo(s.Energy.ToString()));
            Assert.That(((UiLabel)t.Node(StatusScreen.ValueId(1))).Text, Is.EqualTo(s.Focus.ToString()));
            Assert.That(((UiLabel)t.Node(StatusScreen.ValueId(2))).Text, Is.EqualTo(s.Stress.ToString()));
            Assert.That(((UiLabel)t.Node(StatusScreen.LabelId(0))).Text, Is.EqualTo(t.Text.Get("ui.status.energy")));
            Assert.That(((UiLabel)t.Node(StatusScreen.LabelId(2))).Text, Is.EqualTo(t.Text.Get("ui.status.stress")));
            Assert.That(((UiLabel)t.Node(StatusScreen.TitleId)).Text, Is.EqualTo(t.Text.Get("ui.status.title")));
            UiRect first = t.Node(StatusScreen.CardId(0)).Rect, second = t.Node(StatusScreen.CardId(1)).Rect;
            Assert.That(second.Y - first.Y, Is.EqualTo(108f), "cards stack 108 apart");
            Assert.That(first.H, Is.EqualTo(96f));
        }

        [Test]
        public void ValuesFollowACommand_OnlyThroughTheChangeNotice()
        {
            using var t = new TestSession();
            var screen = Open(t);
            int energy = t.Session.Student.Energy;
            t.Session.PerformLocationActivity("location.activity.school-class", "req-direct");
            Assert.That(screen.Model.Energy, Is.EqualTo(energy), "a change outside the wrapper is not seen until a notice");
            t.Commands.Run(s => s.PerformLocationActivity("location.activity.library-hard-book", "req-1"), ChangeTopics.Student);
            Assert.That(screen.Model.Energy, Is.EqualTo(t.Session.Student.Energy));
            Assert.That(((UiLabel)t.Node(StatusScreen.ValueId(0))).Text, Is.EqualTo(t.Session.Student.Energy.ToString()));
            Assert.That(t.Session.Student.Energy, Is.LessThan(energy));
        }

        [Test]
        public void TheCloseButton_Closes()
        {
            using var t = new TestSession();
            Open(t);
            t.Click(StatusScreen.CloseId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Status), Is.False);
        }

        [Test]
        public void WithoutAGame_ShowsTheWaitingTextAndDashes()
        {
            var text = TestData.Text();
            var stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(text, null));
            stack.Layout(1920, 1080);
            stack.Key(ScreenKey.Status, false);
            Assert.That(((UiLabel)stack.Tree.Root.Find(StatusScreen.DayPointsId)).Text, Is.EqualTo(text.Get("ui.status.waiting")));
            Assert.That(((UiLabel)stack.Tree.Root.Find(StatusScreen.ValueId(0))).Text, Is.EqualTo("--"));
        }

        [Test]
        public void English_RedrawsTheText_AndOnlyKnownKeysAreMissing()
        {
            using var t = new TestSession();
            var screen = Open(t);
            t.Text.Language = "en";
            Assert.That(((UiLabel)t.Node(StatusScreen.TitleId)).Text, Is.EqualTo("My status"));
            Assert.That(((UiLabel)t.Node(StatusScreen.LabelId(1))).Text, Is.EqualTo("Focus"));
            Assert.That(((UiLabel)t.Node(StatusScreen.HintId)).Text, Is.EqualTo("Press C to close"));
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys);
        }
    }
}
