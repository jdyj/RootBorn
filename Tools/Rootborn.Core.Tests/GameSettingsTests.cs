using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.Settings;

namespace Rootborn.Core.Tests
{
    /// <summary>슬롯과 무관한 설정 파일 하나: 언어 코드와 이름 → 켬/끔 목록. Unity 판은 설정을 저장하지 않았다.</summary>
    public sealed class GameSettingsTests
    {
        [Test]
        public void FileLivesInAFixedPlaceOutsideTheSaveSlots()
        {
            Assert.That(SaveFileNames.SettingsSlot, Is.Not.Empty);
            Assert.That(SaveFileNames.SettingsSlot, Does.Not.StartWith("slot"), "세이브 슬롯 이름과 겹치지 않는다");
            Assert.That(SaveFileNames.Settings, Is.EqualTo("settings.json"));
        }

        [Test]
        public void NoFile_GivesTheUnitySettingsPanelStartValues_AndNoLanguage()
        {
            var store = new InMemorySaveStore();
            var settings = GameSettingsSaveFiles.LoadOrCreate(store);

            Assert.That(settings.Language, Is.Empty, "언어를 아직 고르지 않았다");
            // Unity SettingsPanel.MakeSettingsGroup의 처음 모양: 소리 켬, 음소거 끔, 음악 끔, 글자 켬.
            Assert.That(settings.GetToggle(GameSettingNames.Speaker), Is.True);
            Assert.That(settings.GetToggle(GameSettingNames.Muted), Is.False);
            Assert.That(settings.GetToggle(GameSettingNames.Music), Is.False);
            Assert.That(settings.GetToggle(GameSettingNames.Text), Is.True);
            Assert.That(settings.ToggleNames, Is.EqualTo(new[] { GameSettingNames.Speaker, GameSettingNames.Muted, GameSettingNames.Music, GameSettingNames.Text }));
            Assert.That(store.Writes, Is.EqualTo(0), "읽기만으로 쓰지 않는다");
        }

        [Test]
        public void SaveThenLoad_KeepsLanguageAndToggles()
        {
            var store = new InMemorySaveStore();
            var settings = GameSettingsSaveFiles.LoadOrCreate(store);
            settings.Language = "en";
            settings.SetToggle(GameSettingNames.Music, true);
            settings.SetToggle(GameSettingNames.Speaker, false);
            GameSettingsSaveFiles.Save(store, settings);

            var again = GameSettingsSaveFiles.LoadOrCreate(store);
            Assert.That(again.Language, Is.EqualTo("en"));
            Assert.That(again.GetToggle(GameSettingNames.Music), Is.True);
            Assert.That(again.GetToggle(GameSettingNames.Speaker), Is.False);
            Assert.That(again.GetToggle(GameSettingNames.Text), Is.True);
        }

        [Test]
        public void UnknownNames_AreKeptAsTheyAre_SoNewSettingsNeedNoNewCode()
        {
            var store = new InMemorySaveStore();
            store.Put(SaveFileNames.SettingsSlot, SaveFileNames.Settings, new GameSettingsSaveData
            {
                Version = 1,
                Language = "ja",
                Toggles = new[] { new GameSettingToggleSaveData { Name = "future.option", Value = true }, new GameSettingToggleSaveData { Name = GameSettingNames.Muted, Value = true } },
            });

            var settings = GameSettingsSaveFiles.LoadOrCreate(store);
            Assert.That(settings.Language, Is.EqualTo("ja"));
            Assert.That(settings.GetToggle("future.option"), Is.True);
            Assert.That(settings.GetToggle(GameSettingNames.Muted), Is.True);
            Assert.That(settings.GetToggle(GameSettingNames.Speaker), Is.True, "파일에 없는 기본 이름은 기본값");
            Assert.That(settings.GetToggle("never.set", fallback: true), Is.True);
            Assert.That(settings.TryGetToggle("never.set", out _), Is.False);

            GameSettingsSaveFiles.Save(store, settings);
            store.TryRead<GameSettingsSaveData>(SaveFileNames.SettingsSlot, SaveFileNames.Settings, out var written);
            Assert.That(written.Toggles.Select(t => t.Name), Does.Contain("future.option"));
        }

        [Test]
        public void EmptyNamesAndDuplicateEntries_AreHandled()
        {
            var settings = GameSettings.CreateDefault();
            Assert.That(settings.SetToggle("", true), Is.False);
            Assert.That(settings.SetToggle(null, true), Is.False);
            Assert.That(settings.GetToggle(null), Is.False);

            var data = new GameSettingsSaveData
            {
                Toggles = new[] { new GameSettingToggleSaveData { Name = "x", Value = true }, new GameSettingToggleSaveData { Name = "x", Value = false }, null, new GameSettingToggleSaveData() },
            };
            var loaded = GameSettings.FromSaveData(data);
            Assert.That(loaded.GetToggle("x"), Is.False, "같은 이름이 둘이면 뒤의 값");
            Assert.That(loaded.Language, Is.Empty);
            Assert.That(loaded.ToSaveData().Toggles.Count(t => t.Name == "x"), Is.EqualTo(1));
        }

        [Test]
        public void SaveData_IsVersioned()
        {
            IVersionedSave data = new GameSettingsSaveData();
            Assert.That(data.Version, Is.EqualTo(SaveVersion.Unity));
            Assert.That(data.KnownVersion, Is.EqualTo(SaveVersion.Current));
        }
    }
}
