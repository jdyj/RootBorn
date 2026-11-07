# FEVM(Strix Halo) Unity 검증 스크립트

2026-11-08부터 ROOTBORN의 Unity 컴파일·EditMode·PlayMode는 여기서 돌린다(사용자 결정, `.claude/rules/unity-cli.md`). Windows PC 메모리를 쓰지 않고 커밋 기준으로 검증한다. 절차와 판정 규칙은 `.claude/rules/strix-verification.md`가 우선이다.

## 한 번 준비된 것 (FEVM, sudo 없이 사용자 영역)
- 에디터 `~/Unity/Hub/Editor/6000.3.13f1` (Linux, Windows 빌드 모듈 포함), Unity Personal 라이선스
- Xvfb·xdotool `~/farmer-unity-verify/tools/root/x`, VirtualGL `~/farmer-unity-verify/tools/root/vgl`

## 사용
1. 커밋한다. 미커밋 수정은 검증에 들어가지 않는다.
2. Windows Git Bash: `Scripts/fevm/sync.sh [커밋]`
   - 번들을 만들어 올리고 `~/farmer-unity-verify/<짧은 해시>`에 체크아웃한 뒤 해시를 다시 확인한다.
   - 새 사본은 처음부터 임포트한다(약 10분). `RB_SEED_LIBRARY=1`이면 쓰지 않는 최근 Library를 복사하지만, 복사본에서 FangAutoTile 에셋이 안 읽혀 가짜 실패가 난 적이 있어 기본은 끈다.
   - `test-fixtures.txt`의 `output/` 고정 증거(git 밖)를 사본에 복사한다. 원본은 고치지 않는다.
3. FEVM: `~/farmer-unity-verify/tools/rb-test.sh <폴더> EditMode|PlayMode [필터] [asset|built|rebuild]`
   - EditMode·PlayMode 모두 Xvfb(`:97`, 1920x1080, `-noreset`) + VirtualGL(`egl0`, `-force-glcore`) GPU 렌더링. `-nographics` 첫 임포트는 8192px 넘는 텍스처를 실패시킨다(`RB_NOGRAPHICS=1`로만 강제).
   - 네 번째 인자는 `ROOTBORN_AA_PLAYMODE`로 넘어간다. `rebuild`는 Addressables를 먼저 빌드하고 빌드본을 읽는다.
   - 결과: `<폴더>/rb-results/<플랫폼>-<시각>.xml|.log`, 요약(통과·실패 이름·컴파일 오류)을 출력한다.
   - 결과 저장 뒤 Unity가 MCP 플러그인 종료에서 멈추면 90초 뒤 감시가 종료한다. 실행 뒤 바뀐 파일을 `git status`로 보여 준다.
   - `-buildTarget StandaloneWindows64`로 열어 Linux 타깃 전환을 피한다. 그래도 바뀐 `ProjectSettings`나 `.meta`는 Windows로 가져오지 않는다.

## 주의
- 같은 폴더에서 Unity를 둘 이상 띄우지 않는다(스크립트가 막는다).
- ComfyUI(8188), 다른 세션의 llama-server, 귀혼 VM은 건드리지 않는다. 시작 전에 `free -g`를 본다.
- Linux 통과를 Windows 화면·빌드 확인으로 보고하지 않는다.

## 기준선 (d70d74345, 2026-11-08)
- EditMode GPU: 2090건 중 2029 통과, 59 실패. Windows 대비 FEVM만 실패 11건 = `output/` 증거 9건(고정 증거 복사로 해결 대상) + `DailyEventPresentationTests` 라벨 높이 2건(글꼴 차이 추정, 미확인).

## AI Game Developer MCP (FEVM 편집기)
- FEVM: `~/farmer-unity-verify/tools/rb-editor.sh <사본> start|stop|status`
  - 창이 있는 편집기를 Xvfb `:99` + GPU로 띄운다.
  - PC와 같은 연결 설정(Custom, streamableHttp, 22394, 인증 없음)을 `UserSettings/AI-Game-Developer-Config.json`에 넣는다.
- PC: `Scripts/fevm/mcp-tunnel.sh`를 켜 둔다(PC 22395 → FEVM 22394).
  - `.mcp.json`의 `ai-game-developer-fevm`으로 붙는다. 새 MCP 항목은 Claude Code를 다시 시작하거나 `/mcp`에서 다시 연결해야 보인다.
- PC 편집기용 `ai-game-developer`(22394)는 그대로 둔다.
- FEVM 사본에서 바꾼 장면·에셋은 `git diff` 패치로 가져와 PC 저장소에 적용하고 커밋한다.
  - Linux가 바꾼 ProjectSettings와 .meta는 가져오지 않는다.
- 같은 사본에서 편집기와 배치 테스트를 동시에 돌리지 않는다.
