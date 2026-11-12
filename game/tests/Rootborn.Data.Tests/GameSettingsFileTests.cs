using System.IO;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.Settings;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>설정 파일이 저장 폴더의 고정 자리(세이브 슬롯 밖)에 쓰이고 다시 읽힌다. 자동 테스트다.</summary>
    public sealed class GameSettingsFileTests
    {
        [Test]
        public void Settings_AreWrittenOutsideEverySaveSlot_AndReadBack()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            var settings = GameSettingsSaveFiles.LoadOrCreate(store);
            settings.Language = "EN";
            settings.SetToggle(GameSettingNames.Muted, true);
            GameSettingsSaveFiles.Save(store, settings);

            Assert.That(File.Exists(Path.Combine(dir.Path, SaveFileNames.SettingsSlot, SaveFileNames.Settings)), Is.True);
            var again = GameSettingsSaveFiles.LoadOrCreate(new FileSaveStore(dir.Path));
            Assert.That(again.Language, Is.EqualTo("en"));
            Assert.That(again.GetToggle(GameSettingNames.Muted), Is.True);
            Assert.That(store.TryRead(SaveFileNames.SettingsSlot, SaveFileNames.Settings, out GameSettingsSaveData data), Is.True);
            Assert.That(data.Version, Is.EqualTo(GameSettingsSaveData.KnownVersion));
        }

        [Test]
        public void AWearablesFileThatNeedsANewerProgram_IsRefused_LikeEveryOtherSaveShape()
        {
            // 옷 파일은 소문자 version 필드라 버전 검사에서 빠져 있었다. 다른 모양처럼 ReadableFrom이 높으면 읽지 않는다.
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            string folder = Path.Combine(dir.Path, "slot-0");
            Directory.CreateDirectory(folder);
            string file = SaveFileNames.Wearables(SaveFileNames.DefaultPlayerId);
            File.WriteAllText(Path.Combine(folder, file), "{ \"version\": 999, \"ReadableFrom\": 999, \"entries\": [] }");
            Assert.Throws<SaveFileTooNewException>(() => store.TryRead("slot-0", file, out Rootborn.Core.Characters.WearableSaveData _));

            File.WriteAllText(Path.Combine(folder, file), "{ \"legacyImported\": true, \"entries\": [] }");
            Assert.That(store.TryRead("slot-0", file, out Rootborn.Core.Characters.WearableSaveData old), Is.True, "version이 없는 옛 파일은 읽힌다");
            Assert.That(old.legacyImported, Is.True);
            store.Write("slot-0", file, old);
            Assert.That(File.ReadAllText(Path.Combine(folder, file)), Does.Contain("\"version\": 1"), "필드 이름은 소문자 그대로");
        }
    }
}
