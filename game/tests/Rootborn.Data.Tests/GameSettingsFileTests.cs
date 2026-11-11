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
    }
}
