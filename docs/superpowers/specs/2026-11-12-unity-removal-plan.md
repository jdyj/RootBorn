# Unity 제거 준비 — 조사와 계획 (프로젝트 7)

- 날짜: 2026-11-12 (main `306e1ba1f` 기준)
- 상태: **조사와 계획만.** 파일을 지우거나 옮기지 않았다. 사용자가 4절의 결정을 내린 뒤 5절의 단계를 하나씩 한다.
- 근거 스크립트: `bash Tools/unity-removal/list.sh` (약 10초, 읽기만 한다). 결과는 `output/unity-removal/`(git 밖).
- 표시: "작성자 판단"은 이 문서를 쓴 Claude의 추천이다. 사용자가 정한 것이 아니다.
- 기준본: 태그 `unity-playable-20261111`에 Unity 판 전부가 있다. 지워도 이 태그로 복구된다(확인: 태그는 로컬과 원격(`origin`)에 모두 있고 그 트리에 Assets, Packages, ProjectSettings가 모두 들어 있다).

## 1. 저장소에 있는 것

추적되는 파일 **121,202개, 474.4MB**(`git ls-tree -r -l HEAD`). 근거 명령은 스크립트 안에 있다. 한 파일은 한 분류에만 든다.

| 분류 | 뜻 | 파일 | MB |
|---|---|---:|---:|
| 가 | MonoGame 판이 실행 중에 읽는 것 (`Tools/publish/content.txt` 규칙) | 3,414 | 15.0 |
| 가* | 실행 중에 읽지만 content.txt에 **빠진** 것 (`game/themes`) | 3 | 0.0 |
| 나 | 테스트가 읽는 고정 파일(`game/tests`) 22 + 테스트가 직접 여는 Unity 씬 1 | 23 | 0.6 |
| 다 | 변환기가 원본으로 읽는 Unity 에셋 | 10,223 | 9.9 |
| 라 | Unity 전용 (코드, 씬, 프리팹, 설정, `.meta`, 생성 에셋, Packages 정보) | 74,538 | 329.6 |
| 라' | 그림·맵 원본 팩 중 게임이 읽지 않는 파일 (대부분 PNG) | 32,205 | 101.2 |
| 마 | 문서·도구·검사·에이전트·루트 파일 | 262 | 14.8 |
| 바 | 남는 코드 (`game/`, `Tools/Rootborn.*`, Core 소스) | 534 | 3.8 |

세부 (스크립트 `summary.tsv`, `by-folder.tsv`):

- **가 3,414개**: JSON 1,784(좌표표 1,575 + 데이터·월드·현지화 209), PNG 1,606, 맵 `.ldtk` 3 + 레벨 `.ldtkl` 21. PNG의 대부분은 `Assets/moderninteriors-win` 가구 1,473개(2.3MB). 큰 것은 타일셋 `Assets/Modern_Exteriors_32x32` 12개(4.7MB)와 맵 `Assets/LDtk` 24개(3.9MB).
  `Tools/publish/publish.sh`에는 목록만 내는 방법이 없어서(`bash publish.sh`는 게시까지 한다) list.sh가 같은 규칙을 다시 만든다. 그림 경로 읽기에서 `data/atlases`의 JSON도 훑는 것(`@refs data/**/*.json`의 `**`)까지 같게 했다. 결과로 목록에 있는데 저장소에 없는 파일은 0개.
- **다 10,223개**: `Assets/Data` 7,852(`.asset` 3,829 + `.meta`), 문구 카탈로그 2, 스크립트 `.cs.meta` 764(m_Script guid로 클래스 이름을 얻는 데 쓴다), 실행용 PNG 1,605개의 `.png.meta`(스프라이트 자르는 표, 5.2MB).
- **라 74,538개** 중 큰 덩어리: 그림·맵 팩 안의 `.meta`와 생성 에셋 72,054(291MB; `Assets/LDtk/Generated`의 Unity 타일 에셋 5,050 포함), `Assets/` 코드·설정 2,428(Plugins 17.8MB, Scenes 7.2MB, Tiles 6.1MB, Tests 1,041개, Scripts 709개), Packages·ProjectSettings 56.
- **라' 32,205개**: `Assets/Modern_Exteriors_32x32` 13,423(71.5MB), `moderninteriors-win` 14,691(19.7MB), `Assets/Gif`(Super_Retro_Collection, 옛 Farm용) 3,636(5.9MB), `modernuserinterface-win` 323, `Modern_Farm_v1.2` 41, `Art` 88. 이 중 게임이 쓰는 건 가에 이미 들어 있다.
- **마**: `docs/art` 12.5MB(대부분 `modern-asset-inventory.json` 6MB), `production/qa` 50개, `.claude`·`.agents` 76개(에이전트 14, 스킬 24, 규칙 16), `Scripts` 중 Unity를 가리키는 21개(`fevm` 9, `qa` 5, `unity` 1, `validation` 1, `ci`의 Unity 검사 5), 찌꺼기 11개(7절 단계 3).
- **바 534개**: Core 소스 145(`Packages/com.rootborn.core/Runtime`), `game/` 코드·테스트, `Tools/Rootborn.Core.Tests`(81), `DataExport`(6)와 그 테스트(5).

