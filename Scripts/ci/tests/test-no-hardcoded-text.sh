#!/usr/bin/env bash
set -euo pipefail
T_NAME=no-hardcoded-text
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
S=game/Rootborn.Screens/A.cs

new_root; put $S 'class A { string k = "ui.hud.energy"; /* 한글 주석 */ } // 한글 주석'
run_check check-no-hardcoded-text.sh; expect "키와 주석만 있으면 통과" pass

new_root; put $S 'class A { string t = "체력"; }'
run_check check-no-hardcoded-text.sh; expect "한글 리터럴은 걸림" fail "$S:1"

new_root; put $S 'class A { string t = $"체력 {1}"; }'
run_check check-no-hardcoded-text.sh; expect "보간 문자열도 걸림" fail

new_root; put $S 'class A { void F() { throw new System.InvalidOperationException("이미 등록됨"); } }'
run_check check-no-hardcoded-text.sh; expect "예외 메시지는 개발자용이라 통과" pass

new_root; put game/tests/Rootborn.Screens.Tests/T.cs 'class T { string t = "체력"; }'
run_check check-no-hardcoded-text.sh; expect "테스트는 보지 않는다" pass

new_root; put game/Rootborn.Host/H.cs 'class H { string t = "체력"; }'
run_check check-no-hardcoded-text.sh; expect "Screens 밖은 보지 않는다" pass

new_root; put $S 'class A { string t = "체력"; }'
put allow.txt "$S|\"체력\"|임시"
run_check check-no-hardcoded-text.sh TEXT_ALLOWLIST="$T_ROOT/allow.txt"; expect "허용 목록에 있으면 통과" pass "허용 목록 1건"
finish
