# 규칙 변경 이력

## 2026-11-13 — 균형 시뮬레이터 돌리는 절차를 스킬로
- 요청: "일단 시뮬레이터 기반을 만들어서 계속 수정하면서 돌릴거라 잘 돌아갈 수 있게 스킬로 만들어둬"
- 배경: 균형 시뮬레이터를 고치고 원격에서 돌려 표를 받는 순서(커밋 → 빠른 검사 → 훑기 → 이어 받기 → 앞 실행과 견주기)와 걸려 넘어진 곳(이 PC에서 돌리면 꺼짐, 배경 대기가 꺼짐, 0을 버는 전략, 거절될 일을 시작)이 세션 기억에만 있었다. 덮어쓰기 폴더는 원격에서 쓸 길이 없었다(절대 경로만 받았다).
- 변경: 스킬 `balance-sim-run`(`.claude/skills`, `.agents/skills`) 신규. `engine-migration.md` 스킬 목록에 한 줄. 덮어쓰기 폴더의 상대 경로를 저장소 뿌리 기준으로 읽게 하고 자리(`game/tests/Rootborn.Balance.Tests/overlays/`)를 정함.
- 일반화: 되풀이해 돌릴 도구는 만든 세션이 끝나기 전에 돌리는 순서와 함정을 스킬로 남긴다. 원격에서만 도는 도구는 입력(설정·덮어쓰기)도 커밋된 것으로 줄 수 있어야 한다.

## 2026-11-13 — 상위호환은 "옛 빌드에서도 플레이된다"
- 요청: "옛 빌드에서 플레이까지 되어야 한다는 뜻이지 당연히"
- 배경: 전날 만든 스킬은 상위호환을 "옛 프로그램이 새 세이브를 읽지 않고 덮어쓰지 않는다"로 적었고, 저장소도 더 높은 형식 버전을 거절했다.
- 변경: 저장소가 더 높은 버전의 파일을 읽고, 쓸 때 모르는 필드를 그대로 다시 쓴다. 거절은 파일이 `ReadableFrom`으로 요구할 때만. `data-driven.md` "세이브" 두 줄, 스킬 `save-compat-change`(양쪽)를 그 뜻으로 다시 씀.
- 일반화: 사용자가 쓴 말(상위호환)의 뜻을 좁게 정해 놓고 진행하지 않는다. 뜻이 둘로 갈리면 만들기 전에 어느 쪽인지 확인한다.

## 2026-11-12 — 테스트 프로젝트 표에 수치 균형 시뮬레이터
- 요청: "수치 균형을 재는 시뮬레이터도 필요할거 같은데 설계해줘", "시뮬레이션 하는건 strix halo에서 진행해"
- 배경: 직업(시설)과 알바를 계속 돌렸을 때 돈이 얼마나 벌리는지 견줄 표가 없었다. 새 테스트 프로젝트 `game/tests/Rootborn.Balance.Tests`가 `GameSession` 명령으로 여러 날을 돌려 `output/balance/`에 표를 낸다. 전체 보고는 원격 장치에서 돌리고 결과 폴더로 받는다.
- 변경: `testing-discipline.md` "어디에 무엇을" 표에 Balance 한 줄(근거 종류 "자동 테스트(실제 플레이 아님)").
- 일반화: 가정을 넣은 계산(균형 표)은 실제 플레이 근거가 아니다. 표의 숫자를 보고할 때 가정과 함께 "자동 테스트"로 적는다.

## 2026-11-12 — 세이브 하위호환·상위호환 절차를 스킬로
- 요청: "스킬로도 만들어놔 데이터 더 수정 필요한 상황일 때 하위호환, 상위호환 무조건 되어야한다고"
- 변경: 스킬 `save-compat-change`(`.claude/skills`, `.agents/skills`) 신규. `data-driven.md` "세이브"와 `engine-migration.md` 스킬 목록에 한 줄씩.
- 일반화: 세이브에 닿는 변경은 두 약속(옛 세이브가 열리고 진행이 남는다 / 새 세이브를 옛 프로그램이 상하게 하지 않는다)을 테스트로 확인한 뒤에만 끝났다고 한다. 하나를 포기해야 하면 먼저 묻는다.

## 2026-11-12 — 세이브 무결성: 고정 폴더 고치지 않기, 빠진 기록 버리지 않기
- 요청: "데이터 저장하는 부분에서 확장성 있는지, 만약 추가로 업데이트 되었을 때 깨지진 않을지 … 제대로 확인 및 조사하고 위험한 부분 확인해"
- 배경: 세션을 나눠 개발하는 동안 세이브를 조사했다. 저장 자체(바꿔치기, 묶음)는 튼튼했지만, 데이터·맵을 고친 뒤 옛 세이브가 안 열리거나 진행이 조용히 사라지는 길이 있었고(데이터에 없는 아이템이 든 슬롯 거절, 빠진 퀘스트 기록 삭제, 사라진 자리에서 시작 거절), 새 세이브 파일의 등록은 손 목록에만 기대고 있었다.
- 변경: `data-driven.md` "세이브"에 고정 폴더를 고치지 않고 더하기, 새 파일은 `Save()` 묶음에, 데이터에 없는 id의 기록 보존, 숫자 enum 값 고정, 함께 쓰는 파일 요구 목록의 조건.
- 일반화: 세이브의 "기존 것이 계속 읽힌다"는 약속은 사람이 기억하는 목록이 아니라 실제로 쓴 파일과 그날의 고정 파일로 확인한다. 읽을 수 없거나 모르는 것은 버리지도 덮지도 않고 그대로 둔다.

## 2026-11-12 — Unity를 저장소에서 지움, data/가 원본, 테스트는 원격에서도
- 요청: "준비되면 Unity는 바로 지우면 돼", "이 테스트를 여기서 하지 말고 strix halo에서 하면 안 돼?", "다음부턴 이어서 하도록 해 줘"
- 배경: Unity 전용 파일과 변환기, 안 읽는 그림 팩을 지웠다(추적 파일 12만 개 → 4,600개). 이 PC의 메모리가 모자라 전체 테스트가 여러 번 꺼졌다.
- 변경: `engine-migration.md` "지금 상태"(Unity 판은 지웠다, `data/`가 원본이고 손으로 고친다, 남은 것), `localization.md`·`coding-standards.md`·`AGENTS.md`·`constitution.md`의 "얼렸다"·"변환기" 문장, `testing-discipline.md`에 원격 실행과 이어 받기, `ui-standards.md` 키 표에 L·F1·F11.
- 일반화: 규칙 문서의 "지금 상태"는 큰 합치기마다 지금 사실로 고친다. 지운 것의 문장을 남겨 두면 다음 세션이 없는 도구(변환기)를 찾는다.

## 2026-11-12 — 헌법과 AGENTS.md를 MonoGame 판 기준으로 다시 씀, Unity 규칙 삭제
- 요청: "규칙문서 직접 고쳐도돼 unity 관련된거 지워"
- 배경: 게임이 MonoGame 판으로 옮겨져 메인 체인이 실제 입력으로 통과했다. `AGENTS.md`와 `constitution.md`에는 Unity 기준 조항(SO 와이어링, PlayMode, Addressables, Style2 좌표, NGO, Unity 빌드)이 남아 있었다.
- 변경: `AGENTS.md`와 `constitution.md`를 지금 구조 기준으로 다시 씀. `rules/strix-verification.md` 삭제(Unity 판 검증 절차). 그 밖의 `rules/*.md`는 같은 날 먼저 정리됨(`path-based/` 삭제, `data-driven.md` 신규).
- 같은 날 사용자 지시로 더한 규칙: 그림과 맵은 갈아 끼울 수 있게(`engine-migration.md`), 다국어(`localization.md`).
- 일반화: 규칙 문서는 지금 코드의 사실만 담는다. 옮겨 간 뒤의 낡은 조항은 다음 세션이 낡은 전제를 따르게 하므로 그때 지운다. 아래 이력은 Unity 판과 그 전 프로젝트 시절의 기록이라 지금 코드와 맞지 않는 것이 많다.

## 2026-11-11 — Unity 판 동결, Unity 도구·규칙 삭제
- 요청: "MonoGame으로 가자", "1번 + 2번으로 가자", "unity 관련된걸 하나하나 끝내면 문서나 메모리 등 남기지 않도록 진행해", "추천대로 계속 진행해"
- 배경: 게임을 Unity에서 MonoGame으로 옮긴다. 속도를 위해 Unity 판을 태그 `unity-playable-20261111`로 얼리고 규칙은 `Rootborn.Core`에 바로 새로 쓴다.
- 변경: `rules/engine-migration.md` 신규. `rules/unity-cli.md`, Unity MCP 스킬(`.claude/skills` 93개, `.agents/skills` 전체), Unity 전용 에이전트 6개, `.mcp.json` 삭제. AGENTS.md 1번 삭제. `rules/strix-verification.md`와 `Scripts/fevm`은 기준본 화면 비교가 끝날 때까지만 둔다.
- 일반화: 끝난 부분의 규칙·도구·메모리는 그때 지운다. 남겨 두면 다음 세션이 낡은 전제를 따른다.

