using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Interaction;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests
{
    /// <summary>시험용 화면: 가운데 버튼 둘, 받은 알림과 키를 적어 둔다.</summary>
    internal sealed class ProbeScreen : GameScreen
    {
        public readonly List<string> Log = new List<string>();
        public UiButton First, Second;
        public UiTextField Field;
        public bool ClaimInteract;
        public readonly bool WithField;

        public ProbeScreen(ScreenServices services, bool withField = false) : base(services) { WithField = withField; }

        protected override UiNode Build()
        {
            var root = new UiPanel();
            root.Place(0.5f, 0.5f, -200, -150, 400, 300);
            First = root.Add(new UiButton { Id = "first", Text = "1" });
            First.Place(0, 0, 20, 20, 160, 60);
            Second = root.Add(new UiButton { Id = "second", Text = "2" });
            Second.Place(0, 0, 220, 20, 160, 60);
            First.Clicked += () => Log.Add("click:first");
            Second.Clicked += () => Log.Add("click:second");
            if (WithField)
            {
                Field = root.Add(new UiTextField { Id = "field" });
                Field.Place(0, 0, 20, 200, 360, 60);
            }
            return root;
        }

        protected override void OnOpened(ScreenOpenContext context) => Log.Add("open" + (context.Interaction.HasValue ? ":" + context.Interaction.Value.TargetId : ""));
        protected override void OnClosed() => Log.Add("close");
        protected override void OnChanged(string topic) => Log.Add("changed:" + topic);
        protected override bool OnKey(ScreenKey key, bool shift)
        {
            if (ClaimInteract && key == ScreenKey.Interact) { Log.Add("key:interact"); return true; }
            return false;
        }
    }

    public sealed class ScreenStackTests
    {
        private ScreenStack _stack;
        private ScreenServices _services;

        [SetUp]
        public void Make()
        {
            var registry = new ScreenRegistry();
            registry.Add("hud", ScreenLayer.Hud, false, s => new ProbeScreen(s));
            registry.Add("bag", ScreenLayer.Panel, false, s => new ProbeScreen(s)).OpenedBy(ScreenKey.Inventory);
            registry.Add("settings", ScreenLayer.Panel, false, s => new ProbeScreen(s)).OpenedBy(ScreenKey.Escape);
            registry.Add("search", ScreenLayer.Panel, false, s => new ProbeScreen(s, withField: true)).OpenedBy(ScreenKey.Status);
            registry.Add("event", ScreenLayer.Modal, true, s => new ProbeScreen(s)).OpenedByInteraction("daily-event");
            registry.Add("result", ScreenLayer.Modal, true, s => new ProbeScreen(s));
            registry.Add("talk", ScreenLayer.Modal, false, s => new ProbeScreen(s)).OpenedByInteraction("npc");
            registry.Add("toast", ScreenLayer.Notice, false, s => new ProbeScreen(s));
            _services = new ScreenServices(TestData.Text(), null);
            _stack = new ScreenStack(registry, _services);
            _stack.Layout(1920, 1080);
        }

        private ProbeScreen Screen(string id) => (ProbeScreen)_stack.Find(id);

        private void Click(float x, float y)
        {
            _stack.Pointer(x, y, false, 0);
            _stack.Pointer(x, y, true, 0);
            _stack.Pointer(x, y, false, 0);
        }

        [Test]
        public void Open_BuildsTheScreenOnce_AndKeepsItAfterClosing()
        {
            GameScreen first = _stack.Open("bag");
            _stack.Close("bag");
            GameScreen again = _stack.Open("bag");
            Assert.That(again, Is.SameAs(first));
            Assert.That(Screen("bag").Log, Is.EqualTo(new[] { "open", "close", "open" }));
        }

        [Test]
        public void Panels_AreExclusive_ModalsStack()
        {
            _stack.Open("bag");
            _stack.Open("settings");
            Assert.That(_stack.IsOpen("bag"), Is.False);
            Assert.That(_stack.IsOpen("settings"), Is.True);
            Assert.That(Screen("bag").Log.Last(), Is.EqualTo("close"));

            _stack.Open("event");
            _stack.Open("result");
            Assert.That(_stack.OpenScreens.Select(s => s.Id), Is.EqualTo(new[] { "settings", "event", "result" }));
        }

        [Test]
        public void Layers_DrawBottomToTop_RegardlessOfOpenOrder()
        {
            _stack.Open("toast");
            _stack.Open("event");
            _stack.Open("bag");
            _stack.Open("hud");
            Assert.That(_stack.OpenScreens.Select(s => s.Id), Is.EqualTo(new[] { "hud", "bag", "event", "toast" }));
            Assert.That(_stack.Top.Id, Is.EqualTo("toast"));
        }

        [Test]
        public void MovementBlocked_OnlyWhileABlockingScreenIsOpen()
        {
            _stack.Open("settings");
            _stack.Open("talk");
            Assert.That(_stack.MovementBlocked, Is.False, "설정·대화는 이동을 막지 않는다(Unity 판과 같음)");
            _stack.Open("event");
            Assert.That(_stack.MovementBlocked, Is.True);
            _stack.Close("event");
            Assert.That(_stack.MovementBlocked, Is.False);
        }

        [Test]
        public void BoundKey_TogglesItsPanel()
        {
            Assert.That(_stack.Key(ScreenKey.Escape, false), Is.True);
            Assert.That(_stack.IsOpen("settings"), Is.True);
            Assert.That(_stack.Key(ScreenKey.Inventory, false), Is.True);
            Assert.That(_stack.IsOpen("settings"), Is.False, "큰 패널은 하나만");
            Assert.That(_stack.IsOpen("bag"), Is.True);
            _stack.Key(ScreenKey.Inventory, false);
            Assert.That(_stack.IsOpen("bag"), Is.False);
        }

        [Test]
        public void BoundKeys_AreIgnoredWhileMovementIsBlocked()
        {
            _stack.Open("event");
            _stack.Key(ScreenKey.Escape, false);
            Assert.That(_stack.IsOpen("settings"), Is.False, "Unity 판: 입력을 막는 화면이 떠 있으면 단축키를 받지 않는다");
        }

        [Test]
        public void NonBlockingScreen_DoesNotTakePlainArrows_ButShiftArrowsMoveFocusAndEnterRuns()
        {
            _stack.Open("settings");
            foreach (ScreenKey arrow in new[] { ScreenKey.Up, ScreenKey.Down, ScreenKey.Left, ScreenKey.Right })
                Assert.That(_stack.Key(arrow, false), Is.False, arrow + "는 캐릭터로 간다");
            Assert.That(_stack.Tree.Focused, Is.Null);
            Assert.That(_stack.MovementBlockedWhile(false), Is.False);
            Assert.That(_stack.MovementBlockedWhile(true), Is.True, "Shift를 누르고 있는 동안은 방향 키가 포커스를 옮기므로 걷지 않는다");

            Assert.That(_stack.Key(ScreenKey.Right, true), Is.True);
            Assert.That(_stack.Tree.Focused, Is.SameAs(Screen("settings").First));
            _stack.Key(ScreenKey.Right, true);
            Assert.That(_stack.Tree.Focused, Is.SameAs(Screen("settings").Second));
            _stack.Key(ScreenKey.Left, true);
            Assert.That(_stack.Tree.Focused, Is.SameAs(Screen("settings").First));
            Assert.That(_stack.Key(ScreenKey.Enter, false), Is.True);
            Assert.That(Screen("settings").Log, Does.Contain("click:first"));
        }

        [Test]
        public void Tab_DoesNotMoveFocus_ItIsLeftForItsKeyBinding()
        {
            _stack.Open("settings");
            Assert.That(_stack.Key(ScreenKey.Tab, false), Is.False, "Tab이 배정된 화면이 없으면 아무 일도 없다");
            Assert.That(_stack.Tree.Focused, Is.Null);
        }

        [Test]
        public void Interaction_IsReported_EvenWhenNoScreenIsRegisteredForIt()
        {
            var seen = new List<(string, GameScreen)>();
            _stack.Interacted += (e, s) => seen.Add((e.Kind, s));
            _stack.Interaction(new InteractionEvent("Town", "location-activity", "location.library"));
            _stack.Interaction(new InteractionEvent("Town", "npc", "npc.first-guide"));
            Assert.That(seen[0], Is.EqualTo(("location-activity", (GameScreen)null)));
            Assert.That(seen[1].Item2, Is.SameAs(_stack.Find("talk")));
        }

        [Test]
        public void EnterMap_TellsOpenScreensOnce()
        {
            _stack.Open("settings");
            _stack.EnterMap("House");
            _stack.EnterMap("House");
            Assert.That(_stack.MapId, Is.EqualTo("House"));
            Assert.That(Screen("settings").Log.Count(l => l == "changed:map"), Is.EqualTo(1));
        }

        [Test]
        public void InteractionTarget_RaisesOnlyWhenItChanges()
        {
            int raised = 0;
            _stack.TargetChanged += () => raised++;
            var t = new InteractionTargetView("npc", "npc.first-guide", 100, 200, 64);
            _stack.SetInteractionTarget(t);
            _stack.SetInteractionTarget(t);
            _stack.SetInteractionTarget(null);
            Assert.That(raised, Is.EqualTo(2));
        }

        [Test]
        public void OpenHud_OpensEveryHudScreen_AndNothingElse()
        {
            _stack.OpenHud();
            Assert.That(_stack.OpenScreens.Select(s => s.Id), Is.EqualTo(new[] { "hud" }));
        }

        [Test]
        public void Listener_IsAttachedWhenTheStackIsMade()
        {
            var registry = new ScreenRegistry();
            ScreenStack seen = null;
            registry.AddListener(s => seen = s.Screens);
            var stack = new ScreenStack(registry, new ScreenServices(TestData.Text(), null));
            Assert.That(seen, Is.SameAs(stack));
        }

        [Test]
        public void BlockingScreen_TakesArrowKeys()
        {
            _stack.Open("event");
            Assert.That(_stack.Key(ScreenKey.Right, false), Is.True);
            Assert.That(_stack.Tree.Focused, Is.SameAs(Screen("event").First));
            _stack.Key(ScreenKey.Right, false);
            Assert.That(_stack.Tree.Focused, Is.SameAs(Screen("event").Second));
        }

        [Test]
        public void Focus_StaysInTheTopScreen()
        {
            _stack.Open("settings");
            _stack.Open("talk");
            _stack.Key(ScreenKey.Right, true);
            _stack.Key(ScreenKey.Right, true);
            _stack.Key(ScreenKey.Right, true);
            Assert.That(_stack.Tree.Focused, Is.Not.Null);
            Assert.That(new[] { Screen("talk").First, Screen("talk").Second }, Does.Contain(_stack.Tree.Focused), "포커스는 맨 위 화면 안에만 있다");
        }

        [Test]
        public void TextFieldFocus_SkipsKeyBindings_AndBlocksMovement()
        {
            _stack.Open("search");
            ProbeScreen search = Screen("search");
            Click(search.Field.Rect.CenterX, search.Field.Rect.CenterY);
            Assert.That(_stack.TextInputFocused, Is.True);
            Assert.That(_stack.MovementBlocked, Is.True, "글자를 치는 동안은 걷지 않는다(WASD가 글자다)");

            Assert.That(_stack.Key(ScreenKey.Inventory, false), Is.True);
            Assert.That(_stack.IsOpen("bag"), Is.False, "글자 칸에 포커스가 있으면 키 배정을 건너뛴다");
            _stack.TextInput("가");
            Assert.That(search.Field.Text, Is.EqualTo("가"));

            _stack.Key(ScreenKey.Escape, false);
            Assert.That(_stack.TextInputFocused, Is.False, "Esc는 먼저 글자 칸을 놓는다");
            Assert.That(_stack.IsOpen("search"), Is.True);
        }

        [Test]
        public void ScreenClaimedKey_WinsOverEverythingElse()
        {
            _stack.Open("talk");
            Screen("talk").ClaimInteract = true;
            Assert.That(_stack.Key(ScreenKey.Interact, false), Is.True);
            Assert.That(Screen("talk").Log, Does.Contain("key:interact"));
        }

        [Test]
        public void Interact_IsNotTakenWhenNoScreenClaimsIt()
        {
            _stack.Open("settings");
            Assert.That(_stack.Key(ScreenKey.Interact, false), Is.False, "월드로 간다");
        }

        [Test]
        public void Interaction_OpensTheScreenRegisteredForItsKind()
        {
            Assert.That(_stack.Interaction(new InteractionEvent("Town", "npc", "npc.first-guide")), Is.True);
            Assert.That(_stack.IsOpen("talk"), Is.True);
            Assert.That(Screen("talk").Log, Is.EqualTo(new[] { "open:npc.first-guide" }));
            Assert.That(_stack.Interaction(new InteractionEvent("Town", "door", "x")), Is.False, "등록 안 된 종류는 무시");
        }

        [Test]
        public void Changed_GoesOnlyToOpenScreens()
        {
            _stack.Open("bag");
            _stack.Close("bag");
            _stack.Open("settings");
            _stack.NotifyChanged(ChangeTopics.Inventory);
            Assert.That(Screen("settings").Log.Last(), Is.EqualTo("changed:inventory"));
            Assert.That(Screen("bag").Log.Last(), Is.EqualTo("close"));
        }

        [Test]
        public void LanguageChange_IsSentAsAChange()
        {
            _stack.Open("settings");
            _services.Text.Language = "en";
            Assert.That(Screen("settings").Log.Last(), Is.EqualTo("changed:language"));
        }

        [Test]
        public void Pointer_HitsTheTopScreen_AndModalsBlockWhatIsBelow()
        {
            _stack.Open("settings");
            ProbeScreen settings = Screen("settings");
            UiRect first = settings.First.Rect;
            Click(first.CenterX, first.CenterY);
            Assert.That(settings.Log, Does.Contain("click:first"));

            _stack.Open("result"); // 같은 자리에 같은 크기의 모달
            ProbeScreen result = Screen("result");
            Click(first.CenterX, first.CenterY);
            Assert.That(result.Log, Does.Contain("click:first"));
            Click(10, 10); // 모달 밖: 아래 화면으로 가지 않는다
            Assert.That(settings.Log.Count(l => l == "click:first"), Is.EqualTo(1));
        }

        [Test]
        public void ClosingTheFocusedScreen_ClearsFocus()
        {
            _stack.Open("settings");
            _stack.Key(ScreenKey.Right, true);
            Assert.That(_stack.Tree.Focused, Is.Not.Null);
            _stack.Close("settings");
            Assert.That(_stack.Tree.Focused, Is.Null);
        }

        [Test]
        public void Emit_DrawsOpenScreensOnly()
        {
            _stack.Open("bag");
            _stack.Close("bag");
            var list = new DrawList();
            _stack.Emit(list, new TestMeasurer());
            Assert.That(list.Commands, Is.Empty);
            _stack.Open("settings");
            _stack.Emit(list, new TestMeasurer());
            Assert.That(list.Commands.Count(c => c.Kind == DrawKind.Panel), Is.EqualTo(3), "패널 + 버튼 둘");
        }

        [Test]
        public void UnknownScreen_Throws()
        {
            Assert.That(() => _stack.Open("nope"), Throws.InvalidOperationException);
        }
    }

    public sealed class ScreenRegistryTests
    {
        [Test]
        public void KeyAndInteraction_CanBeAssignedOnlyOnce()
        {
            var r = new ScreenRegistry();
            r.Add("a", ScreenLayer.Panel, false, s => null).OpenedBy(ScreenKey.Escape).OpenedByInteraction("npc");
            Assert.That(() => r.Add("b", ScreenLayer.Panel, false, s => null).OpenedBy(ScreenKey.Escape), Throws.InvalidOperationException);
            Assert.That(() => r.Add("c", ScreenLayer.Panel, false, s => null).OpenedByInteraction("npc"), Throws.InvalidOperationException);
            Assert.That(() => r.Add("a", ScreenLayer.Panel, false, s => null), Throws.InvalidOperationException);
        }

        [Test]
        public void Catalog_RegistersSettingsOnEscape_AsANonBlockingPanel()
        {
            var r = new ScreenRegistry();
            ScreenCatalog.Register(r);
            Assert.That(r.TryGetByKey(ScreenKey.Escape, out string id), Is.True);
            Assert.That(id, Is.EqualTo(ScreenCatalog.Settings));
            Assert.That(r.TryGet(id, out ScreenEntry entry), Is.True);
            Assert.That((entry.Layer, entry.BlocksMovement), Is.EqualTo((ScreenLayer.Panel, false)));
        }
    }
}
