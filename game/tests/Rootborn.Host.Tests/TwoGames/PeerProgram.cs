using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 테스트 어셈블리의 진입점. dotnet test는 이것을 부르지 않는다(테스트 실행기가 어셈블리를 읽어 테스트를 돈다).
    /// 두 게임 테스트가 이 어셈블리를 <see cref="PeerProtocol.PeerArgument"/>로 자식 프로세스로 띄우면 두 번째 게임이 된다:
    /// 표준 입력에서 동사를 한 줄씩 읽어 <see cref="PeerVerbs"/>로 하고, 결과를 표준 출력에 한 줄로 답한다.
    /// 게임(Rootborn.Host)에는 테스트용 입구가 없다 — 여기서 같은 HostGame을 같은 PlayerDriver(장치 이벤트 주입)로 돌릴 뿐이다.
    /// 표준 입력이 닫히면(부모가 끝났거나 죽었다) 게임을 닫고 끝난다.
    /// </summary>
    public static class PeerProgram
    {
        /// <summary>시험용 가짜 자식: 동사 한 줄을 읽은 뒤 Tick만 계속 보내고(Go를 받을 때마다) 답은 하지 않는다. PeerGame의 총 한도 시험용.</summary>
        private static int RunFakeTicker()
        {
            var utf8 = new UTF8Encoding(false);
            TextReader input = new StreamReader(Console.OpenStandardInput(), utf8);
            var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
            if (input.ReadLine() == null) return 0;
            output.WriteLine("가짜 자식이 Tick을 보내기 시작한다");
            while (true)
            {
                output.WriteLine(PeerProtocol.Tick);
                if (input.ReadLine() == null) return 0; // 부모가 죽거나 끝나면 입력이 닫힌다
            }
        }

        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == PeerProtocol.FakeTickerArgument) return RunFakeTicker();
            if (args.Length == 0 || args[0] != PeerProtocol.PeerArgument)
            {
                Console.Error.WriteLine("Rootborn.Host.Tests: 테스트는 dotnet test로 돈다. 두 번째 게임으로 띄울 때만 " + PeerProtocol.PeerArgument + "를 준다.");
                return 2;
            }

            var utf8 = new UTF8Encoding(false);
            Console.OutputEncoding = utf8;
            Console.InputEncoding = utf8;
            TextReader input = new StreamReader(Console.OpenStandardInput(), utf8);
            var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };
            // 게임의 로그(Console.WriteLine)는 같은 줄 흐름으로 간다. 접두어(@@)가 없는 줄은 부모가 로그로 읽는다.
            Console.SetOut(output);
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            using var verbs = new PeerVerbs(input, output);
            return verbs.Run();
        }
    }
}
