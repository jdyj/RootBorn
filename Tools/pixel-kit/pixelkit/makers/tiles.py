"""타일: 실내 바닥, 실내 벽, 보도. 외곽선 없이 칸 배수 크기로 그리고, 무늬 주기는 32의 약수만 쓴다."""
from ..canvas import Canvas
from ..rand import rnd
from . import RecipeError, register
from .common import ramp_steps, seed, target_size


def _prefixes(spec: dict, n: int):
    cs = spec.get("colors")
    if not isinstance(cs, list) or len(cs) != n or not all(isinstance(c, str) for c in cs):
        raise RecipeError(f"{spec.get('id', '?')}: colors는 팔레트 키 접두사 {n}개여야 한다")
    return cs


def _key(spec: dict, name: str) -> str:
    v = spec.get(name)
    if not isinstance(v, str):
        raise RecipeError(f"{spec.get('id', '?')}: {name}(팔레트 키 접두사)가 없다")
    return v


def _fill(cv: Canvas, spec: dict, ctx) -> Canvas:
    """명세의 art_cells(또는 pixels)가 무늬 한 장보다 크면 무늬를 되풀이해 빈틈없이 채운다. 무늬 주기가 한 장의 크기라 이음매가 없다.
    같거나 작으면 그대로 돌려준다(작은 쪽은 그림 맞춤이 잘라낸다)."""
    tw, th = target_size(spec, ctx.style.cell)
    w, h = max(tw, cv.w), max(th, cv.h)
    if (w, h) == (cv.w, cv.h):
        return cv
    out = Canvas(w, h)
    for y in range(0, h, cv.h):
        for x in range(0, w, cv.w):
            out.paste(cv, x, y)
    return out


@register("tile.floor-checker")
def floor_checker(spec: dict, ctx) -> Canvas:
    """16px 체크. colors는 [밝은 칸 줄, 어두운 칸 줄], 줄마다 단계 0 줄눈 / 1 잔 점 / 2 바탕 / 3 빛 모서리.
    두 칸 모두 위·왼쪽은 빛 모서리, 아래·오른쪽은 줄눈. 잔 점은 칸마다 다른 자리(타일 주기 32)."""
    sd = seed(spec)
    cv = Canvas(32, 32)
    ramps = [ramp_steps(spec, ctx, p, range(4)) for p in _prefixes(spec, 2)]
    for ty in range(2):
        for tx in range(2):
            joint, speck, base, edge = ramps[(tx + ty) % 2]
            ox, oy = tx * 16, ty * 16
            cv.rect(ox, oy, 16, 16, base)
            cv.hline(ox, oy, 15, edge)
            cv.vline(ox, oy, 15, edge)
            cv.hline(ox, oy + 15, 16, joint)
            cv.vline(ox + 15, oy, 16, joint)
            for k in range(2):                           # 잔 점 둘: 칸마다 다른 자리
                n = (ty * 2 + tx) * 2 + k
                cv.px(ox + 2 + rnd(n, 1, 12, sd), oy + 2 + rnd(n, 2, 12, sd), speck)
    return _fill(cv, spec, ctx)


@register("tile.wall-interior")
def wall_interior(spec: dict, ctx) -> Canvas:
    """32x64: 윗면 6px + 외곽선 한 줄 + 천장 그늘 + 세로 줄 벽지 + 허리 몰딩 + 징두리 판 + 걸레받이."""
    cv = Canvas(32, 64)
    paper = ramp_steps(spec, ctx, _key(spec, "paper"), (0, 1, 2))
    cap = ramp_steps(spec, ctx, _key(spec, "cap"), (0, 1))
    w = ramp_steps(spec, ctx, _key(spec, "wood"), range(5))
    ol = ctx.style.outline
    cv.rect(0, 0, 32, 64, paper[2])
    for x in range(0, 32, 8):
        cv.rect(x, 7, 3, 33, paper[1])                  # 세로 줄 벽지(주기 8)
    cv.rect(0, 0, 32, 6, cap[0])                         # 벽 윗면
    cv.hline(0, 0, 32, cap[1])                           # 윗면 빛 받는 줄
    cv.hline(0, 6, 32, ol)                               # 윗면 아래 외곽선 한 줄
    cv.rect(0, 7, 32, 2, paper[0])                       # 천장 그늘
    cv.rect(0, 40, 32, 2, w[3])                          # 허리 몰딩
    cv.hline(0, 42, 32, w[1])
    cv.rect(0, 43, 32, 17, w[2])                         # 징두리 판
    for x in (0, 16):
        cv.rect(x + 2, 46, 12, 11, w[3])
        cv.hline(x + 2, 46, 12, w[4])
        cv.vline(x + 13, 46, 11, w[1])
        cv.hline(x + 2, 56, 12, w[1])
    cv.rect(0, 60, 32, 4, w[0])                          # 걸레받이
    cv.hline(0, 60, 32, w[1])
    return _fill(cv, spec, ctx)


