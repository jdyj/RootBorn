"""픽셀 키트 명령줄: python Tools/pixel-kit/kit.py <명령> [id…] [--kit art/kit] [--out output/pixel-kit] [--scale N]

여기는 인자 읽기만 한다. 명령 본문은 pixelkit/cli.py의 COMMANDS 표에 있다.
"""
import argparse
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from pixelkit import cli  # noqa: E402

REPO_ROOT = HERE.parents[1]  # Tools/pixel-kit/kit.py → 저장소 루트. 현재 폴더와 무관하다.


def main(argv=None) -> int:
    # 출력은 어느 콘솔 코드페이지에서도 UTF-8(한글 메시지가 깨지지 않게).
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    p = argparse.ArgumentParser(prog="kit.py", description="픽셀 키트: 그림 만들기·검사·견본판")
    p.add_argument("command", choices=sorted(cli.COMMANDS))
    p.add_argument("ids", nargs="*", help="명세 id(render는 id@상태 가능), sheet는 세트 이름")
    p.add_argument("--kit", default=str(REPO_ROOT / "art" / "kit"), help="키트 폴더(기본: 저장소의 art/kit)")
    p.add_argument("--out", default=None,
                   help="내보낼 폴더(기본: 저장소의 output/pixel-kit. test-level은 --repo의 art/world/test, room build는 art/world/rooms/<id>)")
    p.add_argument("--scale", type=int, default=None, help="보기용 확대 배율(render 6, sheet·scene 4, parts 8)")
    p.add_argument("--repo", default=str(REPO_ROOT), help="export가 쓸 저장소 뿌리(기본: 이 도구가 든 저장소)")
    p.add_argument("--dry-run", action="store_true", help="export·room build·room repaint: 쓸 파일을 찍기만 한다")
    p.add_argument("--force", action="store_true", help="room build: 이미 있는 방 파일을 평면에서 통째로 다시 쓴다(LDtk에서 손으로 고친 것은 지워진다 — 그때는 room repaint. 시트는 덧붙이기만)")
    args = p.parse_intermixed_args(argv)  # 옵션을 id 앞에 써도 된다
    if args.out is None and args.command not in ("test-level", "room"):   # 이 둘은 None을 "저장소의 기본 자리"로 읽는다
        args.out = str(REPO_ROOT / "output" / "pixel-kit")
    return cli.run(args)


if __name__ == "__main__":
    sys.exit(main())
