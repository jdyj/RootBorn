# 그림 요청 목록

구현 중 필요한 그림을 모아 두는 곳이다. 그림은 사용자가 Strix Halo(FEVM)에서 만든다. 항목을 만들면 그 아래에 결과 파일 경로와 날짜를 적고 상태를 바꾼다. 구현 쪽은 그림이 없어도 동작하도록 데이터 슬롯과 기본값을 먼저 만든다.

상태: `요청` → `제작 중` → `들어옴`(게임에 연결 전) → `연결됨`

## 공통 규칙
- 픽셀아트. Modern 계열(Modern Exteriors/Interiors/UI) 톤과 맞춘다. Pixelwood 톤은 쓰지 않는다.
- 월드 타일과 소품: 32px 격자(Town LDtk가 32x32). 캐릭터는 성장 캐릭터 시트 형식을 따른다.
- 투명 배경 PNG. 그림자는 가구 규칙과 같이 넣지 않는다(그림자 없는 Modern Interiors 기준).
- 원본과 프레임별로 나란히 비교해 결함을 확인한 뒤 넘긴다.

## 1. 착용 칸 캐릭터 (설계: `docs/superpowers/specs/2026-11-07-wearable-character-design.md`)
시트 형식: 지금 성장 캐릭터와 같이 4행(아래·옆·위·옆 반대 등 기존 행 순서) × 6열 프레임. 성장 단계 3개(어릴 때 `playground`, 학교 `school`, 어른 `adult`)마다 몸 크기가 달라서 착용품도 단계별로 한 장씩 필요하다. 참고 원본: `Assets/Art/Characters/Growing/`의 `body_*`, `head_*`, `outfit_*`.

| 칸 | 필요한 것 | 비고 | 상태 |
|---|---|---|---|
| 얼굴 | 표정·눈 모양 몇 종(단계별 3장씩) | 사용자 취향: 큰 어두운 눈 대신 작고 대칭인 점 눈 | 요청 |
| 머리 | 짧은 갈색 외 머리 모양 여러 종(단계별) | 지금은 짧은 갈색 1종뿐 | 요청 |
| 상의 | 지금 옷 7벌 외 추가 옷(단계별) | 상하의를 나누려면 하의 칸도 함께 | 요청 |
| 하의 | 상하의 분리할 때만 | 선택 | 요청 |
| 모자 | 모자 여러 종(단계별) | 모자가 머리 윗부분을 가리는지 표시 | 요청 |
| 액세서리 | 안경, 가방 등(단계별) | 칸을 더 나눌지 정하면 데이터로 추가 | 요청 |

## 2. UI
| 대상 | 필요한 것 | 지금 상태 | 상태 |
|---|---|---|---|
| 목표 저널 "보상 받기" 버튼 | 290x60 버튼, 일반·호버·눌림 3상태(9-slice 가능). 쓰는 곳: `Assets/Scripts/UI/Objectives/ObjectiveJournalPanel.cs`의 `ObjectiveDetailActionButton`(완료·미수령 퀘스트 상세) | 2026-11-07: '목표 추적' 버튼과 같은 공용 Style2/도시형 몰딩 버튼(`ModernUiRecipes.CommonPanel`) + 글자 '보상 받기'를 임시 사용. 화면: `output/quest-fixes-20261107/quest-wood-e2e-001-journal-claim-button.png` | 요청 |
| 캐릭터 생성 화면 | 칸별 선택 화살표, 미리보기 배경 | 단색 사각형과 텍스트 화살표 | 요청 |
| 옷장 패널 | 칸별 탭 아이콘(얼굴·머리·상의·모자·액세서리) | 착용 칸 구현 때 필요 | 요청 |

## 3. 월드 (Town·건물)
| 대상 | 필요한 것 | 지금 상태 | 상태 |
|---|---|---|---|
| 마을 나무 자원 지점 | 128x128(4x4칸, 32px 격자) 나무 3상태: 베기 전·맞는 중(흔들림 2~3프레임)·벤 뒤(그루터기). 밑동 기준점이 아래 10% 높이. 쓰는 곳: `Assets/Scenes/Town.unity` 루트 `TownResourcePoints`의 `WoodResourcePoint_01~03/Visual`(SpriteRenderer), 데이터 `Assets/Data/Resources/Resource_Tree.asset` | 2026-11-07: Modern Exteriors `ME_Singles_Camping_32x32_Tree_1.png`(Sprite, PPU 32) 1장만 사용. 맞는 중·벤 뒤 그림이 없어 다 베면 나무가 사라진다. 화면: `output/quest-fixes-20261107/quest-wood-e2e-001-town-trees.png` | 요청 |
| 나무(아이템) 아이콘 | 16x16 장작/통나무 인벤토리 아이콘 1장. 쓰는 곳: `Assets/Data/Items/Item_Wood.asset`의 `_icon` | 2026-11-07 확인: Modern Farm `Pickup_Fishing_Branch_16x16.png`(나뭇가지)를 쓰고 있어 '나무'로 읽히기 어렵다 | 요청 |

## 4. 실내·가구
| 대상 | 필요한 것 | 지금 상태 | 상태 |
|---|---|---|---|
| 집 꾸미기 퀘스트 가구 | 방에 놓을 소품 3~6종(그림자 없는 Modern Interiors 기준, 1~2칸). 쓰는 곳: `Assets/Data/Quests/Objectives/Objective_DecorateHouse.asset`의 `_acceptedFurniture` | 2026-11-07: 오프닝 '내 방 만들기'(`Objective_MyRoom`)와 같은 6종(사진판·공부책상·장난감 주방·구급 놀이 세트·조립 책상·훈련 타이어)을 그대로 씀. 새 게임 팔레트 첫 장에는 훈련 타이어만 보여 E2E가 타이어 3개를 놓는다 | 요청 |

## 추가 방법
구현하다가 그림이 필요하면 해당 표에 한 줄을 추가한다: 대상, 크기·프레임·상태 수, 어디에 쓰는지(파일·데이터 경로), 지금 임시로 무엇을 쓰는지.
