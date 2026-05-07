# 테스트 디시플린 (Testing Discipline)

> **최우선 규칙**. 다른 모든 규칙(`coding-standards.md`, `path-based/*.md`, `game-design.md`)에 우선하여 적용.

## 절대 원칙

1. **TDD 의무**: 모든 신규 코드(클라/서버/스크립트)는 **실패 테스트 먼저, 구현 다음**.
2. **시나리오 자동화 100% 커버**: 게임 코어 루프의 **모든 상태 전이**는 시나리오 테스트가 한 번 이상 실행한다. 100%에 최대한 가깝게(≥95%) 유지.
3. **PR 차단 게이트**: 테스트가 빠진 PR은 머지 금지. CI가 강제.
4. **AI 생성 코드도 예외 없음**: AI 에이전트가 작성한 코드도 동일 게이트. 에이전트가 테스트를 동시 작성하지 않으면 작업 미완료로 간주.
5. **보상·인벤토리 멱등성 의무**: 퀘스트/스토리/미션/지식/툴 해금 등 보상 지급 코드는 지급 전에 인벤토리 수용 가능 여부와 중복 수령 상태를 검증해야 한다. 인벤토리 공간 부족, 유효하지 않은 보상, 이미 수령한 보상은 인벤토리와 진행 상태를 변경하지 않아야 한다. 보상 지급 테스트는 정상 지급, 공간 부족, 부분 지급 방지, 반복 호출 이중 지급 방지, 저장/로드 후 재지급 방지를 포함한다.

## 테스트 계층

### Tier 1 — 단위 테스트 (Unit Tests)
- **Unity (EditMode)**: NUnit, `Assets/Tests/EditMode/`
- **Server (Vitest)**: `server/tests/unit/**/*.test.ts`
- **대상**: 단일 함수·클래스 하나의 행동
- **격리**: 외부 의존성 없음(DB·네트워크·시간·랜덤 모두 모킹)
- **속도**: 단일 케이스 ≤ 50ms

### Tier 2 — 통합 테스트 (Integration Tests)
- **Unity (PlayMode)**: `Assets/Tests/PlayMode/`, `UnityTest` 코루틴 패턴
- **Server (Vitest)**: `server/tests/integration/**/*.test.ts`, 실제 Postgres + Redis(Docker testcontainers 또는 dev compose)
- **대상**: 여러 컴포넌트 간 상호작용 (예: API → 서비스 → DB → 응답)
- **속도**: 단일 케이스 ≤ 2s

### Tier 3 — 시나리오 테스트 (Scenario Tests, **100% 커버 의무**)
- **클라**: PlayMode + 결정론 시뮬을 헤드리스로 실행. **하나의 시나리오 = 하나의 매치 풀 플레이**
- **서버**: 매치 검증 + 가챠 + 시즌 패스 등 **유저 여정(journey)** 전체
- **크로스 스트림**: 클라 입력 → 서버 검증 → 보상 → 클라 반영 풀체인 (testcontainers + 헤드리스 시뮬)
- **속도**: 단일 시나리오 ≤ 30s, 전체 시나리오 스위트 ≤ 10분 (CI 병렬화)

