#!/usr/bin/env bash
set -euo pipefail
T_NAME=localization-keys
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

new_root
put data/localization/ko.json '{"a":"가","b":"나"}'; put data/localization/en.json '{"a":"A"}'
put game/localization/ko.json '{"c":"다"}';          put game/localization/en.json '{"b":"B","c":"C"}'
run_check check-localization-keys.sh; expect "두 폴더를 합치면 같은 키면 통과" pass

new_root
put data/localization/ko.json '{"a":"가","b":"나"}'; put data/localization/en.json '{"a":"A"}'
run_check check-localization-keys.sh; expect "en에 b가 없으면 걸림" fail "en: 키 'b'"

new_root
put data/localization/ko.json '{"a":"가"}'; put data/localization/en.json '{"a":"A"}'; put game/localization/fr.json '{"a":"A"}'
run_check check-localization-keys.sh; expect "새 언어 파일을 더하면 그 언어도 검사한다(파일만 더하면 된다)" pass "3개 언어"

new_root
put data/localization/ko.json '{"a":"가"}'; put data/localization/en.json '{"a":"A"}'; put game/localization/fr.json '{"a":"A","z":"Z"}'
run_check check-localization-keys.sh; expect "새 언어에만 있는 키는 다른 언어가 빠진 것" fail "ko: 키 'z'"

new_root; run_check check-localization-keys.sh; expect "현지화 파일이 없으면 실패" fail
finish
