# 프롤로그 & 챕터 1 — 계획 1: 기반 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 씬 자산을 정규화하고, Input System 기반의 플레이어·생존자원·인벤토리·상호작용·전투 골격을 세워 "움직이고, 숨이 차고, 아이템을 줍고, 도구로 때릴 수 있는" 상태를 만든다.

**Architecture:** 코드를 두 계층으로 나눈다. **도메인 계층**(`Assets/02.Scripts/Domain/`, `Survive.Domain.asmdef`)은 순수 로직과 데이터 SO만 담으며 MonoBehaviour·Feel·DOTween을 쓰지 않는다 — EditMode 테스트 대상이다. **거동 계층**(그 외 `Assets/02.Scripts/`, `Assembly-CSharp`)은 MonoBehaviour 전부를 담고 Feel·DOTween·Cinemachine을 자유롭게 쓴다. `Assembly-CSharp`가 asmdef를 자동 참조하므로 의존은 거동 → 도메인 한 방향으로 흐른다.

**Tech Stack:** Unity 6000.5.6f1, URP 17.5, Input System(신규 도입), Cinemachine 2.10.7, Feel(MMFeedbacks), DOTween, Unity Test Framework 1.7.0, uloop CLI

**설계 문서:** `docs/superpowers/specs/2026-08-02-prologue-chapter1-design.md`

---

## Global Constraints

- Unity 버전은 `6000.5.6f1`이다. 프로젝트 버전을 올리지 않는다.
- **asmdef는 `Assets/02.Scripts/Domain/Survive.Domain.asmdef` 하나만 만든다.** 그 아래를 더 쪼개지 않는다.
- **`Domain/` 안에는 MonoBehaviour를 두지 않는다.** Feel·DOTween·Cinemachine·InputSystem 어느 것도 `using`하지 않는다. 순수 C# 클래스, 데이터 ScriptableObject, 인터페이스만 넣는다. 이 규칙을 어기면 EditMode 테스트가 깨진다.
- **`Domain/` 밖(= `Assembly-CSharp`)에서는 Feel `MMF_Player`·DOTween·Cinemachine을 적극적으로 쓴다.** 연출 방침은 설계 문서 4.15절을 따른다.
- **제3자 자산에 asmdef를 만들거나 고치지 않는다** — `Assets/Feel/`, `Assets/Plugins/Demigiant/`. Feel의 `MMFeedbacks`에 asmdef가 없는 것은 의도된 상태로 두고, 그래서 도메인 계층을 분리하는 것이다.
- **기존 파일 중 다음 3개는 삭제·개명하지 않는다** (씬과 프리팹에서 참조 중): `Assets/02.Scripts/CameraShake.cs`, `Assets/02.Scripts/UI/ValueBarScript.cs`, `Assets/02.Scripts/Utill/EasingFunctions.cs`. `ValueBarScript`의 공개 메서드 시그니처(`ResetValue`, `RefreshMaxValue`, `SetUpdateValue`)도 유지한다.
- 아이템 식별자는 `int`가 아니라 `string`이다. 값은 소문자 스네이크 케이스(`scrap`, `oxygen_filter`).
- 자막·아이템명 등 사용자에게 보이는 문자열은 **한국어**로 쓴다.
- 한글이 들어가는 `UnityEngine.UI.Text`에는 반드시 `ChosunGu` 폰트를 쓴다. `LegacyRuntime` 폰트는 한글을 렌더링하지 못한다.
- 컴파일 확인은 `uloop compile`, 테스트는 `uloop run-tests`로 한다. 모든 uloop 명령은 `E:/SurviveProject/SurviveProject`에서 실행한다.
- 커밋은 저장소 루트 `E:/SurviveProject`에서 한다. Unity 프로젝트는 그 하위 `SurviveProject/`다.

---

## File Structure

### 신규 생성

**도메인 계층** — `Assets/02.Scripts/Domain/` (MonoBehaviour·Feel·DOTween 금지)

| 경로 | 책임 |
|---|---|
| `Domain/Survive.Domain.asmdef` | 도메인 어셈블리 정의 |
| `Domain/Core/EventChannelSO.cs` | 제네릭 이벤트 채널 기반 클래스 |
| `Domain/Core/EventChannels.cs` | Void/Int/Float/String 구체 채널 |
| `Domain/Core/GameServices.cs` | 정적 서비스 레지스트리 |
| `Domain/Core/ISaveable.cs` | 저장 대상 인터페이스 |
| `Domain/Core/SceneReferenceSO.cs` | 씬 이름을 담는 에셋 |
| `Domain/Vitals/Vital.cs` | **순수 클래스** — 값·클램프·변경 이벤트 |
| `Domain/Vitals/VitalDefinitionSO.cs` | 생존자원 정의 데이터 |
| `Domain/Vitals/IOxygenModifier.cs` | 환경의 산소 보정 계약 |
| `Domain/Vitals/OxygenRate.cs` | **순수 정적** — 산소 보정 겹침 계산 |
| `Domain/Items/ItemCategory.cs` | 아이템 분류 enum |
| `Domain/Items/ToolType.cs` | 도구 종류 enum |
| `Domain/Items/ItemDataSO.cs` | 아이템 정의 |
| `Domain/Items/ToolItemSO.cs` | 도구 아이템 |
| `Domain/Items/ConsumableItemSO.cs` | 소모품 아이템 |
| `Domain/Items/ItemDatabaseSO.cs` | id → 아이템 조회 |
| `Domain/Items/ItemStack.cs` | 아이템 + 수량 |
| `Domain/Items/Inventory.cs` | **순수 클래스** — 슬롯·스택 로직 |
| `Domain/Combat/DamageInfo.cs` | 피해 정보 구조체 |
| `Domain/Combat/IDamageable.cs` | 피해 수신 계약 |

**거동 계층** — `Assets/02.Scripts/` (Feel·DOTween·Cinemachine 사용)

| 경로 | 책임 |
|---|---|
| `Core/GameBootstrap.cs` | 씬 진입점, 서비스 등록 |
| `Input/InputReaderSO.cs` | 입력 → 이벤트 변환 |
| `Input/PlayerInputActions.inputactions` | 액션 맵 정의 |
| `Vitals/PlayerVitals.cs` | 체력·산소 보유 및 갱신 |
| `Inventory/PlayerInventory.cs` | 인벤토리 보유 + 저장 |
| `Player/PlayerContext.cs` | 플레이어 하위 시스템 묶음 |
| `Player/PlayerLocomotion.cs` | 이동·점프·중력 |
| `Player/PlayerCameraRig.cs` | 시점 회전 |
| `Player/PlayerAnimatorDriver.cs` | Animator 파라미터 |
| `Player/PlayerToolHolder.cs` | 손 소켓 도구 장착 |
| `Interaction/IInteractable.cs` | 상호작용 계약 (`PlayerContext`를 받으므로 거동 계층) |
| `Interaction/PlayerInteractor.cs` | 대상 탐지·실행 |
| `Interaction/ItemPickup.cs` | 바닥 아이템 |
| `Interaction/LootContainer.cs` | 상자 |
| `Combat/MeleeSwing.cs` | 근접 공격 + Feel 타격 피드백 |
| `Combat/PlayerDamageReceiver.cs` | 플레이어 피격 + Feel 피격 피드백 |
| `Assets/09.Tests/EditMode/Survive.Tests.EditMode.asmdef` | 테스트 어셈블리 |
| `Assets/09.Tests/EditMode/VitalTests.cs` | `Vital` 검증 |
| `Assets/09.Tests/EditMode/InventoryTests.cs` | `Inventory` 검증 |
| `Assets/09.Tests/EditMode/ItemDatabaseTests.cs` | `ItemDatabaseSO` 검증 |
| `Assets/05.Prefabs/Player.prefab` | 정규화된 플레이어 |

### 수정

| 경로 | 변경 |
|---|---|
| `Assets/01.Scenes/StartScene.unity` | Player·UI를 프리팹 인스턴스로 교체, URP 컴포넌트 정리 |
| `Assets/01.Scenes/MainScene.unity` | 동일 |
| `Assets/05.Prefabs/CvsUI.prefab` | HP 바를 `GaugeBarPrefab` 규격으로 통일 |
| `ProjectSettings/EditorBuildSettings.asset` | 씬 2개 등록 |
| `ProjectSettings/ProjectSettings.asset` | `activeInputHandler` 변경 |
| `Packages/manifest.json` | `com.unity.inputsystem` 추가 |

### 삭제

| 경로 | 사유 |
|---|---|
| `Assets/02.Scripts/PlayerController.cs` | Task 16에서 4개로 분해 완료 후 |
| `Assets/02.Scripts/Inventory/ItemInfo.cs` | `ItemDataSO`로 대체 |
| `Assets/02.Scripts/Inventory/ItemListSO.cs` | `ItemDatabaseSO`로 대체 |
| `Assets/02.Scripts/Inventory/InventoryItem.cs` | `ItemStack`으로 대체 |

---

## Task 1: 어셈블리 정의와 테스트 하네스

가장 먼저 하는 이유: 이후 모든 태스크가 테스트를 돌려야 하는데, 테스트 어셈블리가 게임플레이 코드를 참조할 수 있는지부터 확인해야 한다.

**Files:**
- Create: `Assets/02.Scripts/Domain/Survive.Domain.asmdef`
- Create: `Assets/09.Tests/EditMode/Survive.Tests.EditMode.asmdef`
- Create: `Assets/09.Tests/EditMode/HarnessTests.cs`
- Create: `Assets/02.Scripts/Domain/Core/BuildMarker.cs`
- Create: `Assets/02.Scripts/BehaviourLayerProbe.cs` (임시 — Step 6에서 삭제)

**Interfaces:**
- Consumes: 없음
- Produces: `Survive.Domain` 어셈블리 이름, `Survive.Tests.EditMode` 어셈블리 이름. 이후 순수 로직·데이터 SO는 전부 `Domain/` 안에 들어간다.

- [ ] **Step 1: `Survive.Domain.asmdef` 생성**

`Assets/02.Scripts/Domain/Survive.Domain.asmdef`:

```json
{
    "name": "Survive.Domain",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "noEngineReferences": false
}
```

`references`가 빈 배열인 것이 핵심이다. 도메인은 UnityEngine 외에 아무것도 필요로 하지 않는다. `autoReferenced: true`라야 `Assembly-CSharp`가 이 어셈블리를 자동으로 참조한다.

- [ ] **Step 2: 테스트 어셈블리 생성**

`Assets/09.Tests/EditMode/Survive.Tests.EditMode.asmdef`:

```json
{
    "name": "Survive.Tests.EditMode",
    "rootNamespace": "",
    "references": ["Survive.Domain", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "noEngineReferences": false
}
```

- [ ] **Step 3: 참조 확인용 표식 클래스 생성**

`Assets/02.Scripts/Domain/Core/BuildMarker.cs`:

```csharp
namespace Survive.Domain
{
    /// <summary>
    /// 테스트 어셈블리가 도메인 어셈블리를 참조할 수 있는지 확인하는 표식.
    /// 게임 로직은 들어가지 않는다.
    /// </summary>
    public static class BuildMarker
    {
        public const string AssemblyName = "Survive.Domain";
    }
}
```

- [ ] **Step 4: 실패하는 테스트 작성**

`Assets/09.Tests/EditMode/HarnessTests.cs`:

```csharp
using NUnit.Framework;
using Survive.Domain;

public class HarnessTests
{
    [Test]
    public void 테스트_어셈블리가_도메인_어셈블리를_참조한다()
    {
        Assert.AreEqual("Survive.Domain", BuildMarker.AssemblyName);
    }
}
```

- [ ] **Step 4b: 거동 계층에서 Feel·DOTween·도메인이 모두 보이는지 확인하는 임시 파일**

이 계획의 구조 전체가 성립하는지를 지금 한 번에 검증한다. 나중에 발견하면 손해가 크다.

`Assets/02.Scripts/BehaviourLayerProbe.cs`:

```csharp
using UnityEngine;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Core;

/// <summary>
/// 거동 계층에서 Feel·DOTween·도메인 어셈블리가 모두 보이는지 확인하는 임시 파일.
/// 컴파일만 통과하면 목적을 다한 것이므로 곧바로 지운다.
/// </summary>
public class BehaviourLayerProbe : MonoBehaviour
{
    [SerializeField] MMF_Player feedbacks;
    [SerializeField] CanvasGroup group;

    void Start()
    {
        Debug.Log(BuildMarker.AssemblyName);   // 도메인 참조
        group.DOFade(1f, 0.3f);                // DOTween + Modules(UI 확장)
        feedbacks?.PlayFeedbacks();            // Feel
    }
}
```

`DOFade`는 `DOTween/Modules/DOTweenModuleUI.cs`가 주는 확장 메서드다. 이것이 컴파일되면 Modules까지 쓸 수 있다는 뜻이다.

- [ ] **Step 5: .meta 생성을 위해 에셋 새로고침 후 컴파일**

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'UnityEditor.AssetDatabase.Refresh(); return "refreshed";'
uloop compile --wait-for-domain-reload true
```

기대 결과: 컴파일 에러 0건.

에러가 난다면 원인은 셋 중 하나다:
- `MoreMountains.Feedbacks` 네임스페이스를 찾을 수 없다 → Feel이 `Assembly-CSharp`가 아닌 곳에 있다. `find Assets/Feel -name "*.asmdef"`로 확인한다.
- `DG.Tweening`을 찾을 수 없다 → `Assets/Plugins/Demigiant/DOTween/DOTween.dll`이 있는지 확인한다.
- `Survive.Domain`을 찾을 수 없다 → `Survive.Domain.asmdef`의 `autoReferenced`가 `true`인지 확인한다.

- [ ] **Step 6: 테스트 실행**

```bash
cd E:/SurviveProject/SurviveProject
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 1개 통과. **여기서 실패하면 이후 모든 태스크의 검증이 불가능하므로 반드시 해결하고 넘어간다.**

- [ ] **Step 7: 임시 확인 파일 삭제**

`BehaviourLayerProbe`는 구조 검증이 목적이었고 이제 끝났다.

```bash
cd E:/SurviveProject/SurviveProject
rm Assets/02.Scripts/BehaviourLayerProbe.cs Assets/02.Scripts/BehaviourLayerProbe.cs.meta
uloop compile --wait-for-domain-reload true
uloop get-logs --log-type Error --max-count 10
```

기대 결과: 에러 0건.

- [ ] **Step 8: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts SurviveProject/Assets/09.Tests
git commit -m "테스트 하네스 구축: 도메인 어셈블리와 EditMode 테스트 어셈블리 추가

테스트 어셈블리는 Assembly-CSharp를 참조할 수 없고, asmdef 또한
사전 정의 어셈블리를 참조할 수 없다. Feel의 MMFeedbacks는 asmdef가
없어 Assembly-CSharp에 있으므로, 순수 로직만 도메인 어셈블리로 떼어
테스트 가능성과 Feel 사용 가능성을 동시에 확보한다."
```

---

## Task 2: 이벤트 채널과 서비스 레지스트리

**Files:**
- Create: `Assets/02.Scripts/Domain/Core/EventChannelSO.cs`
- Create: `Assets/02.Scripts/Domain/Core/EventChannels.cs`
- Create: `Assets/02.Scripts/Domain/Core/GameServices.cs`
- Test: `Assets/09.Tests/EditMode/GameServicesTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `Survive.Domain.EventChannelSO<T>` — `void Raise(T)`, `event Action<T> OnRaised`
  - `Survive.Domain.VoidEventChannelSO` — `void Raise()`, `event Action OnRaised`
  - `Survive.Domain.GameServices` — `static void Register<T>(T)`, `static T Get<T>()`, `static bool TryGet<T>(out T)`, `static void Unregister<T>()`, `static void Clear()`

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/09.Tests/EditMode/GameServicesTests.cs`:

```csharp
using NUnit.Framework;
using Survive.Core;

public class GameServicesTests
{
    class 더미서비스 { public int 값; }

    [SetUp]
    public void 초기화() => GameServices.Clear();

    [TearDown]
    public void 정리() => GameServices.Clear();

    [Test]
    public void 등록한_서비스를_되찾는다()
    {
        var s = new 더미서비스 { 값 = 7 };
        GameServices.Register(s);
        Assert.AreSame(s, GameServices.Get<더미서비스>());
    }

