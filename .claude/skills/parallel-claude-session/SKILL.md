---
name: parallel-claude-session
description: "ROOTBORN 작업을 Orca 작업 폴더로 나눠 다른 Claude 세션에 맡기고, 끝나면 검토해 main에 합친다. '세션 열어서 진행', '나눠서 동시에', '다른 세션에 맡겨', '세션 결과 확인해 합쳐' 요청에 사용."
---

# 작업을 나눠 다른 Claude 세션에 맡기기

먼저 `orca-cli` 스킬을 불러온다. 동시에 도는 세션은 **3개까지**다(사용자 지시, 토큰 사용량 때문에 1개였다가 2개, 2026-11-12에 3개로). 서로 같은 파일을 고치지 않게 자른다.

## 맡기기

1. **겹치지 않게 자른다.** 세션마다 고칠 수 있는 폴더를 정한다. 같은 파일을 두 세션이 고치지 않는다.
2. **main을 푸시한다.** 새 작업 폴더는 원격 main에서 갈라진다. 미커밋·미푸시 내용은 따라가지 않는다.
3. **지시문을 UTF-8 파일로 쓴다.** 한글을 명령줄로 넘기면 깨진다. 지시문에 넣을 것: 먼저 읽을 문서, 목표, 만들 것, 고칠 수 있는 폴더, 지킬 것, 테스트 방법, 커밋 규칙(자기 브랜치에만, 푸시와 main 병합 금지), 끝났을 때 보고할 항목, "질문하지 말고 추천안으로 진행".
4. **작업 폴더를 만들고 지시문을 넣은 뒤 세션을 시작한다.**

```
orca worktree create --name <이름> --no-parent --json
cp <지시문> C:/Users/jdyj/orca/workspaces/farmer/<이름>/BRIEF.local.md
orca agent launch --agent claude --model <모델> --worktree "id:<repoId>::C:/Users/jdyj/orca/workspaces/farmer/<이름>" --prompt "Read the UTF-8 file BRIEF.local.md in the root of this worktree and follow it exactly. Do not commit or delete BRIEF.local.md. Work and report in Korean." --json
```

모델은 작업에 맞춰 고른다: 판단이 필요한 구현은 `claude-opus-5-5`, 기계적 변환과 검증은 `claude-sonnet-5-5` 이하. `repoId`는 `orca repo list --json`.

## 상태 보기와 멈추기

- `orca worktree ps --json`: 세션 상태(`working`/`done`), 상태 메모, 마지막 메시지.
- 멈추기: `orca terminal send --terminal <handle> --text "STOP NOW. Commit only work that passes tests, leave a paused comment with what remains, then stop." --enter --json`. 터미널 handle은 `orca terminal list --json`.
- 막 시작해 잃을 것이 없으면 `orca terminal close --worktree <selector> --all --json`.

## 끝난 세션 검토와 합치기

세션의 보고를 그대로 믿지 않는다. 직접 확인한다.

1. 마지막 메시지를 읽는다. "확인하지 못한 것"과 "판단이 갈린 곳"을 본다.
2. `git -C <작업 폴더> status --short`와 `git diff --stat main...HEAD`로 범위 밖을 고치지 않았는지 본다.
3. main에서 `git merge --squash jdyj/<이름>` 뒤 **테스트를 직접 다시 돌린다.** 화면 작업이면 스크린샷을 직접 연다.
4. 커밋 메시지에 누가 만들고 무엇으로 확인했는지 적는다. 푸시한다.
5. 증거(`output/`)가 필요하면 main 쪽으로 복사한 뒤 작업 폴더와 브랜치를 지운다: `orca worktree rm --worktree <selector> --force --json`, `git branch -D jdyj/<이름>`.
6. 세션이 다른 영역 대신 둔 임시 인터페이스는 합친 뒤 조정 담당이 잇는다.
