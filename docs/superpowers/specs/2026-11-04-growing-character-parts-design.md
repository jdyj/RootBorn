# 성장하는 캐릭터 파츠 설계

작성일: 2026-11-04
상태: 사용자 승인 대기

## 1. 의도

같은 아이가 놀이터를 뛰어다니다 학교에 다니고, 졸업 후 회사·주방·인테리어 현장으로 간다. 이 성장이 그림으로 보여야 하고, 스타듀밸리처럼 옷을 자유롭게 갈아입을 수 있어야 한다. 옷 아래에도 얼굴, 팔, 손, 다리 같은 몸이 항상 존재해야 한다. 옷을 바꿀 때 몸이 비거나 달라 보이면 실패다.

이 작업은 Strix Halo의 ComfyUI로 베이스 파츠와 옷 파츠를 만들고, 프롬프트와 생성 이력을 재현 가능하게 남긴다. 이번 범위의 결과물은 검수 가능한 아트 팩이며, 게임 런타임 연결은 팩 승인 뒤의 별도 작업이다.

이 문서는 기존 계획서의 "새 캐릭터 이미지 제작 금지"를 2026-11-04 사용자 요청에 따라 대체한다. 환경·건물 아트 규칙은 그대로 유지한다.

## 2. 사용자가 정한 것

- **성장 방식:** 큰 머리를 유지하고 키만 조금씩 자란다(시안 B).
- **구조:** SPUM을 참고한다. 맨몸 부위 조각 위에 같은 부위의 옷 조각을 겹친다.
- **방향:** 앞·뒤·옆 모습이 모두 보여야 한다.
- **생성 도구:** Strix Halo의 ComfyUI.
- **옷 범위:** 첫 세트는 단계 전용이다. 5벌 × 3방향 = 15장이며, 다른 단계용은 나중에 필요한 옷만 추가한다.

## 3. 목표와 비목표

**목표**
- 3단계(놀이터, 학교, 졸업 후) × 3방향(앞, 뒤, 옆)의 베이스 파츠.
- 옷과 머리를 어떤 조합으로 바꿔도 틈이나 어긋남이 없는 파츠 규격.
- 첫 옷 세트: 놀이터 평상복, 학교 교복과 가방, 졸업 후 회사원·요리사·인테리어 작업복.
- 재실행 가능한 프롬프트, 워크플로, 후처리.

**비목표 (이번 범위 밖)**
- Unity 런타임 교체, 캐릭터 생성 UI 변경, 저장 데이터 변경.
- EmergencyCare, Soldier 작업복과 NPC 전용 외모. 규격이 검증된 뒤 같은 방법으로 추가한다.
- 실제 게임용 애니메이션 클립. 이번에는 파츠가 움직여도 틈이 없는지 확인하는 검수용 걷기 미리보기만 만든다.

**실패로 보는 것**
- 방향이나 단계가 바뀌면 다른 사람처럼 보인다.
- 옷을 바꾸면 몸의 크기나 위치가 달라진다.
- 팔다리가 움직일 때 몸과 옷 사이에 틈이 보인다.
- 게임의 기존 픽셀 아트 옆에 두면 그림체가 튄다.
- SPUM 픽셀을 복제하거나 입력으로 쓴다.

## 4. 안전 제약 (타협 불가)

- 주인공은 미성년자다. 벗은 몸통을 생성하지 않는다. 모든 단계의 몸통 베이스는 민소매 속옷과 반바지를 입은 상태로 만든다. 일관성을 위해 졸업 후 단계도 같다.
- 맨살은 얼굴, 목, 팔, 손, 다리, 발에만 그린다. 신체는 데포르메된 인형 형태이며 해부학적 묘사를 넣지 않는다.
- 프롬프트에 nude, naked, bare body, underwear 강조, 신체 부각 표현을 쓰지 않는다. 부정 프롬프트에 해당 개념을 넣는다.
- 위 조건에 어긋난 출력은 수정하지 않고 즉시 폐기하며, 검수 시트에도 넣지 않는다.

## 5. 파츠 규격

모든 수치는 비율 시안 B에서 온 제안값이다. 첫 체크포인트(§8)에서 기존 게임 아트 옆에 놓아 확인한다.

