"""바닥에 놓는 작은 물건: 사진 게시판(받침대), 훈련 타이어, 구급 상자 놀이 세트.

면 나누기는 다른 가구와 같다(밝은 윗면, 한 단계 어두운 앞면, 튀어나온 판 밑 짙은 줄). 사진은 색 덩어리일 뿐
글자·얼굴을 그리지 않는다. 좌표는 이 그림(외곽선 전) 기준이고 자리는 spec["_layout"]에 적는다(테스트가 읽는다).
"""
from ..canvas import Canvas
from . import RecipeError, register
from .common import art_cells_in, col, ramp_of, rid


def _ellipse(cx2: int, cy2: int, rx2: int, ry2: int, x: int, y: int) -> bool:
    """점 (x, y)의 가운데가 타원 안인가. 가운데와 반지름은 2배 값(반 px까지 적는다)."""
    dx, dy = 2 * x + 1 - cx2, 2 * y + 1 - cy2
    return dx * dx * ry2 * ry2 + dy * dy * rx2 * rx2 <= rx2 * rx2 * ry2 * ry2


# ---------- 사진 게시판 ----------

BOARD_W, BOARD_H = 28, 24          # 판(틀 포함)
BOARD_LEGS = 20                    # 판 밑 다리 길이
PHOTO_SLOTS = ((3, 3), (14, 2), (4, 12), (15, 13))   # 사진 왼쪽 위(판 안 좌표). 줄을 조금씩 어긋나게 꽂는다
PHOTO_W, PHOTO_H = 10, 8


def _photos(spec: dict, ctx):
    v = spec.get("photos")
    ok = (isinstance(v, list) and 1 <= len(v) <= len(PHOTO_SLOTS)
          and all(isinstance(p, list) and len(p) == 2 and all(isinstance(k, str) for k in p) for p in v))
    if not ok:
        raise RecipeError(f"{rid(spec)}: photos는 [하늘 색, 땅 색](팔레트 키) 목록 1~{len(PHOTO_SLOTS)}개여야 한다")
    return [(col(spec, ctx, a), col(spec, ctx, b)) for a, b in v]


