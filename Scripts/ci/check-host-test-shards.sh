#!/usr/bin/env bash
# CI의 Host 테스트 묶음(매트릭스) 정의 Scripts/ci/host-test-shards.txt 가 테스트 클래스를 빠뜨리거나 겹치게 나누지 않았는지 본다.
# - 정의의 클래스가 모두 game/tests/Rootborn.Host.Tests 에 있다(이름이 바뀌면 걸린다).
# - 한 클래스가 두 묶음에 있지 않다. 이름 있는 묶음이 비지 않았다.
# - 나머지 묶음('<이름>: *')이 정확히 하나, 맨 끝에 있다: 어느 묶음에도 적지 않은 클래스(새 테스트)는 거기서 돈다.
# - 필터('<완전한 이름>.' 포함)가 다른 클래스를 함께 고르지 않는다. 중첩 클래스 안의 테스트가 없다.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"
"$PY" "$CI_LIB_DIR/host_shards.py" check