| 단계 | 전체 키 | 머리 | 몸통 | 다리 |
|---|---|---|---|---|
| 놀이터 | 32px | 16 | 7 | 5 + 신발 |
| 학교 | 36px | 16 | 9 | 7 + 신발 |
| 졸업 후 | 40px | 16 | 11 | 9 + 신발 |

- PPU는 32이고, Point 필터와 무압축을 쓴다. 외곽 알파는 0 또는 255만 허용한다.
- 머리 크기는 모든 단계에서 같다. 그래서 머리, 얼굴, 머리카락 파츠를 단계 간에 재사용할 수 있다.
- 옆모습은 한 방향만 만들고 좌우 반전한다. 가방처럼 좌우가 다른 소품도 반전되어도 어색하지 않게 디자인한다.

**파츠 슬롯 (앞에서 뒤 순서로 겹침)**

| 슬롯 | 조각 | 소속 |
|---|---|---|
| 얼굴·눈 | face | 머리 공통 |
| 머리카락 | hair_front, hair_back | 머리 공통 |
| 머리 | head | 베이스, 단계 공통 |
| 상의 | top_torso, top_arm_l, top_arm_r | 옷, 단계별 |
| 몸통 | torso | 베이스, 단계별 |
| 팔·손 | arm_l, arm_r | 베이스, 단계별 |
| 하의 | bottom_l, bottom_r, bottom_hip | 옷, 단계별 |
| 신발 | shoe_l, shoe_r | 옷, 단계별 |
| 다리·발 | leg_l, leg_r | 베이스, 단계별 |
| 등 소품 | back_item | 옷, 단계별 |

방향별 겹침 순서는 뒷모습과 옆모습에서 달라진다. 정확한 순서표는 파츠 팩의 `layer-order.json`에 고정한다.

**맞물림 규칙**
- 팔다리 조각은 관절 쪽으로 2px 이상 겹쳐 그려, 움직여도 틈이 없게 한다. 몸통 조각은 겹침을 받는 쪽에 빈 부분이 없게 채운다.
- 각 조각의 pivot은 관절 위치로 고정하고, 단계·방향·조각별로 JSON에 기록한다.
- 옷마다 덮는 구간을 선언한다(예: 반소매는 팔의 위쪽 40%, 반바지는 다리의 위쪽 50%, 긴바지는 발목까지). 선언한 구간 안에서는 베이스가 삐져나오면 안 되고, 구간 밖의 팔다리 맨살은 그대로 보인다. 옷은 몸보다 커질 수 있다. 이를 픽셀 단위 검사로 강제한다.

**머리와 변형**
- 머리 조각은 머리카락 없는 둥근 머리로 만들고, 머리카락은 별도 조각으로 위에 겹친다. 민머리 머리 파츠는 항상 머리카락에 가려지는 부품이며 단독으로 쓰지 않는다.
- 첫 세트는 피부색 1종, 얼굴 1종, 머리카락 3종(짧은 갈색, 단발 검정, 곱슬 적갈색)이다.
- 피부와 머리카락은 공용 팔레트의 전용 색 구간으로 칠해, 나중에 색 교체로 변형을 늘릴 수 있게 한다.

## 6. 모델과 생성 방식

**실행 환경:** FEVM의 ComfyUI(127.0.0.1:8188, Docker `sword-asset-factory-fevm-comfyui-1`). 다른 프로젝트와 공유하므로, 제출 전 큐가 비었는지 확인하고 큐를 비우거나 재시작하지 않는다. 8192 포트는 2026-11-04 확인 때 응답하지 않았다.

**주 모델:** Qwen Image Edit 2511 (`qwen_image_edit_2511_bf16.safetensors`, `qwen_2.5_vl_7b_fp8_scaled.safetensors`, `qwen_image_vae.safetensors`). 시작값은 기존 작업과 같은 20 steps이며, 시드는 기록한다. 기존 기록상 1장에 약 330~500초 걸린다.

