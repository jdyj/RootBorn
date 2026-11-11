using System.Reflection;
using NUnit.Framework;
using Rootborn.Net.Room;

namespace Rootborn.Net.Tests
{
    /// <summary>방에 들어갈 때 맞춰 보는 버전을 서버와 Host가 같은 방법으로 만든다(RoomVersions.ForProgram).</summary>
    public sealed class RoomVersionsTests
    {
        [Test]
        public void ForProgram_UsesTheInformationalVersion_AndTheGivenFingerprint()
        {
            Assembly program = typeof(RoomVersions).Assembly;
            string informational = program.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            RoomVersions v = RoomVersions.ForProgram(program, "abcd");
            Assert.That(v.Protocol, Is.EqualTo(RoomProtocol.Version));
            Assert.That(v.Game, Is.EqualTo(informational));
            Assert.That(v.Data, Is.EqualTo("abcd"));
        }

        [Test]
        public void GameVersion_CarriesACommitStamp()
        {
            // Directory.Build.props: 1.0.0+<커밋> (git이 없으면 +unknown-commit 또는 ROOTBORN_COMMIT). 1.0.0만이면 다른 커밋끼리 가려내지 못한다.
            string game = RoomVersions.ForProgram(typeof(RoomVersions).Assembly, "x").Game;
            Assert.That(game, Does.Contain("+"), game);
            Assert.That(game.Substring(game.IndexOf('+') + 1), Is.Not.Empty);
        }
    }
}
