---
name: pixel-kit-room
description: "ROOTBORN 실내 방·집 구조를 칸마다 벽/바닥/밖만 정해 만들면 벽 그림(32x32)과 걸을 수 있는 칸(Collision)이 함께 든 LDtk 방이 나오는 절차. '실내 방 만들기', '집 구조 만들어 줘', '벽', '방 구조', '걸을 수 있는 칸', '벽이라서 이동하면 안 되는 곳', 'LDtk 방', 'Collision 칠하기', 'room build', 'room repaint', 'room check' 요청에 사용. 가구·소품·건물 외관 그림은 pixel-kit, 바깥 맵은 이 스킬이 아니다."
---

# 방 구조 만들기 — 칠하면 벽 그림과 충돌이 같이 나온다

도구는 `Tools/pixel-kit/`(명령·검사 코드·파일 형식은 `Tools/pixel-kit/README.md`의 "방 만들기" 세 절), 설계는 `docs/superpowers/specs/2026-11-13-room-builder-design.md`, 게임이 읽는 층·충돌 약속은 `game/world/README.md` 3절. 이 스킬은 순서와 판단만 적는다. 명령 옵션과 형식은 README를 읽는다.

핵심: 칸 하나(1 벽, 2 바닥, 0 밖)가 **그림과 충돌의 하나뿐인 원본**이다. 같은 격자에서 `Wall`·`Ground` 타일과 `Collision`이 함께 나오므로 둘이 어긋나지 않는다.

## 쓰지 않을 때

- 가구·소품·건물 외관·타일 그림: `pixel-kit`. UI 테두리: `ui-nine-slice-skin`.
- 지금 집 맵(`House`)을 이 방식으로 바꾸기: 사용자가 정하기 전에는 하지 않는다(아래 "하지 않는다").

## 순서

1. **평면을 쓴다**(`art/kit/rooms/<방 id>.json`, 본보기 `test.two-rooms.json`). 글자 줄 `plan`(`#` 벽, `.` 바닥, 빈칸 밖), `wall`(벽 모양: 지금 `paper`·`plaster`), `floor`(키트의 32x32 타일 명세 id — `kind: tile`이고 32x32로 그려지는 명세면 받는다. 본보기 평면은 `bakery.tile.floor`), `spawn`([x, y] 칸). 방 사이는 벽 줄을 끊은 문간(바닥 칸)으로 잇는다. 벽 두께는 한 칸이고 가로 벽은 3칸 이상 두꺼워야 윗면이 보인다(`room-wall-thin` 알림). 문 글자(`doors`)는 써도 되지만 아래 "하지 않는다"의 문 표를 본다.
2. **`python Tools/pixel-kit/kit.py room check <방 id>`**로 평면부터 검사한다(쓰지 않는다). 문제(열린 가장자리, 닿지 못하는 바닥, 벽 안의 도착 지점)는 평면을 고쳐서 푼다.
3. **`room build <방 id>`**(`--dry-run`으로 쓸 파일 먼저). 결과는 `art/world/rooms/<방 id>/`의 `<방 id>.ldtk`, `tiles.png`, `tiles.slots.json`, `maps.json`. 맵 id는 방 id를 PascalCase로(`test.two-rooms` → `TestTwoRooms`), 도착 지점은 `<맵 id 소문자>-entry`.
4. **게임에서 본다**: `dotnet run --project game/Rootborn.Host -- --world <맵 id> --maps art/world/rooms/<방 id>/maps.json --size 1920x1080`. 벽에 막히는지, 문간을 지나는지 걷고, F12 스크린샷을 **직접 열어** 겹친 그림·잘린 타일·빈 칸을 본다(스킬 `host-play-test`로 장치 이벤트 주입도 된다).
5. **LDtk에서 고친 뒤**: 편집기에서 `Collision` 층만 칠한다(1 벽, 2 바닥). 저장한 뒤(편집기에 파일이 열린 채로 `repaint`하면 편집기가 나중에 저장할 때 옛 벽 그림으로 덮는다 — 편집기에서 저장하고 닫은 뒤, 또는 `repaint` 뒤 편집기에서 다시 불러온 뒤 쓴다) `room check <.ldtk 경로>`(어긋난 칸 수가 나온다) → `room repaint <.ldtk 경로>`(`--dry-run` 먼저)로 벽·바닥 그림을 칠한 칸에 맞춘다. `repaint`는 `Wall`·`Ground` 타일과 시트만 바꾸고 `Collision`·`Door`·`Furniture`와 엔티티는 그대로 둔다. 다시 `room check`, 4번.
6. **가구는 사람이 `Furniture` 층에 놓는다**(태그 `ysort`, 비어 있게 만들어진다). 도구는 가구를 놓지도 지우지도 않는다.

