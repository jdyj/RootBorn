# Objective Journal UI Rule

목표성 UI는 즉흥적으로 Canvas와 좌표를 만들어 붙이지 않는다. 목표, 퀘스트, 캠페인, 진로 힌트처럼 "플레이어가 지금 무엇을 하면 되는가"를 설명하는 UI는 `Tab`으로 여는 `Rootborn.UI.Objectives.ObjectiveJournalPanel`을 기본 소유자로 한다.

## 필수 규칙

- `Tab`은 Objective Journal 토글 입력이다. 기존 status/book UI가 필요하면 다른 입력 또는 Objective Journal 내부 보조 페이지로 배치한다.
- 플레이 화면에 항상 떠 있는 목표성 HUD는 `Rootborn.UI.Objectives.TrackedObjectiveHud` 하나만 사용하고, 추적 중인 목표 1개만 표시한다.
- 신규 goal/quest/campaign/hint UI는 각자 Canvas, 좌표, 크기를 직접 들고 상시 패널을 만들지 않는다.
- 상세 목록, 설명, 진행도, 보상/완료 액션은 Objective Journal의 category rail + list + detail 슬롯으로 공급한다.
- 목표성 UI 배경과 반복 row는 `ModernUiTileImage + ModernUiRecipes.CommonPanel` 기반 Style2 패널 규약을 따른다.
- provider/adapter는 SO와 진행 상태를 표시 모델로 변환할 뿐, `questId`, `campaignId`, `milestoneId` 등 엔티티 ID별 C# 분기를 만들지 않는다.

## 허용 예외

- 대화창, 결과창, 보상 실패 알림처럼 사용자의 즉시 행동 결과를 보여주는 일회성 modal/toast는 별도 패널일 수 있다.
- 단, 그 내용이 추적 가능한 목표 목록이나 캠페인 상세로 확장되는 순간 Objective Journal provider로 옮긴다.

## 구현 기준

- Shell: `Rootborn.UI.Objectives.ObjectiveJournalPanel`
- Compact HUD: `Rootborn.UI.Objectives.TrackedObjectiveHud`
- Input owner: `Rootborn.UI.Modern.ModernUiPanelInputRouter`
- Install path: `Rootborn.UI.Modern.ModernUiPanelAutoInstaller`
- Design spec: `docs/superpowers/specs/2026-05-13-objective-journal-ui-design.md`
