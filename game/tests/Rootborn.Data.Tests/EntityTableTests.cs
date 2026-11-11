using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Data.Tests
{
    public sealed class EntityTableTests
    {
        [Test]
        public void ReadsEntitiesByTypeAndId()
        {
            using var dir = new TempDir();
            dir.Write("a/Leaf.json", @"[{""type"":""Leaf"",""id"":""leaf.1"",""asset"":""Assets/L1.asset""},
                                        {""type"":""Leaf"",""id"":""leaf.2"",""asset"":""Assets/L2.asset""}]");
            dir.Write("b/Branch.json", @"[{""type"":""Branch"",""id"":""branch.1"",""asset"":""Assets/B1.asset""}]");
            var errors = new List<LoadError>();

            var table = EntityTable.ReadDirectory(dir.Path, errors);

            Assert.That(errors, Is.Empty);
            Assert.That(table.All.Count(), Is.EqualTo(3));
            Assert.That(table.TryGet("Leaf", "leaf.2", out var node), Is.True);
            Assert.That(node.Asset, Is.EqualTo("Assets/L2.asset"));
            Assert.That(node.Type, Is.EqualTo("Leaf"));
            Assert.That(node.Id, Is.EqualTo("leaf.2"));
            Assert.That(table.TryGet("Branch", "branch.1", out var branch), Is.True);
            Assert.That(branch.Asset, Is.EqualTo("Assets/B1.asset"));
            Assert.That(table.TryGet("Branch", "leaf.1", out _), Is.False);
            Assert.That(table.Types, Is.EquivalentTo(new[] { "Leaf", "Branch" }));
        }

        [Test]
        public void BrokenFile_IsOneParseError_AndOthersStillLoad()
        {
            using var dir = new TempDir();
            var broken = dir.Write("a/Broken.json", @"[{""type"":""Leaf"",""id"":");
            dir.Write("b/Leaf.json", @"[{""type"":""Leaf"",""id"":""leaf.1"",""asset"":""Assets/L1.asset""}]");
            var errors = new List<LoadError>();

            var table = EntityTable.ReadDirectory(dir.Path, errors);

            var error = errors.Single();
            Assert.That(error.Kind, Is.EqualTo("parse"));
            Assert.That(Path.GetFullPath(error.Source), Does.EndWith(Path.Combine("a", "Broken.json")));
            Assert.That(table.TryGet("Leaf", "leaf.1", out _), Is.True);
        }

        [Test]
        public void SameTypeAndIdTwice_IsADuplicateError()
        {
            using var dir = new TempDir();
            dir.Write("a/Leaf.json", @"[{""type"":""Leaf"",""id"":""leaf.1"",""asset"":""Assets/First.asset""}]");
            dir.Write("b/Leaf.json", @"[{""type"":""Leaf"",""id"":""leaf.1"",""asset"":""Assets/Second.asset""}]");
            var errors = new List<LoadError>();

            EntityTable.ReadDirectory(dir.Path, errors);

            var error = errors.Single();
            Assert.That(error.Kind, Is.EqualTo("duplicate"));
            Assert.That(error.Type, Is.EqualTo("Leaf"));
            Assert.That(error.Id, Is.EqualTo("leaf.1"));
            Assert.That(error.Message, Does.Contain("Assets/First.asset"));
            Assert.That(error.Message, Does.Contain("Assets/Second.asset"));
        }

        [Test]
        public void NonEntityJson_IsSkippedWithoutError()
        {
            using var dir = new TempDir();
            dir.Write("registry.json", @"{""quests"":[""q.1""]}");
            dir.Write("localization/ko.json", @"{""a.b"":""가""}");
            dir.Write("lists/Names.json", @"[""a"",""b""]");
            dir.Write("lists/NoId.json", @"[{""type"":""Leaf""}]");
            dir.Write("lists/Empty.json", @"[]");
            var errors = new List<LoadError>();

            var table = EntityTable.ReadDirectory(dir.Path, errors);

            Assert.That(errors, Is.Empty);
            Assert.That(table.All, Is.Empty);
        }

        [Test]
        public void RealData_HasNoParseOrDuplicateErrors()
        {
            var errors = new List<LoadError>();

            var table = EntityTable.ReadDirectory(RepoPaths.DataDirectory, errors);

            Assert.That(errors, Is.Empty, string.Join("\n", errors.Take(20)));
            Assert.That(table.All.Count(), Is.EqualTo(2289));
        }

        [Test]
        public void RegistryIds_ReadTheRealRegistryInOrder()
        {
            var table = EntityTable.ReadDirectory(RepoPaths.DataDirectory, new List<LoadError>());

            Assert.That(table.RegistryIds("quests")[0], Is.EqualTo("quest.gatherWood"));
            Assert.That(table.RegistryIds("없는키"), Is.Empty);
        }
    }
}
