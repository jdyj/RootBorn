using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Net.Messages;
using Rootborn.Net.Room;
using Rootborn.Net.Session;

namespace Rootborn.Net.Tests
{
    /// <summary>
    /// 방에서 보이는 사람 이름(캐릭터 만들기에서 정한 이름). 화면 표시용일 뿐 신원(PlayerId)과 따로 오간다.
    /// 받은 이름은 줄바꿈·제어 문자를 빼고 길이를 자른다(화면이 깨지지 않게). 자동 테스트다.
    /// </summary>
    public sealed class PlayerNameTests
    {
        // 보이지 않는 문자는 코드 단위로 만든다(파일에 그대로 두면 편집기마다 다르게 보인다).
        private static readonly string LineSeparator = ((char)0x2028).ToString(), ParagraphSeparator = ((char)0x2029).ToString();
        private static readonly string LeftToRightMark = ((char)0x200E).ToString(), Joiner = ((char)0x200D).ToString();

        private static MessageCatalog Catalog() => RootbornMessages.CreateCatalog(4096);

        private static T RoundTrip<T>(T message) where T : class
        {
            var catalog = Catalog();
            Assert.That(catalog.TryEncode(message, out var bytes), Is.True);
            Assert.That(catalog.Decode(bytes, out var back), Is.EqualTo(DecodeOutcome.Ok));
            return (T)back;
        }

        [Test]
        public void Clean_RemovesLineBreaksAndControlCharacters_AndTrims()
        {
            Assert.That(PlayerName.Clean("  민\n지\r\t\u0007 "), Is.EqualTo("민지"));
            Assert.That(PlayerName.Clean("가" + LineSeparator + "나" + ParagraphSeparator + "다"), Is.EqualTo("가나다"), "줄 구분·문단 구분 문자도 뺀다");
            Assert.That(PlayerName.Clean("a" + LeftToRightMark + "b"), Is.EqualTo("ab"), "보이지 않는 방향 표시 문자도 뺀다");
            Assert.That(PlayerName.Clean(null), Is.Empty);
            Assert.That(PlayerName.Clean(" \n\t "), Is.Empty);
        }

        [Test]
        public void Clean_CutsToTheCharacterCreatorLimit_ByVisibleCharacters()
        {
            string longName = new string('가', PlayerName.MaxTextElements * 2);
            Assert.That(PlayerName.Clean(longName), Is.EqualTo(new string('가', PlayerName.MaxTextElements)));

            // 한 글자가 여러 코드 단위인 것(그림 글자)도 반으로 자르지 않는다.
            string family = char.ConvertFromUtf32(0x1F468) + Joiner + char.ConvertFromUtf32(0x1F469) + Joiner + char.ConvertFromUtf32(0x1F467);
            string many = string.Concat(Enumerable.Repeat(family, PlayerName.MaxTextElements + 3));
            string cut = PlayerName.Clean(many);
            Assert.That(Encoding.UTF8.GetByteCount(cut), Is.LessThanOrEqualTo(PlayerName.MaxBytes), "보낼 수 있는 크기 안");
            Assert.That(cut.Length % family.Length, Is.Zero, "글자 중간에서 자르지 않는다");
            Assert.That(cut, Is.Not.Empty);
        }

        [Test]
        public void HelloAndJoined_CarryTheName_AndReceivedNamesAreCleaned()
        {
            Assert.That(RoundTrip(new HelloMessage { PlayerId = "a", Look = "child|", Name = "민지" }).Name, Is.EqualTo("민지"));
            Assert.That(RoundTrip(new PlayerJoinedMessage { NetId = 2, PlayerId = "b", Name = "철수" }).Name, Is.EqualTo("철수"));
            Assert.That(RoundTrip(new PlayerJoinedMessage { NetId = 2, PlayerId = "b", Name = "철수", HasPose = true, Pose = new PoseMessage { MapId = "Town" } }).Name,
                Is.EqualTo("철수"), "몸이 함께 와도");

            // 보내는 쪽이 다듬지 않은 이름(고친 프로그램)을 보내도 받는 쪽이 다듬는다.
            var writer = new NetWriter(64);
            writer.WriteUInt16(1);
            writer.WriteByte(2);
            writer.WriteString("a");
            writer.WriteString("child|");
            writer.WriteString("나쁜\n이름");
            Assert.That(Catalog().Decode(writer.ToArray(), out var raw), Is.EqualTo(DecodeOutcome.Ok));
            Assert.That(((HelloMessage)raw).Name, Is.EqualTo("나쁜이름"));
        }

