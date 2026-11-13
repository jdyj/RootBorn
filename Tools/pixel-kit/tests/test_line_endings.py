"""art/ 아래 도구가 쓰는 글 파일의 줄바꿈 규칙.

도구는 "같은 내용이면 쓰지 않는다"를 바이트로 견준다(export, test-level). 그래서 체크아웃이 줄바꿈을 CRLF로 바꾸면
다시 내보낼 때 바뀐 것으로 보고 다시 쓴다. 이 테스트는 증상(한 번의 비교)이 아니라 규칙을 지킨다:
도구가 art/ 아래에 쓰는 글 파일(자리 표 *.slots.json, 시험 맵 maps.json·*.ldtk)은 git 속성으로 LF가 고정되고,
실제 저장소의 그 파일에 CRLF가 없다. PNG는 binary다.

art/kit/**/*.json(팔레트, 부품, 명세)은 도구가 읽기만 하고 다시 쓰지 않으므로 이 규칙이 필요 없다(바이트 비교 대상이 아니다).
"""

import shutil
import subprocess
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
ART = REPO / "art"


def written_text_files():
    """도구가 art/ 아래에 쓰는 글 파일(실제 저장소에 있는 것). art/kit은 도구의 입력이라 뺀다."""
    found = []
    for pattern in ("**/*.slots.json", "**/*.ldtk", "**/maps.json"):
        for path in ART.glob(pattern):
            if (ART / "kit") in path.parents:
                continue
            if path not in found:
                found.append(path)
    return sorted(found)


def check_attr(paths, *attrs):
    """git check-attr 결과 {(상대 경로, 속성): 값}."""
    rel = [p.relative_to(REPO).as_posix() for p in paths]
    out = subprocess.run(["git", "check-attr", *attrs, "--", *rel], cwd=REPO, capture_output=True, text=True, check=True).stdout
    result = {}
    for line in out.splitlines():
        path, attr, value = line.rsplit(": ", 2)
        result[(path, attr)] = value
    return result


@unittest.skipIf(shutil.which("git") is None, "git이 없다")
class ArtLineEndingTests(unittest.TestCase):
    def test_tool_written_text_files_exist(self):
        # 규칙이 빈 목록에서 저절로 통과하지 않게: Task 9에서 내보낸 자리 표와 시험 맵이 있어야 한다.
        names = {p.name for p in written_text_files()}
        self.assertTrue({"maps.json", "pixel-kit-test.ldtk"} <= names, names)
        self.assertTrue(any(n.endswith(".slots.json") for n in names), names)

    def test_attributes_file_covers_art(self):
        self.assertTrue((ART / ".gitattributes").is_file(), "art/.gitattributes가 없다")

    def test_tool_written_text_files_are_lf_by_attribute(self):
        files = written_text_files()
        attrs = check_attr(files, "text", "eol")
        for path in files:
            rel = path.relative_to(REPO).as_posix()
            with self.subTest(path=rel):
                self.assertEqual(attrs[(rel, "text")], "set", f"{rel}: text 속성이 없다")
                self.assertEqual(attrs[(rel, "eol")], "lf", f"{rel}: eol=lf가 아니다")

    def test_tool_written_text_files_have_no_crlf(self):
        for path in written_text_files():
            with self.subTest(path=path.relative_to(REPO).as_posix()):
                self.assertNotIn(b"\r\n", path.read_bytes())

    def test_exported_pngs_are_binary(self):
        pngs = [p for p in ART.glob("**/*.png") if (ART / "kit") not in p.parents]
        self.assertTrue(pngs)
        attrs = check_attr(pngs, "text", "diff")
        for path in pngs:
            rel = path.relative_to(REPO).as_posix()
            with self.subTest(path=rel):
                self.assertEqual(attrs[(rel, "text")], "unset", f"{rel}: binary가 아니다")


if __name__ == "__main__":
    unittest.main()
