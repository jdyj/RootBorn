using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>Unity 판 QA 세이브(Fixtures/saves)를 MonoGame 판 저장소로 읽는다. 자동 테스트다.</summary>
    public sealed class OldSaveTests
    {
        private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "saves");
        private static readonly JsonSerializerOptions Compare = new JsonSerializerOptions { IncludeFields = true };

        private static IEnumerable<string> AllFixtureFiles()
        {
            return Directory.GetFiles(FixtureRoot, "*.json", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => Path.GetRelativePath(FixtureRoot, path).Replace('\\', '/'));
        }

        private static IEnumerable<string> ShapedFixtureFiles()
        {
            return AllFixtureFiles().Where(file => SaveShapeCoverage.ShapeFor(Path.GetFileName(file)) != null);
        }

        [Test]
        public void TheFixtureFolderHasFiles()
        {
            Assert.That(AllFixtureFiles().Count(), Is.GreaterThanOrEqualTo(18));
            Assert.That(ShapedFixtureFiles().Count(), Is.GreaterThanOrEqualTo(11));
        }

        [TestCaseSource(nameof(AllFixtureFiles))]
        public void EveryFixtureFile_IsEitherShapedOrListed(string file)
        {
            var name = Path.GetFileName(file);
            Assert.That(SaveShapeCoverage.ShapeFor(name) != null || SaveShapeCoverage.IsListedAsNotShaped(name), Is.True,
                $"{file}: SaveShapeCoverage에 모양도 '아직 모양 없음'도 없다");
        }

        [TestCaseSource(nameof(ShapedFixtureFiles))]
        public void OldFile_Reads(string file)
        {
            Assert.That(ReadFixture(file), Is.Not.Null);
        }

        [TestCaseSource(nameof(ShapedFixtureFiles))]
        public void OldFile_HasNoFieldTheShapeDrops(string file)
        {
            var name = Path.GetFileName(file);
            var shape = SaveShapeCoverage.ShapeFor(name);
            using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureRoot, file)));
            var dropped = new List<string>();

            Walk(document.RootElement, shape, "", path =>
            {
                if (!SaveShapeCoverage.IsDropAllowed(name, path)) dropped.Add(path);
            });

            Assert.That(dropped, Is.Empty, $"{file}: 모양({shape.Name})에 없는 필드 — 모양에 더하거나 허용 목록에 이유와 함께 적는다");
        }

        [TestCaseSource(nameof(ShapedFixtureFiles))]
        public void ReadWriteRead_IsTheSame(string file)
        {
            var shape = SaveShapeCoverage.ShapeFor(Path.GetFileName(file));
            var first = ReadFixture(file);
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);

            Invoke(store, nameof(FileSaveStore.Write), shape, "slot", "file.json", first);
            var second = ReadWith(store, shape, "slot", "file.json");

            // Write는 버전 0(Unity 판) 모양을 아는 버전으로 올려 쓴다. 그 뒤의 첫 객체와 다시 읽은 객체를 비교한다.
            Assert.That(JsonSerializer.Serialize(second, shape, Compare), Is.EqualTo(JsonSerializer.Serialize(first, shape, Compare)));
        }

        [Test]
        public void Snapshot_StudentSurvivesTheProgressObject()
        {
            var snapshot = (AdultCareerSnapshotSaveData)ReadFixture(
                "exe-20261105/slot-0/adult-career-snapshot-006c006f00630061006c002d0070006c0061007900650072.json");
            Assert.That(snapshot.Version, Is.EqualTo(2));
            Assert.That(snapshot.Adult.PlayerId, Is.EqualTo("local-player"));
            Assert.That(snapshot.Inventory, Is.Not.Null);
            Assert.That(snapshot.QuestChains, Is.Not.Null);

            var again = StudentLifeProgress.FromSaveData(snapshot.Student).ToSaveData();

            Assert.That(again.Energy, Is.EqualTo(11));
            Assert.That(again.Focus, Is.EqualTo(9));
            Assert.That(again.CurrentDay, Is.EqualTo(2));
            Assert.That(again.TimeMinutes, Is.EqualTo(510));
            Assert.That(again.Traits.Length, Is.EqualTo(snapshot.Student.Traits.Length));
            Assert.That(again.Skills.Length, Is.EqualTo(snapshot.Student.Skills.Length));
            Assert.That(again.SaveSlot, Is.EqualTo(snapshot.Student.SaveSlot));
            Assert.That(again.PlayerId, Is.EqualTo(snapshot.Student.PlayerId));
        }

        [TestCase("exe-20261105/slot-0/quest-log.json")]
        [TestCase("editor-20261105/slot-1/quest-log.json")]
        public void QuestLog_SurvivesTheLogObject(string file)
        {
            var saved = (QuestLogSaveData)ReadFixture(file);
            var definitions = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            definitions.ThrowIfFailed();
            var log = new QuestLog(definitions.Definitions.All<QuestDefinition>());

            log.LoadFromSaveData(saved);
            var again = log.ToSaveData();

            var savedIds = saved.Quests.Select(q => q.QuestId).ToArray();
            var kept = again.Quests.Where(q => savedIds.Contains(q.QuestId)).ToArray();
            Assert.That(kept.Select(q => q.QuestId), Is.EqualTo(savedIds));
            Assert.That(kept.Select(q => q.State), Is.EqualTo(saved.Quests.Select(q => q.State)));
            Assert.That(kept.Select(q => q.ObjectiveCounts), Is.EqualTo(saved.Quests.Select(q => q.ObjectiveCounts)));
            Assert.That(again.Quests.Where(q => !savedIds.Contains(q.QuestId)).Select(q => q.State), Is.All.EqualTo(QuestStage.NotStarted),
                "세이브에 없는 퀘스트는 시작 전으로 남는다");
        }

        private static object ReadFixture(string file)
        {
            // <묶음>/<슬롯>/<파일>, 방 슬롯의 손님 세이브는 <묶음>/<방 슬롯>/guests/<플레이어 id>/<파일> — 파일의 폴더가 슬롯, 그 위가 저장소 뿌리.
            var full = Path.Combine(FixtureRoot, file);
            var slotDir = Path.GetDirectoryName(full);
            var store = new FileSaveStore(Path.GetDirectoryName(slotDir));
            var name = Path.GetFileName(full);
            return ReadWith(store, SaveShapeCoverage.ShapeFor(name), Path.GetFileName(slotDir), name);
        }

        private static object ReadWith(FileSaveStore store, Type shape, string slot, string file)
        {
            var arguments = new object[] { slot, file, null };
            var found = (bool)Generic(nameof(FileSaveStore.TryRead), shape).Invoke(store, arguments);
            Assert.That(found, Is.True, $"{slot}/{file}");
            return arguments[2];
        }

        private static void Invoke(FileSaveStore store, string method, Type shape, params object[] arguments)
        {
            Generic(method, shape).Invoke(store, arguments);
        }

        private static MethodInfo Generic(string method, Type shape)
        {
            return typeof(FileSaveStore).GetMethods().Single(m => m.Name == method && m.IsGenericMethodDefinition).MakeGenericMethod(shape);
        }

        /// <summary>JSON의 모든 경로를 돌며, 모양에 같은 이름의 공개 필드(또는 설정 가능한 속성)가 없는 경로를 알린다.</summary>
        private static void Walk(JsonElement json, Type shape, string path, Action<string> missing)
        {
            if (json.ValueKind == JsonValueKind.Array)
            {
                var element = ElementType(shape);
                if (element == null) return;
                foreach (var item in json.EnumerateArray()) Walk(item, element, path + "[]", missing);
                return;
            }

            if (json.ValueKind != JsonValueKind.Object || IsLeaf(shape)) return;

            foreach (var property in json.EnumerateObject())
            {
                var childPath = path.Length == 0 ? property.Name : path + "." + property.Name;
                var member = MemberType(shape, property.Name);
                if (member == null)
                {
                    missing(childPath);
                    continue;
                }

                Walk(property.Value, member, childPath, missing);
            }
        }

        private static Type MemberType(Type shape, string name)
        {
            var field = shape.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && !field.IsInitOnly) return field.FieldType;
            var property = shape.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.CanWrite ? property.PropertyType : null;
        }

        private static Type ElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type)) return type.GetGenericArguments()[0];
            return null;
        }

        private static bool IsLeaf(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
    }
}
