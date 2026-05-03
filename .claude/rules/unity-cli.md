# Unity CLI (batchmode) 참조 규칙

## ⚠️ 절대 원칙 — `Assets/**/*.cs` 직접 디스크 쓰기 금지

신규 C# 스크립트(`Assets/Scripts/**/*.cs`, Editor 스크립트 포함) 생성은 **반드시 Unity API 경유**:
- `script-update-or-create` MCP tool, 또는
- Unity Editor 내 메뉴(Project 우클릭 → Create → C# Script)

`Write`/`bash echo >` 등 외부 디스크 쓰기는 **MonoScript 자산 등록은 되지만 `CompilationPipeline.sourceFiles`에 누락**되는 케이스 발생 (Library/Bee 캐시 미갱신). 결과: 컴파일러가 해당 클래스 인식 실패 → CS0103.

증상: 같은 폴더의 다른 .cs 는 정상이지만 특정 신규 .cs 만 "이름이 컨텍스트에 없음" 오류.

검증: `CompilationPipeline.GetAssemblies()` 의 `sourceFiles` 에 파일 경로 존재 확인. 없으면 `assets-delete` 후 `script-update-or-create` 로 재생성.

`.json`, `.md`, `.asmdef` 등 비-스크립트 자산은 Write 직접 쓰기 허용.

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
