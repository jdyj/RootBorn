"""색 수는 몸통만 센다: 붙인 점 부품의 색은 뺀다."""
import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.lint import lint_sprite
from pixelkit.makers import register
from pixelkit.parts import load_parts
from pixelkit.cli import Kit, draw_and_check
from pixelkit.recipes import Ctx, ghost_ctx, load_recipes
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
KIT = ROOT / "art" / "kit"
STYLE = Style.load(str(KIT / "style.json"))
PARTS = load_parts(str(KIT / "parts"), STYLE)
REC = {"id": "t", "kind": "furniture"}


def palette_colors(n, skip=0):
    """팔레트의 외곽선 아닌 색 n개(결정적 순서)."""
    cs = sorted(c for c in STYLE.palette if c != STYLE.outline)
    return cs[skip:skip + n]


def dot_part(colors):
    cv = Canvas(4, len(colors))
    for i, c in enumerate(colors):
        cv.rect(0, i, 4, 1, c)
    return cv


# 몸통은 palette_colors(3), 부품 5개는 각자 4색(모두 달라 합치면 23색, 몸통과도 겹치지 않는다)
BODY = palette_colors(3)
FAKE = {f"p{i}": dot_part(palette_colors(4, skip=3 + 4 * i)) for i in range(5)}


@register("test.bodycolors")
def _maker(spec, ctx):
    cv = Canvas(30, 30)
    for i, c in enumerate(spec.get("body", BODY)):
        cv.rect(0, i, 30, 1, c)
    x = 0
    for pid in spec.get("use", []):
        cv.paste(ctx.part(pid), x, 24)
        x += 5
    return cv


def recipe(**kw):
    r = {"id": "t.body", "kind": "furniture", "part": "test.bodycolors", "pixels": [32, 32]}
    r.update(kw)
    return r


def body_problems(r, parts=None):
    """cli와 같은 길(그리고 몸통을 유령 그림으로 세어) 검사한 too-many-colors 문제들."""
    kit = Kit(STYLE, FAKE if parts is None else parts, {}, {})
    cv, problems = draw_and_check(kit, r, None)
    return cv, [p for p in problems if p.code == "too-many-colors"]


class BodyColorTests(unittest.TestCase):
    def test_pasted_parts_do_not_count(self):
        cv, ps = body_problems(recipe(use=sorted(FAKE)))
        self.assertGreater(len({c for c in cv.colors() if c != STYLE.outline}), 16)
        self.assertEqual(ps, [])

    def test_body_over_limit_reports_count(self):
        _, ps = body_problems(recipe(body=palette_colors(17, skip=30)))
        self.assertEqual(len(ps), 1)
        self.assertIn("17", ps[0].message)

    def test_body_colors_also_in_a_part_still_count(self):
        """몸통의 17색이 모두 붙인 부품에도 있어도 몸통은 17색이다(부품 색을 빼는 규칙이 틀렸던 경우)."""
        cs = palette_colors(17, skip=30)
        parts = dict(FAKE, all17=dot_part(cs))
        _, ps = body_problems(recipe(body=cs, use=["all17"]), parts)
        self.assertEqual(len(ps), 1)
        self.assertIn("17", ps[0].message)

    def test_part_size_logic_survives_ghost(self):
        """부품을 투명하게 바꿔도 크기는 같다."""
        g = ghost_ctx(Ctx(STYLE, FAKE, []))
        for k, v in FAKE.items():
            self.assertEqual((g.parts[k].w, g.parts[k].h), (v.w, v.h))
            self.assertEqual(g.parts[k].colors(), set())

    def test_same_picture_twice_gives_same_result(self):
        kit = Kit(STYLE, FAKE, {}, {})
        r = recipe(use=["p0", "p1"])
        cv1, p1 = draw_and_check(kit, r, None)
        cv2, p2 = draw_and_check(kit, r, None)
        self.assertEqual(cv1.to_image().tobytes(), cv2.to_image().tobytes())
        self.assertEqual([(p.code, p.message) for p in p1], [(p.code, p.message) for p in p2])

    def test_ghost_problems_are_discarded(self):
        ctx = Ctx(STYLE, FAKE, [])
        ghost_ctx(ctx).problems.append("x")
        self.assertEqual(ctx.problems, [])

    def test_lint_without_body_counts_the_picture_itself(self):
        cv = Canvas(32, 32)
        for i, c in enumerate(palette_colors(17, skip=30)):
            cv.rect(2, i, 28, 1, c)
        ps = [p for p in lint_sprite(cv, {"id": "t", "kind": "furniture"}, Ctx(STYLE, {}, []))
              if p.code == "too-many-colors"]
        self.assertEqual(len(ps), 1)

    def test_limits_are_original_values(self):
        self.assertEqual(STYLE.max_colors["furniture"], 16)
        self.assertEqual(STYLE.max_colors["building"], 32)


class RealPicturesTests(unittest.TestCase):
    def test_every_real_picture_within_body_limit(self):
        kit = Kit(STYLE, PARTS, {}, load_recipes(str(KIT / "recipes")))
        for rid, r in sorted(kit.recipes.items()):
            if r.get("kind") in ("scene", "tile"):
                continue
            for st in [None] + sorted(r.get("states") or {}):
                with self.subTest(rid=rid, state=st):
                    _, problems = draw_and_check(kit, r, st)
                    ps = [p for p in problems if p.code == "too-many-colors"]
                    self.assertEqual(ps, [], [p.message for p in ps])


if __name__ == "__main__":
    unittest.main()