    [Test]
    public void 미등록_서비스_조회는_예외를_던진다()
    {
        Assert.Throws<System.InvalidOperationException>(() => GameServices.Get<더미서비스>());
    }

    [Test]
    public void TryGet은_미등록시_false를_돌려준다()
    {
        Assert.IsFalse(GameServices.TryGet<더미서비스>(out var found));
        Assert.IsNull(found);
    }

    [Test]
    public void 해제한_서비스는_조회되지_않는다()
    {
        GameServices.Register(new 더미서비스());
        GameServices.Unregister<더미서비스>();
        Assert.IsFalse(GameServices.TryGet<더미서비스>(out _));
    }

    [Test]
    public void 같은_타입_재등록은_덮어쓴다()
    {
        GameServices.Register(new 더미서비스 { 값 = 1 });
        GameServices.Register(new 더미서비스 { 값 = 2 });
        Assert.AreEqual(2, GameServices.Get<더미서비스>().값);
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
```

기대 결과: `GameServices` 형식을 찾을 수 없다는 컴파일 에러. `uloop get-logs --log-type Error`로 확인한다.

- [ ] **Step 3: `GameServices` 구현**

`Assets/02.Scripts/Domain/Core/GameServices.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Survive.Core
{
    /// <summary>
    /// 시스템 간 직접 참조를 없애기 위한 최소 레지스트리.
    /// DI 프레임워크를 도입하지 않는다 — 등록·조회·해제만 한다.
    /// </summary>
    public static class GameServices
    {
        static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            _services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var found)) return (T)found;
            throw new InvalidOperationException(
                $"서비스가 등록되지 않았습니다: {typeof(T).Name}. GameBootstrap이 씬에 있는지 확인하세요.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var found))
            {
                service = (T)found;
                return true;
            }
            service = null;
            return false;
        }

        public static void Unregister<T>() where T : class => _services.Remove(typeof(T));

        public static void Clear() => _services.Clear();
    }
}
```

- [ ] **Step 4: 이벤트 채널 구현**

`Assets/02.Scripts/Domain/Core/EventChannelSO.cs`:

```csharp
using System;
using UnityEngine;

namespace Survive.Core
{
    /// <summary>
    /// 시스템 간 유일한 통신 수단. 발신자와 수신자가 서로를 모른다.
    /// </summary>
    public abstract class EventChannelSO<T> : ScriptableObject
    {
        public event Action<T> OnRaised;

        public void Raise(T payload) => OnRaised?.Invoke(payload);

        /// <summary>
        /// ScriptableObject는 플레이 모드 종료 후에도 살아남아 구독이 남는다.
        /// 에디터에서 유령 구독이 쌓이는 것을 막는다.
        /// </summary>
        void OnDisable() => OnRaised = null;
    }
}
```

`Assets/02.Scripts/Domain/Core/EventChannels.cs`:

```csharp
using System;
using UnityEngine;

namespace Survive.Core
{
    [CreateAssetMenu(menuName = "Survive/Core/Void Event Channel")]
    public class VoidEventChannelSO : ScriptableObject
    {
        public event Action OnRaised;
        public void Raise() => OnRaised?.Invoke();
        void OnDisable() => OnRaised = null;
    }

    [CreateAssetMenu(menuName = "Survive/Core/Int Event Channel")]
    public class IntEventChannelSO : EventChannelSO<int> { }

    [CreateAssetMenu(menuName = "Survive/Core/Float Event Channel")]
    public class FloatEventChannelSO : EventChannelSO<float> { }

    [CreateAssetMenu(menuName = "Survive/Core/String Event Channel")]
    public class StringEventChannelSO : EventChannelSO<string> { }
}
```

- [ ] **Step 5: 테스트 통과 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 6개 통과 (하네스 1 + 서비스 5).

- [ ] **Step 6: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Core SurviveProject/Assets/09.Tests
git commit -m "Core: 이벤트 채널과 서비스 레지스트리 추가"
```

---

## Task 3: Vital 순수 클래스

**Files:**
- Create: `Assets/02.Scripts/Domain/Vitals/Vital.cs`
- Test: `Assets/09.Tests/EditMode/VitalTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `Survive.Vitals.Vital` — 생성자 `Vital(float max, float start)`, 프로퍼티 `float Current`, `float Max`, `float Normalized`, `bool IsEmpty`, `bool IsFull`, 메서드 `void Modify(float delta)`, `void SetMax(float value)`, 이벤트 `event Action<float,float> Changed`

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/09.Tests/EditMode/VitalTests.cs`:

```csharp
using NUnit.Framework;
using Survive.Vitals;

public class VitalTests
{
    [Test]
    public void 생성시_시작값을_가진다()
    {
        var v = new Vital(100f, 60f);
        Assert.AreEqual(60f, v.Current);
        Assert.AreEqual(100f, v.Max);
    }

    [Test]
    public void 최대치를_넘지_못한다()
    {
        var v = new Vital(100f, 90f);
        v.Modify(50f);
        Assert.AreEqual(100f, v.Current);
    }

    [Test]
    public void 0_아래로_내려가지_않는다()
    {
        var v = new Vital(100f, 10f);
        v.Modify(-50f);
        Assert.AreEqual(0f, v.Current);
    }

    [Test]
    public void 비었을때_IsEmpty가_참이다()
    {
        var v = new Vital(100f, 1f);
        Assert.IsFalse(v.IsEmpty);
        v.Modify(-1f);
        Assert.IsTrue(v.IsEmpty);
    }

    [Test]
    public void Normalized는_0에서_1_사이다()
    {
        var v = new Vital(200f, 50f);
        Assert.AreEqual(0.25f, v.Normalized, 0.0001f);
    }

    [Test]
    public void 값이_바뀌면_Changed가_발생한다()
    {
        var v = new Vital(100f, 50f);
        float 받은현재 = -1f, 받은최대 = -1f;
        int 횟수 = 0;
        v.Changed += (cur, max) => { 받은현재 = cur; 받은최대 = max; 횟수++; };

        v.Modify(-10f);

        Assert.AreEqual(1, 횟수);
        Assert.AreEqual(40f, 받은현재);
        Assert.AreEqual(100f, 받은최대);
    }

    [Test]
    public void 값이_그대로면_Changed가_발생하지_않는다()
    {
        var v = new Vital(100f, 100f);
        int 횟수 = 0;
        v.Changed += (_, __) => 횟수++;

        v.Modify(10f);   // 이미 최대치라 변화 없음

        Assert.AreEqual(0, 횟수);
    }

    [Test]
    public void 최대치를_줄이면_현재값도_함께_잘린다()
    {
        var v = new Vital(100f, 100f);
        v.SetMax(40f);
        Assert.AreEqual(40f, v.Max);
        Assert.AreEqual(40f, v.Current);
    }

    [Test]
    public void 최대치는_0_아래로_설정되지_않는다()
    {
        var v = new Vital(100f, 100f);
        v.SetMax(-5f);
        Assert.AreEqual(0f, v.Max);
        Assert.AreEqual(0f, v.Current);
    }
}
```

`Normalized`가 `Max`가 0일 때 0으로 나누는 문제를 일으키므로, 구현에서 방어한다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
```

기대 결과: `Vital` 형식을 찾을 수 없다는 컴파일 에러.

- [ ] **Step 3: `Vital` 구현**

`Assets/02.Scripts/Domain/Vitals/Vital.cs`:

```csharp
using System;
using UnityEngine;

namespace Survive.Vitals
{
    /// <summary>
    /// 체력·산소 같은 0~Max 사이의 값 하나.
    /// MonoBehaviour가 아니므로 Unity 실행 없이 테스트할 수 있다.
    /// </summary>
    public class Vital
    {
        float _current;
        float _max;

        public Vital(float max, float start)
        {
            _max = Mathf.Max(0f, max);
            _current = Mathf.Clamp(start, 0f, _max);
        }

        public float Current => _current;
        public float Max => _max;
        public float Normalized => _max <= 0f ? 0f : _current / _max;
        public bool IsEmpty => _current <= 0f;
        public bool IsFull => _current >= _max;

        /// <summary>값이 실제로 바뀐 경우에만 발생한다. (current, max)</summary>
        public event Action<float, float> Changed;

        public void Modify(float delta)
        {
            float 이전 = _current;
            _current = Mathf.Clamp(_current + delta, 0f, _max);
            if (!Mathf.Approximately(이전, _current)) Changed?.Invoke(_current, _max);
        }

        public void SetMax(float value)
        {
            _max = Mathf.Max(0f, value);
            float 이전 = _current;
            _current = Mathf.Clamp(_current, 0f, _max);
            if (!Mathf.Approximately(이전, _current)) Changed?.Invoke(_current, _max);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 15개 통과 (누적).

- [ ] **Step 5: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Vitals SurviveProject/Assets/09.Tests
git commit -m "Vitals: Vital 순수 클래스와 테스트 추가"
```

---

## Task 4: 아이템 데이터와 데이터베이스

**Files:**
- Create: `Assets/02.Scripts/Domain/Items/ItemCategory.cs`
- Create: `Assets/02.Scripts/Domain/Items/ToolType.cs`
- Create: `Assets/02.Scripts/Domain/Items/ItemDataSO.cs`
- Create: `Assets/02.Scripts/Domain/Items/ToolItemSO.cs`
- Create: `Assets/02.Scripts/Domain/Items/ConsumableItemSO.cs`
- Create: `Assets/02.Scripts/Domain/Items/ItemDatabaseSO.cs`
- Test: `Assets/09.Tests/EditMode/ItemDatabaseTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces:
  - `Survive.Items.ItemCategory` — `Resource`, `Tool`, `Consumable`, `Quest`
  - `Survive.Items.ToolType` — `None`, `Pickaxe`, `Hammer`, `Axe`
  - `Survive.Items.ItemDataSO` — 필드 `string id`, `string displayName`, `string description`, `Sprite icon`, `int maxStack`, `ItemCategory category`, `GameObject worldPrefab`
  - `Survive.Items.ToolItemSO : ItemDataSO` — 필드 `ToolType toolType`, `int tier`, `float harvestPower`, `float damage`, `float attackRange`, `float attackCooldown`, `string socketChildName`
  - `Survive.Items.ConsumableItemSO : ItemDataSO` — 필드 `string targetVitalId`, `float instantAmount`, `float durationSeconds`, `float ratePerSecond`
  - `Survive.Items.ItemDatabaseSO` — `ItemDataSO[] items`, `ItemDataSO GetById(string)`, `bool TryGetById(string, out ItemDataSO)`, `IReadOnlyList<string> Validate()`

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/09.Tests/EditMode/ItemDatabaseTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Survive.Items;

public class ItemDatabaseTests
{
    static ItemDataSO 아이템만들기(string id, int maxStack = 1)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        return it;
    }

    static ItemDatabaseSO DB만들기(params ItemDataSO[] items)
    {
        var db = ScriptableObject.CreateInstance<ItemDatabaseSO>();
        db.items = items;
        return db;
    }

    [Test]
    public void id로_아이템을_찾는다()
    {
        var scrap = 아이템만들기("scrap");
        var db = DB만들기(scrap, 아이템만들기("pickaxe"));
        Assert.AreSame(scrap, db.GetById("scrap"));
    }

    [Test]
    public void 없는_id는_null을_돌려준다()
    {
        var db = DB만들기(아이템만들기("scrap"));
        Assert.IsNull(db.GetById("없는아이템"));
    }

    [Test]
    public void TryGetById는_없으면_false다()
    {
        var db = DB만들기(아이템만들기("scrap"));
        Assert.IsFalse(db.TryGetById("없는아이템", out var found));
        Assert.IsNull(found);
    }

    [Test]
    public void 중복_id를_검증에서_보고한다()
    {
        var db = DB만들기(아이템만들기("scrap"), 아이템만들기("scrap"));
        var 문제 = db.Validate();
        Assert.AreEqual(1, 문제.Count);
        StringAssert.Contains("scrap", 문제[0]);
    }

    [Test]
    public void 빈_id를_검증에서_보고한다()
    {
        var db = DB만들기(아이템만들기(""));
        Assert.AreEqual(1, db.Validate().Count);
    }

    [Test]
    public void null_항목을_검증에서_보고한다()
    {
        var db = DB만들기(아이템만들기("scrap"), null);
        Assert.AreEqual(1, db.Validate().Count);
    }

    [Test]
    public void 문제가_없으면_검증결과가_비어있다()
    {
        var db = DB만들기(아이템만들기("scrap"), 아이템만들기("pickaxe"));
        Assert.AreEqual(0, db.Validate().Count);
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
```

기대 결과: `ItemDataSO`, `ItemDatabaseSO` 형식을 찾을 수 없다는 컴파일 에러.

- [ ] **Step 3: enum과 아이템 정의 구현**

`Assets/02.Scripts/Domain/Items/ItemCategory.cs`:

```csharp
namespace Survive.Items
{
    public enum ItemCategory
    {
        Resource,
        Tool,
        Consumable,
        Quest
    }
}
```

`Assets/02.Scripts/Domain/Items/ToolType.cs`:

```csharp
namespace Survive.Items
{
    public enum ToolType
    {
        None,
        Pickaxe,
        Hammer,
        Axe
    }
}
```

`Assets/02.Scripts/Domain/Items/ItemDataSO.cs`:

```csharp
using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Item")]
    public class ItemDataSO : ScriptableObject
    {
        [Tooltip("소문자 스네이크 케이스. 예: scrap, oxygen_filter")]
        public string id;

        public string displayName;

        [TextArea]
        public string description;

        public Sprite icon;

        [Min(1)]
        public int maxStack = 1;

        public ItemCategory category = ItemCategory.Resource;

        [Tooltip("바닥에 떨어질 때 생성할 프리팹")]
        public GameObject worldPrefab;
    }
}
```

`Assets/02.Scripts/Domain/Items/ToolItemSO.cs`:

```csharp
using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Tool")]
    public class ToolItemSO : ItemDataSO
    {
        [Header("채집")]
        public ToolType toolType = ToolType.Pickaxe;

        [Tooltip("채집 노드의 requiredTier 이상이어야 캘 수 있다")]
        public int tier = 1;

        [Tooltip("채집 시간 = 노드 baseDuration / harvestPower")]
        [Min(0.01f)]
        public float harvestPower = 1f;

        [Header("전투")]
        public float damage = 10f;
        public float attackRange = 2f;
        public float attackCooldown = 0.6f;

        [Header("장착")]
        [Tooltip("손 소켓(jointItemR) 아래 자식 오브젝트 이름. 예: pickaxe01")]
        public string socketChildName;
    }
}
```

`Assets/02.Scripts/Domain/Items/ConsumableItemSO.cs`:

```csharp
using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Consumable")]
    public class ConsumableItemSO : ItemDataSO
    {
        [Tooltip("health 또는 oxygen")]
        public string targetVitalId = "health";

        [Tooltip("사용 즉시 회복량")]
        public float instantAmount;

        [Tooltip("0이면 즉시형, 0보다 크면 지속형")]
        public float durationSeconds;

        [Tooltip("지속형일 때 초당 변화량")]
        public float ratePerSecond;
    }
}
```

- [ ] **Step 4: `ItemDatabaseSO` 구현**

`Assets/02.Scripts/Domain/Items/ItemDatabaseSO.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Item Database")]
    public class ItemDatabaseSO : ScriptableObject
    {
        public ItemDataSO[] items = new ItemDataSO[0];

        Dictionary<string, ItemDataSO> _byId;

        void OnEnable() => _byId = null;   // 재생성 강제

        void 색인만들기()
        {
            _byId = new Dictionary<string, ItemDataSO>();
            if (items == null) return;
            foreach (var it in items)
            {
                if (it == null || string.IsNullOrWhiteSpace(it.id)) continue;
                _byId[it.id] = it;
            }
        }

        public ItemDataSO GetById(string id)
        {
            TryGetById(id, out var found);
            return found;
        }

        public bool TryGetById(string id, out ItemDataSO item)
        {
            if (_byId == null) 색인만들기();
            if (string.IsNullOrEmpty(id))
            {
                item = null;
                return false;
            }
            return _byId.TryGetValue(id, out item);
        }

        /// <summary>
        /// 데이터 문제를 사람이 읽을 수 있는 문장으로 돌려준다.
        /// 비어 있으면 문제가 없다는 뜻이다.
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var 문제 = new List<string>();
            var 본것 = new HashSet<string>();

