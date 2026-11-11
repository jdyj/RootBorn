#!/usr/bin/env bash
# 다국어 규칙: data/localization 과 game/localization 을 합친 뒤(GameText.LoadMerged와 같다) 모든 언어가 같은 키를 가진다.
# 언어 목록은 두 폴더의 *.json 이름에서 읽는다(코드에 적지 않는다). jq가 있으면 jq, 없으면 파이썬.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

DIRS=("data/localization" "game/localization")

langs=()
for d in "${DIRS[@]}"; do
  [ -d "$d" ] || continue
  for f in "$d"/*.json; do
    [ -f "$f" ] || continue
    l="$(basename "$f" .json)"
    case " ${langs[*]:-} " in *" $l "*) ;; *) langs+=("$l") ;; esac
  done
done
if [ "${#langs[@]}" -eq 0 ]; then
  echo "FAIL: 현지화 파일이 없다 (${DIRS[*]})" >&2
  exit 1
fi

# 한 언어의 합친 키 목록(정렬, 한 줄에 하나). 폴더마다 있으면 합친다.
keys_of() {
  local lang="$1" f files=()
  for d in "${DIRS[@]}"; do
    f="$d/$lang.json"
    [ -f "$f" ] && files+=("$f")
  done
  if [ "${#files[@]}" -eq 0 ]; then return 0; fi
  if command -v jq >/dev/null 2>&1; then
    jq -r 'keys[]' "${files[@]}" | tr -d '\015' | LC_ALL=C sort -u
  else
    "$PY" - "${files[@]}" <<'PYEOF' | LC_ALL=C sort -u
import json, sys
sys.stdout.reconfigure(encoding="utf-8", newline="\n")
for p in sys.argv[1:]:
    with open(p, encoding="utf-8-sig") as f:
        d = json.load(f)
    if not isinstance(d, dict):
        sys.stderr.write("%s: 키-값 객체가 아니다\n" % p)
        sys.exit(1)
    for k in d:
        print(k)
PYEOF
  fi
}

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
: > "$tmp/all"
for l in "${langs[@]}"; do
  keys_of "$l" > "$tmp/$l.keys"
  cat "$tmp/$l.keys" >> "$tmp/all"
done
LC_ALL=C sort -u "$tmp/all" > "$tmp/union"

violations=0
for l in "${langs[@]}"; do
  while IFS= read -r k; do
    echo "$l: 키 '$k'가 없다 (다른 언어에는 있다)"
    violations=$((violations + 1))
  done < <(LC_ALL=C comm -23 "$tmp/union" "$tmp/$l.keys")
done

if [ "$violations" -gt 0 ]; then
  echo "FAIL: 언어마다 키가 다르다 — ${violations}건 (언어: ${langs[*]})." >&2
  exit 1
fi
echo "OK: ${#langs[@]}개 언어(${langs[*]})가 같은 키 $(wc -l < "$tmp/union" | tr -d ' ')개를 가진다."
