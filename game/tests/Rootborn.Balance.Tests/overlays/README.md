# 덮어쓰기 폴더 (시험용 수치)

`data/`를 고치지 않고 수치를 바꿔 균형 시뮬레이터를 돌릴 때 쓰는 폴더를 여기에 `<이름>/`으로 둔다. 원격 실행은 커밋된 것만 받으므로 커밋하고, 저장소 뿌리 기준 상대 경로로 가리킨다.

```bash
bash Scripts/remote-test/run.sh HEAD -- game/tests/Rootborn.Balance.Tests --filter "FullyQualifiedName~WriteFullReport" \
  -e ROOTBORN_DATA_OVERLAY=game/tests/Rootborn.Balance.Tests/overlays/<이름>
```

- 모양과 규칙: `Scripts/remote-test/README.md` "수치를 덮어써서 돌리기". 돌리는 순서: 스킬 `balance-sim-run`.
- 게임은 이 폴더를 읽지 않는다. 배포에 들어가지 않는다.
- 여기의 값은 시험용이다. 정해진 값은 사용자가 정한 뒤 `data/`·`game/`에 옮기고, 끝난 폴더는 지운다.
