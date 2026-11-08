# Unity CLI (batchmode) 참조 규칙

## ⚠️ 절대 원칙 — 열려 있는 Unity 편집기 밑에서 `.cs`를 바꾸지 않는다

2026-11-08 사용자 결정으로 고쳤다. 피드백: "새 .cs를 왜 Unity를 거쳐 만들어? 이러면 더 유니티에 결합되는거 아니야?"

- 새 `.cs`와 그 `.meta`는 일반 파일처럼 직접 작성하고 함께 커밋한다. `.meta`는 `fileFormatVersion: 2`와 새 32자리 GUID 두 줄이면 된다(코어 패키지와 같은 방식). 특정 편집기나 MCP 도구를 거칠 필요가 없다.
- 금지: 그 프로젝트의 Unity 편집기가 열려 있는 동안 그 폴더의 `.cs`·`.asmdef`를 바깥에서 쓰는 것. 2026-04-29 사고(CS0103)의 원인은 직접 쓰기가 아니라, 열린 편집기의 컴파일 캐시(Library/Bee)가 바깥에서 바뀐 파일을 놓친 것이었다.
- FEVM 사본은 커밋을 체크아웃해 처음부터 임포트하므로 이 문제가 생기지 않는다. MCP로 열어 둔 FEVM 편집기 사본을 새 커밋으로 바꿀 때는 편집기를 닫고 `git fetch` + `checkout`한 뒤 다시 띄운다. 열린 채 체크아웃하는 것도 바깥에서 고치는 것과 같다.
- 증상이 나오면(같은 폴더의 다른 파일은 되는데 새 파일만 CS0103): 편집기를 닫고 다시 열거나 `CompilationPipeline.GetAssemblies()`의 `sourceFiles`에 그 파일이 있는지 확인한다.

---


## 기본 실행 위치 — Strix Halo(FEVM) (2026-11-08 사용자 결정)

"이제 unity사용은 strix halo로 진행". ROOTBORN의 Unity 컴파일 확인, EditMode, PlayMode, Addressables 빌드는 Windows PC가 아니라 FEVM에서 커밋 해시 기준으로 돌린다. Windows Unity는 사용자가 따로 요청할 때만 켠다.

- 절차: 수정 → 로컬 커밋 → `Scripts/fevm/sync.sh [커밋]` → FEVM `~/farmer-unity-verify/tools/rb-test.sh <사본> EditMode|PlayMode [필터] [asset|built|rebuild]` → 통과 후 push. 상세: `Scripts/fevm/README.md`, 판정 규칙: `rules/strix-verification.md`.
- EditMode·PlayMode 모두 Xvfb + VirtualGL GPU로 돌린다. `-nographics`로 처음 임포트하면 8192px보다 큰 텍스처가 실패하고 Library에 그대로 남는다.
- git 밖 `output/` 증거를 읽는 테스트는 `Scripts/fevm/test-fixtures.txt`에 적힌 폴더를 sync.sh가 사본에 복사한다.
- Linux 결과를 Windows 화면·빌드 확인으로 보고하지 않는다. 실행 파일 실플레이는 FEVM의 Windows VM(`rootborn-win11`)에서 한다.
- 아래 Windows 경로·명령은 Windows에서 돌려야 할 때의 참조로 남긴다.

---

Unity를 헤드리스 CLI로 구동하는 표준 절차. `unity-builder`, `unity-test-runner`, `deploy-manager` 에이전트가 참조한다.

## 실행 파일 경로

Windows: `C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe`

환경변수 `UNITY_EDITOR_PATH`가 설정되어 있으면 우선 사용.

## 공통 플래그

| 플래그 | 용도 |
|--------|------|
| `-batchmode` | GUI 없이 실행 |
| `-nographics` | 그래픽 디바이스 초기화 생략 (빌드/테스트 환경) |
| `-quit` | 작업 후 자동 종료 (테스트 러너는 생략) |
| `-projectPath "<path>"` | 프로젝트 경로 |
| `-logFile "<path>"` | 로그 출력 경로 (`-` 은 stdout) |
| `-executeMethod <Class.Method>` | 정적 메서드 실행 |
| `-buildTarget <target>` | StandaloneWindows64, Android, iOS, WebGL, StandaloneOSX |

## 빌드 명령 예시

```bash
"$UNITY_EDITOR_PATH" \
  -batchmode -nographics -quit \
  -projectPath "d:/Develop/Unity/My project" \
  -logFile "Logs/build.log" \
  -executeMethod Game.Editor.BuildScript.BuildWindows64 \
  -buildTarget StandaloneWindows64
```

빌드 스크립트는 `Assets/Editor/BuildScript.cs`에 정적 메서드로 구현:

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Game.Editor
{
    public static class BuildScript
    {
        public static void BuildWindows64() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Game.exe");
        public static void BuildAndroid()   => Build(BuildTarget.Android,            "Builds/Android/Game.apk");
        public static void BuildWebGL()     => Build(BuildTarget.WebGL,              "Builds/WebGL");

        private static void Build(BuildTarget target, string outputPath)
        {
            var scenes = System.Array.ConvertAll(
                EditorBuildSettings.scenes,
                s => s.path);

            var report = BuildPipeline.BuildPlayer(
                scenes, outputPath, target, BuildOptions.None);

            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
#endif
```

## 테스트 명령 예시

EditMode:
```bash
"$UNITY_EDITOR_PATH" \
  -batchmode -nographics \
  -projectPath "d:/Develop/Unity/My project" \
  -runTests -testPlatform EditMode \
  -testResults "Logs/test-editmode.xml" \
  -logFile "Logs/test-editmode.log"
```

PlayMode:
```bash
"$UNITY_EDITOR_PATH" \
  -batchmode \
  -projectPath "d:/Develop/Unity/My project" \
  -runTests -testPlatform PlayMode \
  -testResults "Logs/test-playmode.xml" \
  -logFile "Logs/test-playmode.log"
```

> PlayMode는 `-nographics` 금지 (렌더/코루틴 필요).

## 종료 코드

- `0` — 성공
- `2` — 컴파일 에러 / 빌드 실패
- `3` — 테스트 실패
- 기타 — Unity 내부 에러. `Logs/*.log` 확인

## 락 충돌 회피

Unity Editor가 실행 중이면 CLI 호출이 락 에러로 실패한다. 에이전트는:
1. 먼저 Editor 프로세스가 떠 있는지 확인 (`tasklist | grep Unity` 또는 Windows: `Get-Process Unity`)
2. 있으면 사용자에게 Editor 종료 요청 후 대기
3. 없으면 CLI 호출

## Log 파싱 규칙

- 에러: `^(.*):(\\d+): error ` 또는 `^Error: `
- 경고: `^(.*):(\\d+): warning `
- 테스트 실패: JUnit XML (`Logs/test-*.xml`)의 `<failure>` 태그
