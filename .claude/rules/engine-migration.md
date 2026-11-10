# 엔진 이전 기간 규칙 (Unity → MonoGame)

2026-11-11 사용자 결정. Claude와 Codex 모두 따른다. 전체 방향은 `docs/superpowers/specs/2026-11-11-unity-removal-design.md`, 규칙 떼어내기는 `docs/superpowers/specs/2026-11-11-core-rules-extraction-design.md`.

## 지금 상태

- **Unity 판은 얼렸다.** 태그 `unity-playable-20261111`이 플레이 가능한 기준본이다. Unity 코드와 `Assets/` 아래는 고치지 않고 읽기 전용 참고 자료로만 쓴다. Unity 테스트를 돌리지 않는다.
- MonoGame 판이 메인 체인을 돌릴 때까지 새 기능을 넣지 않는다. 같은 게임을 옮기는 일만 한다.
- 게임 데이터와 맵의 원본: Unity 에셋(`Assets/Data`)과 LDtk 파일. `Tools/Rootborn.DataExport`가 JSON(`data/`)으로 바꾼다. 데이터 변환이 끝나 JSON이 원본이 되면 이 줄을 고친다.

## 어디에 무엇을 쓰는가

- 게임 규칙: `Packages/com.rootborn.core`(`Rootborn.Core`). 엔진 참조 없음, LangVersion 9. 값은 생성자로 받고 엔티티는 id 문자열로 가리킨다. 엔티티 id로 분기하지 않는다.
- MonoGame 판: `game/`. MonoGame 참조는 실행 프로그램(Host)에만 둔다.
- 옮기면서 규칙의 동작을 바꾸지 않는다. Unity 코드와 다르게 해야 할 이유가 보이면 기록하고 사용자에게 묻는다.

## 테스트

- 규칙은 `dotnet test Tools/Rootborn.Core.Tests`. 기존 EditMode 규칙 테스트의 내용을 옮겨 와 같은 동작임을 확인한다.
- dotnet 통과는 "자동 테스트"다. "실제 플레이로 확인"은 MonoGame 판의 장치 입력 테스트나 직접 본 화면에만 쓴다.

## 동시에 진행할 때

- 작업 폴더(worktree) 하나에 쓰는 세션 하나. 세션마다 자기 브랜치에 커밋하고 푸시하지 않는다.
- 자기 범위 밖 폴더를 고치지 않는다. main에 합치는 일은 조정 담당 세션이 한다.

## 되풀이하는 절차는 스킬로

사용자 지시(2026-11-11): 진행하면서 스킬로 만들 수 있는 것은 스킬로 만들어 Claude와 Codex 양쪽에 저장한다. 같은 내용을 `.claude/skills/<이름>/SKILL.md`와 `.agents/skills/<이름>/SKILL.md`에 둔다. 절차가 바뀌면 양쪽을 함께 고치고, 그 절차가 더 쓰이지 않게 되면 양쪽에서 지운다.

지금 있는 것: `port-rule-to-core`(규칙 옮기기), `parallel-claude-session`(세션 나눠 맡기고 합치기), `export-game-data`(데이터 변환), `host-play-test`(MonoGame 판 화면 테스트).

## 끝난 것은 남기지 않는다

Unity 관련 부분(영역, 도구, 절차)이 더 쓰이지 않게 되면 그때 해당 규칙 조항, 문서, 스킬·에이전트, 메모리, 낡은 테스트를 지운다. 전환 때까지 모아 두지 않는다. Unity 판이 아직 쓰는 것은 그 사용이 끝날 때까지 둔다. 끝난 구현 계획 문서도 지운다.

## MonoGame 판

- MonoGame 3.8.6.1로 고정한다. MonoGame 참조는 실행 프로그램(Host)에만 둔다.
- 테스트의 프레임 진행은 MonoGame 내부 `SdlRunLoop` 호출에, 한글 조합 입력은 SDL2 직접 호출에 기댄다. 각각 한 곳에만 두고, 버전을 올릴 때 이 둘을 먼저 확인한다.
- 그림은 PNG를 그대로 읽고, 맵은 LDtk 파일을 그대로 읽는다. 변환 단계를 두지 않는다.
