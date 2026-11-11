using System;
using NUnit.Framework;
using Rootborn.Net.Messages;

namespace Rootborn.Net.Tests
{
    public sealed class MessageCatalogTests
    {
        private static MessageCatalog Catalog() => RootbornMessages.CreateCatalog(4096);

        private static T RoundTrip<T>(T message) where T : class
        {
            var catalog = Catalog();
            Assert.That(catalog.TryEncode(message, out var bytes), Is.True);
            Assert.That(catalog.Decode(bytes, out var back), Is.EqualTo(DecodeOutcome.Ok));
            return (T)back;
        }

        [Test]
        public void Header_IsKindLittleEndianThenVersion()
        {
            Catalog().TryEncode(new PoseMessage { MapId = "Town" }, out var bytes);
            Assert.That(bytes[0], Is.EqualTo(6));
            Assert.That(bytes[1], Is.EqualTo(0));
            Assert.That(bytes[2], Is.EqualTo(1));
        }

        [Test]
        public void EveryRootbornMessage_RoundTrips()
        {
            var hello = RoundTrip(new HelloMessage { PlayerId = "스팀-1", Look = "child|a,b" });
            Assert.That(hello.PlayerId, Is.EqualTo("스팀-1"));
            Assert.That(hello.Look, Is.EqualTo("child|a,b"));

            var welcome = RoundTrip(new WelcomeMessage { NetId = 7, JoinIndex = 5, SpawnOffsetX = -1f, SpawnOffsetY = -1f });
            Assert.That((welcome.NetId, welcome.JoinIndex, welcome.SpawnOffsetX, welcome.SpawnOffsetY), Is.EqualTo(((ushort)7, 5, -1f, -1f)));

            Assert.That(RoundTrip(new FarewellMessage { Reason = "server full" }).Reason, Is.EqualTo("server full"));

            var pose = RoundTrip(new PoseMessage { NetId = 3, Sequence = 42, SenderTime = 12.5, MapId = "House", X = 1.5f, Y = -2f, Facing = 2, Walking = true });
            Assert.That((pose.NetId, pose.Sequence, pose.SenderTime, pose.MapId, pose.X, pose.Y, pose.Facing, pose.Walking),
                Is.EqualTo(((ushort)3, 42u, 12.5, "House", 1.5f, -2f, (byte)2, true)));

            var joined = RoundTrip(new PlayerJoinedMessage { NetId = 2, PlayerId = "b", Look = "teen|", HasPose = true, Pose = new PoseMessage { Sequence = 9, MapId = "Town", X = 4f } });
            Assert.That(joined.HasPose, Is.True);
            Assert.That(joined.Pose.X, Is.EqualTo(4f));
            Assert.That(joined.Pose.NetId, Is.EqualTo(2));
            Assert.That(RoundTrip(new PlayerJoinedMessage { NetId = 2, PlayerId = "b" }).HasPose, Is.False);

            Assert.That(RoundTrip(new PlayerLeftMessage { NetId = 9 }).NetId, Is.EqualTo(9));
            Assert.That(RoundTrip(new LookMessage { NetId = 1, Look = "child|x" }).Look, Is.EqualTo("child|x"));

            var clock = RoundTrip(new RoomClockMessage
            {
                Day = 12, PartIndex = 1, PartsToday = 3,
                Entries = new[] { new RoomClockEntry { NetId = 4, FinishedPart = true }, new RoomClockEntry { NetId = 7, FinishedPart = true, DayEnded = true }, new RoomClockEntry { NetId = 9 } },
            });
            Assert.That((clock.Day, clock.PartIndex, clock.PartsToday, clock.Entries.Length), Is.EqualTo((12, (byte)1, (byte)3, 3)));
            Assert.That((clock.Entries[1].NetId, clock.Entries[1].FinishedPart, clock.Entries[1].DayEnded), Is.EqualTo(((ushort)7, true, true)));
            Assert.That((clock.Entries[2].FinishedPart, clock.Entries[2].DayEnded), Is.EqualTo((false, false)));
            Assert.That(RoundTrip(new FinishPartMessage { Points = 2 }).Points, Is.EqualTo(2));

            Assert.That(RoundTrip(new DayEndReadyMessage()), Is.Not.Null);
            Assert.That(RoundTrip(new HeartbeatMessage()), Is.Not.Null);
        }

        [Test]
        public void UnknownKind_TooNewVersion_Truncated_TooLarge_AreReportedWithoutThrowing()
        {
            var catalog = Catalog();
            Assert.That(catalog.Decode(new byte[] { 0xFF, 0xFF, 1 }, out _), Is.EqualTo(DecodeOutcome.UnknownKind));
            Assert.That(catalog.Decode(new byte[] { 1, 0, 2, 0, 0, 0, 0 }, out _), Is.EqualTo(DecodeOutcome.TooNewVersion));
            Assert.That(catalog.Decode(new byte[] { 1, 0 }, out _), Is.EqualTo(DecodeOutcome.Malformed));
            Assert.That(catalog.Decode(null, out _), Is.EqualTo(DecodeOutcome.Malformed));
            Assert.That(catalog.Decode(new byte[4097], out _), Is.EqualTo(DecodeOutcome.TooLarge));

            catalog.TryEncode(new PoseMessage { MapId = "Town", X = 3f }, out var pose);
            for (int cut = 0; cut < pose.Length; cut++)
                Assert.That(catalog.Decode(pose.AsSpan(0, cut).ToArray(), out _), Is.EqualTo(DecodeOutcome.Malformed), "잘린 길이 " + cut);
        }

