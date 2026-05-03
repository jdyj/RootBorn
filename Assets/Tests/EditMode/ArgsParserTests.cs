using NUnit.Framework;
using Rootborn.Game.Bootstrap;

namespace Rootborn.Tests.EditMode
{
    public sealed class ArgsParserTests
    {
        [Test]
        public void NoArgs_ReturnsModeNone()
        {
            var cfg = ArgsParser.Parse(new string[] { });
            Assert.AreEqual(SessionMode.None, cfg.Mode);
            Assert.AreEqual((ushort)7777, cfg.Port);
            Assert.AreEqual(4, cfg.MaxPlayers);
            Assert.AreEqual("default", cfg.SaveSlot);
        }

        [Test]
        public void ServerMode_ParsesAllFields()
        {
            var cfg = ArgsParser.Parse(new[]
            {
                "rootborn-server.exe", "-batchmode", "-nographics",
                "-mode", "server",
                "-port", "9999",
                "-maxPlayers", "8",
                "-saveSlot", "myfarm"
            });
            Assert.AreEqual(SessionMode.Server, cfg.Mode);
            Assert.AreEqual((ushort)9999, cfg.Port);
            Assert.AreEqual(8, cfg.MaxPlayers);
            Assert.AreEqual("myfarm", cfg.SaveSlot);
        }

        [Test]
        public void ClientMode_ParsesJoinIp()
        {
            var cfg = ArgsParser.Parse(new[]
            {
                "-mode", "client", "-joinIp", "192.168.1.10", "-port", "7777"
            });
            Assert.AreEqual(SessionMode.Client, cfg.Mode);
            Assert.AreEqual("192.168.1.10", cfg.JoinIp);
        }

        [Test]
        public void HostMode_CaseInsensitive()
        {
            var cfg = ArgsParser.Parse(new[] { "-mode", "HOST" });
            Assert.AreEqual(SessionMode.Host, cfg.Mode);
        }

        [Test]
        public void UnknownMode_DefaultsToNone()
        {
            var cfg = ArgsParser.Parse(new[] { "-mode", "junk" });
            Assert.AreEqual(SessionMode.None, cfg.Mode);
        }

        [Test]
        public void UnityReservedArgs_AreIgnored()
        {
            var cfg = ArgsParser.Parse(new[] { "-batchmode", "-nographics", "-logFile", "out.log" });
            Assert.AreEqual(SessionMode.None, cfg.Mode);
            Assert.AreEqual((ushort)7777, cfg.Port);
        }

        [Test]
        public void PortInvalid_KeepsDefault()
        {
            var cfg = ArgsParser.Parse(new[] { "-mode", "server", "-port", "notanumber" });
            Assert.AreEqual((ushort)7777, cfg.Port);
        }
    }
}