            if (items == null) return 문제;

            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                if (it == null)
                {
                    문제.Add($"{i}번 항목이 비어 있습니다.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(it.id))
                {
                    문제.Add($"{i}번 항목({it.name})의 id가 비어 있습니다.");
                    continue;
                }
                if (!본것.Add(it.id))
                    문제.Add($"id가 중복되었습니다: {it.id}");
            }
            return 문제;
        }

        void OnValidate()
        {
            _byId = null;
            foreach (var 문제 in Validate())
                Debug.LogError($"[ItemDatabaseSO] {문제}", this);
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 22개 통과 (누적).

- [ ] **Step 6: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Inventory SurviveProject/Assets/09.Tests
git commit -m "Inventory: 아이템 정의 SO와 데이터베이스 추가"
```

---

## Task 5: ItemStack과 Inventory 순수 클래스

이 계획에서 로직이 가장 촘촘한 부분이다. 스택 병합·초과분 반환·슬롯 교환의 경계 조건을 테스트로 못 박는다.

**Files:**
- Create: `Assets/02.Scripts/Domain/Items/ItemStack.cs`
- Create: `Assets/02.Scripts/Domain/Items/Inventory.cs`
- Test: `Assets/09.Tests/EditMode/InventoryTests.cs`

**Interfaces:**
- Consumes: `Survive.Items.ItemDataSO` (Task 4)
- Produces:
  - `Survive.Items.ItemStack` — 필드 `ItemDataSO item`, `int count`; 프로퍼티 `bool IsEmpty`; 메서드 `void Clear()`
  - `Survive.Items.Inventory` — 생성자 `Inventory(int slotCount)`, 프로퍼티 `int SlotCount`, `IReadOnlyList<ItemStack> Slots`; 메서드 `int TryAdd(ItemDataSO, int)`, `bool TryRemove(string, int)`, `int CountOf(string)`, `bool Has(string, int)`, `void MoveOrSwap(int, int)`; 이벤트 `event Action Changed`

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/09.Tests/EditMode/InventoryTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Survive.Items;

public class InventoryTests
{
    static ItemDataSO 아이템(string id, int maxStack)
    {
        var it = ScriptableObject.CreateInstance<ItemDataSO>();
        it.id = id;
        it.displayName = id;
        it.maxStack = maxStack;
        return it;
    }

    [Test]
    public void 빈_인벤토리에_넣으면_전부_들어간다()
    {
        var inv = new Inventory(4);
        int 남은수 = inv.TryAdd(아이템("scrap", 99), 30);
        Assert.AreEqual(0, 남은수);
        Assert.AreEqual(30, inv.CountOf("scrap"));
    }

    [Test]
    public void 기존_스택을_먼저_채운다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 90);
        inv.TryAdd(scrap, 5);

        Assert.AreEqual(95, inv.CountOf("scrap"));
        Assert.AreEqual(95, inv.Slots[0].count);
        Assert.IsTrue(inv.Slots[1].IsEmpty, "두 번째 슬롯을 쓰면 안 된다");
    }

    [Test]
    public void 스택이_넘치면_다음_슬롯으로_넘어간다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 150);

        Assert.AreEqual(99, inv.Slots[0].count);
        Assert.AreEqual(51, inv.Slots[1].count);
        Assert.AreEqual(150, inv.CountOf("scrap"));
    }

    [Test]
    public void 자리가_모자라면_남은_수량을_돌려준다()
    {
        var scrap = 아이템("scrap", 10);
        var inv = new Inventory(2);
        int 남은수 = inv.TryAdd(scrap, 25);   // 최대 20개만 들어감

        Assert.AreEqual(5, 남은수);
        Assert.AreEqual(20, inv.CountOf("scrap"));
    }

    [Test]
    public void 스택_불가_아이템은_슬롯을_하나씩_쓴다()
    {
        var 곡괭이 = 아이템("pickaxe", 1);
        var inv = new Inventory(3);
        int 남은수 = inv.TryAdd(곡괭이, 3);

        Assert.AreEqual(0, 남은수);
        Assert.AreEqual(1, inv.Slots[0].count);
        Assert.AreEqual(1, inv.Slots[1].count);
        Assert.AreEqual(1, inv.Slots[2].count);
    }

    [Test]
    public void 제거하면_수량이_준다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 50);

        Assert.IsTrue(inv.TryRemove("scrap", 20));
        Assert.AreEqual(30, inv.CountOf("scrap"));
    }

    [Test]
    public void 수량이_모자라면_제거하지_않는다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 10);

        Assert.IsFalse(inv.TryRemove("scrap", 20));
        Assert.AreEqual(10, inv.CountOf("scrap"), "실패했으면 원상태여야 한다");
    }

    [Test]
    public void 여러_슬롯에_걸쳐_제거한다()
    {
        var scrap = 아이템("scrap", 10);
        var inv = new Inventory(4);
        inv.TryAdd(scrap, 25);          // 10 / 10 / 5

        Assert.IsTrue(inv.TryRemove("scrap", 22));
        Assert.AreEqual(3, inv.CountOf("scrap"));
    }

    [Test]
    public void 다_비운_슬롯은_비워진다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 5);
        inv.TryRemove("scrap", 5);

        Assert.IsTrue(inv.Slots[0].IsEmpty);
        Assert.IsNull(inv.Slots[0].item);
    }

    [Test]
    public void 없는_아이템_제거는_실패한다()
    {
        var inv = new Inventory(2);
        Assert.IsFalse(inv.TryRemove("없는아이템", 1));
    }

    [Test]
    public void Has는_보유량을_판정한다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 5);

        Assert.IsTrue(inv.Has("scrap", 5));
        Assert.IsFalse(inv.Has("scrap", 6));
    }

    [Test]
    public void 빈_슬롯으로_옮기면_이동한다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(3);
        inv.TryAdd(scrap, 5);

        inv.MoveOrSwap(0, 2);

        Assert.IsTrue(inv.Slots[0].IsEmpty);
        Assert.AreEqual(5, inv.Slots[2].count);
    }

    [Test]
    public void 다른_아이템끼리는_교환된다()
    {
        var scrap = 아이템("scrap", 99);
        var 곡괭이 = 아이템("pickaxe", 1);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 5);
        inv.TryAdd(곡괭이, 1);

        inv.MoveOrSwap(0, 1);

        Assert.AreEqual("pickaxe", inv.Slots[0].item.id);
        Assert.AreEqual("scrap", inv.Slots[1].item.id);
    }

    [Test]
    public void 같은_아이템끼리는_병합된다()
    {
        var scrap = 아이템("scrap", 99);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 5);
        inv.Slots[1].item = scrap;      // 두 번째 슬롯에 수동 배치
        inv.Slots[1].count = 3;

        inv.MoveOrSwap(1, 0);

        Assert.AreEqual(8, inv.Slots[0].count);
        Assert.IsTrue(inv.Slots[1].IsEmpty);
    }

    [Test]
    public void 병합시_최대치를_넘는_분량은_남는다()
    {
        var scrap = 아이템("scrap", 10);
        var inv = new Inventory(2);
        inv.TryAdd(scrap, 8);
        inv.Slots[1].item = scrap;
        inv.Slots[1].count = 7;

        inv.MoveOrSwap(1, 0);

        Assert.AreEqual(10, inv.Slots[0].count);
        Assert.AreEqual(5, inv.Slots[1].count);
    }

    [Test]
    public void 범위를_벗어난_슬롯_이동은_무시된다()
    {
        var inv = new Inventory(2);
        Assert.DoesNotThrow(() => inv.MoveOrSwap(0, 5));
        Assert.DoesNotThrow(() => inv.MoveOrSwap(-1, 0));
    }

    [Test]
    public void 변경되면_Changed가_발생한다()
    {
        var inv = new Inventory(2);
        int 횟수 = 0;
        inv.Changed += () => 횟수++;

        inv.TryAdd(아이템("scrap", 99), 5);

        Assert.AreEqual(1, 횟수);
    }

    [Test]
    public void 아무것도_넣지_못하면_Changed가_발생하지_않는다()
    {
        var scrap = 아이템("scrap", 1);
        var inv = new Inventory(1);
        inv.TryAdd(scrap, 1);

        int 횟수 = 0;
        inv.Changed += () => 횟수++;
        int 남은수 = inv.TryAdd(scrap, 1);

        Assert.AreEqual(1, 남은수);
        Assert.AreEqual(0, 횟수);
    }

    [Test]
    public void null_아이템_추가는_전량_반환한다()
    {
        var inv = new Inventory(2);
        Assert.AreEqual(5, inv.TryAdd(null, 5));
    }

    [Test]
    public void 0개_이하_추가는_전량_반환한다()
    {
        var inv = new Inventory(2);
        Assert.AreEqual(0, inv.TryAdd(아이템("scrap", 99), 0));
        Assert.AreEqual(0, inv.CountOf("scrap"));
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
```

기대 결과: `Inventory`, `ItemStack` 형식을 찾을 수 없다는 컴파일 에러.

- [ ] **Step 3: `ItemStack` 구현**

`Assets/02.Scripts/Domain/Items/ItemStack.cs`:

```csharp
using System;

namespace Survive.Items
{
    [Serializable]
    public class ItemStack
    {
        public ItemDataSO item;
        public int count;

        public ItemStack() { }

        public ItemStack(ItemDataSO item, int count)
        {
            this.item = item;
            this.count = count;
        }

        public bool IsEmpty => item == null || count <= 0;

        /// <summary>이 스택에 더 들어갈 수 있는 개수.</summary>
        public int RemainingSpace => item == null ? 0 : item.maxStack - count;

        public void Clear()
        {
            item = null;
            count = 0;
        }
    }
}
```

- [ ] **Step 4: `Inventory` 구현**

`Assets/02.Scripts/Domain/Items/Inventory.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Items
{
    /// <summary>
    /// 고정 슬롯 인벤토리. MonoBehaviour가 아니므로 Unity 실행 없이 테스트할 수 있다.
    /// </summary>
    public class Inventory
    {
        readonly ItemStack[] _slots;

        public Inventory(int slotCount)
        {
            if (slotCount <= 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
            _slots = new ItemStack[slotCount];
            for (int i = 0; i < slotCount; i++) _slots[i] = new ItemStack();
        }

        public int SlotCount => _slots.Length;
        public IReadOnlyList<ItemStack> Slots => _slots;

        public event Action Changed;

        /// <summary>
        /// 기존 스택을 먼저 채우고, 남으면 빈 슬롯을 쓴다.
        /// </summary>
        /// <returns>넣지 못하고 남은 개수. 0이면 전부 들어갔다.</returns>
        public int TryAdd(ItemDataSO item, int count)
        {
            if (item == null || count <= 0) return count > 0 ? count : 0;

            int 남은수 = count;

            // 1단계 — 기존 스택 채우기
            for (int i = 0; i < _slots.Length && 남은수 > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.item != item) continue;

                int 넣을수 = Mathf.Min(slot.RemainingSpace, 남은수);
                slot.count += 넣을수;
                남은수 -= 넣을수;
            }

            // 2단계 — 빈 슬롯 쓰기
            for (int i = 0; i < _slots.Length && 남은수 > 0; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty) continue;

                int 넣을수 = Mathf.Min(item.maxStack, 남은수);
                slot.item = item;
                slot.count = 넣을수;
                남은수 -= 넣을수;
            }

            if (남은수 != count) Changed?.Invoke();
            return 남은수;
        }

        /// <summary>수량이 모자라면 아무것도 건드리지 않고 false를 돌려준다.</summary>
        public bool TryRemove(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (!Has(itemId, count)) return false;

            int 남은수 = count;
            for (int i = 0; i < _slots.Length && 남은수 > 0; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty || slot.item.id != itemId) continue;

                int 뺄수 = Mathf.Min(slot.count, 남은수);
                slot.count -= 뺄수;
                남은수 -= 뺄수;
                if (slot.count <= 0) slot.Clear();
            }

            Changed?.Invoke();
            return true;
        }

        public int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int 합 = 0;
            foreach (var slot in _slots)
                if (!slot.IsEmpty && slot.item.id == itemId) 합 += slot.count;
            return 합;
        }

        public bool Has(string itemId, int count) => CountOf(itemId) >= count;

        /// <summary>
        /// 같은 아이템이면 병합(초과분은 출발 슬롯에 남음), 다르면 교환, 빈 곳이면 이동.
        /// </summary>
        public void MoveOrSwap(int fromSlot, int toSlot)
        {
            if (!유효한슬롯(fromSlot) || !유효한슬롯(toSlot) || fromSlot == toSlot) return;

            var from = _slots[fromSlot];
            var to = _slots[toSlot];
            if (from.IsEmpty) return;

            if (!to.IsEmpty && to.item == from.item)
            {
                int 옮길수 = Mathf.Min(to.RemainingSpace, from.count);
                if (옮길수 <= 0) return;

                to.count += 옮길수;
                from.count -= 옮길수;
                if (from.count <= 0) from.Clear();
            }
            else
            {
                var 임시아이템 = to.item;
                var 임시개수 = to.count;
                to.item = from.item;
                to.count = from.count;
                from.item = 임시아이템;
                from.count = 임시개수;
            }

            Changed?.Invoke();
        }

        bool 유효한슬롯(int index) => index >= 0 && index < _slots.Length;
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 42개 통과 (누적).

- [ ] **Step 6: 낡은 인벤토리 스크립트 삭제**

`ItemInfo`, `ItemListSO`, `InventoryItem`은 대체되었다. `InventoryUI.cs`는 빈 클래스지만 씬 참조가 없으므로 함께 지운다 — 계획 2에서 새로 만든다.

```bash
cd E:/SurviveProject/SurviveProject
rm Assets/02.Scripts/Inventory/ItemInfo.cs Assets/02.Scripts/Inventory/ItemInfo.cs.meta
rm Assets/02.Scripts/Inventory/ItemListSO.cs Assets/02.Scripts/Inventory/ItemListSO.cs.meta
rm Assets/02.Scripts/Inventory/InventoryItem.cs Assets/02.Scripts/Inventory/InventoryItem.cs.meta
rm Assets/02.Scripts/Inventory/InventoryUI.cs Assets/02.Scripts/Inventory/InventoryUI.cs.meta
```

- [ ] **Step 7: 삭제 후 컴파일 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --force-recompile true --wait-for-domain-reload true
uloop get-logs --log-type Error --max-count 20
```

기대 결과: 에러 0건. `ItemListSO` 에셋이 프로젝트에 있었다면 스크립트 참조가 끊어졌다는 경고가 날 수 있다 — 해당 `.asset` 파일이 있으면 함께 지운다.

- [ ] **Step 8: 커밋**

```bash
cd E:/SurviveProject
git add -A SurviveProject/Assets/02.Scripts/Inventory SurviveProject/Assets/09.Tests
git commit -m "Inventory: 슬롯 로직 구현, 낡은 인벤토리 스크립트 제거

스택 병합·초과분 반환·슬롯 교환의 경계 조건을 테스트 20개로 고정."
```

---

## Task 6: Input System 도입

**Files:**
- Modify: `Packages/manifest.json`
- Modify: `ProjectSettings/ProjectSettings.asset` (`activeInputHandler`)
- Modify: `Assets/02.Scripts/Survive.asmdef`
- Create: `Assets/02.Scripts/Input/PlayerInputActions.inputactions`

**Interfaces:**
- Consumes: 없음
- Produces: 생성된 C# 클래스 `PlayerInputActions`, 액션 맵 `Gameplay`·`UI`

- [ ] **Step 1: 패키지 추가**

`Packages/manifest.json`의 `dependencies`에 다음 줄을 추가한다 (알파벳 순서상 `com.unity.ide.vscode` 다음):

```json
    "com.unity.inputsystem": "1.14.0",
```

- [ ] **Step 2: 입력 핸들러를 Both로 변경**

`ProjectSettings/ProjectSettings.asset`에서 `activeInputHandler: 0`을 `activeInputHandler: 2`로 바꾼다.

`0` = 레거시 전용, `1` = Input System 전용, `2` = 양쪽. **Task 16에서 `PlayerController`를 제거한 뒤 `1`로 바꾼다.** 지금 바로 `1`로 바꾸면 아직 `UnityEngine.Input`을 쓰는 `PlayerController`가 런타임 예외를 던진다.

- [ ] **Step 3: 에디터 재시작**

패키지 추가와 `activeInputHandler` 변경은 에디터 재시작이 필요하다.

```bash
cd E:/SurviveProject/SurviveProject
uloop launch --restart true
```

`--restart` 옵션이 없으면 `uloop launch --help`로 확인해 해당하는 옵션을 쓴다. 재시작 후:

```bash
uloop compile --wait-for-domain-reload true
uloop get-logs --log-type Error --max-count 20
```

기대 결과: 에러 0건.

- [ ] **Step 4: 액션 에셋 생성**

`Assets/02.Scripts/Input/PlayerInputActions.inputactions` 파일을 아래 내용으로 만든다. 액션 맵은 `Gameplay`와 `UI` 둘이다.

```json
{
    "name": "PlayerInputActions",
    "maps": [
        {
            "name": "Gameplay",
            "id": "a1000000-0000-0000-0000-000000000001",
            "actions": [
                { "name": "Move", "type": "Value", "id": "a1000000-0000-0000-0000-000000000101", "expectedControlType": "Vector2", "processors": "", "interactions": "", "initialStateCheck": true },
                { "name": "Look", "type": "Value", "id": "a1000000-0000-0000-0000-000000000102", "expectedControlType": "Vector2", "processors": "", "interactions": "", "initialStateCheck": true },
                { "name": "Jump", "type": "Button", "id": "a1000000-0000-0000-0000-000000000103", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false },
                { "name": "Sprint", "type": "Button", "id": "a1000000-0000-0000-0000-000000000104", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false },
                { "name": "Interact", "type": "Button", "id": "a1000000-0000-0000-0000-000000000105", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false },
                { "name": "Attack", "type": "Button", "id": "a1000000-0000-0000-0000-000000000106", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false },
                { "name": "ToggleInventory", "type": "Button", "id": "a1000000-0000-0000-0000-000000000107", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false },
                { "name": "Pause", "type": "Button", "id": "a1000000-0000-0000-0000-000000000108", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false }
            ],
            "bindings": [
                { "name": "WASD", "id": "b1000000-0000-0000-0000-000000000001", "path": "2DVector", "interactions": "", "processors": "", "groups": "", "action": "Move", "isComposite": true, "isPartOfComposite": false },
                { "name": "up", "id": "b1000000-0000-0000-0000-000000000002", "path": "<Keyboard>/w", "interactions": "", "processors": "", "groups": "", "action": "Move", "isComposite": false, "isPartOfComposite": true },
                { "name": "down", "id": "b1000000-0000-0000-0000-000000000003", "path": "<Keyboard>/s", "interactions": "", "processors": "", "groups": "", "action": "Move", "isComposite": false, "isPartOfComposite": true },
                { "name": "left", "id": "b1000000-0000-0000-0000-000000000004", "path": "<Keyboard>/a", "interactions": "", "processors": "", "groups": "", "action": "Move", "isComposite": false, "isPartOfComposite": true },
                { "name": "right", "id": "b1000000-0000-0000-0000-000000000005", "path": "<Keyboard>/d", "interactions": "", "processors": "", "groups": "", "action": "Move", "isComposite": false, "isPartOfComposite": true },
                { "name": "", "id": "b1000000-0000-0000-0000-000000000006", "path": "<Mouse>/delta", "interactions": "", "processors": "", "groups": "", "action": "Look", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "b1000000-0000-0000-0000-000000000007", "path": "<Keyboard>/space", "interactions": "", "processors": "", "groups": "", "action": "Jump", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "b1000000-0000-0000-0000-000000000008", "path": "<Keyboard>/leftShift", "interactions": "", "processors": "", "groups": "", "action": "Sprint", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "b1000000-0000-0000-0000-000000000009", "path": "<Keyboard>/e", "interactions": "", "processors": "", "groups": "", "action": "Interact", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "b1000000-0000-0000-0000-00000000000a", "path": "<Mouse>/leftButton", "interactions": "", "processors": "", "groups": "", "action": "Attack", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "b1000000-0000-0000-0000-00000000000b", "path": "<Keyboard>/tab", "interactions": "", "processors": "", "groups": "", "action": "ToggleInventory", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "b1000000-0000-0000-0000-00000000000c", "path": "<Keyboard>/escape", "interactions": "", "processors": "", "groups": "", "action": "Pause", "isComposite": false, "isPartOfComposite": false }
            ]
        },
        {
            "name": "UI",
            "id": "a1000000-0000-0000-0000-000000000002",
            "actions": [
                { "name": "Point", "type": "Value", "id": "a1000000-0000-0000-0000-000000000201", "expectedControlType": "Vector2", "processors": "", "interactions": "", "initialStateCheck": true },
                { "name": "Click", "type": "Button", "id": "a1000000-0000-0000-0000-000000000202", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false },
                { "name": "Cancel", "type": "Button", "id": "a1000000-0000-0000-0000-000000000203", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false }
            ],
            "bindings": [
                { "name": "", "id": "b1000000-0000-0000-0000-000000000101", "path": "<Mouse>/position", "interactions": "", "processors": "", "groups": "", "action": "Point", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "b1000000-0000-0000-0000-000000000102", "path": "<Mouse>/leftButton", "interactions": "", "processors": "", "groups": "", "action": "Click", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "b1000000-0000-0000-0000-000000000103", "path": "<Keyboard>/escape", "interactions": "", "processors": "", "groups": "", "action": "Cancel", "isComposite": false, "isPartOfComposite": false }
            ]
        }
    ],
    "controlSchemes": []
}
```

- [ ] **Step 5: C# 클래스 생성 활성화**

`.inputactions` 에셋의 임포터에서 "Generate C# Class"를 켜야 `PlayerInputActions` 타입이 생긴다.

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'UnityEditor.AssetDatabase.Refresh(); var p="Assets/02.Scripts/Input/PlayerInputActions.inputactions"; var imp=UnityEditor.AssetImporter.GetAtPath(p); var so=new UnityEditor.SerializedObject(imp); so.FindProperty("m_GenerateWrapperCode").boolValue=true; so.ApplyModifiedProperties(); imp.SaveAndReimport(); return "generated";'
```

- [ ] **Step 6: asmdef에 Input System 참조 추가**

`Assets/02.Scripts/Survive.asmdef`의 `references`를 다음으로 바꾼다:

```json
    "references": ["Cinemachine", "Unity.InputSystem", "Unity.AI.Navigation", "Unity.Timeline"],
```

- [ ] **Step 7: 컴파일 및 기존 테스트 회귀 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --force-recompile true --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 에러 0건, 42개 통과 (Task 5와 동일 — 회귀 없음).

- [ ] **Step 8: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Packages/manifest.json \
        SurviveProject/ProjectSettings/ProjectSettings.asset \
        SurviveProject/Assets/02.Scripts/Input \
        SurviveProject/Assets/02.Scripts/Survive.asmdef
git commit -m "Input System 패키지 도입과 액션 맵 정의

activeInputHandler를 Both(2)로 둔다. PlayerController가 아직 레거시
Input을 쓰므로, 제거 후 Input System 전용(1)으로 바꾼다."
```

---

## Task 7: InputReaderSO

**Files:**
- Create: `Assets/02.Scripts/Input/InputReaderSO.cs`
- Create: `Assets/08.Data/Input/InputReader.asset`

**Interfaces:**
- Consumes: `PlayerInputActions` (Task 6)
- Produces: `Survive.InputSystem.InputReaderSO` — 이벤트 `MoveEvent(Vector2)`, `LookEvent(Vector2)`, `JumpEvent()`, `SprintEvent(bool)`, `InteractEvent()`, `InteractCancelledEvent()`, `AttackEvent()`, `ToggleInventoryEvent()`, `PauseEvent()`, `CancelEvent()`; 메서드 `EnableGameplayInput()`, `EnableUIInput()`, `DisableAllInput()`; 프로퍼티 `Vector2 MoveValue`, `Vector2 LookValue`, `bool IsSprinting`, `bool IsInteractHeld`

- [ ] **Step 1: `InputReaderSO` 구현**

`Assets/02.Scripts/Input/InputReaderSO.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survive.InputSystem
{
    /// <summary>
    /// 입력을 이벤트로 바꾸는 유일한 통로.
    /// 다른 시스템은 UnityEngine.Input이나 InputAction을 직접 만지지 않는다.
    /// ScriptableObject라서 씬이 달라도 같은 에셋 하나를 공유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Input/Input Reader")]
    public class InputReaderSO : ScriptableObject,
        PlayerInputActions.IGameplayActions,
        PlayerInputActions.IUIActions
    {
        PlayerInputActions _actions;

        public event Action<Vector2> MoveEvent;
        public event Action<Vector2> LookEvent;
        public event Action JumpEvent;
        public event Action<bool> SprintEvent;
        public event Action InteractEvent;
        public event Action InteractCancelledEvent;
        public event Action AttackEvent;
        public event Action ToggleInventoryEvent;
        public event Action PauseEvent;
        public event Action CancelEvent;

        public Vector2 MoveValue { get; private set; }
        public Vector2 LookValue { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsInteractHeld { get; private set; }

        void OnEnable()
        {
            if (_actions == null)
            {
                _actions = new PlayerInputActions();
                _actions.Gameplay.SetCallbacks(this);
                _actions.UI.SetCallbacks(this);
            }
            EnableGameplayInput();
        }

        void OnDisable() => DisableAllInput();

        public void EnableGameplayInput()
        {
            _actions.UI.Disable();
            _actions.Gameplay.Enable();
        }

        public void EnableUIInput()
        {
            _actions.Gameplay.Disable();
            _actions.UI.Enable();

            // 게임플레이 맵을 끄면 콜백이 오지 않으므로 이동이 눌린 채로 남는다.
            MoveValue = Vector2.zero;
            LookValue = Vector2.zero;
            IsSprinting = false;
            IsInteractHeld = false;
            MoveEvent?.Invoke(Vector2.zero);
            LookEvent?.Invoke(Vector2.zero);
        }

        public void DisableAllInput()
        {
            _actions?.Gameplay.Disable();
            _actions?.UI.Disable();
            MoveValue = Vector2.zero;
            LookValue = Vector2.zero;
            IsSprinting = false;
            IsInteractHeld = false;
        }

        // ── Gameplay ─────────────────────────────────────────────

        public void OnMove(InputAction.CallbackContext ctx)
        {
            MoveValue = ctx.ReadValue<Vector2>();
            MoveEvent?.Invoke(MoveValue);
        }

        public void OnLook(InputAction.CallbackContext ctx)
        {
            LookValue = ctx.ReadValue<Vector2>();
            LookEvent?.Invoke(LookValue);
        }

        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) JumpEvent?.Invoke();
        }

        public void OnSprint(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) { IsSprinting = true; SprintEvent?.Invoke(true); }
            else if (ctx.canceled) { IsSprinting = false; SprintEvent?.Invoke(false); }
        }

        public void OnInteract(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) { IsInteractHeld = true; InteractEvent?.Invoke(); }
            else if (ctx.canceled) { IsInteractHeld = false; InteractCancelledEvent?.Invoke(); }
        }

        public void OnAttack(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) AttackEvent?.Invoke();
        }

        public void OnToggleInventory(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) ToggleInventoryEvent?.Invoke();
        }

        public void OnPause(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) PauseEvent?.Invoke();
        }

        // ── UI ───────────────────────────────────────────────────

        public void OnPoint(InputAction.CallbackContext ctx) { }
        public void OnClick(InputAction.CallbackContext ctx) { }

        public void OnCancel(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) CancelEvent?.Invoke();
        }
    }
}
```

생성된 `PlayerInputActions`의 인터페이스 메서드 이름이 위와 다르면 (예: `OnToggleInventory`가 없다면) 액션 이름 철자를 Task 6의 에셋과 대조한다.

- [ ] **Step 2: 컴파일 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop get-logs --log-type Error --max-count 20
```

기대 결과: 에러 0건. 인터페이스 미구현 에러가 나면 생성된 클래스가 요구하는 메서드를 추가한다.

- [ ] **Step 3: `InputReader` 에셋 생성**

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'using UnityEditor; using UnityEngine; var t=System.Type.GetType("Survive.InputSystem.InputReaderSO, Assembly-CSharp"); var so=ScriptableObject.CreateInstance(t); System.IO.Directory.CreateDirectory("Assets/08.Data/Input"); AssetDatabase.CreateAsset(so,"Assets/08.Data/Input/InputReader.asset"); AssetDatabase.SaveAssets(); return AssetDatabase.GetAssetPath(so);'
```

`System.IO`는 동적 코드에서 금지되므로, 폴더는 먼저 터미널로 만든다:

```bash
cd E:/SurviveProject/SurviveProject
mkdir -p Assets/08.Data/Input
uloop execute-dynamic-code --code 'using UnityEditor; using UnityEngine; AssetDatabase.Refresh(); var t=System.Type.GetType("Survive.InputSystem.InputReaderSO, Assembly-CSharp"); var so=ScriptableObject.CreateInstance(t); AssetDatabase.CreateAsset(so,"Assets/08.Data/Input/InputReader.asset"); AssetDatabase.SaveAssets(); return AssetDatabase.GetAssetPath(so);'
```

기대 결과: `Assets/08.Data/Input/InputReader.asset` 경로가 반환된다.

- [ ] **Step 4: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Input SurviveProject/Assets/08.Data
git commit -m "Input: InputReaderSO와 공유 에셋 추가

UI 맵으로 전환할 때 이동·시점 값을 0으로 초기화한다. 맵을 끄면
콜백이 오지 않아 입력이 눌린 채로 남기 때문이다."
```

---

## Task 8: PlayerContext 계약 확정

설계 문서 10장의 워크스트림 계약이다. 이후 태스크들이 이 타입을 참조하므로 **구현보다 먼저 시그니처를 고정한다.**

**Files:**
- Create: `Assets/02.Scripts/Player/PlayerContext.cs`

**Interfaces:**
- Consumes: 없음 (하위 컴포넌트는 아직 없어도 된다)
- Produces: `Survive.Player.PlayerContext` — 프로퍼티 `PlayerLocomotion Locomotion`, `PlayerCameraRig CameraRig`, `PlayerToolHolder ToolHolder`, `PlayerVitals Vitals`, `PlayerInventory Inventory`, `Transform Transform`

- [ ] **Step 1: 빈 컴포넌트 스텁 4개 생성**

`PlayerContext`가 참조할 타입들이 아직 없으므로, 컴파일이 되도록 최소 스텁을 만든다. 각각은 이후 태스크에서 채운다.

`Assets/02.Scripts/Player/PlayerLocomotion.cs`:

```csharp
using UnityEngine;

namespace Survive.Player
{
    public class PlayerLocomotion : MonoBehaviour { }
}
```

`Assets/02.Scripts/Player/PlayerCameraRig.cs`:

```csharp
using UnityEngine;

namespace Survive.Player
{
    public class PlayerCameraRig : MonoBehaviour { }
}
```

`Assets/02.Scripts/Player/PlayerToolHolder.cs`:

```csharp
using UnityEngine;

namespace Survive.Player
{
    public class PlayerToolHolder : MonoBehaviour { }
}
```

`Assets/02.Scripts/Vitals/PlayerVitals.cs`:

```csharp
using UnityEngine;

namespace Survive.Vitals
{
    public class PlayerVitals : MonoBehaviour { }
}
```

`Assets/02.Scripts/Inventory/PlayerInventory.cs`:

```csharp
using UnityEngine;

namespace Survive.Items
{
    public class PlayerInventory : MonoBehaviour { }
}
```

- [ ] **Step 2: `PlayerContext` 구현**

`Assets/02.Scripts/Player/PlayerContext.cs`:

```csharp
using UnityEngine;
using Survive.Items;
using Survive.Vitals;

namespace Survive.Player
{
    /// <summary>
    /// 플레이어 하위 시스템의 단일 접근점.
    /// 상호작용 대상이나 UI가 플레이어의 개별 컴포넌트를 찾아다니지 않게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerContext : MonoBehaviour
    {
        public PlayerLocomotion Locomotion { get; private set; }
        public PlayerCameraRig CameraRig { get; private set; }
        public PlayerToolHolder ToolHolder { get; private set; }
        public PlayerVitals Vitals { get; private set; }
        public PlayerInventory Inventory { get; private set; }
        public Transform Transform { get; private set; }

        void Awake()
        {
            Transform = transform;
            Locomotion = GetComponentInChildren<PlayerLocomotion>(true);
            CameraRig  = GetComponentInChildren<PlayerCameraRig>(true);
            ToolHolder = GetComponentInChildren<PlayerToolHolder>(true);
            Vitals     = GetComponentInChildren<PlayerVitals>(true);
            Inventory  = GetComponentInChildren<PlayerInventory>(true);
        }
    }
}
```

`GetComponentInChildren`으로 채우므로 하위 컴포넌트의 구현 순서에 얽매이지 않는다.

- [ ] **Step 3: 컴파일 및 회귀 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 에러 0건, 42개 통과.

- [ ] **Step 4: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Player \
        SurviveProject/Assets/02.Scripts/Vitals \
        SurviveProject/Assets/02.Scripts/Inventory
git commit -m "Player: PlayerContext 계약 확정과 하위 컴포넌트 스텁

설계 문서 10장의 워크스트림 계약. 이후 태스크가 이 시그니처에 의존한다."
```

---

## Task 9: PlayerVitals와 산소 시스템

**Files:**
- Create: `Assets/02.Scripts/Domain/Vitals/VitalDefinitionSO.cs`
- Create: `Assets/02.Scripts/Domain/Vitals/IOxygenModifier.cs`
- Modify: `Assets/02.Scripts/Vitals/PlayerVitals.cs` (Task 8의 스텁을 채움)
- Create: `Assets/08.Data/Vitals/Health.asset`, `Assets/08.Data/Vitals/Oxygen.asset`

**Interfaces:**
- Consumes: `Survive.Vitals.Vital` (Task 3)
- Produces:
  - `Survive.Vitals.IOxygenModifier` — 프로퍼티 `float OxygenDeltaPerSecond`
  - `Survive.Vitals.VitalDefinitionSO` — 필드 `string id`, `string displayName`, `float maxValue`, `float startValue`, `float passiveRatePerSecond`
  - `Survive.Vitals.PlayerVitals` — 프로퍼티 `Vital Health`, `Vital Oxygen`; 이벤트 `event Action Died`; 메서드 `void RegisterOxygenModifier(IOxygenModifier)`, `void UnregisterOxygenModifier(IOxygenModifier)`, `float CurrentOxygenRate()`

- [ ] **Step 1: 실패하는 테스트 작성**

산소 보정 겹침 규칙(**최댓값 하나만 채택, 합산하지 않음**)을 검증한다.

`PlayerVitals`는 MonoBehaviour라 `Assembly-CSharp`에 있고, 테스트 어셈블리는 거기에 닿을 수 없다. 그래서 **계산만 도메인의 정적 클래스 `OxygenRate`로 떼어낸다.** `PlayerVitals`는 그것을 호출하기만 한다.

`Assets/09.Tests/EditMode/OxygenRateTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Survive.Vitals;

public class OxygenRateTests
{
    class 보정 : IOxygenModifier
    {
        public float 값;
        public float OxygenDeltaPerSecond => 값;
    }

    [Test]
    public void 보정이_없으면_기본_감소율만_적용된다()
    {
        var 결과 = OxygenRate.Calculate(-1.5f, new List<IOxygenModifier>());
        Assert.AreEqual(-1.5f, 결과, 0.0001f);
    }

    [Test]
    public void 회복_지대에_들어가면_그_값이_적용된다()
    {
        var 목록 = new List<IOxygenModifier> { new 보정 { 값 = 5f } };
        Assert.AreEqual(5f, OxygenRate.Calculate(-1.5f, 목록), 0.0001f);
    }

    [Test]
    public void 여러_보정이_겹치면_가장_유리한_값만_쓴다()
    {
        var 목록 = new List<IOxygenModifier>
        {
            new 보정 { 값 = -8f },   // 모래폭풍
            new 보정 { 값 = 5f }     // 버섯 군락
        };
        // 합산(-3)이 아니라 최댓값(5)이어야 한다
        Assert.AreEqual(5f, OxygenRate.Calculate(-1.5f, 목록), 0.0001f);
    }

    [Test]
    public void 기본_감소율보다_불리한_보정도_최댓값_규칙을_따른다()
    {
        var 목록 = new List<IOxygenModifier> { new 보정 { 값 = -8f } };
        Assert.AreEqual(-8f, OxygenRate.Calculate(-1.5f, 목록), 0.0001f);
    }

    [Test]
    public void null_보정은_무시된다()
    {
        var 목록 = new List<IOxygenModifier> { null, new 보정 { 값 = 3f } };
        Assert.AreEqual(3f, OxygenRate.Calculate(-1.5f, 목록), 0.0001f);
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
```

기대 결과: `IOxygenModifier`와 `OxygenRate`를 찾을 수 없다는 컴파일 에러.

- [ ] **Step 3: `IOxygenModifier`와 `VitalDefinitionSO` 구현**

`Assets/02.Scripts/Domain/Vitals/IOxygenModifier.cs`:

```csharp
namespace Survive.Vitals
{
    /// <summary>
    /// 환경이 산소에 거는 보정. 양수는 회복, 음수는 추가 소모.
    /// 여러 개가 겹치면 가장 유리한 값 하나만 쓴다 (합산하지 않는다).
    /// </summary>
    public interface IOxygenModifier
    {
        float OxygenDeltaPerSecond { get; }
    }
}
```

`Assets/02.Scripts/Domain/Vitals/OxygenRate.cs`:

```csharp
using System.Collections.Generic;

namespace Survive.Vitals
{
    /// <summary>
    /// 산소 변화율 계산. 도메인 계층의 순수 정적 클래스라 EditMode로 검증한다.
    /// </summary>
    public static class OxygenRate
    {
        /// <summary>
        /// 보정이 하나도 없으면 기본 감소율, 있으면 그 중 가장 유리한(가장 큰) 값.
        /// 합산하지 않는 이유: 버섯 군락 안에 있으면 안전하다는 규칙이 읽히기 쉽다.
        /// </summary>
        public static float Calculate(float baseRate, IReadOnlyList<IOxygenModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0) return baseRate;

            bool 하나라도있음 = false;
            float 최댓값 = float.NegativeInfinity;

            for (int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                if (m == null) continue;
                하나라도있음 = true;
                if (m.OxygenDeltaPerSecond > 최댓값) 최댓값 = m.OxygenDeltaPerSecond;
            }

            return 하나라도있음 ? 최댓값 : baseRate;
        }
    }
}
```

`Assets/02.Scripts/Domain/Vitals/VitalDefinitionSO.cs`:

```csharp
using UnityEngine;

namespace Survive.Vitals
{
    [CreateAssetMenu(menuName = "Survive/Vitals/Vital Definition")]
    public class VitalDefinitionSO : ScriptableObject
    {
        [Tooltip("health 또는 oxygen")]
        public string id = "health";

        public string displayName = "체력";

        public float maxValue = 100f;
        public float startValue = 100f;

        [Tooltip("초당 자연 변화량. 산소는 음수, 체력은 0")]
        public float passiveRatePerSecond;
    }
}
```

- [ ] **Step 4: `PlayerVitals` 구현**

`Assets/02.Scripts/Vitals/PlayerVitals.cs` (Task 8 스텁을 대체):

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using Survive.Core;

namespace Survive.Vitals
{
    /// <summary>
    /// 플레이어의 체력과 산소를 보유하고 매 프레임 갱신한다.
    /// 산소가 0이면 체력이 깎인다 (질식).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerVitals : MonoBehaviour
    {
        [SerializeField] VitalDefinitionSO healthDefinition;
        [SerializeField] VitalDefinitionSO oxygenDefinition;

        [Tooltip("산소가 0일 때 초당 깎이는 체력")]
        [SerializeField] float suffocationDamagePerSecond = 5f;

        readonly List<IOxygenModifier> _oxygenModifiers = new List<IOxygenModifier>();

        public Vital Health { get; private set; }
        public Vital Oxygen { get; private set; }

        public event Action Died;

        bool _죽음통보함;

        void Awake()
        {
            Health = 만들기(healthDefinition, 100f);
            Oxygen = 만들기(oxygenDefinition, 100f);
        }

        static Vital 만들기(VitalDefinitionSO def, float 기본최대)
        {
            if (def == null) return new Vital(기본최대, 기본최대);
            return new Vital(def.maxValue, def.startValue);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            Oxygen.Modify(CurrentOxygenRate() * dt);

            if (Oxygen.IsEmpty)
                Health.Modify(-suffocationDamagePerSecond * dt);
            else if (healthDefinition != null && healthDefinition.passiveRatePerSecond != 0f)
                Health.Modify(healthDefinition.passiveRatePerSecond * dt);

            if (Health.IsEmpty && !_죽음통보함)
            {
                _죽음통보함 = true;
                Died?.Invoke();
            }
        }

        public float CurrentOxygenRate()
        {
            float 기본 = oxygenDefinition != null ? oxygenDefinition.passiveRatePerSecond : -1f;
            return OxygenRate.Calculate(기본, _oxygenModifiers);
        }


        public void RegisterOxygenModifier(IOxygenModifier modifier)
        {
            if (modifier != null && !_oxygenModifiers.Contains(modifier))
                _oxygenModifiers.Add(modifier);
        }

        public void UnregisterOxygenModifier(IOxygenModifier modifier)
            => _oxygenModifiers.Remove(modifier);

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<PlayerVitals>();
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 47개 통과 (누적).

- [ ] **Step 6: 정의 에셋 2개 생성**

```bash
cd E:/SurviveProject/SurviveProject
mkdir -p Assets/08.Data/Vitals
uloop execute-dynamic-code --code 'using UnityEditor; using UnityEngine; AssetDatabase.Refresh(); var t=System.Type.GetType("Survive.Vitals.VitalDefinitionSO, Survive.Domain"); var h=ScriptableObject.CreateInstance(t); var so=new SerializedObject(h); so.FindProperty("id").stringValue="health"; so.FindProperty("displayName").stringValue="체력"; so.FindProperty("maxValue").floatValue=100f; so.FindProperty("startValue").floatValue=100f; so.FindProperty("passiveRatePerSecond").floatValue=0f; so.ApplyModifiedProperties(); AssetDatabase.CreateAsset(h,"Assets/08.Data/Vitals/Health.asset"); var o=ScriptableObject.CreateInstance(t); var so2=new SerializedObject(o); so2.FindProperty("id").stringValue="oxygen"; so2.FindProperty("displayName").stringValue="산소"; so2.FindProperty("maxValue").floatValue=100f; so2.FindProperty("startValue").floatValue=100f; so2.FindProperty("passiveRatePerSecond").floatValue=-1.5f; so2.ApplyModifiedProperties(); AssetDatabase.CreateAsset(o,"Assets/08.Data/Vitals/Oxygen.asset"); AssetDatabase.SaveAssets(); return "created";'
```

산소 기본 감소율 `-1.5`는 최대치 100을 약 67초에 소진한다. 계획 3의 실플레이에서 조정한다.

- [ ] **Step 7: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Vitals SurviveProject/Assets/08.Data SurviveProject/Assets/09.Tests
git commit -m "Vitals: PlayerVitals와 산소 보정 규칙 구현

보정이 겹치면 합산하지 않고 가장 유리한 값 하나만 채택한다."
```

---

## Task 10: PlayerInventory와 저장 인터페이스

**Files:**
- Create: `Assets/02.Scripts/Domain/Core/ISaveable.cs`
- Modify: `Assets/02.Scripts/Inventory/PlayerInventory.cs` (Task 8의 스텁을 채움)

**Interfaces:**
- Consumes: `Survive.Items.Inventory` (Task 5), `Survive.Items.ItemDatabaseSO` (Task 4)
- Produces:
  - `Survive.Domain.ISaveable` — 프로퍼티 `string SaveKey`, 메서드 `object CaptureState()`, `void RestoreState(object)`
  - `Survive.Items.PlayerInventory` — 프로퍼티 `Inventory Inventory`, `int ScrapCount`; 메서드 `int Add(ItemDataSO, int)`, `bool Remove(string, int)`

- [ ] **Step 1: `ISaveable` 구현**

`Assets/02.Scripts/Domain/Core/ISaveable.cs`:

```csharp
namespace Survive.Core
{
    /// <summary>
    /// 체크포인트 저장 대상. SaveService가 CaptureState의 반환값을 JSON으로 직렬화한다.
    /// </summary>
    public interface ISaveable
    {
        string SaveKey { get; }
        object CaptureState();
        void RestoreState(object state);
    }
}
```

- [ ] **Step 2: `PlayerInventory` 구현**

`Assets/02.Scripts/Inventory/PlayerInventory.cs` (Task 8 스텁을 대체):

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using Survive.Core;

namespace Survive.Items
{
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour, ISaveable
    {
        [Tooltip("CvsUI/PanelInven의 슬롯 개수와 맞춘다")]
        [SerializeField] int slotCount = 15;

        [SerializeField] ItemDatabaseSO database;

        public Inventory Inventory { get; private set; }

        public const string ScrapId = "scrap";
        public int ScrapCount => Inventory?.CountOf(ScrapId) ?? 0;

        void Awake() => Inventory = new Inventory(slotCount);

        void OnEnable() => GameServices.Register(this);
        void OnDisable() => GameServices.Unregister<PlayerInventory>();

        public int Add(ItemDataSO item, int count) => Inventory.TryAdd(item, count);
        public bool Remove(string itemId, int count) => Inventory.TryRemove(itemId, count);

        // ── 저장 ─────────────────────────────────────────────────

        [Serializable]
        public class 저장상태
        {
            public List<string> itemIds = new List<string>();
            public List<int> counts = new List<int>();
        }

        public string SaveKey => "player_inventory";

        public object CaptureState()
        {
            var s = new 저장상태();
            foreach (var slot in Inventory.Slots)
            {
                s.itemIds.Add(slot.IsEmpty ? "" : slot.item.id);
                s.counts.Add(slot.IsEmpty ? 0 : slot.count);
            }
            return s;
        }

        public void RestoreState(object state)
        {
            if (!(state is 저장상태 s)) return;
            if (database == null)
            {
                Debug.LogError("[PlayerInventory] database가 비어 있어 복원할 수 없습니다.", this);
                return;
            }

            Inventory = new Inventory(slotCount);
            int 개수 = Mathf.Min(s.itemIds.Count, Inventory.SlotCount);
            for (int i = 0; i < 개수; i++)
            {
                if (string.IsNullOrEmpty(s.itemIds[i])) continue;
                if (!database.TryGetById(s.itemIds[i], out var item)) continue;
                Inventory.Slots[i].item = item;
                Inventory.Slots[i].count = s.counts[i];
            }
        }
    }
}
```

- [ ] **Step 3: 컴파일 및 회귀 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 에러 0건, 47개 통과.

- [ ] **Step 4: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Core SurviveProject/Assets/02.Scripts/Inventory
git commit -m "Inventory: PlayerInventory와 ISaveable 추가"
```

---

## Task 11: 상호작용 시스템

**Files:**
- Create: `Assets/02.Scripts/Interaction/IInteractable.cs`
- Create: `Assets/02.Scripts/Interaction/PlayerInteractor.cs`
- Create: `Assets/02.Scripts/Interaction/ItemPickup.cs`
- Create: `Assets/02.Scripts/Interaction/LootContainer.cs`

**Interfaces:**
- Consumes: `Survive.Player.PlayerContext` (Task 8), `Survive.InputSystem.InputReaderSO` (Task 7), `Survive.Items.ItemDataSO` (Task 4)
- Produces:
  - `Survive.Interaction.IInteractable` — `string InteractionPrompt { get; }`, `bool CanInteract(PlayerContext)`, `void Interact(PlayerContext)`
  - `Survive.Interaction.IHoldInteractable : IInteractable` — `float HoldDuration { get; }`, `void OnHoldProgress(float)`, `void OnHoldCancelled()`
  - `Survive.Interaction.PlayerInteractor` — 프로퍼티 `IInteractable Current`; 이벤트 `event Action<string> PromptChanged`, `event Action<float> HoldProgressChanged`

- [ ] **Step 1: 인터페이스 정의**

`Assets/02.Scripts/Interaction/IInteractable.cs`:

```csharp
using Survive.Player;

namespace Survive.Interaction
{
    public interface IInteractable
    {
        /// <summary>화면에 띄울 문구. 예: "[E] 곡괭이 줍기"</summary>
        string InteractionPrompt { get; }

        bool CanInteract(PlayerContext player);
        void Interact(PlayerContext player);
    }

    /// <summary>채집처럼 일정 시간 누르고 있어야 하는 상호작용.</summary>
    public interface IHoldInteractable : IInteractable
    {
        float HoldDuration { get; }
        void OnHoldProgress(float normalized);
        void OnHoldCancelled();
    }
}
```

- [ ] **Step 2: `PlayerInteractor` 구현**

`Assets/02.Scripts/Interaction/PlayerInteractor.cs`:

```csharp
using System;
using UnityEngine;
using Survive.InputSystem;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>
    /// 카메라 전방을 훑어 상호작용 대상을 찾고 실행한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] Transform rayOrigin;      // 보통 카메라
        [SerializeField] float detectDistance = 3f;
        [SerializeField] float detectRadius = 0.3f;
        [SerializeField] LayerMask interactableMask = ~0;

        PlayerContext _player;

        public IInteractable Current { get; private set; }

        public event Action<string> PromptChanged;      // null이면 숨김
        public event Action<float> HoldProgressChanged;  // 0~1

        IHoldInteractable _누르는중;
        float _누른시간;
        string _마지막문구;

        void Awake()
        {
            _player = GetComponentInParent<PlayerContext>();
            if (rayOrigin == null && Camera.main != null) rayOrigin = Camera.main.transform;
        }

        void OnEnable()
        {
            if (input == null) return;
            input.InteractEvent += 상호작용시작;
            input.InteractCancelledEvent += 상호작용취소;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.InteractEvent -= 상호작용시작;
            input.InteractCancelledEvent -= 상호작용취소;
        }

        void Update()
        {
            대상갱신();
            누름진행();
        }

        void 대상갱신()
        {
            if (rayOrigin == null) return;

            IInteractable 찾은것 = null;
            if (Physics.SphereCast(rayOrigin.position, detectRadius, rayOrigin.forward,
                                   out var hit, detectDistance, interactableMask,
                                   QueryTriggerInteraction.Collide))
            {
                찾은것 = hit.collider.GetComponentInParent<IInteractable>();
            }

            if (!ReferenceEquals(찾은것, Current))
            {
                if (_누르는중 != null) 상호작용취소();
                Current = 찾은것;
            }

            string 문구 = null;
            if (Current != null && Current.CanInteract(_player))
                문구 = Current.InteractionPrompt;

            if (문구 != _마지막문구)
            {
                _마지막문구 = 문구;
                PromptChanged?.Invoke(문구);
            }
        }

        void 상호작용시작()
        {
            if (Current == null || !Current.CanInteract(_player)) return;

            if (Current is IHoldInteractable hold && hold.HoldDuration > 0f)
            {
                _누르는중 = hold;
                _누른시간 = 0f;
            }
            else
            {
                Current.Interact(_player);
            }
        }

        void 상호작용취소()
        {
            if (_누르는중 == null) return;
            _누르는중.OnHoldCancelled();
            _누르는중 = null;
            _누른시간 = 0f;
            HoldProgressChanged?.Invoke(0f);
        }

        void 누름진행()
        {
            if (_누르는중 == null) return;

            _누른시간 += Time.deltaTime;
            float 진행도 = Mathf.Clamp01(_누른시간 / _누르는중.HoldDuration);
            _누르는중.OnHoldProgress(진행도);
            HoldProgressChanged?.Invoke(진행도);

            if (진행도 >= 1f)
            {
                var 완료할것 = _누르는중;
                _누르는중 = null;
                _누른시간 = 0f;
                HoldProgressChanged?.Invoke(0f);
                완료할것.Interact(_player);
            }
        }
    }
}
```

- [ ] **Step 3: `ItemPickup` 구현**

`Assets/02.Scripts/Interaction/ItemPickup.cs`:

```csharp
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Items;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>바닥에 떨어져 있는 아이템.</summary>
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemDataSO item;
        [Min(1)] [SerializeField] int count = 1;

        [Tooltip("획득 성공 시 재생. 획득음·파티클")]
        [SerializeField] MMF_Player pickupFeedback;

        public string InteractionPrompt =>
            item == null ? "" : $"[E] {item.displayName} 줍기" + (count > 1 ? $" ×{count}" : "");

        public bool CanInteract(PlayerContext player) => item != null && player?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            int 남은수 = player.Inventory.Add(item, count);
            if (남은수 <= 0)
            {
                pickupFeedback?.PlayFeedbacks();
                Destroy(gameObject);
                return;
            }

            // 일부만 들어갔으면 남은 만큼만 남긴다
            if (남은수 != count) count = 남은수;
        }

