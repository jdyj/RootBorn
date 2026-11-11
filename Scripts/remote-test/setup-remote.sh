#!/usr/bin/env bash
# 원격 장치 준비. 이 PC에서 `bash Scripts/remote-test/run.sh --setup`이 ssh 표준 입력으로 넘겨 원격에서 돈다.
# root 없이 원격 홈의 작업 폴더(인자 1, 홈 기준 상대 경로) 안에만 만든다. 여러 번 돌려도 된다.
#   repo.git      이 PC가 커밋을 밀어 넣는 bare 저장소
#   dotnet/       .NET 8 SDK (dotnet-install.sh)
#   x/            Xvfb와 xkb 자료 (apt-get download + dpkg -x)
#   nuget/        NuGet 패키지 캐시(실행끼리 함께 씀)
#   env.sh        위 위치와 한계 값. 이미 있으면 덮어쓰지 않는다.
set -euo pipefail

DIR_NAME="${1:?작업 폴더 이름(원격 홈 기준)을 인자로 준다}"
case "$DIR_NAME" in /*|*..*) echo "작업 폴더는 홈 아래 상대 경로여야 한다: $DIR_NAME" >&2; exit 2 ;; esac
BASE="$HOME/$DIR_NAME"
mkdir -p "$BASE"/{runs,locks,nuget,dl}

step() { echo "== $*"; }

step "bare 저장소"
if [ ! -d "$BASE/repo.git" ]; then git init --quiet --bare "$BASE/repo.git"; fi
# 실행마다 refs/runs/<id>로 밀어 넣고 끝나면 지운다. 가비지 수집은 git이 알아서 한다.
git -C "$BASE/repo.git" config gc.auto 256

step "dotnet SDK 8.0"
if [ ! -x "$BASE/dotnet/dotnet" ] || ! "$BASE/dotnet/dotnet" --list-sdks | grep -q '^8\.0\.'; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$BASE/dl/dotnet-install.sh"
  bash "$BASE/dl/dotnet-install.sh" --channel 8.0 --install-dir "$BASE/dotnet" --no-path
fi
"$BASE/dotnet/dotnet" --list-sdks

step "Xvfb (root 없이 풀어 둔다)"
if [ ! -x "$BASE/x/usr/bin/Xvfb" ]; then
  ( cd "$BASE/dl" && rm -f ./*.deb && apt-get download xvfb x11-xkb-utils xkb-data xdotool libxdo3 )
  for deb in "$BASE"/dl/*.deb; do dpkg -x "$deb" "$BASE/x"; done
fi
missing=$(LD_LIBRARY_PATH="$BASE/x/usr/lib/x86_64-linux-gnu" ldd "$BASE/x/usr/bin/Xvfb" | grep 'not found' || true)
if [ -n "$missing" ]; then echo "Xvfb에 빠진 라이브러리가 있다(사용자가 설치해야 한다):" >&2; echo "$missing" >&2; exit 1; fi

step "env.sh"
if [ ! -f "$BASE/env.sh" ]; then
  font=$(fc-list ':lang=ko' file | sed 's/: *$//' | grep -i 'NotoSansCJK-Regular' | head -1 || true)
  [ -n "$font" ] || font=$(fc-list ':lang=ko' file | sed 's/: *$//' | head -1 || true)
  cat > "$BASE/env.sh" <<ENV
# rootborn 원격 테스트 설정. Scripts/remote-test/remote-run.sh가 읽는다. 저장소 밖에 둔다.
export ROOTBORN_TEST_DOTNET="$BASE/dotnet/dotnet"
export ROOTBORN_TEST_XVFB="$BASE/x/usr/bin/Xvfb"
export ROOTBORN_TEST_XKBDIR="$BASE/x/usr/share/X11/xkb"
export ROOTBORN_TEST_XLIB="$BASE/x/usr/lib/x86_64-linux-gnu"
export ROOTBORN_FONT="$font"
# 화면 번호는 이 범위에서 빈 것을 고른다(이미 떠 있는 화면은 건드리지 않는다).
export ROOTBORN_TEST_DISPLAY_FIRST=140
export ROOTBORN_TEST_DISPLAY_LAST=199
# 동시에 도는 실행 수, 한 실행 안에서 동시에 도는 테스트 프로세스 수, 시작에 필요한 가용 메모리(GB).
export ROOTBORN_TEST_MAX_RUNS=2
export ROOTBORN_TEST_PARALLEL=2
export ROOTBORN_TEST_MIN_AVAILABLE_GB=8
ENV
fi
cat "$BASE/env.sh"
step "끝"
