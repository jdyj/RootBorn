---
name: unity-cli-reference
description: "Unity 6 batchmode CLI 호출 레퍼런스. 빌드/테스트/에디터 자동화 명령어, BuildScript 템플릿, 로그 파싱 규칙, 락 충돌 회피. unity-builder/unity-test-runner/deploy-manager 에이전트가 필요 시 로드. '유니티 빌드'/'유니티 테스트'/'Unity CLI' 키워드에서 트리거."
---

# Unity CLI Reference

Unity 6000.3.13f1 batchmode CLI 호출의 공식 참조. 하네스 내 Unity 관련 에이전트가 이 스킬을 읽어 일관되게 Unity를 구동한다.

## 상세 내용

**반드시 `.claude/rules/unity-cli.md`를 Read하라.** 해당 파일이 정식 규칙이며, 이 스킬은 포인터 역할이다.

## 요약

| 작업 | 핵심 커맨드 |
|------|-----------|
| Windows 빌드 | `Unity.exe -batchmode -nographics -quit -projectPath ... -executeMethod Game.Editor.BuildScript.BuildWindows64 -buildTarget StandaloneWindows64` |
| EditMode 테스트 | `Unity.exe -batchmode -nographics -projectPath ... -runTests -testPlatform EditMode -testResults Logs/test-editmode.xml` |
| PlayMode 테스트 | `Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testResults Logs/test-playmode.xml` (※ `-nographics` 금지) |

## 주의사항

- **Unity Editor 실행 중이면 CLI 실패** — 락 충돌. 사전 프로세스 체크 필수
- **PlayMode는 -nographics 금지** — 렌더/코루틴 필요
- **종료 코드만 믿지 말 것** — 로그/XML 파싱 병행
- **BuildScript.cs 없으면 생성** — 템플릿은 `.claude/rules/unity-cli.md` 참조

## 경로 컨벤션

- 빌드 산출물: `Builds/{target}/`
- 로그: `Logs/build-{target}.log`, `Logs/test-{platform}.log`, `Logs/test-{platform}.xml`
- 작업 중간 산출물: `_workspace/`

## 트리거

Claude는 다음 상황에서 이 스킬을 참조한다:
- Unity 빌드 스크립트 작성/수정
- EditMode/PlayMode 테스트 CLI 실행
- Unity batchmode 호출 디버깅
- 빌드 로그 파싱

규칙 파일(`rules/unity-cli.md`)이 500줄 이내라 전체 로드가 가능하며, 세부 실행 예시와 스크립트 템플릿은 모두 그곳에 있다.