**입력 이미지**
1. 규격에서 코드로 렌더한 가이드 시트. 부위별로 다른 평면 색을 칠한 단계·방향별 실루엣이며, 위치와 비율을 고정한다.
2. 스타일 참조. 이 프로젝트에서 이미 Comfy로 만든 아트(`Assets/Art/StudentLifePixels`, `Assets/Art/SchoolPixels`)에서 고른다. SPUM 그림은 넣지 않는다.
3. 일관성 참조. 첫 승인 이후에는 승인된 앞모습을 나머지 방향과 옷 생성의 입력으로 넣는다.

**순서:** 이미지별 독립 생성은 캐릭터 일관성이 잘 유지되지 않는다(`~/asset-factory/STRIX-HALO.md`, `QUALITY-RECOVERY-DESIGN.md`). 그래서 다음 순서로 승인된 결과를 입력으로 쌓아 간다.

놀이터 앞 → 놀이터 뒤·옆 → 학교 앞·뒤·옆 → 졸업 후 앞·뒤·옆 → 옷

**대체 경로:** Qwen이 가이드를 지키지 못하면 SDXL base + `pixel-art-xl` LoRA와 고정 시드 쌍으로 비교한다. 승인 시트가 쌓인 뒤에도 일관성이 부족하면 ComfyUI의 `TrainLoraNode`로 전용 LoRA 학습을 검토한다. 처음부터 학습하지 않는 이유는 학습 데이터가 될 승인 그림이 아직 없기 때문이다.

**후처리 (Strix Halo CPU, 재실행 가능한 스크립트)**
1. 큰 해상도로 생성한 시트를 가이드 격자에 맞춰 조각별로 자른다.
2. 배경을 제거하고 목표 해상도로 축소한다. 격자가 어긋나면 수정하지 않고 실패로 기록한다.
3. 공용 팔레트로 양자화하고 알파를 0/255로 만든다.
4. 조각을 아틀라스로 묶고 pivot JSON을 생성한다.

사후 처리는 기술 처리만 허용한다. 수작업 페인팅은 하지 않는다. 격자에 의한 결정론적 정리는 허용하며, 어떤 결과물이 확산 생성인지 규칙 생성인지 표시한다.

**자동 보정 한도:** 규칙으로 채우는 픽셀(덮는 구간의 작은 구멍, 관절 겹침, 끊긴 외곽선)은 조각당 불투명 픽셀의 5% 이하만 허용하고, 보정한 좌표를 기록한다. 넘으면 그 조각은 재생성한다. 같은 조각이 3회 실패하면 멈추고 대체 경로(§6)로 전환하거나 사용자에게 알린다.

## 7. 프롬프트

모든 프롬프트는 공통부 + 단계부 + 방향부로 조립한다. 실행 시 실제 사용한 전문을 이력에 남긴다. 아래는 시작안이며 첫 체크포인트에서 조정한다.

**공통 (positive)**

> Use image 1 as the exact layout: keep every body part in the same position, size and proportion, and replace each flat colored region with finished artwork. Match the rendering of image 2: contemporary dimensional pixel art, warm soft lighting, clean dark outline, limited palette, cozy healing life-simulation game. A cute deformed chibi game character with a large round head, simple face, small hands and feet. Each body part is drawn separately on a plain white background with empty space between parts. Same character design, same skin tone, same outline weight across all parts.

**공통 (negative)**

> nude, naked, bare torso, bare chest, realistic anatomy, sexualized, photorealistic, 3d render, gradient background, shadows on background, text, watermark, extra limbs, extra fingers, merged body parts, cropped parts, different character, blurry, anti-aliased edges

**베이스 단계부**

| 단계 | 문구 |
|---|---|
| 놀이터 | a small playful young child, very short legs, plain white sleeveless undershirt and plain white shorts, bare arms and legs, bare feet |
| 학교 | a school-age child, slightly taller with slightly longer legs, plain white sleeveless undershirt and plain white shorts, bare arms and legs, bare feet |
| 졸업 후 | a young adult with the same chibi head, taller with longer legs but still cute and deformed, plain white sleeveless undershirt and plain white shorts, bare arms and legs, bare feet |

**방향부:** `front view, facing the camera` / `back view, facing away, no face visible` / `side view, facing left, one eye visible`

**머리카락 생성 (승인된 머리를 입력으로)**

> Use image 1 as the exact head and keep its size and position unchanged. Draw only the hair, separated into a front piece and a back piece, on a plain white background: {머리카락 문구}. Do not draw the face or skin.