GRASS_TUFTS = 14     # 32px 안의 풀포기(V자) 수
GRASS_BLADES = 18    # 32px 안의 낱 풀잎 수
GRASS_PATCHES = 5    # 32px 안의 짙은 얼룩 수


@register("tile.grass")
def grass(spec: dict, ctx) -> Canvas:
    """풀밭. 줄 단계 0 짙은 밑동 / 1 얼룩 / 2 바탕 / 3 밝은 잎끝. 짙은 얼룩, V자 풀포기, 낱 풀잎을
    32px 전체에 흩는다(16px로 되풀이하면 칸 격자가 보인다). 가장자리를 넘는 것은 맞은편으로 감아 이음매가 없다."""
    sd = seed(spec)
    cv = Canvas(32, 32)
    (prefix,) = _prefixes(spec, 1)
    dark, patch, base, light = ramp_steps(spec, ctx, prefix, range(4))
    cv.rect(0, 0, 32, 32, base)

    def put(x: int, y: int, c) -> None:
        cv.px(x % 32, y % 32, c)

    for k in range(GRASS_PATCHES):                        # 짙은 얼룩: 가로로 긴 낮은 덩어리
        x, y, n = rnd(k, 11, 32, sd), rnd(k, 12, 32, sd), 3 + rnd(k, 13, 3, sd)
        for i in range(n):
            put(x + i, y, patch)
        for i in range(1, n - 1):
            put(x + i, y + 1, patch)
    for k in range(GRASS_TUFTS):                          # 풀포기: 밑동 하나 + 양옆 위로 잎끝
        x, y = rnd(k, 21, 32, sd), rnd(k, 22, 32, sd)
        put(x, y + 1, dark)
        put(x - 1, y, light)
        put(x + 1, y, light)
    for k in range(GRASS_BLADES):                         # 낱 풀잎: 잎끝 + 밑동
        x, y = rnd(k, 31, 32, sd), rnd(k, 32, 32, sd)
        put(x, y, light)
        put(x, y + 1, dark)
    return _fill(cv, spec, ctx)


# 판석의 잔 점 자리(x, y 소금). 씨앗 0에서 칸 안 (+6, +7)이 나오는 값이라 1단계 그림과 같다. 한 판석 넷이 같은 자리를 쓴다(주기 16).
PAVER_DOT_SALT = (4, 7)


@register("tile.paver")
def paver(spec: dict, ctx) -> Canvas:
    """16px 판석 넷. 빛 모서리·줄눈·잔 점. 주기 16."""
    sd = seed(spec)
    cv = Canvas(32, 32)
    (prefix,) = _prefixes(spec, 1)
    dark, base, light = ramp_steps(spec, ctx, prefix, (0, 1, 2))
    for ty in range(2):
        for tx in range(2):
            ox, oy = tx * 16, ty * 16
            cv.rect(ox, oy, 16, 16, base)
            cv.hline(ox, oy, 15, light)
            cv.vline(ox, oy, 15, light)
            cv.hline(ox, oy + 15, 16, dark)
            cv.vline(ox + 15, oy, 16, dark)
            cv.px(ox + 4 + rnd(0, PAVER_DOT_SALT[0], 8, sd), oy + 4 + rnd(0, PAVER_DOT_SALT[1], 8, sd), dark)
    return _fill(cv, spec, ctx)


