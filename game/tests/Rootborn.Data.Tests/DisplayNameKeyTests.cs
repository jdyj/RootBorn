using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        [TestCase("RelationshipMilestoneObjective")]
        [TestCase("OutsideSchoolLogMilestoneObjective")]
        public void MilestoneObjectives_CarryTheDisplayNameKeyFromData(string type)
        {
            var expected = Read(type);
            Assert.That(expected, Is.Not.Empty);
            foreach (var pair in expected) Assert.That(_definitions.Get<MilestoneObjective>(pair.Key).DisplayNameKey, Is.EqualTo(pair.Value), pair.Key);
        }

        [TestCase("CampaignActivityLogObjective")]
        [TestCase("CampaignLocationVisitObjective")]
        public void CampaignObjectives_CarryTheDisplayNameKeyFromData(string type)
        {
            var expected = Read(type);
            Assert.That(expected, Is.Not.Empty);
            foreach (var pair in expected) Assert.That(_definitions.Get<CampaignObjective>(pair.Key).DisplayNameKey, Is.EqualTo(pair.Value), pair.Key);
        }

        [Test]
        public void CampaignsAndCampaignDays_CarryTheDisplayNameKeyFromData()
        {
            var campaigns = Read("CampaignDefinition");
            var days = Read("CampaignDayDefinition");
            Assert.That(campaigns, Is.Not.Empty);
            Assert.That(days, Is.Not.Empty);
            foreach (var pair in campaigns) Assert.That(_definitions.Get<Campaign>(pair.Key).DisplayNameKey, Is.EqualTo(pair.Value), pair.Key);
            foreach (var pair in days) Assert.That(_definitions.Get<CampaignDay>(pair.Key).DisplayNameKey, Is.EqualTo(pair.Value), pair.Key);
            Assert.That(_definitions.Get<Campaign>("campaign.first-three-days").DisplayNameKey, Is.Not.EqualTo("campaign.first-three-days"), "id가 아니라 데이터 값");
        }

        [Test]
        public void ChallengeSymbolSprites_ComeFromData_KeepingEmptySlots()
        {
            var minigames = _definitions.All<Rootborn.Core.Minigames.Minigame>();
            var all = minigames.SelectMany(m => m.Challenges).OfType<Rootborn.Core.Minigames.ChallengeBase>().ToArray();
            Assert.That(all.Any(c => c.SymbolSprites.Count > 0), Is.True);
            Assert.That(all.SelectMany(c => c.SymbolSprites).Where(s => s.Length > 0).All(s => s.EndsWith(".png")), Is.True, "그림 경로");
            var care1 = all.OfType<Rootborn.Core.Minigames.ChoiceChallenge>().First(c => c.InstructionKey == "minigame.care.instructions" && c.SymbolSprites.Count == 3);
            Assert.That(care1.SymbolSprites[0], Does.EndWith("bandage.png"));
            Assert.That(all.Any(c => c.SymbolSprites.Contains(string.Empty)), Is.True, "그림이 빠진 칸은 빈 문자열로 자리를 지킨다(Travel2)");
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