        /// <summary>런타임 생성용 (전리품 드롭 등).</summary>
        public void Setup(ItemDataSO newItem, int newCount)
        {
            item = newItem;
            count = Mathf.Max(1, newCount);
        }
    }
}
```

- [ ] **Step 4: `LootContainer` 구현**

`Assets/02.Scripts/Interaction/LootContainer.cs`:

```csharp
using System;
using UnityEngine;
using Survive.Items;
using Survive.Player;

namespace Survive.Interaction
{
    /// <summary>우주선 잔해 상자처럼 한 번 열면 내용물을 전부 주는 대상.</summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        [Serializable]
        public class 내용물
        {
            public ItemDataSO item;
            [Min(1)] public int count = 1;
        }

        [SerializeField] string displayName = "잔해";
        [SerializeField] 내용물[] contents = new 내용물[0];

        bool _열림;

        public string InteractionPrompt => _열림 ? "" : $"[E] {displayName} 뒤지기";

        public bool CanInteract(PlayerContext player) => !_열림 && player?.Inventory != null;

        public void Interact(PlayerContext player)
        {
            if (_열림) return;
            _열림 = true;

            foreach (var c in contents)
            {
                if (c?.item == null) continue;
                int 남은수 = player.Inventory.Add(c.item, c.count);
                if (남은수 > 0)
                    Debug.LogWarning($"[LootContainer] 인벤토리가 가득 차 {c.item.displayName} {남은수}개를 넣지 못했습니다.", this);
            }
        }
    }
}
```

- [ ] **Step 5: 컴파일 및 회귀 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 에러 0건, 47개 통과.

- [ ] **Step 6: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Interaction
git commit -m "Interaction: 상호작용 계약과 탐지기, 줍기·상자 구현"
```

