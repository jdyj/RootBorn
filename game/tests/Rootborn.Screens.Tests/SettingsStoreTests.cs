using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Data.Save;
using Rootborn.Screens.Settings;

namespace Rootborn.Screens.Tests
{
    /// <summary>
    /// 읽을 수 없는 설정 파일: 기본값으로 시작한다. 더 새 프로그램이 쓴 파일은 덮어쓰지 않고(이 판에서 바꾼 설정은 이번 실행에만 남는다),
    /// 깨진 파일은 되살릴 것이 없으므로 다음 저장이 새로 쓴다. 자동 테스트다.
    /// </summary>
    public sealed class SettingsStoreTests
    {
        private static string NewRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "rootborn-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, SaveFileNames.SettingsSlot));
            return root;
        }

        [Test]
        public void ASettingsFileFromANewerProgram_IsUsed_AndItsUnknownSettingsSurviveASave()
        {
            string root = NewRoot();
            try
            {
                string path = Path.Combine(root, SaveFileNames.SettingsSlot, SaveFileNames.Settings);
                File.WriteAllText(path, "{ \"Version\": 999, \"Language\": \"ja\", \"NewThing\": 3 }");

                var settings = new SettingsStore(new FileSaveStore(root));
                Assert.That(settings.LoadError, Is.Null);
                Assert.That(settings.Settings.Language, Is.EqualTo("ja"), "더 새 프로그램의 설정을 읽는다");
                settings.Settings.Language = "en";
                settings.Save();

                var saved = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
                Assert.That((string)saved["Language"], Is.EqualTo("en"));
                Assert.That((int)saved["NewThing"], Is.EqualTo(3), "모르는 설정은 그대로 다시 쓴다");
                Assert.That((int)saved["Version"], Is.EqualTo(999));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void ASettingsFileThatNeedsANewerProgram_IsNotOverwritten()
        {
            string root = NewRoot();
            try
            {
                string path = Path.Combine(root, SaveFileNames.SettingsSlot, SaveFileNames.Settings);
                const string newer = "{ \"Version\": 999, \"ReadableFrom\": 999, \"Language\": \"ja\", \"NewThing\": 3 }";
                File.WriteAllText(path, newer);

                var settings = new SettingsStore(new FileSaveStore(root));
                Assert.That(settings.LoadError, Is.Not.Null);
                settings.Settings.Language = "en";
                settings.Save();

                Assert.That(File.ReadAllText(path), Is.EqualTo(newer), "더 새 프로그램의 설정을 기본값으로 덮지 않는다");
                Assert.That(settings.Settings.Language, Is.EqualTo("en"), "이번 실행에서는 바꾼 값이 쓰인다");
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void ABrokenSettingsFile_IsReplacedByTheNextSave()
        {
            string root = NewRoot();
            try
            {
                string path = Path.Combine(root, SaveFileNames.SettingsSlot, SaveFileNames.Settings);
                File.WriteAllText(path, "{ 깨진");

                var settings = new SettingsStore(new FileSaveStore(root));
                Assert.That(settings.LoadError, Is.Not.Null);
                settings.Settings.Language = "en";
                settings.Save();

                Assert.That(new SettingsStore(new FileSaveStore(root)).Settings.Language, Is.EqualTo("en"));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