### Tier 4 — 결정론 패리티 (Sim Parity Vectors)
- 클라(C#) ↔ 서버(TS) 시뮬이 동일 (seed, inputs)에 대해 동일 final hash 반환
- `contracts/sim-parity-vectors.json` 의 모든 벡터는 **모든 PR**에서 통과해야 함
- 신규 메카닉/코인/적 추가 시 벡터 1개 이상 추가 의무

## 코어 루프 시나리오 카탈로그 (100% 커버 대상)

게임의 모든 코어 루프 상태 전이를 명명된 시나리오로 표현한다. 각 시나리오는 PlayMode에서 헤드리스 실행 가능해야 한다.

### ROOTBORN 농장 시나리오 (필수 커버, prefix: GEN/HEIR/TOOL/CROP/KNOW/STATUS/NET)
| ID | 시나리오 | 검증 |
|---|---|---|
| GEN-001 | 1세대 lifetimeSec 경과 → 2세대 자동 전환 + Lineage 기록 | GenerationManagerTests |
| HEIR-001 | 같은 시드 입력 → 동일 후계자 생성, 비계승 특성은 부모로부터 전달 안 됨 | HeirGeneratorTests |
| KNOW-001 | 맨손 + 돌 + 땅 표면 반복 N회 → Knowledge_StoneTool 트리거 평가 통과 | KnowledgeTriggerTests |
| KNOW-002 | KnowledgeProgress가 임계 도달 시 OnUnlocked 1회 발화, 이후 누적해도 이중 발화 X | KnowledgeProgressTests |
| TOOL-001 | ToolDefinition.ApplyEffects가 SO 전략 배열을 순서대로 호출 | (Phase 8 추가 예정) |
| CROP-001 | CropPlot.Tick이 stageDurations에 따라 결정론적으로 단계 전환 | CropGrowthTests |
| STATUS-001 | StatusValue.Tick 누적, max 클램프, Restore 0 floor, Penalty 곡선 | StatusValueTests |
| STATUS-002 | GameClock.Tick의 Day/DayProgress 결정론 | GameClockTests |
| NET-001 | ArgsParser가 -mode/-port/-maxPlayers/-saveSlot/-joinIp 파싱, Unity 예약 인자 무시 | ArgsParserTests |
| NET-002 | dedicated server 빌드 → 클라 loopback 접속 → 플레이어 spawn (PlayMode) | (Phase 8 추가 예정) |

### 매치 시나리오 (참고 — coin-defense 호환 보존, ROOTBORN 미사용)
| ID | 시나리오 | 검증 |
|---|---|---|
| MS-001 | 매치 시작 → 첫 소환 → 첫 적 처치 → 보상 | 자원 += 처치 보상, 코인 1마리 보드 위 |
| MS-002 | 동일 등급·종류 2개 소환 → 합성 → 다음 등급 랜덤 | DefaultMergeRule 동작, RNG 결정론적 |
| MS-003 | FixedCombo (BitKnight + EthSage + ChainLinker → GenesisCore 잠금해제) | 조합 잠금해제 발동 |
| MS-004 | UpgraderRule (보조 유닛이 다른 유닛 +1 등급) | 보조는 소멸, 대상은 1등급 상승 |
| MS-005 | AlwaysSelfRule | 합성 결과가 항상 자기 자신 |
| MS-006 | 매치 패배 (지갑 라이프 0) → 부활 광고 → 재시작 | 부활 후 1회 한정 |
| MS-007 | 매치 패배 (보스 페이즈) → 부활 불가 | 보스 페이즈는 부활 X |
| MS-008 | 보스 웨이브 (5/10마다 등장) → 처치 → 보너스 보상 | 보스 패턴 동작 |
| MS-009 | 무한 모드 (적 HP `1.05^N` 스케일) | N=50, N=100 웨이브 도달 가능성 |
| MS-010 | 네트워크 단절 (10초 내) → 재연결 → 명령 큐 일괄 전송 | 매치 진행 보존 |
| MS-011 | 네트워크 단절 (5분 초과) → 매치 강제 종료 → EXP 70% 보장 | 보상 환원 |
| MS-012 | 매치 종료 → 서버 재시뮬 검증 → 보상 적용 | finalStateHash 동등 |
| MS-013 | 매치 종료 → 변조된 finalStateHash → 거부 | 부정 카운터 +1 |
| MS-014 | 매치 종료 → 동일 matchId 두 번째 호출 → 멱등 거부 | 이중 보상 방지 |

### 가챠 시나리오 (필수 100% 커버)
| ID | 시나리오 | 검증 |
|---|---|---|
| GS-001 | 골드 가챠 1회 (Common 60/Rare 30/Epic 10) | 분포 시뮬레이션 (10000회 ±2%) |
| GS-002 | 골드 가챠 50회 천장 → Epic 확정 | 확정 보장 |
| GS-003 | 보석 가챠 1회 (Mythic 0% 확인) | Mythic 절대 등장 X |
| GS-004 | 보석 가챠 80회 천장 → Legendary 또는 시즌 한정 확정 | 확정 + 천장 이월 |
| GS-005 | 가챠 idempotencyKey 중복 → 캐시된 결과 반환 | 이중 결제 방지 |
| GS-006 | 잔액 부족 → 402 + 결제 안 됨 | 트랜잭션 롤백 |
| GS-007 | 시즌 한정 코인 가챠 풀 진입 (시즌 시작) | 시즌 종료 시 풀에서 빠짐 |
| GS-008 | 듀플리케이트 → 별 카운터 증가 → 별업 | ★ 무한 + 디미니싱 곡선 검증 |
| GS-009 | ★N+ 풀 도달 후 듀플 → 범용 별 조각 변환 | 도태 방지 |

### 메타 시나리오 (필수 100% 커버)
| ID | 시나리오 | 검증 |
|---|---|---|
| META-001 | 일일 미션 4개 클리어 → 출석 → 보상 수령 | SP, 보석, 골드 누적 |
| META-002 | 시즌 패스 60티어 진행 (라이트 14일 시뮬) | 14일 ≈ 100% 도달 |
| META-003 | 시즌 패스 조기 완료 → 초기화 + 재구매 | 두 번째 시즌 정상 동작 |
| META-004 | 코인 ★3 잠금해제 패시브 발동 | 정체성 패시브 활성 |
| META-005 | 코인 ★5 시그니처 액티브 잠금해제 | 신규 능력 사용 가능 |
| META-006 | 영구 업그레이드 트리 노드 잠금해제 | 시작 자원 변화 |
| META-007 | 신규 가입 → FTUE 5분 → 무료 10연 | 튜토리얼 완전 진행 |
| META-008 | 30일 F2P 시뮬 (자동 데일리) | 코인 12종 보유, 핵심 5종 ★3 도달 |

### 별등급 시나리오 (Star Rank — ADR-0001)
| ID | 시나리오 | 검증 |
|---|---|---|
| STAR-001 | ★2 도달 시 스탯 +δ% 반영 | 선형 곡선 |
| STAR-002 | ★3 마이너 해금 발동 | 패시브 활성 |
| STAR-003 | ★5 시그니처 액티브 사용 | 쿨다운/효과 |
| STAR-004 | ★8 메이저 해금 (Epic+) | 메카닉 변형 (4→8방향 등) |
| STAR-005 | ★N+ 풀에서 듀플 → 별 조각 변환 | GS-009 보강 |
| STAR-006 | Legendary ★1→★10 누적 듀플 = 90 | 등차 2N |
| STAR-007 | Common ★1→★10 누적 듀플 = 36 | 기하 1.3 |
| STAR-008 | F2P 30일 픽 12종 평균 ★ 중앙값 ≥ 4 | F2P Reachability |
| STAR-009 | 고래 90일 DPS / F2P 평균 < 1.8x | WhaleGap |
| STAR-010 | ★12 Transcend 외형 변화 + 메카닉 추가 | Legendary/Mythic |
| STAR-011 | MarginalValue 단조 감소 (모든 등급, ★1→★50) | 한계효용 |
| STAR-012 | Common ★50 DPS < Legendary ★1 DPS | 캐치업 방지 |
| STAR-013 | Mythic ★5 DPS > Legendary ★8 DPS | 희귀 우월 |
| STAR-014 | Epic ★1→★10 누적 듀플 = 45 | 기하 1.2 |
| STAR-015 | δ 순서: Common(1.0%) < Rare(1.5%) < Epic(2.0%) < Legendary(3.0%) < Mythic(4.0%) | 등급 우월 |

### 경제·과금 시나리오 (Economy Gate — ADR-0001)
| ID | 시나리오 | 검증 |
|---|---|---|
| ECON-001 | F2P 30일 시뮬 → Epic 1종 ★5 도달 | 곡선 매칭 |
| ECON-002 | F2P 90일 → Epic 1종 ★8 도달 | 곡선 매칭 |
| ECON-003 | 고래 90일 → Legendary 1종 ★12 도달 | 곡선 매칭 |
| ECON-004 | WhaleGap @90d < 1.8x | P2W 톤업 가드 |
| ECON-005 | CrossTierCatchup < 0.7 | 캐치업 방지 |
| ECON-006 | 미스틱 가챠 외 획득 (패스/이벤트) — 슬롯 배타 룰 | Model 3 강제 |

### 미스틱 슬롯 시나리오 (Mythic Model 3)
| ID | 시나리오 | 검증 |
|---|---|---|
| MYT-A-001 | Slot A 동시 2종 편성 시도 → 거부 | 슬롯 배타 |
| MYT-B-001 | Slot B 동시 2종 편성 시도 → 거부 | |
| MYT-C-001 | Slot C 동시 2종 편성 시도 → 거부 | |
| MYT-D-001 | Slot D 동시 2종 편성 시도 → 거부 | |
| MYT-001 | 4 슬롯 1종씩 편성 → 정상 동작 | 최대 4 미스틱 |

### 밸런스 밴드 시나리오 (EPIC-032)
| ID | 범위 | 검증 |
|---|---|---|
| BAL-001~027 | 27 유닛 × ★1/3/5/8/12 단독 DPS 밴드 | balance-bands.json |
| SYN-* | 의도 시너지 페어 Δ% +15~30% | SynergyMatrix |

### 패시브 후크 시나리오 (ADR-0002, MatchSimulation 통합)

각 패시브 클래스는 **3개 축의 EditMode 시나리오**를 충족해야 한다:

1. **효과 발현** (예: AS Aura → 인접 코인 데미지/공속 증가)
2. **사용 안 함 케이스 무영향** (RNG 미소비 + Hash 변경 없음 — 기존 parity vector 유지)
3. **결정론** (동일 시드 N회 실행 시 final hash 일치)

| 패시브 | 테스트 클래스 | 커버 |
|---|---|---|
| AuraAttackSpeedBuff | `MatchSimulationAuraTests` | ✅ 3 case |
| AuraAttackPowerBuff | `MatchSimulationAttackPowerAuraTests` | ✅ 3 case |
| CritBuff | `MatchSimulationCritTests` | ✅ 4 case |
| DotApply | `MatchSimulationDotTests` | ✅ 4 case |
| SlowDebuff | `MatchSimulationSlowTests` | ✅ 4 case |
| InstantKillChance | `MatchSimulationInstantKillTests` | ✅ 4 case |
| RootChance | `MatchSimulationRootTests` | ✅ 4 case |
| RampOnSameTarget | `MatchSimulationRampTests` | ✅ 5 case |
| BounceAttack | `MatchSimulationBounceTests` | ✅ 4 case |
| PierceAttack | `MatchSimulationPierceTests` | ✅ 5 case |

**신규 패시브 클래스 추가 시 동일 3축 의무**. 또한:
- RNG 사용 패시브는 "보스 면역 / chance==0 시 RNG 미소비" 검증 (Rng.State 비교).
- 상태 누적 패시브 (DOT/Slow/Ramp)는 opt-in hash segment 사용 시 사용 안 한 케이스 hash 회귀 테스트 의무.
- per-coin 상태 추가 시 `CoinInstance` 또는 `EnemyState` 필드 default 값 검증 테스트 추가.

### 결제·광고 시나리오 (필수 100% 커버)
| ID | 시나리오 | 검증 |
|---|---|---|
| PAY-001 | Apple sandbox 보석 패키지 결제 → 영수증 검증 → 적립 | ASNV2 webhook 처리 |
| PAY-002 | Google sandbox 보석 패키지 결제 → 영수증 검증 → 적립 | RTDN 처리 |
| PAY-003 | 시즌 패스 구매 → 진행 가능 → 즉시 보상 5개 잠금해제 | 트랜잭션 |
| PAY-004 | 프리미엄 1개월권 구매 → 2배속 활성 + 광고 제거 + 일일 30💎 | 갱신·만료 |
| PAY-005 | Apple 환불 webhook → 보석/혜택 회수 | 잔액 차감, 음수 방지 |
| PAY-006 | Google 48시간 자동 환불 → 동기화 | 잔액 차감 |
| PAY-007 | 광고 시청 완료 콜백 → 보상 지급 | 서버 검증 |
| PAY-008 | 광고 시청 위조 시도 → 거부 | 보상 X |

### 네트워크·인프라 시나리오 (필수 100% 커버)
| ID | 시나리오 | 검증 |
|---|---|---|
| NET-001 | 인증 → 토큰 만료 → 리프레시 자동 | 끊김 없는 진행 |
| NET-002 | Rate limit 초과 → 429 + 재시도 | exponential backoff |
| NET-003 | 서버 점검 모드 → 클라 안내 화면 + 매치 시작 차단 | 어드민 토글 동작 |
| NET-004 | 미니PC 다운 → UPS → 자동 안전 종료 → 복구 | 데이터 무손실 |
| NET-005 | DB 백업 → 복구 → 데이터 정합 | 시간당 스냅샷 |

## 시나리오 테스트 프레임워크

### Unity (PlayMode)
```csharp
[UnityTest]
public IEnumerator MS_002_DefaultMerge_TwoSameRarity_NextRarityRandom()
{
    var harness = new MatchTestHarness(seed: "fixed-test-seed-MS002");
    harness.GiveCoin(CoinId.CoinPenny, count: 2, rarity: Rarity.Common);
    
    yield return harness.RunUntilCommand(new MergeCommand(slotA: 0, slotB: 1));
    
    Assert.AreEqual(1, harness.Board.CoinsAt(0).Count);
    Assert.AreEqual(Rarity.Rare, harness.Board.CoinsAt(0)[0].Rarity);
    Assert.AreEqual(harness.ExpectedHash("MS002"), harness.FinalStateHash);
}
```

### 헤드리스 시뮬 사용
- 시나리오는 Unity 에디터·디바이스 없이 `MatchSimulation` 클래스 직접 인스턴스화로 실행
- 시각·사운드 무시 (`TestMode = Headless`)
- CI에서 병렬 실행 가능 (시나리오당 독립 시드)

### 서버 통합 시나리오
```typescript
describe("PAY-001: Apple receipt verification + credit", () => {
  beforeEach(resetDb);
  it("verifies receipt, credits gem, persists IAP record", async () => {
    const account = await createTestAccount();
    const receipt = mockAppleReceipt({ productId: "gem_280", price: 3300 });
    const res = await app.inject({
      method: "POST", url: "/v1/iap/verify-apple",
      headers: { authorization: `Bearer ${account.token}` },
      payload: { receipt },
    });
    expect(res.statusCode).toBe(200);
    const profile = await getProfile(account.id);
    expect(profile.gem).toBe(280);
    const iap = await getLatestIap(account.id);
    expect(iap.validated).toBe(true);
  });
});
```

## 커버리지 목표

| 영역 | 단위 (line) | 시나리오 (state transitions) |
|---|---|---|
| Game/Simulation/ | ≥ 85% | **100%** |
| Game/ (전체) | ≥ 70% | **100%** (코어 루프) |
| Meta/ | ≥ 60% | **100%** (메타 루프) |
| Network/ | ≥ 70% | **100%** (네트워크 시나리오) |
| UI/ | ≥ 40% | (수동 QA + 시각 회귀) |
| server/services/ | ≥ 80% | **100%** (모든 라우트 시나리오) |
| server/routes/ | ≥ 90% | **100%** |
| server/lib/sim/ | ≥ 95% | **100%** + parity vectors |
| Editor/ | ≥ 50% | — |

## CI 게이트

PR 통과 조건:
1. ✅ 모든 단위 테스트 통과
2. ✅ 모든 통합 테스트 통과
3. ✅ 모든 시나리오 테스트 통과
4. ✅ 모든 sim parity vectors 통과
5. ✅ 라인 커버리지 임계값 충족 (위 표)
6. ✅ **Δ 시나리오 검사**: 신규/수정 코어루프 코드는 신규 시나리오 ≥ 1개 요구 (코드 라인 변경 + 시나리오 카운트 변경 비율 모니터링)
7. ✅ 코드 리뷰 통과 (`code-reviewer` 또는 `oh-my-claudecode:code-reviewer`)

게이트 실패 시 자동 머지 차단. AI 에이전트가 게이트 우회 시도 금지(헌법 위반).

## 시나리오 추가 의무

- 신규 코인 메카닉 → 시나리오 ≥ 2개
- 신규 적·보스 → 시나리오 ≥ 1개
- 신규 머지 룰 → 시나리오 ≥ 1개
- 신규 수익화 항목 (가챠 풀·패스 보상·IAP·광고) → 시나리오 ≥ 1개
- 신규 라우트 (서버) → 해피 + 에러 + 멱등 시나리오 각 1개
- 신규 맵 → 적 진행 + 코인 배치 검증 시나리오 1개
- 신규 보상 지급 경로(퀘스트/미션/스토리/해금/출석 등) → 인벤토리 수용 가능성, 원자적 지급, 반복 호출 멱등성, 저장/로드 후 재지급 방지 시나리오 각 1개 이상

## 관련 문서

- `rules/coding-standards.md` (Unity)
- `rules/path-based/server-node.md` (서버)
- `contracts/sim-protocol.md` (결정론 규약)
- 플랜의 EPIC-031 — Automated Test Coverage Sweep