---

## Task 12: 전투 골격

**Files:**
- Create: `Assets/02.Scripts/Domain/Combat/DamageInfo.cs`
- Create: `Assets/02.Scripts/Domain/Combat/IDamageable.cs`
- Create: `Assets/02.Scripts/Combat/MeleeSwing.cs`
- Create: `Assets/02.Scripts/Combat/PlayerDamageReceiver.cs`

**Interfaces:**
- Consumes: `Survive.Player.PlayerToolHolder` (Task 13에서 채움 — 지금은 스텁), `Survive.Vitals.PlayerVitals` (Task 9), `Survive.InputSystem.InputReaderSO` (Task 7)
- Produces:
  - `Survive.Combat.DamageInfo` — 생성자 `DamageInfo(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal)`
  - `Survive.Combat.IDamageable` — `bool IsDead { get; }`, `void TakeDamage(in DamageInfo)`
  - `Survive.Combat.MeleeSwing` — 메서드 `void TrySwing()`

**주의:** `MeleeSwing`은 `PlayerToolHolder.EquippedTool`을 읽는다. Task 13에서 그 프로퍼티를 만들기 전이므로, **Task 13을 먼저 끝낸 뒤 이 태스크를 진행한다.** 순서를 바꿔도 무방하다.

- [ ] **Step 1: 피해 정보와 계약 정의**