## 하지 않는다

- `Wall`·`Ground` 층을 LDtk에서 손으로 칠하기. 다음 `repaint`가 `Collision`대로 덮는다. 구조는 `Collision`으로만 고친다.
- LDtk에서 손으로 고친 뒤에 `room build --force`. 평면에서 통째로 다시 만들어 다시 칠한 칸·놓은 가구·고친 문을 지운다. 손 고친 뒤의 길은 `repaint`다.
- 지금 집 맵 `House`를 바꾸기. 크기가 바뀌면 옛 세이브의 가구 자리가 걸린다. 사용자가 정하면 스킬 `save-compat-change`를 먼저 따른다. 지금은 시험 방(`art/world/rooms/test.two-rooms/`)으로만 확인한다.
- 벽의 옆으로 기운 면 그리기, 걸어서 지나는 "벽에 박힌 문" 만들기. 이번 도구에 없다(방 사이는 문간).
- 평면에 문 글자를 쓰고 끝내기. `Door` 엔티티는 놓이지만 만들어진 `maps.json`의 `doors` 표가 비어 있어 손으로 목적지를 채우기 전에는 게임에서 그 문이 열리지 않는다(알림 `room-door-unregistered`).
- LDtk 편집기에서 레벨을 따로 저장(`externalLevels`)하게 하기. `repaint`가 거절한다.
- 벽·바닥 종류나 벽 윗면 색을 묻지 않고 정하기. 지금 고르는 중이다(견본 W1~W8, F1~F9). 고른 것이 도구에 들어오기 전에는 벽은 `paper`·`plaster`뿐이고, 바닥은 키트의 32x32 타일 명세 id(`kind: tile`) 중에서 고른다(본보기는 `bakery.tile.floor`).
- 검사를 끄거나 `--force`로 문제를 덮기. 도구 코드를 이 스킬 안에서 고치기(도구는 별도 일이다).

## 아직 확인하지 못한 것

**편집기에서 처음 열었을 때 실패했다(2026-11-14).** 사용자가 LDtk로 `room build`의 파일을 열자 `Cannot read properties of null (reading 'externalLevels') (TypeError)`가 났다 — 도구가 게임이 읽는 키만 쓰고 편집기가 기대하는 키를 많이 빠뜨렸다. 공식 스키마(1.5.3, `Tools/pixel-kit/tests/fixtures/ldtk-1.5.3/`)의 필수 키와 편집기 저장 파일에 늘 있는 키를 모두 채우고 테스트(`tests/test_ldtk_editor_shape.py`)로 고정했다. 그 전에 만든 방 파일은 `room check`가 `room-ldtk-incomplete`로 알리고 `repaint`가 거절한다 — `room build <id> --force`로 다시 만든다. **편집기에서 실제로 열리는지는 다시 확인 대기다.** 도구 테스트는 편집기가 저장한 모양을 흉내 낸 고정 파일(`Tools/pixel-kit/tests/fixtures/room-saved-by-editor.ldtk`)로만 했다. 모르는 것: (1) 다시 만든 `.ldtk`가 편집기에서 열리는가, (2) 편집기가 저장한 진짜 파일을 `repaint`가 받는가, (3) `repaint`가 다시 쓴 파일을 편집기가 다시 여는가. 사용자가 처음 해 볼 때 함께 확인하고, 확인되면 이 절을 지운다. 그 전에는 "편집기에서 된다"고 쓰지 않는다. 확인 순서 초안은 이 스킬이 아니라 사용자와 정한다(시험 방 `art/world/rooms/test.two-rooms/test.two-rooms.ldtk`로, 끝나면 `git checkout -- art/world/rooms/test.two-rooms`).

## 보고

근거 종류를 나눠 쓴다(`.claude/rules/core-logic-and-play-evidence.md`). 도구 테스트·`room check`는 "자동 테스트", 게임에서 걸은 것은 입력 방식을 붙여 "장치 이벤트 주입", 스크린샷은 직접 연 경로(`D:\…` 전체 경로). 편집기에서 열거나 칠한 것은 사용자만 확인할 수 있다.