# ---------------------------------------------------------------- 방 바닥(art/kit/recipes/floor/tiles.json)
# 캐릭터·가구가 위에서 잘 보이게 조용히: 넓은 바탕 한 단계 + 이음·결 한 단계 + 빛 모서리. 줄의 단계 0은 대개 벽 밑 그림자
# (방 바닥 열쇠 floor:shade가 색마다 그 줄의 가장 어두운 단계로 옮긴다)나 이음에 쓴다. 무늬 주기는 32의 약수.

def _wrap(cv: Canvas):
    def put(x: int, y: int, c) -> None:
        cv.px(x % 32, y % 32, c)
    return put


def _choice(spec: dict, name: str, allowed: tuple, default):
    v = spec.get(name, default)
    if type(v) is not type(default) or v not in allowed:
        raise RecipeError(f"{spec.get('id', '?')}: {name}는 {', '.join(map(repr, allowed))} 중 하나여야 한다(지금 {v!r})")
    return v


@register("tile.floor-plank")
def floor_plank(spec: dict, ctx) -> Canvas:
    """나무 판자 바닥. 판 폭 board(8 기본 · 16), vertical(false 기본 · true: 가로 판을 돌린 세로 판).
    colors: 줄 1개, 단계 0 이음·끝선 / 1 결 / 2 바탕 / 3 빛 모서리. 끝 이음은 8px 판 둘에 하나만(이음이 많으면 벽돌처럼 읽힌다).
    16px 넓은 판은 끝 이음이 없다(타일 32px마다 이음이 오면 돌 판처럼 읽힌다 — 판이 길게 이어진다)."""
    sd = seed(spec)
    board = _choice(spec, "board", (8, 16), 8)
    vertical = _choice(spec, "vertical", (False, True), False)
    (prefix,) = _prefixes(spec, 1)
    joint, grain, base, light = ramp_steps(spec, ctx, prefix, range(4))
    cv = Canvas(32, 32)
    put = _wrap(cv)
    for r in range(32 // board):
        y0 = r * board
        cv.rect(0, y0, 32, board, base)
        cv.hline(0, y0, 32, light)
        cv.hline(0, y0 + board - 1, 32, joint)
        for i in range(6):                                # 결: 판마다 짧은 선 둘(자리는 판 번호로)
            put(r * 9 + 4 + i, y0 + board * 3 // 8, grain)
            put(r * 9 + 20 + i, y0 + board * 5 // 8, grain)
        if r % 2 and board == 8:
            jx = ((21 if r % 4 == 1 else 6) + sd) % 32
            cv.vline(jx, y0, board - 1, joint)
            cv.vline((jx + 1) % 32, y0 + 1, board - 2, light)
    if vertical:
        turned = Canvas(32, 32)
        for y in range(32):
            for x in range(32):
                turned.px(y, x, cv.get(x, y))
        cv = turned
    return _fill(cv, spec, ctx)


@register("tile.floor-herringbone")
def floor_herringbone(spec: dict, ctx) -> Canvas:
    """헤링본(판 16x8). colors: 줄 1개, 단계 0 이음(판의 아래·오른쪽 변) / 2 세로 판 / 3 가로 판(1은 쓰지 않는다 — 결까지 넣으면
    잔무늬가 시끄럽다). 빛 줄은 두지 않는다."""
    (prefix,) = _prefixes(spec, 1)
    joint, vface, hface = ramp_steps(spec, ctx, prefix, (0, 2, 3))
    w = 8
    cv = Canvas(32, 32)
    put = _wrap(cv)

    def plank(x0, y0, pw, ph, face):
        for yy in range(ph):
            for xx in range(pw):
                put(x0 + xx, y0 + yy, joint if (yy == ph - 1 or xx == pw - 1) else face)

    for s in range(-4, 5):                                # 띠 이동 (2w, -2w)
        for t in range(-10, 10):                          # 띠 안 걸음 (w, w)
            ox, oy = t * w + s * 2 * w, t * w - s * 2 * w
            plank(ox, oy, 2 * w, w, hface)
            plank(ox, oy + w, w, 2 * w, vface)
    return _fill(cv, spec, ctx)


@register("tile.floor-basket")
def floor_basket(spec: dict, ctx) -> Canvas:
    """바구니 짜임 쪽매(16px 칸마다 판 둘, 칸마다 가로·세로가 엇갈림). colors: 줄 1개, 단계 0 이음(판의 아래·오른쪽 변) /
    2 세로 판 / 3 가로 판(1은 쓰지 않는다 — 결까지 넣으면 짜임이 시끄럽다)."""
    (prefix,) = _prefixes(spec, 1)
    joint, vface, hface = ramp_steps(spec, ctx, prefix, (0, 2, 3))
    cv = Canvas(32, 32)
    for by in range(2):
        for bx in range(2):
            ox, oy = bx * 16, by * 16
            across = (bx + by) % 2 == 0
            for i in range(2):
                x0, y0, pw, ph = (ox, oy + i * 8, 16, 8) if across else (ox + i * 8, oy, 8, 16)
                cv.rect(x0, y0, pw, ph, hface if across else vface)
                cv.hline(x0, y0 + ph - 1, pw, joint)
                cv.vline(x0 + pw - 1, y0, ph, joint)
    return _fill(cv, spec, ctx)


@register("tile.floor-slab")
def floor_slab(spec: dict, ctx) -> Canvas:
    """큰 석재 판 32x32 하나: 위·왼쪽 빛, 아래·오른쪽 줄눈, 옅은 결 두 줄. colors: 줄 1개, 단계 0 줄눈 / 1 결 / 2 바탕 / 3 빛."""
    (prefix,) = _prefixes(spec, 1)
    joint, vein, base, light = ramp_steps(spec, ctx, prefix, range(4))
    cv = Canvas(32, 32)
    cv.rect(0, 0, 32, 32, base)
    cv.hline(0, 0, 31, light)
    cv.vline(0, 0, 31, light)
    cv.hline(0, 31, 32, joint)
    cv.vline(31, 0, 32, joint)
    for x, y in ((6, 9), (7, 10), (8, 10), (9, 11), (10, 11), (11, 12), (19, 22), (20, 22), (21, 23), (22, 24), (23, 24)):
        cv.px(x, y, vein)
    return _fill(cv, spec, ctx)


@register("tile.floor-mosaic")
def floor_mosaic(spec: dict, ctx) -> Canvas:
    """작은 모자이크(알 7px + 줄눈 1px). 밝은 알 바탕에 한 단계 짙은 알이 대각선으로 고르게(넓게 깔아도 얼룩지지 않게).
    colors: 줄 1개, 단계 1 줄눈 / 2 짙은 알 / 3 밝은 알(0은 벽 밑 그림자용)."""
    (prefix,) = _prefixes(spec, 1)
    joint, accent, face = ramp_steps(spec, ctx, prefix, (1, 2, 3))
    cv = Canvas(32, 32)
    cv.rect(0, 0, 32, 32, joint)
    for ty in range(4):
        for tx in range(4):
            cv.rect(tx * 8, ty * 8, 7, 7, accent if (tx + 2 * ty) % 4 == 0 else face)
    return _fill(cv, spec, ctx)


@register("tile.floor-carpet")
def floor_carpet(spec: dict, ctx) -> Canvas:
    """단색 카펫: 바탕 + 아주 옅은 점(4px 엇갈림). colors: 줄 1개, 단계 1 점 / 2 바탕(0은 벽 밑 그림자용)."""
    (prefix,) = _prefixes(spec, 1)
    dot, base = ramp_steps(spec, ctx, prefix, (1, 2))
    cv = Canvas(32, 32)
    cv.rect(0, 0, 32, 32, base)
    for y in range(0, 32, 4):
        for x in range(0 if (y // 4) % 2 else 2, 32, 4):
            cv.px(x, y, dot)
    return _fill(cv, spec, ctx)


@register("tile.floor-carpet-diamond")
def floor_carpet_diamond(spec: dict, ctx) -> Canvas:
    """무늬 카펫(테두리 없음): 바탕에 16px 마름모 격자 선과 마름모 가운데 점. colors: 줄 1개, 단계 1 선·점 / 2 바탕
    (0은 벽 밑 그림자용)."""
    (prefix,) = _prefixes(spec, 1)
    line, base = ramp_steps(spec, ctx, prefix, (1, 2))
    cv = Canvas(32, 32)
    for y in range(32):
        for x in range(32):
            dx, dy = abs(x % 16 - 8), abs(y % 16 - 8)
            cv.px(x, y, line if dx + dy == 8 or (dx == 0 and dy == 0) else base)
    return _fill(cv, spec, ctx)


@register("tile.floor-tatami")
def floor_tatami(spec: dict, ctx) -> Canvas:
    """정사각 돗자리 16x16 넷, 결 방향이 엇갈린다. colors: [몸 줄(단계 2 바탕 / 3 결), 테 줄(단계 1이 아래·오른쪽 테)].
    결은 바탕보다 한 단계 밝게, 테는 한 줄만 — 격자가 도드라지지 않게."""
    body, edge = _prefixes(spec, 2)
    base, light = ramp_steps(spec, ctx, body, (2, 3))
    (rim,) = ramp_steps(spec, ctx, edge, (1,))
    cv = Canvas(32, 32)
    for ty in range(2):
        for tx in range(2):
            ox, oy = tx * 16, ty * 16
            cv.rect(ox, oy, 16, 16, base)
            for i in range(3, 15, 4):
                if (tx + ty) % 2 == 0:
                    cv.hline(ox, oy + i, 15, light)
                else:
                    cv.vline(ox + i, oy, 15, light)
            cv.hline(ox, oy + 15, 16, rim)
            cv.vline(ox + 15, oy, 16, rim)
    return _fill(cv, spec, ctx)


@register("tile.floor-concrete")
def floor_concrete(spec: dict, ctx) -> Canvas:
    """콘크리트 판 32x32: 옅은 얼룩 두 덩이 + 잔 점 + 아래·오른쪽 줄눈. colors: 줄 1개, 단계 0 줄눈 / 1 잔 점 / 2 바탕 / 3 얼룩."""
    (prefix,) = _prefixes(spec, 1)
    joint, speck, base, patch = ramp_steps(spec, ctx, prefix, range(4))
    cv = Canvas(32, 32)
    cv.rect(0, 0, 32, 32, base)
    for x0, y0, rows in ((4, 6, ((1, 4), (0, 7), (2, 5))), (16, 18, ((2, 5), (0, 9), (1, 8), (3, 4)))):
        for dy, (dx, w) in enumerate(rows):                 # 둥근 얼룩: 줄마다 시작·길이
            cv.hline(x0 + dx, y0 + dy, w, patch)
    for x, y in ((9, 14), (24, 7), (13, 26), (27, 27), (3, 22)):
        cv.px(x, y, speck)
    cv.hline(0, 31, 32, joint)
    cv.vline(31, 0, 32, joint)
    return _fill(cv, spec, ctx)


LINO_SPECKS = 16     # 32px 안의 잔 점 수


@register("tile.floor-linoleum")
def floor_linoleum(spec: dict, ctx) -> Canvas:
    """리놀륨: 단색 바탕 + 잔 점(좌표 해시로 흩음, 이음매 없음). colors: 줄 1개, 단계 1 잔 점 / 2 바탕(0은 벽 밑 그림자용)."""
    sd = seed(spec)
    (prefix,) = _prefixes(spec, 1)
    speck, base = ramp_steps(spec, ctx, prefix, (1, 2))
    cv = Canvas(32, 32)
    cv.rect(0, 0, 32, 32, base)
    for k in range(LINO_SPECKS):
        cv.px(rnd(k, 41, 32, sd), rnd(k, 42, 32, sd), speck)
    return _fill(cv, spec, ctx)


@register("tile.floor-brick")
def floor_brick(spec: dict, ctx) -> Canvas:
    """벽돌 바닥(16x8 엇갈림 쌓기, 가게 뒤·창고). colors: 줄 1개, 단계 1 줄눈 / 2 벽돌 / 3 밝은 벽돌(몇 장만 — 좌표 해시)
    (0은 벽 밑 그림자용). 줄눈은 한 단계만 어둡게 해 조용하다."""
    sd = seed(spec)
    (prefix,) = _prefixes(spec, 1)
    mortar, face, light = ramp_steps(spec, ctx, prefix, (1, 2, 3))
    cv = Canvas(32, 32)
    for r in range(4):
        off = 8 if r % 2 else 0
        for b in range(2):
            c = light if rnd(r * 2 + b, 51, 5, sd) == 0 else face
            for xx in range(16):
                for yy in range(8):
                    cv.px((b * 16 - off + xx) % 32, r * 8 + yy, mortar if (yy == 7 or xx == 15) else c)
    return _fill(cv, spec, ctx)
