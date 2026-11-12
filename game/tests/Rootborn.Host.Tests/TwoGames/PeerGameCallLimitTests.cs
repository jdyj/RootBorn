using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>Tick만 계속 보내고 답하지 않는 자식에게 Call하면 줄마다 한도에 안 걸려도 총 한도 뒤에 자식이 죽고 실패하는지 본다.</summary>
    [TestFixture, NonParallelizable]
    public sealed class PeerGameCallLimitTests
    {
        [Test]
        public void Call_ChildKeepsTickingWithoutAnswer_FailsAtTotalLimitAndKillsChild()
        {
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (string.IsNullOrEmpty(dotnet) || !File.Exists(dotnet)) dotnet = "dotnet";
            var info = new ProcessStartInfo(dotnet) { WorkingDirectory = AppContext.BaseDirectory };
            info.ArgumentList.Add(typeof(PeerProgram).Assembly.Location);
            info.ArgumentList.Add(PeerProtocol.FakeTickerArgument);
            using PeerGame peer = PeerGame.Start(info, "가짜");
            peer.CallLimit = TimeSpan.FromSeconds(3);

            var watch = Stopwatch.StartNew();
            var ex = Assert.Throws<PeerException>(() => peer.Call("never-answered"));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(60)), "총 한도(3초)에서 끝났다");
            Assert.That(ex.Message, Does.Contain("총 3초"));
            Assert.That(ex.Message, Does.Contain("가짜 자식이 Tick을 보내기 시작한다"), "자식 출력의 끝 줄이 메시지에 있다");
            Assert.That(peer.Exited, Is.True.After(10_000, 100), "자식을 죽였다");
        }
    }
}
