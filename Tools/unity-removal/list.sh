#!/usr/bin/env bash
# Unity 제거 준비용 목록 내기. 아무것도 지우거나 옮기지 않는다 — 읽기만 하고 output/unity-removal/ 에 쓴다.
#   bash Tools/unity-removal/list.sh
# 근거는 작업 폴더의 파일이 아니라 커밋된 목록(HEAD)이다: `git ls-tree -r -l HEAD` 한 번으로 경로와 크기를 얻고,
# 파일마다 프로세스를 띄우지 않는다(Windows 에서 느리다). 그림 경로 읽기는 `git grep ... HEAD` 한 번.
# 설계와 해석은 docs/superpowers/specs/2026-11-12-unity-removal-plan.md.
#
# 분류 (표의 한 파일은 딱 한 분류에 든다. 위에서부터 먼저 맞는 것):
#   가   MonoGame 판이 실행 중에 읽는 것 — 배포 목록(Tools/publish/publish.sh --list, 클라이언트·서버) 그대로
#   나   테스트가 읽는 것 — game/tests 의 코드 아닌 고정 파일. 테스트가 직접 여는 Unity 씬은 없다
#        (학교 충돌 대조는 2026-11-12부터 School.unity 대신 고정 파일 school-structure-colliders.json 을 읽는다)
#   다   변환기(Tools/Rootborn.DataExport)가 원본으로 읽는 Unity 에셋 — Assets/Data, 문구 카탈로그,
#        실행용 PNG 의 .meta(스프라이트 자르는 표), 스크립트 .cs.meta(guid → 클래스 이름)
#   라   Unity 전용 — 스크립트, 씬, 프리팹, 설정, 나머지 .meta, ProjectSettings, Unity 패키지 정보
#   라'  그림·맵 원본 팩 중 게임이 읽지 않는 파일 (Unity 전용은 아니지만 Unity 프로젝트 안에서 온 것)
#   마   문서·도구·검사·에이전트·루트 파일 (아래 하위 분류)
#   바   남는 코드 — game/ 의 MonoGame 판, Core 소스, Tools 의 dotnet 프로젝트
set -euo pipefail
export LC_ALL=C

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
cd "$REPO"
OUT="${UNITY_REMOVAL_OUT:-$REPO/output/unity-removal}"
mkdir -p "$OUT"
rm -f "$OUT"/*.txt "$OUT"/*.tsv

REV="${UNITY_REMOVAL_REV:-HEAD}"

# 1. 커밋된 파일 전부: 크기 \t 경로 (제출 안 된 BRIEF.local.md 같은 것은 빠진다)
git -c core.quotepath=off ls-tree -r -l "$REV" | awk -F'\t' '{ split($1, a, / +/); print a[4] "\t" $2 }' > "$OUT/tree.tsv"

# 2. 실행 프로그램이 읽는 파일 목록: 배포 목록 그대로(publish.sh --list, 클라이언트와 서버). 규칙을 여기서 다시 만들지 않는다.
#    배포 목록은 작업 폴더를 읽는다 — 커밋 기준과 같으려면 추적 파일에 커밋 안 한 변경이 없어야 한다.
if [ "$REV" = HEAD ] && [ -n "$(git status --porcelain --untracked-files=no)" ]; then
  echo "경고: 추적 파일에 커밋 안 한 변경이 있다. 실행 목록(작업 폴더)과 분류(HEAD)가 어긋날 수 있다." >&2
fi
{ bash "$REPO/Tools/publish/publish.sh" --list; bash "$REPO/Tools/publish/publish.sh" --server --list; } 2>"$OUT/runtime-warnings.txt" |
  LC_ALL=C sort -u > "$OUT/.cand"
awk -F'	' -v out="$OUT" '
  FNR == NR { tree[$2] = 1; next }
  { if ($0 in tree) print $0; else print $0 > (out "/runtime-missing.txt") }
' "$OUT/tree.tsv" "$OUT/.cand" > "$OUT/runtime-files.txt"
touch "$OUT/runtime-missing.txt"
rm -f "$OUT/.cand"

# 3. 분류
awk -F'\t' -v out="$OUT" '
  function top(p,   n, a) { n = split(p, a, "/"); if (n == 1) return "(루트 파일)"; if (n == 2) return a[1] "/(직속 파일)"; return a[1] "/" a[2] }
  function classify(p,   base) {
    if (p in rt) return "가  실행 중에 읽음 (content.txt)"
    if (p ~ /^game\/tests\// && p !~ /\.(cs|csproj)$/) return "나  테스트 고정 파일 (game/tests)"
    if (p ~ /^Assets\/Data\//) return "다  변환기 원본: Assets/Data"
    if (p ~ /^Assets\/Localization\/GameTextCatalog\.asset/) return "다  변환기 원본: 문구 카탈로그"
    if (p ~ /\.cs\.meta$/ && (p ~ /^Assets\/Scripts\// || p ~ /^Packages\/com\.rootborn\.core\//)) return "다  변환기 원본: 스크립트 .cs.meta (guid → 클래스 이름)"
    if (p ~ /\.png\.meta$/) { base = substr(p, 1, length(p) - 5); if (base in rt) return "다  변환기 원본: 실행용 PNG 의 .meta (스프라이트 자르는 표)" }
    if (p ~ /^Packages\/com\.rootborn\.core\/Runtime\/.*\.cs$/) return "바  남는 코드: Core 소스 (Packages 에서 옮길 것)"
    if (p ~ /^(Packages|ProjectSettings)\//) return "라  Unity 전용: Packages·ProjectSettings"
    if (p ~ /^Assets\/[^\/]*$/ || p ~ /^Assets\/(Scripts|Tests|Scenes|Prefabs|Settings|AddressableAssetsData|Plugins|Resources|Linker|_Recovery|TutorialInfo|Tiles|TilePalettes|ModernInteriors|Localization)\//) return "라  Unity 전용: Assets 의 코드·씬·설정·에셋"
    if (p ~ /^Assets\/.*\.(meta|asset|ldtkc|prefab|unity|cs|asmdef|shader|mat|controller|anim|overrideController|inputactions|physicsMaterial2D|spriteatlas|mixer|playable|lighting|guiskin|fontsettings)$/) return "라  Unity 전용: 그림·맵 팩 안의 .meta·생성 에셋·프리팹"
    if (p ~ /^Assets\//) return "라\x27 그림·맵 원본 팩 중 게임이 읽지 않는 파일"
    if (p ~ /^(game|Tools\/Rootborn\.)/) return "바  남는 코드: game/ 와 Tools/Rootborn.* (dotnet)"
    if (p ~ /^Scripts\/(fevm|qa|unity|validation)\// || p ~ /^Scripts\/ci\/(check-addressables-linkxml-preserved\.ps1|run-multiplayer-validation-gate\.ps1|report-playmode-bypasses\.py|check-meta-files\.sh|tests\/test-meta-files\.sh)$/) return "마  Unity 를 가리키는 스크립트·검사"
    if (p ~ /^(Scripts|\.github)\//) return "마  CI·검사·git 훅 (남김)"
    if (p ~ /^Tools\//) return "마  도구 (Tools/publish, Tools/unity-removal)"
    if (p ~ /^(\.tmp-|Builds\/)/) return "마  찌꺼기: 루트 .tmp-*.json, Builds/Logs 스크린샷"
    if (p ~ /^\.claude\/agents\//) return "마  .claude/agents"
    if (p ~ /^\.claude\/skills\//) return "마  .claude/skills"
    if (p ~ /^\.claude\/rules\//) return "마  .claude/rules"
    if (p ~ /^(\.claude|\.agents)\//) return "마  .claude·.agents 기타"
    if (p ~ /^(docs|production)\//) return "마  문서 (docs, production)"
    return "마  루트 파일"
  }
  FNR == NR { rt[$0] = 1; next }   # 첫 입력: runtime-files.txt
  {
    size = $1 + 0; p = $2
    c = classify(p)
    n[c]++; b[c] += size
    tn++; tb += size
    # 폴더별 (분류 앞 한 글자 + 둘째 단계 폴더)
    match(c, /^[^ ]+/); k = substr(c, RSTART, RLENGTH)
    key = k "\t" top(p)
    fn[key]++; fb[key] += size
    code = k
    if (code == "라") print p > (out "/delete-unity-only.txt")
    else if (code == "다") print p > (out "/delete-after-data-is-source.txt")
    else if (code == "라\x27") print p > (out "/art-pack-unread.txt")
    else if (code == "나") print p > (out "/tests-read.txt")
    else if (c ~ /^마  Unity 를/ || c ~ /^마  찌꺼기/) print p > (out "/unity-pointing-leftovers.txt")
  }
  END {
    for (c in n) printf "%s\t%d\t%d\t%.1f\n", c, n[c], b[c], b[c] / 1048576 | "sort > \"" out "/.summary.body\""
    close("sort > \"" out "/.summary.body\"")
    printf "합계\t%d\t%d\t%.1f\n", tn, tb, tb / 1048576 > (out "/.summary.total")
    for (key in fn) { split(key, kk, "\t"); printf "%s\t%s\t%d\t%d\t%.1f\n", kk[1], kk[2], fn[key], fb[key], fb[key] / 1048576 | "sort -t\"\t\" -k1,1 -k4,4nr > \"" out "/.folder.body\"" }
    close("sort -t\"\t\" -k1,1 -k4,4nr > \"" out "/.folder.body\"")
  }
' "$OUT/runtime-files.txt" "$OUT/tree.tsv"
{ printf '분류\t파일 수\t바이트\tMB\n'; cat "$OUT/.summary.body" "$OUT/.summary.total"; } > "$OUT/summary.tsv"
{ printf '분류\t폴더\t파일 수\t바이트\tMB\n'; cat "$OUT/.folder.body"; } > "$OUT/by-folder.tsv"
rm -f "$OUT"/.summary.* "$OUT"/.folder.body
for f in delete-unity-only delete-after-data-is-source art-pack-unread tests-read unity-pointing-leftovers; do
  touch "$OUT/$f.txt"; sort -o "$OUT/$f.txt" "$OUT/$f.txt"
done

# 4. 요약 출력
echo "== 분류별 (커밋 기준 $REV) =="; cat "$OUT/summary.tsv"
echo
echo "실행 중에 읽는 파일 $(wc -l < "$OUT/runtime-files.txt")개, 목록에 있는데 저장소에 없는 것 $(wc -l < "$OUT/runtime-missing.txt")개"
echo "지워도 되는 Unity 전용 파일 목록: $OUT/delete-unity-only.txt ($(wc -l < "$OUT/delete-unity-only.txt")줄)"
echo "나머지 목록: $OUT/{summary,by-folder}.tsv, delete-after-data-is-source.txt, art-pack-unread.txt, tests-read.txt, unity-pointing-leftovers.txt, runtime-files.txt, runtime-missing.txt, runtime-warnings.txt"