기록 크기: `.git` 팩 307MB. 지워도 기록에는 남으므로 **저장소 크기는 줄지 않는다**. 작업 폴더 크기만 줄어든다(474MB → 라와 라'와 다를 다 지우면 약 35MB, 라'를 남기면 약 136MB). 줄이려면 기록을 다시 쓰는 일인데 이 계획에는 넣지 않았다(7절 끝, 사용자 결정).

## 2. Unity 에셋이 아직 원본인 것 → `data/`가 원본이 될 때

지금 규칙(`engine-migration.md`): 원본은 `Assets/Data`(3,829 `.asset`), `data/`는 변환 결과(엔티티 2,289개, 현지화 키 1,096개).

바뀌는 것:

- **변환기 `Tools/Rootborn.DataExport`와 그 테스트**: 입력(Assets/Data, `.meta`)이 없어지면 돌릴 수 없다. 테스트(5건 파일)는 임시 폴더에 가짜 저장소를 만들어서 Unity 없이도 돌지만, 지키는 대상이 없다. 작성자 판단: 마지막 한 번 변환해 `data/`를 고정한 뒤 변환기와 테스트를 지운다(태그에 있다). `export-game-data` 스킬(양쪽)과 `engine-migration.md`의 "원본" 줄도 함께 지운다.
- **"커밋된 JSON == 변환 결과" 검사**: **없다.** `ci.yml`은 변환기 단위 테스트만 돌리고 변환 결과 비교는 하지 않는다(`export-report.json`은 사람이 읽는 보고서). 그래서 없앨 검사도 없다.
- **`data/`를 직접 고칠 때의 검증**: `Rootborn.Data.Tests`(`RealDataTests` 등)가 하는 일은 실제 `data/` 전체를 읽고 정의를 만드는 것(id 중복, 끊어진 참조, 형식)이다. 변환기가 보고서로 알려 주던 다음은 **지금 어느 테스트도 안 본다**: 데이터가 가리키는 PNG가 저장소에 있는지(`missingPngs`), 그림마다 좌표표가 있는지, 현지화 키 빠짐(화면별 키 검사는 `Rootborn.Screens.Tests`와 `check-localization-keys.sh`가 일부 한다). `publish.sh`가 "없는 파일" 경고를 낼 뿐 막지 않는다. → 새 테스트가 필요하다(5절 단계 1).
- **`data/`의 `asset` 필드**(원본 경로, 비-좌표표 JSON 2,367줄): 읽는 코드는 `EntityNode.Source`(오류 메시지에 출처로 쓴다)뿐이다. 값이 사라진 파일을 가리키게 되지만 코드는 깨지지 않는다. 작성자 판단: 당분간 둔다. 새 엔티티는 `asset` 없이 만들 수 있게(필드가 선택인지는 확인 못함, 7절) 하고, 나중에 일괄 삭제는 선택.
- **`data/.gitattributes`, `export-report.json`(345KB)**: 변환기가 사라지면 보고서는 낡은 기록이다. 지운다.
- 규칙·문서: `engine-migration.md`의 "게임 데이터와 맵의 원본" 줄을 "`data/`와 LDtk가 원본"으로 고치고, `assets-data.md`·`AGENTS.md` 2·4번의 "SO" 용어는 데이터 파일 규약으로 다시 쓴다(설계 문서 8절).

## 3. `Packages/com.rootborn.core`의 처지

