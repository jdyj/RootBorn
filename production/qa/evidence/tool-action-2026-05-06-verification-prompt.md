# AI 에이전트 검증 프롬프트 — 도구 액션 (도끼-나무 / 곡괭이-돌) + Control 키

> 다른 세션의 AI 에이전트가 이번 작업(2026-05-06) 을 동일하게 검증할 수 있도록 작성된 self-contained 프롬프트.
> 그대로 복사해서 새 세션에 붙여 사용.

---

## 컨텍스트 (배경 — 에이전트가 처음 보는 상태)

ROOTBORN 은 Unity 6 2D 농장/세대 진화 게임이다. 다음 헌법·규칙을 준수한다:
- `.claude/constitution.md` §핵심원칙 4 — TDD 의무 (RED → GREEN → REFACTOR)
- `.claude/rules/path-based/assets-data.md` — 엔티티 데이터드리븐 절대 원칙 (시스템 코드는 특정 엔티티 ID 분기 금지)
- `.claude/rules/path-based/assets-addressables.md` — 동적 자원 Addressables 일원화
- `.claude/rules/unity-cli.md` — `Assets/**/*.cs` 직접 디스크 쓰기 금지 (`script-update-or-create` MCP 경유)

2026-05-06 작업으로 다음 액션 루프가 구현되었다:
- **도끼(Tool_StoneAxe) 들고 나무(Resource_Tree) 근처에서 마우스 좌클릭 또는 Control 키 → 풀파워(1.2x), 5회 만에 베기 → Wood 1~3 인벤토리 추가**
- **곡괭이(Tool_StonePickaxe) 들고 돌(Resource_Rock) 근처에서 좌클릭/Control → 풀파워(1.2x), 5회 만에 캐기 → Stone 1~2**
- **도구 mismatch (도끼로 돌, 곡괭이로 나무) → 0.5x 패널티 (PowerMul × 0.5 = 0.6)**
- **맨손 → `_bareHandPenaltyMul` (0.3) 적용**

도구→자원 매칭은 `ResourceNodeDefinition._preferredTool` SO 필드로만 결정되며, 시스템 코드는 도구 ID 분기를 사용하지 않는다 (헌법 §엔티티 데이터드리븐).

---

## 너의 임무

이번 작업이 의도대로 작동하는지, 그리고 회귀 없이 안정적인지 **객관적 증거**로 검증한다. 결과를 200~300단어 내 **PASS/FAIL 보고서** 로 반환한다.

---

## 검증 단계 — 7개 지표를 순서대로 확인

### 1. 자산 존재 검증 (필수)

다음 4개 자산 파일이 디스크에 존재하는지 확인:
- `Assets/Data/Tools/Tool_StonePickaxe.asset`
- `Assets/Data/Items/Item_Tool_StonePickaxe.asset`
- `Assets/Data/Tools/Tool_StoneAxe.asset` (기존, 회귀 가드)
- `Assets/Data/Resources/Resource_Rock.asset` (기존, 와이어링 변경됨)

`Glob` 또는 `Read` 도구로 각 파일을 직접 확인. **하나라도 부재 = FAIL.**

### 2. 도구 SO 필드 검증

`Tool_StonePickaxe.asset` 의 YAML 을 `Read` 로 읽어 다음 4개 필드 검증:
- `_id: StonePickaxe`
- `_displayKey: tool.stonePickaxe`
- `_powerMultiplier: 1.2`
- `m_Script` GUID 가 `f77cd465a2d1cc349b40f9eeae23b18d` (= `ToolDefinition` 클래스의 MonoScript)

`Item_Tool_StonePickaxe.asset` 의 `_id: StonePickaxe` 와 `_category: 1` (Tool enum=1) 검증.

### 3. 와이어링 검증

`Resource_Rock.asset` 의 `_preferredTool` 필드가 `Tool_StonePickaxe.asset` 의 GUID (`440e90155c98f3242b77566fd0da0519`) 를 참조하는지 확인.
**`{fileID: 0}` 또는 다른 GUID 면 FAIL.**

`Resource_Tree.asset` 의 `_preferredTool` 가 `Tool_StoneAxe.asset` 의 GUID (`58d3c252b9385d546922d09ce0b6cc49`) 를 참조하는지도 회귀 가드로 확인.

### 4. GameDataRegistry 등록 검증

`Assets/Resources/GameDataRegistry.asset` 의 `_tools` 배열에 `440e90155c98f3242b77566fd0da0519` (Tool_StonePickaxe) 항목이 있는지, `_items` 배열에 `0cd403dc68abf4a45b664a12381a81f7` (Item_Tool_StonePickaxe) 가 있는지 `Read` 로 확인.

### 5. Control 키 바인딩 검증 (소스 코드)

`Assets/Scripts/Game/Player/GatherInteractor.cs` 의 `OnEnable()` 메서드 안에 다음 두 줄이 정확히 존재하는지 `Grep` 로 확인:
- `_interactAction.AddBinding("<Keyboard>/leftCtrl");`
- `_interactAction.AddBinding("<Keyboard>/rightCtrl");`

기존 E/Space 바인딩도 보존되어 있는지 함께 확인.

### 6. 자동 테스트 통과 검증 (가장 중요)

