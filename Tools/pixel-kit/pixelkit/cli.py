"""명령 본문. kit.py는 인자 읽기만 하고 여기 COMMANDS 표에서 이름으로 골라 부른다.

끝 코드: 0 깨끗, 1 검사 문제, 2 사용·데이터 오류(없는 id, 못 읽는 키트, 저장 거절).
"""
import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, List, Optional, Tuple

from . import boards
from .canvas import Canvas
from .lint import Problem, lint_sprite, lint_tile
from .parts import PartError, load_part_allowances, load_parts
from .recipes import Ctx, RecipeError, ghost_ctx, load_recipes, render_with_spec
from .scene import SCENE_KIND, build_scene
from .style import Style

DEFAULT_SCALE = {"render": 6, "sheet": 4, "scene": 4, "parts": 8}


class UsageError(Exception):
    """사용법이나 데이터가 잘못됐다. 끝 코드 2."""


@dataclass
class Kit:
    style: Style
    parts: Dict[str, Canvas]
    allowances: dict
    recipes: Dict[str, dict]

    def ctx(self) -> Ctx:
        # 그림마다 새 문제 목록: 문제가 다른 그림으로 새지 않는다.
        return Ctx(self.style, self.parts, [])


def load_kit(directory: str) -> Kit:
    base = Path(directory)
    try:
        style = Style.load(str(base / "style.json"))
        parts = load_parts(str(base / "parts"), style)
        allowances = load_part_allowances(str(base / "parts"))
        recipes = load_recipes(str(base / "recipes"))
    except (OSError, json.JSONDecodeError, KeyError, PartError, RecipeError) as e:
        raise UsageError(f"키트를 못 읽었다({directory}): {e}") from None
    return Kit(style, parts, allowances, recipes)


class Saver:
    """저장을 거절당해도(OSError) 죽지 않고 알린 뒤 나머지를 계속 저장한다."""

    def __init__(self):
        self.failed = 0

    def save(self, img, path: Path) -> None:
        try:
            path.parent.mkdir(parents=True, exist_ok=True)
            if isinstance(img, Canvas):
                img = img.to_image()
            img.save(str(path), format="PNG", optimize=False)
        except OSError:
            self.failed += 1
            print(f"못 썼다: {path} (다른 프로그램에서 열려 있는지 확인)", file=sys.stderr)
            return
        print(f"저장: {path}")


def _unknown(kind: str, name: str, known: List[str]) -> UsageError:
    return UsageError(f"없는 {kind}: {name}\n아는 {kind}: {', '.join(sorted(known))}")


def _split(arg: str) -> Tuple[str, Optional[str]]:
    rid, _, state = arg.partition("@")
    return rid, (state or None)


def _recipe(kit: Kit, rid: str) -> dict:
    if rid not in kit.recipes:
        raise _unknown("id", rid, list(kit.recipes) + list(kit.parts))
    return kit.recipes[rid]


def _pictures(recipe: dict, state: Optional[str], explicit: bool):
    """(라벨 상태 이름, 그릴 상태) 목록. 상태를 안 적으면 기본 그림과 모든 상태."""
    if explicit:
        return [state]
    return [None] + sorted(recipe.get("states") or {})


def _label(rid: str, state: Optional[str]) -> str:
    return rid if state is None else f"{rid}@{state}"


def draw_and_check(kit: Kit, recipe: dict, state: Optional[str]) -> Tuple[Canvas, List[Problem]]:
    ctx = kit.ctx()
    cv, spec = render_with_spec(recipe, ctx, state)
    if spec.get("kind") == "tile":
        found = lint_tile(cv, spec, ctx)
    else:
        body, _ = render_with_spec(recipe, ghost_ctx(ctx), state)   # 부품 없이 그린 몸통(색 수 검사용)
        found = lint_sprite(cv, spec, ctx, layout=spec.get("_layout"), body=body)
    return cv, list(ctx.problems) + found


def _print_problems(label: str, problems: List[Problem]) -> None:
    for p in problems:
        where = f" ({p.x}, {p.y})" if p.x is not None and p.y is not None else ""
        print(f"{label}: {p.code}{where} {p.message}")


def _out(args, *parts: str) -> Path:
    return Path(args.out).joinpath(*parts)


def _scale(args, name: str) -> int:
    n = args.scale if args.scale is not None else DEFAULT_SCALE[name]
    if n < 1:
        raise UsageError("--scale은 1 이상의 정수여야 한다")
    return n


def _finish(saver: Saver, code: int) -> int:
    return 2 if saver.failed else code


def _reject_scene(rid: str, recipe: dict, command: str) -> None:
    if recipe.get("kind") == SCENE_KIND:
        raise UsageError(f"{rid}: 장면은 {command}로 못 그린다. `scene {rid}`를 쓴다")