Core 소스는 `.cs` 145개. Unity 패키지 폴더에 있고 `Rootborn.Core.asmdef`(`noEngineReferences: true`), `package.json`, `.meta`들이 붙어 있다. 옮기면 `.meta`(145 `.cs.meta` + 폴더 `.meta`)·asmdef·`package.json`은 지운다. 소스는 `game/Rootborn.Core/`로 옮긴다(작성자 판단: `game/Rootborn.Core/src/` 아래, 지금 그 폴더는 csproj 하나뿐이라 자리가 있다).

고쳐야 할 곳 (`git grep "Packages/com.rootborn.core"` 22개 파일):

| 곳 | 바뀌는 것 |
|---|---|
| `game/Rootborn.Core/Rootborn.Core.csproj` | `Compile Include`를 `src/**/*.cs`로. 주석 "그대로 묶는다" 삭제 |
| `Tools/Rootborn.Core.Tests/Rootborn.Core.Tests.csproj` | `../../game/Rootborn.Core/src/**/*.cs`를 포함하거나, 소스를 직접 담는 대신 `ProjectReference`로 바꾼다(작성자 판단: 참조로 바꾸면 한 번만 컴파일된다. 단 테스트가 `internal`을 쓰면 안 된다 — 확인 못함) |
| `Scripts/ci/check-core-references.sh` | `CORE` 경로. **asmdef의 `noEngineReferences` 검사는 asmdef가 없어지므로 뺀다**(대신 csproj에 `UnityEngine` 참조가 없는지는 이미 `using` 검사가 막는다) |
| `Scripts/ci/check-meta-files.sh` + `tests/test-meta-files.sh` | **통째로 지운다**(`.meta`가 없어진다). `run-all.sh`의 `CHECKS` 배열에서 `meta-files`도 뺀다 |
| `check-monogame-references.sh`, `check-no-entity-id-branching.sh`, 그 시험 3개 | 경로 상수 한 줄씩 |
| `.github/workflows/ci.yml` | 주석 한 줄 |
| `Scripts/ci/README.md`, `game/README.md`, `game/world/README.md`, `.claude`·`.agents`의 `port-rule-to-core` 스킬 2개, `engine-migration.md`, `core-logic-and-play-evidence.md`(asmdef 언급), `coding-standards.md`, `AGENTS.md` | 경로와 "asmdef" 말 |
| `docs/superpowers/specs/*`, `handoffs/*` (5개) | 과거 기록이므로 고치지 않는다 |
| 변환기(`ScanDirs = Assets, Packages`) | 변환기를 지우면 해당 없음. 그 전에 옮기면 `.cs.meta`를 못 찾아 클래스 이름이 안 풀린다 → **변환기를 지운 뒤에 Core를 옮기거나, 옮기기 전에 마지막 변환을 끝낸다**(순서 문제, 5절에 반영) |

LangVersion 9는 "Unity가 컴파일하는 버전과 같다"는 이유로 있다. Unity가 없어지면 올릴 수 있지만 그건 별도 결정이다(작성자 판단: 지금은 건드리지 않는다).

## 4. 그림과 맵 파일의 처지

게임이 읽는 그림·맵은 가의 3,414개 중 PNG 1,606 + LDtk 24. 전부 `Assets/` 아래 흩어져 있다.

**경로를 가리키는 곳 (센 것):**

| 곳 | 수 |
|---|---:|
| `data/` 비-좌표표 JSON의 그림 경로 | 17개 파일, 3,516줄 (서로 다른 PNG 1,580개) |
| `data/atlases/`: 파일 이름이 그림 경로를 따라가고 안에 `"image"` 필드도 있다 | 1,575개 파일 |
| `game/themes/{urban,style2}/theme.json` | 62줄 (PNG 17개) |
| `game/world/maps.json`의 LDtk 경로 | 10줄 (`interiors.ldtk` 7, `town.ldtk` 2, `house.ldtk` 1) |
| `.ldtk` 안의 타일셋 `relPath` | `town` 14, `interiors` 13, `house` 11 (상대 경로라 폴더 구조까지 같이 옮겨야 한다) |
| `Tools/publish/content.txt` | 2줄 |
| 코드 | `HostOptions.cs`(저장소 루트를 `Assets/modernuserinterface-win`이 있는 폴더로 찾는다) 1곳 |
| 테스트가 `Assets/…` 경로를 쓴다 | 7개 파일 (`HouseSizeScreenTests`, `UiSkinDataTests`, `CharacterArtTests`, `HouseSizeTests`, `MapContractTests`, `MissingPathsTests`, `WorldMapTests`) |