## 2026-11-09 — 새 .cs 직접 작성 허용을 AGENTS.md 1번에 반영
- 요청: "새 .cs를 왜 Unity를 거쳐 만들어? 이러면 더 유니티에 결합되는거 아니야?"(11-08), "승인할게 수정하고 진행해봐"(11-09)
- 배경: `unity-cli.md`는 11-08에 고쳤지만 AGENTS.md 1번에는 'MCP·편집기 경유 의무'가 남아 두 문서가 어긋났다. `unity-cli.md`의 MCP 편집기 사본 갱신 절차는 열린 편집기 밑에서 체크아웃하는 것이라 같은 위험이 있었다.
- 변경: AGENTS.md 1번 문구 교체, `unity-cli.md` MCP 사본 갱신은 편집기를 닫고 체크아웃한 뒤 다시 띄우도록 수정.
- 일반화: 금지 대상은 '직접 쓰기'가 아니라 '열린 편집기 밑에서 소스가 바뀌는 것'이다. 커밋 체크아웃도 여기에 포함된다.

## 2026-11-07 — 엔진 무관 게임 규칙, 실제 플레이 근거 보고
- 요청: "권하는 쪽으로 진행해보자", "헌법에도 적어놔 codex, claude", "직접 플레이를 안해놓고 한 척하고 있어서 짜증나서 계속 얘기한거"
- 배경: Unity 에디터 테스트가 수 분과 4~10GB 메모리를 쓰고, 에디터가 열린 채 파일을 고치면 테스트가 깨진다. 내부 호출·값 직접 대입으로 확인한 것을 실제 플레이 확인처럼 보고한 일이 반복됐다.
- 변경: `rules/core-logic-and-play-evidence.md` 신규, `AGENTS.md` 13번과 Rules Reference, `constitution.md` 4-3절 추가.
- 일반화: 화면과 무관한 규칙은 엔진 밖에서 빠르게 검증한다. 완료 보고는 근거의 종류(실제 입력, 스크린샷, 자동 테스트, 내부 호출)를 나눠 쓰고, 실제 입력 근거가 없는 항목은 확인했다고 쓰지 않는다.

## 2026-11-05 — Strix Halo 커밋 기준 검증 절차
- 요청: "어떻게 진행되는건지 claude, codex 헌법에 넣어주고", "그냥 git 가져와서 해당 버전으로 검증", "ai를 사용했으면 좋겠는데 … 토큰도 아껴야할 필요가 있는데"
- 배경: 복사한 사본이 Windows의 후속 수정과 어긋났고, 미커밋 작업은 git 기준 검증에 포함되지 않는다. 검증 AI 검토에서는 토큰 비용과 FEVM 로컬 비전 모델을 고려하지 않았다.
- 변경: `rules/strix-verification.md` 신규, `AGENTS.md` 12번과 Rules Reference, `constitution.md` 4-2절 추가.
- 일반화: 검증 대상은 해시로 고정하고, 판정은 스크립트 → 로컬 모델 → 상위 모델 → 사용자 순으로 필요한 만큼만 올린다. 로컬 모델은 보정 전이나 단독으로 필수 항목을 완료 처리하지 못한다.

## 2026-05-16 - Runtime-only tilemap verification false completion
- Feedback: "전혀 안됐는데 어딜 했다고하는거야?"
- Cause: In the House wall tile task, the agent verified only the PlayMode runtime-generated result and reported completion before checking the saved `Assets/Scenes/House.unity` EditMode Tilemap. The saved scene still had `tile_r02_c09` only on the original two window cells, so the user's visible scene/palette had not actually changed.
- Change: Added a Direct Visual Play Verification Gate rule in `AGENTS.md`: when a visual change must persist in a scene, prefab, ScriptableObject, or palette asset, runtime-only verification is insufficient. The saved EditMode asset/scene state must be separately verified, including exact Tilemap tile names/counts or serialized asset references.
- Generalization: Before reporting tilemap/palette/scene work as complete, verify and distinguish (1) runtime Game View or Camera output, (2) saved scene/asset serialized state, and (3) scene/asset reopen or re-inspection when persistence matters. Never claim a user-visible saved change from runtime generated state alone.

---

`feedback-constitution-update` 스킬에 의한 헌법 및 규칙 문서 변경 이력.

형식:
```
## YYYY-MM-DD — {요약}
- 피드백: "{사용자 피드백 원문 또는 요지}"
- 원인: {왜 AI가 잘못했는지}
- 변경: {어느 파일 어떤 섹션이 어떻게 수정됐는지}
- 일반화: {오버피팅 방지를 위해 원리 수준에서 어떻게 정리했는지}
```

---

## 2026-05-13 — NPC 대화창 Style2 9-slice recipe (r14_c3~r16_c5)
- 피드백: "r14_c3 ~ r14_c5  r15 r16 둘다 c3~5 를 가지고 npc 대화창 기본 sprite를 만들고싶어"
- 원인/배경: NPC 대화창은 `LocationNpcRuntimeInstaller.EnsureDialoguePanel`이 만든 빈 panel 위에 `Image.color`만 어두운 단색으로 깔고 있었다. 헌법 `rules/ui-standards.md` "Modern UI Style2 공통 패널" 절은 크기 가변 패널은 `Image.sprite` 단일 대신 `ModernUiTileImage` 3x3 tiled 빌더를 써야 한다고 강제하지만, 대화창은 이 규약 밖에 있었다. Style2 sheet의 r14_c3~r16_c5 (시각 검증: 둥근 모서리 + 어두운 outline + 흰 fill의 9-slice speech panel) 좌표가 비어있어 dialogue 전용 9-slice로 사용 가능.
- 변경:
  - `Assets/Scripts/Game/Common/ModernUiStyle2Sprites.cs`에 `DialoguePanel` 정적 클래스 (9칸 9-slice 카탈로그) 추가, `PreloadSubSprites` 배열에 9개 sub-sprite 이름 추가.
  - `Assets/Scripts/UI/Modern/ModernUiRecipes.cs`에 `ModernUiRecipes.DialoguePanel` recipe 1줄 등록.
  - `Assets/Scripts/UI/Quests/DialoguePanel.cs`에 `EnsureBackground()` 메서드 추가 — `ModernUiTileImage` 자식을 부모 전체로 stretch한 뒤 `SetRecipe(ModernUiRecipes.DialoguePanel)` + `Rebuild()`. 기존 root `Image` 색은 투명화하여 9-slice를 가리지 않도록 처리. 호출은 `Open()` 진입부.
  - `Assets/Tests/EditMode/UI/Modern/DialoguePanelRecipeTests.cs` 신규 4 케이스 — 9칸 (row,col) 매칭, 9개 role 존재, PreloadSubSprites 포함, recipe registry 동등성. 신규 4개 모두 통과.
- 일반화: "신규 9-slice 패널 추가 = (1) Style2 카탈로그 정적 클래스 + Tile() 헬퍼 (2) ModernUiRecipes에 ModernUiTileRecipe 등록 (3) PreloadSubSprites 9개 이름 추가 (4) 일관성 EditMode 테스트 4축(좌표 범위/role 9개/preload 포함/registry 동등). PlayMode `TownStyle2SpriteExhaustivePlayModeTests`가 AllSheets 기준 전수 검증을 이미 수행하므로 별도 PlayMode 추가 불필요."

---

## 2026-05-12 — 성능·최적화 동시 설계 원칙 헌법화
- 피드백: "최적화도 같이 생각하면서 진행하는걸로 헌법에도 추가해주고 프롬프트도 같이 작성해줘"
- 원인: 도감, 마일스톤, 퀘스트, 인벤토리처럼 항목 수가 늘어나는 시스템은 기능 구현 뒤에 최적화를 붙이면 UI 구조와 저장/조회 구조를 다시 갈아엎을 위험이 크다.
- 변경:
  - `AGENTS.md` Constitutional Rules에 "성능·최적화 동시 설계 원칙" 추가.
  - `.claude/constitution.md` 핵심 원칙에 성능 예산, 대량 목록 UI, dirty 갱신, 캐싱, Addressables 중복 로드 방지 원칙 추가.
  - `.claude/rules/game-design.md`에 금지/필수 성능 설계 규칙 추가.
- 일반화: "신규 시스템은 기획·데이터 구조·UI 목업·테스트 단계부터 성능과 확장 비용을 함께 고려한다. 대량 목록 UI는 가상화/페이징/캐싱/지연 로딩/dirty 갱신을 우선 검토하고, 완료 보고에는 성능 리스크와 검증 범위를 포함한다."

---

## 2026-05-11 — 플레이어 대리 테스트 의무 헌법화
- 피드백: "모든 테스트 시나리오를 유저가 플레이하듯 모든 상호작용 테스트와 함께 했어?" 이후 "실제 플레이 방식으로 테스트하도록 헌법에도 수정하고 테스트진행해"
- 원인: 기존 규칙은 사용자가 구체적 조작 경로를 말한 경우의 우회 금지를 다뤘지만, 모든 유저 여정/코어 루프/PlayMode 시나리오의 완료 판정이 실제 입력·UI·트리거 기반이어야 한다는 기본 원칙이 약했다.
- 변경:
  - `AGENTS.md` Constitutional Rules 에 "플레이어 대리 테스트"를 추가.
  - `constitution.md` 테스트 원칙에 저장 슬롯 선택, UI 클릭, 입력, 이동, 트리거, 상호작용, 씬 전환 관찰을 실제 플레이 경로로 검증해야 한다고 명시.
  - `rules/testing-discipline.md` 절대 원칙에 모든 유저 여정/코어 루프/PlayMode 시나리오의 완료 판정은 실제 플레이 경로 테스트로 한다는 조항 추가.
