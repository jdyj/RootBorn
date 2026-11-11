using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 슬롯 정보 파일(metadata.json)을 파일 저장소로 쓰고 읽는다. 자동 테스트다.
    /// 옮겨 옴: Assets/Tests/EditMode/Save/SaveSlotServiceTests.cs
    /// (SaveAndLoadMetadata_PreservesSeedsAndCharacter, SaveAndLoadMetadata_PreservesAppearancePartIds, LoadMetadata_LegacyMetadataWithoutAppearanceGetsEmptyAppearanceFallback).
    /// 슬롯 3개·지우기·이상한 슬롯 id·원자적 쓰기·깨진 슬롯 정보 위의 새 이야기는 Screens·Data·Core 테스트에 이미 있다(조사 문서 2번 항목).
    /// </summary>
    public sealed class SaveSlotMetadataFileTests
    {
        [Test]
        public void SeedsAndCharacter_SurviveAWriteAndRead()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            var metadata = SaveSlotMetadata.CreateDeterministic("slot-0", 10);
            metadata.WorldSeed = 1234;
            metadata.TileSeed = 5678;
            metadata.Character = new CharacterCustomizationSaveData { _bodyVariant = 1, _hairVariant = 2, _outfitVariant = 3, _defaultFacing = 3 };
            store.Write("slot-0", SaveFileNames.Metadata, metadata);

            Assert.That(store.TryRead("slot-0", SaveFileNames.Metadata, out SaveSlotMetadata loaded), Is.True);
            Assert.That(loaded.SlotId, Is.EqualTo("slot-0"));
            Assert.That((loaded.WorldSeed, loaded.TileSeed), Is.EqualTo((1234, 5678)));
            var c = loaded.Character;
            Assert.That((c._bodyVariant, c._hairVariant, c._outfitVariant, c._defaultFacing), Is.EqualTo((1, 2, 3, 3)), "_defaultFacing 3은 위쪽");
        }

        [Test]
        public void AppearancePartIds_AndTheCreatorSnapshot_SurviveAWriteAndRead()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            var metadata = SaveSlotMetadata.CreateDeterministic("slot-0", 10);
            metadata.Appearance = new CharacterAppearanceSaveData
            {
                _parts = new[]
                {
                    Part("body", "character.body.01"), Part("eyes", "character.eyes.blue"), Part("hair", "character.hair.short.blonde"),
                    Part("outfit", "character.outfit.braces.brown"), Part("accessory", "character.accessory.bamboo.brown"),
                },
            };
            metadata.CharacterAppearanceSnapshot = new CharacterAppearanceSnapshotSaveData
            {
                visualKind = "spum",
                selectedParts = new[] { new CharacterAppearancePartSaveData { categoryId = "hair", partId = "spum.hair.short" } },
            };
            store.Write("slot-0", SaveFileNames.Metadata, metadata);

            Assert.That(store.TryRead("slot-0", SaveFileNames.Metadata, out SaveSlotMetadata loaded), Is.True);
            Assert.That(loaded.Appearance._parts.Length, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                Assert.That(loaded.Appearance._parts[i]._categoryId, Is.EqualTo(metadata.Appearance._parts[i]._categoryId));
                Assert.That(loaded.Appearance._parts[i]._partId, Is.EqualTo(metadata.Appearance._parts[i]._partId));
            }
            Assert.That(loaded.CharacterAppearanceSnapshot.visualKind, Is.EqualTo("spum"));
            Assert.That(loaded.CharacterAppearanceSnapshot.selectedParts[0].partId, Is.EqualTo("spum.hair.short"));
        }

        [Test]
        public void AUnityMetadataFileWithoutAppearance_ReadsWithAnEmptyAppearance()
        {
            using var dir = new TempDir();
            dir.Write("slot-0/" + SaveFileNames.Metadata,
                "{\"SlotId\":\"slot-0\",\"DisplayName\":\"slot-0\",\"CreatedAtUtcTicks\":1,\"UpdatedAtUtcTicks\":1,\"WorldSeed\":11,\"TileSeed\":22,\"Character\":{\"_bodyVariant\":2}}");
            var store = new FileSaveStore(dir.Path);

            Assert.That(store.TryRead("slot-0", SaveFileNames.Metadata, out SaveSlotMetadata loaded), Is.True);
            loaded.FillMissing();
            Assert.That(loaded.Character._bodyVariant, Is.EqualTo(2));
            Assert.That((loaded.WorldSeed, loaded.TileSeed), Is.EqualTo((11, 22)));
            Assert.That(loaded.Appearance, Is.Not.Null);
            Assert.That(loaded.Appearance._parts, Is.Empty);
            Assert.That(loaded.Version, Is.EqualTo(0), "Unity 판 파일은 형식 버전 0으로 읽힌다");
        }

        private static CharacterPartSelectionSaveData Part(string category, string part)
            => new CharacterPartSelectionSaveData { _categoryId = category, _partId = part };
    }
}
