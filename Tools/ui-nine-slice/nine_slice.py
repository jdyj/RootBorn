"""UI 9조각 측정과 견본판 (2026-11-11 사용자 요청: "스킬 만들어줘", 스킬 ui-nine-slice-skin).

게임(game/Rootborn.Ui/NineSlice.cs Build, 테마 "fill": "tile")과 같은 방식으로 반복해 그린다:
모서리 4개는 한 번, 위·아래 변은 가로로, 왼쪽·오른쪽 변은 세로로, 가운데는 양쪽으로 반복한다.

  python Tools/ui-nine-slice/nine_slice.py measure <시트.png> --at X,Y [--out 폴더]
  python Tools/ui-nine-slice/nine_slice.py measure <시트.png> --rect X,Y,W,H [--out 폴더]
  python Tools/ui-nine-slice/nine_slice.py preview <시트.png> --rect X,Y,W,H --border L,T,R,B [--scale 3] [--out 폴더]

measure는 rect(그림 칸)와 border(테두리 두께)를 짐작하지 않고 계산한다:
  - rect: --at 점에서 이어진 불투명 픽셀 덩어리의 경계 상자(--rect를 주면 그대로).
  - border: 열 전체(위 테두리부터 아래 테두리까지)가 똑같이 되풀이되는 구간을 찾아, 그 앞이 왼쪽 테두리,
    한 주기 뒤부터가 오른쪽 테두리다. 행도 같다. 열 전체를 비교하므로 위·아래 변과 가운데가 함께 이어진다.
측정 뒤 preview를 같이 그린다. 결과(JSON, 견본판 PNG)는 --out 폴더(기본: 저장소 output/ui-nine-slice/)에 둔다.
"""
import argparse, json, sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

REPO = Path(__file__).resolve().parents[2]
MAX_PERIOD = 16


def load(path):
    a = np.asarray(Image.open(path).convert("RGBA")).copy()
    a[a[..., 3] == 0] = 0  # 완전 투명 픽셀은 색이 달라도 같은 것으로 본다
    return a


def blob_rect(a, x, y):
    h, w = a.shape[:2]
    if not (0 <= x < w and 0 <= y < h) or a[y, x, 3] == 0:
        sys.exit(f"({x},{y})는 투명하거나 그림 밖이다. 테두리 안쪽의 불투명한 점을 고른다")
    seen = np.zeros((h, w), bool); seen[y, x] = True; q = deque([(x, y)]); x0 = x1 = x; y0 = y1 = y
    while q:
        cx, cy = q.popleft()
        x0, x1, y0, y1 = min(x0, cx), max(x1, cx), min(y0, cy), max(y1, cy)
        for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
            if 0 <= nx < w and 0 <= ny < h and not seen[ny, nx] and a[ny, nx, 3] > 0:
                seen[ny, nx] = True; q.append((nx, ny))
    return x0, y0, x1 - x0 + 1, y1 - y0 + 1


