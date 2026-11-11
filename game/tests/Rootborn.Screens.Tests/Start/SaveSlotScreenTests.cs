using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Start
{
    public sealed class SaveSlotScreenTests
    {
        [Test]
        public void Model_HasTheThreeUiSlots_EmptyOnesOfferANewStory()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-1", "하늘");
            SaveSlotSelectModel model = SaveSlotSelectModel.From(t.Menu);
            Assert.That(model.Rows.Select(r => r.SlotId), Is.EqualTo(new[] { "slot-0", "slot-1", "slot-2" }));
            Assert.That(model.Rows.Select(r => r.Exists), Is.EqualTo(new[] { false, true, false }));
            Assert.That(model.Rows[0].IconKey, Is.EqualTo("plan"));
            Assert.That(model.Rows[1].IconKey, Is.EqualTo("journal"));
            Assert.That(model.Rows[1].DisplayName, Is.EqualTo("하늘"));
            Assert.That(model.Rows[1].UpdatedAtUtcTicks, Is.EqualTo(t.Now));
            Assert.That(model.Rows[1].Look.Id, Is.EqualTo("playground_short_brown_casual"), "첫 단계(첫날) 몸의 고른 겉모습");
        }

        [Test]
        public void Screen_FillsTheFrameAndCards_AsTheUnityPanel()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-1", "하늘");
            t.OpenSlots();
            Assert.That(t.Stack.MovementBlocked, Is.True, "시작 화면에서는 방향 키가 버튼으로 간다");
            Assert.That(t.Node(SaveSlotScreen.RootId).Rect, Is.EqualTo(new UiRect(0, 0, 1920, 1080)));
            Assert.That(t.Node(SaveSlotScreen.FrameId).Rect, Is.EqualTo(new UiRect(240, 120, 1440, 840)));
            // Unity: 위 가운데 기준 (i-1)*445, -447에 414x470.
            for (int i = 0; i < 3; i++)
                Assert.That(t.Node(SaveSlotScreen.CardId(i)).Rect, Is.EqualTo(new UiRect(960 + (i - 1) * 445 - 207, 120 + 447 - 235, 414, 470)));
            Assert.That(t.LabelText(SaveSlotScreen.TitleId), Is.EqualTo("이어갈 이야기 선택"));

            Assert.That(t.LabelText(SaveSlotScreen.CardTitleId(0)), Is.EqualTo("새로운 이야기"));
            Assert.That(t.Node(SaveSlotScreen.NewGameId(0)).IsActive, Is.True);
            Assert.That(t.Node(SaveSlotScreen.ContinueId(0)).Visible, Is.False);
            Assert.That(t.Node(SaveSlotScreen.DeleteId(0)).Visible, Is.False);
            Assert.That(((UiImage)t.Node(SaveSlotScreen.CardIconId(0))).Image, Is.EqualTo("icon:plan"));

            Assert.That(t.LabelText(SaveSlotScreen.CardTitleId(1)), Is.EqualTo("하늘"));
            Assert.That(t.Node(SaveSlotScreen.NewGameId(1)).Visible, Is.False);
            Assert.That(((UiButton)t.Node(SaveSlotScreen.ContinueId(1))).Text, Is.EqualTo("이어서 시작"));
            Assert.That(((UiButton)t.Node(SaveSlotScreen.DeleteId(1))).Text, Is.EqualTo("삭제"));
            var preview = (CharacterPreview)t.Node(SaveSlotScreen.CardPreviewId(1));
            Assert.That(preview.Frames.Select(f => f.Image), Is.EqualTo(new[] { "sheet:body_playground", "sheet:outfit_playground_casual", "sheet:head_playground_short_brown" }));
            Assert.That(t.Node(SaveSlotScreen.ErrorId).Visible, Is.False);
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.UsedKeys());
        }

        [Test]
        public void Continue_AsksToEnterTheSlot_FailureShowsWhy()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-2", "바다");
            t.OpenSlots();
            t.Click(SaveSlotScreen.ContinueId(2));
            Assert.That(t.Entries, Is.EqualTo(new[] { new StartEntry("slot-2", false) }));
            Assert.That(t.Node(SaveSlotScreen.ErrorId).Visible, Is.False);

            t.EnterSucceeds = false;
            t.Click(SaveSlotScreen.ContinueId(2));
            Assert.That(t.Node(SaveSlotScreen.ErrorId).Visible, Is.True);
            Assert.That(t.LabelText(SaveSlotScreen.ErrorId), Is.EqualTo(t.Text.Get("world.entry.failed")));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
        }

        [Test]
        public void Delete_AsksFirst_CancelKeepsTheStory_ConfirmRemovesTheSlotFolder()
        {
            using var t = new StartMenuFixture();
            t.WriteStory("slot-0", "하늘");
            t.Store.Write("slot-0", SaveFileNames.QuestLog, new QuestLogPlaceholder());
            t.OpenSlots();
            t.Click(SaveSlotScreen.DeleteId(0));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DeleteSlot), Is.True);
            Assert.That(t.Stack.Tree.Focused?.Id, Is.EqualTo(DeleteSlotScreen.CancelId), "Unity: 취소를 먼저 고른다");
            Assert.That(t.LabelText(DeleteSlotScreen.PromptId), Is.EqualTo("이 이야기를 지울까요?"));
            Assert.That(t.LabelText(DeleteSlotScreen.NameId), Is.EqualTo("하늘"));
            Assert.That(t.LabelText(DeleteSlotScreen.WarningId), Is.EqualTo("삭제한 기록은 되돌릴 수 없어요."));
            UiRect continueRect = t.Node(SaveSlotScreen.ContinueId(0)).Rect;
            Assert.That(t.Stack.Tree.HitTest(continueRect.CenterX, continueRect.CenterY), Is.Not.SameAs(t.Node(SaveSlotScreen.ContinueId(0))), "확인 창이 아래 버튼을 가린다");

            t.Click(DeleteSlotScreen.CancelId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.DeleteSlot), Is.False);
            Assert.That(File.Exists(Path.Combine(t.Saves, "slot-0", SaveFileNames.Metadata)), Is.True);

            t.Click(SaveSlotScreen.DeleteId(0));
            t.Click(DeleteSlotScreen.ConfirmId);
            Assert.That(Directory.Exists(Path.Combine(t.Saves, "slot-0")), Is.False, "슬롯 폴더 통째로");
            Assert.That(t.Node(SaveSlotScreen.NewGameId(0)).IsActive, Is.True, "지운 슬롯은 새 이야기 칸이 된다");
            Assert.That(t.LabelText(SaveSlotScreen.CardTitleId(0)), Is.EqualTo("새로운 이야기"));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.UsedKeys());
        }

        [Test]
        public void UnreadableSlotInfo_LooksEmpty_AsInUnity()
        {
            using var t = new StartMenuFixture();
            Directory.CreateDirectory(Path.Combine(t.Saves, "slot-1"));
            File.WriteAllText(Path.Combine(t.Saves, "slot-1", SaveFileNames.Metadata), "{ 깨진");
            SaveSlotSummary summary = t.Menu.Slots.Read("slot-1");
            Assert.That(summary.Exists, Is.False);
            Assert.That(summary.Unreadable, Is.True);
            t.OpenSlots();
            Assert.That(t.Node(SaveSlotScreen.NewGameId(1)).IsActive, Is.True);
        }

        [Test]
        public void Back_QuitsBecauseNothingIsBehindTheSlotScreen()
        {
            using var t = new StartMenuFixture();
            t.OpenSlots();
            Assert.That(((UiButton)t.Node(SaveSlotScreen.BackId)).Text, Is.EqualTo("돌아가기"));
            t.Click(SaveSlotScreen.BackId);
            Assert.That(t.QuitCount, Is.EqualTo(1));
        }

        [Test]
        public void ArrowKeysMoveBetweenButtons_EnterPresses()
        {
            using var t = new StartMenuFixture();
            t.OpenSlots();
            t.Stack.Tree.SetFocus(t.Node(SaveSlotScreen.NewGameId(0)));
            Assert.That(t.Stack.Key(ScreenKey.Right, false), Is.True, "이동을 막는 화면이라 방향 키를 화면이 가져간다");
            Assert.That(t.Stack.Tree.Focused?.Id, Is.EqualTo(SaveSlotScreen.NewGameId(1)));
            Assert.That(t.Stack.Key(ScreenKey.Enter, false), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.CharacterCreator), Is.True);
            Assert.That(t.Screen<CharacterCreatorScreen>(ScreenCatalog.CharacterCreator).SlotId, Is.EqualTo("slot-1"));
        }

        [Test]
        public void LanguageChange_RewritesTheOpenScreen()
        {
            using var t = new StartMenuFixture();
            t.OpenSlots();
            t.Text.Language = "en";
            Assert.That(t.LabelText(SaveSlotScreen.TitleId), Is.EqualTo("Choose your story"));
            Assert.That(((UiButton)t.Node(SaveSlotScreen.NewGameId(0))).Text, Is.EqualTo("New story"));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.UsedKeys());
        }

        private sealed class QuestLogPlaceholder { public int Version = 1; }
    }
}
