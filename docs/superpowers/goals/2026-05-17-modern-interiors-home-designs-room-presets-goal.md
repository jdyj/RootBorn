# Goal: Modern Interiors Home Designs 기반 House 방 디자인 확장

`C:\Users\jdyj\Downloads\moderninteriors-win\6_Home_Designs`를 기준으로, 48x48 Home Design 샘플들을 조사해서 House 씬에 적용 가능한 방 디자인/배치 프리셋으로 변환한다.

## 소스 범위

- 조사 대상:
  - `C:\Users\jdyj\Downloads\moderninteriors-win\6_Home_Designs`
  - 48x48 기준 디자인을 우선 사용
- 각 디자인 폴더 안에서:
  - `layer_1`은 방 구조/타일 배치 기준으로 사용
  - `layer_2`는 참고만 한다
  - 실제 가구/오브젝트는 기존에 import한 `1_Interiors/48x48/Theme_Sorter_Shadowless_Singles_48x48` 기반 single furniture catalog를 우선 사용한다
- 다른 Theme_Sorter 폴더는 보지 않는다.
- 기존 Modern Interiors shadowless single 가구 import 정책과 충돌하지 않게 한다.

## 목표

Home_Designs의 예시 방들을 보고 다음을 만든다.

1. 각 Home Design의 방 구조 분석
   - 방 크기
   - 벽/바닥/문/창문 위치
   - 주요 가구 배치 의도
   - 어떤 카테고리 방인지 분류

2. House 씬에 적용 가능한 방 디자인 프리셋 제안
   - Bedroom
   - Bathroom
   - Kitchen
   - Living Room
   - Japanese/Condominium 계열 방
   - 필요하면 Studio/Compact Room 같은 혼합형

3. 프리셋 데이터화 방향
   - 하드코딩 금지
   - 가능하면 ScriptableObject 또는 기존 데이터 구조에 맞게 정의
   - 엔티티 ID별 `if`/`switch` 금지
   - 배치 좌표는 48x48 cell 기준
   - 가구는 좌측 아래 anchor 기준
   - footprint는 기존 정책을 그대로 사용

4. 배치 UX 유지
   - 가구 선택 시 실제 ghost preview 표시
   - 차지하는 칸 footprint preview 표시
   - 배치 가능/불가능 색상 표시
   - 배치 후 ghost/footprint preview clear

## 구현 전 조사

먼저 `6_Home_Designs`를 전수 조사해서 다음 문서를 작성한다.

- 어떤 디자인 폴더가 있는지
- 각 디자인의 `layer_1` 크기와 용도
- House 씬에 바로 적용 가능한 디자인
- 추가 변환이 필요한 디자인
- 현재 single furniture catalog와 매칭 가능한 가구 종류
- 매칭이 애매한 가구/타일 목록

문서는 다음 위치에 작성한다.

`docs/superpowers/audits/YYYY-MM-DD-modern-interiors-home-designs-audit.md`

## 구현 정책

- 우선 샘플 1~2개 방만 실제 House에 적용해서 검증한다.
- 모든 방을 한 번에 구현하지 않고, 먼저 파이프라인을 만든 뒤 확장한다.
- Home Design 원본을 그대로 베끼기보다, ROOTBORN House 씬에서 보기 좋게 맞춰도 된다.
- 단, 원본 `layer_1`에서 읽은 구조적 의도는 유지한다.
- 저장되는 프리셋은 재사용 가능해야 하며, 이후 다른 방 디자인을 추가할 때 같은 파이프라인을 사용할 수 있어야 한다.

## 검증 기준

완료 전 반드시 확인한다.

1. EditMode 테스트
   - Home Design `layer_1` 분석 결과가 기대한 room size/cell layout으로 파싱되는지 확인
   - 생성된 프리셋의 tile/furniture count가 맞는지 확인

2. PlayMode 테스트
   - House 씬에서 방 프리셋 선택 또는 적용 경로 검증
   - 실제 Tilemap에 바닥/벽/가구가 들어갔는지 확인
   - stale sample/debug tilemap이 보이지 않는지 확인

3. 직접 시각 검증
   - PlayMode에서 실제 사용자 흐름으로 House 진입
   - 디자인 적용
   - Game View screenshot 캡처
   - runtime Tilemap 상태 출력:
     - 적용된 디자인 이름
     - floor/wall/object tile count
     - furniture count
     - 첫 가구 이름
     - footprint/occupancy count

## 완료 보고에 포함할 것

- 조사한 Home Design 폴더 수
- 실제 적용한 디자인 이름
- 생성/수정한 파일
- 테스트 결과
- Game View 확인 결과
- 아직 적용하지 않은 디자인과 다음 확장 방향

## 실행 요청 문구

아래 문구로 작업을 시작하면 된다.

```md
위 goal 기준으로 진행해줘.
단, 처음부터 전부 구현하지 말고 `6_Home_Designs` 전수 조사 후,
House에 가장 잘 맞는 방 디자인 1~2개를 골라서 파이프라인을 먼저 만들고
PlayMode Game View 검증까지 해줘.
```

## 핵심 작업 순서

전수 조사 -> 문서화 -> 작은 샘플 구현 -> 실제 PlayMode 시각 검증 -> 확장 가능성 보고