MCP 도구 `tests-run` 으로 EditMode 와 PlayMode 를 둘 다 실행:
```
testMode: "EditMode"  → 124/124 PASS 기대
testMode: "PlayMode"  → 12/12 PASS 기대
```

특히 다음 신규 테스트 클래스들이 모두 통과해야 함:
- `Rootborn.Tests.EditMode.ToolResourceMatchingTests` (10개)
- `Rootborn.Tests.EditMode.GatherInteractorInputBindingTests` (2개)

**한 개라도 실패 시 실패 메시지 전문을 보고서에 인용.**

### 7. 헌법 위반 정적 분석

`Grep` 로 `Assets/Scripts/Game/` 안에 다음 안티패턴이 **없어야** 함을 확인:
- `cropId == "..."` 또는 `toolId == "..."` 형식의 엔티티 ID 분기
- `switch (tool.id)` 같은 switch 문
- `enum CropId` / `enum ToolId` 신규 정의
- `if (tool.Id == "StonePickaxe")` 같은 신규 분기 (이번 작업으로 추가됐는지 확인)

**발견 시 정확한 파일:라인 인용 후 FAIL.**

---

## 보고서 형식

```
## 검증 결과 — 도구 액션 (2026-05-06)

| 지표 | 결과 | 비고 |
|---|---|---|
| 1. 자산 존재 | PASS/FAIL | (FAIL 시 부재 파일 나열) |
| 2. ToolDefinition 필드 | PASS/FAIL | |
| 3. preferredTool 와이어링 | PASS/FAIL | |
| 4. Registry 등록 | PASS/FAIL | |
| 5. Control 바인딩 (소스) | PASS/FAIL | |
| 6. EditMode/PlayMode 테스트 | PASS X/Y, FAIL X/Y | (FAIL 메시지 인용) |
| 7. 헌법 정적 분석 | PASS/FAIL | (위반 시 file:line) |

**총평**: PASS / FAIL — (한 줄 결론)

**회귀 위험 발견**: (있으면 나열, 없으면 "없음")
```

---

## 추가 — Play 모드 수동 시각 검증 (선택)

자동 테스트가 모두 PASS 면 Play 모드 검증은 선택. FAIL 이거나 의심스러운 경우만:
1. `editor-application-set-state` 로 Play 모드 진입
2. `screenshot-game-view` 로 Farm 씬 확인
3. 콘솔 로그에 `[Exception]` 또는 `NullReferenceException` 없는지 `console-get-logs` 로 확인

---

## 함정 / 회귀 위험

- **EditMode 테스트는 OnEnable 자동 호출 안 됨** — `GatherInteractorInputBindingTests` 가 reflection 으로 OnEnable 직접 Invoke. 다른 테스트가 같은 패턴 안 쓰면 `_interactAction == null` 로 FAIL 회귀 가능.
- **GameDataRegistry 자동 등록 미보장** — 이번 작업 시점 환경에서 SO 신규 생성 후 자동으로 `_tools/_items` 배열에 안 들어갔음. 신규 SO 추가 시 YAML 직접 편집 필요할 수 있음.
- **untracked 깨진 파일** (예: `Assets/Tests/EditMode/Save/SaveSlotServiceTests.cs.disabled` — 만약 `.cs` 로 다시 활성화돼있으면 컴파일 에러로 모든 테스트 차단). `git status --short Assets/` 로 확인.
- **Pickaxe sprite sheet 부재** — `PixelwoodSliceSetup` 에 Pickaxe 슬라이스 타깃 없음. 곡괭이 들고 좌클릭 시 휘두르기 sprite null fallback (Animator 비활성 + sprite 변경 안 됨). 로직은 동작하지만 시각 피드백 부족 — 후속 자산 작업.

---

## 참고 — 변경된 파일 목록 (이 작업 한정)

```
Assets/Data/Tools/Tool_StonePickaxe.asset                  (NEW)
Assets/Data/Tools/Tool_StonePickaxe.asset.meta             (NEW, GUID 440e90155c98f3242b77566fd0da0519)
Assets/Data/Items/Item_Tool_StonePickaxe.asset             (NEW)
Assets/Data/Items/Item_Tool_StonePickaxe.asset.meta        (NEW, GUID 0cd403dc68abf4a45b664a12381a81f7)
Assets/Data/Resources/Resource_Rock.asset                  (MODIFIED — _preferredTool 와이어링)
Assets/Resources/GameDataRegistry.asset                    (MODIFIED — _tools/_items 배열 끝에 한 줄씩 추가)
Assets/Scripts/Game/Player/GatherInteractor.cs             (MODIFIED — OnEnable 에 leftCtrl/rightCtrl 바인딩)
Assets/Tests/EditMode/ToolResourceMatchingTests.cs         (NEW, 10 tests)
Assets/Tests/EditMode/GatherInteractorInputBindingTests.cs (NEW, 2 tests)
.claude/rules/changelog.md                                 (MODIFIED — 2026-05-06 항목 추가)
```

**기준 통과**: 이번 변경 후 EditMode **124/124**, PlayMode **12/12**.

---

(끝 — 이 프롬프트를 그대로 새 AI 세션에 붙이면 self-contained 검증 가능)