        [Test]
        public void TrailingBytes_AreIgnoredForForwardCompatibility()
        {
            var catalog = Catalog();
            catalog.TryEncode(new PlayerLeftMessage { NetId = 4 }, out var bytes);
            var longer = new byte[bytes.Length + 3];
            bytes.CopyTo(longer, 0);
            Assert.That(catalog.Decode(longer, out var back), Is.EqualTo(DecodeOutcome.Ok));
            Assert.That(((PlayerLeftMessage)back).NetId, Is.EqualTo(4));
        }

        [Test]
        public void BadValues_AreMalformed()
        {
            var catalog = Catalog();
            Assert.That(catalog.TryEncode(new PoseMessage { MapId = "Town", X = float.NaN }, out var nan), Is.True);
            Assert.That(catalog.Decode(nan, out _), Is.EqualTo(DecodeOutcome.Malformed), "NaN 위치");
            catalog.TryEncode(new PoseMessage { MapId = "Town", Facing = 9 }, out var facing);
            Assert.That(catalog.Decode(facing, out _), Is.EqualTo(DecodeOutcome.Malformed), "없는 방향");
            catalog.TryEncode(new PoseMessage { MapId = "" }, out var noMap);
            Assert.That(catalog.Decode(noMap, out _), Is.EqualTo(DecodeOutcome.Malformed), "맵 없는 위치");
            catalog.TryEncode(new RoomClockMessage { Day = 0 }, out var day0);
            Assert.That(catalog.Decode(day0, out _), Is.EqualTo(DecodeOutcome.Malformed), "0일");
            catalog.TryEncode(new RoomClockMessage { Day = 1, PartIndex = 3, PartsToday = 2 }, out var pastTheDay);
            Assert.That(catalog.Decode(pastTheDay, out _), Is.EqualTo(DecodeOutcome.Malformed), "하루의 때 수를 넘은 때");
            catalog.TryEncode(new FinishPartMessage { Points = 0 }, out var noPoints);
            Assert.That(catalog.Decode(noPoints, out _), Is.EqualTo(DecodeOutcome.Malformed), "0 포인트로 때를 끝내기");
            catalog.TryEncode(new HelloMessage { PlayerId = new string('a', RootbornMessages.MaxPlayerIdBytes + 1) }, out var longId);
            Assert.That(catalog.Decode(longId, out _), Is.EqualTo(DecodeOutcome.Malformed), "너무 긴 신원");
            Assert.That(catalog.Decode(new byte[] { 1, 0, 1, 2, 0, 0xC3, 0x28, 0, 0 }, out _), Is.EqualTo(DecodeOutcome.Malformed), "UTF-8이 아닌 글자");
        }

        [Test]
        public void TooLargeToSend_IsRefusedByEncode()
        {
            var catalog = RootbornMessages.CreateCatalog(16);
            Assert.That(catalog.TryEncode(new HelloMessage { PlayerId = "a-long-player-id", Look = "child|" }, out var bytes), Is.False);
            Assert.That(bytes, Is.Null);
            Assert.That(catalog.TryEncode(new object(), out _), Is.False, "등록하지 않은 형식");
        }

        [Test]
        public void NewKind_IsOneLine_AndKindNumbersCannotBeReused()
        {
            var catalog = Catalog();
            catalog.Add<Extra>(500, 1, (w, m) => w.WriteInt32(m.Value), (NetReader r, int v, out Extra m) =>
            {
                m = new Extra();
                return r.TryReadInt32(out m.Value);
            });
            catalog.TryEncode(new Extra { Value = 33 }, out var bytes);
            catalog.Decode(bytes, out var back);
            Assert.That(((Extra)back).Value, Is.EqualTo(33));
            Assert.Throws<ArgumentException>(() => catalog.Add<Other>(6, 1, (w, m) => { }, (NetReader r, int v, out Other m) => { m = new Other(); return true; }));
        }

        [Test]
        public void OlderVersion_IsReadByTheRegisteredReader()
        {
            var catalog = new MessageCatalog(64);
            catalog.Add<Extra>(1, 2, (w, m) => { w.WriteInt32(m.Value); w.WriteInt32(m.Second); }, (NetReader r, int v, out Extra m) =>
            {
                m = new Extra();
                if (!r.TryReadInt32(out m.Value)) return false;
                return v < 2 || r.TryReadInt32(out m.Second);
            });
            Assert.That(catalog.Decode(new byte[] { 1, 0, 1, 5, 0, 0, 0 }, out var old), Is.EqualTo(DecodeOutcome.Ok));
            Assert.That(((Extra)old).Value, Is.EqualTo(5));
            Assert.That(((Extra)old).Second, Is.EqualTo(0));
        }

        [Test]
        public void Handlers_DispatchByType()
        {
            int seen = 0;
            var handlers = new MessageHandlers<string>().On<PlayerLeftMessage>((ctx, m) => seen = m.NetId);
            Assert.That(handlers.Dispatch("x", new PlayerLeftMessage { NetId = 3 }), Is.True);
            Assert.That(seen, Is.EqualTo(3));
            Assert.That(handlers.Dispatch("x", new HeartbeatMessage()), Is.False);
        }

        private sealed class Extra
        {
            public int Value;
            public int Second;
        }

        private sealed class Other
        {
        }
    }
}
