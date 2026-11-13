import unittest

from PIL import Image

from pixelkit.boards import RED_FRAME, Entry, board_image, enlarge
from pixelkit.canvas import Canvas


def _cv(w, h, rgba=(10, 200, 30, 255)):
    cv = Canvas(w, h)
    cv.rect(0, 0, w, h, rgba)
    return cv


class BoardTests(unittest.TestCase):
    def test_enlarge_is_exact_integer_nearest(self):
        img = Image.new("RGBA", (2, 1))
        img.putpixel((0, 0), (255, 0, 0, 255))
        img.putpixel((1, 0), (0, 0, 255, 255))
        big = enlarge(img, 3)
        self.assertEqual(big.size, (6, 3))
        self.assertEqual(big.getpixel((2, 2)), (255, 0, 0, 255))
        self.assertEqual(big.getpixel((3, 0)), (0, 0, 255, 255))

    def test_board_wraps_and_marks_bad_entries_only(self):
        entries = [Entry(f"{i} item.{i}", _cv(32, 32), i == 2) for i in range(1, 9)]
        img = board_image(entries, 4)
        self.assertGreater(img.height, 32 * 4 + 20)  # 한 줄을 넘어 줄바꿈
        reds = sum(1 for p in img.getdata() if p[:3] == RED_FRAME)
        self.assertGreater(reds, 0)
        clean = board_image([Entry("1 a", _cv(32, 32), False)], 4)
        self.assertEqual(sum(1 for p in clean.getdata() if p[:3] == RED_FRAME), 0)

    def test_board_is_deterministic(self):
        entries = [Entry("1 a", _cv(32, 32), True), Entry("2 b", _cv(64, 32), False)]
        a = board_image(entries, 2)
        b = board_image(entries, 2)
        self.assertEqual(a.tobytes(), b.tobytes())

    def test_a_long_label_is_not_cut_off(self):
        # 그림보다 라벨이 넓어도 칸이 라벨에 맞게 넓어져 오른쪽이 잘리지 않는다.
        img = board_image([Entry("1 " + "x" * 80, _cv(32, 32), False)], 1)
        self.assertGreater(img.width, 200)


if __name__ == "__main__":
    unittest.main()
