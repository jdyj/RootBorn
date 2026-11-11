#!/usr/bin/env bash
set -euo pipefail
T_NAME=core-references
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
C=game/Rootborn.Core/src

new_root; put $C/A.cs 'using System; namespace N { class A { int F() => 1; } }'
run_check check-core-references.sh; expect "깨끗한 Core는 통과" pass

new_root; put $C/A.cs 'using UnityEngine; class A {}'
run_check check-core-references.sh; expect "using UnityEngine은 걸림" fail "A.cs:1"

new_root; put $C/A.cs 'class A { System.IO.File F; }'
run_check check-core-references.sh; expect "완전한 이름 System.IO도 걸림" fail "System.IO"

new_root; put $C/A.cs 'using System.Text.Json; class A {}'
run_check check-core-references.sh; expect "System.Text.Json은 걸림" fail

new_root; put $C/A.cs '// UnityEngine 이야기
/* System.IO */ class A { string s = "System.IO UnityEngine"; }'
run_check check-core-references.sh; expect "주석과 문자열 안의 이름은 무시" pass

new_root; run_check check-core-references.sh; expect "Core 폴더가 없으면 실패" fail
finish
