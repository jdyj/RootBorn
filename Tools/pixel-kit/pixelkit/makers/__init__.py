"""만드는 부품 등록 표. 새 종류는 함수 하나와 @register 한 줄로 더한다(if/switch로 닫지 않는다)."""
from typing import Callable, Dict


class RecipeError(Exception):
    """조립 명세가 잘못됐다. 메시지는 항상 명세 id로 시작한다."""


_MAKERS: Dict[str, Callable] = {}


def register(name: str):
    """def maker(spec: dict, ctx: Ctx) -> Canvas 를 name으로 등록한다. 그림은 외곽선 전, 크기 자유."""
    def deco(fn: Callable) -> Callable:
        if name in _MAKERS:
            raise RecipeError(f"{name}: 만드는 부품 이름이 겹친다")
        _MAKERS[name] = fn
        return fn
    return deco


def get_maker(name: str) -> Callable:
    try:
        return _MAKERS[name]
    except KeyError:
        raise RecipeError(f"{name}: 등록되지 않은 만드는 부품. 있는 것: {', '.join(sorted(_MAKERS))}") from None


# 만드는 부품 모듈은 여기서 가져와 @register가 돌게 한다(모듈 하나에 한 줄).
from . import tiles  # noqa: E402,F401
from . import furniture  # noqa: E402,F401
from . import building  # noqa: E402,F401
from . import seating  # noqa: E402,F401
from . import storage  # noqa: E402,F401
from . import objects  # noqa: E402,F401
from . import care  # noqa: E402,F401
