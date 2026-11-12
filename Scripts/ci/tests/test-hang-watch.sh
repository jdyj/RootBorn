#!/usr/bin/env bash
set -euo pipefail
T_NAME=hang-watch
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
# Host 테스트를 돌리는 세 곳이 dotnet test에 멈춤 감시 인자를 넘기는지(실제 파일을 읽는다 — 임시 저장소가 아니다).
REPO="$(cd "$CI_DIR/../.." && pwd)"

has() { # has <파일> <조각>
  CODE=0; OUT=""
  grep -qF -- "$2" "$REPO/$1" || { CODE=1; OUT="$1에 '$2'가 없다"; }
}

for f in Scripts/remote-test/remote-run.sh Scripts/ci/run-all.sh .github/workflows/ci.yml; do
  has "$f" "--blame-hang-timeout"; expect "$f: 멈춤 감시 시간 인자" pass
  has "$f" "ROOTBORN_TEST_HANG_MINUTES"; expect "$f: 분 값을 환경 변수로 받는다" pass
  has "$f" "--blame-hang-dump-type none"; expect "$f: 덤프를 만들지 않는다" pass
done
finish
