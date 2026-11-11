#!/usr/bin/env bash
# 배포용 실행 폴더를 만든다. 사용법과 목록 고치는 법은 README.md.
#   Tools/publish/publish.sh [플랫폼...]   (기본: win-x64 linux-x64)
#   Tools/publish/publish.sh --list       복사할 게임 파일 목록만 낸다(게시하지 않는다. verify.sh 가 쓴다)
set -euo pipefail
shopt -s globstar nullglob dotglob

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
LIST="${PUBLISH_CONTENT_LIST:-$HERE/content.txt}"
OUT_ROOT="${PUBLISH_OUT:-$REPO/output/publish}"
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

LIST_ONLY=0
if [ "${1:-}" = "--list" ]; then LIST_ONLY=1; shift; fi
PLATFORMS=("$@")
[ ${#PLATFORMS[@]} -gt 0 ] || PLATFORMS=(win-x64 linux-x64)

cd "$REPO"

# ---- 복사할 파일 목록 만들기 (저장소 기준 상대 경로, 중복 없음) ----
# Windows(Git Bash)에서는 프로세스 하나 띄우는 데 수십 ms가 든다. 파일마다 grep·cp 를 띄우지 않는다
# (예전에는 @refs 가 JSON 파일마다 grep 을 띄워 목록 만들기만 20분이 넘었다).
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
  local pattern="$1" m f any=0
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

# 배포에 들어가는 JSON(앞 줄들이 모은 것) 가운데 글롭에 맞는 것 안의 "Assets/...png" 문자열을 더한다.
# 앞에서 뺀 JSON(예: data/atlases)은 보지 않는다 — 그러지 않으면 좌표표가 가리키는 그림이 쓰이든 말든 전부 따라온다.
add_refs() {
  local pattern="$1" j p
  local -a jsons=()
  for j in $pattern; do
    if [ -n "${FILES[$j]+x}" ]; then jsons+=("$j"); fi
  done
  if [ ${#jsons[@]} -eq 0 ]; then
    PROBLEMS+=("@refs 가 볼 JSON 이 없는 줄(배포에 들어가는 JSON 만 본다): $pattern")
    return 0
  fi
  while IFS= read -r p; do
    if [ -n "$p" ]; then add_file "$p"; fi
  done < <(printf '%s\0' "${jsons[@]}" | xargs -0 grep -ohE 'Assets/[^"]+\.png' | LC_ALL=C sort -u)
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
  line="${line#"${line%%[![:space:]]*}"}"       # 앞뒤 공백 떼기(프로세스를 띄우지 않고)
  line="${line%"${line##*[![:space:]]}"}"
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

if [ $LIST_ONLY -eq 1 ]; then
  printf '%s\n' "${!FILES[@]}" | LC_ALL=C sort
  exit 0
fi

# ---- 플랫폼마다: 게시 + 복사 ----
for rid in "${PLATFORMS[@]}"; do
  dest="$OUT_ROOT/$rid"
  echo "== $rid -> $dest"
  case "$dest" in "$REPO"/output/publish/*|"$OUT_ROOT"/*) ;; *) echo "출력 폴더가 이상하다: $dest" >&2; exit 2 ;; esac
  rm -rf "$dest"
  dotnet publish game/Rootborn.Host -c Release -r "$rid" --self-contained -o "$dest" -nologo -v quiet
  # 파일마다 cp 를 띄우면 Windows 에서 몹시 느리다 — tar 한 번으로 같은 상대 경로에 푼다.
  printf '%s\n' "${!FILES[@]}" | LC_ALL=C sort | tar -cf - -T - | tar -xf - -C "$dest"
  echo "   복사한 게임 파일 ${#FILES[@]}개"
done
