#!/usr/bin/env bash
# 배포용 실행 폴더를 만든다. 사용법과 목록 고치는 법은 README.md.
#   Tools/publish/publish.sh [플랫폼...]   (기본: win-x64 linux-x64)
set -euo pipefail
shopt -s globstar nullglob dotglob

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
LIST="${PUBLISH_CONTENT_LIST:-$HERE/content.txt}"
OUT_ROOT="${PUBLISH_OUT:-$REPO/output/publish}"
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

PLATFORMS=("$@")
[ ${#PLATFORMS[@]} -gt 0 ] || PLATFORMS=(win-x64 linux-x64)

cd "$REPO"

# ---- 복사할 파일 목록 만들기 (저장소 기준 상대 경로, 중복 없음) ----
declare -A FILES=()     # 상대 경로 → 1
declare -a PROBLEMS=()

is_unity_only() {
  case "$1" in *.meta|*.unity|*.asset|*.prefab|*.cs) return 0 ;; esac
  return 1
}

add_file() {
  local f="${1#./}"
  is_unity_only "$f" && return 0
  if [ -f "$f" ]; then FILES["$f"]=1; else PROBLEMS+=("없는 파일: $f"); fi
}

add_glob() {
  local pattern="$1" m any=0
  for m in $pattern; do
    any=1
    if [ -d "$m" ]; then
      while IFS= read -r -d '' f; do add_file "$f"; done < <(find "$m" -type f -print0)
    else
      add_file "$m"
    fi
  done
  [ $any -eq 1 ] || PROBLEMS+=("맞는 파일이 없는 줄: $pattern")
}

remove_glob() {
  local pattern="$1" f
  for f in "${!FILES[@]}"; do
    # shellcheck disable=SC2053
    if [[ "$f" == $pattern ]]; then unset 'FILES[$f]'; fi
  done
  return 0
}

add_refs() {      # JSON 안의 "Assets/...png" 문자열
  local pattern="$1" j p
  for j in $pattern; do
    [ -f "$j" ] || continue
    while IFS= read -r p; do
      if [ -n "$p" ]; then add_file "$p"; fi
    done < <(grep -ohE 'Assets/[^"]+\.png' "$j" | sort -u)
  done
}

add_ldtk() {      # .ldtk 하나: 파일, 레벨 파일, 타일셋 PNG
  local pattern="$1" ldtk dir name p resolved
  for ldtk in $pattern; do
    [ -f "$ldtk" ] || continue
    add_file "$ldtk"
    dir="$(dirname "$ldtk")"; name="$(basename "$ldtk" .ldtk)"
    if [ -d "$dir/$name" ]; then
      for p in "$dir/$name"/*.ldtkl; do add_file "$p"; done
    fi
    while IFS= read -r p; do
      resolved="$(realpath -m --relative-to="$REPO" "$dir/$p")"
      case "$resolved" in
        ../*) continue ;;                       # 저장소 밖을 가리키는 타일셋: 쓰지 않는 참조(게임은 없는 타일셋을 건너뛴다)
      esac
      if [ -f "$resolved" ]; then add_file "$resolved"; fi
    done < <(grep -ohE '"relPath": *"[^"]+"' "$ldtk" | sed -E 's/.*: *"([^"]+)"/\1/' | sort -u)
  done
}

lineno=0
while IFS= read -r line || [ -n "$line" ]; do
  lineno=$((lineno + 1))
  line="${line%$'\r'}"
  [[ "$line" =~ ^[[:space:]]*(#|$) ]] && continue
  line="$(echo "$line" | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')"
  case "$line" in
    '!'*)       remove_glob "${line#!}" ;;
    '@refs '*)  add_refs "${line#@refs }" ;;
    '@ldtk '*)  add_ldtk "${line#@ldtk }" ;;
    '@png '*)   add_glob "${line#@png }" ;;
    '@'*)       echo "content.txt:$lineno 모르는 지시어: $line" >&2; exit 2 ;;
    *)          add_glob "$line" ;;
  esac
done < "$LIST"

# 자동: 그림마다 좌표표
for f in "${!FILES[@]}"; do
  case "$f" in
    *.png) if [ -f "data/atlases/$f.atlas.json" ]; then FILES["data/atlases/$f.atlas.json"]=1; fi ;;
  esac
done
for p in "${PROBLEMS[@]+"${PROBLEMS[@]}"}"; do echo "경고: $p" >&2; done

# ---- 플랫폼마다: 게시 + 복사 ----
for rid in "${PLATFORMS[@]}"; do
  dest="$OUT_ROOT/$rid"
  echo "== $rid -> $dest"
  case "$dest" in "$REPO"/output/publish/*|"$OUT_ROOT"/*) ;; *) echo "출력 폴더가 이상하다: $dest" >&2; exit 2 ;; esac
  rm -rf "$dest"
  dotnet publish game/Rootborn.Host -c Release -r "$rid" --self-contained -o "$dest" -nologo -v quiet
  # 파일마다 cp 를 띄우면 Windows 에서 몹시 느리다 — tar 한 번으로 같은 상대 경로에 푼다.
  printf '%s
' "${!FILES[@]}" | LC_ALL=C sort | tar -cf - -T - | tar -xf - -C "$dest"
  echo "   복사한 게임 파일 ${#FILES[@]}개"
done
