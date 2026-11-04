# ROOTBORN Day/Night Lighting System Goal Prompt

아래 프롬프트를 새 goal 세션에 그대로 사용한다.

```text
ROOTBORN Unity 프로젝트에 2D Pixel Top Down 낮/밤 조명 시스템을 설계/구현해줘.

작업 전 필수 규칙:
- AGENTS.md와 .claude/rules/* 규칙을 따른다.
- Assets/**/*.cs 파일은 디스크에 직접 쓰지 않는다. 신규/수정 C# 스크립트는 반드시 Unity MCP script-update-or-create 또는 Unity Editor 안전 경로를 사용한다.
- TDD 원칙을 따른다. 실패 테스트를 먼저 추가하고, 구현 후 테스트를 통과시킨다.
- 시각/카메라/조명/씬 변경은 자동 테스트만으로 완료 처리하지 않는다. 실제 PlayMode 사용자 흐름과 Game View 또는 Camera screenshot 검증까지 수행한다.
- 엔티티 ID별 if/switch/enum 분기는 금지한다. 시간대 색상, 조명 강도, 오디오/로컬 라이트 반응 같은 튜닝 값은 ScriptableObject 데이터로 분리한다.
- 기존 Time/GameClock 구조가 있으면 새 시스템을 중복 생성하지 말고 기존 구조와 통합한다.
- 기존 사용자 변경 사항을 되돌리지 않는다.

목표:
1. Unity 2D URP 2D Renderer 기반으로 Global Light 2D를 사용해 낮 -> 저녁 -> 밤 -> 새벽이 자연스럽게 변하도록 구현한다.
2. 낮/밤용 타일/스프라이트 에셋을 따로 만들지 않고 조명 색상과 intensity로 분위기를 만든다.
3. Pixel Perfect Camera와 픽셀 아트 감성을 유지한다. 조명으로 인한 블러, 번짐, 과한 bloom, 압축 깨짐을 방지한다.
4. 시간 흐름은 0~24 게임 시간으로 표현하고, 시간 흐름 속도를 데이터로 조절 가능하게 한다.
5. IsMorning, IsAfternoon, IsEvening, IsNight 같은 시간대 판별 API를 제공한다.
6. 낮/저녁/밤/새벽 색상과 intensity는 ScriptableObject 프로필로 관리한다.
7. Global Light 전환은 프레임 독립적인 부드러운 보간으로 처리한다.
8. 가로등, 횃불, 창문 불빛, 플레이어 주변광 같은 Point Light 2D 확장 지점을 마련한다.
9. 모바일/저사양 환경을 고려해 Light2D 개수, Shadow Caster 2D, Update 전체 탐색을 제한한다.
10. Town/Farm 같은 실제 플레이 씬에서 플레이어가 보는 화면 기준으로 낮/저녁/밤/새벽 변화가 확인되어야 한다.

권장 아키텍처:
- TimeManager 또는 기존 GameClock 확장
  - 현재 게임 시간 0~24
  - 시간 흐름 속도
  - 시간 표시용 Hour/Minute
  - 시간대 판별 함수
  - TimeChanged 이벤트
- DayNightLightingProfile ScriptableObject
  - 0~24 시간대 색상 Gradient 또는 시간대 키 배열
  - 0~24 시간대 intensity AnimationCurve
  - 전환 속도
  - 실외/실내용 프로필 분리 가능성
- LightingManager
  - Global Light 2D 참조
  - DayNightLightingProfile 참조
  - TimeChanged 또는 Tick 기반으로 목표 색상/intensity 계산
  - Update에서는 현재값을 목표값으로 부드럽게 보간
- LocalLightController
  - Point Light 2D를 시간대에 따라 켜고 끄거나 intensity를 조절
  - 가로등/창문/횃불/플레이어 주변광에 재사용
  - 거리 기반 또는 Renderer visibility 기반 비활성화 여지를 둔다.
- AmbientAudioManager 확장 지점
  - 낮/밤 앰비언스 전환을 AudioMixer Snapshot 또는 이벤트로 연결할 수 있게 한다.

추천 색상 방향:
- 새벽: 회청색에서 연한 노랑으로 넘어가는 톤, intensity 0.45~0.65
- 낮: 밝고 따뜻한 흰색, intensity 0.9~1.0
- 저녁: 주황/분홍 계열, intensity 0.7~0.9
- 밤: 완전 검정이 아닌 남색/청색 계열, intensity 0.22~0.4
- 심야: 채도를 너무 죽이지 말고, 로컬 라이트의 따뜻한 색과 대비되도록 한다.

픽셀 아트 보호 요구사항:
- Sprite Import Filter Mode는 Point (no filter)를 유지한다.
- Compression은 None 또는 시각 품질을 해치지 않는 설정으로 둔다.
- Mip Maps는 끈다.
- Sprite/Tilemap Material은 조명 대상이면 Sprite-Lit-Default 계열을 사용한다.
- UI 또는 조명 영향을 받으면 안 되는 대상은 Sprite-Unlit-Default 계열을 사용한다.
- Pixel Perfect Camera의 Assets Pixels Per Unit과 주요 Sprite PPU가 일치하는지 확인한다.
- URP/HDR/Bloom/후처리로 픽셀 경계가 번지지 않도록 설정을 점검한다.
- 2D Renderer Data의 Blend Style이 과하게 밝거나 뿌옇게 합성되지 않는지 확인한다.

필수 테스트:
- TIME-DAYNIGHT-001: 시간 값을 0~24 범위로 반복 처리하고 Hour/Minute 표시가 결정적으로 계산된다.
- TIME-DAYNIGHT-002: 시간 흐름 속도 변경 시 게임 시간이 기대한 속도로 진행된다.
- TIME-DAYNIGHT-003: IsMorning/IsAfternoon/IsEvening/IsNight가 경계 시간에서 정확히 동작한다.
- LIGHT-DAYNIGHT-001: DayNightLightingProfile이 시간값에 따라 기대 색상/intensity 목표값을 반환한다.
- LIGHT-DAYNIGHT-002: LightingManager가 Global Light 2D 색상과 intensity를 목표값으로 부드럽게 보간한다.
- LIGHT-DAYNIGHT-003: LocalLightController가 밤에는 지정 조명을 켜고 낮에는 끄거나 약화한다.
- VISUAL-DAYNIGHT-001: PlayMode에서 실제 플레이 흐름으로 Town 또는 적용 대상 씬에 진입해 낮, 저녁, 밤, 새벽 Game View screenshot을 확보한다.
- VISUAL-DAYNIGHT-002: Tilemap/SpriteRenderer/Global Light/Point Light 상태를 런타임에서 검사해 실제 화면에 적용된 Light2D 이름, 색상, intensity, 활성 상태를 기록한다.

구현 순서:
1. 현재 시간 시스템, 씬 조명, URP 2D Renderer, Pixel Perfect Camera, Tilemap Material 상태를 조사한다.
2. 기존 GameClock/TimeManager가 있으면 통합 설계를 먼저 정리한다. 없으면 최소 TimeManager를 추가한다.
3. 실패 EditMode 테스트를 추가한다.
4. DayNightLightingProfile 데이터 모델과 시간대 평가 로직을 구현한다.
5. LightingManager를 구현하고 Global Light 2D 참조를 씬에 연결한다.
6. LocalLightController를 구현해 Point Light 2D 확장 지점을 만든다.
7. 기본 프로필 asset을 생성하고 GameDataRegistry 등록 필요 여부를 판단한다.
8. Town 또는 현재 플레이 가능한 대표 씬에 Global Light 2D와 LightingManager를 연결한다.
9. 필요한 경우 가로등/창문/플레이어 주변광 중 최소 1개 이상을 Point Light 2D로 구성한다.
10. EditMode 테스트를 통과시킨다.
11. PlayMode에서 사용자 흐름으로 씬에 진입해 시간 진행 또는 테스트 입력으로 낮/저녁/밤/새벽을 관찰한다.
12. Game View 또는 Camera screenshot을 남기고, 런타임 Light2D/Renderer/Tilemap 상태를 검사한다.
13. 저장되어야 하는 scene/prefab/SO 상태는 EditMode에서 다시 열거나 검사해 실제 저장 상태를 확인한다.
14. 최종 보고에는 수정 파일, 테스트 결과, PlayMode 시각 검증 결과, 남은 리스크를 포함한다.

완료 조건:
- 낮/저녁/밤/새벽이 Global Light 2D 색상과 intensity로 자연스럽게 전환된다.
- 낮/밤용 별도 월드 에셋 교체 없이 조명으로 분위기가 표현된다.
- 시간 API와 시간대 판별 함수가 테스트로 검증된다.
- 조명 프로필은 ScriptableObject 데이터로 조정 가능하다.
- Pixel Perfect/Point filtering/압축/Material/URP 설정 점검 결과가 보고된다.
- Point Light 2D 기반 로컬 조명 확장 구조가 존재한다.
- 실제 PlayMode Game View에서 낮/저녁/밤/새벽 screenshot 또는 동등한 시각 증거가 확보된다.
- 자동 테스트와 직접 시각 검증 중 하나라도 실패하면 완료로 보고하지 않는다.
```

