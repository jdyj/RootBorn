"""명령 본문. kit.py는 인자 읽기만 하고 여기 COMMANDS 표에서 이름으로 골라 부른다.

끝 코드: 0 깨끗, 1 검사 문제, 2 사용·데이터 오류(없는 id, 못 읽는 키트, 저장 거절).
"""
import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Dict, List, Optional, Tuple

from PIL import Image

from . import boards
from .canvas import Canvas
from .lint import Problem, lint_sprite, lint_tile
from .parts import PartError, load_part_allowances, load_parts
from .recipes import Ctx, RecipeError, ghost_ctx, load_recipes, render_with_spec
from .scene import SCENE_KIND, build_scene
from .style import Style

DEFAULT_SCALE = {"render": 6, "sheet": 4, "scene": 4, "parts": 8, "styles": 2}


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


def cmd_export(args, kit: Kit) -> int:
    from . import export   # export가 cli를 부르므로 늦게 가져온다
    if not args.ids:
        raise UsageError("export에는 id가 하나 이상 필요하다")
    root = Path(args.repo)
    plans = []
    groups: Dict[str, List[dict]] = {}     # 시트 PNG 경로 → 명세들(인자 순서). 건물과 소품은 같은 시트다
    for rid in args.ids:
        recipe = _recipe(kit, rid)
        kind = recipe.get("kind")
        if kind == "furniture":
            try:
                plans.append(export.export_furniture(recipe, kit, root))
            except RecipeError as e:
                raise UsageError(str(e)) from None
        elif kind in export.SHEET_KINDS:
            groups.setdefault(export.sheet_path(kind, rid.split(".")[0]), []).append(recipe)
        else:
            raise UsageError(f"{rid}: {kind} 내보내기는 아직 없다(가구, 타일, 건물, 소품만)")
    for recipes in groups.values():
        try:
            plans.append(export.export_sheet(recipes, recipes[0]["kind"], recipes[0]["id"].split(".")[0], kit, root))
        except RecipeError as e:
            raise UsageError(str(e)) from None
    plan = export.merge_plans(plans)
    if plan.problems:
        for p in plan.problems:
            where = f" ({p.x}, {p.y})" if p.x is not None and p.y is not None else ""
            print(f"{p.code}{where} {p.message}")
        return 1
    pending = export.pending_writes(plan, root)
    if export.FURNITURE_DEFS in pending:
        for rid, sprite, name in plan.replace:
            print(f"가구 정의 {rid}: sprite → {sprite}, name → {name}")
    if args.dry_run:
        for rel in plan.files:
            print(f"쓸 파일: {rel}" + ("" if rel in pending else " (같은 내용)"))
        if not pending:
            print("바꿀 것 없음")
        print("--dry-run: 아무것도 쓰지 않았다")
        return 0
    if not pending:
        print("바꿀 것 없음")
        return 0
    try:
        for rel in export.apply(plan, root):
            print(f"저장: {rel}")
    except OSError as e:
        print(f"못 썼다: {e}", file=sys.stderr)
        return 2
    return 0


def cmd_test_level(args, kit: Kit) -> int:
    """지금 시트로 시험용 맵(pixel-kit-test.ldtk + maps.json)을 쓴다. --out의 기본은 저장소의 art/world/test."""
    from . import ldtk_test_level
    if args.ids:
        raise UsageError("test-level에는 인자가 없다")
    try:
        written = ldtk_test_level.write_test_level(args.repo, args.out)
    except ldtk_test_level.TestLevelError as e:
        raise UsageError(str(e)) from None
    except OSError as e:
        print(f"못 썼다: {e}", file=sys.stderr)
        return 2
    for rel in written:
        print(f"저장: {rel}")
    if not written:
        print("바꿀 것 없음")
    return 0


def _room_problems(problems) -> int:
    """문제를 찍고 끝 코드를 돌려준다(알림만 있으면 0)."""
    from .room_plan import is_warning
    code = 0
    for p in problems:
        where = f" ({p.x}, {p.y})" if p.x is not None and p.y is not None else ""
        warn = is_warning(p)
        print(f"{'알림 ' if warn else ''}{p.code}{where} {p.message}")
        code = code or (0 if warn else 1)
    return code


FORCE_NOTE = ("알림 --force: 평면에서 통째로 다시 만든다 — LDtk에서 손으로 고친 것(다시 칠한 칸, 놓은 가구)은 지워진다. "
              "손으로 고친 뒤에는 room repaint <ldtk 경로>로 그림만 맞춘다")


