using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Start
{
    public sealed class CharacterCreatorScreenTests
    {
        private static CharacterCreatorScreen OpenCreator(StartMenuFixture t, int slot = 0)
        {
            t.OpenSlots();
            t.Click(SaveSlotScreen.NewGameId(slot));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.CharacterCreator), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotSelect), Is.False, "만들기 화면이 슬롯 화면을 대신한다(Unity: 슬롯 내용을 숨김)");
            return t.Screen<CharacterCreatorScreen>(ScreenCatalog.CharacterCreator);
        }

        [Test]
        public void Stages_ComeFromTheAppearanceData_YoungestFirst_FacilityOutfitsLeftOut()
        {
            CreatorLooks looks = CreatorLooks.Load(TestData.DataDirectory);
            Assert.That(looks.Stages.Select(s => s[0].BodyId), Is.EqualTo(new[] { "body_playground", "body_school", "body_adult" }));
            Assert.That(looks.Stages.Select(s => s.Select(l => l.Id)), Is.EqualTo(new[]
            {
                new[] { "playground_short_brown_casual" },
                new[] { "school_short_brown_school_casual", "school_short_brown_school_uniform" },
                new[] { "adult_short_brown_adult_casual", "adult_short_brown_office" },
            }));
        }

        [Test]
        public void Screen_ShowsNameHairAndOneRowPerStage_AsTheUnityPanel()
        {
            using var t = new StartMenuFixture();
            CharacterCreatorScreen screen = OpenCreator(t);
            Assert.That(t.Node(CharacterCreatorScreen.PanelId).Rect, Is.EqualTo(new UiRect(960 - 648, 540 - 432, 1296, 864)));
            Assert.That(t.Node(CharacterCreatorScreen.NameFieldId).Rect, Is.EqualTo(new UiRect(960 + 300 - 240, 540 - 230 - 28, 480, 56)));
            Assert.That(t.LabelText(CharacterCreatorScreen.TitleId), Is.EqualTo("캐릭터 만들기"));
            Assert.That(t.LabelText(CharacterCreatorScreen.HairChoiceId), Is.EqualTo("짧은 갈색 머리"));
            Assert.That(new[] { 0, 1, 2 }.Select(i => t.LabelText(CharacterCreatorScreen.StageLabelId(i))), Is.EqualTo(new[] { "어릴 때", "학교 다닐 때", "어른이 되면" }));
            Assert.That(new[] { 0, 1, 2 }.Select(i => t.LabelText(CharacterCreatorScreen.StageChoiceId(i))), Is.EqualTo(new[] { "평상복", "사복", "캐주얼" }));
            Assert.That(t.Node(CharacterCreatorScreen.StagePreviousId(0)).Visible, Is.False, "고를 것이 하나면 화살표가 없다");
            Assert.That(t.Node(CharacterCreatorScreen.StageNextId(1)).IsActive, Is.True);
            Assert.That(t.LabelText(CharacterCreatorScreen.CaptionId), Is.EqualTo("어릴 때 모습"));
            var preview = (CharacterPreview)t.Node(CharacterCreatorScreen.CharacterId);
            Assert.That(preview.Frames.Select(f => f.Image), Is.EqualTo(new[] { "sheet:body_playground", "sheet:outfit_playground_casual", "sheet:head_playground_short_brown" }));
            Assert.That(((UiSelectableRow)t.Node(CharacterCreatorScreen.StageRowId(0))).Selected, Is.True);
            Assert.That(screen.PlayerName, Is.Empty, "Unity: 기본 이름은 빈 글자");
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.UsedKeys());
        }

        [Test]
        public void Preview_FitsTheBodyInItsBox_AndStandsEveryLayerOnTheSameFeet()
        {
            using var t = new StartMenuFixture();
            OpenCreator(t);
            var preview = (CharacterPreview)t.Node(CharacterCreatorScreen.CharacterId);
            var rects = new List<UiRect>();
            preview.LayerRects(rects);
            UiRect box = preview.Rect;
            Assert.That(box.W, Is.EqualTo(300f));
            // 몸 32x64 → 배율 300/64, 높이를 꽉 채우고 가로 가운데.
            Assert.That(rects[0].H, Is.EqualTo(300f).Within(0.01f));
            Assert.That(rects[0].CenterX, Is.EqualTo(box.CenterX).Within(0.01f));
            foreach (UiRect r in rects)
            {
                Assert.That(r.Bottom, Is.EqualTo(rects[0].Bottom).Within(0.01f), "발(아래 가운데)이 같은 자리");
                Assert.That(r.CenterX, Is.EqualTo(rects[0].CenterX).Within(0.01f));
            }
        }

        [Test]
        public void NextOnAStage_ChangesItsChoice_AndPreviewsThatStage_ClickingARowPreviewsIt()
        {
            using var t = new StartMenuFixture();
            CharacterCreatorScreen screen = OpenCreator(t);
            t.Click(CharacterCreatorScreen.StageNextId(1));
            Assert.That(screen.Model.ChoiceAt(1).Id, Is.EqualTo("school_short_brown_school_uniform"));
            Assert.That(t.LabelText(CharacterCreatorScreen.StageChoiceId(1)), Is.EqualTo("교복"));
            Assert.That(screen.Model.PreviewStage, Is.EqualTo(1));
            Assert.That(t.LabelText(CharacterCreatorScreen.CaptionId), Is.EqualTo("학교 다닐 때 모습"));
            var preview = (CharacterPreview)t.Node(CharacterCreatorScreen.CharacterId);
            Assert.That(preview.Frames[1].Image, Is.EqualTo("sheet:outfit_school_school_uniform"));
            t.Click(CharacterCreatorScreen.StageNextId(1));
            Assert.That(screen.Model.ChoiceAt(1).Id, Is.EqualTo("school_short_brown_school_casual"), "끝 다음은 처음");
            t.Click(CharacterCreatorScreen.StagePreviousId(1));
            Assert.That(screen.Model.ChoiceAt(1).Id, Is.EqualTo("school_short_brown_school_uniform"), "처음 앞은 끝");

            UiRect row = t.Node(CharacterCreatorScreen.StageRowId(2)).Rect;
            Assert.That(t.Stack.Tree.HitTest(row.X + 4, row.CenterY), Is.SameAs(t.Node(CharacterCreatorScreen.StageRowId(2))), "줄의 빈 곳");
            t.Stack.Pointer(row.X + 4, row.CenterY, false, 0);
            t.Stack.Pointer(row.X + 4, row.CenterY, true, 0);
            t.Stack.Pointer(row.X + 4, row.CenterY, false, 0);
            Assert.That(screen.Model.PreviewStage, Is.EqualTo(2));
            Assert.That(t.LabelText(CharacterCreatorScreen.CaptionId), Is.EqualTo("어른이 되면 모습"));
            Assert.That(screen.Model.ChoiceAt(2).Id, Is.EqualTo("adult_short_brown_adult_casual"), "줄을 누르면 고르기는 바뀌지 않는다");
        }

        [Test]
        public void Confirm_WritesTheNewStory_AndAsksToEnterAsANewGame()
        {
            using var t = new StartMenuFixture();
            CharacterCreatorScreen screen = OpenCreator(t, slot: 2);
            t.Click(CharacterCreatorScreen.NameFieldId);
            Assert.That(t.Stack.TextInputFocused, Is.True);
            t.Stack.TextComposition("ㅎ");
            t.Stack.TextComposition("하");
            t.Stack.TextInput("하");
            t.Stack.TextInput("늘");
            t.Stack.TextInput("  ");
            Assert.That(t.Stack.MovementBlocked, Is.True);
            t.Stack.Key(ScreenKey.Escape, false); // 글자 칸에서 나온다
            t.Click(CharacterCreatorScreen.StageNextId(1));
            t.Click(CharacterCreatorScreen.ConfirmId);

            Assert.That(t.Entries, Is.EqualTo(new[] { new StartEntry("slot-2", true) }));
            Assert.That(t.Store.TryRead("slot-2", SaveFileNames.Metadata, out SaveSlotMetadata saved), Is.True);
            Assert.That(saved.SlotId, Is.EqualTo("slot-2"));
            Assert.That(saved.DisplayName, Is.EqualTo("하늘"), "앞뒤 공백을 지운 이름");
            Assert.That(saved.Version, Is.EqualTo(SaveSlotMetadata.KnownVersion));
            Assert.That((saved.CreatedAtUtcTicks, saved.UpdatedAtUtcTicks), Is.EqualTo((t.Now, t.Now)));
            int hash = SaveSlotMetadata.StableHash("slot-2", 0);
            Assert.That(saved.WorldSeed, Is.EqualTo(unchecked(t.Ticks * 397) ^ hash), "Unity: 시각 값과 슬롯으로 만든 시드");
            Assert.That(saved.TileSeed, Is.EqualTo(unchecked(t.Ticks * 491) ^ (hash << 1)));
            CharacterAppearanceSnapshotSaveData snapshot = saved.CharacterAppearanceSnapshot;
            Assert.That((snapshot.visualKind, snapshot.catalogId, snapshot.appearanceDefinitionId), Is.EqualTo(("growing", "growing.creator", "playground_short_brown_casual")));
            Assert.That(snapshot.selectedParts.Select(p => (p.categoryId, p.partId)), Is.EqualTo(new[]
            {
                ("outfit:body_playground", "playground_short_brown_casual"),
                ("outfit:body_school", "school_short_brown_school_uniform"),
                ("outfit:body_adult", "adult_short_brown_adult_casual"),
            }));
        }

        [Test]
        public void NewStorySeeds_DependOnTheTime_NotOnlyTheSlot()
        {
            SaveSlotMetadata a = SaveSlots.CreateMetadata("slot-0", "", null, 1, 1000);
            SaveSlotMetadata b = SaveSlots.CreateMetadata("slot-0", "", null, 1, 2000);
            Assert.That(a.WorldSeed, Is.Not.EqualTo(b.WorldSeed));
            Assert.That(a.DisplayName, Is.EqualTo("slot-0"), "이름이 비면 슬롯 id(Unity)");
            Assert.Throws<System.ArgumentException>(() => SaveSlots.CreateMetadata("slot-7", "", null, 1, 1));
        }

        [Test]
        public void NameIsCutAtTwelveCharacters()
        {
            using var t = new StartMenuFixture();
            CharacterCreatorScreen screen = OpenCreator(t);
            t.Click(CharacterCreatorScreen.NameFieldId);
            t.Stack.TextInput("가나다라마바사아자차카타파하");
            Assert.That(screen.PlayerName, Is.EqualTo("가나다라마바사아자차카타"));
            Assert.That(CharacterCreatorModel.CleanName("  가나다라마바사아자차카타파  "), Is.EqualTo("가나다라마바사아자차카타"));
        }

        [Test]
        public void Back_ReturnsToTheSlots_WithoutWritingAnything()
        {
            using var t = new StartMenuFixture();
            OpenCreator(t, slot: 1);
            t.Click(CharacterCreatorScreen.BackId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.CharacterCreator), Is.False);
            Assert.That(Directory.Exists(Path.Combine(t.Saves, "slot-1")), Is.False);
            Assert.That(t.Entries, Is.Empty);
        }

        [Test]
        public void Random_PicksOnePerStageFromTheSeed()
        {
            using var t = new StartMenuFixture();
            CharacterCreatorScreen screen = OpenCreator(t);
            t.Click(CharacterCreatorScreen.RandomId);
            var expected = new System.Random(t.Ticks);
            for (int i = 0; i < screen.Model.StageCount; i++)
                Assert.That(screen.Model.Choices[i], Is.EqualTo(expected.Next(t.Looks.Stages[i].Count)));
        }

        [Test]
        public void ASlotThatGotAStoryMeanwhile_IsNotOverwritten_AndTheSlotsShowWhy()
        {
            using var t = new StartMenuFixture();
            OpenCreator(t, slot: 0);
            SaveSlotMetadata other = t.WriteStory("slot-0", "먼저 쓴 이야기");
            t.Click(CharacterCreatorScreen.ConfirmId);
            Assert.That(t.Entries, Is.Empty);
            Assert.That(t.Store.TryRead("slot-0", SaveFileNames.Metadata, out SaveSlotMetadata kept), Is.True);
            Assert.That(kept.DisplayName, Is.EqualTo(other.DisplayName), "읽히는 이야기는 바꾸지 않는다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
            Assert.That(t.Node(SaveSlotScreen.ErrorId).Visible, Is.True);
        }

        [Test]
        public void EnterFailure_AfterWriting_GoesBackToTheSlotsWithTheReason()
        {
            using var t = new StartMenuFixture();
            OpenCreator(t, slot: 0);
            t.EnterSucceeds = false;
            t.Click(CharacterCreatorScreen.ConfirmId);
            Assert.That(t.Entries.Count, Is.EqualTo(1));
            Assert.That(t.Stack.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
            Assert.That(t.LabelText(SaveSlotScreen.ErrorId), Is.EqualTo(t.Text.Get("world.entry.failed")));
        }

        [Test]
        public void NewStoryInAnUnreadableSlot_ClearsTheFilesLeftBehind()
        {
            using var t = new StartMenuFixture();
            string dir = Path.Combine(t.Saves, "slot-1");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, SaveFileNames.Metadata), "{ 깨진");
            File.WriteAllText(Path.Combine(dir, SaveFileNames.WorldProgress), "{}");
            OpenCreator(t, slot: 1);
            t.Click(CharacterCreatorScreen.ConfirmId);
            Assert.That(File.Exists(Path.Combine(dir, SaveFileNames.WorldProgress)), Is.False, "Unity StartNewSlot: 남은 파일을 지운다");
            Assert.That(t.Menu.Slots.Read("slot-1").Exists, Is.True);
        }

        [Test]
        public void English_HasEveryKey()
        {
            using var t = new StartMenuFixture("en");
            OpenCreator(t);
            Assert.That(t.LabelText(CharacterCreatorScreen.TitleId), Is.EqualTo(t.Text.Get("ui.creator.title")));
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, t.UsedKeys());
        }
    }
}
