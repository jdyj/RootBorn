"""조립 명세 읽기와 그리기. 명세의 part가 만드는 부품을 부르고, 외곽선을 입히고,
칸 배수 캔버스의 아래 가운데에 붙인다."""
import copy
import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Optional, Tuple

from .canvas import Canvas
from .makers import RecipeError, get_maker, register  # noqa: F401  (다른 모듈이 여기서 가져다 쓴다)
from .style import Style


@dataclass
class Ctx:
    style: Style
    parts: Dict[str, Canvas]
    problems: list = field(default_factory=list)

    def part(self, id: str) -> Canvas:
        try:
            return self.parts[id]
        except KeyError:
            raise RecipeError(f"{id}: 점 부품이 없다") from None


def load_recipes(directory: str) -> Dict[str, dict]:
    """폴더(하위 폴더 포함)의 *.json 명세 배열을 id → 명세로. 경로순(결정적)."""
    out: Dict[str, dict] = {}
    origin: Dict[str, str] = {}
    base = Path(directory)
    for path in sorted(base.rglob("*.json")):
        name = path.relative_to(base).as_posix()
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            raise RecipeError(f"{name}: JSON 읽기 실패: {e}") from None
        if not isinstance(data, list):
            raise RecipeError(f"{name}: 파일은 명세 배열이어야 한다")
        for r in data:
            rid = r.get("id") if isinstance(r, dict) else None
            if not isinstance(rid, str) or not rid:
                raise RecipeError(f"{name}: id가 없는 명세")
            if rid in out:
                raise RecipeError(f"{rid}: id가 겹친다({origin[rid]}와 {name})")
            out[rid] = r
            origin[rid] = name
    return out


def _is_int_pair(v, minimum: int = 1) -> bool:
    return (isinstance(v, list) and len(v) == 2
            and all(isinstance(n, int) and not isinstance(n, bool) and n >= minimum for n in v))


def _target_size(spec: dict, cell: int) -> Tuple[int, int]:
    rid = spec.get("id", "?")
    has_cells = "art_cells" in spec
    has_px = "pixels" in spec
    if has_cells == has_px:
        raise RecipeError(f"{rid}: art_cells와 pixels 중 정확히 하나를 적어야 한다")
    if has_cells:
        if not _is_int_pair(spec["art_cells"]):
            raise RecipeError(f"{rid}: art_cells는 양의 정수 [w, h]여야 한다")
        w, h = spec["art_cells"]
        return w * cell, h * cell
    if not _is_int_pair(spec["pixels"]):
        raise RecipeError(f"{rid}: pixels는 양의 정수 [w, h]여야 한다")
    w, h = spec["pixels"]
    if w % cell or h % cell:
        raise RecipeError(f"{rid}: pixels {w}x{h}는 {cell}의 배수여야 한다")
    return w, h


def render_with_spec(recipe: dict, ctx: Ctx, state: Optional[str] = None) -> Tuple[Canvas, dict]:
    """그린 캔버스와, 만드는 부품이 받아 쓴 명세 사본(상태 적용됨)을 돌려준다."""
    rid = recipe.get("id", "?")
    spec = copy.deepcopy(recipe)
    if state is not None:
        states = spec.get("states") or {}
        if state not in states:
            raise RecipeError(f"{rid}: 없는 상태 {state}. 있는 것: {', '.join(sorted(states)) or '(없음)'}")
        spec.update(copy.deepcopy(states[state]))
    cell = ctx.style.cell
    tw, th = _target_size(spec, cell)
    part = spec.get("part")
    try:
        maker = get_maker(part)
    except RecipeError as e:
        raise RecipeError(f"{rid}: {e}") from None
    drawn = maker(spec, ctx)
    if spec.get("kind") != "tile":
        # 가장자리까지 찬 그림도 바깥 1px 외곽선이 들어가도록 사방 1px 여유를 두고 입힌다.
        padded = Canvas(drawn.w + 2, drawn.h + 2)
        padded.paste(drawn, 1, 1)
        drawn = padded.outline(ctx.style.outline)
    out = Canvas(tw, th)
    # 아래 가운데. 더 크면 그대로 잘리게 둔다(넘침은 검사 단계가 알린다).
    out.paste(drawn, (tw - drawn.w) // 2, th - drawn.h)
    return out, spec


def render(recipe: dict, ctx: Ctx, state: Optional[str] = None) -> Canvas:
    return render_with_spec(recipe, ctx, state)[0]