사용자의 계획(그림·맵 전부 새로 그림, `engine-migration.md` "그림은 갈아 끼울 수 있게")을 놓고 두 길을 비교했다.

- **지금 옮긴다** (예: `game/content/`로): 위 표의 약 5,200곳을 고치거나 다시 만든다(대부분 기계적이지만 좌표표 1,575개는 파일 이름이 바뀐다). 맵 3개의 `relPath`도 맞춰야 하고, 어떤 파일을 어디로 보낼지 정하는 일이 생긴다. 옮긴 지 얼마 안 되어 새 그림이 오면 같은 곳을 또 고친다.
- **옮기지 않고 두고, 새 그림이 오면 새 자리에 넣는다**: 지금 쓰는 3,414개(15MB)는 `Assets/…` 그대로 둔다. 새 그림과 맵은 처음부터 새 자리(작성자 판단: `game/content/`)에 두고 데이터가 그리로 가리키게 하면, 옛 파일은 대체되는 만큼 지워진다. 5,200곳을 두 번 고치지 않는다.

**작성자 판단: 옮기지 않는다.** 대신 두 가지만 지금 한다. (1) Unity가 없는데 폴더 이름이 `Assets`라 헷갈리는 문제는 문서(`game/README.md`)에 "Unity 이전 이름이고 새 그림이 오면 사라진다"고 적는다. (2) 저장소 루트 찾기를 `Assets/modernuserinterface-win`에 기대지 않게 한다(표지 파일 하나, 예: `game/world/maps.json`이 있는 위 폴더). 이유는 Unity를 지울 때 `modernuserinterface-win`의 나머지 322개는 지워지는데 그림 한 장이 루트 표지라서 폴더를 못 지우고 이름이 퍼지기 때문이다. `content.txt`의 `@png` 한 줄도 같이 바뀐다.

