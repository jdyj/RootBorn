#!/usr/bin/env bash
# CI의 Host 테스트 묶음(Scripts/ci/host-test-shards.txt)을 쓴다. 정의 검사는 check-host-test-shards.sh.
#   bash Scripts/ci/host-test-shards.sh filter <묶음>   dotnet test --filter 식(나머지 묶음은 다른 묶음에 없는 전부)
#   bash Scripts/ci/host-test-shards.sh names           묶음 이름 JSON 배열
#   bash Scripts/ci/host-test-shards.sh list            클래스마다 묶음
# 로컬에서 한 묶음만: dotnet test game/tests/Rootborn.Host.Tests --filter "$(bash Scripts/ci/host-test-shards.sh filter soak)"
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"
"$PY" "$CI_LIB_DIR/host_shards.py" "$@"
