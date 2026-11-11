---
name: game-code-review
description: "ROOTBORN MonoGame 판 코드 리뷰. 헌법·규칙 준수, 프로젝트 참조 방향, 성능, 테스트 적정성을 본다. PR 전, 기능 완성 후 '리뷰해줘', '코드 검토', 'review' 요청에 사용."
---

# 코드 리뷰

완성된 변경을 합치기 전에 점검한다. 작은 변경은 `code-reviewer` 에이전트 하나로 충분하다.

## 순서
1. 범위: `git diff origin/main...HEAD`(또는 사용자가 정한 범위)의 파일 목록.
2. `code-reviewer`에게 범위를 주고 리뷰를 맡긴다. 점검 기준은 `AGENTS.md` 절대 원칙과 `.claude/rules/`(코딩 표준, 데이터 드리븐, 화면, 현지화, 환경 값).
3. 변경이 새 규칙·흐름·화면이면 `test-designer`에게 같은 범위의 테스트가 빠진 곳을 묻는다(`rules/testing-discipline.md` 기준).
4. 두 결과를 합쳐 P0(바로 고침) / P1(권장) / P2(선택)로 보고한다. 파일:줄, 문제, 고치는 법을 쓴다.
5. 지적은 코드로 확인한 뒤 반영한다. P0는 `bug-fixer`나 직접 고친다.

## 보고에 넣을 것
- 범위(파일 수), 건수(P0/P1/P2), 테스트가 빠진 곳.
- 돌려 본 명령과 결과: `dotnet test` 프로젝트별 건수, `bash Scripts/ci/run-all.sh --checks-only`.