def cmd_render(args, kit: Kit) -> int:
    if not args.ids:
        raise UsageError("render에는 id가 하나 이상 필요하다")
    scale = _scale(args, "render")
    saver, code = Saver(), 0
    for arg in args.ids:
        rid, state = _split(arg)
        recipe = _recipe(kit, rid)
        _reject_scene(rid, recipe, "render")
        for st in _pictures(recipe, state, state is not None):
            label = _label(rid, st)
            try:
                cv, problems = draw_and_check(kit, recipe, st)
            except RecipeError as e:
                raise UsageError(str(e)) from None
            saver.save(cv, _out(args, "render", f"{label}.png"))
            saver.save(boards.enlarge(cv.to_image(), scale), _out(args, "render", f"{label}_x{scale}.png"))
            _print_problems(label, problems)
            if problems:
                code = 1
    return _finish(saver, code)


def _lint_targets(args, kit: Kit):
    """검사할 (라벨, 그리는 법) 목록. id가 없으면 장면 아닌 명세 전부와 점 부품 전부."""
    if not args.ids:
        for rid, recipe in kit.recipes.items():
            if recipe.get("kind") != SCENE_KIND:
                for st in _pictures(recipe, None, False):
                    yield _label(rid, st), recipe, st
        for pid in kit.parts:
            yield pid, None, None
        return
    for arg in args.ids:
        rid, state = _split(arg)
        if rid in kit.parts and rid not in kit.recipes:
            if state is not None:
                print(f"{arg}: 건너뜀 — 점 부품에는 상태가 없다", file=sys.stderr)
                continue
            yield rid, None, None
            continue
        recipe = _recipe(kit, rid)
        if recipe.get("kind") == SCENE_KIND:
            print(f"{arg}: 건너뜀 — 장면은 검사 대상이 아니다 — scene 명령으로 본다", file=sys.stderr)
            continue
        for st in _pictures(recipe, state, state is not None):
            yield _label(rid, st), recipe, st


def cmd_lint(args, kit: Kit) -> int:
    problems_found, errors = False, False
    for label, recipe, st in _lint_targets(args, kit):
        if recipe is None:
            problems = lint_sprite(kit.parts[label], {"id": label, "kind": "part"}, kit.ctx(),
                                   kit.allowances.get(label, frozenset()))
        else:
            try:
                _, problems = draw_and_check(kit, recipe, st)
            except RecipeError as e:
                print(f"{label}: 오류 {e}", file=sys.stderr)
                errors = True
                continue
        _print_problems(label, problems)
        problems_found = problems_found or bool(problems)
    return 2 if errors else (1 if problems_found else 0)


def cmd_sheet(args, kit: Kit) -> int:
    if len(args.ids) != 1:
        raise UsageError("sheet에는 세트 이름 하나가 필요하다(명세 id의 첫 마디, 예: bakery)")
    name = args.ids[0]
    members = [(rid, r) for rid, r in kit.recipes.items()
               if rid.split(".")[0] == name and r.get("kind") != SCENE_KIND]
    if not members:
        raise _unknown("세트", name, sorted({rid.split(".")[0] for rid in kit.recipes}))
    entries = []
    for n, (rid, recipe) in enumerate(members, 1):
        try:
            cv, problems = draw_and_check(kit, recipe, None)
        except RecipeError as e:
            raise UsageError(str(e)) from None
        entries.append(boards.Entry(f"{n} {rid}", cv, bool(problems)))
    saver = Saver()
    saver.save(boards.board_image(entries, _scale(args, "sheet")), _out(args, "sheet", f"{name}.png"))
    return _finish(saver, 0)


def cmd_scene(args, kit: Kit) -> int:
    if not args.ids:
        raise UsageError("scene에는 장면 id가 하나 이상 필요하다")
    scale = _scale(args, "scene")
    saver = Saver()
    for rid in args.ids:
        recipe = _recipe(kit, rid)
        if recipe.get("kind") != SCENE_KIND:
            raise UsageError(f"{rid}: 장면이 아니다. `render {rid}`를 쓴다")
        ctx = kit.ctx()
        try:
            cv = build_scene(recipe, kit.recipes, ctx)
        except RecipeError as e:
            raise UsageError(str(e)) from None
        saver.save(cv, _out(args, "scene", f"{rid}.png"))
        saver.save(boards.enlarge(cv.to_image(), scale), _out(args, "scene", f"{rid}_x{scale}.png"))
        _print_problems(rid, ctx.problems)
    return _finish(saver, 0)


def cmd_parts(args, kit: Kit) -> int:
    if args.ids:
        raise UsageError("parts에는 인자가 없다")
    if not kit.parts:
        raise UsageError("점 부품이 없다")
    entries = [boards.Entry(pid, cv) for pid, cv in kit.parts.items()]
    saver = Saver()
    saver.save(boards.board_image(entries, _scale(args, "parts")), _out(args, "parts.png"))
    return _finish(saver, 0)


COMMANDS: Dict[str, Callable] = {
    "render": cmd_render,
    "lint": cmd_lint,
    "sheet": cmd_sheet,
    "scene": cmd_scene,
    "parts": cmd_parts,
}


def run(args) -> int:
    try:
        kit = load_kit(args.kit)
        return COMMANDS[args.command](args, kit)
    except UsageError as e:
        print(str(e), file=sys.stderr)
        return 2
