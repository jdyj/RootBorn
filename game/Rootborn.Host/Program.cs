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
            if (!options.Sample && !options.WorldActive)
            {
                Console.Error.WriteLine("띄울 화면을 고른다: --slot <슬롯> --save-root <세이브 폴더>(새 게임·이어 하기), --world <맵>(예: Shop, Town, House) 또는 --sample(UI 견본).");
                Console.Error.WriteLine("인자: --slot <슬롯>  --save-root <세이브 폴더>  --world <맵>  --spawn <도착 지점>  --maps <맵 목록>  --sample  --size 1280x720  --root <저장소>  --font <TTF>  --evidence <스크린샷 폴더>");
                return 2;
            }
            using var game = new HostGame(options);
            game.Run();
            return 0;
        }
    }
}
