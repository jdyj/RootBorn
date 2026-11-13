import unittest
from pathlib import Path

from pixelkit.canvas import Canvas
from pixelkit.shadow import hung_shadow, under_shadow
from pixelkit.style import Style

ROOT = Path(__file__).resolve().parents[3]
style = Style.load(str(ROOT / "art" / "kit" / "style.json"))
FLOOR = (200, 180, 160, 255)
BODY = (100, 60, 40, 255)


def flat(w, h, rgba):
    cv = Canvas(w, h)
    cv.rect(0, 0, w, h, rgba)
    return cv


def legs_sprite(body_bottom, leg_bottom, legs_at, width, leg_w=2):
    """폭 width의 상자(0..body_bottom 줄)가 다리 둘(legs_at 열부터 leg_w px)로 leg_bottom 줄까지 떠 있다."""
    cv = Canvas(width, leg_bottom + 1)
    cv.rect(0, 0, width, body_bottom + 1, BODY)
    for lx in legs_at:
        cv.rect(lx, body_bottom + 1, leg_w, leg_bottom - body_bottom, BODY)
    return cv


def darker(a, b):
    return sum(a[:3]) < sum(b[:3])


class UnderShadowTests(unittest.TestCase):
    def test_floating_body_gets_shadow_under_the_body_between_the_legs(self):
        spr = legs_sprite(body_bottom=20, leg_bottom=25, legs_at=(2, 17), width=20)   # 다리 둘로 5px 뜬 상자
        scene = flat(40, 40, FLOOR)
        under_shadow(scene, spr, 10, 5, style)
        self.assertTrue(all(scene.get(10 + i, 5 + 22) != FLOOR for i in range(4, 16)))   # 몸통 밑이 어둡다
        self.assertTrue(scene.get(9, 5 + 26) == FLOOR and scene.get(31, 5 + 26) == FLOOR)  # 물체 폭 밖은 그대로

    def test_whole_gap_under_the_body_is_dark_down_to_the_bottom_row(self):
        spr = legs_sprite(20, 25, (2, 17), 20)
        scene = flat(40, 40, FLOOR)
        under_shadow(scene, spr, 10, 5, style)
        for y in range(21, 26):
            self.assertNotEqual(scene.get(10 + 10, 5 + y), FLOOR, y)

    def test_shadow_under_body_is_darker_than_the_front_strip(self):
        spr = legs_sprite(20, 25, (2, 17), 20)
        scene = flat(40, 40, FLOOR)
        under_shadow(scene, spr, 10, 5, style)
        under, front = scene.get(20, 5 + 22), scene.get(20, 5 + 26)
        self.assertNotEqual(front, FLOOR)
        self.assertTrue(darker(under, front))

    def test_front_strip_is_rows_minus_one_and_ends_are_one_row_shorter_and_lighter(self):
        spr = legs_sprite(20, 25, (2, 17), 20)
        scene = flat(40, 40, FLOOR)
        under_shadow(scene, spr, 10, 5, style, rows=3)
        mid = 10 + 10
        self.assertNotEqual(scene.get(mid, 5 + 27), FLOOR)        # 앞 띠 2줄
        self.assertEqual(scene.get(mid, 5 + 28), FLOOR)
        left_end = 10 + 2                                          # 아래 4줄에 칸이 있는 첫 열
        self.assertNotEqual(scene.get(left_end, 5 + 26), FLOOR)
        self.assertEqual(scene.get(left_end, 5 + 27), FLOOR)       # 양 끝은 한 줄 짧다
        self.assertTrue(darker(scene.get(mid, 5 + 26), scene.get(left_end, 5 + 26)))

    def test_shadow_never_leaves_the_feet_column_range(self):
        spr = legs_sprite(20, 25, (2, 17), 20)
        scene = flat(40, 40, FLOOR)
        under_shadow(scene, spr, 10, 5, style)
        for y in range(40):
            for x in list(range(0, 12)) + list(range(29, 40)):     # 발(2..18열) 밖
                self.assertEqual(scene.get(x, y), FLOOR, (x, y))

    def test_sprite_with_nothing_in_its_bottom_rows_casts_no_shadow_and_does_not_raise(self):
        spr = Canvas(20, 30)
        spr.rect(0, 0, 20, 10, BODY)                               # 아래 4줄이 비었다
        scene = flat(40, 40, FLOOR)
        under_shadow(scene, spr, 5, 5, style)
        under_shadow(scene, Canvas(8, 8), 5, 5, style)             # 완전히 빈 그림
        self.assertEqual(scene.colors(), {FLOOR})

    def test_shadow_only_darkens_what_is_already_on_the_scene(self):
        spr = legs_sprite(20, 25, (2, 17), 20)
        scene = Canvas(40, 40)                                     # 투명한 장면
        under_shadow(scene, spr, 10, 5, style)
        self.assertIsNone(scene.bbox())


class HungShadowTests(unittest.TestCase):
    def test_two_rows_right_under_the_hung_thing_within_its_columns(self):
        spr = Canvas(10, 6)
        spr.rect(0, 0, 10, 6, BODY)
        scene = flat(30, 30, FLOOR)
        hung_shadow(scene, spr, 5, 5, style)
        for x in range(5, 15):
            self.assertNotEqual(scene.get(x, 11), FLOOR)
            self.assertNotEqual(scene.get(x, 12), FLOOR)
            self.assertEqual(scene.get(x, 13), FLOOR)
        self.assertEqual(scene.get(4, 11), FLOOR)
        self.assertEqual(scene.get(15, 11), FLOOR)

    def test_follows_the_lowest_pixel_of_each_column(self):
        spr = Canvas(10, 8)
        spr.rect(0, 0, 10, 4, BODY)
        spr.rect(3, 4, 4, 4, BODY)                                 # 가운데만 아래로 길다
        scene = flat(30, 30, FLOOR)
        hung_shadow(scene, spr, 0, 0, style)
        self.assertNotEqual(scene.get(0, 4), FLOOR)                # 짧은 열은 바로 밑부터
        self.assertNotEqual(scene.get(4, 8), FLOOR)
        self.assertEqual(scene.get(4, 10), FLOOR)

    def test_empty_sprite_does_not_raise(self):
        scene = flat(10, 10, FLOOR)
        hung_shadow(scene, Canvas(4, 4), 0, 0, style)
        self.assertEqual(scene.colors(), {FLOOR})


if __name__ == "__main__":
    unittest.main()