def _room_ldtk_action(action: str, target: Path, args, kit: Kit) -> int:
    """room check <ldtk 경로> | room repaint <ldtk 경로> [--dry-run]."""
    from . import room_ldtk
    from .room_plan import is_warning
    if action == "check":
        problems = room_ldtk.check_room(target)
        code = _room_problems(problems)
        if not problems:
            print(f"{target.name}: 문제 없음")
        return code
    written = room_ldtk.repaint_room(target, dry_run=args.dry_run, kit=kit)
    _room_problems(p for p in room_ldtk.check_room(target) if is_warning(p))      # 얇은 벽, 표에 없는 문
    for name in written:
        print(("쓸 파일: " if args.dry_run else "저장: ") + (target.parent / name).as_posix())
    if args.dry_run:
        print("--dry-run: 아무것도 쓰지 않았다")
    elif not written:
        print("바꿀 것 없음")
    return 0


# room styles 견본판의 조각. 벽: 윗면 띠(정면 위) + 정면 2칸 + 바닥 한 줄, 문간(6열), 방 안에서 끝나는 세로 벽(3열 —
# 좁은 정면, 정면을 지나는 늘인 정면). 바닥: 4x3칸.
STYLE_WALL_PIECE = ["######.##", "######.##", "######.##", "...#.....", "...#.....", "........."]
STYLE_TOP_PIECE = ["#######", "#######", "#######", "#..#..#", "#..#..#", "#.....#", "#######"]
STYLE_FLOOR_CELLS = (4, 3)
STYLE_WALL_FLOOR = "floor.checker-soft"


def _room_piece(kit: Kit, plan: List[str], wall: str, floor: dict, top: Optional[str] = None) -> Canvas:
    """평면 조각을 Ground 위에 Wall을 얹어 한 장으로(밖은 투명)."""
    from .room_plan import cells
    from .room_tiles import CELL, draw_tile, plan_tiles
    tiles = plan_tiles(cells(plan))
    cache: Dict[str, Canvas] = {}
    h, w = len(plan), len(plan[0])
    img = Image.new("RGBA", (w * CELL, h * CELL), (0, 0, 0, 0))
    for layer in (tiles.ground, tiles.wall):
        for y, row in enumerate(layer):
            for x, key in enumerate(row):
                if key is None:
                    continue
                if key not in cache:
                    cache[key] = draw_tile(kit.style, key, wall, floor, top)
                img.alpha_composite(cache[key].to_image().convert("RGBA"), (x * CELL, y * CELL))
    return Canvas.from_image(img)


def _room_styles(args, kit: Kit) -> int:
    """room styles [--list] [--out 폴더] [--scale N]: 등록된 벽 모양·띠 색·바닥을 견본판 두 장으로(또는 글로만)."""
    from .room_ldtk import floor_ids
    from .room_tiles import wall_about, wall_styles, wall_top, wall_tops
    floors = floor_ids(kit)
    if args.list:
        print(f"벽 모양 {len(wall_styles())}개 (평면의 wall):")
        for name in wall_styles():
            print(f"  {name:<12} 기본 띠 {wall_top(name):<16} {wall_about(name)}")
        print(f"띠 색 {len(wall_tops())}개 (평면의 top, 적지 않으면 벽 모양의 기본):")
        for name in wall_tops():
            print(f"  {name}")
        print(f"바닥 {len(floors)}개 (평면의 floor, 키트의 한 칸 타일 명세):")
        for rid in floors:
            print(f"  {rid:<22} {kit.recipes[rid].get('part', '')}")
        return 0
    scale = _scale(args, "styles")
    out = Path(args.out) if args.out is not None else Path(args.repo) / "output" / "pixel-kit" / "room"
    base_floor = kit.recipes[STYLE_WALL_FLOOR if STYLE_WALL_FLOOR in floors else floors[0]]       # 벽 조각 밑 바닥(조용한 것)
    try:
        walls = [boards.Entry(f"{name} (top {wall_top(name)})", _room_piece(kit, STYLE_WALL_PIECE, name, base_floor))
                 for name in wall_styles()]
        walls += [boards.Entry(f"paper + top {top}", _room_piece(kit, STYLE_TOP_PIECE, "paper", base_floor, top))
                  for top in wall_tops()]
        fw, fh = STYLE_FLOOR_CELLS
        flist = [boards.Entry(rid, _room_piece(kit, ["." * fw] * fh, "paper", kit.recipes[rid])) for rid in floors]
    except RecipeError as e:
        raise UsageError(str(e)) from None
    saver = Saver()
    for name, entries in (("styles-walls", walls), ("styles-floors", flist)):
        saver.save(boards.board_image(entries, 1), out / f"{name}.png")
        saver.save(boards.board_image(entries, scale), out / f"{name}_x{scale}.png")
    return _finish(saver, 0)


