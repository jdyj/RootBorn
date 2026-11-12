using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using NUnit.Framework;
using Rootborn.Net.Room;

namespace Rootborn.Server.Tests
{
    public sealed class ServerOptionsTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-server-options", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "data"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static Func<string, string> Env(Dictionary<string, string> values = null)
            => name => values != null && values.TryGetValue(name, out var v) ? v : null;

        // 127.0.0.1은 테스트가 넘기는 값일 뿐이다(서버 코드에는 주소 기본값이 없다).
        private string[] Full(params string[] extra)
        {
            var args = new List<string> { "--root", _root, "--save-root", Path.Combine(_root, "saves"), "--slot", "room-1", "--bind", "127.0.0.1", "--port", "0" };
            args.AddRange(extra);
            return args.ToArray();
        }

        [Test]
        public void AllGiven_AreRead_AndCapacityDefaultsToFour()
        {
            var o = ServerOptions.Resolve(Full(), Env());
            Assert.That(o.DataDirectory, Is.EqualTo(Path.Combine(_root, "data")));
            Assert.That(o.SaveRoot, Is.EqualTo(Path.Combine(_root, "saves")));
            Assert.That(o.SaveSlot, Is.EqualTo("room-1"));
            Assert.That(o.BindAddress, Is.EqualTo(IPAddress.Loopback));
            Assert.That(o.Port, Is.EqualTo(0));
            Assert.That(o.MaxPlayers, Is.EqualTo(4));
        }

        [Test]
        public void LogAndAutosave_HaveSafeDefaults_AndReadArgumentsBeforeEnvironment()
        {
            var o = ServerOptions.Resolve(Full(), Env());
            Assert.That(o.LogFile, Is.Null, "로그 파일 기본 경로를 지어내지 않는다 — 표준 출력만");
            Assert.That(o.LogMaxBytes, Is.EqualTo(ServerOptions.DefaultLogMaxBytes));
            Assert.That(o.LogFiles, Is.EqualTo(ServerOptions.DefaultLogFiles));
            Assert.That(o.AutosaveSeconds, Is.EqualTo(ServerOptions.DefaultAutosaveSeconds));

            string file = Path.Combine(_root, "server.log");
            o = ServerOptions.Resolve(Full("--log", file, "--log-max-mb", "2", "--log-files", "0", "--autosave-seconds", "0"), Env(new Dictionary<string, string>
            {
                [ServerOptions.LogVariable] = "ignored.log", [ServerOptions.AutosaveVariable] = "30",
            }));
            Assert.That(o.LogFile, Is.EqualTo(file));
            Assert.That(o.LogMaxBytes, Is.EqualTo(2L * 1024 * 1024));
            Assert.That(o.LogFiles, Is.EqualTo(0));
            Assert.That(o.AutosaveSeconds, Is.EqualTo(0), "0이면 끈다");

            o = ServerOptions.Resolve(Full(), Env(new Dictionary<string, string> { [ServerOptions.LogVariable] = file, [ServerOptions.AutosaveVariable] = "2.5" }));
            Assert.That(o.LogFile, Is.EqualTo(file));
            Assert.That(o.AutosaveSeconds, Is.EqualTo(2.5));
        }

        [TestCase("--autosave-seconds", "-1")]
        [TestCase("--autosave-seconds", "soon")]
        [TestCase("--log-max-mb", "0")]
        [TestCase("--log-max-mb", "big")]
        [TestCase("--log-files", "-1")]
        public void BadLogOrAutosaveValues_AreArgumentErrors(string name, string value)
        {
            var e = Assert.Throws<ServerOptionsException>(() => ServerOptions.Resolve(Full(name, value), Env()));
            StringAssert.Contains(name, e.Message);
        }

        [Test]
        public void Help_MentionsTheNewArguments_TheConsoleCommands_AndTheExitCodes()
        {
            foreach (var word in new[] { "--log ", "--log-max-mb", "--log-files", "--autosave-seconds", "status", "6 슬롯이 이미 열려 있음", "7 세이브" })
                StringAssert.Contains(word, ServerOptions.HelpText);
        }

        [Test]
        public void Share_DefaultsToTimeOnly_ReadsNamesFromArgumentOrEnvironment_AndRefusesUnknownOnes()
        {
            Assert.That(ServerOptions.Resolve(Full(), Env()).SharedPieces, Is.Empty, "기본은 시간만");
            Assert.That(ServerOptions.Resolve(Full("--share", "wallet, town"), Env()).SharedPieces, Is.EqualTo(new[] { "wallet", "town" }));
            Assert.That(ServerOptions.Resolve(Full("--share", ServerOptions.ShareNothing), Env()).SharedPieces, Is.Empty);
            Assert.That(ServerOptions.Resolve(Full(), Env(new Dictionary<string, string> { [ServerOptions.ShareVariable] = "wallet" })).SharedPieces,
                Is.EqualTo(new[] { "wallet" }));
            var e = Assert.Throws<ServerOptionsException>(() => ServerOptions.Resolve(Full("--share", "items"), Env()));
            StringAssert.Contains("items", e.Message);
            StringAssert.Contains("wallet", e.Message, "아는 이름을 알려 준다");
        }

