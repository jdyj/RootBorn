---
name: export-game-data
description: "ROOTBORN의 Unity 에셋(Assets/Data)을 Unity 없이 data/ 아래 JSON으로 다시 내보낸다. '데이터 내보내기', '데이터 변환 다시', 'data/ 갱신', 에셋을 고친 뒤 JSON을 맞출 때 사용."
---

# 게임 데이터를 JSON으로 내보내기

전환 전까지 원본은 Unity 에셋(`Assets/Data`)이고 `data/`는 변환 결과다. 변환기는 `Tools/Rootborn.DataExport`(Unity 불필요).

## 명령

```
C:\Users\jdyj\.dotnet\dotnet.exe run --project Tools/Rootborn.DataExport -- --repo .
C:\Users\jdyj\.dotnet\dotnet.exe test Tools/Rootborn.DataExport.Tests
```

- 인자: `--repo <저장소>`, `--out <폴더>`, `--allow-unresolved`.
- 풀지 못한 참조가 있으면 종료 코드 1이다. 2026-11-11 기준 12건이 알려져 있다(저장소에 없는 그림을 가리키는 책상 가구, 오피스 타일셋, Town2 LDtk 정의).

## 결과 읽는 법

- `data/<Assets/Data의 첫 폴더, 소문자>/<클래스 이름>.json`: 배열, id 순. 항목마다 `type`, `id`, `asset`.
- 다른 에셋 참조 `{"ref": id, "type": 클래스}`, 스프라이트 `{"sprite": png 경로, "name": 이름}`, 그 밖 `{"asset": 경로}`, 못 푼 것 `{"unresolved": …}`.
- bool은 0/1, enum은 정수. 필드 이름은 Unity 직렬화 이름에서 앞의 밑줄만 뗀 것.
- `data/atlases/<png 경로>.atlas.json`: 스프라이트 이름 → 사각형(왼쪽 위 기준).
- `data/localization/ko.json`과 `en.json`, `data/registry.json`, `data/export-report.json`.

## 확인

1. `data/export-report.json`에서 `unresolved`, `duplicateIds`, `missingPngs`, `parseErrors`를 이전과 비교한다. 늘었으면 원인을 찾는다.
2. `git status --short data`로 바뀐 파일을 본다. 에셋을 안 고쳤는데 바뀌면 변환기가 결정적이지 않은 것이다(버그).
3. 고친 에셋의 JSON을 원본 `.asset`과 눈으로 대조한다.
4. `data/`만 따로 커밋한다(`[ASSET]` 또는 `[CHORE]`, 한글).