머리카락 문구: `short messy brown hair` / `straight black bob` / `curly reddish-brown hair`

**옷 생성 (승인된 베이스를 입력으로)**

> Use image 1 as the exact body and keep its pose, size and silhouette unchanged. Draw only the following clothing, as separate pieces in the same positions as the matching body parts, on a plain white background, fully covering the matching body part: {옷 문구}. Do not draw the head, face, hands or skin.

| 옷 | 문구 |
|---|---|
| 놀이터 평상복 | a mustard yellow T-shirt, olive green shorts, small brown sneakers |
| 학교 교복 | a neat ivory school shirt with navy collar and tie, navy trousers, black school shoes, a rust-red backpack |
| 회사원 | a charcoal blazer over a white shirt with a navy tie, charcoal trousers, black dress shoes |
| 요리사 | a white double-breasted chef jacket, cream apron, checked trousers, black kitchen shoes |
| 인테리어 작업복 | a slate blue work jacket, tool belt with a measuring tape, khaki work trousers, sturdy boots |

## 8. 체크포인트와 검수

사용자가 그림을 확인하는 지점은 다음과 같다. 체크포인트 없이 다음 단계로 넘어가지 않는다.

1. **놀이터 앞모습 베이스.** 그림체, 비율, 게임 아트 옆에 나란히 놓은 비교. 여기서 안 맞으면 나머지를 만들지 않는다.
2. **3단계 3방향 베이스 전체.** 같은 사람으로 보이는지, 성장이 보이는지.
3. **옷 세트와 조합 매트릭스.** 옷을 바꿔 입힌 전체 조합과 걷기 미리보기.

**자동 검사**
- 규격 일치: 크기, 알파 0/255, 팔레트 수, pivot 존재.
- 옷이 대응 베이스 실루엣을 덮는지.
- 머리·얼굴·머리카락이 3단계에서 같은 위치에 맞는지.
- 검수용 걷기 미리보기의 모든 프레임에서 틈 픽셀이 없는지.

자동 검사 통과만으로 완료로 보지 않는다. 그림체와 동일 인물 여부는 사용자가 눈으로 승인한다.

## 9. 산출물과 위치

- 작업 폴더: FEVM `/home/jdyj/farmer-art/character-parts-v1-20261104/`
- 로컬 사본: `output/assetpacks/character-parts-v1-20261104/`
  - `prompts/`, `workflows/`, `raw/`: 실제 프롬프트, 그래프, 원본 출력, 시드, 모델, 해시, 소요 시간
  - `parts/{stage}/{view}/`, `clothes/{stage}/{outfit}/{view}/`: 아틀라스와 pivot JSON
  - `layer-order.json`, `spec.json`, `palette.png`
  - `previews/`: 검수 시트, 조합 매트릭스, 걷기 미리보기
  - `README.md`: 재실행 순서, 생성물과 규칙 생성물 구분

`Assets/`와 씬, 기존 세이브는 이번 범위에서 건드리지 않는다.

## 10. 구현자에게 맡기는 것

- 가이드 시트의 구체적 렌더링과 부위 색.
- Qwen 입력 크기, CFG, 시드 수 같은 샘플링 세부값.
- 후처리 스크립트 구조와 언어.
- 팔레트 색 수. 기존 아트와 맞는 값을 측정해 정한다.

## 11. 설계자가 정한 것 (사용자 확인 전)

- 단계별 키 수치와 PPU 32.
- 머리 파츠의 단계 간 재사용.
- 옷별 덮는 구간 선언 방식과 자동 보정 한도 5%, 3회 실패 시 중단.
- 첫 세트의 피부색 1종, 얼굴 1종, 머리카락 3종과 색 교체용 팔레트 구조.
- 옷 세트 구성과 색상 문구. 회사원은 사용자의 "회사"에서 왔으며 현재 직업 데이터에는 없다.
- 첫 옷 세트에서 EmergencyCare와 Soldier를 제외.
- SPUM은 구조만 참고하고 그림은 입력으로 쓰지 않음.
- 몸통 베이스에 속옷과 반바지 포함(§4).
- 런타임 연결을 별도 작업으로 분리.
