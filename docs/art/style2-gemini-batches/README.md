# Style2 Gemini 배치 누적

`Modern_UI_Style_2.png` (34행 × 49열 = 1666 sprites)을 Gemini로 분류한 결과를 행 범위별 배치 파일로 누적.

배치당 출력 한도가 약 5행이라 7배치로 분할.

| 배치 | 행 범위 | 받은 객체 / 영역 (49 × N) | 상태 | 파일 |
|---|---|---|---|---|
| 1 | r0–r5 | 216 / 294 (빈 셀 일부 포함) | done | `batch-01-r0-r5.json` |
| 2 | r6–r10 | 79 / 245 (콘텐츠 셀만) | done | `batch-02-r6-r10.json` |
| 3 | r11–r15 | 88 / 245 (콘텐츠 셀만) | done | `batch-03-r11-r15.json` |
| 4 | r16–r20 | 41 / 245 (콘텐츠 셀만; r18~r20 sparse) | done | `batch-04-r16-r20.json` |
| 5 | r21–r25 | 30 / 245 (sparse; status/toggle 카테고리 등장) | done | `batch-05-r21-r25.json` |
| 6 | r26–r30 | 34 / 245 (buttonMatrix 21개 첫 등장) | done | `batch-06-r26-r30.json` |
| 7 | r31–r33 | 21 / 147 (panel 모서리 + buttonMatrix) | done | `batch-07-r31-r33.json` |

**누적: 509 / 1666 (30.6%) — 콘텐츠 셀 식별 완료. 빈 셀 1157개는 머지 시 자동 unknown 채움.**
좌표 중복 0건 검증 완료.

## 빈 셀 정책 (길 B)

Gemini는 콘텐츠 있는 셀만 출력. 누락된 좌표는 머지 시점에 자동으로 다음 형태로 채움:

```json
{
  "row": R,
  "column": C,
  "coordinate": "rR_cC",
  "spriteName": "ModernUI_16_Style2_rR_cC",
  "semanticId": "unknown.rR.cC",
  "enumName": "UnknownRRCC",
  "category": "unknown",
  "stateGroupId": null,
  "stateRole": null,
  "confidence": "unknown",
  "note": "Auto-filled empty cell (not emitted by Gemini)."
}
```

## 머지 절차 (모든 배치 수령 후)

1. 7개 배치를 단일 JSON 배열로 합치기.
2. 누락 좌표(`r0_c0` ~ `r33_c48` 중 결손)를 길 B 정책에 따라 unknown으로 자동 채움.
3. 누락 필드 보강: 일부 항목에서 `spriteName` / `stateGroupId` / `stateRole`이 빠진 경우 패턴 재계산 또는 null로 보충.
4. enumName 1666개 유일성 검증.
5. `docs/art/modern-ui-style2-icon-catalog.json` 백업 후 머지. 기존 confirmed 라벨(`panel.base`, `ribbon.items` 등 alias)과 Gemini 시각 분류(`panel.cornerTopLeft` 등) 충돌 시 정책 결정 필요.
6. `ModernUiStyle2IconCatalogTests` + `Scripts/ci/check-no-entity-id-branching.sh` 회귀 통과 확인.