        [Test]
        public void DayCopies_DefaultsToThree_AndMustNotBeNegative()
        {
            Assert.That(ServerOptions.Resolve(Full(), Env()).DayCopies, Is.EqualTo(RoomServerSettings.DefaultDayCopies));
            Assert.That(ServerOptions.Resolve(Full("--day-copies", "0"), Env()).DayCopies, Is.Zero);
            Assert.Throws<ServerOptionsException>(() => ServerOptions.Resolve(Full("--day-copies", "-1"), Env()));
        }

        [Test]
        public void Missing_SaveSlotAddressAndPort_ListsEveryMissingValue()
        {
            var e = Assert.Throws<ServerOptionsException>(() => ServerOptions.Resolve(new[] { "--root", _root }, Env()));
            StringAssert.Contains("--save-root", e.Message);
            StringAssert.Contains(ServerOptions.SaveRootVariable, e.Message);
            StringAssert.Contains("--slot", e.Message);
            StringAssert.Contains(ServerOptions.SlotVariable, e.Message);
            StringAssert.Contains("--bind", e.Message);
            StringAssert.Contains("--port", e.Message);
            Assert.That(e.Missing.Count, Is.EqualTo(4));
        }

        [Test]
        public void EnvironmentVariables_GiveTheSameValues_AndArgumentsWin()
        {
            var env = Env(new Dictionary<string, string>
            {
                [ServerOptions.RootVariable] = _root,
                [ServerOptions.SaveRootVariable] = Path.Combine(_root, "env-saves"),
                [ServerOptions.SlotVariable] = "env-slot",
                [ServerOptions.BindVariable] = "127.0.0.1",
                [ServerOptions.PortVariable] = "27100",
                [ServerOptions.MaxPlayersVariable] = "6",
            });
            var fromEnv = ServerOptions.Resolve(Array.Empty<string>(), env);
            Assert.That(fromEnv.SaveSlot, Is.EqualTo("env-slot"));
            Assert.That(fromEnv.Port, Is.EqualTo(27100));
            Assert.That(fromEnv.MaxPlayers, Is.EqualTo(6));

            var argsWin = ServerOptions.Resolve(new[] { "--slot", "arg-slot", "--port", "27200", "--max-players", "2" }, env);
            Assert.That(argsWin.SaveSlot, Is.EqualTo("arg-slot"));
            Assert.That(argsWin.Port, Is.EqualTo(27200));
            Assert.That(argsWin.MaxPlayers, Is.EqualTo(2));
            Assert.That(argsWin.SaveRoot, Is.EqualTo(Path.Combine(_root, "env-saves")));
        }

        [Test]
        public void DataFolder_CanBeGivenDirectly()
        {
            string data = Path.Combine(_root, "other-data");
            Directory.CreateDirectory(data);
            var o = ServerOptions.Resolve(new[] { "--data", data, "--save-root", _root, "--slot", "s", "--bind", "127.0.0.1", "--port", "1" }, Env());
            Assert.That(o.DataDirectory, Is.EqualTo(data));
        }

        [Test]
        public void NoRootNoData_IsAnError()
        {
            var e = Assert.Throws<ServerOptionsException>(() => ServerOptions.Resolve(
                new[] { "--save-root", _root, "--slot", "s", "--bind", "127.0.0.1", "--port", "1" }, Env(), searchFrom: Path.GetPathRoot(_root)));
            StringAssert.Contains("--root", e.Message);
            StringAssert.Contains("--data", e.Message);
        }

        [TestCase("--max-players", "0")]
        [TestCase("--max-players", "9")]
        [TestCase("--max-players", "x")]
        [TestCase("--port", "-1")]
        [TestCase("--port", "65536")]
        [TestCase("--port", "abc")]
        [TestCase("--bind", "not-an-address")]
        public void BadValues_AreErrors(string name, string value)
        {
            var e = Assert.Throws<ServerOptionsException>(() => ServerOptions.Resolve(Full(name, value), Env()));
            StringAssert.Contains(name, e.Message);
        }

        [Test]
        public void MaxPlayersUpToLimit_IsAllowed()
        {
            Assert.That(ServerOptions.Resolve(Full("--max-players", RoomServerSettings.MaxPlayersLimit.ToString()), Env()).MaxPlayers, Is.EqualTo(8));
        }

        [Test]
        public void UnknownOrValuelessArgument_IsAnError()
        {
            Assert.Throws<ServerOptionsException>(() => ServerOptions.Resolve(Full("--nope", "1"), Env()));
            Assert.Throws<ServerOptionsException>(() => ServerOptions.Resolve(Full("--slot"), Env()));
        }

        [Test]
        public void Help_IsRecognised_AndTheHelpTextNamesEveryArgument()
        {
            Assert.That(ServerOptions.Resolve(new[] { "--help" }, Env()).HelpRequested, Is.True);
            foreach (var name in new[] { "--root", "--data", "--save-root", "--slot", "--bind", "--port", "--max-players" })
                StringAssert.Contains(name, ServerOptions.HelpText);
        }
    }
}