- 일반화: "시나리오 테스트는 결과 상태만 확인하지 않는다. 플레이어가 실제로 통과해야 하는 조작 경로 자체가 검증 대상이다."

---

## 2026-05-10 — 사용자 지정 플레이 흐름 우회 금지 헌법화
- 피드백: "방금도 내가 의사소통이 잘 안됐던거같은데 좀 정제해서 헌법에 넣어줄수있어?"
- 원인: 사용자는 "직접 포탈로 가서 Farm으로 씬 변경"을 요구했지만, 검증 과정에서 포탈 내부 메서드를 직접 호출해 테스트를 통과시킨 적이 있었다. 이는 상태 보존 일부만 확인했을 뿐 실제 사용자 조작 경로를 증명하지 못했다.
- 변경:
  - `AGENTS.md` Constitutional Rules 에 "사용자 지정 플레이 흐름 우회 금지" 추가.
  - `constitution.md` 테스트 원칙에 사용자 재현 순서와 조작 경로를 실제 PlayMode/입력/트리거/UI 경로로 검증해야 한다는 항목 추가.
  - `rules/testing-discipline.md` 절대 원칙에 내부 메서드 호출, 상태 강제 세팅, 씬 강제 로드 등 우회 검증 금지 추가.
- 일반화: "사용자가 플레이 흐름을 지정하면 테스트는 결과 상태만이 아니라 사용자가 지정한 조작 경로 자체를 검증 대상으로 삼는다. 자동화가 어렵다면 우회하지 말고 한계를 먼저 알린 뒤 관찰 가능한 사용자 경로 테스트를 만든다."

---

## 2026-05-09 — Modern UI Style2 공통 패널 좌표 헌법화
- 피드백: "이제 공통 패널은 저렇게 하는걸로 헌법이나 메모리에 넣어줘"
- 원인: Style1과 Style2의 패널 3x3 블록 좌표가 달라, Style2에서 `r0/r1/r2` 블록을 공통 패널로 쓰면 의도한 프레임이 아니라 다른 UI 조각이 들어갈 수 있었다.
- 변경:
  - `constitution.md` 그래픽 자산 섹션에 "Modern UI Style2 공통 패널" 원칙 추가.
  - `AGENTS.md` Constitutional Rules 에 "Modern UI Style2 공통 패널 규약" 추가.
  - `rules/ui-standards.md` 에 Style2 공통 패널 3x3 좌표와 `r3_c1` fill 반복 규칙 명시.
- 일반화: "공통 UI 패널은 source sheet별 좌표 차이를 헌법 문서에 고정하고, 크기 가변 패널은 단일 Image sprite가 아니라 3x3 tiled builder로 생성한다."

---

## 2026-05-09 — ROOTBORN 헌법 Town-first 전환
- 피드백: "지금 헌법에 Farm 관련한거 어떻게 되어있어? 지금 내가 생각하기로는 세대의 느낌보다는 일단 도시느낌으로 가는게 좋을거같아서" 이후 "수정해줘"
- 원인: `constitution.md`와 AGENTS 요약은 여전히 "세대 진화 농장 생존 게임"을 최상위 비전으로 두고 있었지만, 최근 town concept 전환 문서와 구현은 이미 도시 생활, 학생 활동, 특성·기술·진로 성장 중심으로 이동했다. 이 불일치가 신규 기능 판단 기준을 흔들 수 있었다.
- 변경:
  - `constitution.md` 게임 비전, 핵심 메카닉, MVP, 데이터-드리븐 대상, SO 경로를 Town/StudentLife/LifeActivity 중심으로 수정.
  - `rules/testing-discipline.md` 코어 시나리오 카탈로그를 TOWN/LIFE/CAREER/REL/JOB 중심으로 갱신하고 GEN/HEIR/CROP은 레거시 격리 시나리오로 이동.
  - `rules/path-based/assets-data.md` 데이터-드리븐 적용 대상을 생활 활동, 선택지, 기술, 진로, 관계, 직무, 장소로 확장하고 레거시 작물/세대는 유지 가능 대상으로 명시.
  - `AGENTS.md`와 `.claude/README.md`의 헌법 요약, 테스트 prefix, 데이터 경로 설명을 Town-first 기준으로 동기화.
- 일반화: "상위 비전이 바뀌면 헌법, 테스트 카탈로그, 데이터 와이어링 규칙, 에이전트 요약을 함께 갱신한다. 기존 시스템은 삭제 전까지 레거시 회귀 테스트로 보호하되, 신규 기본 루프의 판단 기준으로 승격하지 않는다."

---

## 2026-05-06 — 보상 수령 전 인벤토리 검증과 멱등성 헌법화
- 피드백: "기본적으로 보상받을 때 인벤토리 있는진 검증은 해야해 아이템이 복사/삭제 되지 않도록 이건 헌법에도 추가해줘"
- 원인: 퀘스트 시스템 설계에서 보상 지급은 핵심 상태 전이지만, 기존 헌법에는 인벤토리 수용 가능성 preflight, 부분 지급 방지, 저장/로드 후 재지급 방지 같은 일반 원칙이 명시되어 있지 않았다.
- 변경:
  - `AGENTS.md` Constitutional Rules 에 "보상·인벤토리 트랜잭션 원칙" 추가.
  - `rules/testing-discipline.md` 절대 원칙과 시나리오 추가 의무에 보상 지급 멱등성/인벤토리 검증 테스트 의무 추가.
  - `rules/path-based/assets-data.md` 에 보상 데이터와 인벤토리 안전성 섹션 추가.
- 일반화: "인벤토리를 변경하는 모든 보상 경로는 지급 전 수용 가능 여부와 중복 수령 여부를 검증하고, 실패 시 무변경, 성공 시 원자적 지급, 반복 호출 시 멱등이어야 한다."

---

## 2026-05-06 — 도구 액션 (도끼-나무 / 곡괭이-돌) 매칭 완성 + Control 키 바인딩 + TDD
- 피드백: "지금 특정 키(control, 마우스 좌클릭)등 을 눌렀을 때 도끼를 들고있다면 나무를 벨 수 있고 곡괭이를 들고있다면 돌을 캘 수 있고 등의 액션을 구현하도록 해줘 기존 시나리오 테스트는 다 성공해야함"
- 원인:
  - (A) **도구-자원 매칭 부분 작동**: `ResourceNodeDefinition.ComputeEffectivePower(usedTool)` 와 `_preferredTool` 필드가 이미 있고 `Resource_Tree._preferredTool: Tool_StoneAxe` 와이어링됨 (도끼=1.2x, 다른 도구=0.6x, 맨손=0.3x). 그러나 **Resource_Rock._preferredTool: {fileID: 0}** 비어있어 모든 도구가 돌을 풀파워로 깸 — "곡괭이만 잘 깬다" 로직이 데이터 부재로 무력화. **Tool_StonePickaxe.asset / Item_Tool_StonePickaxe.asset 자체가 부재**.
  - (B) **마우스 좌클릭 휘두르기 흐름은 이미 동작**: `PlayerController.OnAttackPerformed` 가 좌클릭 시 휘두르기 애니메이션 시작 → 마지막 프레임에 `_interactor.TriggerInteract()` → `GatherInteractor.DoInteract()` → `node.Hit(_equippedTool)`. 그러나 **Control 키는 미바인딩** — 사용자 요청은 "Control 또는 마우스 좌클릭" 둘 다.
  - (C) **EditMode 에서 MonoBehaviour OnEnable 자동 호출 미보장** — InputAction 바인딩 검증 테스트에서 `_interactAction == null` 회귀. EditMode 라이프사이클이 PlayMode 와 다름.
- 변경:
  - **신규 자산 (2개)**: `Tool_StoneAxe.asset` 복제 → `Tool_StonePickaxe.asset` (`_id=StonePickaxe`, `_powerMultiplier=1.2`, description "Power x1.2. Designed for breaking rocks."). `Item_Tool_StoneAxe.asset` 복제 → `Item_Tool_StonePickaxe.asset` (`_id=StonePickaxe`, `_category=Tool`).
  - **와이어링**: `Resource_Rock.asset` `_preferredTool` → `Tool_StonePickaxe` GUID 참조. `Resources/GameDataRegistry.asset` `_tools` / `_items` 배열 끝에 신규 SO 두 개 GUID 한 줄씩 추가 (자동 등록 안 되는 환경에서 안전한 YAML 직접 편집).
  - **GatherInteractor.cs OnEnable**: `<Keyboard>/leftCtrl` + `<Keyboard>/rightCtrl` 바인딩 추가. 기존 E/Space 바인딩 그대로 유지 (회귀 가드 테스트 통과).
  - **신규 EditMode 테스트 (12개)**:
    - `Assets/Tests/EditMode/ToolResourceMatchingTests.cs` (10 테스트):
      - TOOL_MATCH_001~004: 합성 SO — 선호 도구=풀파워(1.2) / 불일치=0.6 / 맨손=0.3 / 선호 도구 미설정=풀파워.
      - WIRING_001~005: 디스크 자산 와이어링 — Tool_StonePickaxe 존재/ID/PowerMul, Item ID/Category, Rock.PreferredTool=Pickaxe, Tree.PreferredTool=Axe (회귀), Registry 에 Tool/Item 등록.
      - INTEGRATION: 디스크 자산 기준 곡괭이→돌 풀파워 vs 도끼→돌 0.6x mismatch.
    - `Assets/Tests/EditMode/GatherInteractorInputBindingTests.cs` (2 테스트):
      - CTRL_BIND_001: `<Keyboard>/leftCtrl` 바인딩 검증.
      - CTRL_BIND_002: 기존 `<Keyboard>/e`, `<Keyboard>/space` 바인딩 회귀 가드.
    - **EditMode OnEnable 미호출 회피**: reflection 으로 `OnEnable.Invoke(interactor)` 직접 호출 후 `_interactAction.bindings` 도 reflection 으로 IEnumerable 순회 (Unity.InputSystem 어셈블리 정적 의존 회피 — `Rootborn.Tests.EditMode.asmdef` 추가 참조 불필요).
