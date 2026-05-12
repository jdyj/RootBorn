# Quest System Run Prompt

새 Codex/Claude goal 세션이 열리면 아래 프롬프트를 그대로 붙여 넣는다.

```text
docs/superpowers/goals/2026-05-06-quest-system-goal.md 파일을 읽고, 그 goal을 기준으로 ROOTBORN 퀘스트 시스템 작업을 진행해줘.

진행 방식:
1. AGENTS.md와 .claude/rules/* 규칙을 먼저 확인한다.
2. 퀘스트 goal 파일의 절대 규칙, 목표, 필수 테스트, 완료 조건을 그대로 따른다.
3. 구현 전에 반드시 브레인스토밍/설계 스펙을 작성하고 사용자 승인을 받는다.
4. 승인 후에는 TDD로 진행한다. 실패 테스트 작성 -> 실패 확인 -> 최소 구현 -> 관련 테스트 재실행 순서를 지킨다.
5. Assets/**/*.cs는 디스크 직접 쓰기 금지다. C# 신규/수정은 Unity MCP script-update-or-create 또는 Unity Editor 안전 경로만 사용한다.
6. 퀘스트/NPC/대화/보상/스토리 트리거는 ScriptableObject 데이터 기반으로 설계한다.
7. questId/npcId/resourceId/toolId/cropId 등 엔티티 ID별 if/switch/enum 분기는 만들지 않는다.
8. 기존 테스트와 기존 사용자 변경분을 깨거나 되돌리지 않는다.

첫 응답에서는 바로 구현하지 말고 다음을 보고해줘:
- 현재 퀘스트/대화/상호작용/인벤토리/Knowledge 관련 코드와 테스트를 조사할 계획
- goal에서 확인한 핵심 요구사항
- 설계 전에 확인해야 할 질문이 있다면 한 번에 하나씩 질문

완료 보고에는 반드시 수정 파일, 추가 SO/데이터, 추가 테스트, 실행한 테스트, 통과/실패 결과, 남은 리스크를 포함해줘.
```

## Reference

- Goal: `docs/superpowers/goals/2026-05-06-quest-system-goal.md`
