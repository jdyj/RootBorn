---
name: unity-build-deploy
description: "Unity 게임 빌드 및 배포 오케스트레이터. CLI batchmode 빌드 → 산출물 검증 → 플랫폼별 업로드(itch.io butler, Steam steamcmd, Android Google Play, WebGL 호스팅) 순서로 조율. '빌드해줘', '배포해줘', 'release' 트리거."
---

# Unity Build & Deploy Orchestrator

Unity 게임 빌드부터 배포까지 통합 파이프라인. 프로덕션 배포는 사용자 명시 승인 필수.

## 실행 모드: 에이전트 팀

## 에이전트 구성

| 팀원 | agent_type | 역할 |
|------|-----------|------|
| unity-builder  | unity-builder  | CLI 빌드 실행, 로그 파싱 |
| deploy-manager | deploy-manager | 플랫폼 업로드, 릴리스 노트 |
| code-reviewer  | code-reviewer  | 빌드 전 최종 sanity 리뷰 (변경 소폭일 때만) |
| (리더)         | —              | 승인·조율·보고 |

## 워크플로우

### Phase 1: 준비
1. 사용자 입력 파악 — 타겟 플랫폼, 버전, 배포 채널(staging/production)
2. `.claude/rules/unity-cli.md` 로드
3. `Assets/Editor/BuildScript.cs` 존재 확인, 없으면 생성
4. 버전 번호 결정 — 자동 번프 금지, 사용자 입력 또는 현재 `ProjectSettings` 값
5. `_workspace/` 준비

### Phase 2: 팀 구성

```
TeamCreate(
  team_name: "unity-build-deploy-team",
  members: [
    { name: "unity-builder",  agent_type: "unity-builder",  model: "opus",
      prompt: "unity-cli-reference 로드. 지정된 타겟으로 빌드 실행. 로그 파싱 후 에러/경고 보고." },
    { name: "deploy-manager", agent_type: "deploy-manager", model: "opus",
      prompt: "빌드 산출물 수신 후 지정 플랫폼에 업로드. 사용자 승인 전까지 production 금지." }
  ]
)
```

> code-reviewer는 옵션. 소폭 변경일 때만 추가.

### Phase 3: 빌드
1. unity-builder가 Editor 프로세스 체크
2. CLI 빌드 실행 (`run_in_background: true`, 타임아웃 30분)
3. 종료 후 `Logs/build-{target}.log` 파싱
4. `_workspace/build_report.md` 작성
5. 실패 시 1회 재시도, 2회 실패 → 루프 전환 제안 또는 사용자 보고

### Phase 4: 배포 전 검증
- 산출물 존재, 최소 크기, 실행 가능 여부 (exe/apk/bundle 파일 타입)
- 버전 태그 일치 확인
- 사용자에게 배포 승인 요청 (메시지 + 요약 제시)

### Phase 5: 배포
사용자 승인 후:
1. deploy-manager가 플랫폼별 명령 실행
   - itch.io: `butler push`
   - Steam: `steamcmd +run_app_build`
   - Android: Google Play Console API 또는 `gradle bundleRelease` → 수동 업로드 힌트
   - WebGL: 호스팅 업로드 (s3/netlify)
2. 업로드 결과 수집 → `_workspace/deploy_report.md`
3. 릴리스 노트 자동 생성 (git log 기반, 최근 태그 이후)

### Phase 6: 정리 및 보고
- 팀 해체
- 최종 결과 요약을 사용자에게 제공
- 배포 URL, 버전, 체크섬 포함

## 데이터 흐름

```
요청 → unity-builder → Builds/{target}/ + _workspace/build_report.md
                           ↓ SendMessage
                       사용자 승인
                           ↓
                       deploy-manager → 플랫폼 업로드 → _workspace/deploy_report.md
                           ↓
                       리더 → 최종 보고
```

## 에러 핸들링

| 상황 | 전략 |
|------|------|
| Editor 실행 중 | 사용자에게 종료 요청 |
| 빌드 실패 | 1회 재시도 (`Library/` 캐시 문제 가능), 2회 실패 시 csharp-developer 호출 |
| 서명/인증 누락 | 사용자에게 env/키 입력 요청 |
| 업로드 실패 | 1회 재시도, 2회 실패 시 사용자 보고 |
| 버전 충돌 | 자동 번프 금지, 사용자 확인 |

## 금지

- **사용자 승인 없이 production 배포 금지**
- API 키/토큰을 로그/커밋에 노출 금지
- 동일 버전 덮어쓰기 전 확인 (itch.io 재업로드는 허용, Steam은 빌드 ID 증가)

## 테스트 시나리오

### 정상 흐름
1. 사용자: "win64 빌드 후 itch.io staging에 올려줘, 버전 0.1.0"
2. unity-builder가 `BuildWindows64` 실행 → 42MB exe 생성
3. 검증 통과, 사용자에게 요약 제시 → 승인
4. deploy-manager가 `butler push ... user/game:win64-staging` 실행
5. 업로드 성공, URL 보고

### 에러 흐름
1. 빌드 중 컴파일 에러 (Assets/Scripts/Player/PlayerController.cs:42)
2. unity-builder가 에러 파싱, 1회 재시도, 재실패
3. 사용자에게 에러 상세 보고, dev-iteration-loop 전환 제안
