using System;

namespace Rootborn.Host
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            HostOptions options;
            try
            {
                options = HostOptions.Resolve(args);
            }
            catch (InvalidOperationException e)
            {
                Console.Error.WriteLine(e.Message);
                return 2;
            }
            if (!options.Sample)
            {
                Console.Error.WriteLine("아직 옮긴 게임 화면이 없다. --sample 을 주면 UI 견본 화면을 띄운다.");
                Console.Error.WriteLine("인자: --sample  --size 1280x720  --root <저장소>  --font <TTF>  --evidence <스크린샷 폴더>");
                return 2;
            }
            using var game = new HostGame(options);
            game.Run();
            return 0;
        }
    }
}
