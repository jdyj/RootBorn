using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Player;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    /// <summary>
    /// GatherInteractor 의 InputAction 바인딩 검증.
    /// 사용자 요청: "Control 또는 마우스 좌클릭" — 좌클릭은 PlayerController 가 휘두르기 후
    /// TriggerInteract() 로 같은 코드 경로 진입, Control 은 GatherInteractor._interactAction 직접 바인딩.
    ///
    /// EditMode 에서 MonoBehaviour OnEnable 자동 호출이 보장되지 않으므로 reflection 으로 직접 Invoke.
    /// InputSystem 어셈블리에 정적 의존하지 않기 위해 InputAction.bindings 도 reflection 으로 순회.
    /// </summary>
    public sealed class GatherInteractorInputBindingTests
    {
        private const System.Reflection.BindingFlags Flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        private static System.Collections.Generic.List<string> ReadBindingPaths(GatherInteractor interactor)
        {
            // EditMode 에서 AddComponent 직후 OnEnable 호출 타이밍이 일정치 않음 (Unity 라이프사이클은 PlayMode 우선).
            // OnEnable 을 reflection 으로 직접 호출하여 _interactAction 생성을 강제. 멱등이라 중복 호출 안전.
            var onEnable = typeof(GatherInteractor).GetMethod("OnEnable", Flags);
            Assert.IsNotNull(onEnable, "OnEnable 메서드 부재 — GatherInteractor 리팩터로 이름 변경됐을 수 있음.");
            onEnable.Invoke(interactor, null);

            var actionField = typeof(GatherInteractor).GetField("_interactAction", Flags);
            Assert.IsNotNull(actionField, "_interactAction 필드 부재.");
            var action = actionField.GetValue(interactor);
            Assert.IsNotNull(action, "_interactAction 이 OnEnable 에서 생성돼야 함.");

            // InputAction.bindings → ReadOnlyArray<InputBinding>. IEnumerable 로 순회.
            var bindingsProp = action.GetType().GetProperty("bindings");
            Assert.IsNotNull(bindingsProp, "InputAction.bindings 프로퍼티 부재");
            var bindings = bindingsProp.GetValue(action) as System.Collections.IEnumerable;
            Assert.IsNotNull(bindings, "bindings 가 IEnumerable 이어야 함");

            var paths = new System.Collections.Generic.List<string>();
            foreach (var b in bindings)
            {
                var pathProp = b.GetType().GetProperty("path");
                if (pathProp == null) continue;
                paths.Add(pathProp.GetValue(b) as string);
            }
            return paths;
        }

        private static void DisposeInteractor(GatherInteractor interactor)
        {
            // OnDisable 도 명시 호출하여 InputAction.Dispose 누수 방지.
            var onDisable = typeof(GatherInteractor).GetMethod("OnDisable", Flags);
            if (onDisable != null)
            {
                try { onDisable.Invoke(interactor, null); }
                catch { /* ignore */ }
            }
        }

        [Test]
        public void CTRL_BIND_001_InteractAction_BindsControlKey()
        {
            var go = new GameObject("p");
            GatherInteractor interactor = null;
            try
            {
                interactor = go.AddComponent<GatherInteractor>();
                var paths = ReadBindingPaths(interactor);

                CollectionAssert.Contains(paths, "<Keyboard>/leftCtrl",
                    "Control 키(좌)가 채집 액션에 바인딩돼야 함 — 사용자 요청 액션 키.");
            }
            finally
            {
                if (interactor != null) DisposeInteractor(interactor);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CTRL_BIND_002_InteractAction_PreservesExistingBindings()
        {
            // 회귀 가드: Control 추가 작업이 기존 E/Space 바인딩을 깨뜨리지 않는다.
            var go = new GameObject("p");
            GatherInteractor interactor = null;
            try
            {
                interactor = go.AddComponent<GatherInteractor>();
                var paths = ReadBindingPaths(interactor);

                CollectionAssert.Contains(paths, "<Keyboard>/e",
                    "기존 E 키 바인딩 회귀 가드.");
                CollectionAssert.Contains(paths, "<Keyboard>/space",
                    "기존 Space 키 바인딩 회귀 가드.");
            }
            finally
            {
                if (interactor != null) DisposeInteractor(interactor);
                Object.DestroyImmediate(go);
            }
        }
    }
}
