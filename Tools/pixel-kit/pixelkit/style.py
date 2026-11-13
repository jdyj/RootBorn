"""화풍 파일(art/kit/style.json) 읽기. 색은 팔레트 키(`재질.단계`)로만 얻는다."""
import json
from typing import Dict, FrozenSet, Tuple

RGBA = Tuple[int, int, int, int]


def _hex(s: str) -> RGBA:
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


class Style:
    def __init__(self, raw: dict):
        self.cell: int = int(raw["cell"])
        self.outline: RGBA = _hex(raw["outline"])
        self.shadow: Dict[str, float] = dict(raw["shadow"])
        self.max_colors: Dict[str, int] = dict(raw["max_colors"])
        self._colors: Dict[str, RGBA] = {}
        for name, ramp in raw["palette"].items():
            for i, h in enumerate(ramp):
                self._colors[f"{name}.{i}"] = _hex(h)
        self.palette: FrozenSet[RGBA] = frozenset(set(self._colors.values()) | {self.outline})

    @staticmethod
    def load(path: str) -> "Style":
        with open(path, "r", encoding="utf-8") as f:
            return Style(json.load(f))

    def color(self, key: str) -> RGBA:
        try:
            return self._colors[key]
        except KeyError:
            raise KeyError(f"팔레트에 없는 키: {key}") from None
