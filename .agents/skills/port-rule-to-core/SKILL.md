---
name: port-rule-to-core
description: "ROOTBORN의 Unity 게임 규칙 한 영역을 엔진 무관 Rootborn.Core로 옮긴다. '규칙 옮기기', 'Core로 옮겨', '<영역> 규칙 이식' 요청이나 Assets/Scripts/Game의 판정·계산·진행 상태 코드를 MonoGame 판에서 쓰게 만들 때 사용."
---

# 규칙 한 영역을 Rootborn.Core로 옮기기

Unity 판은 얼렸다(태그 `unity-playable-20261111`). `Assets/` 아래는 읽기만 한다. 규칙은 Unity 코드를 읽고 `Rootborn.Core`에 같은 동작으로 새로 쓴다. 설계: `docs/superpowers/specs/2026-11-11-core-rules-extraction-design.md`, 규칙: `.claude/rules/engine-migration.md`.

## 본보기

원본 `Assets/Scripts/Game/StudentLife/StudentLifeCore.cs` ↔ 옮긴 것 `Packages/com.rootborn.core/Runtime/StudentLife/*.cs`, 테스트 `Tools/Rootborn.Core.Tests/StudentLifeActivityTests.cs`. 나란히 보면 방식이 보인다.

## 순서

1. 영역 폴더를 훑어 파일을 넷으로 나눈다: 진행 상태·실행기(옮김), 정의 SO(값만 가진 Core 클래스로), 전략 SO(값을 생성자로 받는 Core 클래스로), MonoBehaviour·씬·UI(옮기지 않음).
2. `Assets/Tests/EditMode`에서 그 영역의 규칙 테스트를 찾는다. 에셋·씬·등록부·UI를 검사하는 테스트는 뺀다.
3. 테스트를 먼저 옮긴다: SO 만들기(`CreateInstance` + `ConfigureForTests`) → 생성자 호출, SO 참조 → id 문자열. 파일 머리에 `// 옮겨 옴: <원래 경로>` 한 줄. `dotnet test`로 컴파일 실패를 확인한다.
4. Core 구현을 쓴다. 통과를 확인한다.
5. 원래 테스트가 안 다루던 경계를 더한다: 거절 시 무변경, 중복 요청, 빈 id와 null, 세이브 왕복.
6. 묶음마다 커밋한다(`[REFACTOR]`, 한글).
7. `game/Rootborn.Data/Builders`에 그 영역의 만들기 함수를 등록하고(JSON `type` 하나에 `b.Add` 한 줄) `NotPortedYet`에서 뺀다. `dotnet test game/Rootborn.sln`의 `RealDataTests`가 실제 `data/` 전체를 오류 없이 만드는지 본다.
8. 세이브 파일이 있으면 `ISaveStore`와 `SaveFileNames`(`Rootborn.Core.Save`)를 쓰고, 맨 위 모양을 `game/tests/Rootborn.Data.Tests/SaveShapeCoverage.cs`에 등록한다.

## 지킬 것

- 그대로 베끼지 않는다. 구조는 고치고(전역 상태 제거, 섞인 책임 나누기), 분명한 버그와 빠진 검사는 고친다. Unity 판과 달라진 동작은 테스트로 고정하고 커밋 메시지와 설계 문서 9절 "Unity 판과 달라진 동작"에 한 줄씩 적는다.
- 확장하기 쉽게: 새 전략 종류는 클래스 하나와 등록 한 줄이면 되게 한다. 종류를 enum이나 switch로 닫지 않는다. 쓰이지 않는 확장점은 만들지 않는다.
- 기획(수치, 콘텐츠, 진행 순서)은 바꾸지 않는다. 바꿔야 할 것 같으면 보고에 적어 묻는다.
- 엔티티는 id 문자열로만 가리킨다. 엔티티 id로 분기하지 않는다. 전략 종류마다 클래스 하나.
- 세이브 데이터 클래스는 필드 이름과 형을 그대로 둔다(옛 세이브를 읽어야 한다).
- 규칙이 엔진·전역 상태에 닿던 자리(SaveService, 레지스트리, GameClock, Debug.Log)는 값이나 인터페이스로 받는다.
- 아직 안 옮겨진 다른 영역의 타입이 필요하면 그 영역을 옮기지 말고 최소 인터페이스를 두고 보고에 적는다.
- Core 제약: `Packages/com.rootborn.core/Runtime/<영역>`, 네임스페이스 `Rootborn.Core.<영역>`, LangVersion 9(파일 범위 네임스페이스·record·global using 금지), `UnityEngine`·`System.Text.Json` 금지, `Mathf` 대신 `System.Math`나 `Rootborn.Core.Common.CoreMath`.
- 새 `.cs`와 새 폴더마다 `.meta`: 파일은 `fileFormatVersion: 2`와 `guid: <새 32자리 16진수>` 두 줄. 폴더는 `Packages/com.rootborn.core/Runtime/Quests.meta`와 같은 모양.

## 명령

```
C:\Users\jdyj\.dotnet\dotnet.exe test Tools/Rootborn.Core.Tests
C:\Users\jdyj\.dotnet\dotnet.exe test game/Rootborn.sln
```

## 보고에 적을 것

옮긴 것과 테스트 건수, 옮기지 않은 테스트와 이유, 다른 영역 대신 둔 인터페이스, 고친 버그와 달라진 동작, 기획 판단이 필요해 묻는 것, 확인하지 못한 것. 이 통과는 전부 "자동 테스트"다.
