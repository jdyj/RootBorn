# 새 세션 프롬프트 — ROOTBORN을 Unity 밖으로 옮기기 (브레인스토밍)

아래 블록을 새 Claude Code 세션(작업 폴더 `D:\User\farmer`)에 그대로 붙여 넣는다.

---

```
superpowers:brainstorming 스킬로 시작해 줘. 주제: ROOTBORN을 Unity에서 걷어내고 다른 엔진이나 자체 프로그램에서 돌아가게 만드는 것.

## 내가 정한 것
- 목표는 "Unity는 화면만 맡기기"가 아니라, 게임이 Unity 없이 다른 엔진이나 자체 프로그램에서 돌아가는 것이다.
- 어떤 엔진·프레임워크로 갈지는 아직 안 정했다. 후보를 비교해서 추천해 줘.

## 진행 방식
- 브레인스토밍 규칙대로 한 번에 질문 하나씩, 한국어로 해 줘.
- 코드·문서를 읽는 조사는 해도 되지만, 내가 설계 문서를 승인하기 전에는 아무것도 만들거나 고치지 마.
- 설계 문서 위치: docs/superpowers/specs/YYYY-MM-DD-unity-removal-design.md
- 이 일은 여러 독립 프로젝트로 쪼개질 가능성이 크다. 그렇다면 순서를 같이 정하고 하나씩 브레인스토밍해 줘.

## 지금 프로젝트 상태 (2026-11-11, main 기준)
- 엔진: Unity 6000.3.13f1, URP 2D, Unity Netcode for GameObjects + Unity Transport, Steamworks.NET(친구 방),
  Addressables, New Input System, uGUI(Text, 자체 ModernUiTileImage 9-slice), Tilemap + LDtk(com.cammin.ldtkunity), Timeline.
- 코드: Assets/Scripts 619개 .cs(asmdef: Rootborn.Game / UI / Network / Editor — 모두 UnityEngine 참조),
  테스트 491개 .cs(EditMode·PlayMode). PlayMode E2E가 실제 키보드·마우스 장치 입력으로 플레이를 검증한다.
- 이미 엔진 무관인 부분: Packages/com.rootborn.core (noEngineReferences, 13개 .cs — 인벤토리 계획, 보상 트랜잭션,
  퀘스트 추적·목표 매칭, 착용품 규칙·저장, 세션 정원·스폰, 상호작용 대상 규칙).
  Unity 없이 `dotnet test Tools/Rootborn.Core.Tests`(net8.0, LangVersion 9)로 검증한다.
- 데이터: Assets/Data 아래 ScriptableObject .asset 3806개가 게임 데이터 원본이다(활동, 진로, 퀘스트, 가구, 장소, 대사 등).
  엔티티 데이터 드리븐 원칙: 엔티티 ID로 분기하는 C# 금지, SO + 전략 배열로만 표현(AGENTS.md 2번).
- 씬 15개: Boot, MainMenu, HostLobby, Town, House, School, Beach, 시설 실내 6곳(Kitchen/Kitchen2/Hospital/Police/
  PoliceShooting/Shop), 레거시 Farm 등. Town·실내는 LDtk 32px 타일.
- 그림: Modern 계열 픽셀아트 팩(Assets/Modern_*, Assets/moderninteriors-win), 직접 만든 그림은 FEVM ComfyUI 파이프라인.
- 현지화: Assets/Localization/GameTextCatalog.asset (ko/en).
- 세이브: 슬롯별 JSON 파일(SaveService).
- 검증: Unity 컴파일·테스트는 FEVM(Ubuntu, Strix Halo)에서 커밋 해시 기준으로 돌린다(.claude/rules/strix-verification.md).
  Unity 에디터 테스트가 수 분, 메모리 4~10GB를 쓰는 것이 걷어내려는 이유 중 하나다.

## 먼저 읽을 것
- AGENTS.md (헌법급 원칙 1~13)
- .claude/rules/core-logic-and-play-evidence.md (엔진 무관 규칙 원칙, 실제 플레이 근거 보고)
- .claude/rules/testing-discipline.md, .claude/rules/strix-verification.md
- docs/superpowers/audits/2026-11-04-full-design-status.md (기능별 구현 현황)
- docs/story/ 아래 기획 문서 3개

## 꼭 다뤄 줬으면 하는 것
- 후보 비교: 예를 들어 Godot(C#), MonoGame/FNA, 자체 엔진(C# + SDL 등), 기타. 2D 픽셀아트, Tilemap/LDtk, UI,
  Steam 친구 방 멀티플레이, Windows 빌드, 테스트 자동화(실제 입력 E2E) 기준으로.
- 3806개 SO 데이터를 어떤 형식으로 옮길지(그리고 Unity 없이 편집할 방법).
- 순수 C# 규칙 코드를 얼마나 재사용할 수 있는지, 지금 Rootborn.Game에서 화면과 무관한 규칙이 얼마나 되는지.
- 옮기는 동안 게임이 계속 플레이 가능한 상태를 유지할 방법(한 번에 갈아엎기 vs 단계적 이전).
- 실제 입력 E2E와 스크린샷 검증을 새 환경에서 어떻게 할지.
- 바뀌는 헌법·규칙 문서(Unity 전제 조항)를 어떻게 정리할지.

## 지켜야 할 것
- 항상 한국어로 답한다.
- 사용자의 미커밋 변경을 commit·stash·정리하지 않는다. 사용자 세이브를 쓰지 않는다.
- 비밀번호·개인키를 출력하거나 요청하지 않는다. 호스트 키 검사를 끄지 않는다.
```