- 검증 (TDD 흐름):
  - **RED**: 7 테스트 실패 (ToolResourceMatching 5 wiring + CTRL_BIND 2). TOOL_MATCH 4개 합성 단위는 즉시 통과 (`ResourceNodeDefinition.ComputeEffectivePower` 로직 자체는 옳음).
  - **GREEN**: 자산 생성 + 와이어링 + Control 바인딩 + Registry 등록 후 — EditMode **124/124 통과**, PlayMode **12/12 통과**, 신규 12 테스트 모두 GREEN. 사용자 요청 "기존 시나리오 테스트 다 성공" 달성.
- 일반화:
  - "마우스 좌클릭 + 키보드 액션 키는 한 코드 경로 (`DoInteract`) 로 수렴 — 입력 바인딩 다중화는 GatherInteractor 에서, 휘두르기 애니메이션은 PlayerController 에서 분리. 도구 매칭 로직은 한 곳 (`ResourceNodeDefinition.ComputeEffectivePower`)."
  - "EditMode 에서 MonoBehaviour OnEnable/Awake 자동 호출이 보장 안 됨 — InputSystem 같은 라이프사이클 의존 컴포넌트 검증은 reflection 으로 OnEnable 직접 Invoke. PlayMode 테스트로 격상하기보다 EditMode 빠른 피드백 유지."
  - "InputSystem 같은 외부 어셈블리 정적 의존을 피하려면 reflection 으로 InputAction.bindings 순회 (path 프로퍼티는 string). asmdef 추가 참조 없이도 검증 가능 — 테스트 어셈블리 가벼움 유지."
  - "데이터 자산 (.asset) 신규 추가 = `assets-copy` (기존 자산 GUID 보존 안전) + `assets-modify pathPatches` (ID/desc 갱신). YAML 직접 편집은 GameDataRegistry 같은 등록 배열에 한 줄 추가 시에만 (assets-modify 가 배열 추가 미지원하는 케이스)."

---

## 2026-05-05 — 외부 프레임 sprite 자식 UI anchor 는 panel 가장자리 X, 시각 영역 UV O
- 피드백: "북마크의 위치가 회색부분이 아니라 완전 옆면 책의 맨 뒤 완전 갈색부분 보다 왼쪽으로 와야하는데 너무 오른쪽으로 가져있어 딱 북마크 책 옆에 놓는것처럼 해야하는데 그게 안되고있어"
- 원인: 책 BookPanel sprite (Page1.png 290×184) 가 **외부 어두운 프레임 + 갈색 spine 측면 + 내부 베이지 페이지** 를 한 sprite 에 통합. AI 가 BookPanel sizeDelta(=1248×792 = sprite 전체)의 우측 가장자리 (anchor=1.0) 를 "책 우측" 으로 착각하고 북마크를 거기에 붙임. 결과: 북마크가 회색 외부 프레임 위/바깥에 떠있음. 베이지 페이지 우측 끝은 sprite 픽셀 263/290 = UV 0.907 인데 이 차이를 측정 안 함.
- 변경:
  - `Assets/Scripts/UI/HUD/StatusHud.cs` BuildBookmarks: anchorMin/Max `(1, 0.5)` → **`(0.907, 0.5)`** (베이지 페이지 우측 끝 UV). `BookmarkRestX=-10` (베이지 안쪽으로 10px), `BookmarkSelectedX=+14` (회색 프레임 위로 살짝).
  - `rules/path-based/sprite-slicing.md` 신규 섹션 "외부 프레임 sprite 의 자식 UI 배치 — anchor 기준은 시각 영역, panel sizeDelta 아님". 안티패턴/권장 패턴/일반화 3 항목 + 픽셀 측정 절차.
- 일반화:
  - "외부 프레임이 두꺼운 sprite (책/패널/UI 카드) 에서 자식 UI 의 anchor 기준은 panel sizeDelta 가장자리가 아니라 **sprite 안쪽 시각 영역 UV**. PNG 픽셀 측정 후 UV 비율(예: 263/290 = 0.907) 을 anchor 로 사용."
  - "사용자가 'X 영역이 아니라 Y 영역에 붙여' 라고 지적하면 anchor UV 부터 점검 (panel sizeDelta 변경이 아님)."
  - "Pixelwood Page sprite 같은 외부 프레임 통합 sprite 는 콘텐츠 안전 영역의 UV 좌표를 코드 주석에 명시 의무 (재사용 시 디버깅 용이)."

## 2026-05-05 — UI 부팅 타이밍 (Awake → async Start) + 책 패널 1248×792 + Equipment 페이지 통합
- 피드백: "현재는 인벤토리나 장비 등 ui가 제대로 안나오는 경우가 대다수 / Assets/Pixelwood Valley/Fantasy Book UI V2 경로에 있는 sprite를 이용하여 UI 나올 수 있도록 설계 및 테스트 필요 / addressable 사용하는지도 꼭 확인하고 진행해"
- 원인:
  - (A) **타이밍 버그**: `FarmHudController.Awake` 가 `BuildTree` 호출 → `Spr()` (= `ResourceManager.Load<T>` 동기 캐시 조회). 그러나 `Managers.BootstrapAsync` 는 같은 씬 `GameBootstrap.Start` 에서 시작 — Unity 라이프사이클상 모든 MonoBehaviour `Awake` 이후. 결과: 캐시 미스 → 모든 sprite null fallback (단색 사각형) 으로 빌드 → 책/리본/슬롯 sprite 안 보임.
  - (B) **Equipment 페이지 충돌**: `BuildRightPageContent` 가 `_equipmentPageContent` (캐릭터+장착슬롯) 를 우측 ITEMS 페이지 안에 만들고 `SetActive(false)` 로 마감. `ApplyCategory` 는 `_leftPagesByCat`/`_rightPagesByCat` 만 토글 — `_equipmentPageContent` 는 어디서도 켜지지 않음. 별도로 `BuildEquipmentCategoryPages` 가 만든 `RightStats` 만 매핑 → 캐릭터+장착슬롯 영영 안 보임.
  - (C) 책 panel 950×600 + Page1.png 290×184 + `preserveAspect=true` 라 letterboxing 무시했지만, anchor 비율 (0.083~0.493) 이 실제 sprite 안쪽 베이지 영역과 1픽셀 단위로 안 맞아 콘텐츠가 spine 또는 프레임 위로 약간 새어나옴.
  - (D) 폰트 11/12/13/14 다수 — 헌법 `ui-standards.md` 절대 하한선 14, 본문 권장 18 위반.
  - (E) 북마크 `BookmarkRestX=-32`/`SelectedX=-8` (음수, pivot=(1,0.5)) 로 책 panel 안쪽으로 들어가 박혀있어 샘플 이미지의 "책 바깥쪽 우측에 튀어나옴" 효과 안 남.
- 변경:
  - `Assets/Scripts/UI/HUD/StatusHud.cs`:
    - `Awake` 제거 → `async void Start` 한 곳에서 `await Managers.BootstrapAsync()` 후 `BuildTree`. Sprite 캐시가 보장된 후에만 UI 빌드.
    - 책 panel 950×600 → **1248×792** (화면 65%). Page1.png 290×184 실측 기반 anchor 재계산 (좌측 0.103~0.466, 우측 0.545~0.907, y 0.120~0.880).
    - 북마크: pivot `(1, 0.5)` → `(0, 0.5)` 변경 + `BookmarkRestX=0`, `SelectedX=+28` (양수). 책 우측 가장자리에서 바깥쪽으로 튀어나옴.
    - `_equipmentPageContent` 필드 제거 + `BuildRightPageContent` 의 캐릭터/장착슬롯 블록 제거. `BuildEquipmentCategoryPages` 가 좌(캐릭터+6슬롯)+우(STATS) 모두 책임. `_equippedSlotIcon` 는 첫 슬롯(Head) 에 연결.
    - 폰트 11/12/13/14 → 16/18/22 일괄 상향. 책 크기 증가 따라 ribbon 280×48, slot grid cell 84, button 108×44 도 비례 확대.
  - `Assets/Scripts/Editor/Tools/AddressablesSetup.cs` — 테스트용 public 헬퍼 `GetUiSpriteAddresses()` / `GetUiSpriteEntries()` 추가 (UISpriteAddresses 와 일관성 검증).
  - `Assets/Tests/EditMode/UISpriteAddressesTests.cs` 신규 — 6개 EditMode 테스트:
    1. AllSingleSprites 가 모두 AddressablesSetup 에 등록됐는지
    2. AllSheets 도 등록됐는지
    3. UiSpriteEntries 의 모든 asset 파일이 디스크에 존재하는지
    4. BookFlipFrames 9개가 AllSingleSprites 에 포함됐는지
    5. SubBookmark0..4 명명 규약이 PixelwoodSliceSetup 의 `Bookmark_{0..4}` 와 일치하는지
    6. AllSheets 의 BookmarkSheet 가 5개 sub-sprite 선언했는지
  - `Assets/Tests/EditMode/Rootborn.Tests.EditMode.asmdef` — `Rootborn.Editor`, `Rootborn.UI` 참조 추가.