`Assets/02.Scripts/Domain/Combat/DamageInfo.cs`:

```csharp
using UnityEngine;

namespace Survive.Combat
{
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitNormal;

        public DamageInfo(float amount, GameObject source, Vector3 hitPoint, Vector3 hitNormal)
        {
            Amount = amount;
            Source = source;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
        }
    }
}
```

`Assets/02.Scripts/Domain/Combat/IDamageable.cs`:

```csharp
namespace Survive.Combat
{
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(in DamageInfo info);
    }
}
```

- [ ] **Step 2: `MeleeSwing` 구현**

`Assets/02.Scripts/Combat/MeleeSwing.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.InputSystem;
using Survive.Player;

namespace Survive.Combat
{
    /// <summary>
    /// 장착한 도구로 전방 원뿔 안의 대상을 때린다.
    /// 도구가 없으면 발동하지 않는다.
    /// 타격감은 Feel에 위임한다 — 코드는 재생만 요청하고, 내용은 에디터에서 조립한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeleeSwing : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] PlayerToolHolder toolHolder;
        [SerializeField] Transform swingOrigin;         // 보통 카메라
        [SerializeField] LayerMask targetMask = ~0;

        [Tooltip("전방 판정 각도(도). 90이면 좌우 45도씩")]
        [SerializeField] float coneAngle = 90f;

        [Header("피드백")]
        [Tooltip("휘두를 때마다 재생. 도구 스윙음·모션")]
        [SerializeField] MMF_Player swingFeedback;

        [Tooltip("무언가에 맞았을 때만 재생. 화면 흔들림·히트스톱·타격음")]
        [SerializeField] MMF_Player hitFeedback;

        float _다음가능시각;
        readonly List<IDamageable> _이번에맞은것 = new List<IDamageable>();

        void Awake()
        {
            if (toolHolder == null) toolHolder = GetComponentInParent<PlayerToolHolder>();
            if (swingOrigin == null && Camera.main != null) swingOrigin = Camera.main.transform;
        }

        void OnEnable()
        {
            if (input != null) input.AttackEvent += TrySwing;
        }

        void OnDisable()
        {
            if (input != null) input.AttackEvent -= TrySwing;
        }

        public void TrySwing()
        {
            var tool = toolHolder != null ? toolHolder.EquippedTool : null;
            if (tool == null) return;                       // 맨손으로는 때리지 않는다
            if (Time.time < _다음가능시각) return;
            if (swingOrigin == null) return;

            _다음가능시각 = Time.time + tool.attackCooldown;
            _이번에맞은것.Clear();
            swingFeedback?.PlayFeedbacks();

            var 후보 = Physics.OverlapSphere(swingOrigin.position, tool.attackRange,
                                             targetMask, QueryTriggerInteraction.Collide);

            float 코사인한계 = Mathf.Cos(coneAngle * 0.5f * Mathf.Deg2Rad);

            foreach (var col in 후보)
            {
                var 대상 = col.GetComponentInParent<IDamageable>();
                if (대상 == null || 대상.IsDead) continue;
                if (_이번에맞은것.Contains(대상)) continue;        // 콜라이더 여러 개인 대상 중복 방지

                Vector3 방향 = (col.bounds.center - swingOrigin.position).normalized;
                if (Vector3.Dot(swingOrigin.forward, 방향) < 코사인한계) continue;

                _이번에맞은것.Add(대상);
                대상.TakeDamage(new DamageInfo(tool.damage, gameObject,
                                              col.ClosestPoint(swingOrigin.position), -방향));
            }

            if (_이번에맞은것.Count > 0) hitFeedback?.PlayFeedbacks();
        }
    }
}
```

- [ ] **Step 3: `PlayerDamageReceiver` 구현**

`Assets/02.Scripts/Combat/PlayerDamageReceiver.cs`:

```csharp
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Vitals;

namespace Survive.Combat
{
    [DisallowMultipleComponent]
    public class PlayerDamageReceiver : MonoBehaviour, IDamageable
    {
        [SerializeField] PlayerVitals vitals;

        [Tooltip("피격 시 재생. 붉은 비네트·진동·경고음")]
        [SerializeField] MMF_Player hurtFeedback;

        void Awake()
        {
            if (vitals == null) vitals = GetComponentInParent<PlayerVitals>();
        }

        public bool IsDead => vitals != null && vitals.Health.IsEmpty;

        public void TakeDamage(in DamageInfo info)
        {
            if (vitals == null || IsDead) return;
            vitals.Health.Modify(-info.Amount);
            hurtFeedback?.PlayFeedbacks();
        }
    }
}
```

- [ ] **Step 4: 컴파일 및 회귀 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 에러 0건, 47개 통과.

- [ ] **Step 5: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Combat
git commit -m "Combat: 피해 계약과 근접 공격, 플레이어 피격 구현

도구가 없으면 공격이 발동하지 않는다. 콜라이더가 여럿인 대상에
중복 피해가 들어가지 않게 막는다."
```

---

## Task 13: 플레이어 이동·시점·애니메이션·도구

기존 `PlayerController`를 네 조각으로 나눈다. 원본의 동작을 그대로 옮기되, 입력은 `InputReaderSO`에서 받는다.

**Files:**
- Modify: `Assets/02.Scripts/Player/PlayerLocomotion.cs` (Task 8 스텁을 채움)
- Modify: `Assets/02.Scripts/Player/PlayerCameraRig.cs` (동일)
- Modify: `Assets/02.Scripts/Player/PlayerToolHolder.cs` (동일)
- Create: `Assets/02.Scripts/Player/PlayerAnimatorDriver.cs`

**Interfaces:**
- Consumes: `Survive.InputSystem.InputReaderSO` (Task 7), `Survive.Items.ToolItemSO` (Task 4), `CameraShake` (기존)
- Produces:
  - `Survive.Player.PlayerLocomotion` — 프로퍼티 `bool IsGrounded`, `float CurrentSpeed`, `Vector3 PlanarVelocity`; 메서드 `void SetMovementLocked(bool)`
  - `Survive.Player.PlayerCameraRig` — 프로퍼티 `Transform CameraTransform`; 메서드 `void SetLookLocked(bool)`
  - `Survive.Player.PlayerToolHolder` — 프로퍼티 `ToolItemSO EquippedTool`; 메서드 `void Equip(ToolItemSO)`, `void Unequip()`; 이벤트 `event Action<ToolItemSO> ToolChanged`
  - `Survive.Player.PlayerAnimatorDriver` — 공개 API 없음 (다른 컴포넌트를 읽어 Animator를 갱신)

- [ ] **Step 1: `PlayerLocomotion` 구현**

원본 `PlayerController.CalcPlayerMove()`의 이동·점프·중력을 옮긴다. 원본은 `_characterController.Move()`를 두 번 호출했는데(수평 1회, 수직 1회), 한 번으로 합친다.

`Assets/02.Scripts/Player/PlayerLocomotion.cs`:

```csharp
using UnityEngine;
using Survive.InputSystem;

namespace Survive.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerLocomotion : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;

        [Header("이동")]
        [SerializeField] float walkSpeed = 5f;
        [SerializeField] float runSpeed = 7f;
        [SerializeField] float gravityScale = 1f;
        [SerializeField] float jumpPower = 2f;
        [SerializeField] float jumpCooldown = 1f;

        CharacterController _cc;
        Vector2 _입력 = Vector2.zero;
        float _수직속도;
        float _다음점프시각;
        bool _잠김;

        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public float CurrentSpeed { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }

        void Awake() => _cc = GetComponent<CharacterController>();

        void OnEnable()
        {
            if (input == null) return;
            input.MoveEvent += 이동입력;
            input.JumpEvent += 점프;
        }

        void OnDisable()
        {
            if (input == null) return;
            input.MoveEvent -= 이동입력;
            input.JumpEvent -= 점프;
        }

        void 이동입력(Vector2 v) => _입력 = v;

        void 점프()
        {
            if (_잠김) return;
            if (Time.time < _다음점프시각) return;
            _수직속도 = jumpPower;
            _다음점프시각 = Time.time + jumpCooldown;
        }

        public void SetMovementLocked(bool locked)
        {
            _잠김 = locked;
            if (locked) _입력 = Vector2.zero;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            Vector3 방향 = _잠김 ? Vector3.zero : new Vector3(_입력.x, 0f, _입력.y);
            if (방향.sqrMagnitude > 1f) 방향.Normalize();

            bool 달림 = input != null && input.IsSprinting;
            float 속도 = 달림 ? runSpeed : walkSpeed;

            Vector3 수평 = transform.TransformDirection(방향) * 속도;

            if (_cc.isGrounded && _수직속도 < 0f) _수직속도 = -1f;   // 지면에 붙여둔다
            _수직속도 += -9.81f * gravityScale * dt;

            Vector3 전체 = 수평 + Vector3.up * _수직속도;
            _cc.Move(전체 * dt);

            PlanarVelocity = 수평;
            CurrentSpeed = 수평.magnitude;
        }
    }
}
```

- [ ] **Step 2: `PlayerCameraRig` 구현**

원본 `UpdateCam()`을 옮긴다. 원본은 마우스 감도를 `/5f`로 나눴는데, Input System의 `<Mouse>/delta`는 스케일이 다르므로 `mouseSensitivity`를 직접 곱한다. 화면 흔들림은 기존 `CameraShake`에 위임한다.

`Assets/02.Scripts/Player/PlayerCameraRig.cs`:

```csharp
using UnityEngine;
using Survive.InputSystem;

