# 목표 수첩 — 목표성 UI의 주인

"플레이어가 지금 무엇을 하면 되는가"를 알려 주는 UI(목표, 퀘스트, 캠페인, 진로 힌트, 일과 안내)는 목표 수첩이 맡는다. 왜: 목표마다 따로 상시 패널을 띄우면 화면이 덮이고 같은 정보가 여러 곳에서 어긋난다.

## 규칙

- **목표 수첩**: `Rootborn.Screens.Objectives.ObjectiveJournalScreen`. `Tab`으로 연다. 큰 패널 층이고 이동을 막지 않는다(걸으면서 본다). 분류 줄 + 목록 + 상세 + 추적·행동 단추.
- **추적 목표 HUD**: `Rootborn.Screens.Hud.TrackedObjectiveHudScreen` 하나만 늘 떠 있고, 수첩에서 추적한 항목 **하나만** 보인다. 추적한 것이 없으면 숨는다.
- 새 목표성 내용은 새 화면이나 HUD를 만들지 말고 **모으는 쪽**(`IObjectiveJournalSource`) 클래스 하나를 쓰고 `ObjectiveJournalSourceCatalog.Register`에 한 줄 더한다. 모으는 쪽은 `GameSession` 조회로 항목을 만든다.
- 글자는 키와 값(`JournalText`)으로만 넘긴다. 문구를 모으는 쪽에서 이어 붙이지 않는다(`rules/localization.md`).
- 모으는 쪽은 퀘스트·캠페인·마일스톤 id로 분기하지 않는다. 데이터를 표시 모델로 바꿀 뿐이다.
- 수첩은 목록이 길어도 보이는 줄만 그린다.

## 따로 둬도 되는 것

- 대화, 하루 결과, 보상 실패 알림처럼 방금 한 행동의 결과를 한 번 보이는 모달·알림.
- 단, 그 내용이 나중에 다시 볼 목표나 진행 기록이 되면 수첩의 모으는 쪽으로 옮긴다.
- 퀘스트 체인 기록(`QuestChainLogScreen`)은 Unity 판에도 있던 별도 기록 화면이라 그대로 둔다(여는 키 없음).