- 일반화:
  - "Addressables 동기 조회 (`Load<T>`) 는 `BootstrapAsync` 완료 후에만 캐시 hit. UI 빌드 코드는 `Awake` 가 아니라 `async Start` + `await BootstrapAsync` 후 실행. 이는 SlimeMaster 패턴 차용 시 라이프사이클 순서 검증 의무."
  - "한 카테고리 콘텐츠는 한 컨테이너에. ITEMS 우측 페이지 안에 EQUIPMENT 콘텐츠를 끼워넣고 별도 토글 변수로 관리하는 패턴은 사이드이펙트(어느 카테고리에서 어떤 GameObject 가 켜지는지) 가 분산돼 누락되기 쉽다 — `_pagesByCat[Category]` Dictionary 단일 진입점에서만 SetActive."
  - "Sprite 시각 검증 의무: 새 sprite asset 와이어링 전 `Read` 도구로 PNG 직접 시각 확인 (290×184 같은 작은 sprite 도 베이지 안전 영역을 픽셀 단위로 측정해서 anchor 비율 결정)."
  - "UISpriteAddresses 같은 상수 파일과 AddressablesSetup 같은 등록 파일은 자동 일관성 검증 테스트 의무 — 한 쪽만 수정하면 런타임 캐시 미스 → 단색 fallback UI 가 빌드되는 회귀 사고."

## 2026-05-04 — Sprite sheet 균등 분할 안 되는 경우 + 카테고리 컨테이너 분리 + 선택 시각 표시
- 피드백: "오른쪽 북마크는 슬라이스 다시 해야할거같고 / 각 북마크에 맞는 페이지는 하나도 안나오고 헤당 북마크를 했을때 북마크가 선택되었다는 것도 보여줘야해 / 두번째 세번째 오른쪽 북마크를 보면 선택된 북마크가 좀 더 오른쪽으로 나왔다는걸 볼 수 있어 / 그 외에도 아이템칸, 장비칸, 설정칸 등 나오도록 UI 배치가 필요해"
- 원인:
  - (1) Bookmark sheet (22×99, 5색) 가 cell 19px × 5 = 95 + 4 leftover 인데 SliceOne 의 균등 cell 슬라이스로는 마지막 셀이 잘리거나 어긋남.
  - (2) 카테고리(북마크) 5개가 있는데 한 개 페이지 콘텐츠만 만들고 InventoryView 필터로만 분기 → Equipment 같은 다른 구조 카테고리에서 화면 깨짐.
  - (3) 선택된 북마크 시각 표시 부재 — 5개가 모두 같은 위치에 있어 어느 것이 활성인지 식별 불가.
- 변경:
  - `Assets/Scripts/Editor/Tools/PixelwoodSliceSetup.cs` — `SliceBookmarkSheet()` 신규 (명시 rect 5개, 마지막 셀 leftover 흡수)
  - `Assets/Scripts/Game/Common/GameDataRegistry.cs` — Sub-sprite 이름 `Bookmark_r{0..4}_c0` → `Bookmark_{0..4}` 단순화
  - `Assets/Scripts/UI/HUD/StatusHud.cs` — `_leftPagesByCat`/`_rightPagesByCat`/`_bookmarkRectsByCat` Dictionary, `BuildItemsCategoryPages`/`BuildEquipmentCategoryPages` 분리, `ApplyCategory` 가 카테고리별 컨테이너 토글 + 선택 북마크 anchoredPosition.x 변경 (`BookmarkRestX=-32`, `BookmarkSelectedX=-8`)
  - `rules/path-based/sprite-slicing.md` — "6. Sheet 가 cell 균등 분할이 안 되는 경우 — 명시 rect 사용" 섹션 추가, 판별 기준 + 코드 예시
  - `rules/ui-standards.md` — "선택된 탭/북마크는 위치 offset 또는 색조 강조" 명시 + "카테고리/탭 콘텐츠 컨테이너 분리" 섹션 추가 (Dictionary<Category, GameObject> 패턴)
- 일반화:
  - "Sheet 픽셀 크기 % cell 크기 != 0 이면 즉시 명시 rect 슬라이스 메서드 작성. 파일명 추정값보다 실제 sheet 픽셀 크기가 우선."
  - "여러 카테고리 UI 패널은 카테고리당 별도 GameObject 컨테이너 + SetActive 토글. 한 페이지에 모든 콘텐츠 혼재 + sprite/text 만 갈아끼우는 패턴 금지."
  - "선택된 항목은 정적 색만으로 부족 — 위치 offset 또는 채도/색조 강조 중 하나 이상 필수."

## 2026-05-04 — Addressables 도입 (SlimeMaster 패턴 차용)
- 피드백: "C:\Users\jdyj\Downloads\SlimeMaster ... addressable을 사용했는데 동일한 구조로 해볼 수 있겠어?"
- 원인: ROOTBORN이 GameDataRegistry를 `Assets/Resources/`로만 로드 → 빌드 시 메모리 적재, 핫업데이트 불가, 헌법 `path-based/assets-addressables.md` 위반
- 변경:
  - `Packages/manifest.json` — `com.unity.addressables` 2.4.6 추가
  - `Assets/Scripts/Game/Managers/Managers.cs` 신규 — 싱글톤 진입점 (`@Managers` + DontDestroyOnLoad, `BootstrapAsync()`)
  - `Assets/Scripts/Game/Managers/ResourceManager.cs` 신규 — async/await Addressables 래퍼, 캐시, Release 명시
  - `Assets/Scripts/Game/Managers/DataManager.cs` 신규 — `GameDataRegistry` + 도메인별 Dictionary lookup
  - `Assets/Scripts/Editor/Tools/AddressablesSetup.cs` 신규 — 그룹(Data/Sprites/Prefabs/Tiles) + `PreLoad` 라벨 + 자산 자동 등록
  - `GameBootstrap.Start` async, `Managers.BootstrapAsync()` 호출
  - `FarmAutoFiller` Resources.Load → `Managers.Data.Registry` 사용 (Resources fallback 유지)
  - `BuildScript` Client 빌드 시 `AddressableAssetSettings.BuildPlayerContent()` 자동 호출
  - `OneClickSetup` Step 6/6 추가 (`AddressablesSetup.WireAll`)
  - `rules/path-based/assets-addressables.md` — ROOTBORN 매니저 구조와 주소 규약 명시
- 일반화: "동적 에셋 로딩은 Addressables 일원화. SlimeMaster의 단순 콜백 래퍼 대신 async/await + Release 명시. fallback Resources.Load는 단 한 번만 허용."


## 2026-05-04 — Sprite 원본 방향 실측 (Pixelwood Side flipX 사건)
- 피드백: "왼쪽 오른쪽 뛰는게 반대야"
- 원인: Pixelwood Player Character Side.png 원본이 왼쪽을 향함. 일반 관례(원본=오른쪽) 가정으로 flipX = (input.x < 0)로 작성 → 좌우 반대 동작.
- 변경:
  - `PlayerController.cs` — flipX 조건 부호 반전 + 주석 명시
  - `rules/path-based/sprite-slicing.md` — "Sprite 원본 방향 (flipX 부호)" 섹션 추가, 원본 확인 절차 + 부호 규칙
- 일반화: "Side-view sprite의 원본 방향은 일반 관례에 의존하지 말고 PNG를 직접 보고 실측. 코드에 주석으로 원본 방향 명시."

## 2026-05-04 — Sprite sheet 셀 크기 추정 금지 (Pixelwood Idle/Down 사건)
- 피드백: "idle down은 236x49인데 16으로 잘라도 되는거야?"
- 원인: PixelwoodSliceSetup이 모든 sheet에 일괄 16×16 셀 적용. 캐릭터 sheet는 실제 59×49 셀이라 sliced sub-sprite가 잘못됨. 화면에 캐릭터가 안 보임.
- 변경:
  - `PixelwoodSliceSetup.cs` — 캐릭터 sheet 6개를 59×49 + PPU 49로 변경. SliceTarget에 PixelsPerUnit 필드 추가.
  - `FarmAutoFiller` — Player scale 4 → 1.5 (PPU 49로 1 unit 정사각형이 됨).
  - `rules/path-based/sprite-slicing.md` 신규 — Sheet별 실측 의무, PPU 통일 1 world unit 원칙, 정수 분할 검증, Pixelwood 구체 데이터 표.