def find_repeat(lines):
    """lines[i] = 열(또는 행) i 전체. 가운데를 지나는(없으면 가장 긴) 되풀이 구간을 찾는다.
    돌려주는 값: (앞 테두리, 주기, 뒤 테두리, 되풀이 구간 길이) 또는 None. 주기는 짧은 것을 먼저 본다."""
    n = len(lines); mid = n // 2
    for p in range(1, min(MAX_PERIOD, n // 3) + 1):
        eq = [np.array_equal(lines[i], lines[i + p]) for i in range(n - p)]
        runs = []; i = 0
        while i < len(eq):
            if eq[i]:
                j = i
                while j + 1 < len(eq) and eq[j + 1]: j += 1
                runs.append((i, j)); i = j + 1
            else:
                i += 1
        runs = [(s, e) for s, e in runs if e - s + 1 >= p]  # 적어도 두 주기가 이어져야 반복이라 본다
        if not runs: continue
        hit = [r for r in runs if r[0] <= mid <= r[1] + p]
        s, e = (hit or [max(runs, key=lambda r: r[1] - r[0])])[0]
        # 되풀이되는 줄은 s..e+p. 가운데 칸은 그 구간 안에서 주기의 배수만큼 크게 잡는다.
        # 가운데를 1px로 두면 게임이 큰 창을 그릴 때 조각이 수만 개가 된다(NineSlice.Build는 가운데 칸 하나를 한 조각으로 반복).
        run = e + p + 1 - s; k = s + p * (run // p)
        return s, p, n - k, run
    return None


def build_pieces(dest_w, dest_h, rect, border, scale):
    """NineSlice.Build를 그대로 옮긴 것. (화면 x, y, w, h, 원본 x, y, w, h) 목록."""
    rx, ry, rw, rh = rect; l, t, r, b = border
    cx = [rx, rx + l, rx + rw - r, rx + rw]; cy = [ry, ry + t, ry + rh - b, ry + rh]
    cw = lambda c: cx[c + 1] - cx[c]; ch = lambda c: cy[c + 1] - cy[c]
    left = min(cw(0) * scale, dest_w // 2); right = min(cw(2) * scale, dest_w - left)
    top = min(ch(0) * scale, dest_h // 2); bottom = min(ch(2) * scale, dest_h - top)
    out = []
    for row in range(3):
        y = 0 if row == 0 else top if row == 1 else dest_h - bottom
        span_h = top if row == 0 else dest_h - top - bottom if row == 1 else bottom
        step_h = ch(1) * scale if row == 1 else span_h
        for col in range(3):
            x = 0 if col == 0 else left if col == 1 else dest_w - right
            span_w = left if col == 0 else dest_w - left - right if col == 1 else right
            step_w = cw(1) * scale if col == 1 else span_w
            if span_w <= 0 or span_h <= 0: continue
            for oy in range(0, span_h, step_h):
                h = min(step_h, span_h - oy); sh = min(ch(row), (h + scale - 1) // scale)
                for ox in range(0, span_w, step_w):
                    w = min(step_w, span_w - ox); sw = min(cw(col), (w + scale - 1) // scale)
                    out.append((x + ox, y + oy, w, h, cx[col], cy[row], sw, sh))
    return out


def render(sheet, w, h, rect, border, scale):
    im = Image.new("RGBA", (w, h))
    for x, y, pw, ph, sx, sy, sw, sh in build_pieces(w, h, rect, border, scale):
        piece = sheet.crop((sx, sy, sx + sw, sy + sh)).resize((sw * scale, sh * scale), Image.NEAREST).crop((0, 0, pw, ph))
        im.paste(piece, (x, y))
    return im


def checker(w, h, s=8):
    yy, xx = np.mgrid[0:h, 0:w]; v = np.where(((xx // s) + (yy // s)) % 2 == 0, 70, 90).astype(np.uint8)
    return Image.fromarray(np.dstack([v, v, v + 6, np.full_like(v, 255)]), "RGBA")


def preview(sheet_path, rect, border, scale, out, period=(1, 1)):
    sheet = Image.open(sheet_path).convert("RGBA"); rx, ry, rw, rh = rect; l, t, r, b = border
    mw, mh = rw - l - r, rh - t - b
    # 대화창·큰 창은 가운데 칸을 정수 번 반복한 크기(가운데 칸은 무늬 주기의 배수라 무늬가 이어진다).
    # 홀수 크기는 일부러 반복이 끝에서 잘리게 둔다(게임 창은 아무 크기나 된다).
    fit = lambda target, edge, mid: (edge + mid * max(1, round((target / scale - edge) / mid))) * scale
    sizes = [("source x%d" % scale, rw * scale, rh * scale),
             ("dialogue (wide)", fit(600, l + r, mw), fit(150, t + b, mh)),
             ("window (big)", fit(480, l + r, mw), fit(360, t + b, mh)),
             ("odd size (cut tiles)", (l + r) * scale + 157, (t + b) * scale + 61)]
    pad = 16; zoom = max(4, 96 // max(rw, rh))
    src = sheet.crop((rx, ry, rx + rw, ry + rh)).resize((rw * zoom, rh * zoom), Image.NEAREST)
    lines = Image.new("RGBA", src.size); d = ImageDraw.Draw(lines)
    for xv in (l, rw - r): d.line([(xv * zoom, 0), (xv * zoom, rh * zoom)], fill=(255, 40, 40, 255), width=1)
    for yv in (t, rh - b): d.line([(0, yv * zoom), (rw * zoom, yv * zoom)], fill=(255, 40, 40, 255), width=1)
    srcl = Image.alpha_composite(Image.alpha_composite(checker(*src.size), src), lines)
    tiles = [(name, render(sheet, w, h, rect, border, scale)) for name, w, h in sizes]
    W = max(srcl.width, max(t_.width for _, t_ in tiles)) + pad * 2
    H = srcl.height + sum(t_.height + 30 for _, t_ in tiles) + pad * 2 + 30
    board = Image.new("RGBA", (W, H), (28, 28, 34, 255)); dd = ImageDraw.Draw(board); y = pad
    dd.text((pad, y), f"rect {list(rect)}  border {list(border)}  (red = border lines, x{zoom})", fill=(235, 235, 235, 255)); y += 18
    board.paste(srcl, (pad, y)); y += srcl.height + 12
    for name, t_ in tiles:
        dd.text((pad, y), f"{name}  {t_.width}x{t_.height}", fill=(235, 235, 235, 255)); y += 16
        board.alpha_composite(Image.alpha_composite(checker(*t_.size), t_), (pad, y)); y += t_.height + 14
    out.mkdir(parents=True, exist_ok=True)
    stem = f"{Path(sheet_path).stem}_{rx}_{ry}_{rw}x{rh}"
    path = out / f"{stem}-preview.png"; board.convert("RGB").save(path)
    return path


def ints(s, n):
    v = [int(x) for x in s.split(",")]
    if len(v) != n: sys.exit(f"숫자 {n}개를 쉼표로: {s}")
    return v


def main():
    for s in (sys.stdout, sys.stderr): s.reconfigure(encoding="utf-8")  # Windows 콘솔 기본(cp949)에서 한글이 깨지지 않게
    ap =argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["measure", "preview"]); ap.add_argument("sheet")
    ap.add_argument("--at"); ap.add_argument("--rect"); ap.add_argument("--border")
    ap.add_argument("--scale", type=int, default=3); ap.add_argument("--out", default=str(REPO / "output" / "ui-nine-slice"))
    a = ap.parse_args(); out = Path(a.out)
    if a.cmd == "preview":
        if not (a.rect and a.border): sys.exit("preview에는 --rect와 --border가 필요하다")
        print(preview(a.sheet, ints(a.rect, 4), ints(a.border, 4), a.scale, out)); return
    arr = load(a.sheet)
    if a.rect: rect = ints(a.rect, 4)
    elif a.at: rect = blob_rect(arr, *ints(a.at, 2))
    else: sys.exit("measure에는 --at X,Y(테두리 안쪽의 불투명한 점) 또는 --rect가 필요하다")
    rx, ry, rw, rh = rect; crop = arr[ry:ry + rh, rx:rx + rw]
    cols = find_repeat([crop[:, i] for i in range(rw)]); rows = find_repeat([crop[i] for i in range(rh)])
    problems = []
    if not cols: problems.append("가로로 되풀이되는 열 구간이 없다(위·아래 변과 가운데가 함께 반복되는 곳이 없음)")
    if not rows: problems.append("세로로 되풀이되는 행 구간이 없다")
    result = {"sheet": a.sheet, "rect": rect}
    if problems:
        result["problems"] = problems; print(json.dumps(result, ensure_ascii=False, indent=1))
        sys.exit("이 그림은 반복 9조각으로 쓸 수 없다. rect가 맞는지(--at 점, 옆 그림과 붙어 있는지) 보고, 아니면 다른 그림을 고른다")
    l, px, r, runx = cols; t, py, b, runy = rows
    border = [l, t, r, b]
    result.update({"border": border, "period": [px, py], "repeatRun": [runx, runy],
                   "theme": {"rect": rect, "border": border, "fill": "tile"}})
    if px > 1 or py > 1:
        result["warning"] = f"가운데 무늬 주기가 {px}x{py}px다. 패널 가운데 폭·높이를 주기의 배수로 두지 않으면 오른쪽·아래 테두리 앞에서 무늬가 끊긴다"
    result["preview"] = str(preview(a.sheet, rect, border, a.scale, out, (px, py)))
    print(json.dumps(result, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
