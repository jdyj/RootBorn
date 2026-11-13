"""타일: 실내 바닥, 실내 벽, 보도. 외곽선 없이 칸 배수 크기로 그리고, 무늬 주기는 32의 약수만 쓴다."""
from ..canvas import Canvas
from ..rand import rnd
from . import RecipeError, register


def _hash(x: int, y: int, m: int) -> int:
    """난수 대신 칸 좌표의 정수 해시. 같은 입력이면 늘 같다."""
    return ((x * 73856093) ^ (y * 19349663) ^ 0x5BD1E995) % m


def _ramp(spec: dict, ctx, prefix: str, steps):
    try:
        return [ctx.style.color(f"{prefix}.{i}") for i in steps]
    except KeyError as e:
        raise RecipeError(f"{spec.get('id', '?')}: {e.args[0]}") from None


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


@register("tile.floor-checker")
def floor_checker(spec: dict, ctx) -> Canvas:
    """16px 체크. colors는 [밝은 칸 줄, 어두운 칸 줄], 줄마다 단계 0 줄눈 / 1 잔 점 / 2 바탕 / 3 빛 모서리.
    두 칸 모두 위·왼쪽은 빛 모서리, 아래·오른쪽은 줄눈. 잔 점은 칸마다 다른 자리(타일 주기 32)."""
    cv = Canvas(32, 32)
    ramps = [_ramp(spec, ctx, p, range(4)) for p in _prefixes(spec, 2)]
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
                cv.px(ox + 2 + rnd(n, 1, 12), oy + 2 + rnd(n, 2, 12), speck)
    return cv


@register("tile.wall-interior")
def wall_interior(spec: dict, ctx) -> Canvas:
    """32x64: 윗면 6px + 외곽선 한 줄 + 천장 그늘 + 세로 줄 벽지 + 허리 몰딩 + 징두리 판 + 걸레받이."""
    cv = Canvas(32, 64)
    paper = _ramp(spec, ctx, _key(spec, "paper"), (0, 1, 2))
    cap = _ramp(spec, ctx, _key(spec, "cap"), (0, 1))
    w = _ramp(spec, ctx, _key(spec, "wood"), range(5))
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
    return cv


GRASS_TUFTS = 14     # 32px 안의 풀포기(V자) 수
GRASS_BLADES = 18    # 32px 안의 낱 풀잎 수
GRASS_PATCHES = 5    # 32px 안의 짙은 얼룩 수


@register("tile.grass")
def grass(spec: dict, ctx) -> Canvas:
    """풀밭. 줄 단계 0 짙은 밑동 / 1 얼룩 / 2 바탕 / 3 밝은 잎끝. 짙은 얼룩, V자 풀포기, 낱 풀잎을
    32px 전체에 흩는다(16px로 되풀이하면 칸 격자가 보인다). 가장자리를 넘는 것은 맞은편으로 감아 이음매가 없다."""
    cv = Canvas(32, 32)
    (prefix,) = _prefixes(spec, 1)
    dark, patch, base, light = _ramp(spec, ctx, prefix, range(4))
    cv.rect(0, 0, 32, 32, base)

    def put(x: int, y: int, c) -> None:
        cv.px(x % 32, y % 32, c)

    for k in range(GRASS_PATCHES):                        # 짙은 얼룩: 가로로 긴 낮은 덩어리
        x, y, n = rnd(k, 11, 32), rnd(k, 12, 32), 3 + rnd(k, 13, 3)
        for i in range(n):
            put(x + i, y, patch)
        for i in range(1, n - 1):
            put(x + i, y + 1, patch)
    for k in range(GRASS_TUFTS):                          # 풀포기: 밑동 하나 + 양옆 위로 잎끝
        x, y = rnd(k, 21, 32), rnd(k, 22, 32)
        put(x, y + 1, dark)
        put(x - 1, y, light)
        put(x + 1, y, light)
    for k in range(GRASS_BLADES):                         # 낱 풀잎: 잎끝 + 밑동
        x, y = rnd(k, 31, 32), rnd(k, 32, 32)
        put(x, y, light)
        put(x, y + 1, dark)
    return cv


@register("tile.paver")
def paver(spec: dict, ctx) -> Canvas:
    """16px 판석 넷. 빛 모서리·줄눈·잔 점. 주기 16."""
    cv = Canvas(32, 32)
    (prefix,) = _prefixes(spec, 1)
    dark, base, light = _ramp(spec, ctx, prefix, (0, 1, 2))
    for ty in range(2):
        for tx in range(2):
            ox, oy = tx * 16, ty * 16
            cv.rect(ox, oy, 16, 16, base)
            cv.hline(ox, oy, 15, light)
            cv.vline(ox, oy, 15, light)
            cv.hline(ox, oy + 15, 16, dark)
            cv.vline(ox + 15, oy, 16, dark)
            cv.px(ox + 4 + _hash(0, 5, 8), oy + 4 + _hash(2, 0, 8), dark)
    return cv
