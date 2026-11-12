# 임시 문구와 가구 이름 — 사용자 결정 대기 (2026-11-13)

사용자 지시(2026-11-13): "문구랑 이름 우선 너가 임의로 넣어줘". 아래는 **나중에 사용자가 고칠 임시 값**이다. 기획 결정이 아니다.

## 넣은 것

| 무엇 | 개수 | 어디에 |
|---|---:|---|
| 빠진 문구(`docs/superpowers/audits/2026-11-12-missing-text.md`의 83개) | 83 | `game/localization/ko.json`, `en.json` 끝 |
| 가구 이름 | 1,501 | 같은 두 파일 끝(`data.<stableId>.name`) |

- 문구 83개는 키 이름을 그대로 두고(영어 문장 키 57개 포함) 문구만 넣었다. `known-missing-localization-keys.json`은 비었다(`LocalizationKeyTests`).
- 엄마의 첫 대사 `dialogue.firstNpc.greeting`은 호칭 없이 "아침에 깨우며 마을에 나가 보라"는 두 줄 이내 문구다. 첫 안내인이 누구인지는 스토리에서 정해지지 않았다. 테스트가 "엄마·아빠·부모"가 안 들어갔는지 본다.
- 이름에 `test`가 든 시험용 정의의 키도 문구를 넣었다("도서관에서 관심 찾기" 등).

## 가구 이름을 만든 규칙

화면은 이미 이름 키 → `data.<stableId>.name` → `displayName` 순으로 보여 주므로 코드와 `data/`는 고치지 않고 문구만 더했다(`id`·`stableId`·`category` 그대로).

- 한국어 이름이 있던 28개: 그 이름이 ko, en은 손으로 단 뜻. 이름 키가 이미 있는 2개(`story.photo-board`, `story.study-desk`)는 그 문구를 그대로 쓴다.
- 영어 이름뿐이던 13개(`chair.*`, `desk.*`, `comfy31.*`, `school.archive30.*`): 손으로 ko·en을 지었다.
- 그림 시트 이름뿐이던 1,460개: `<분류 이름> <번호>`. 번호는 분류 안에서 `stableId` 끝 번호 순으로 1부터. 시트 이름에는 분류 말고 쓸 만한 낱말이 없었다(`Singles`·`Shadowless`·`48x48`과 번호뿐).

| 분류 | ko | en | 개수 |
|---|---|---|---:|
| Bedroom | 침실 가구 | Bedroom piece | 555 |
| Kitchen | 주방 가구 | Kitchen piece | 408 |
| Bathroom | 욕실 가구 | Bathroom piece | 158 |
| Japanese | 일본풍 가구 | Japanese-style piece | 131 |
| LivingRoom | 거실 가구 | Living room piece | 122 |
| Condominium | 아파트 가구 | Condominium piece | 86 |

시험: Screens `FurnitureNameTextTests`(모든 언어에 문구, 시트 이름 모양 아님, 한 언어 안에서 겹치지 않음).

## 고치는 곳

- 문구: `game/localization/ko.json`, `en.json`. 같은 키가 `data/localization`에 있으면 `game/localization`이 이긴다.
- 가구 이름 키 모양: `data.<stableId를 소문자로, _와 공백은 ->.name`(예: `data.desk.basic.name`). 이름을 바꿔도 세이브는 가구 id를 가리키므로 영향이 없다.

## 사용자가 정할 것

1. 엄마(첫 안내인)의 첫 대사와 호칭. 지금은 호칭 없는 임시 문구다.
2. 시험용 퀘스트 체인(`questchain.test.*`, 이름에 `test`)을 퀘스트 체인 기록에서 숨길지, 지금처럼 임시 이름을 보일지.
3. 가구 이름을 하나하나 지을지, 지금처럼 `분류 번호`로 둘지(그림 시트 번호와 순번은 같지 않다 — 그림이 바뀔 예정이라 번호는 그림 번호가 아닌 순번이다).
4. 키가 영어 문장인 57개를 `{영역}.{이름}` 키로 바꿀지(`data/`와 변환 원본을 고쳐야 함).