- 일반화: "외부 sprite sheet 추가 시 셀 크기 추정 금지. 텍스처 픽셀 크기 + 한 프레임 셀 크기 + PPU 셋 다 실측. 파일명에 적힌 숫자도 우연일 수 있음."

## 2026-05-04 — ROOTBORN 프로젝트 분기 (coin-defense → farmer)
- 변경: coin-defense 하네스를 ROOTBORN(세대 진화 농장 생존 게임)용으로 fork
- `constitution.md` — 헤더 "ROOTBORN", 원칙 2번 "코인 데이터드리븐" → "엔티티(작물·도구·지식·특성·세대) 데이터드리븐", 네임스페이스 `Rootborn.*`, 패시브 ADR-0002 절 삭제
- `rules/path-based/assets-data.md` — 코인 절 → 엔티티 절 일반화. SO 와이어링 경로 `Assets/Data/{Crops|Tools|...}/`로 표기
- `rules/path-based/server.md` — 백엔드 미사용으로 삭제
- `rules/commit-conventions.md` — `[DB]`/`[SIM]` prefix 제거 (DB 미사용, 결정론 시뮬 불필요), 예시를 BitKnight → Wheat/Knowledge로 교체
- `Scripts/ci/check-no-coin-id-branching.sh` 삭제 → `check-no-entity-id-branching.sh` 신규 (cropId/toolId/knowledgeId/traitId 정규식 차단)
- `Scripts/ci/check-scenario-registry-sync.sh` — 시나리오 prefix MS/GS/META/PAY/NET → GEN/HEIR/TOOL/CROP/KNOW/STATUS/NET
- `.github/workflows/ci.yml` — server/admin job 제거, static-checks(entity-id 게이트) + Unity EditMode만
- `.github/workflows/unity-tests-nightly.yml` — PlayMode job 추가
- 에이전트 `web-architect/frontend-developer/backend-developer/database-architect` 4개 제거
- 스킬 `backend-web-dev` 제거

## 2026-04-12 — 초기 헌법 수립
- 하네스 최초 구축
- `constitution.md`, `rules/coding-standards.md`, `rules/unity-cli.md` 작성

## 2026-04-12 — UI 사용성 피드백 반영: 모바일 UI/UX 표준 신설
- 피드백: "X 버튼 동작 안함, 탭이 텍스트로만 보임, 슬롯 수 틀림, 텍스트 너무 작음, 가챠를 상점 내로 합쳐야 함"
- 원인: UI/UX 관련 규칙이 전무. Editor 스크립트가 SerializeField를 부분만 와이어링. 모바일 타이포/터치 기준 없음.
- 변경: `rules/ui-standards.md` 신규 생성 (모바일 타이포, 터치 타겟, 버튼 어포던스, 직렬화 완전성, 수치 추정 금지, UI IA 확인)
- 일반화: "절차적 UI 생성 시 직렬화 완전성" + "모바일 UI 최소 기준" + "게임 수치는 spec 기반" 원칙

## 2026-04-17 — 외부 Game Studios 하네스 통합 (P0~P6)

### P0 — POC 및 준비
- 피드백: "외부 Game Studios 하네스와 현 Unity 하네스를 병합하고 싶음"
- 변경: 외부 레포 clone (`.claude/_workspace/external-repo`), settings.json 백업, POC 3건 결정, .gitignore 업데이트
- POC 결정: (1) 스킬 하이픈 네임스페이스 기존 관례로 충분, (2) 공식 YAML 필드만 사용, (3) P3 수동 검증 지연 (P5 완료로 실증 확인됨)
- 일반화: "외부 하네스 도입 시 POC 선행 + 백업 필수" 원칙

### P1 — 게임 기획 템플릿 15종 도입
- 변경: `.claude/docs/templates/` 15개 (GDD/ADR/Epic/Story/... 외부 MIT 원본 + 한국어 헤더)
- 변경: `.claude/docs/workflow-catalog.yaml` (경로별 파이프라인 카탈로그)
- 일반화: "산출물 템플릿은 `.claude/docs/templates/` 에 집중, 외부 출처 명시"

### P2 — 경로 기반 rules + 게임 도메인 규약
- 변경: `rules/path-based/` 5개 (UI/gameplay/data/addressables/server), `rules/game-design.md`, `rules/localization.md`
- 기존 coding-standards/ui-standards 상단에 path-based 링크만 추가 (본문 무변)
- 일반화: "경로별 코딩 표준은 `rules/path-based/*.md` 에 배치"

### P3 — 훅 분리 (Claude Code + git)
- 변경: `.claude/hooks/session-hint.sh` (UserPromptSubmit 세션 1회 힌트), `scripts/git-hooks/pre-commit`/`pre-push`/`install.sh`/`uninstall.sh` (core.hooksPath 방식), `.gitattributes` (LF 강제)
- settings.json: UserPromptSubmit 배열에 session-hint 추가
- 일반화: "Claude Code 훅은 `.claude/hooks/`, git 훅은 `scripts/git-hooks/` + `core.hooksPath` 로 분리"

### P4a — 핵심 게임 특화 에이전트 6개
- 변경: `t1-technical-director` (unity-architect 섹션 인용), `t2-game-designer`, `t2-qa-lead`, `t2-release-manager`, `t3-gameplay-programmer`, `t3-unity-specialist`
- 기존 18개 에이전트 무변경 (병합은 본문 인용 방식)
- 일반화: "신규 에이전트는 평면 구조 + 파일명 prefix(t1-/t2-/t3-), YAML frontmatter 는 공식 필드만, 메타는 HTML 주석"

### P5 — 게임 개발 파이프라인 스킬 16개
- 변경: `.claude/skills/gs-*/skill.md` 16개
- 래퍼 구조: /gs-code-review → game-code-review, /gs-release-checklist → unity-build-deploy, /gs-dev-story → dev-iteration-loop
- 실증 확인: available skills 목록에 16개 모두 등록됨 (하이픈 네임스페이스 동작)
- 일반화: "스킬 네임스페이스는 `gs-*` 하이픈, 디렉토리명 = 호출명, 파일명 lowercase `skill.md`"

### P4b — 보조 에이전트 (조건부 후속)
- 결정 연기: P5 관통 테스트에서 사용 빈도 확인 후 추가.
- 파킹 대상: t1-creative-director, t1-producer, t2-economy-designer, t2-localization-lead, t3-ui-programmer, t3-accessibility-specialist.
- 일반화: "보조 에이전트는 실제 사용 빈도 확인 후 추가 (YAGNI)"

### P6 — 헌법·피드백 루프 업데이트
- 변경: `constitution.md` 에 게임 개발 파이프라인 섹션 추가 (경로 분기 / GDD-ADR-TR 체인 / Addressables / 3-tier 관례)
- `detect-feedback.js`: 기존 트리거 배열이 이미 한·영 포괄적이어서 변경 없음
- 일반화: "파이프라인 전환은 헌법에 반영, 트리거는 한·영 양방향 지원"

## 2026-04-28 — 코인 데이터-드리븐 절대 원칙 추가 + 디자인 후반 이연
- 피드백: "유닛은 바로 디자인 시작하기 보다 일단 기반기능부터 다 구현하고 추후 별도로 구현하거나 쉽게 변경 될 수 있으면 좋을 듯"
- 원인: 기존 플랜은 M1에서 CoinPenny, M2에서 12종 모두 작성하도록 잡혀 있어, 시스템 안정 전에 코인 디자인이 강제됨. 디자인 변경 시 시스템·테스트 회귀 위험.
- 변경: `rules/path-based/assets-data.md` 끝에 "코인(유닛) 데이터-드리븐 절대 원칙" 섹션 추가. 플랜 Task 0.9는 placeholder archetype 5종 작성 + 코인 디자인 M4 EPIC-023b로 이연. M2 EPIC-007은 archetype mechanic coverage로 변경.
- 일반화: "도메인 컨텐츠는 시스템·아키텍처가 안정된 후 데이터로 채운다. 시스템 코드는 컨텐츠 ID에 의존하지 않는다(static 분석으로 강제)."

## 2026-04-28 — testing-discipline.md 신설 (시나리오 100% 커버 의무)
- 피드백: "각 테스크 및 코어루프마다 자동화 테스트 / 유닛 테스트 필수로 구축. 자동화 테스트는 시나리오 테스트로 코어 루프 100% (최대한 100%) 커버"
- 원인: `coding-standards.md` 와 `path-based/*` 가 코드 스타일 위주로 짜여 있고, 테스트 강제력이 약했음. AI가 작성한 코드가 테스트 없이 머지될 가능성 존재.
- 변경: `rules/testing-discipline.md` 신규 생성 — TDD 의무, Tier 1~4 (단위/통합/시나리오/패리티), 코어 루프 시나리오 카탈로그 36개 명명, 커버리지 임계값 + CI 게이트, 시나리오 추가 의무
- 일반화: "테스트 디시플린은 헌법급 우선 규칙, 모든 신규 코드(특히 AI 생성)는 테스트 동시 작성 의무, 코어 루프는 명명된 시나리오 100% 커버"

