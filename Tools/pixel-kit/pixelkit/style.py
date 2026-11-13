"""화풍 파일(art/kit/style.json) 읽기. 색은 팔레트 키(`재질.단계`)로만 얻는다."""
import json
from typing import Dict, FrozenSet, Optional, Tuple

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
        self._brighter: Dict[RGBA, RGBA] = {}
        for name in sorted(raw["palette"]):
            ramp = [_hex(h) for h in raw["palette"][name]]
            for i, c in enumerate(ramp):
                self._colors[f"{name}.{i}"] = c
                # 같은 색이 여러 줄에 있으면 이름순으로 처음 나온 줄의 다음 단계(결정적).
                if i + 1 < len(ramp) and c not in self._brighter:
                    self._brighter[c] = ramp[i + 1]
        self.palette: FrozenSet[RGBA] = frozenset(set(self._colors.values()) | {self.outline})

    @staticmethod
    def load(path: str) -> "Style":
        with open(path, "r", encoding="utf-8") as f:
            return Style(json.load(f))

    def brighter(self, rgba: RGBA) -> Optional[RGBA]:
        """같은 줄(ramp)의 한 단계 밝은 색. 가장 밝은 단계거나 팔레트 단계가 아니면 None.
        같은 hex가 여러 줄에 있으면 줄 이름순으로 먼저 나온 줄(그 줄에서 가장 밝지 않은 첫 자리)의 다음 단계다."""
        return self._brighter.get(tuple(rgba))

    def color(self, key: str) -> RGBA:
        try:
            return self._colors[key]
        except KeyError:
            raise KeyError(f"팔레트에 없는 키: {key}") from None