namespace Survive.Player
{
    [DisallowMultipleComponent]
    public class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;
        [SerializeField] Transform cameraTransform;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float minPitch = -89f;
        [SerializeField] float maxPitch = 89f;

        float _yaw;
        float _pitch;
        bool _잠김;

        public Transform CameraTransform => cameraTransform;

        void Awake()
        {
            _yaw = transform.localEulerAngles.y;
            if (cameraTransform != null) _pitch = cameraTransform.localEulerAngles.x;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnEnable()
        {
            if (input != null) input.LookEvent += 시점입력;
        }

        void OnDisable()
        {
            if (input != null) input.LookEvent -= 시점입력;
        }

        Vector2 _시점 = Vector2.zero;
        void 시점입력(Vector2 v) => _시점 = v;

        public void SetLookLocked(bool locked)
        {
            _잠김 = locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
            if (locked) _시점 = Vector2.zero;
        }

        void LateUpdate()
        {
            if (_잠김) return;

            _yaw += _시점.x * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - _시점.y * mouseSensitivity, minPitch, maxPitch);

            var 몸각도 = transform.localEulerAngles;
            몸각도.y = _yaw;
            transform.localEulerAngles = 몸각도;

            if (cameraTransform != null)
            {
                var 카메라각도 = cameraTransform.localEulerAngles;
                카메라각도.x = _pitch;
                cameraTransform.localEulerAngles = 카메라각도;
            }
        }
    }
}
```

- [ ] **Step 3: `PlayerAnimatorDriver` 구현**

원본의 Animator 해시 코드를 옮긴다. 파라미터 이름(`HorizontalMove`, `VerticalMove`, `Speed`, `isMove`)은 기존 애니메이터 컨트롤러와 맞춰야 하므로 **바꾸지 않는다.**

`Assets/02.Scripts/Player/PlayerAnimatorDriver.cs`:

```csharp
using UnityEngine;

namespace Survive.Player
{
    [DisallowMultipleComponent]
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] PlayerLocomotion locomotion;
        [SerializeField] float walkSpeedReference = 5f;

        static readonly int 수평 = Animator.StringToHash("HorizontalMove");
        static readonly int 수직 = Animator.StringToHash("VerticalMove");
        static readonly int 속도 = Animator.StringToHash("Speed");
        static readonly int 이동중 = Animator.StringToHash("isMove");

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (locomotion == null) locomotion = GetComponent<PlayerLocomotion>();
        }

        void Update()
        {
            if (animator == null || locomotion == null) return;

            Vector3 지역속도 = transform.InverseTransformDirection(locomotion.PlanarVelocity);
            float 크기 = locomotion.CurrentSpeed;

            animator.SetFloat(수평, 크기 > 0.01f ? 지역속도.x / 크기 : 0f);
            animator.SetFloat(수직, 크기 > 0.01f ? 지역속도.z / 크기 : 0f);
            animator.SetFloat(속도, walkSpeedReference <= 0f ? 1f : 크기 / walkSpeedReference);
            animator.SetBool(이동중, 크기 > 0.1f);
        }
    }
}
```

- [ ] **Step 4: `PlayerToolHolder` 구현**

MainScene의 `Armature/…/Wrist_R/jointItemR` 아래에 `pickaxe01`, `hammer01`, `axe01`이 이미 붙어 있다. 이 자식들을 켜고 끄는 방식을 그대로 쓴다.

`Assets/02.Scripts/Player/PlayerToolHolder.cs`:

```csharp
using System;
using UnityEngine;
using Survive.Items;

namespace Survive.Player
{
    /// <summary>
    /// 손 소켓(jointItemR) 아래 도구 오브젝트를 켜고 끈다.
    /// 도구 모델은 이미 씬에 붙어 있으므로 생성하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerToolHolder : MonoBehaviour
    {
        [Tooltip("Armature/…/Wrist_R/jointItemR")]
        [SerializeField] Transform handSocket;

        public ToolItemSO EquippedTool { get; private set; }
        public event Action<ToolItemSO> ToolChanged;

        void Awake()
        {
            if (handSocket != null) 전부끄기();
        }

        public void Equip(ToolItemSO tool)
        {
            if (handSocket == null)
            {
                Debug.LogError("[PlayerToolHolder] handSocket이 지정되지 않았습니다.", this);
                return;
            }

            전부끄기();
            EquippedTool = tool;

            if (tool != null && !string.IsNullOrEmpty(tool.socketChildName))
            {
                var 자식 = handSocket.Find(tool.socketChildName);
                if (자식 != null) 자식.gameObject.SetActive(true);
                else Debug.LogWarning(
                    $"[PlayerToolHolder] 손 소켓에 '{tool.socketChildName}' 자식이 없습니다.", this);
            }

            ToolChanged?.Invoke(EquippedTool);
        }

        public void Unequip() => Equip(null);

        void 전부끄기()
        {
            for (int i = 0; i < handSocket.childCount; i++)
                handSocket.GetChild(i).gameObject.SetActive(false);
        }
    }
}
```

- [ ] **Step 5: 컴파일 및 회귀 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop compile --wait-for-domain-reload true
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 에러 0건, 47개 통과.

- [ ] **Step 6: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/02.Scripts/Player
git commit -m "Player: PlayerController를 이동·시점·애니메이션·도구로 분해

원본의 Move 2회 호출을 1회로 합치고, 지면에서 수직 속도를 -1로
유지해 경사면에서 떨리는 문제를 막는다."
```

---

## Task 14: Player 프리팹화

**이 태스크부터 씬과 프리팹을 만진다.** 앞선 태스크는 전부 코드만 다뤘다.

**Files:**
- Create: `Assets/05.Prefabs/Player.prefab`
- Modify: `Assets/01.Scenes/MainScene.unity`
- Modify: `Assets/01.Scenes/StartScene.unity`

**Interfaces:**
- Consumes: Task 8·9·10·11·12·13의 모든 컴포넌트
- Produces: `Assets/05.Prefabs/Player.prefab` — 두 씬이 공유하는 유일한 플레이어

**배경:** 현재 두 씬이 각각 `HumanMale_Character_FREE` 인스턴스를 들고 있고 이미 분기했다. MainScene은 손 소켓에 도구가 채워져 있고 StartScene은 비었으며, `Chest`/`Legs`/`Feet`의 레이어도 다르다(MainScene `Body`, StartScene `0`). **도구가 채워진 MainScene 버전을 기준으로 삼는다.**

- [ ] **Step 1: MainScene을 열고 현재 Player 상태 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/01.Scenes/MainScene.unity"); return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;'
uloop find-game-objects --name-pattern "jointItemR" --search-mode Contains --include-inactive true --max-results 3
```

기대 결과: `jointItemR` 아래에 `pickaxe01`, `hammer01`, `axe01`이 보인다.

- [ ] **Step 2: 낡은 `PlayerController` 컴포넌트 제거 후 새 컴포넌트 부착**

```bash
cd E:/SurviveProject/SurviveProject
mkdir -p "C:/Users/Coconut/AppData/Local/Temp/claude/scratch"
```

아래 내용을 `setup_player.csx`로 저장한 뒤 실행한다:

```csharp
using UnityEngine;
using UnityEditor;
using System.Linq;

var player = GameObject.Find("Player");
if (player == null) return "Player를 찾지 못했습니다";

// 낡은 컴포넌트 제거
var old = player.GetComponent("PlayerController");
if (old != null) Object.DestroyImmediate(old);

// 손 소켓 찾기
var socket = player.GetComponentsInChildren<Transform>(true)
                   .FirstOrDefault(t => t.name == "jointItemR");

// 카메라 리그 찾기 (CM vcam1)
var vcam = player.GetComponentsInChildren<Transform>(true)
                 .FirstOrDefault(t => t.name == "CM vcam1");

System.Func<string, Component> 붙이기 = (typeName) => {
    var t = System.Type.GetType(typeName + ", Assembly-CSharp");
    if (t == null) { Debug.LogError("타입 없음: " + typeName); return null; }
    var c = player.GetComponent(t);
    return c != null ? c : player.AddComponent(t);
};

붙이기("Survive.Player.PlayerContext");
붙이기("Survive.Player.PlayerLocomotion");
붙이기("Survive.Player.PlayerCameraRig");
붙이기("Survive.Player.PlayerAnimatorDriver");
붙이기("Survive.Player.PlayerToolHolder");
붙이기("Survive.Vitals.PlayerVitals");
붙이기("Survive.Items.PlayerInventory");
붙이기("Survive.Interaction.PlayerInteractor");
붙이기("Survive.Combat.MeleeSwing");
붙이기("Survive.Combat.PlayerDamageReceiver");

EditorUtility.SetDirty(player);
return "소켓=" + (socket ? socket.name : "없음") + " / vcam=" + (vcam ? vcam.name : "없음");
```

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code-file setup_player.csx
```

기대 결과: `소켓=jointItemR / vcam=CM vcam1`

- [ ] **Step 3: 직렬화 필드 배선**

`InputReaderSO`, 손 소켓, 카메라 트랜스폼, 생존자원 정의를 연결한다. `serialized_wire.csx`:

```csharp
using UnityEngine;
using UnityEditor;
using System.Linq;

var player = GameObject.Find("Player");
var input = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/08.Data/Input/InputReader.asset");
var health = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/08.Data/Vitals/Health.asset");
var oxygen = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/08.Data/Vitals/Oxygen.asset");

var socket = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "jointItemR");
var vcam   = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CM vcam1");

System.Action<string, string, Object> 넣기 = (typeName, field, value) => {
    var t = System.Type.GetType(typeName + ", Assembly-CSharp");
    var c = player.GetComponent(t);
    if (c == null) { Debug.LogError("컴포넌트 없음: " + typeName); return; }
    var so = new SerializedObject(c);
    var p = so.FindProperty(field);
    if (p == null) { Debug.LogError(typeName + "에 필드 없음: " + field); return; }
    p.objectReferenceValue = value;
    so.ApplyModifiedProperties();
};

넣기("Survive.Player.PlayerLocomotion",   "input", input);
넣기("Survive.Player.PlayerCameraRig",    "input", input);
넣기("Survive.Player.PlayerCameraRig",    "cameraTransform", vcam);
넣기("Survive.Player.PlayerToolHolder",   "handSocket", socket);
넣기("Survive.Vitals.PlayerVitals",       "healthDefinition", health);
넣기("Survive.Vitals.PlayerVitals",       "oxygenDefinition", oxygen);
넣기("Survive.Interaction.PlayerInteractor", "input", input);
넣기("Survive.Combat.MeleeSwing",         "input", input);

EditorUtility.SetDirty(player);
return "배선 완료";
```

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code-file serialized_wire.csx
```

기대 결과: `배선 완료`. 에러 로그가 나오면 필드 이름 철자를 해당 스크립트와 대조한다.

- [ ] **Step 4: 프리팹으로 저장**

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'using UnityEngine; using UnityEditor; var p=GameObject.Find("Player"); var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(p,"Assets/05.Prefabs/Player.prefab",InteractionMode.AutomatedAction); UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); return AssetDatabase.GetAssetPath(prefab);'
```

기대 결과: `Assets/05.Prefabs/Player.prefab`

- [ ] **Step 5: StartScene의 Player를 프리팹 인스턴스로 교체**

StartScene의 기존 Player는 위치·회전만 살리고 통째로 바꾼다.

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement; EditorSceneManager.OpenScene("Assets/01.Scenes/StartScene.unity"); var 옛것=GameObject.Find("Player"); var pos=옛것.transform.position; var rot=옛것.transform.rotation; Object.DestroyImmediate(옛것); var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05.Prefabs/Player.prefab"); var 새것=(GameObject)PrefabUtility.InstantiatePrefab(prefab); 새것.transform.SetPositionAndRotation(pos,rot); 새것.name="Player"; EditorSceneManager.SaveOpenScenes(); return "위치=" + pos;'
```

기대 결과: 원래 위치 `(29.346, 0, 28.84)` 근처가 반환된다.

- [ ] **Step 6: 두 씬의 Player가 같은 프리팹인지 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement; var 결과=""; foreach(var s in new[]{"Assets/01.Scenes/StartScene.unity","Assets/01.Scenes/MainScene.unity"}){ EditorSceneManager.OpenScene(s); var p=GameObject.Find("Player"); 결과 += s + " -> " + (p==null?"없음":(PrefabUtility.IsPartOfPrefabInstance(p)? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(p):"프리팹 아님")) + "\n"; } return 결과;'
```

기대 결과: 두 줄 모두 `Assets/05.Prefabs/Player.prefab`.

- [ ] **Step 7: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/05.Prefabs/Player.prefab* \
        SurviveProject/Assets/01.Scenes/StartScene.unity \
        SurviveProject/Assets/01.Scenes/MainScene.unity
git commit -m "씬 정규화: Player를 프리팹으로 승격하고 두 씬에 공유

두 씬이 각자 들고 있던 플레이어가 이미 분기해 있었다. 손 소켓에
도구가 채워진 MainScene 버전을 기준으로 삼는다."
```

---

## Task 15: UI 프리팹 통합과 HP 바 규격 통일

**Files:**
- Modify: `Assets/05.Prefabs/CvsUI.prefab`
- Modify: `Assets/01.Scenes/StartScene.unity`

**Interfaces:**
- Consumes: 없음
- Produces: `CvsUI.prefab` — 두 씬이 공유하는 유일한 UI

**배경:** StartScene은 프리팹이 아닌 맨 `Canvas`를 쓰고, MainScene은 `CvsUI` 프리팹을 쓴다. HP 바만 `GaugeBarPrefab` 인스턴스가 아니라 수작업 오브젝트라 자식 구성이 다르다(`LookImage`/`FillImage`/`Image (1)`/`Text`, Text에 `Outline` 없음). 나머지 셋은 `GaugeBG(Mask)`/`FillImage`/`FillEffectImage` 구성이다.

- [ ] **Step 1: 현재 HP 바와 Oxygen 바의 자식 구성 비교**

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement; EditorSceneManager.OpenScene("Assets/01.Scenes/MainScene.unity"); var 결과=""; foreach(var n in new[]{"HP","Oxygen"}){ var g=GameObject.Find("CvsUI/InfoBar/"+n); 결과 += n + ": "; if(g!=null) foreach(Transform c in g.transform) 결과 += c.name + ", "; 결과 += "\n"; } return 결과;'
```

기대 결과: `Oxygen`은 `GaugeBG`/`FillImage`/`FillEffectImage`/`Text`, `HP`는 다른 구성.

- [ ] **Step 2: HP를 `GaugeBarPrefab` 인스턴스로 교체**

`Oxygen`의 `ValueBarScript` 설정값(`fullColor`, `zeroColor`, `lerpTime`)을 참고해 HP용 색상만 바꾼다.

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code-file replace_hp.csx
```

`replace_hp.csx`:

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

EditorSceneManager.OpenScene("Assets/01.Scenes/MainScene.unity");

var infoBar = GameObject.Find("CvsUI/InfoBar");
if (infoBar == null) return "InfoBar를 찾지 못했습니다";

var 옛HP = infoBar.transform.Find("HP");
if (옛HP == null) return "HP를 찾지 못했습니다";

var rt = 옛HP as RectTransform;
var pos = rt.anchoredPosition;
var min = rt.anchorMin;
var max = rt.anchorMax;
var size = rt.sizeDelta;
int 순서 = 옛HP.GetSiblingIndex();

Object.DestroyImmediate(옛HP.gameObject);

var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05.Prefabs/GaugeBarPrefab.prefab");
var 새HP = (GameObject)PrefabUtility.InstantiatePrefab(prefab, infoBar.transform);
새HP.name = "HP";
새HP.transform.SetSiblingIndex(순서);

var nrt = 새HP.GetComponent<RectTransform>();
nrt.anchorMin = min;
nrt.anchorMax = max;
nrt.sizeDelta = size;
nrt.anchoredPosition = pos;

// 체력 색상: 가득 찼을 때 붉은색, 비었을 때 어두운 붉은색
var bar = 새HP.GetComponent("ValueBarScript");
var so = new SerializedObject(bar);
so.FindProperty("fullColor").colorValue = new Color(0.85f, 0.20f, 0.20f, 1f);
so.FindProperty("zeroColor").colorValue = new Color(0.35f, 0.05f, 0.05f, 1f);
so.ApplyModifiedProperties();

EditorSceneManager.SaveOpenScenes();
return "HP 바를 GaugeBarPrefab 규격으로 교체했습니다";
```