        [Test]
        public void OldHello_WithoutName_ReadsAsNoName()
        {
            var writer = new NetWriter(64);
            writer.WriteUInt16(1);
            writer.WriteByte(1);
            writer.WriteString("a");
            writer.WriteString("child|");
            Assert.That(Catalog().Decode(writer.ToArray(), out var old), Is.EqualTo(DecodeOutcome.Ok));
            Assert.That(((HelloMessage)old).Name, Is.Empty);
        }

        [Test]
        public void OthersSeeTheName_ButIdentityStaysTheId()
        {
            using var rig = new SessionRig();
            var a = rig.Join("slot-aaa", name: "민지");
            var b = rig.Join("slot-bbb", name: "  철\n수  ");
            rig.Run(0.5);

            Assert.That(b.TryGetPlayerName("slot-aaa", out string aName), Is.True);
            Assert.That(aName, Is.EqualTo("민지"));
            Assert.That(a.TryGetPlayerName("slot-bbb", out string bName), Is.True);
            Assert.That(bName, Is.EqualTo("철수"), "보내기 전에 다듬는다");
            Assert.That(a.TryGetPlayerName("slot-aaa", out string mine), Is.True);
            Assert.That(mine, Is.EqualTo("민지"), "자기 이름도");
            Assert.That(rig.Server.Players.Select(p => p.PlayerId), Is.EquivalentTo(new[] { "slot-aaa", "slot-bbb" }), "신원은 id 그대로");
            Assert.That(rig.Server.Players.Single(p => p.PlayerId == "slot-bbb").Name, Is.EqualTo("철수"));

            // 늦게 들어온 사람도 이미 있던 사람의 이름을 받는다.
            var c = rig.Join("slot-ccc");
            rig.Run(0.5);
            Assert.That(c.TryGetPlayerName("slot-aaa", out string seenByLate), Is.True);
            Assert.That(seenByLate, Is.EqualTo("민지"));
            Assert.That(a.TryGetPlayerName("slot-ccc", out string noName), Is.True);
            Assert.That(noName, Is.Empty, "이름 없이 들어오면 빈 이름(화면이 기본 말을 쓴다)");
            Assert.That(a.TryGetPlayerName("nobody", out _), Is.False);
        }

        [Test]
        public void RemoteRoomTime_GivesNamesOfPresentPeople()
        {
            using var rig = new SessionRig(timeRules: Rootborn.Core.Time.TimeRules.Legacy);
            var a = rig.Join("slot-aaa", name: "민지");
            var b = rig.Join("slot-bbb", name: "철수");
            using var time = new RemoteRoomTime(b, "local-player", Rootborn.Core.Time.TimeRules.Legacy);
            rig.Run(0.5);

            Assert.That(time.NameOf("slot-aaa"), Is.EqualTo("민지"));
            Assert.That(time.NameOf("local-player"), Is.EqualTo("철수"), "자기 자신은 게임 한 판의 신원으로 묻는다");
            Assert.That(time.NameOf("nobody"), Is.Empty);
        }

        [Test]
        public void ProtocolVersion_WasRaisedForNames_AndTheOldVersionIsRefused()
        {
            Assert.That(RoomProtocol.Version, Is.GreaterThanOrEqualTo(6), "이름이 들어간 접속 메시지 모양");
            var old = new RoomVersions(5, "g", "d");
            Assert.That(RoomRefusal.Check(new RoomVersions(RoomProtocol.Version, "g", "d"), old), Is.EqualTo(RoomRefusal.ProtocolMismatch));
        }
    }
}
