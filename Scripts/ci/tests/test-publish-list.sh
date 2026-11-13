#!/usr/bin/env bash
# 배포 목록(Tools/publish/publish.sh --list)이 데이터가 가리키는 그림을 고르는지 임시 저장소에서 본다.
# C# 쪽 PublishListCopy 는 @refs·그림을 보지 않으므로 이 시험이 그림 따라오기의 유일한 근거다.
set -euo pipefail
T_NAME=publish-list
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
REAL="$(cd "$CI_DIR/../.." && pwd)"

# 실제 publish.sh·content.txt 를 임시 저장소에 복사해 그 저장소의 목록을 만든다(스크립트는 자기 위치로 저장소를 정한다).
fake_repo() {
  new_root
  mkdir -p "$T_ROOT/Tools/publish"
  cp "$REAL/Tools/publish/publish.sh" "$REAL/Tools/publish/content.txt" "$T_ROOT/Tools/publish/"
}
list() {
  set +e
  OUT="$(bash "$T_ROOT/Tools/publish/publish.sh" --list 2>/dev/null)"
  CODE=$?
  set -e
}
# has <이름> <경로>  /  lacks <이름> <경로>
has()   { if printf '%s\n' "$OUT" | grep -qxF -- "$2"; then CODE=0; expect "$1" pass; else CODE=1; expect "$1" pass; fi; }
lacks() { if printf '%s\n' "$OUT" | grep -qxF -- "$2"; then CODE=1; expect "$1" pass; else CODE=0; expect "$1" pass; fi; }

fake_repo
put data/interiors/F.json '[{"type":"furniture","id":"a","sprite":"art/props/x/y.png"},{"type":"furniture","id":"b","sprite":"Assets/Old/b.png"}]'
put art/props/x/y.png png; put data/atlases/art/props/x/y.png.atlas.json '{}'
put Assets/Old/b.png png; put data/atlases/Assets/Old/b.png.atlas.json '{}'
put art/props/x/unused.png png
put art/kit/src/tool.png png
put art/world/test/t.png png
list
has   "art/ 그림이 데이터가 가리키면 목록에 있다" art/props/x/y.png
has   "art/ 그림의 좌표표도 따라온다" data/atlases/art/props/x/y.png.atlas.json
has   "Assets/ 그림은 그대로 따라온다" Assets/Old/b.png
has   "Assets/ 그림의 좌표표도 따라온다" data/atlases/Assets/Old/b.png.atlas.json
lacks "데이터가 가리키지 않는 art/ 그림은 없다" art/props/x/unused.png
lacks "art/kit 도구 원본은 없다" art/kit/src/tool.png
lacks "art/world/test 시험 맵은 없다" art/world/test/t.png
finish
