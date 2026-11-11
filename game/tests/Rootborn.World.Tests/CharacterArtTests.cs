using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Characters;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;

namespace Rootborn.World.Tests
{
    /// <summary>자동 테스트: 실제 data/characters와 좌표표로 레이어 캐릭터의 그림을 고른다. 화면 근거가 아니다.</summary>
    public sealed class CharacterArtTests
    {
        private static CharacterArt _art;
        private static WearableCatalog _catalog;

        private static CharacterArt Art => _art ??= CharacterArt.Load(Repo.Root);

        private static WearableCatalog Catalog
        {
            get
            {
                if (_catalog != null) return _catalog;
                GameDefinitions defs = GameDataLoader.Load(Path.Combine(Repo.Root, "data"), RootbornBuilders.Register).Definitions;
                return _catalog = new WearableCatalog(defs.All<WearableSlotRule>(), defs.All<WearableRule>(), defs.All<GrowthStageRule>());
            }
        }

        [Test]
        public void Sheets_AreFourDirectionRowsOfSixFramesCutFromTheAtlas()
        {
            CharacterSheet body = Art.Sheet("body_playground");
            Assert.That(body.Frames.Count, Is.EqualTo(CharacterSheet.FrameCount));
            CharacterFrame stand = body.Frame(CharacterAnimator.RowDown, CharacterAnimator.StandingColumn);
            Assert.That(stand.Texture, Is.EqualTo(Path.GetFullPath(Path.Combine(Repo.Root, "Assets/Art/Characters/Growing/body_playground.png"))));
            Assert.That((stand.X, stand.Y, stand.W, stand.H), Is.EqualTo((32, 0, 32, 48)));
            Assert.That((stand.PivotX, stand.PivotY), Is.EqualTo((0.5f, 1f)), "발(아래 가운데)이 기준점");
            CharacterFrame left = body.Frame(1, 0);
            Assert.That((left.X, left.Y), Is.EqualTo((0, 48)), "두 번째 줄이 왼쪽");
            Assert.That(Art.Sheet("body_adult").Frame(0, 0).H, Is.EqualTo(59), "단계마다 칸 높이가 다르다(좌표표 실측)");
            Assert.That(Art.Problems, Is.Empty);
        }

        [Test]
        public void NewGameLook_IsThePlaygroundBodyWithCasualOutfitUnderShortBrownHair()
        {
            string stage = WearableRules.SelectStage(Catalog.Stages, 1, false);
            Assert.That(stage, Is.EqualTo("stage.playground"));
            CharacterLook look = CharacterLook.Resolve(Art, Catalog, new WearableState(), stage, null);
            Assert.That(look.Body.Id, Is.EqualTo("body_playground"));
            Assert.That(look.Layers.Select(s => s.Id), Is.EqualTo(new[] { "outfit_playground_casual", "head_playground_short_brown" }), "옷(drawOrder 1) 다음 머리(2)");
            Assert.That(look.Code, Is.EqualTo("stage.playground|wearable.outfit_casual,wearable.hair_short_brown"));
        }

        [Test]
        public void AdultAtTheChefFacilityWearsTheUniform()
        {
            string stage = WearableRules.SelectStage(Catalog.Stages, 30, true);
            CharacterLook look = CharacterLook.Resolve(Art, Catalog, new WearableState(), stage, "facility.chef");
            Assert.That(look.Body.Id, Is.EqualTo("body_adult"));
            Assert.That(look.Layers.Select(s => s.Id), Is.EqualTo(new[] { "outfit_adult_chef", "head_adult_short_brown" }));
        }

        [Test]
        public void LegacyCreatorOutfitMapsToTheWearableThatDrawsIt()
        {
            Dictionary<string, string> map = Art.LegacyAppearanceMap();
            Assert.That(map["adult_short_brown_chef"], Is.EqualTo("wearable.outfit_chef"));
            Assert.That(map.Values, Has.None.Null);
        }

        [Test]
        public void PlayerWearables_CreatorOutfitIsMovedInOnceAndWrittenBack()
        {
            string dir = Path.Combine(Path.GetTempPath(), "rootborn-wearables-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileSaveStore(dir);
                var metadata = new SaveSlotMetadata
                {
                    SlotId = "slot-0",
                    CharacterAppearanceSnapshot = new CharacterAppearanceSnapshotSaveData
                    {
                        visualKind = "growing",
                        selectedParts = new[] { new CharacterAppearancePartSaveData { categoryId = "outfit:body_adult", partId = "adult_short_brown_chef" } },
                    },
                };
                WearableState state = PlayerWearables.Load(store, "slot-0", SaveFileNames.DefaultPlayerId, metadata, Catalog, Art);
                Assert.That(state.Get("slot.outfit", "stage.adult"), Is.EqualTo("wearable.outfit_chef"));
                Assert.That(store.TryRead("slot-0", "wearables-local-player.json", out WearableSaveData saved), Is.True);
                Assert.That(saved.legacyImported, Is.True);
                System.DateTime written = File.GetLastWriteTimeUtc(Path.Combine(dir, "slot-0", "wearables-local-player.json"));
                PlayerWearables.Load(store, "slot-0", SaveFileNames.DefaultPlayerId, metadata, Catalog, Art);
                Assert.That(File.GetLastWriteTimeUtc(Path.Combine(dir, "slot-0", "wearables-local-player.json")), Is.EqualTo(written), "두 번째는 쓰지 않는다");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        [Test]
        public void Animator_WalkCycleIsStandStepStandStepAtEightFramesPerSecond()
        {
            var animator = new CharacterAnimator();
            Assert.That((animator.Row, animator.Column), Is.EqualTo((CharacterAnimator.RowDown, CharacterAnimator.StandingColumn)));
            var columns = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                animator.Advance(true, Facing.Right, 1f / 8f);
                columns.Add(animator.Column);
            }
            Assert.That(animator.Row, Is.EqualTo(2), "세 번째 줄이 오른쪽(뒤집지 않는다)");
            Assert.That(columns, Is.EqualTo(new[] { 1, 2, 1, 0, 1, 2 }), "Unity GrowingCharacterVisualView WalkCycle {1,2,1,0}");
            animator.Advance(false, Facing.Up, 1f / 60f);
            Assert.That((animator.Row, animator.Column), Is.EqualTo((3, CharacterAnimator.StandingColumn)), "멈추면 서 있는 칸, 보던 방향");
            animator.Advance(true, Facing.Left, 1f / 60f);
            Assert.That((animator.Row, animator.Column), Is.EqualTo((1, 1)), "다시 걸으면 처음부터");
        }
    }
}
