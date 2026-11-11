#!/usr/bin/env bash
set -euo pipefail
T_NAME=no-entity-id-branching
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
C=game/Rootborn.Core/src/A.cs
G=game/Rootborn.Game/G.cs

new_root; put $C 'class A { bool F(string id) => id != null && id.Length > 0; string s = "id"; }'
run_check check-no-entity-id-branching.sh; expect "id를 리터럴과 비교하지 않으면 통과" pass

new_root; put $C 'class A { bool F(string questId) { if (questId == "intro") return true; return false; } }'
run_check check-no-entity-id-branching.sh; expect "xxxId == 리터럴은 걸림" fail "$C:1"

new_root; put $G 'class G { bool F(string x) => "intro" != x.ActivityId; }'
run_check check-no-entity-id-branching.sh; expect "리터럴 != xxx.Id도 걸림" fail

new_root; put $G 'class G { void F(string careerId) { switch (careerId) { case "a": break; } } }'
run_check check-no-entity-id-branching.sh; expect "switch (…Id)는 걸림" fail "switch"

new_root; put $G 'class G { bool F(string id) => id.Equals("a"); }'
run_check check-no-entity-id-branching.sh; expect "id.Equals(리터럴)도 걸림" fail

new_root; put $G 'class G { bool F(string id) => string.Equals(id, "b"); }'
run_check check-no-entity-id-branching.sh; expect "string.Equals(id, 리터럴)도 걸림" fail

new_root; put $G 'class G { bool F(object itemId) => itemId is "a"; }'
run_check check-no-entity-id-branching.sh; expect "is 리터럴도 걸림" fail

new_root; put $G 'enum ToolId { Axe }'
run_check check-no-entity-id-branching.sh; expect "enum …Id는 걸림" fail

new_root; put $G 'class G { bool F(string valid, string android) => valid == "x" || android == "y"; }'
run_check check-no-entity-id-branching.sh; expect "valid·android 같은 낱말 끝은 id가 아니다" pass

new_root; put $G '// if (questId == "a") 예시
class G { string m = "questId == a"; }'
run_check check-no-entity-id-branching.sh; expect "주석과 문자열 안은 무시" pass

new_root; put game/tests/Rootborn.Game.Tests/T.cs 'class T { bool F(string questId) => questId == "intro"; }'
run_check check-no-entity-id-branching.sh; expect "tests 폴더는 보지 않는다" pass

new_root; put $G 'class G { bool F(string questId) => questId == "intro"; }'
put allow.txt "# 주석
$G|questId == \"intro\"|시험용 이유"
run_check check-no-entity-id-branching.sh ENTITY_ID_ALLOWLIST="$T_ROOT/allow.txt"; expect "허용 목록에 있으면 통과" pass "허용 목록 1건"
finish
