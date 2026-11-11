using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 화면이 data/ JSON을 직접 읽지 않아도 되게, 화면 이름 키(displayNameKey)를 Core 정의가 갖는다.
    /// 값은 JSON의 displayNameKey, 비면 id(Unity StudentLifeDefinitionBase.DisplayNameKey와 같음). 자동 테스트다.
    /// </summary>
    public sealed class DisplayNameKeyTests
    {
        private GameDefinitions _definitions;

        [OneTimeSetUp]
        public void LoadOnce()
        {
            var result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            result.ThrowIfFailed();
            _definitions = result.Definitions;
        }

        [Test]
        public void DailyEvents_CarryTheDisplayNameKeyFromData()
        {
            var expected = Read("DailyEventDefinition");
            Assert.That(expected, Is.Not.Empty);
            foreach (var pair in expected) Assert.That(_definitions.Get<DailyEvent>(pair.Key).DisplayNameKey, Is.EqualTo(pair.Value), pair.Key);
        }

        [Test]
        public void DailyEventChoices_CarryTheDisplayNameKeyFromData()
        {
            var expected = Read("DailyEventChoiceDefinition");
            Assert.That(expected, Is.Not.Empty);
            foreach (var pair in expected) Assert.That(_definitions.Get<DailyEventChoice>(pair.Key).DisplayNameKey, Is.EqualTo(pair.Value), pair.Key);
        }

        [Test]
        public void Locations_CarryTheDisplayNameKeyFromData()
        {
            var expected = Read("LocationDefinition");
            Assert.That(expected, Is.Not.Empty);
            foreach (var pair in expected) Assert.That(_definitions.Get<Location>(pair.Key).DisplayNameKey, Is.EqualTo(pair.Value), pair.Key);
        }

        // 종류 이름의 JSON 파일에서 id → displayNameKey(비면 id).
        private static Dictionary<string, string> Read(string type)
        {
            var map = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(RepoPaths.DataDirectory, type + ".json", SearchOption.AllDirectories))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    string id = item.GetProperty("id").GetString();
                    string key = item.TryGetProperty("displayNameKey", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
                    map[id] = string.IsNullOrEmpty(key) ? id : key;
                }
            }
            return map;
        }
    }
}
