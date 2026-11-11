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
            if (!options.Sample && !options.WorldActive && !options.StartMenu)
            {
                Console.Error.WriteLine("세이브 폴더를 준다: --save-root <세이브 폴더>(또는 환경 변수 " + HostOptions.SaveRootVariable + ")면 슬롯 선택 화면부터 시작한다.");
                Console.Error.WriteLine("다른 화면: --slot <슬롯> --save-root <세이브 폴더>(그 슬롯으로 바로), --world <맵>(예: Shop, Town, House) 또는 --sample(UI 견본).");
                Console.Error.WriteLine("인자: --slot <슬롯>  --save-root <세이브 폴더>  --world <맵>  --spawn <도착 지점>  --maps <맵 목록>  --sample  --size 1280x720  --fullscreen | --windowed  --root <저장소>  --font <TTF>  --evidence <스크린샷 폴더>");
                Console.Error.WriteLine("      --lang <언어>(없으면 저장된 설정 → 운영체제 언어 → 기준 언어)  --ui-theme <테마 이름 또는 폴더>(없으면 " + HostOptions.UiThemeVariable + " → " + HostOptions.DefaultUiTheme + ")");
                Console.Error.WriteLine("      --join <호스트:포트>(또는 " + HostOptions.JoinVariable + ": 서버 주소로 들어가기. --save-root가 필요하고, --slot을 주면 그 인물로 바로 접속)");
                Console.Error.WriteLine("      --frames <N>(N프레임 그린 뒤 끝낸다)  --screenshot <PNG>(--frames와 함께: 끝내기 직전 화면을 저장한다)");
                Console.Error.WriteLine("      --day-copies <N>(다음 날마다 슬롯의 backups/<날>/에 남길 사본 수, 0이면 끔. 없으면 " + HostOptions.DayCopiesVariable + " → " + Rootborn.Game.GameSession.DefaultDayCopiesToKeep + ")");
                return 2;
            }
            using var game = new HostGame(options);
            game.Run();
            return 0;
        }
    }
}