## Brainstorming Notes

### 추천 접근

가장 적합한 접근은 기존 시간 시스템을 유지하면서 조명 전용 계층만 추가하는 방식이다. `GameClock` 또는 기존 `TimeManager`가 이미 하루 진행, 일자 롤오버, UI 표시를 담당한다면, 낮/밤 시스템은 새 시간을 만들지 않고 그 시간을 구독해야 한다. 이렇게 해야 작물 성장, NPC 스케줄, 학생 생활 루프, 저장/로드 시간과 조명 시간이 어긋나지 않는다.

조명 값은 코드 상수보다 `DayNightLightingProfile` ScriptableObject에 둔다. 색상은 `Gradient`, 강도는 `AnimationCurve`가 Unity Inspector에서 가장 조정하기 쉽다. 유지보수 관점에서는 실외 프로필, 실내 프로필, 이벤트용 특수 프로필을 나눌 수 있게 만드는 것이 좋다.

### 대안 비교

1. **Global Light 2D + ScriptableObject 프로필**
   - 장점: 구현이 단순하고 URP 2D에 잘 맞으며 타일맵 전체 분위기 전환이 쉽다.
   - 단점: 지역별 조명 차이는 Point Light 2D 보강이 필요하다.
   - 추천: 1차 구현 기준.

