using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Rootborn.Host.Sample;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 견본 화면을 실제 창과 게임 루프로 띄워 PlayerDriver로만 조작한다.
    /// 창 하나를 모든 테스트가 함께 쓰고, 테스트마다 견본 화면을 새로 만든다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class SampleScreenTests
    {
        private const int Width = 1280, Height = 720;
        private PlayerDriver _player;
        private Stopwatch _clock;
        private long _startupMs;
        private string _evidenceDir;

        private SampleScreen Sample => _player.Game.Sample;

        [OneTimeSetUp]
        public void StartGame()
        {
            HostOptions options = HostOptions.Resolve(new[] { "--sample", "--size", Width + "x" + Height });
            options.TestMode = true;
            _evidenceDir = options.EvidenceDir;
            _clock = Stopwatch.StartNew();
            _player = new PlayerDriver(options);
            _startupMs = _clock.ElapsedMilliseconds;
        }

        [OneTimeTearDown]
        public void StopGame()
        {
            if (_player == null) return;
            try
            {
                using Process self = Process.GetCurrentProcess();
                File.WriteAllText(Path.Combine(_evidenceDir, "measure.txt"),
                    $"startup_ms={_startupMs}\nall_tests_ms={_clock.ElapsedMilliseconds}\npeak_working_set_mb={self.PeakWorkingSet64 / (1024 * 1024)}\n" +
                    $"renderer={_player.Game.GraphicsDevice.Adapter.Description}\n");
            }
            finally
            {
                _player.Dispose(); // 창을 닫는다
                _player = null;
            }
        }

        [SetUp]
        public void FreshSample()
        {
            _player.ReleaseEverything();
            if (_player.Game.Fit.TileScale != 2) _player.Game.Resize(Width, Height);
            _player.Game.ShowSample();
            _player.Frames(3);
        }

        [Test]
        public void HOSTUI_001_PointerClicksAButton_AndThePressedLookIsDrawn()
        {
            _player.Screenshot("01-sample-start");
            _player.MovePointerTo("ok");
            Assert.That(Sample.Ok.Hovered, Is.True);
            _player.PressPointer();
            Assert.That(Sample.Ok.Pressed, Is.True);
            Assert.That(Sample.OkClicks, Is.EqualTo(0), "누르기만 해서는 실행되지 않는다");
            _player.Screenshot("02-button-pressed");
            _player.ReleasePointer();
            Assert.That(Sample.OkClicks, Is.EqualTo(1));
            Assert.That(Sample.Status.Text, Is.EqualTo("확인 버튼을 1번 눌렀습니다."));
            _player.Screenshot("03-button-clicked");
        }

        [Test]
        public void HOSTUI_002_PressOnAButton_ReleaseElsewhere_DoesNotClick()
        {
            _player.MovePointerTo("ok");
            _player.PressPointer();
            _player.MovePointerTo("slot7");
            _player.ReleasePointer();
            Assert.That(Sample.OkClicks, Is.EqualTo(0));
            Assert.That(Sample.LastSlot, Is.EqualTo(-1));
        }

        [Test]
        public void HOSTUI_003_DisabledButton_IsNotClicked()
        {
            string before = Sample.Status.Text;
            _player.Click("disabled");
            Assert.That(Sample.DisabledClicks, Is.EqualTo(0));
            Assert.That(Sample.Disabled.Hovered, Is.False);
            Assert.That(Sample.Status.Text, Is.EqualTo(before));
            _player.Screenshot("04-disabled-button-under-pointer");
        }

        [Test]
        public void HOSTUI_004_PointerClicksASlot()
        {
            _player.Click("slot7");
            Assert.That(Sample.LastSlot, Is.EqualTo(7));
            Assert.That(Sample.Status.Text, Is.EqualTo("2행 3열 칸을 골랐습니다."));
            _player.Screenshot("05-slot-selected");
        }

        [Test]
        public void HOSTUI_005_WheelScrollsTheList_ThenRow150IsClicked()
        {
            const int target = 149; // 150번째 줄
            UiScrollList list = Sample.List;
            _player.MovePointerTo("list");
            _player.Wheel(-1);
            Assert.That(list.ScrollOffset, Is.EqualTo(list.RowHeight * 3), "휠 한 칸에 세 줄");
            _player.Screenshot("06-list-wheel-one-notch");

            UiRect row = default;
            for (int i = 0; i < 100 && !(list.TryGetRowRect(target, out row) && row.H >= list.RowHeight); i++) _player.Wheel(-1);
            Assert.That(row.H, Is.EqualTo(list.RowHeight), "150번째 줄이 온전히 보일 때까지 굴렸어야 한다");

            _player.ClickAt(row.CenterX, row.CenterY);
            Assert.That(Sample.LastRow, Is.EqualTo(target));
            Assert.That(Sample.Status.Text, Is.EqualTo("150번째 줄을 골랐습니다."));
            _player.Screenshot("07-list-row-150-clicked");

            _player.Wheel(1000);
            Assert.That(list.ScrollOffset, Is.EqualTo(0), "위 끝에서 멈춘다");
        }

        [Test]
        public void HOSTUI_006_DraggingTheList_Scrolls_AndDoesNotClickARow()
        {
            UiScrollList list = Sample.List;
            UiRect view = list.View;
            _player.MovePointer(view.CenterX, view.Bottom - 60);
            _player.PressPointer();
            _player.DragPointerBy(0, -480);
            _player.Screenshot("08-list-dragging");
            Assert.That(list.ScrollOffset, Is.EqualTo(480).Within(2), "끌어 올린 만큼 내용이 올라간다");
            _player.ReleasePointer();
            Assert.That(Sample.LastRow, Is.EqualTo(-1), "끌기는 클릭이 아니다");
            Assert.That(list.ScrollOffset, Is.EqualTo(480).Within(2));

            _player.MovePointer(view.CenterX, view.Y + 60);
            _player.PressPointer();
            _player.DragPointerBy(0, 2000);
            _player.ReleasePointer();
            Assert.That(list.ScrollOffset, Is.EqualTo(0), "위 끝을 넘겨 끌어도 멈춘다");
        }

        [Test]
        public void HOSTUI_007_KeyboardMovesFocus_AndEnterRunsTheFocusedElement()
        {
            _player.PressKey(TestKey.Tab);
            Assert.That(_player.Game.Ui.Focused, Is.SameAs(Sample.Ok), "Tab은 첫 요소부터");
            _player.PressKey(TestKey.Enter);
            Assert.That(Sample.OkClicks, Is.EqualTo(1));

            _player.PressKey(TestKey.Tab);
            Assert.That(_player.Game.Ui.Focused, Is.SameAs(Sample.Reset));
            _player.PressKey(TestKey.Tab);
            Assert.That(_player.Game.Ui.Focused, Is.SameAs(Sample.Name), "비활성 버튼은 건너뛴다");
            _player.KeyDown(TestKey.LeftShift);
            _player.PressKey(TestKey.Tab, 2);
            _player.KeyUp(TestKey.LeftShift);
            Assert.That(_player.Game.Ui.Focused, Is.SameAs(Sample.Ok), "Shift+Tab은 거꾸로");

            _player.PressKey(TestKey.Right);
            Assert.That(_player.Game.Ui.Focused, Is.SameAs(Sample.Slots[0]));
            _player.PressKey(TestKey.Right, 2);
            _player.PressKey(TestKey.Down);
            Assert.That(_player.Game.Ui.Focused, Is.SameAs(Sample.Slots[7]));
            _player.Screenshot("09-keyboard-focus-on-slot");
            _player.PressKey(TestKey.Enter);
            Assert.That(Sample.LastSlot, Is.EqualTo(7));

            _player.PressKey(TestKey.Right, 3);
            Assert.That(_player.Game.Ui.Focused, Is.SameAs(Sample.List), "칸 격자 오른쪽 끝에서 목록으로");
            _player.PressKey(TestKey.Down, 20);
            Assert.That(Sample.List.SelectedIndex, Is.EqualTo(19));
            Assert.That(Sample.List.ScrollOffset, Is.GreaterThan(0), "고른 줄을 따라 스크롤된다");
            _player.PressKey(TestKey.Enter);
            Assert.That(Sample.LastRow, Is.EqualTo(19));
            _player.Screenshot("10-keyboard-list-row-20");
        }

        [Test]
        public void HOSTUI_008_NameField_TakesKoreanText_Backspace_AndEnter()
        {
            _player.TypeText("가");
            Assert.That(Sample.Name.Text, Is.EqualTo(""), "칸을 고르기 전에는 글자가 들어가지 않는다");
            Assert.That(_player.Game.TextInputActive, Is.False);

            _player.Click("name");
            Assert.That(_player.Game.Ui.Focused, Is.SameAs(Sample.Name));
            Assert.That(_player.Game.TextInputActive, Is.True);

            _player.TypeText("하");
            _player.Compose("늘");
            Assert.That(Sample.Name.Composition, Is.EqualTo("늘"));
            _player.Screenshot("11-name-composing");
            _player.Compose("");
            _player.TypeText("늘이");
            Assert.That(Sample.Name.Text, Is.EqualTo("하늘이"));
            Assert.That(Sample.Name.Composition, Is.EqualTo(""));
            _player.Screenshot("12-name-typed");

            _player.PressKey(TestKey.Backspace);
            Assert.That(Sample.Name.Text, Is.EqualTo("하늘"));

            _player.PressKey(TestKey.Left);
            Assert.That(_player.Game.Ui.Focused, Is.SameAs(Sample.Name), "글자 칸 안에서 좌우 키는 포커스를 옮기지 않는다");

            _player.PressKey(TestKey.Enter);
            Assert.That(Sample.SubmittedName, Is.EqualTo("하늘"));
            Assert.That(Sample.Status.Text, Is.EqualTo("이름: 하늘"));
            Assert.That(_player.Game.TextInputActive, Is.False, "Enter로 확정하면 입력기가 꺼진다");
            _player.Screenshot("13-name-submitted");

            _player.TypeText("가");
            Assert.That(Sample.Name.Text, Is.EqualTo("하늘"));
        }

        [Test]
        public void HOSTUI_009_ClickingOutsideTheNameField_EndsTextInput()
        {
            _player.Click("name");
            _player.TypeText("봄");
            _player.Click("title");
            Assert.That(_player.Game.TextInputActive, Is.False);
            _player.TypeText("비");
            Assert.That(Sample.Name.Text, Is.EqualTo("봄"));
        }

        [Test]
        public void HOSTUI_010_FullHdWindow_UsesTileScaleThree_AndStillClicks()
        {
            _player.Game.Resize(1920, 1080);
            _player.Frames(3);
            Assert.That(_player.Game.Fit.Scale, Is.EqualTo(1f).Within(1e-4));
            Assert.That(_player.Game.Fit.TileScale, Is.EqualTo(3));
            Assert.That(_player.Node("panel").Rect, Is.EqualTo(new UiRect(60, 60, 1800, 960)));
            _player.Click("name");
            _player.TypeText("한글이름");
            _player.Click("ok");
            Assert.That(Sample.OkClicks, Is.EqualTo(1));
            _player.MovePointerTo("reset");
            _player.Screenshot("14-full-hd");
        }

        [Test]
        public void HOSTUI_011_ResetButton_ReturnsTheScreenToItsStartState()
        {
            _player.Click("ok");
            _player.Click("slot3");
            _player.MovePointerTo("list");
            _player.Wheel(-4);
            _player.Click("reset");
            Assert.That(Sample.OkClicks, Is.EqualTo(0));
            Assert.That(Sample.LastSlot, Is.EqualTo(-1));
            Assert.That(Sample.List.ScrollOffset, Is.EqualTo(0));
            Assert.That(Sample.Status.Text, Is.EqualTo("처음 상태로 돌렸습니다."));
        }
    }

    /// <summary>창 없이 도는 Host 쪽 테스트.</summary>
    public sealed class HostOptionsTests
    {
        private static string RepoRoot() => HostOptions.Resolve(new[] { "--sample" }).RepoRoot;

        [Test]
        public void ArgumentsWinOverEnvironment()
        {
            string root = RepoRoot();
            HostOptions o = HostOptions.Resolve(new[] { "--root", root, "--size", "1920x1080", "--sample", "--evidence", "x" },
                name => name == HostOptions.RootVariable ? "Z:/nowhere" : null);
            Assert.That(o.RepoRoot, Is.EqualTo(root));
            Assert.That((o.Width, o.Height, o.Sample, o.EvidenceDir), Is.EqualTo((1920, 1080, true, "x")));
        }

        [Test]
        public void EnvironmentIsUsedWhenThereIsNoArgument()
        {
            string root = RepoRoot();
            HostOptions o = HostOptions.Resolve(Array.Empty<string>(), name => name == HostOptions.RootVariable ? root : null, Path.GetTempPath());
            Assert.That(o.RepoRoot, Is.EqualTo(root));
            Assert.That(o.EvidenceDir, Is.EqualTo(Path.Combine(root, "output", "host-ui-evidence")));
        }

        [Test]
        public void UnknownRoot_SaysHowToGiveIt()
        {
            var e = Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(Array.Empty<string>(), name => null, Path.GetTempPath()));
            Assert.That(e.Message, Does.Contain("--root").And.Contain(HostOptions.RootVariable));
        }

        [Test]
        public void WrongRoot_NamesTheMissingFile()
        {
            var e = Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--root", Path.GetTempPath() }, name => null));
            Assert.That(e.Message, Does.Contain("Modern_UI_Style_2.png"));
        }

        [Test]
        public void MissingFontFile_IsAClearError()
        {
            var e = Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--font", "no-such-font.ttf" }));
            Assert.That(e.Message, Does.Contain("no-such-font.ttf"));
        }

        [TestCase("--size", "big")]
        [TestCase("--size", "10x10")]
        [TestCase("--wat", "1")]
        public void BadArguments_AreRejected(string name, string value)
        {
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { name, value }));
        }
    }
}
