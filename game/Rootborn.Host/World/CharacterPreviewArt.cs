using Rootborn.Screens.Start;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.World
{
    /// <summary>
    /// 시작 화면의 캐릭터 미리보기 그림: 월드가 그리는 것과 같은 캐릭터 시트(data/characters + 좌표표)에서 아래를 보고 선 칸을 준다.
    /// 그림 id는 PNG 경로, 칸은 좌표표 값이다(UiRenderer가 그 PNG의 그 칸을 그린다).
    /// </summary>
    public sealed class CharacterPreviewArt : ICharacterPreviewArt
    {
        private readonly CharacterArt _art;

        public CharacterPreviewArt(CharacterArt art) { _art = art; }

        public bool TryStandingFrame(string sheetId, out PreviewFrame frame)
        {
            frame = default;
            CharacterFrame f = _art?.Sheet(sheetId)?.Frame(CharacterAnimator.RowDown, CharacterAnimator.StandingColumn);
            if (f == null) return false;
            frame = new PreviewFrame(f.Texture, new PixelRect(f.X, f.Y, f.W, f.H), f.PivotX, f.PivotY);
            return true;
        }
    }
}