- [ ] **Step 3: 변경을 `CvsUI.prefab`에 반영**

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'using UnityEngine; using UnityEditor; var g=GameObject.Find("CvsUI"); PrefabUtility.ApplyPrefabInstance(g, InteractionMode.AutomatedAction); UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes(); return "프리팹에 반영";'
```

- [ ] **Step 4: StartScene의 맨 Canvas를 `CvsUI` 프리팹으로 교체**

StartScene의 `Canvas`에는 `InfoBar`와 `PanelDialog`가 있다. `CvsUI`에는 그 둘에 더해 `PanelInven`, `QuickSlot`이 있으므로 상위 호환이다.

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code-file swap_canvas.csx
```

`swap_canvas.csx`:

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

EditorSceneManager.OpenScene("Assets/01.Scenes/StartScene.unity");

var 옛Canvas = GameObject.Find("Canvas");
if (옛Canvas != null) Object.DestroyImmediate(옛Canvas);

var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05.Prefabs/CvsUI.prefab");
if (prefab == null) return "CvsUI.prefab을 찾지 못했습니다";

var 새것 = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
새것.name = "CvsUI";

EditorSceneManager.SaveOpenScenes();
return "StartScene의 Canvas를 CvsUI 프리팹으로 교체했습니다";
```

- [ ] **Step 5: 양 씬에서 UI가 같은 프리팹인지 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code 'using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement; var 결과=""; foreach(var s in new[]{"Assets/01.Scenes/StartScene.unity","Assets/01.Scenes/MainScene.unity"}){ EditorSceneManager.OpenScene(s); var g=GameObject.Find("CvsUI"); 결과 += s + " -> " + (g==null?"없음":PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)) + "\n"; } return 결과;'
```

기대 결과: 두 줄 모두 `Assets/05.Prefabs/CvsUI.prefab`.

- [ ] **Step 6: 커밋**

```bash
cd E:/SurviveProject
git add SurviveProject/Assets/05.Prefabs SurviveProject/Assets/01.Scenes
git commit -m "씬 정규화: UI를 CvsUI 프리팹으로 통합, HP 바 규격 통일

HP만 수작업 오브젝트라 자식 구성이 달랐다. GaugeBarPrefab 인스턴스로
바꿔 네 게이지의 구조를 같게 만든다."
```

---

## Task 16: 레거시 입력 제거와 씬 등록

**Files:**
- Delete: `Assets/02.Scripts/PlayerController.cs`
- Modify: `ProjectSettings/ProjectSettings.asset` (`activeInputHandler: 2` → `1`)
- Modify: `ProjectSettings/EditorBuildSettings.asset`
- Create: `Assets/02.Scripts/Domain/Core/SceneReferenceSO.cs`
- Create: `Assets/08.Data/Scenes/Scene_StartScene.asset`, `Assets/08.Data/Scenes/Scene_MainScene.asset`

**Interfaces:**
- Consumes: 없음
- Produces: `Survive.Domain.SceneReferenceSO` — 필드 `string sceneName`, `string displayName`

- [ ] **Step 1: `PlayerController.cs` 삭제**

Task 13에서 기능을 전부 옮겼고, Task 14에서 컴포넌트도 떼어냈다.

```bash
cd E:/SurviveProject/SurviveProject
rm Assets/02.Scripts/PlayerController.cs Assets/02.Scripts/PlayerController.cs.meta
uloop compile --force-recompile true --wait-for-domain-reload true
uloop get-logs --log-type Error --max-count 20
```

기대 결과: 에러 0건.

- [ ] **Step 2: 두 씬에 `PlayerController` 참조가 남아 있지 않은지 확인**

```bash
cd E:/SurviveProject/SurviveProject
grep -c "003db8ca5443ebf44ab649754f485910" Assets/01.Scenes/StartScene.unity Assets/01.Scenes/MainScene.unity Assets/05.Prefabs/Player.prefab
```

`003db8ca5443ebf44ab649754f485910`은 `PlayerController.cs`의 GUID다. 기대 결과: 세 파일 모두 `0`. 0이 아니면 해당 씬을 열어 깨진 컴포넌트를 제거한다.

- [ ] **Step 3: Input System 전용으로 전환**

`ProjectSettings/ProjectSettings.asset`에서 `activeInputHandler: 2`를 `activeInputHandler: 1`로 바꾼다. 앞서 확인한 대로 `Other Assets` 아래에 레거시 `Input`을 쓰는 스크립트가 없으므로 안전하다.

```bash
cd E:/SurviveProject/SurviveProject
uloop launch --restart true
uloop compile --wait-for-domain-reload true
uloop get-logs --log-type Error --max-count 20
```

기대 결과: 에러 0건.

- [ ] **Step 4: `SceneReferenceSO` 구현**

`Assets/02.Scripts/Domain/Core/SceneReferenceSO.cs`:

```csharp
using UnityEngine;

namespace Survive.Core
{
    /// <summary>
    /// 씬 이름을 문자열로 흩뿌리지 않기 위한 에셋.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Core/Scene Reference")]
    public class SceneReferenceSO : ScriptableObject
    {
        [Tooltip("Build Settings에 등록된 씬 이름")]
        public string sceneName;

        public string displayName;
    }
}
```

- [ ] **Step 5: 씬을 Build Settings에 등록하고 참조 에셋 생성**

```bash
cd E:/SurviveProject/SurviveProject
mkdir -p Assets/08.Data/Scenes
uloop execute-dynamic-code --code-file register_scenes.csx
```

`register_scenes.csx`:

```csharp
using UnityEngine;
using UnityEditor;

EditorBuildSettings.scenes = new[]
{
    new EditorBuildSettingsScene("Assets/01.Scenes/StartScene.unity", true),
    new EditorBuildSettingsScene("Assets/01.Scenes/MainScene.unity", true),
};

AssetDatabase.Refresh();

var t = System.Type.GetType("Survive.Core.SceneReferenceSO, Survive.Domain");

System.Action<string, string, string> 만들기 = (assetPath, sceneName, display) => {
    var so = ScriptableObject.CreateInstance(t);
    var s = new SerializedObject(so);
    s.FindProperty("sceneName").stringValue = sceneName;
    s.FindProperty("displayName").stringValue = display;
    s.ApplyModifiedProperties();
    AssetDatabase.CreateAsset(so, assetPath);
};

만들기("Assets/08.Data/Scenes/Scene_StartScene.asset", "StartScene", "프롤로그 — 화성 지표면");
만들기("Assets/08.Data/Scenes/Scene_MainScene.asset",  "MainScene",  "챕터 1 — 부유섬");

AssetDatabase.SaveAssets();
return "등록된 씬 수: " + EditorBuildSettings.scenes.Length;
```

기대 결과: `등록된 씬 수: 2`

- [ ] **Step 6: 전체 테스트 회귀 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop run-tests --test-mode EditMode --filter-type assembly --filter-value Survive.Tests.EditMode
```

기대 결과: 47개 통과.

- [ ] **Step 7: 커밋**

```bash
cd E:/SurviveProject
git add -A SurviveProject/Assets SurviveProject/ProjectSettings
git commit -m "레거시 입력 제거와 씬 등록

PlayerController를 삭제하고 activeInputHandler를 Input System
전용(1)으로 전환. Build Settings에 두 씬을 등록."
```

---

## Task 17: 부트스트랩과 실플레이 확인

계획 1의 마지막이다. 실제로 게임을 돌려 "움직이고, 숨이 차고, 아이템을 줍는" 것을 눈으로 확인한다.

**Files:**
- Create: `Assets/02.Scripts/Core/GameBootstrap.cs`
- Modify: `Assets/01.Scenes/StartScene.unity`

**Interfaces:**
- Consumes: `Survive.Domain.GameServices` (Task 2), `Survive.InputSystem.InputReaderSO` (Task 7)
- Produces: `Survive.Domain.GameBootstrap` — 씬의 진입점

- [ ] **Step 1: `GameBootstrap` 구현**

`Assets/02.Scripts/Core/GameBootstrap.cs`:

```csharp
using UnityEngine;
using Survive.InputSystem;

namespace Survive.Core
{
    /// <summary>
    /// 플레이 가능한 씬의 진입점. 서비스를 등록하고 입력을 켠다.
    /// 실행 순서를 앞당겨 다른 컴포넌트의 Awake보다 먼저 돈다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] InputReaderSO input;

        void Awake()
        {
            GameServices.Clear();

            if (input != null)
            {
                GameServices.Register(input);
                input.EnableGameplayInput();
            }
            else
            {
                Debug.LogError("[GameBootstrap] InputReader가 지정되지 않았습니다.", this);
            }
        }

        void OnDestroy() => GameServices.Clear();
    }
}
```

- [ ] **Step 2: 두 씬에 `GameBootstrap` 배치**

```bash
cd E:/SurviveProject/SurviveProject
uloop execute-dynamic-code --code-file add_bootstrap.csx
```

`add_bootstrap.csx`:

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

var t = System.Type.GetType("Survive.Core.GameBootstrap, Assembly-CSharp");
var input = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/08.Data/Input/InputReader.asset");
var 결과 = "";

foreach (var path in new[] { "Assets/01.Scenes/StartScene.unity", "Assets/01.Scenes/MainScene.unity" })
{
    EditorSceneManager.OpenScene(path);

    var go = GameObject.Find("GameBootstrap");
    if (go == null) go = new GameObject("GameBootstrap");

    var c = go.GetComponent(t);
    if (c == null) c = go.AddComponent(t);

    var so = new SerializedObject(c);
    so.FindProperty("input").objectReferenceValue = input;
    so.ApplyModifiedProperties();

    EditorSceneManager.SaveOpenScenes();
    결과 += path + " 완료\n";
}
return 결과;
```

- [ ] **Step 3: StartScene에 시험용 줍기 아이템 배치**

곡괭이 아이템 에셋을 만들고, 플레이어 앞에 줍을 수 있는 오브젝트를 둔다.

```bash
cd E:/SurviveProject/SurviveProject
mkdir -p Assets/08.Data/Items
uloop execute-dynamic-code --code-file make_pickaxe.csx
```

`make_pickaxe.csx`:

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

var toolType = System.Type.GetType("Survive.Items.ToolItemSO, Survive.Domain");
var 곡괭이 = ScriptableObject.CreateInstance(toolType);
var so = new SerializedObject(곡괭이);
so.FindProperty("id").stringValue = "pickaxe";
so.FindProperty("displayName").stringValue = "곡괭이";
so.FindProperty("description").stringValue = "광부의 기본 도구. 기계 잔해와 광맥을 캘 수 있다.";
so.FindProperty("maxStack").intValue = 1;
so.FindProperty("category").enumValueIndex = 1;      // Tool
so.FindProperty("toolType").enumValueIndex = 1;      // Pickaxe
so.FindProperty("tier").intValue = 1;
so.FindProperty("harvestPower").floatValue = 1f;
so.FindProperty("damage").floatValue = 12f;
so.FindProperty("attackRange").floatValue = 2.2f;
so.FindProperty("attackCooldown").floatValue = 0.6f;
so.FindProperty("socketChildName").stringValue = "pickaxe01";
so.ApplyModifiedProperties();
AssetDatabase.CreateAsset(곡괭이, "Assets/08.Data/Items/Pickaxe.asset");
AssetDatabase.SaveAssets();

// StartScene에 줍기 오브젝트 배치
EditorSceneManager.OpenScene("Assets/01.Scenes/StartScene.unity");
var player = GameObject.Find("Player");

var 줍기 = GameObject.CreatePrimitive(PrimitiveType.Cube);
줍기.name = "Pickup_Pickaxe";
줍기.transform.localScale = Vector3.one * 0.4f;
줍기.transform.position = player.transform.position + player.transform.forward * 2.5f + Vector3.up * 0.5f;

var pickupType = System.Type.GetType("Survive.Interaction.ItemPickup, Assembly-CSharp");
var pc = 줍기.AddComponent(pickupType);
var pso = new SerializedObject(pc);
pso.FindProperty("item").objectReferenceValue = 곡괭이;
pso.FindProperty("count").intValue = 1;
pso.ApplyModifiedProperties();

EditorSceneManager.SaveOpenScenes();
return "곡괭이 에셋과 줍기 오브젝트 배치 완료";
```

- [ ] **Step 4: 플레이 모드로 실제 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop clear-console
uloop control-play-mode --action play
```

플레이 후:

```bash
uloop screenshot --window-name "Game" --capture-mode rendering
uloop get-logs --log-type Error --max-count 20
```

확인 항목:
1. 에러 로그 0건
2. 화면에 게이지 바가 보인다
3. 시간이 지나면 산소 게이지가 줄어든다

- [ ] **Step 5: 입력 시뮬레이션으로 이동 확인**

```bash
cd E:/SurviveProject/SurviveProject
uloop simulate-keyboard --key w --action hold --duration 1500
uloop execute-dynamic-code --code 'using UnityEngine; var p=GameObject.Find("Player"); return "위치=" + p.transform.position;'
```

기대 결과: 앞서 Task 14에서 확인한 초기 위치 `(29.346, 0, 28.84)`에서 달라져 있다.

- [ ] **Step 6: 상호작용 확인**

플레이어를 `Pickup_Pickaxe` 쪽으로 이동시킨 뒤 `E`를 눌러 줍는다.

```bash
cd E:/SurviveProject/SurviveProject
uloop simulate-keyboard --key e --action press
uloop execute-dynamic-code --code 'using UnityEngine; var t=System.Type.GetType("Survive.Items.PlayerInventory, Assembly-CSharp"); var inv=Object.FindFirstObjectByType(t); var prop=t.GetProperty("Inventory"); var i=prop.GetValue(inv); var m=i.GetType().GetMethod("CountOf"); return "곡괭이 보유=" + m.Invoke(i, new object[]{"pickaxe"});'
```

기대 결과: `곡괭이 보유=1`

`0`이면 상호작용이 되지 않은 것이다. `PlayerInteractor`의 `rayOrigin`이 비어 있거나, 줍기 오브젝트의 레이어가 `interactableMask`에서 빠졌을 가능성이 크다.

- [ ] **Step 7: 플레이 모드 종료 후 커밋**

```bash
cd E:/SurviveProject/SurviveProject
uloop control-play-mode --action stop

cd E:/SurviveProject
git add -A SurviveProject/Assets
git commit -m "부트스트랩 추가와 실플레이 확인

두 씬에 GameBootstrap을 배치하고, 이동·산소 감소·아이템 줍기가
실제로 동작하는지 플레이 모드로 확인."
```

---

## 완료 기준

계획 1이 끝나면 다음이 성립한다.

- [ ] `uloop run-tests --test-mode EditMode`가 47개 통과
- [ ] `uloop compile`이 에러 0건
- [ ] 거동 계층에서 Feel(`MMF_Player`)·DOTween(`DOFade` 포함)·도메인 어셈블리가 모두 컴파일됨 (Task 1 Step 5)
- [ ] `Domain/` 안에 MonoBehaviour가 없고 Feel·DOTween `using`이 없음
- [ ] 두 씬의 Player가 `Assets/05.Prefabs/Player.prefab` 인스턴스
- [ ] 두 씬의 UI가 `Assets/05.Prefabs/CvsUI.prefab` 인스턴스
- [ ] `activeInputHandler: 1` (Input System 전용)
- [ ] Build Settings에 씬 2개 등록
- [ ] `PlayerController.cs`, `ItemInfo.cs`, `ItemListSO.cs`, `InventoryItem.cs` 삭제됨
- [ ] 플레이 모드에서 WASD 이동, 산소 감소, `E`로 아이템 줍기가 동작

## 다음 계획으로 넘기는 것

| 항목 | 계획 |
|---|---|
| 채집·제작·생물 AI·포탈·목표·HUD·인벤토리 UI | 계획 2 |
| `SceneFlowService`·`SaveService` 실제 구현 | 계획 2 |
| 자막 시스템·프롤로그 시퀀스·부유섬 레벨 배치 | 계획 3 |
| URP 컴포넌트 불일치 정리 | 계획 2 (Task 15에서 UI를 통합했으므로 렌더링 설정과 함께 처리) |