2. **화면 전체 Color Overlay UI**
   - 장점: 빠르게 만들 수 있고 조명 Material 의존성이 작다.
   - 단점: Sprite-Lit 조명, 로컬 라이트, 그림자와 자연스럽게 섞이지 않는다.
   - 비추천: 임시 프로토타입 외에는 사용하지 않는다.

3. **낮/밤별 타일/스프라이트 에셋 교체**
   - 장점: 아트 디렉션을 세밀하게 통제할 수 있다.
   - 단점: 에셋 제작/관리 비용이 커지고 타일맵 유지보수가 어려워진다.
   - 비추천: 이번 목표의 "에셋 별도 제작 없음" 조건과 맞지 않는다.

### 주요 리스크

- Sprite/Tilemap Material이 Unlit이면 Global Light 2D 변화가 화면에 보이지 않는다.
- 후처리 Bloom/HDR 또는 Texture compression이 픽셀 경계를 흐릴 수 있다.
- 기존 시간 시스템과 별도 TimeManager를 만들면 UI, 저장, NPC 스케줄, 조명이 서로 다른 시간을 보게 된다.
- Point Light 2D를 모든 창문/소품에 배치하면 모바일 성능이 빠르게 나빠진다.
- 테스트에서 Light2D 값만 맞고 Game View가 실제로 어둡거나 가려진 상태일 수 있으므로 시각 검증이 반드시 필요하다.

## Verification Checklist

- [ ] 기존 시간 시스템과 통합 지점을 확인했다.
- [ ] Global Light 2D가 실제 적용 대상 씬에 존재하고 LightingManager에 연결되어 있다.
- [ ] 조명을 받을 Sprite/Tilemap Material이 Lit 계열이다.
- [ ] DayNightLightingProfile asset이 저장되어 있고 씬 또는 registry에서 참조된다.
- [ ] 낮/저녁/밤/새벽 자동 테스트가 존재하고 통과한다.
- [ ] PlayMode에서 실제 플레이 흐름으로 대표 씬에 진입했다.
- [ ] Game View 또는 Camera screenshot으로 낮/저녁/밤/새벽을 확인했다.
- [ ] 런타임 Light2D 색상/intensity/활성 상태를 검사했다.
- [ ] Pixel Perfect, Filter Mode, Compression, Mip Maps, URP 2D Renderer 설정을 점검했다.
- [ ] 성능 리스크와 Light 개수/Shadow 사용 범위를 보고했다.
