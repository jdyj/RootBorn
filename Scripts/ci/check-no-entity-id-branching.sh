#!/usr/bin/env bash
# 엔티티 데이터-드리븐 원칙(.claude/rules/path-based/assets-data.md): 코드는 특정 엔티티 id로 갈라지지 않는다.
# 대상: Packages/com.rootborn.core/Runtime, game/ (tests 폴더 제외). 주석은 보지 않는다.
# 찾는 것: id 이름(id, xxxId)을 문자열 리터럴과 ==, !=, is, Equals로 비교, switch (…Id), enum …Id.
# 지금 코드에서 걸리는 것은 Scripts/ci/allowlists/entity-id-branching.txt 에 이유와 함께 고정한다.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

ALLOW="${ENTITY_ID_ALLOWLIST:-$CI_DIR/allowlists/entity-id-branching.txt}"
SCAN=("Packages/com.rootborn.core/Runtime" "game")

# id로 읽히는 이름 한 덩어리(낱말 전체): id, _id, Id, someId. valid·android 같은 낱말 끝은 id가 아니다.
ID='([A-Za-z0-9_]*Id|_?id)'
B='(^|[^A-Za-z0-9_])'          # 이름 앞 경계
Q='([A-Za-z0-9_]+\.)*'          # a.b. 같은 앞 접두
E='([^A-Za-z0-9_]|$)'           # 이름 뒤 경계
PATTERNS=(
  "${B}${Q}${ID}\s*(==|!=)\s*\"|\"\s*(==|!=)\s*${Q}${ID}${E}"   # 이름 == "리터럴" / "리터럴" == 이름
  "${B}${Q}${ID}\s+is\s+(not\s+)?\""                              # 이름 is "리터럴"
  "${B}${Q}${ID}\s*\.\s*Equals\s*\(\s*\""                         # 이름.Equals("리터럴")
  "Equals\s*\(\s*${Q}${ID}\s*,\s*\""                              # string.Equals(이름, "리터럴")
  "Equals\s*\(\s*\"[^\"]*\"\s*,\s*${Q}${ID}${E}"                  # string.Equals("리터럴", 이름)
  "switch\s*\(\s*${Q}${ID}\s*\)"                                  # switch (이름)
  "(^|[^A-Za-z0-9_])enum\s+[A-Za-z0-9_]*Id\b"                     # enum …Id
)

mapfile -t files < <(cs_files "${SCAN[@]}")
violations=0
allowed=0
if [ "${#files[@]}" -gt 0 ]; then
  scanned="$(printf '%s\n' "${files[@]}" | cs_scan nocomment)"
  for pat in "${PATTERNS[@]}"; do
    while IFS= read -r hit; do
      [ -n "$hit" ] || continue
      path="${hit%%:*}"
      rest="${hit#*:}"; text="${rest#*:}"
      if is_allowed "$ALLOW" "$path" "$text"; then
        allowed=$((allowed + 1))
        continue
      fi
      echo "$hit  <- 엔티티 id로 분기하지 않는다"
      violations=$((violations + 1))
    done < <(printf '%s\n' "$scanned" | grep -E -- "$pat" || true)
  done
fi

if [ "$violations" -gt 0 ]; then
  echo "FAIL: 엔티티 id 분기 ${violations}건 (허용 목록 ${allowed}건 제외). .claude/rules/path-based/assets-data.md 참고." >&2
  exit 1
fi
echo "OK: 엔티티 id 분기가 없다 (${#files[@]}개 파일, 허용 목록 ${allowed}건)."
