"""색 수는 몸통만 센다: 붙인 점 부품의 색은 뺀다."""
import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.lint import lint_sprite
from pixelkit.makers import register
from pixelkit.makers import common
from pixelkit.parts import load_parts
from pixelkit.recipes import Ctx, load_recipes, render_with_spec
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


@register("test.bodycolors-local")
def _maker_local(spec, ctx):
    cv = Canvas(30, 30)
    cv.rect(0, 0, 30, 10, BODY[0])
    inner = common.local(ctx)
    cv.paste(common.part(spec, inner, "p0"), 0, 24)
    common.merge(spec, ctx, inner)
    return cv


def recipe(**kw):
    r = {"id": "t.body", "kind": "furniture", "part": "test.bodycolors", "pixels": [32, 32]}
    r.update(kw)
    return r


def draw(r, parts=None):
    ctx = Ctx(STYLE, FAKE if parts is None else parts, [])
    cv, spec = render_with_spec(r, ctx)
    return ctx, cv, spec


class BodyColorTests(unittest.TestCase):
    def test_pasted_parts_do_not_count(self):
        ctx, cv, spec = draw(recipe(use=sorted(FAKE)))
        self.assertGreater(len({c for c in cv.colors() if c != STYLE.outline}), 8)
        ps = [p for p in lint_sprite(cv, spec, ctx, allow=frozenset({"orphan-pixel", "outline-open"}))
              if p.code == "too-many-colors"]
        self.assertEqual(ps, [])

    def test_used_parts_recorded(self):
        ctx, _, _ = draw(recipe(use=["p0", "p2"]))
        self.assertEqual(ctx.used_parts, {"p0", "p2"})

    def test_used_parts_survive_local_ctx(self):
        ctx, _, _ = draw(recipe(part="test.bodycolors-local"))
        self.assertEqual(ctx.used_parts, {"p0"})

    def test_two_pictures_do_not_leak(self):
        a, _, _ = draw(recipe(use=["p0"]))
        b, _, _ = draw(recipe())
        self.assertEqual(a.used_parts, {"p0"})
        self.assertEqual(b.used_parts, set())

    def test_body_over_limit_reports_body_count(self):
        limit = STYLE.max_colors["furniture"]
        body = palette_colors(limit + 1, skip=30)
        ctx, cv, spec = draw(recipe(body=body, use=["p0"]))
        ps = [p for p in lint_sprite(cv, spec, ctx, allow=frozenset({"orphan-pixel", "outline-open"}))
              if p.code == "too-many-colors"]
        self.assertEqual(len(ps), 1)
        self.assertIn(f"{limit + 1}개", ps[0].message)
        self.assertIn("부품", ps[0].message)

    def test_no_parts_counts_like_before(self):
        limit = STYLE.max_colors["furniture"]
        body = palette_colors(limit + 1, skip=30)
        ctx, cv, spec = draw(recipe(body=body))
        ps = [p for p in lint_sprite(cv, spec, ctx, allow=frozenset({"orphan-pixel", "outline-open"}))
              if p.code == "too-many-colors"]
        self.assertEqual(len(ps), 1)
        self.assertIn(f"{limit + 1}개", ps[0].message)

    def test_limits_are_original_values(self):
        self.assertEqual(STYLE.max_colors["furniture"], 16)
        self.assertEqual(STYLE.max_colors["building"], 32)


class RealPicturesTests(unittest.TestCase):
    def test_every_real_picture_within_body_limit(self):
        recipes = load_recipes(str(KIT / "recipes"))
        for rid, r in sorted(recipes.items()):
            if r.get("kind") in ("scene", "tile"):
                continue
            for st in [None] + sorted(r.get("states") or {}):
                with self.subTest(rid=rid, state=st):
                    ctx = Ctx(STYLE, PARTS, [])
                    cv, spec = render_with_spec(r, ctx, st)
                    ps = [p for p in lint_sprite(cv, spec, ctx, layout=spec.get("_layout"))
                          if p.code == "too-many-colors"]
                    self.assertEqual(ps, [], [p.message for p in ps])


if __name__ == "__main__":
    unittest.main()
