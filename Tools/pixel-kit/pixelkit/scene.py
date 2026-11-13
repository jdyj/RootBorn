"""장면(kind: scene): 타일을 깔고, 벽에 걸고, 바닥에 놓아 방·거리 미리 보기를 만든다(견본 PNG만, 게임에 안 들어간다).

명세 값:
  size  [칸 w, 칸 h]
  fill  [{tile, rect: [칸 x, y, w, h], wall?}]   rect를 타일로 채운다. wall이 참이면 그 아랫변(벽과 바닥이 만나는 줄)에
                                                 shadow.wall 2줄.
  place [{id | part, state?, cell: [x, y], dx?, dy?, flat?}]   바닥에 선 것. cell은 그림 캔버스의 왼쪽 아래 칸,
                                                 dx·dy는 정수 px 미세 이동. flat이 참이면 바닥에 깔린 것(깔개):
                                                 그림자가 없고 선 것보다 먼저 그린다.
  hang  [{id | part, state?, at: [px x, y]}]     벽에 걸린 것. at은 그림의 보이는 왼쪽 위 칸(외곽선 포함)이 갈 자리.

그리는 순서: 타일 → 벽 밑 줄 그림자 → 걸린 것(hung_shadow 먼저) → 깔린 것(명세 순서, 그림자 없음) →
선 것을 아랫줄이 작은 것부터(뒤에서 앞으로), 하나마다 under_shadow를 장면에 깐 뒤 붙인다.
"""
from dataclasses import dataclass
from typing import Dict, List

from .canvas import Canvas
from .recipes import Ctx, RecipeError, render
from .shadow import hung_shadow, under_shadow

SCENE_KIND = "scene"


@dataclass
class Item:
    name: str          # 명세 id 또는 점 부품 id
    sprite: Canvas
    x: int             # 그림 캔버스의 왼쪽 위(장면 좌표)
    y: int
    hung: bool
    flat: bool = False


def _sid(scene: dict) -> str:
    return scene.get("id", "?")


def _ints(v, n: int) -> bool:
    return isinstance(v, list) and len(v) == n and all(isinstance(i, int) and not isinstance(i, bool) for i in v)


def _recipe(scene: dict, recipes: Dict[str, dict], rid) -> dict:
    r = recipes.get(rid) if isinstance(rid, str) else None
    if r is None:
        raise RecipeError(f"{_sid(scene)}: 없는 명세 {rid}")
    if r.get("kind") == SCENE_KIND:
        raise RecipeError(f"{_sid(scene)}: 장면 {rid}는 장면 안에 놓을 수 없다")
    return r


def _sprite(scene: dict, entry, recipes: Dict[str, dict], ctx: Ctx, where: str):
    """place·hang 한 줄의 그림과 이름. id(명세)와 part(점 부품) 중 정확히 하나."""
    if not isinstance(entry, dict) or (("id" in entry) == ("part" in entry)):
        raise RecipeError(f"{_sid(scene)}: {where}의 줄마다 id와 part 중 정확히 하나를 적는다: {entry}")
    if "part" in entry:
        try:
            return ctx.part(entry["part"]), entry["part"]
        except RecipeError as e:
            raise RecipeError(f"{_sid(scene)}: {e}") from None
    r = _recipe(scene, recipes, entry["id"])
    try:
        return render(r, ctx, entry.get("state")), entry["id"]
    except RecipeError as e:
        raise RecipeError(f"{_sid(scene)}: {e}") from None


def scene_items(scene: dict, recipes: Dict[str, dict], ctx: Ctx) -> List[Item]:
    """걸린 것(명세 순서), 깔린 것(명세 순서), 선 것(아랫줄이 작은 것부터, 같으면 명세 순서). 그리는 순서 그대로다."""
    cell = ctx.style.cell
    hung: List[Item] = []
    for entry in scene.get("hang", []):
        spr, name = _sprite(scene, entry, recipes, ctx, "hang")
        at = entry.get("at")
        if not _ints(at, 2):
            raise RecipeError(f"{_sid(scene)}: {name}의 at은 정수 [x, y](px)여야 한다")
        box = spr.bbox() or (0, 0, 0, 0)
        hung.append(Item(name, spr, at[0] - box[0], at[1] - box[1], True))
    flat: List[Item] = []
    standing: List[Item] = []
    for entry in scene.get("place", []):
        spr, name = _sprite(scene, entry, recipes, ctx, "place")
        c, dx, dy = entry.get("cell"), entry.get("dx", 0), entry.get("dy", 0)
        if not _ints(c, 2) or not _ints([dx, dy], 2):
            raise RecipeError(f"{_sid(scene)}: {name}의 cell은 정수 [x, y](칸), dx·dy는 정수 px여야 한다")
        is_flat = entry.get("flat", False)
        if not isinstance(is_flat, bool):
            raise RecipeError(f"{_sid(scene)}: {name}의 flat은 true/false여야 한다")
        # cell = 그림 캔버스의 왼쪽 아래 칸
        it = Item(name, spr, c[0] * cell + dx, (c[1] + 1) * cell - spr.h + dy, False, is_flat)
        (flat if is_flat else standing).append(it)
    standing.sort(key=lambda it: it.y + it.sprite.h)
    return hung + flat + standing


def _fill(cv: Canvas, scene: dict, recipes: Dict[str, dict], ctx: Ctx) -> None:
    cell = ctx.style.cell
    for f in scene.get("fill", []):
        if not isinstance(f, dict) or not _ints(f.get("rect"), 4):
            raise RecipeError(f"{_sid(scene)}: fill의 줄마다 {{tile, rect: [칸 x, y, w, h]}}: {f}")
        tid = f.get("tile")
        tile = _recipe(scene, recipes, tid)
        try:
            t = render(tile, ctx)
        except RecipeError as e:
            raise RecipeError(f"{_sid(scene)}: {e}") from None
        if not isinstance(f.get("wall", False), bool):
            raise RecipeError(f"{_sid(scene)}: fill의 wall은 true/false여야 한다: {f}")
        x, y, w, h = (v * cell for v in f["rect"])
        if w <= 0 or h <= 0 or w % t.w or h % t.h:
            raise RecipeError(f"{_sid(scene)}: {tid}({t.w}x{t.h}px)가 rect {f['rect']}에 나누어떨어지지 않는다")
        for ty in range(y, y + h, t.h):
            for tx in range(x, x + w, t.w):
                cv.paste(t, tx, ty)
    for f in scene.get("fill", []):
        if f.get("wall"):
            x, y, w, h = (v * cell for v in f["rect"])
            cv.darken(x, y + h, w, 2, ctx.style.shadow["wall"])        # 벽과 바닥이 만나는 줄


def build_scene(recipe: dict, recipes: Dict[str, dict], ctx: Ctx) -> Canvas:
    size = recipe.get("size")
    if not _ints(size, 2) or min(size) <= 0:
        raise RecipeError(f"{_sid(recipe)}: size는 양의 정수 [칸 w, 칸 h]여야 한다")
    cv = Canvas(size[0] * ctx.style.cell, size[1] * ctx.style.cell)
    _fill(cv, recipe, recipes, ctx)
    for it in scene_items(recipe, recipes, ctx):
        if it.hung:
            hung_shadow(cv, it.sprite, it.x, it.y, ctx.style)
        elif not it.flat:
            under_shadow(cv, it.sprite, it.x, it.y, ctx.style)
        cv.paste(it.sprite, it.x, it.y)
    return cv
