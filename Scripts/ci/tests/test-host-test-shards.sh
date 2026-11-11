#!/usr/bin/env bash
set -euo pipefail
T_NAME=host-test-shards
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
H=game/tests/Rootborn.Host.Tests
S=Scripts/ci/host-test-shards.txt

# run_tool <스크립트> <인자...> : OUT, CODE (run_check는 뒤 인자를 환경 변수로 받는다)
run_tool() {
  local script="$1"; shift
  set +e
  OUT="$(bash "$CI_DIR/$script" "$@" 2>&1)"
  CODE=$?
  set -e
}

fixture() { # fixture <파일> <네임스페이스> <클래스>
  put "$H/$1" "using NUnit.Framework;
namespace $2
{
    public sealed class $3
    {
        [Test]
        public void Works() { var s = \"{ class Fake [Test] }\"; }
    }
}"
}

base() {
  new_root
  fixture A.cs Rootborn.Host.Tests Alpha
  fixture B.cs Rootborn.Host.Tests Beta
  fixture Sim/C.cs Rootborn.Host.Tests.Simulation Gamma
  put "$H/Helper.cs" 'namespace Rootborn.Host.Tests { public sealed class Helper { // [Test]
  /* [Test] */ public void NotATest() { } } }'
}

base; put $S '# 주석
soak: Gamma
one: Alpha   # 끝 주석
rest: *'
run_check check-host-test-shards.sh; expect "겹치지 않고 나머지 묶음이 맨 끝이면 통과" pass "클래스 3개가 묶음 3개"

run_tool host-test-shards.sh filter one; expect "이름 있는 묶음의 필터는 그 클래스만" pass "FullyQualifiedName~Rootborn.Host.Tests.Alpha."
run_tool host-test-shards.sh filter rest
expect "나머지 묶음의 필터는 적은 클래스를 뺀다" pass "FullyQualifiedName!~Rootborn.Host.Tests.Alpha.&FullyQualifiedName!~Rootborn.Host.Tests.Simulation.Gamma."
if printf '%s' "$OUT" | grep -q Beta; then CODE=9; fi; expect "나머지 묶음 필터에 적지 않은 클래스가 들지 않는다" pass
run_tool host-test-shards.sh names; expect "묶음 이름은 정의 순서" pass '["soak", "one", "rest"]'
run_tool host-test-shards.sh list; expect "적지 않은 새 클래스는 나머지 묶음" pass "rest	Rootborn.Host.Tests.Beta"
run_tool host-test-shards.sh filter nope; expect "없는 묶음은 실패" fail "있는 묶음"

base; put $S 'a: Alpha
b: Alpha Beta
rest: *'
run_check check-host-test-shards.sh; expect "한 클래스가 두 묶음이면 걸림" fail "겹친다"

base; put $S 'a: Alpha Delta
rest: *'
run_check check-host-test-shards.sh; expect "없는 클래스를 적으면 걸림(이름 바뀜)" fail "Delta"

base; put $S 'a: Helper
rest: *'
run_check check-host-test-shards.sh; expect "주석 속 [Test]만 있는 클래스는 테스트가 아니다" fail "Helper"

base; put $S 'a: Alpha
b: Beta'
run_check check-host-test-shards.sh; expect "나머지 묶음이 없으면 걸림" fail "나머지 묶음"

base; put $S 'rest: *
a: Alpha'
run_check check-host-test-shards.sh; expect "나머지 묶음이 맨 끝이 아니면 걸림" fail "맨 끝"

base; put $S 'a:
rest: *'
run_check check-host-test-shards.sh; expect "빈 묶음은 걸림" fail "비었다"

base; put $S 'a: Alpha
a: Beta
rest: *'
run_check check-host-test-shards.sh; expect "묶음 이름이 겹치면 걸림" fail "두 번"

base; put $S 'a: Alpha \
  Beta
rest: *'
run_check check-host-test-shards.sh; expect "줄 끝 \ 로 이어 쓴다" pass "클래스 3개가 묶음 2개"

base; fixture D.cs Rootborn.Host.Tests.Alpha Inner; put $S 'rest: *'
run_check check-host-test-shards.sh; expect "한 클래스의 필터가 다른 클래스를 함께 고르면 걸림" fail "골린다"

base; put "$H/N.cs" 'namespace Rootborn.Host.Tests { public sealed class Outer { public sealed class Inner { [Test] public void T() { } } } }'; put $S 'rest: *'
run_check check-host-test-shards.sh; expect "중첩 클래스 안의 테스트는 걸림" fail "중첩"

base; fixture A2.cs Rootborn.Host.Tests.Other Alpha; put $S 'rest: *'
run_check check-host-test-shards.sh; expect "네임스페이스만 다른 같은 이름은 걸림" fail "겹친다"

base; put $S 'one: Alpha
rest: *'; put .github/workflows/ci.yml '    strategy:
      matrix:
        shard: [one, rest]'
run_check check-host-test-shards.sh; expect "워크플로 matrix 가 묶음 이름과 같으면 통과" pass
put .github/workflows/ci.yml '        shard: [one, other]'
run_check check-host-test-shards.sh; expect "워크플로 matrix 가 묶음 이름과 다르면 걸림" fail "matrix shard"

new_root; put $S 'rest: *'
run_check check-host-test-shards.sh; expect "테스트 폴더가 없으면 실패" fail "폴더가 없다"
finish