**라이선스 (확인 못함, 사용자 질문):** 가의 PNG는 Modern Exteriors·Modern Interiors·Modern UI(LimeZu 유료 팩으로 보이나 `LICENSE`는 `Assets/Modern_Farm_v1.2/LICENSE.txt` 하나만 있다)에서 온 것이다. 저장소가 공개인지에 따라 원본 팩 파일(라' 101MB, 가의 일부)을 저장소에 두는 것이 허용되는지 달라진다. 이 조사는 라이선스 문서를 읽지 않았다.

## 5. Unity를 가리키는 남은 것

| 곳 | 상태 |
|---|---|
| `.github/` | 워크플로 `ci.yml` 하나. 이미 dotnet 기준이다. Unity 언급은 주석 몇 줄과 `check-meta-files`(단계 2에서 삭제) |
| `Scripts/` | Unity 전용 21개: `fevm/`(9, Unity 원격 테스트), `qa/`(5, 멀티플레이 스모크 — `.ps1`이 Unity 플레이어를 부른다), `unity/run-tests.ps1`, `validation/…Preflight.ps1`, `ci/`의 `check-addressables-linkxml-preserved.ps1`, `run-multiplayer-validation-gate.ps1`, `report-playmode-bypasses.py`, `check-meta-files.sh`와 그 시험. 남기는 것: `ci/`의 나머지, `git-hooks/`. `pre-push`·`pre-commit` 훅이 Unity를 부르는지는 확인 못함(`pre-push`에 Unity 언급 4줄) |
| `.gitignore` | `# Unity` 항목: `[Ll]ibrary/`, `[Tt]emp/`, `[Oo]bj/`(**dotnet에도 필요**하니 남김), `[Bb]uild*/`, `[Ll]ogs/`, `[Uu]serSettings/`, `*.unityproj`, 그리고 **`*.csproj`·`*.sln`**: 이것 때문에 `game/`·`Tools/`의 csproj는 강제 추가(`git add -f`)로만 추적된다. Unity가 없어지면 이 두 줄을 지워야 새 csproj가 무시되지 않는다. `Assets/LDtk/**/backups` 줄은 LDtk 편집기용이라 남긴다 |
| `.claude/` | 에이전트 14개 중 Unity 전용 `csharp-developer`, `bug-fixer`, `test-designer`, `test-result-analyzer`, `qa-inspector`, `deploy-manager`, `t3-gameplay-programmer`, `t1-technical-director`(Unity 표현이 5~8줄씩). 스킬 `gs-*` 16개와 `.claude/docs/templates` 15개는 coin-defense 시절 하네스라 Unity 말은 적지만 지금 게임과 무관하다(작성자 판단: 사용자에게 지울지 묻는다). `settings.json`의 Unity 8줄, `README.md`·`constitution.md`·`changelog.md`(이력이라 지우지 않음), `testing-discipline.md`·`coding-standards.md`·`sprite-slicing.md`·`assets-*.md`·`ui-standards.md`는 설계 문서 8절의 표대로 다시 쓰거나 지운다 |
| 루트 | `AGENTS.md`(Unity 14줄), `.vsconfig`(Unity용 Visual Studio 구성). **추적되는 `*.sln`/`*.csproj`는 Unity 것이 없다**: 추적되는 것은 `game/Rootborn.sln`과 `game/`·`Tools/`의 csproj 뿐이다. 사용자 폴더(`D:/User/farmer`)에는 Unity가 만든 `farmer.sln`과 `Assembly-CSharp*.csproj` 등이 git 밖으로 있다 |
| 찌꺼기 | 루트 `.tmp-*.json` 9개(집 확장 테스트 출력), `Builds/Logs/…png` 2개(`.gitignore`에 `Builds/`가 있는데 추적됨), `production/qa/`(Unity 시절 증거 50개), `docs/art`의 거대 인벤토리 |
| 사용자의 커밋 안 한 Unity 파일 변경 (`git -C D:/User/farmer status`, **손대지 않았다**, 목록만) | 변경: `Assets/AddressableAssetsData/Windows/addressables_content_state.bin`, `Assets/Gif/…/SuperRetro_LegacytilePalette.prefab`. 새 파일: `Assets/InitTestScene*.unity`(+`.meta`) 4쌍(테스트가 만든 임시 씬). 이 조사 폴더(`unity-removal-plan`)에는 Unity 변경이 없다. 같은 사용자 폴더에 `game/` 아래 스테이징된 변경도 있다(다른 세션의 작업) |

## 6. 위험

- **지우면 못 돌리는 것**: Unity 판 실행·편집·빌드·Unity 테스트(EditMode 2,090건, PlayMode 166건)·FEVM의 Unity 검증·Unity 시절 QA 스크립트·변환기 재실행. 모두 태그 `unity-playable-20261111`을 체크아웃하면 돌아온다(작성자는 체크아웃을 해 보지 않았다 — 확인 못함). 태그를 지우지 않는 것이 전제다. 태그는 원격(`origin`)에 있는 것을 `git ls-remote`로 확인했다.
- **테스트가 Unity 파일에 기대는 곳**: 사본인지 직접 읽는지 따졌다.
  - `unity-baked-collision.json`(World.Tests/Fixtures): **사본**이다(씬에서 뽑아 둔 JSON). 안전.
  - `MissingPathsTests.School_…`: **`Assets/Scenes/School.unity`를 직접 읽는다**(씬 YAML에서 `Structure_x_y` 이름을 정규식으로 뽑는다). Unity를 지우면 이 테스트 1건이 깨진다. 같은 모양의 사본 JSON으로 바꿔야 한다(단계 1). 유일하다: `game/tests`와 `Tools/Rootborn.Core.Tests`에서 `.unity`·`.asset`·`.prefab`을 읽는 테스트는 이것 하나다(합성 가짜 경로 문자열은 제외).
  - 그 밖의 `Assets/…` 사용은 LDtk·PNG(가에 있고 유지)와 설명 주석이다. `Assets/LDtk`의 `.ldtk` 3개는 가에 속한다(`HouseSizeTests` 등이 읽음).
  - `Tools/Rootborn.DataExport.Tests`는 임시 가짜 저장소를 만든다(Unity 파일 의존 없음).
- **`game/themes` 빠짐 (지금 있는 버그)**: Host의 기본 테마는 `game/themes/urban`(`ThemeFolder`: 폴더가 없으면 오류로 멈춘다)인데 `content.txt`에 `game/themes`가 없어서 `publish.sh` 결과에는 테마 JSON이 안 들어간다. 테마가 가리키는 PNG 17개는 이미 `@refs`로 들어간다. 목록에 `game/themes/**` 한 줄이면 된다(필요한 변경, 7절).
- **저장소 크기**: 1절대로 `.git`은 줄지 않는다(307MB). 작업 폴더는 474MB → 약 35MB(가 15 + 나 0.6 + 마 14.8 + 바 3.8, 라'까지 지운 경우). 라'를 남기면 약 136MB.
- **다른 세션 충돌**: 지우기는 `git rm` 수만 개 파일이라 다른 세션 브랜치와 충돌한다(예: `game/`을 고치는 세션은 겹치지 않지만 `.claude/rules` 등은 겹침). 마지막에 한꺼번에 하지 않고 5절의 순서대로 작은 커밋으로 나눈다. 지우는 일은 조정 담당 세션이 하고 다른 세션이 모두 main 위로 맞춘 직후에 한다.
- **Windows 경로 길이**: `data/atlases/…`의 일부 경로가 길어서 작업 폴더에 대고 `git grep`을 하면 "Filename too long"이 난다(이 조사에서 확인). list.sh는 커밋된 기록(`HEAD`)을 읽어서 피한다. 단계 4에서 좌표표를 다룰 때 이 문제를 조심한다.
- **Core 옮기기와 변환기의 순서**(3절): 변환기가 Packages의 `.cs.meta`를 읽는다.

## 7. 추천 순서 (작성자 판단)

단계마다 커밋 하나, 끝나면 `dotnet test game/Rootborn.sln`, `dotnet test Tools/Rootborn.Core.Tests`, `bash Scripts/ci/run-all.sh --checks-only`로 확인한다(통과 줄뿐 아니라 프로젝트별 건수가 줄지 않았는지). 지우기 전에는 `list.sh`를 다시 돌려 목록과 수치를 새로 만든다.

| # | 단계 | 하는 일 | 확인 |
|---|---|---|---|
| 0 | **필요한 변경(작은 것, Unity 파일은 안 건드림)** | (가) `content.txt`에 `game/themes/**` 추가. (나) 저장소 루트 표지를 `Assets/modernuserinterface-win`에서 다른 파일로(HostOptions, publish README, `@png` 줄). (다) `MissingPathsTests.School_…`를 사본 JSON(`school-baked-collision.json`)으로 바꾼다 — 사본은 지금 School.unity에서 한 번 뽑는다. (라) 데이터 검사 테스트 추가: 데이터가 가리키는 PNG와 좌표표가 모두 있다, `.ldtk`의 타일셋이 모두 있다. | 위 테스트 통과(건수 +), `publish.sh`로 만든 폴더에서 호스트 시작(`ROOTBORN_REPO_ROOT`) |
| 1 | **마지막 데이터 변환 고정** | 마지막으로 변환기를 돌려 `data/`를 최신으로 맞춘다. 알려진 풀지 못한 참조 12건은 그대로 기록. 태그 `unity-playable-20261111` 이후 Assets/Data가 바뀌지 않았는지 확인(`git diff tag -- Assets/Data`). | `data/` 변화 없음 또는 설명 가능 |
| 2 | **Core 옮기기** | 3절. `Packages/com.rootborn.core/Runtime/**/*.cs` → `game/Rootborn.Core/src/`(`git mv`), `.meta`·asmdef·`package.json` 삭제, csproj·검사·문서 경로 수정, `check-meta-files` 삭제 | `dotnet test`(Core 856건, 전체 826건 유지), `run-all.sh --checks-only` |
| 3 | **Unity 전용 지우기** | `delete-unity-only.txt`(74,538개) 지움: `Assets`의 코드·씬·설정·생성 에셋, 그림 팩 `.meta`, `ProjectSettings`, `Packages/manifest.json`·`packages-lock.json`. `Scripts/fevm·qa·unity·validation` 등 21개, 찌꺼기(`.tmp-*`, `Builds/Logs`). 단 다 분류(변환기 입력)는 아직 남긴다 | 같음. `git status`에서 Assets에 가 분류와 다만 남았는지 `list.sh`로 확인 |
| 4 | **데이터가 원본이 된다** | 변환기·테스트·스킬(양쪽) 삭제, `Assets/Data`(3,829 `.asset` + `.meta`), 문구 카탈로그, 스크립트·PNG `.meta`(다 분류 10,223개) 삭제, `data/export-report.json` 삭제. 규칙 문서에서 "원본" 문장 교체(2절) | `dotnet test`(DataExport.Tests 5건 제거로 건수가 줄어드는 것은 예상), 단계 0의 데이터 검사 |
| 5 | **원본 팩의 안 읽는 파일** | `art-pack-unread.txt`(32,205개, 101MB) 삭제. **사용자 결정 후** (8절). 가 분류는 남긴다 | `publish.sh` 결과가 단계 이전과 같은 파일 수(3,417개 = 가 3,414 + 테마 3) |
| 6 | **규칙·문서·설정 정리** | AGENTS.md 1번 삭제, 설계 문서 8절 표대로. `.gitignore`의 `*.csproj`·`*.sln` 줄 삭제, Unity 항목 정리, `.vsconfig` 삭제. `.claude/agents`의 Unity 에이전트와 Unity 스킬 정리. `unity-cli.md`·`assets-addressables.md`가 아직 있으면 삭제 | `run-all.sh --checks-only`, 문서 속 경로 검사 |
| 7 | **끝난 기록** | `production/qa`(Unity 시절 증거), 거대 인벤토리(`docs/art/modern-asset-inventory.json` 6MB) 등. 사용자 판단 | — |
| (선택) | **기록 줄이기** | 필요하면 `git filter-repo`로 옛 그림 팩을 기록에서 제거. 모든 세션·태그·원격에 영향이 크므로 이 계획 밖 | — |

Unity가 없는 상태에서 "빌드·테스트·실플레이"(설계 문서 7번 끝 기준)는 단계 3 이후 `publish.sh` 폴더를 저장소 밖으로 복사해 실행하는 것으로 확인한다(README의 방법). 이것은 자동 테스트이고, 실제 입력 확인은 `host-play-test`로 따로 한다.

## 8. 사용자가 정할 것

1. **그림을 지금 옮길지.** 작성자 판단: 옮기지 않는다(4절). 새 그림은 `game/content/`에서 시작.
2. **변환기를 남길지.** 작성자 판단: 지운다(단계 4). 남긴다면 `Assets/Data`와 `.meta`도 남아야 하고 Unity 파일이 저장소에 계속 있게 된다.
3. **원본 팩 파일을 저장소에 둘지.** 안 읽는 32,205개(101MB)는 지우는 걸 추천(작성자 판단). 읽는 1,606개 PNG는 새 그림이 올 때까지 둔다. 저장소가 공개인가, 팩 라이선스가 재배포를 허용하는가에 따라 바뀐다.
4. **기록에서 옛 그림 팩을 뺄지**(`filter-repo`). 작성자 판단: 지금은 안 한다.
5. **`gs-*` 스킬 16개와 `.claude/docs/templates`**(coin-defense 시절 하네스)를 같이 지울지.
6. **`production/qa` 증거 50개와 `docs/art`의 큰 인벤토리**를 지울지.
7. **`data/`의 `asset` 필드**를 지우고 새 엔티티는 안 쓰게 할지(작성자 판단: 당분간 둔다).

## 9. 필요한 변경 (이 세션 범위 밖이라 하지 않음)

- `Tools/publish/content.txt`: `game/themes/**` 추가(6절 버그).
- `game/Rootborn.Host/HostOptions.cs`: 루트 표지(4절).
- `game/tests/Rootborn.World.Tests/MissingPathsTests.cs`와 새 사본 JSON.
- 새 데이터 검사 테스트.

## 10. 확인하지 못한 것

- 태그를 체크아웃해서 Unity 판이 실제로 열리고 도는지(태그가 원격에 있는 것은 확인함).
- `Rootborn.Core.Tests`가 `internal` 멤버를 쓰는지(Core를 `ProjectReference`로 바꿀 수 있는지).
- `data/`의 `asset` 필드가 새 엔티티에서 선택인지(`Rootborn.Data` 읽기가 없어도 되는지).
- `pre-commit`·`pre-push` 훅이 Unity를 부르는지.
- 팩 라이선스와 저장소 공개 여부.
- `docs/art`·`production`의 문서 중 아직 읽는 것.
- 변환기의 풀지 못한 참조 12건(Unity 에셋이 없는 그림을 가리키는 책상 가구 등)이 `data/`에 어떤 모습으로 남았는지는 보지 않았다.
- list.sh의 분류는 경로 규칙이다. 예: `Assets/Localization` 4개(`ko.json` 등 Unity 쪽 문구)와 `Assets/Resources` 38개를 Unity 전용으로 보았지만 내용은 열어 보지 않았다. 게임이 읽는 것은 `data/localization`이다(런타임 목록으로 확인).
- 실행해 본 것: `list.sh`만(약 10초). 테스트와 Unity는 돌리지 않았다.