def cmd_room(args, kit: Kit) -> int:
    """room build <id> [--force] [--dry-run] [--out 폴더]: 평면에서 LDtk 방을 쓴다. room check <id>: 평면만 검사한다.
    room check <ldtk 경로>: LDtk 방을 검사한다. room repaint <ldtk 경로> [--dry-run]: 칠한 Collision에 벽 그림을 맞춘다.
    room styles [--list] [--out 폴더]: 벽 모양·띠 색·바닥 견본판."""
    from . import room_ldtk, room_plan
    from .room_tiles import check_top, wall_styles
    usage = ("사용법: room build <방 id> [--force] [--dry-run] [--out 폴더] | room check <방 id 또는 .ldtk 경로> | "
             "room repaint <.ldtk 경로> [--dry-run] | room styles [--list] [--out 폴더] [--scale N]")
    if args.ids == ["styles"]:
        return _room_styles(args, kit)
    if len(args.ids) != 2 or args.ids[0] not in ("build", "check", "repaint"):
        raise UsageError(usage)
    action, rid = args.ids
    rooms_dir = Path(args.kit) / "rooms"
    try:
        if action == "repaint" or (action == "check" and rid.endswith(".ldtk")):
            if not rid.endswith(".ldtk"):
                raise UsageError("room repaint에는 .ldtk 파일 경로를 준다\n" + usage)
            return _room_ldtk_action(action, Path(rid), args, kit)
        if action == "check":
            room = room_plan.load_room(rooms_dir / f"{rid}.json")
            if room.wall not in wall_styles():
                raise UsageError(f"{rid}: 등록되지 않은 벽 모양 {room.wall}. 있는 것: {', '.join(wall_styles())}")
            room_ldtk.floor_spec(kit, room.floor)                              # 없으면 RoomError(받는 바닥 목록)
            check_top(room.top)                                                # 없으면 RecipeError(고를 수 있는 띠 색 목록)
            problems = room_plan.check_grid(room.grid, room.spawn, room.doors, room.face_height)
            code = _room_problems(problems)
            if not problems:
                print(f"{rid}: 문제 없음")
            return code
        room = room_plan.load_room(rooms_dir / f"{rid}.json")
        _room_problems(p for p in room_plan.check_grid(room.grid, room.spawn, room.doors, room.face_height)
                       if room_plan.is_warning(p))
        _room_problems(room_ldtk.door_problems(room.doors, {}))          # build가 쓰는 maps.json의 doors는 비어 있다
        overwrote = args.force and not args.dry_run and room_ldtk.existing_room_files(args.repo, rid, args.out)
        written = room_ldtk.build_room(args.repo, rid, out_dir=args.out, force=args.force, dry_run=args.dry_run,
                                       kit=kit, rooms_dir=rooms_dir)
        if overwrote:
            print(FORCE_NOTE)                          # 빌드가 거절되면 찍지 않는다(덮어쓴 것이 없다)
    except room_ldtk.RoomBuildError as e:
        _room_problems(e.problems)
        return 1
    except room_plan.RoomError as e:
        raise UsageError(str(e)) from None
    except RecipeError as e:
        raise UsageError(str(e)) from None
    except OSError as e:
        print(f"못 썼다: {e}", file=sys.stderr)
        return 2
    for rel in written:
        print(("쓸 파일: " if args.dry_run else "저장: ") + rel)
    if args.dry_run:
        print("--dry-run: 아무것도 쓰지 않았다")
    elif not written:
        print("바꿀 것 없음")
    return 0


COMMANDS: Dict[str, Callable] = {
    "room": cmd_room,
    "test-level": cmd_test_level,
    "render": cmd_render,
    "lint": cmd_lint,
    "sheet": cmd_sheet,
    "scene": cmd_scene,
    "parts": cmd_parts,
    "export": cmd_export,
}


def run(args) -> int:
    try:
        kit = load_kit(args.kit)
        return COMMANDS[args.command](args, kit)
    except UsageError as e:
        print(str(e), file=sys.stderr)
        return 2