## 2026-04-29 (P3) — 패시브 라이브러리 + 와이어링 + CI 워크플로우
- 추가: 8 패시브 SO 클래스 (CritBuff, SlowDebuff, DotApply, BounceAttack, PierceAttack, RootChance, InstantKillChance, RampOnSameTarget) + 8 .asset + 8 코인 와이어링 (DogWifChef/HederaHash/FrogPepe/XRipple/TonRocket/WLDOrb/MoneroMask/BitKnight) — 합계 10 코인 (BNBancer/ChainLinker 포함)
- 추가: 5 신규 EditMode 테스트 클래스 (PassiveLibrary 8 + Roster 4 + Synergy 3 + AutoPlayBot 1) → Coin 네임스페이스 76 통과
- 추가: `.github/workflows/unity-tests-nightly.yml` — game-ci 기반 야간 + PR EditMode 테스트 + DataDrivenScanner gate
- 검증: SynergyMatrix CSV 재생성 후 의도 시너지 페어 +14~18% 안착, DataDrivenScanner 위반 0건
- 일반화: "패시브는 SO 클래스 + .asset + 코인 와이어링 3-요소. 코인은 데이터로만 연결, 코드 분기 없음."

## 2026-04-29 (P2) — Unity .cs 신규 생성: 디스크 직접 쓰기 금지
- 피드백: 사용자가 MCP 사용 종용 ("mcp 연결 안되어 있음? 직접 해") 후 테스트 실행이 컴파일 에러로 차단됨. 원인 추적 결과, `Write` 로 디스크에 직접 쓴 `Assets/Scripts/Game/Coin/StarRank/UnitDpsCalculator.cs` 가 MonoScript 자산으로는 등록되었지만 `CompilationPipeline.GetAssemblies().sourceFiles` 에 누락되어 컴파일 어셈블리에 클래스가 반영되지 않음. 같은 폴더 다른 신규 파일 일부도 동일 증상.
- 원인: Unity 외부에서 디스크에 직접 .cs 를 쓰면 .meta 는 Unity 가 자동 생성하지만 Library/Bee 캐시·.csproj 가 즉시 갱신되지 않는 경합 윈도우가 존재. 본 프로젝트 헌법에 신규 스크립트 생성 표준 부재.
- 변경: `rules/unity-cli.md` 상단에 "Assets/**/*.cs 직접 디스크 쓰기 금지" 절대 원칙 신설. `script-update-or-create` MCP tool 또는 Editor 메뉴 경유 의무. 검증·복구 절차 명시.
- 일반화: "Unity 자산은 Unity API 경유 생성. 외부 도구가 메타 동시 생성을 보장하지 않는 자산 타입(.cs/.shader/.unity 등)은 모두 동일."

## 2026-04-29 — 별등급 시스템 + 27 유닛 로스터 + 미스틱 Model 3 확정 (ADR-0001)
- 사용자 결정: 모델 3 (4 슬롯 × 2종 출시), MA-2 옵션 B, 선형 스탯 + 등급별 분리 비용 곡선, P2W 톤업, 캐치업 방지
- 변경:
  - `design/systems-index.md` 신규 — 별등급/가챠/풀 예산 immutable 상수
  - `docs/architecture/adr-0001-star-rank-cost-curves.md` 신규 — 곡선 결정 근거
  - `docs/architecture/tr-registry.yaml` 신규 — TR-0001/TR-0002 등록
  - `design/gdd/units.md` 신규 — 27 유닛 (4C/5R/5E/5L/StableShield/8M) × 마일스톤 ★3/5/8/12
  - `production/epics/EPIC-032-balance-metrics-pipeline.md` 신규
  - `.claude/rules/testing-discipline.md` STAR-001~015, ECON-001~006, MYT-A~D-001, BAL-001~027, SYN-* 시나리오 추가
- 일반화: "수치 곡선은 systems-index.md immutable 상수, 코인 SO는 디폴트 사용 + 예외 시만 오버라이드. 곡선 변경은 ADR 신설 + EPIC-032 시뮬 게이트 통과 의무."

## 2026-05-11 — 자유 경로 성장 원칙 헌법화
- 피드백: "오픈월드 식으로 자유를 줘서 학교만 다니는 하나의 경로가 아니라, 학교 중간에 나와 다른 특성/성향을 직접 올리는 다양한 요소가 있었으면 한다."
- 원인: 기존 학생 생활 목표가 학교/수업 중심으로 읽힐 수 있어, 성장·퀘스트·진로 힌트가 단일 등교 루트에 종속될 위험이 있었다.
- 변경:
  - `AGENTS.md` Constitutional Rules에 "자유 경로 성장 원칙" 추가.
  - `.claude/constitution.md`에 "자유 경로 성장" 핵심 메카닉과 "Open-Ended Progression" 원칙 추가.
  - `.claude/rules/game-design.md`에 금지/필수 설계 규칙 추가.
- 일반화: "학교 수업은 중요한 경로 중 하나일 뿐 유일한 정답이 아니다. 핵심 성장 목표에는 가능한 한 2개 이상의 대체 경로를 제공하고, 학교 밖 활동도 핵심 성장·진로·관계 진행에 참여해야 한다. 신규 코어 루프는 학교 밖 경로 실제 플레이 검증을 포함한다."

## 2026-05-08 — Sprite PlayMode 전수 검증 헌법화
- 피드백: "개별 sprite 픽셀/주소 전체를 PlayMode에서 전수 검증"
- 원인: 기존 Style2 sprite 검증은 EditMode manifest/source audit와 PlayMode smoke에 머물러, 런타임 Addressables 경로에서 모든 개별 sprite 주소와 픽셀 rect가 실제로 resolve/read 가능한지 전수 확인하지 못했다.
- 변경:
  - `.claude/constitution.md`에 "Sprite 런타임 전수 검증" 절 추가.
  - `Assets/Tests/PlayMode/TownConcept/TownStyle2SpriteExhaustivePlayModeTests.cs` 신규 — `ModernUISpriteAddresses.AllSheets`의 모든 sheet address/sub-sprite를 PlayMode에서 로드하고, 이름 일치와 `textureRect` 전체 픽셀 배열 count를 검증.
  - `Assets/Tests/PlayMode/TownConcept/TownStyle2UiSmokeTests.cs` 강화 — Town 인벤토리 패널을 실제로 열고 생성된 UI tile `Image.sprite`가 Style2 sprite로 resolve 되었는지 검증.
  - `Assets/Scripts/UI/Modern/ModernUiPanelAutoInstaller.cs` 수정 — Town UI 패널 설치 전에 `Managers.BootstrapAsync()` 완료를 기다려 Addressables sprite cache가 준비된 뒤 패널을 빌드.
- 일반화: "sprite 완료 판정은 manifest 존재나 대표 smoke가 아니라 PlayMode 런타임 로더 기준 주소 resolve + 개별 sprite 전체 픽셀 rect 읽기 + 실제 렌더 대상(Image/SpriteRenderer) sprite 할당 확인까지 포함한다."

## 2026-05-24 — LDtk Tileset 원본 좌표 검증 규칙 추가

- 피드백: "지금까지 계속 실수했던거 다음 세션이나 이후에 재발되지않게 헌법에 적어놓거나 조치 취해줘"
- 원인: LDtk Town Ground 타일이 `Tile.name`, Tilemap count, sorting order 기준으로는 정상처럼 보였지만, 생성된 `Sprite.rect`가 원본 PNG 높이(예: `2_City_Terrains_32x32.png` 1888×3296)가 아니라 Unity의 축소 import 높이(2048) 기준으로 잘려 있었다. 그 결과 LDtk `src=(320,736)` 보도블록 타일이 실제 Game View에서는 전혀 다른 픽셀 줄을 렌더링했다.
- 변경:
  - `constitution.md` 그래픽 자산 섹션에 **LDtk 타일 좌표 원본성 보존** 원칙 추가.
  - `rules/path-based/sprite-slicing.md`에 **LDtk Tileset 좌표 변환 — 원본 PNG 높이 기준** 절 추가.
  - `AGENTS.md` Direct Visual Play Verification Gate에 해당 실패 패턴 추가.
- 일반화: LDtk `src`는 PNG 좌상단 기준이고 Unity `Sprite.rect`는 좌하단 기준이다. 변환은 `unityY = originalPngHeight - srcY - tileHeight`이며, `Tile.name`이나 Tilemap draw count만으로는 시각 동일성을 증명할 수 없다. LDtk/Town/Interior tile 작업은 원본 PNG 크기, TextureImporter max size/compression/filter, `Sprite.rect`, 저장 씬 Tilemap 참조, PlayMode Game View 스크린샷을 모두 확인해야 완료다.

## 2026-04-29 — 커밋 메시지 컨벤션 신설
- 피드백: "커밋명 규칙 추가 [FEATURE], [BUGFIX]. [UI], [DB], [ASSET] 등의 prefix 활용. 제목+내용 모두 한글로"
- 원인: 컨벤션 부재 — 영문 conventional 형식 (`feat(sim):`)이 혼용되었고 AI 에이전트가 임의 형식으로 커밋.
- 변경: `rules/commit-conventions.md` 신규 (prefix 카탈로그 13종, 제목·본문 규칙, 예시, 다중 변경 분리 원칙). `constitution.md`에 "커밋 메시지" 섹션 추가하여 최상위 규칙으로 승격.
- 일반화: "사용자가 보는 모든 산출물(커밋 메시지 포함)은 한글이 1차"; "prefix 분류는 변경 카테고리 자가 점검을 강제하여 다중 카테고리 혼합 커밋을 자연스럽게 분리".

