using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 겹친 캐릭터 그림(몸 → 옷 → 머리). 월드 화면과 같은 방식으로 겹친다: 층마다 자기 기준점(발)을 몸의 발 자리에 맞춘다.
    /// 몸 그림이 칸 안에 비율을 지키며 가장 크게 들어가도록 배율을 정한다(그림 크기를 가정하지 않는다).
    /// Rootborn.Ui에 없는 부품이라 화면 폴더에 둔다(보고서: 옷장 화면도 쓸 후보).
    /// </summary>
    public sealed class CharacterPreview : UiNode
    {
        private readonly List<PreviewFrame> _frames = new List<PreviewFrame>(4);

        /// <summary>그리는 층(몸이 맨 앞). 테스트가 본다.</summary>
        public IReadOnlyList<PreviewFrame> Frames => _frames;

        /// <summary>겉모습을 바꾼다(그림을 찾지 못한 층은 건너뛴다). 몸이 없으면 아무것도 그리지 않는다.</summary>
        public void Show(CreatorLook look, ICharacterPreviewArt art)
        {
            _frames.Clear();
            if (look == null || art == null) return;
            foreach (string sheet in look.SheetIds())
            {
                if (string.IsNullOrEmpty(sheet) || !art.TryStandingFrame(sheet, out PreviewFrame frame) || frame.Source.W <= 0 || frame.Source.H <= 0)
                {
                    if (sheet == look.BodyId) return; // 몸이 없으면 옷만 떠 보이지 않게
                    continue;
                }
                _frames.Add(frame);
            }
        }

        /// <summary>층마다 그릴 사각형(논리 좌표). 몸이 없으면 빈 목록.</summary>
        public void LayerRects(List<UiRect> into)
        {
            into.Clear();
            if (_frames.Count == 0) return;
            PreviewFrame body = _frames[0];
            float scale = System.Math.Min(Rect.W / body.Source.W, Rect.H / body.Source.H);
            float bodyW = body.Source.W * scale, bodyH = body.Source.H * scale;
            float bodyX = Rect.X + (Rect.W - bodyW) / 2f, bodyY = Rect.Y + (Rect.H - bodyH) / 2f;
            float feetX = bodyX + bodyW * body.PivotX, feetY = bodyY + bodyH * body.PivotY;
            foreach (PreviewFrame f in _frames)
            {
                float w = f.Source.W * scale, h = f.Source.H * scale;
                into.Add(new UiRect(feetX - w * f.PivotX, feetY - h * f.PivotY, w, h));
            }
        }

        private readonly List<UiRect> _rects = new List<UiRect>(4);

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            LayerRects(_rects);
            for (int i = 0; i < _rects.Count; i++) list.Image(_rects[i], _frames[i].Image, _frames[i].Source, UiTheme.White);
        }
    }
}
