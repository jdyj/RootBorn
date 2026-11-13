# LDtk 1.5.3 기준 파일

픽셀 키트가 쓰는 `.ldtk`가 LDtk 편집기가 기대하는 꼴인지 보는 테스트(`tests/ldtk_schema.py`, `tests/test_ldtk_editor_shape.py`)가 읽는다.

## `JSON_SCHEMA.json`

- 출처: LDtk 공식 JSON 스키마 <https://ldtk.io/files/JSON_SCHEMA.json> (2026-11-14 받음, 파일 안 `"version": "1.5.3"`).
  SHA-256 `b96ca8a106d12c5bf8aa42da223a4ceb02e93dcfbb0d1b49fbbaeb9959264bda` — **저장소에 든 바이트(줄바꿈 LF) 기준**이다
  (`git show HEAD:Tools/pixel-kit/tests/fixtures/ldtk-1.5.3/JSON_SCHEMA.json | sha256sum`). Windows 작업 사본은 줄바꿈이 CRLF로 바뀌어
  해시가 다르다(`2aa84b0d…`). 내용은 고치지 않았다.
- 라이선스: LDtk(<https://github.com/deepnight/ldtk>)는 MIT 라이선스다. 저작권 Sébastien Bénard(deepnight).
  스키마는 LDtk가 만들어 배포하는 파일이고 이 저장소에서는 테스트 기준으로만 쓴다.
- 쓰는 법: 정의마다 `required`에 든 키가 빠짐없이 있고 형(`type`, `oneOf`, `enum`, null 허용)이 맞는지 재귀로 본다.

## `editor-keys.json`

- 저장소의 편집기 저장 파일(옛 집 맵 — 지금은 이 폴더의 `house.ldtk`, `Assets/LDtk/Town/town.ldtk`와 그 `.ldtkl`,
  `Assets/LDtk/Interiors/interiors.ldtk`와 그 `.ldtkl` — 모두 `jsonVersion` 1.5.3, `appBuildId` 473703)에서
  정의 종류마다 **모든 사례에 늘 있는 키**(교집합)를 뽑은 표다. 스키마의 `required`보다 넓다(편집기는 선택 키도 늘 쓴다).
- 그 맵들은 새로 그릴 예정이라 테스트가 맵 파일을 직접 읽지 않고 이 표를 읽는다. 표를 다시 뽑을 일이 생기면 같은 규칙
  (종류별 교집합)으로 뽑고 이 README의 출처를 고친다.

## `house.ldtk`

- 2026-11-14까지 게임의 집 맵이던 `Assets/LDtk/House/house.ldtk`(LDtk 1.5.3, `appBuildId` 473703이 저장)를 바이트 그대로 옮긴 것이다.
  게임의 집이 키트 방(`art/world/rooms/house/`)으로 바뀌며 원래 자리에서 지웠다. **편집기가 저장한 견본**으로만 쓴다:
  스키마 검사가 진짜 편집기 파일에서 오탐하지 않는지(`test_ldtk_editor_shape.py`), 방 모양이 아닌 편집기 맵을 `room build --force`가
  덮지 않는지(`test_room_ldtk.py`). 타일셋 PNG 경로(`relPath`)는 옛 자리 기준이라 이 폴더에서는 풀리지 않는다 — 그림은 읽지 않는다.
  고치지 않는다.