## 2026-04-29 — 패시브 시뮬 후크 + 데이터 와이어링 + 디자인 결정 (대규모 세션)

### 시뮬 통합 (10 패시브 후크 완료)
- **AuraAttackSpeedBuff** (P0.3): 인접 코인 공속 곱연산 buff. EffectiveAttackSpeed.
- **CritBuff**: 공격 시 RNG 굴림 → 데미지 ×CritMultiplier. Crit-free 코인은 RNG 미소비 → parity vector 유지.
- **DotApply**: per-enemy DOT 스택 (FIFO 캡), TickDots 프레임당 1회. EnemyState에 List<DotStack>.
- **SlowDebuff**: per-enemy 속도 디버프, 강한 magnitude/늦은 expire 우선. EffectiveMoveSpeed.
- **AuraAttackPowerBuff**: 인접 코인 공격력 곱연산 buff. EffectiveDamage.
- **InstantKillChance**: 공격 시 RNG 굴림 → HP=0. 보스 면역(IncludeBoss=false 시 RNG 미소비).
- **RootChance**: RNG 굴림 → magnitude=1.0 슬로우. SlowMagnitude/SlowExpire 슬롯 재사용.
- **RampOnSameTarget**: 코인별 LastTargetEnemyId + RampBonusRaw. 데미지 가산.
- **BounceAttack**: 추가 적 N마리에 감쇠 데미지 체인. FindNearestEnemyExcluding.
- **PierceAttack**: 추가 적 N마리에 감쇠 없는 풀 데미지 (또는 unlimited).

공통 원칙:
- **결정론**: 모든 RNG는 XorShift64 흐름. RNG 미소비 케이스 명시 (보스 면역, 미장착, chance=0).
- **Hash invariance**: opt-in segment 패턴 (`,dots:`, `,slow:`, `,ramp:`, `,star:`) — 사용 안 한 코인/적의 hash format 변경 없음. 기존 parity vector 유지.
- **HandleEnemyKill 헬퍼**: gold + boss bonus + KillCount 단일 출처. TickCombat·TickDots·Bounce·Pierce 4곳 호출.

### 데이터 와이어링 (28 코인 / 18 패시브 .asset)
- 기존 10 코인 wired (BNBancer/ChainLinker/DogWifChef/HederaHash/FrogPepe/XRipple/TonRocket/WLDOrb/MoneroMask/BitKnight)
- 신규 8 코인 wired (LightSilver/FlokiViking/TronTraffic/EthSage/SolWiz/HyperBitKing/CritLordWif/BittensorOmni) — 기존 패시브 클래스 재활용
- 잔여 10 코인 (CashFork/NearByte/MoonShibe/MemeMint/StableShield/GenesisCore/OracleEye/TerraTombstone/BitConnectMax/ChronoFetch)는 신규 패시브 클래스 필요 → ADR-0002 로드맵

### 디자인 결정 (ADR-0002 / TR-0003)
- **SO 인스턴스 = 튜닝 단위** (`Passive_<Type>_<CoinName>.asset`). 같은 클래스의 다른 .asset이 인스턴스별 다른 수치.
- **코인당 다중 패시브 허용** (서로 다른 클래스). 같은 클래스 두 인스턴스는 first-wins.
- **★별 잠금해제·곡선 — 코인별 자유**. 단일 글로벌 정책 강제하지 않음 (post-MVP `_milestones` 데이터 입력).
- **액티브 슬롯 데이터 예약** (`ActiveSkillBase[] _activeSkills` + `IActiveSkill` 추상). MVP 시뮬 미호출.

### 데이터 + 인프라
- **CoinInstance.Star** 추가 (default 1, clamp <1 to 1). FinalStateHash opt-in segment.
- **DpsDistributionRunner** (EPIC-032): N-시드 분포 측정 + JSON 산출 (overflow-safe Q16.16 평균).
- **DpsDistributionMenu**: 7 시나리오 메뉴 (synthetic + 4 solo + 2 synergy). Solo 결과 — BitKnight 42.6 / DogWifChef 15.8 / CritLordWif 117.6 / HyperBitKing 89.4 DPS. SYN-001 +46.7%, SYN-002 +94%.
- **balance-bands.json**: passive_ranges (10 클래스 × 등급 × 필드) + synergy_pairs (SYN-001..010 스켈레톤).
- **BitKnight ATK 25 → 35** (DPS 12.5 → 17.5). ECON-005 worst-case 강화 (SolWiz 대신 BitKnight로 단언).
- **밴드 좁히기**: Common ★50 max 30→18, Legendary ★1 min 6→15.

### 일반화 (헌법급 원칙)
- "패시브·이펙트 추가 시 RNG·Hash 보존 패턴" — 사용 안 한 케이스에 비용 0 (early return), opt-in segment 로 hash 호환.
- "kill-reward 단일 출처 (HandleEnemyKill)" — 데미지 경로 추가 시 복붙 금지.
- "데이터 와이어링은 SerializedObject 기반 Editor 자동화" — YAML 직접 편집 회피.
- 437/437 EditMode 테스트 통과 (시작 점 ~360 → 종료 437).

## 2026-05-01 — 환경 의존 값 하드코딩 금지 (CoinDefense MCP host 사건)
- 피드백: "db 127.0.0.1:55432가 아니라 58.123.57.182:55432"
- 원인: AI 가 환경 통념(dev=localhost)을 데이터 검증 없이 코드에 박음. 같은 `.env.dev` 의 `DISCORD_REDIRECT_URI`/`OPS_ADMIN_PUBLIC_URL` 에 실제 dev 호스트(`58.123.57.182`)가 노출돼 있었으나 인접 단서 무시. 헌법 §"금지 사항"의 시크릿 톤 규칙은 host/port 등 비-시크릿 환경 값을 충분히 커버하지 못했고, `tools/`·`scripts/` 등 server/ 외 경로는 사실상 사각지대.
- 변경:
  - `rules/environment-config.md` 신규 생성 — Why / 금지 / 허용 패턴 / PR 검증 절차 / 인접 단서 체크리스트 / 환경 분리 작업 의무 6 섹션. 적용 범위: Unity 클라·서버·도구·스크립트·MCP·CI 전 경로.
  - `constitution.md` §"금지 사항" 에 "환경 의존 값(host/port/url/endpoint/credential/path) 하드코딩 금지" 한 줄 추가 + `rules/environment-config.md` 포인터.
  - `rules/coding-standards.md` 상단에 "전 경로 적용 — 환경 설정" 포인터 추가.
  - `rules/path-based/server.md` 상단에 "환경 의존 값 일반 규칙" 포인터 추가 (분석서가 지시한 `server-node.md` 는 디스크에 부재 — 활성 서버 규칙 파일에 추가).
- 일반화: "환경 의존 값(host/port/url/credential/path)은 환경 데이터로 정의. 환경 통념 가정 금지, 데이터로 확인. `.env`·설정 파일의 인접 키를 일관성 단서로 활용 의무. 도메인 한정(코인 데이터-드리븐) 원칙을 환경 설정으로 확장 — 모든 환경 의존 값은 코드 외부에서 주입."

## 2026-05-01 — EPIC-039 별 마일스톤 액티브/★8 v8 데이터 규약
- 피드백: "EPIC-039 Star Milestone + Active 병렬 작업에서 asset/data/docs Worker는 코드 분기 없이 안전한 와이어링과 문서 갱신만 수행"
- 원인: 액티브 스킬과 ★8 패시브 변형은 데이터 와이어링 작업이지만, 선행 C# 타입과 `StarUnlockTier` 선택 규칙이 없으면 Unity asset을 먼저 만들 수 없음. 잘못 진행하면 누락된 MonoScript 참조나 의미 없는 v8 asset이 생김.
- 변경: `docs/architecture/tr-registry.yaml` 에 TR-039 등록, `design/gdd/units.md` 에 EPIC-039 ★5 액티브 5종/★8 v8 패시브 4종/★12 데이터 규약 추가.
- 일반화: "SO asset 와이어링은 대상 ScriptableObject 타입과 선택 규칙이 존재한 뒤 수행한다. 의존 코드가 없을 때는 문서·추적성만 갱신하고 asset 생성은 보류한다."

## 2026-04-28 — server-node.md 신설, server.md(Python/Flask) deprecated
- 피드백 없음 (CoinDefense 프로젝트 시작 시점 결정)
- 변경: `rules/path-based/server-node.md` 신규 생성 (Node 20 + Fastify + TS + Drizzle + Zod + vitest 표준), `path-based/server.md` 상단에 deprecation 배너
- 일반화: "스택 변경 시 신규 규칙 + 기존 규칙 deprecation 명시 의무"
## 2026-05-16 — Direct Visual Play Verification Gate

- Feedback: repeated completion claims were made before the actual in-game visual result was verified, causing long loops around chair placement, sliced sprites, stale sample Tilemaps, and overlay/UI coverage.
- Change: added a constitutional addendum to `AGENTS.md` and a matching gate to `rules/testing-discipline.md`.
- General rule: visible gameplay/UI/sprite/tilemap/placement work must be verified through the real PlayMode player flow, with Game View or Camera screenshot inspection plus runtime Tilemap/Renderer/UI state checks. Passing tests, asset-name checks, or internal calls alone are not enough to claim completion.