@register("furniture.photo-board")
def photo_board(spec: dict, ctx) -> Canvas:
    """받침대에 세운 코르크 게시판. 사진마다 흰 테두리 안에 하늘·땅 두 색 풍경과 위쪽 압정."""
    art_cells_in(spec, (1, 1), (2, 2))
    photos = _photos(spec, ctx)
    frame = ramp_of(spec, ctx, "frame", "wood.school", 5)
    cork = ramp_of(spec, ctx, "cork", "box.kraft", 4)
    paper = col(spec, ctx, spec.get("paper", "cream.2"))
    pins = [col(spec, ctx, k) for k in ("red.1", "yellow.1")]
    ol = ctx.style.outline
    w, h = BOARD_W, BOARD_H + BOARD_LEGS
    cv = Canvas(w, h)
    for x in (4, w - 6):                                      # 다리: 판 뒤로 올라가 판 밑에서 보인다
        cv.rect(x, BOARD_H, 2, BOARD_LEGS, frame[1])
        cv.vline(x, BOARD_H, BOARD_LEGS, frame[2])
    cv.rect(4, BOARD_H + 11, w - 8, 2, frame[1])              # 가로대
    cv.hline(4, BOARD_H + 11, w - 8, frame[2])
    cv.rect(0, 0, w, BOARD_H, frame[2])                       # 틀: 위·왼쪽 밝게, 아래·오른쪽 어둡게
    cv.hline(0, 0, w, frame[4])
    cv.vline(0, 0, BOARD_H, frame[3])
    cv.hline(0, BOARD_H - 1, w, frame[0])
    cv.vline(w - 1, 0, BOARD_H, frame[1])
    cv.rect(2, 2, w - 4, BOARD_H - 4, cork[2])                # 코르크
    cv.hline(2, 2, w - 4, cork[1])                            # 틀 밑 짙은 줄
    for x, y in ((6, 18), (20, 10), (12, 11), (23, 19)):      # 코르크 결(짧은 줄)
        cv.hline(x, y, 2, cork[3])
    slots = []
    for i, (sky, ground) in enumerate(photos):
        px, py = PHOTO_SLOTS[i]
        px, py = px + 1, py + 1                                # 판 안 좌표 → 그림 좌표(틀 1px 안쪽부터)
        cv.rect(px, py, PHOTO_W, PHOTO_H, paper)               # 흰 테두리
        cv.rect(px + 1, py + 1, PHOTO_W - 2, 3, sky)           # 하늘
        cv.rect(px + 1, py + 4, PHOTO_W - 2, PHOTO_H - 5, ground)   # 땅
        cv.hline(px, py + PHOTO_H, PHOTO_W, cork[0])           # 사진 밑 짧은 그림자
        cv.rect(px + PHOTO_W // 2 - 1, py - 1, 2, 2, pins[i % 2])   # 압정
        slots.append([px, py, PHOTO_W, PHOTO_H])
    spec["_layout"] = {"size": [cv.w, cv.h], "board": [0, 0, w, BOARD_H], "photos": slots}
    return cv


# ---------- 훈련 타이어 ----------

TIRE_W = 28
TIRE_TOP = 14              # 윗면 타원 높이
TIRE_SIDE = 6              # 옆면(트레드) 높이
TIRE_HOLE = (14, 8)        # 구멍 타원 [폭, 높이]


@register("furniture.tire")
def tire(spec: dict, ctx) -> Canvas:
    """바닥에 눕힌 훈련 타이어. 밝은 윗면 고리, 한 단계 어두운 옆면에 트레드 홈, 가운데 구멍 위쪽은 안쪽 벽,
    아래쪽은 뚫려 바닥이 보인다. rope(선택, 팔레트 줄)를 적으면 끌기 밧줄을 옆에 감아 둔다."""
    art_cells_in(spec, (1, 1), (1, 1))
    r = ramp_of(spec, ctx, "rubber", "metal.dark", 3)        # 0 홈·안쪽 벽, 1 옆면, 2 윗면
    w, h = TIRE_W, TIRE_TOP + TIRE_SIDE
    cv = Canvas(w, h)
    cx2, rx2 = w, w                                            # 가운데 x = w/2, 반지름 w/2 (2배 값)
    hw, hh = TIRE_HOLE
    for x in range(w):
        for y in range(h):
            top = _ellipse(cx2, TIRE_TOP, rx2, TIRE_TOP, x, y)
            side = any(_ellipse(cx2, TIRE_TOP, rx2, TIRE_TOP, x, y - d) for d in range(TIRE_SIDE + 1))
            if top:
                cv.px(x, y, r[2])
            elif side:
                cv.px(x, y, r[1] if (x + 1) % 4 else r[0])     # 트레드 홈
    for x in range(w):                                         # 윗면 바깥 테두리 밑: 옆면이 시작하는 짙은 줄
        ys = [y for y in range(h) if _ellipse(cx2, TIRE_TOP, rx2, TIRE_TOP, x, y)]
        if ys and ys[-1] + 1 < h:
            cv.px(x, ys[-1] + 1, r[0])
    hole = []
    for x in range(w):
        for y in range(TIRE_TOP):
            if _ellipse(cx2, TIRE_TOP, hw, hh, x, y):
                hole.append((x, y))
    ymid = TIRE_TOP // 2
    for x, y in hole:                                          # 구멍: 위쪽 절반은 안쪽 벽, 아래쪽은 뚫림
        cv.px(x, y, r[0] if y < ymid else (0, 0, 0, 0))
    low = sorted({y for _, y in hole if y >= ymid})
    spec["_layout"] = {"size": [cv.w, cv.h], "hole": [w // 2, low[len(low) // 2]]}
    rope = spec.get("rope")
    if rope is not None:
        rp = ramp_of(spec, ctx, "rope", "box.kraft", 3)
        for i, x in enumerate(range(3, w - 3)):                # 윗면을 가로지르는 밧줄(꼰 무늬)
            y = TIRE_TOP // 2 + 4 + (1 if 8 < x < w - 8 else 0)
            cv.px(x, y, rp[2] if i % 3 else rp[1])
    return cv


# ---------- 구급 상자 놀이 세트 ----------

KIT_W, KIT_LID, KIT_FRONT = 22, 6, 12


@register("furniture.first-aid-kit")
def first_aid_kit(spec: dict, ctx) -> Canvas:
    """구급 상자 놀이 세트: 위 손잡이, 밝은 뚜껑 윗면, 앞면에 십자, 뚜껑에 걸쳐 옆으로 늘어진 장난감 청진기."""
    art_cells_in(spec, (1, 1), (1, 1))
    case = ramp_of(spec, ctx, "case", "ceramic", 3)
    cross = ramp_of(spec, ctx, "cross", "red", 2)
    handle = ramp_of(spec, ctx, "handle", "metal.dark", 3)
    tube = ramp_of(spec, ctx, "tube", "cushion.navy", 3)
    disc = ramp_of(spec, ctx, "disc", "metal", 3)
    ol = ctx.style.outline
    w, h = 30, 5 + KIT_LID + KIT_FRONT + 2
    cv = Canvas(w, h)
    kx, ky = 1, 5                                              # 상자 왼쪽 위
    cv.rect(kx + 6, 0, 10, 2, handle[1])                       # 손잡이: 윗대 + 두 다리, 사이는 뚫림
    cv.hline(kx + 6, 0, 10, handle[2])
    cv.rect(kx + 6, 2, 2, 3, handle[1])
    cv.rect(kx + 14, 2, 2, 3, handle[0])
    cv.rect(kx, ky, KIT_W, KIT_LID, case[2])                   # 뚜껑 윗면(가장 밝다)
    cv.hline(kx, ky + KIT_LID - 1, KIT_W, case[1])             # 뚜껑 앞 모서리
    cv.hline(kx, ky + KIT_LID, KIT_W, ol)
    fy = ky + KIT_LID + 1
    cv.rect(kx, fy, KIT_W, KIT_FRONT, case[1])                 # 앞면
    cv.hline(kx, fy, KIT_W, case[0])                           # 뚜껑 밑 짙은 줄
    cv.rect(kx, fy + KIT_FRONT, KIT_W, 1, case[0])             # 밑
    cx, cy = kx + KIT_W // 2 - 1, fy + 2                       # 십자(3px 굵기, 9px)
    cv.rect(cx - 1, cy, 3, 9, cross[1])
    cv.rect(cx - 4, cy + 3, 9, 3, cross[1])
    cv.hline(cx - 4, cy + 5, 9, cross[0])
    cv.vline(cx + 1, cy + 6, 3, cross[0])
    cv.rect(kx + 2, fy + 4, 2, 3, handle[1])                   # 걸쇠
    cv.rect(kx + KIT_W - 4, fy + 4, 2, 3, handle[1])
    # 청진기: 귀꽂이 둘이 뚜껑 위, 관이 뚜껑 오른쪽 끝을 넘어 옆으로 늘어지고 끝에 둥근 판
    cv.rect(kx + 15, ky + 1, 2, 2, disc[1])                    # 귀꽂이
    cv.rect(kx + 15, ky + 3, 7, 2, tube[1])                    # 뚜껑 위 관
    cv.hline(kx + 15, ky + 3, 7, tube[2])
    cv.rect(kx + KIT_W, ky + 3, 2, 12, tube[1])                # 옆으로 늘어진 관
    cv.vline(kx + KIT_W, ky + 3, 12, tube[2])
    cv.vline(kx + KIT_W + 1, ky + 5, 10, tube[0])
    dx, dy = kx + KIT_W, h - 6                                 # 둥근 판(5x5, 모서리 깎음)
    cv.rect(dx + 1, dy, 4, 5, disc[1])
    cv.rect(dx, dy + 1, 6, 3, disc[1])
    cv.rect(dx + 1, dy + 1, 2, 2, disc[2])
    cv.hline(dx + 1, dy + 4, 4, disc[0])
    spec["_layout"] = {"size": [cv.w, cv.h], "case": [kx, ky, KIT_W, KIT_LID + KIT_FRONT + 1]}
    return cv
