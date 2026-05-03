---
name: unity-test-framework
description: "Unity Test Framework(EditMode/PlayMode) 작성 가이드. Assembly Definition 설정, NUnit assert, UnityTest 코루틴 패턴, TestCase 파라미터화, Setup/TearDown, 모킹 전략. test-designer와 unity-test-runner가 참조."
---

# Unity Test Framework Guide

Unity Test Framework로 EditMode/PlayMode 테스트를 작성하는 가이드.

## 디렉토리 구조

```
Assets/
├── Scripts/
│   ├── Game.asmdef                 (런타임 코드)
│   └── Editor/
│       └── Game.Editor.asmdef      (에디터 전용)
└── Tests/
    ├── EditMode/
    │   ├── Game.Tests.EditMode.asmdef
    │   └── Combat/
    │       └── DamageCalculatorTests.cs
    └── PlayMode/
        ├── Game.Tests.PlayMode.asmdef
        └── Player/
            └── PlayerMovementTests.cs
```

## Assembly Definition (EditMode 예시)

```json
{
  "name": "Game.Tests.EditMode",
  "rootNamespace": "Game.Tests.EditMode",
  "references": [
    "Game",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"]
}
```

## EditMode 테스트 패턴

```csharp
using NUnit.Framework;
using Game.Combat;

namespace Game.Tests.EditMode.Combat
{
    public class DamageCalculatorTests
    {
        [Test]
        public void Calculate_GivenBase100Def20_Returns80()
        {
            int result = DamageCalculator.Calculate(baseDamage: 100, defense: 20);
            Assert.That(result, Is.EqualTo(80));
        }

        [TestCase(100, 0, 100)]
        [TestCase(100, 50, 50)]
        [TestCase(100, 150, 0)]   // defense clamp
        public void Calculate_TableDriven(int baseDmg, int def, int expected)
        {
            Assert.That(DamageCalculator.Calculate(baseDmg, def), Is.EqualTo(expected));
        }
    }
}
```

## PlayMode 테스트 패턴 (UnityTest 코루틴)

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Player;

namespace Game.Tests.PlayMode.Player
{
    public class PlayerMovementTests
    {
        [UnityTest]
        public IEnumerator Player_MovesRight_OverTime()
        {
            var go = new GameObject("Player");
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            var controller = go.AddComponent<PlayerController>();

            controller.SetInput(Vector2.right);
            yield return new WaitForSeconds(0.5f);

            Assert.That(go.transform.position.x, Is.GreaterThan(0f));
            Object.Destroy(go);
        }
    }
}
```

## 핵심 원칙

- **순수 로직은 EditMode**: `static`/POCO로 분리 가능한 것은 MonoBehaviour 밖으로
- **PlayMode는 물리/코루틴만**: 비용이 크므로 최소화
- **AAA 패턴**: Arrange → Act → Assert
- **1 테스트 1 assert**: 이름만 봐도 무엇을 검증하는지 명확
- **이름 규약**: `{Method}_{Given}_{Then}` 또는 `{Method}_When{X}_Returns{Y}`
- **외부 의존 없음**: 파일/네트워크는 모킹 또는 인메모리 대체

## Setup / TearDown

```csharp
public class FooTests
{
    private GameObject _root;

    [SetUp]
    public void SetUp() => _root = new GameObject("TestRoot");

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);
}
```

## 내부 접근이 필요할 때

`AssemblyInfo.cs` (Game asmdef) 또는 asmdef의 `defineConstraints`/`references` 조정:
```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Game.Tests.EditMode")]
[assembly: InternalsVisibleTo("Game.Tests.PlayMode")]
```

## 트리거

- "Unity 테스트 작성", "EditMode/PlayMode 테스트", "UnityTest 코루틴", "Assembly Definition 테스트"
- test-designer / unity-test-runner / bug-fixer 가 테스트 생성 시 이 스킬을 Read
