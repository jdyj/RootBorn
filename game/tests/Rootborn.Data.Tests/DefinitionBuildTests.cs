using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Data.Tests
{
    public sealed class DefinitionBuildTests
    {
        // 이 테스트 안에서만 쓰는 가짜 정의들.
        private interface IThing
        {
            string Id { get; }
        }

        private sealed class Leaf : IThing
        {
            public Leaf(string id, int power)
            {
                if (power < 0) throw new ArgumentOutOfRangeException(nameof(power), "힘은 음수일 수 없다");
                Id = id;
                Power = power;
            }

            public string Id { get; }
            public int Power { get; }
        }

        private sealed class Branch : IThing
        {
            public Branch(string id, IReadOnlyList<Leaf> leaves, string friendId)
            {
                Id = id;
                Leaves = leaves;
                FriendId = friendId;
            }

            public string Id { get; }
            public IReadOnlyList<Leaf> Leaves { get; }
            public string FriendId { get; }
        }

        private sealed class Link
        {
            public Link(string id, Link next)
            {
                Id = id;
                Next = next;
            }

            public string Id { get; }
            public Link Next { get; }
        }

        private enum Color
        {
            Red = 0,
            Green = 1,
        }

        private sealed class Paint
        {
            public Paint(string id, Color color)
            {
                Id = id;
                Color = color;
            }

            public string Id { get; }
            public Color Color { get; }
        }

        private static void Register(DefinitionBuilders b)
        {
            b.Add("Leaf", (n, c) => new Leaf(n.Id, n.Int("power")));
            b.Add("Branch", (n, c) => new Branch(n.Id, c.All<Leaf>(n, "leaves"), n.RefId("friend")));
            b.Add("Link", (n, c) => new Link(n.Id, c.One<Link>(n, "next")));
            b.Add("Paint", (n, c) => new Paint(n.Id, n.Enum<Color>("color")));
        }

        private static LoadResult Load(TempDir dir, Action<DefinitionBuilders> register = null)
        {
            return GameDataLoader.Load(dir.Path, register ?? Register);
        }

        private const string TwoLeaves = @"[
            {""type"":""Leaf"",""id"":""leaf.a"",""asset"":""Assets/LeafA.asset"",""power"":1},
            {""type"":""Leaf"",""id"":""leaf.b"",""asset"":""Assets/LeafB.asset"",""power"":2}]";

        [Test]
        public void BuildsAnEntityAndItsReferences()
        {
            using var dir = new TempDir();
            dir.Write("leaf/Leaf.json", TwoLeaves);
            dir.Write("branch/Branch.json", @"[{""type"":""Branch"",""id"":""branch.1"",""asset"":""Assets/B.asset"",""friend"":{""ref"":""branch.2"",""type"":""Branch""},
                ""leaves"":[{""ref"":""leaf.a"",""type"":""Leaf""},{""ref"":""leaf.b"",""type"":""Leaf""}]}]");

            var result = Load(dir);

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            var branch = result.Definitions.Get<Branch>("branch.1");
            Assert.That(branch.Leaves.Select(l => l.Id), Is.EqualTo(new[] { "leaf.a", "leaf.b" }));
            Assert.That(branch.Leaves.Select(l => l.Power), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(branch.FriendId, Is.EqualTo("branch.2"));
        }

        [Test]
        public void SameReference_IsBuiltOnce()
        {
            using var dir = new TempDir();
            dir.Write("leaf/Leaf.json", TwoLeaves);
            dir.Write("branch/Branch.json", @"[
                {""type"":""Branch"",""id"":""branch.1"",""friend"":null,""leaves"":[{""ref"":""leaf.a"",""type"":""Leaf""}]},
                {""type"":""Branch"",""id"":""branch.2"",""friend"":null,""leaves"":[{""ref"":""leaf.a"",""type"":""Leaf""}]}]");

            var result = Load(dir);

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            var first = result.Definitions.Get<Branch>("branch.1").Leaves.Single();
            var second = result.Definitions.Get<Branch>("branch.2").Leaves.Single();
            Assert.That(ReferenceEquals(first, second), Is.True);
            Assert.That(ReferenceEquals(first, result.Definitions.Get<Leaf>("leaf.a")), Is.True);
        }

        [Test]
        public void InlineStrategy_IsBuiltInPlace()
        {
            using var dir = new TempDir();
            dir.Write("branch/Branch.json", @"[{""type"":""Branch"",""id"":""branch.1"",""friend"":null,""leaves"":[{""type"":""Leaf"",""power"":3}]}]");

            var result = Load(dir);

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(result.Definitions.Get<Branch>("branch.1").Leaves.Single().Power, Is.EqualTo(3));
            Assert.That(result.Definitions.All<Leaf>(), Is.Empty, "자리에서 만든 전략은 id가 없어 정의 목록에 들어가지 않는다");
        }

        [Test]
        public void MissingField_IsAnError_WithTheAssetPath()
        {
            using var dir = new TempDir();
            dir.Write("leaf/Leaf.json", @"[{""type"":""Leaf"",""id"":""leaf.a"",""asset"":""Assets/LeafA.asset""}]");

            var result = Load(dir);

            var error = result.Errors.Single();
            Assert.That(error.Kind, Is.EqualTo("missing-field"));
            Assert.That(error.Source, Is.EqualTo("Assets/LeafA.asset"));
            Assert.That(error.Id, Is.EqualTo("leaf.a"));
            Assert.That(error.Message, Does.Contain("power"));
            Assert.That(result.Ok, Is.False);
        }

        [Test]
        public void NullField_IsNoReference_NotAnError()
        {
            using var dir = new TempDir();
            dir.Write("branch/Branch.json", @"[{""type"":""Branch"",""id"":""branch.1"",""friend"":null,""leaves"":null}]");
            dir.Write("link/Link.json", @"[{""type"":""Link"",""id"":""link.1"",""next"":null}]");

            var result = Load(dir);

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            var branch = result.Definitions.Get<Branch>("branch.1");
            Assert.That(branch.FriendId, Is.EqualTo(""));
            Assert.That(branch.Leaves, Is.Empty);
            Assert.That(result.Definitions.Get<Link>("link.1").Next, Is.Null);
        }

        [Test]
        public void MissingReference_IsAnError()
        {
            using var dir = new TempDir();
            dir.Write("branch/Branch.json", @"[{""type"":""Branch"",""id"":""branch.1"",""asset"":""Assets/B.asset"",""friend"":null,
                ""leaves"":[{""ref"":""leaf.gone"",""type"":""Leaf""}]}]");

            var result = Load(dir);

            var error = result.Errors.Single();
            Assert.That(error.Kind, Is.EqualTo("missing-ref"));
            Assert.That(error.Source, Is.EqualTo("Assets/B.asset"));
            Assert.That(error.Message, Does.Contain("leaf.gone"));
        }

        [Test]
        public void ReferenceOfTheWrongKind_IsAnError()
        {
            using var dir = new TempDir();
            dir.Write("branch/Branch.json", @"[
                {""type"":""Branch"",""id"":""branch.1"",""friend"":null,""leaves"":[{""ref"":""branch.2"",""type"":""Branch""}]},
                {""type"":""Branch"",""id"":""branch.2"",""friend"":null,""leaves"":[]}]");

            var result = Load(dir);

            var error = result.Errors.Single();
            Assert.That(error.Kind, Is.EqualTo("wrong-ref-type"));
            Assert.That(error.Id, Is.EqualTo("branch.1"));
        }

        [Test]
        public void ReferencesThatNeverEnd_AreACycleError()
        {
            using var dir = new TempDir();
            dir.Write("link/Link.json", @"[
                {""type"":""Link"",""id"":""link.a"",""next"":{""ref"":""link.b"",""type"":""Link""}},
                {""type"":""Link"",""id"":""link.b"",""next"":{""ref"":""link.a"",""type"":""Link""}}]");

            var result = Load(dir);

            Assert.That(result.Errors.Select(e => e.Kind), Has.Some.EqualTo("cycle"));
            Assert.That(result.Errors.Select(e => e.Kind), Is.All.EqualTo("cycle"));
        }

        [Test]
        public void ConstructorThrowing_IsCollected_NotThrown()
        {
            using var dir = new TempDir();
            dir.Write("leaf/Leaf.json", @"[{""type"":""Leaf"",""id"":""leaf.bad"",""asset"":""Assets/Bad.asset"",""power"":-1},
                                           {""type"":""Leaf"",""id"":""leaf.ok"",""power"":1}]");

            var result = Load(dir);

            var error = result.Errors.Single();
            Assert.That(error.Kind, Is.EqualTo("constructor"));
            Assert.That(error.Source, Is.EqualTo("Assets/Bad.asset"));
            Assert.That(error.Message, Does.Contain("힘은 음수일 수 없다"));
            Assert.That(result.Definitions.TryGet<Leaf>("leaf.ok", out _), Is.True);
            Assert.That(result.Definitions.TryGet<Leaf>("leaf.bad", out _), Is.False);
        }

        [Test]
        public void EnumOutOfRange_IsAnError()
        {
            using var dir = new TempDir();
            dir.Write("paint/Paint.json", @"[{""type"":""Paint"",""id"":""paint.ok"",""color"":1},
                                             {""type"":""Paint"",""id"":""paint.bad"",""color"":7}]");

            var result = Load(dir);

            var error = result.Errors.Single();
            Assert.That(error.Kind, Is.EqualTo("enum-range"));
            Assert.That(error.Id, Is.EqualTo("paint.bad"));
            Assert.That(result.Definitions.Get<Paint>("paint.ok").Color, Is.EqualTo(Color.Green));
        }

        [Test]
        public void TypeWithNoBuilderAndNoListing_IsUnclassified()
        {
            using var dir = new TempDir();
            dir.Write("leaf/Leaf.json", TwoLeaves);
            dir.Write("odd/Mystery.json", @"[{""type"":""Mystery"",""id"":""mystery.1""}]");

            var result = Load(dir);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.UnclassifiedTypes, Is.EqualTo(new[] { "Mystery" }));
            Assert.That(result.Ok, Is.False);
            Assert.Throws<GameDataException>(() => result.ThrowIfFailed());
        }

        [Test]
        public void NotARule_And_NotPortedYet_AreClassified()
        {
            using var dir = new TempDir();
            dir.Write("leaf/Leaf.json", TwoLeaves);
            dir.Write("art/Sprite.json", @"[{""type"":""SpriteSheet"",""id"":""sheet.1""}]");
            dir.Write("later/Later.json", @"[{""type"":""LaterRule"",""id"":""later.1""}]");

            var result = Load(dir, b =>
            {
                Register(b);
                b.NotARule("SpriteSheet", "그림 전용");
                b.NotPortedYet("LaterRule", "core-world");
            });

            Assert.That(result.Ok, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.UnclassifiedTypes, Is.Empty);
            Assert.That(result.Definitions.TryGet<object>("sheet.1", out _), Is.False);
            Assert.That(result.Definitions.TryGet<object>("later.1", out _), Is.False);
            Assert.DoesNotThrow(() => result.ThrowIfFailed());
        }

        [Test]
        public void All_FollowsRegistryOrder()
        {
            using var dir = new TempDir();
            dir.Write("leaf/Leaf.json", @"[
                {""type"":""Leaf"",""id"":""leaf.a"",""power"":1},
                {""type"":""Leaf"",""id"":""leaf.b"",""power"":2},
                {""type"":""Leaf"",""id"":""leaf.c"",""power"":3},
                {""type"":""Leaf"",""id"":""leaf.unlisted"",""power"":4}]");
            dir.Write("branch/Branch.json", @"[{""type"":""Branch"",""id"":""branch.1"",""friend"":null,""leaves"":[]}]");
            dir.Write("registry.json", @"{""branches"":[""branch.1""],""leaves"":[""leaf.c"",""leaf.a"",""leaf.b""]}");

            var result = Load(dir);

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(result.Definitions.All<Leaf>().Select(l => l.Id), Is.EqualTo(new[] { "leaf.c", "leaf.a", "leaf.b", "leaf.unlisted" }));
            Assert.That(result.Definitions.All<IThing>().Count, Is.EqualTo(5), "기반 형·인터페이스로도 조회한다");
        }

        [Test]
        public void Get_UnknownId_Throws_WithIdAndTypeName()
        {
            using var dir = new TempDir();
            dir.Write("leaf/Leaf.json", TwoLeaves);

            var result = Load(dir);

            var e = Assert.Throws<KeyNotFoundException>(() => result.Definitions.Get<Leaf>("leaf.zzz"));
            Assert.That(e.Message, Does.Contain("leaf.zzz"));
            Assert.That(e.Message, Does.Contain(nameof(Leaf)));
            Assert.Throws<KeyNotFoundException>(() => result.Definitions.Get<Branch>("leaf.a"));
        }

        private sealed class Probe
        {
            public int[] Hex;
            public int[] Bare;
            public int[] BareBig;
            public int[] Empty;
            public int[] Plain;
            public bool Later;
            public List<Leaf> Slots;
            public int Peeked;
        }

        [Test]
        public void IntArray_ReadsUnityHexStrings()
        {
            // 변환기는 Unity의 묶은 int 배열을 늘 문자열로 낸다(숫자처럼 보이는 "1e00000003000000"도).
            using var dir = new TempDir();
            dir.Write("p/Probe.json", @"[{""type"":""Probe"",""id"":""p"",
                ""hex"":""0100000002000000"", ""bare"":""1e00000003000000"", ""bareBig"":""2800000004000000"", ""empty"":"""", ""plain"":[5,6]}]");

            var result = Load(dir, b => b.Add("Probe", (n, c) => new Probe
            {
                Hex = n.IntArray("hex"), Bare = n.IntArray("bare"), BareBig = n.IntArray("bareBig"),
                Empty = n.IntArray("empty"), Plain = n.IntArray("plain"),
            }));

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            var probe = result.Definitions.Get<Probe>("p");
            Assert.That(probe.Hex, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(probe.Bare, Is.EqualTo(new[] { 30, 3 }));
            Assert.That(probe.BareBig, Is.EqualTo(new[] { 40, 4 }));
            Assert.That(probe.Empty, Is.Empty);
            Assert.That(probe.Plain, Is.EqualTo(new[] { 5, 6 }));
        }

        [Test]
        public void IntArray_ABareNumber_IsAWrongTypeError()
        {
            // 따옴표 없는 16진 숫자를 원문으로 풀던 임시 처리는 변환기를 고친 뒤 지웠다.
            using var dir = new TempDir();
            dir.Write("p/Probe.json", @"[{""type"":""Probe"",""id"":""p"",""bare"":2800000004000000}]");

            var result = Load(dir, b => b.Add("Probe", (n, c) => new Probe { Bare = n.IntArray("bare") }));

            Assert.That(result.Errors.Single().Kind, Is.EqualTo("wrong-type"));
            Assert.That(result.Definitions.Get<Probe>("p").Bare, Is.Empty);
        }

        [Test]
        public void FieldAddedLater_MissingIsTheDefault_OtherMissingFieldsStillErrors()
        {
            using var dir = new TempDir();
            dir.Write("p/Probe.json", @"[{""type"":""Probe"",""id"":""p""}]");

            var result = Load(dir, b => b.Add("Probe", (n, c) =>
            {
                var o = n.Optional("later");
                return new Probe { Later = o.Bool("later"), Plain = o.IntArray("plain") };
            }));

            var error = result.Errors.Single();
            Assert.That(error.Kind, Is.EqualTo("missing-field"));
            Assert.That(error.Message, Does.Contain("plain"));
            Assert.That(result.Definitions.Get<Probe>("p").Later, Is.False);
        }

        [Test]
        public void Slots_KeepThePositionOfEmptyReferences()
        {
            using var dir = new TempDir();
            dir.Write("leaf/Leaf.json", TwoLeaves);
            dir.Write("p/Probe.json", @"[{""type"":""Probe"",""id"":""p"",
                ""items"":[{""ref"":""leaf.b"",""type"":""Leaf""}, null, {""ref"":""leaf.a"",""type"":""Leaf""}]}]");

            var result = Load(dir, b =>
            {
                Register(b);
                b.Add("Probe", (n, c) => new Probe { Slots = c.Slots<Leaf>(n, "items") });
            });

            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(result.Definitions.Get<Probe>("p").Slots.Select(l => l?.Id), Is.EqualTo(new[] { "leaf.b", null, "leaf.a" }));
        }

        [Test]
        public void TryPeek_ReadsAReferencedEntity_WithoutBuildingIt()
        {
            using var dir = new TempDir();
            dir.Write("later/Later.json", @"[{""type"":""LaterRule"",""id"":""later.1"",""stage"":4}]");
            dir.Write("p/Probe.json", @"[{""type"":""Probe"",""id"":""p"",""entry"":{""ref"":""later.1"",""type"":""LaterRule""}}]");

            var result = Load(dir, b =>
            {
                b.NotPortedYet("LaterRule", "core-world");
                b.Add("Probe", (n, c) => new Probe { Peeked = c.TryPeek(n, "entry", out var entry) ? entry.Int("stage") : -1 });
            });

            Assert.That(result.Ok, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Definitions.Get<Probe>("p").Peeked, Is.EqualTo(4));
        }

        [Test]
        public void AddingTheSameTypeTwice_Throws()
        {
            var b = new DefinitionBuilders();
            b.Add("Leaf", (n, c) => new Leaf(n.Id, 0));

            Assert.Throws<ArgumentException>(() => b.Add("Leaf", (n, c) => new Leaf(n.Id, 1)));
            Assert.Throws<ArgumentException>(() => b.NotARule("Leaf", "그림"));
            Assert.Throws<ArgumentException>(() => b.NotPortedYet("Leaf", "core-world"));
        }
    }
}
