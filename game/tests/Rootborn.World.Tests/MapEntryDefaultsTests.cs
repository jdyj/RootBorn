using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.World.Tests
{
    /// <summary>자동 테스트: 새 맵은 지금 맵의 어긋남을 맞추는 값 없이 기본값으로 읽힌다.</summary>
    public sealed class MapEntryDefaultsTests
    {
        private const string Minimal = @"{ ""maps"": [ {
            ""id"": ""NewRoom"", ""ldtk"": ""maps/new.ldtk"", ""level"": ""Room"", ""viewCells"": 12,
            ""collision"": { ""rule"": ""interior-auto"" }, ""doors"": {}, ""spawns"": { ""room-entry"": [ 48, 48 ] } } ] }";

        [Test]
        public void MapWithoutOptionalValues_HasNoRowShiftAndTheDocumentedDoorFieldsAndCoreLayerNames()
        {
            MapEntry entry = WorldCatalog.Parse(Minimal).Find("NewRoom");
            Assert.That(entry.EntityRowShift, Is.EqualTo(0), "줄 밀기는 지금 맵의 어긋남을 재현하는 값이라 기본은 0");
            Assert.That(entry.DoorOpenSeconds, Is.Null);
            Assert.That(entry.DayNight, Is.False);
            Assert.That((entry.DoorEntity, entry.DoorField, entry.SpawnField), Is.EqualTo(("Door", "Where", "Position")));
            Assert.That(entry.GroundKeys, Is.Null);
            Assert.That(entry.PropPrefixes, Is.Null);
            Assert.That(CollisionRules.Create(entry.CollisionRule, entry.CollisionOptions), Is.InstanceOf<InteriorAutoCollision>());
        }

        [Test]
        public void InteriorLayerNamesAreReadFromTheMapList()
        {
            string json = Minimal.Replace(@"""rule"": ""interior-auto"" }", @"""rule"": ""interior-auto"", ""groundKeys"": [ ""Floor"" ], ""propPrefixes"": [ ""Desk"", ""Bed"" ] }");
            MapEntry entry = WorldCatalog.Parse(json).Find("NewRoom");
            Assert.That(entry.GroundKeys, Is.EqualTo(new[] { "Floor" }));
            Assert.That(entry.SolidKeys, Is.Null);
            Assert.That(entry.PropPrefixes, Is.EqualTo(new[] { "Desk", "Bed" }));
        }
    }
}
