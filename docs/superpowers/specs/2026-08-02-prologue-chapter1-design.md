# 생존게임 — 프롤로그 & 챕터 1「부유섬」시스템 설계

- 작성일: 2026-08-02
- 대상 프로젝트: `E:\SurviveProject\SurviveProject` (Unity 6000.5.6f1 / URP 17.5)
- 목적: 프롤로그와 챕터 1을 구현하기 위한 **기본 시스템 정의**. 에이전트가 병렬로 구현에 착수할 수 있는 수준까지 경계와 인터페이스를 확정한다.

---

## 1. 설계 원칙

이 문서의 산출물은 여러 에이전트가 동시에 구현한다. 따라서 시스템 경계는 **의존성이 한 방향으로만 흐르도록** 자른다.

1. **데이터는 ScriptableObject, 로직은 MonoBehaviour, 시스템 간 통신은 이벤트 채널(SO).**
   시스템끼리 직접 참조하지 않는다. 인벤토리를 구현하는 에이전트가 제작 시스템 코드를 몰라도 되어야 한다.
2. **모든 시스템은 세 줄로 설명 가능해야 한다** — 무엇을 하는가 / 어떻게 쓰는가 / 무엇에 의존하는가.
3. **파일 하나가 커지면 책임이 섞인 것이다.** 기존 `PlayerController`가 이동·점프·카메라·화면흔들림을 한꺼번에 들고 있는 것이 그 예이며, 이번에 분해한다.
4. **YAGNI.** 챕터 1을 완주시키지 못하는 기능은 넣지 않는다.

---

## 2. 현황 — 기존 자산 목록

재개하는 프로젝트이므로 무엇이 이미 있는지가 설계의 전제다.

### 2.1 스크립트 (`Assets/02.Scripts/`)

| 파일 | 상태 | 처리 |
|---|---|---|
| `PlayerController.cs` (121줄) | 이동·점프·마우스룩·카메라흔들림이 한 클래스에 뭉쳐 있음. 레거시 Input Manager | **분해 후 삭제** |
| `CameraShake.cs` (73줄) | 정상 동작 | **그대로 재사용** |
| `UI/ValueBarScript.cs` (101줄) | 게이지 바. 값 보간 + 색상 보간 | **그대로 재사용** |
| `Utill/EasingFunctions.cs` (1107줄) | 이징 함수 모음 | **그대로 재사용** |
| `Inventory/ItemInfo.cs` | 직렬화 클래스. `itemIdx`(int) 기반 | `ItemDataSO`로 승격 |
| `Inventory/ItemListSO.cs` | `ItemInfo[]`만 보유. 조회 기능 없음 | `ItemDatabaseSO`로 대체 |
| `Inventory/InventoryItem.cs` | `ItemInfo` + `curStack`만 | `ItemStack`으로 대체 |
| `Inventory/InventoryUI.cs` | **빈 클래스** | 신규 구현 |

인벤토리 폴더는 데이터 정의만 있고 **실제 로직이 전혀 없다.**

### 2.2 씬

**`StartScene.unity`** — 프롤로그 씬. 6 루트 / 291 오브젝트.

- `Enviorment/Terrain` + 바위 클러스터 7세트 (`rock01~08_m`) — 화성 지표면 협곡
- `Enviorment/DustStorm` **×4** (`ParticleSystem`) — 모래폭풍 연출 **이미 배치됨**
- `Player` — `Animator` + `CharacterController` + `PlayerController`, 자식 `CM vcam1`(`CinemachineVirtualCamera` + `CameraShake`)
- `Canvas/InfoBar` — `Water`/`Hunger`/`Oxygen`/`HP` 4바, **4개 모두 `ValueBarScript` 부착**
- `Canvas/PanelDialog` **[비활성]** — `VerticalLayoutGroup` + `ContentSizeFitter` + `Panel`×4 → `Text`.
  이것은 대화 패널이 아니라 **프롤로그 내레이션 자막**이며, 우주복 AI의 대사 4줄이 이미 작성돼 있다:
  1. `주의 : 행성에 현재 $#@%급 모래 폭풍 접근 중, 즉시 철수를 권고합니다.`
  2. `주의 : 현재 생명 유지 장치가 위험 상태입니다.`
  3. `파일럿의 생존을 위해 주변 탐색을 실시합니다...`
  4. `근처에 적절한 피난처 감지. 표시합니다...`
  폰트는 `ChosunGu`(한글 지원), 24pt. 레이아웃은 정상이다 — 가장 긴 1번 문장도 필요 너비 717px로 패널 800px 안에 들어가고 높이도 24px < 45px다.
- **우주선 잔해·조종석 등 추락 연출 오브젝트는 없다.**

**`MainScene.unity`** — 지하 맵. 8 루트 / 273 오브젝트. 원래 **부유섬 컨셉으로 제작하던 씬**이다.

- `Ground/Ground Terrain` + `Ground/Rocks` + `Ground/Water`(`WaterBlock_50m` ×9)
- `UndergroundWater`(`WaterBlock_50m`), `BackGround`(암벽 프리팹 다수)
- `Player` — StartScene과 같은 구성이나, `Armature/…/Wrist_R/jointItemR`에 **`pickaxe01`[비활성] / `hammer01`[비활성] / `axe01`[활성]** 이 부착됨. **도구 장착 소켓이 이미 작동하는 형태**
- `CvsUI` (프리팹) —
  - `PanelInven` : `GridLayoutGroup` + `ContentSizeFitter` + **슬롯 이미지 15개**
  - `QuickSlot` : **7칸**
  - `InfoBar` : StartScene과 동일한 4바
  - `PanelDialog` : **활성** (StartScene에선 비활성) — StartScene과 같은 프롤로그 자막 4줄. 대사는 `Assets/05.Prefabs/CvsUI.prefab`의 `Text` 컴포넌트에 하드코딩돼 있다

### 2.3 반드시 먼저 해결할 구조적 문제

1. **Player가 프리팹이 아니라 씬마다 복제되어 있고, 이미 분기했다.** MainScene은 도구 소켓이 채워져 있고 StartScene은 비었다. `Chest`/`Legs`/`Feet`의 레이어도 다르다(MainScene `Body`, StartScene `0`).
2. **UI도 같은 방식으로 중복.** 게이지 로직 수정이 두 씬에 두 번 필요하다.
3. **HP 바만 규격이 다르다.** Water/Hunger/Oxygen은 `GaugeBarPrefab` 인스턴스(`GaugeBG(Mask)`/`FillImage`/`FillEffectImage`)인데, HP는 프리팹이 아닌 수작업 오브젝트(`LookImage`/`FillImage`/`Image (1)`/`Text`, Text에 `Outline` 없음)다.
4. **URP 컴포넌트 불일치.** StartScene은 라이트에만 `UniversalAdditionalLightData`, MainScene은 카메라에만 `UniversalAdditionalCameraData`.
5. **Build Settings 씬 목록이 비어 있다** (`m_Scenes: []`).
6. **두 씬 어디에도 GameManager류 진입점이 없다.** 모든 로직이 플레이어와 UI 바에만 존재한다.
7. **자막이 고장나 있다.** 네 가지가 겹쳐 있다:
   - **재생 로직이 없다** — 4줄이 각각 고정 패널로 박혀 동시에 표시되는 정적 구조다. 순차 출력·타이핑·페이드·해제를 하는 스크립트가 프로젝트에 존재하지 않는다.
   - **표시 상태가 반대다** — 정작 프롤로그인 `StartScene`에선 `PanelDialog`가 비활성이라 자막이 나오지 않고, 챕터 1인 `MainScene`에선 활성이라 프롤로그 대사 4줄이 상시 떠 있다.
   - **대사가 프리팹에 하드코딩** — `CvsUI.prefab`의 `Text`에 직접 박혀 있어 순서 변경·추가가 불가능하다.
   - **가독성 미달** — 글자색 `RGB(0.196, 0.196, 0.196)` 짙은 회색에 배경 `RGBA(0.376, 0.639, 0.737, 0.392)` 반투명 하늘색. 배경을 불투명으로 가정해도 명암비 **4.56:1**로 WCAG AA(4.5:1)를 겨우 넘고, 실제로는 알파 0.39라 붉은 화성 지형이 비쳐 실효 대비는 더 낮다.

### 2.4 패키지

보유: URP 17.5, Cinemachine 2.10.7, AI Navigation 2.0.14, Timeline 1.8.12, uGUI 2.5.0, Test Framework 1.7.0, DOTween(Demigiant).
**추가 필요: `com.unity.inputsystem`.**

### 2.5 기획 문서 (`E:\SurviveProject\Plan\`)

- **세계관** — 2720년, 외곽에서 홀로 일하던 광부가 미사일에 격추되어 화성에 불시착. 지표는 모래폭풍뿐이라 동굴로 들어가나 바닥이 무너져 지하로 낙하. 지하는 테라포밍이 성공해 기계 생태계가 자리 잡았다. **스크랩**이 이 생태계의 연료이며 "별도 과정 없이 에너지로 대체될 수 있다". 흑막은 코어의 AI **MARSO**.
- **맵 디자인** — 부유섬(400~600m) → 얕은 평야(800~1200m) → 깊은 절벽(1200~2000m). 부유섬은 **매크로늄이 떠받치는 호수 위의 작은 섬**으로, **섬을 가로지르는 강 하나**와 **강 양 끝의 외계 구조물(포탈)** 이 있다. 서식 생물은 **소형 분해자·소형 생산자뿐**.
- **생물 도감** — 분해자 «눈», «공» / 생산자 «날개», «열매게», «하늘 가오리», «갉아먹는 자», «버섯게». 소비자급과 식물군은 미작성.

---

## 3. 확정된 설계 결정

| # | 결정 | 근거 |
|---|---|---|
| D1 | 챕터 1의 무대는 **부유섬(400~600m) 전체** | 기획서 맵 순서와 일치. 서식 생물이 소형 분해자·생산자뿐이라 전투 부담이 낮고, 강 + 양 끝의 포탈이라는 지형이 이미 목표 구조를 제공 |
| D2 | 플레이어 자원은 **체력 / 산소 / 스크랩** 3종 | 허기·갈증·체온은 잡무만 늘리고 탐사 압박에 기여하지 않음 |
| **D2-R** | **산소는 상시 자원이 아니다. 수중과 특수 필드(모래폭풍 등)에서만 소모된다** | **정정.** 세계관에 "지하는 테라포밍이 성공해, 새로운 생태계가 들어선"이라고 명시돼 있다. 식물군이 자라는 생태계라면 대기는 호흡 가능하다. 지하에서 산소를 관리하게 한 것은 오독이었다 |
| **D2-L** | **지하의 핵심 압박은 빛/어둠이다** | 세계관: "빛이 거의 공급되지않아 버섯들이 많아졌습니다", "천장에 박힌 버섯들이 큰 조명역할을 겸하면서". 랜턴 배터리가 스크랩을 소모하고, 발광 버섯 군락이 무료 충전 거점이 된다 |
| **D6-R** | **프롤로그는 불시착과 이동 두 가지만 다룬다** | 채집·줍기·도구 획득을 전부 뺀다. 도구는 챕터 1에서 제작한다 |
| D3 | **스크랩은 게이지가 아니라 인벤토리 자원 아이템**, HUD엔 카운터로 표시 | 세계관상 스크랩은 연료 겸 화폐 겸 제작재다. 게이지로 만들면 저장·제작에 쓸 수 없다. 압박은 "산소 필터와 랜턴이 스크랩을 소비"하는 방식으로 유지 |
| D4 | 전투는 **채집 도구 기반의 약한 전투** | 도구 소켓(`jointItemR`)이 이미 있고, 부유섬 생물은 소형뿐. 이후 챕터로 확장할 골격만 남긴다 |
| D5 | **Input System 도입 + 기존 코드 리팩터** | Unity 6 표준. 레거시 입력은 UI 입력과 섞일 때 문제가 생긴다 |
| D6 | 프롤로그는 **StartScene 확장**, **협곡에서 깨어나며 시작(A안)** | 조종석·우주선 오브젝트가 전혀 없어 신규 제작이 필요하다. 격추와 불시착은 암전 + 자막 시퀀스로 처리한다 |
| D7 | HUD 4바를 **HP=체력 / Oxygen=산소 / Water 슬롯→스크랩 카운터 / Hunger 제거**로 재배치 | 이미 만들어둔 바 UI와 `ValueBarScript`를 그대로 살린다 |
| D8 | 챕터 1 무대는 **MainScene을 부유섬으로 발전** | 원래 부유섬 컨셉으로 만들던 씬이고, 물+지형 구조가 이미 맞는다 |
| D9 | `PanelDialog`(세로 목록)를 **목표(Objective) 리스트 HUD**로 재활용 | `VerticalLayoutGroup` 구조가 목표 목록에 그대로 맞는다 |
| D10 | **씬 자산 정규화를 다른 모든 작업보다 먼저** 수행 | Player/UI 중복이 이미 분기했다. 정규화 없이는 에이전트 작업이 두 씬에서 계속 갈라진다 |

---

## 4. 시스템 정의

각 시스템은 **책임 / 공개 인터페이스 / 의존성** 순으로 정의한다. 구현 에이전트는 자기 시스템의 폴더 밖 `.cs`를 수정하지 않는다.

### 4.1 Core — `Assets/02.Scripts/Core/`

**책임**: 시스템 간 결합을 끊는 기반 장치. 게임 진입점.

```csharp
// 이벤트 채널 — 시스템 간 유일한 통신 수단
public abstract class EventChannelSO<T> : ScriptableObject {
    public event Action<T> OnRaised;
    public void Raise(T payload);
}
public class VoidEventChannelSO   : EventChannelSO<Unit> {}
public class IntEventChannelSO    : EventChannelSO<int> {}
public class FloatEventChannelSO  : EventChannelSO<float> {}
public class StringEventChannelSO : EventChannelSO<string> {}

// 서비스 레지스트리 — DI 프레임워크는 도입하지 않는다
public static class GameServices {
    public static void Register<T>(T service) where T : class;
    public static T Get<T>() where T : class;      // 미등록 시 예외
    public static bool TryGet<T>(out T service) where T : class;
    public static void Clear();                    // 씬 언로드 시
}

// 씬 진입점 — 각 플레이 가능 씬의 루트에 하나
public class GameBootstrap : MonoBehaviour {
    // Awake에서 서비스 등록, OnDestroy에서 해제. 실행 순서 -100.
}

// 씬 전환
public class SceneFlowService {
    public IEnumerator LoadScene(SceneReferenceSO target, float fadeSeconds);
    public event Action<string> SceneLoaded;
}

// 저장 — 체크포인트 방식, JSON, Application.persistentDataPath
public interface ISaveable {
    string SaveKey { get; }
    object CaptureState();
    void RestoreState(object state);
}
public class SaveService {
    public void Save(string slot);
    public bool Load(string slot);
    public bool HasSave(string slot);
}
```

**의존성**: 없음 (최하위 레이어).

### 4.2 Input — `Assets/02.Scripts/Input/`

**책임**: 물리적 입력을 의미 있는 이벤트로 변환. 다른 시스템은 `UnityEngine.Input`이나 `InputAction`을 직접 만지지 않는다.

`PlayerInputActions.inputactions` 액션 맵 2개:

| 맵 | 액션 |
|---|---|
| `Gameplay` | `Move`(Vector2), `Look`(Vector2), `Jump`, `Sprint`, `Interact`, `Attack`, `ToggleInventory`, `QuickSlot1~7`, `Pause` |
| `UI` | `Navigate`, `Submit`, `Cancel`, `Point`, `Click`, `Close` |

```csharp
[CreateAssetMenu(menuName = "Survive/Input/InputReader")]
public class InputReaderSO : ScriptableObject, PlayerInputActions.IGameplayActions,
                                               PlayerInputActions.IUIActions {
    public event Action<Vector2> MoveEvent;
    public event Action<Vector2> LookEvent;
    public event Action JumpEvent;
    public event Action<bool> SprintEvent;      // 눌림/뗌
    public event Action InteractEvent;
    public event Action AttackEvent;
    public event Action ToggleInventoryEvent;
    public event Action<int> QuickSlotEvent;    // 1~7
    public event Action PauseEvent;
    public event Action CancelEvent;

    public void EnableGameplayInput();
    public void EnableUIInput();
    public void DisableAllInput();              // 컷신용
}
```

**의존성**: 없음. `InputReaderSO` 에셋 하나를 두 씬이 공유하므로 씬 간 중복이 사라진다.

### 4.3 Player — `Assets/02.Scripts/Player/`

**책임**: 플레이어 아바타의 이동·시점·애니메이션·도구 장착. 기존 `PlayerController`를 네 조각으로 분해한다.

```csharp
public class PlayerLocomotion : MonoBehaviour {          // CharacterController 이동·점프·중력
    public bool IsGrounded { get; }
    public float CurrentSpeed { get; }
    public void SetMovementLocked(bool locked);          // 컷신·UI용
}

public class PlayerCameraRig : MonoBehaviour {           // 시점 회전 + CameraShake 연동
    public Transform CameraTransform { get; }
    public void SetLookLocked(bool locked);
}

public class PlayerAnimatorDriver : MonoBehaviour {}     // 기존 Animator 해시 코드 이관

public class PlayerToolHolder : MonoBehaviour {          // jointItemR 소켓 관리
    [SerializeField] Transform handSocket;               // Armature/…/Wrist_R/jointItemR
    public ToolItemSO EquippedTool { get; }
    public void Equip(ToolItemSO tool);                  // 해당 자식만 SetActive(true)
    public void Unequip();
    public event Action<ToolItemSO> ToolChanged;
}

public class PlayerContext : MonoBehaviour {             // 플레이어 하위 시스템 묶음 참조
    public PlayerLocomotion   Locomotion   { get; }
    public PlayerVitals       Vitals       { get; }
    public PlayerInventory    Inventory    { get; }
    public PlayerToolHolder   ToolHolder   { get; }
    public PlayerInteractor   Interactor   { get; }
    public Transform          Transform    { get; }
}
```

`PlayerToolHolder`는 소켓 자식(`pickaxe01`/`hammer01`/`axe01`)을 켜고 끄는 방식을 그대로 쓴다 — MainScene에 이미 그렇게 구성돼 있다.

**의존성**: Core, Input.
**기존 `PlayerController.cs`는 이 네 개로 옮긴 뒤 삭제한다.**

### 4.4 Vitals — `Assets/02.Scripts/Vitals/`

**책임**: 체력·산소의 값 관리와 상호작용. 챕터 1의 핵심 압박 장치.

```csharp
[CreateAssetMenu(menuName = "Survive/Vitals/VitalDefinition")]
public class VitalDefinitionSO : ScriptableObject {
    public string id;                 // "health" | "oxygen"
    public string displayName;
    public float  maxValue;
    public float  startValue;
    public float  passiveRatePerSecond;   // 산소는 음수, 체력은 0
}

public class Vital {                                    // 순수 클래스 — EditMode 테스트 대상
    public float Current { get; }
    public float Max { get; }
    public float Normalized { get; }
    public bool  IsEmpty { get; }
    public void  Modify(float delta);                   // 0~Max로 클램프
    public void  SetMax(float value);
    public event Action<float, float> Changed;          // (current, max)
}

public class PlayerVitals : MonoBehaviour, ISaveable {
    public Vital Health { get; }
    public Vital Oxygen { get; }
    public event Action Died;

    // 매 프레임: Oxygen.Modify((기본감소율 + 환경보정 + 장비보정) * dt)
    // Oxygen.IsEmpty 이면 Health.Modify(-질식데미지 * dt)
    public void RegisterOxygenModifier(IOxygenModifier m);
    public void UnregisterOxygenModifier(IOxygenModifier m);
}

public interface IOxygenModifier {
    float OxygenDeltaPerSecond { get; }   // 양수 = 회복, 음수 = 추가 소모
}
```

**겹침 규칙**: 여러 `IOxygenModifier`가 동시에 적용되면 **가장 유리한 값(최댓값) 하나만** 채택한다. 합산하지 않는다 — 버섯 군락 안에 있으면 모래폭풍이든 뭐든 안전하다는 규칙이 플레이어에게 읽히기 쉽다.

**의존성**: Core.

### 4.5 Inventory — `Assets/02.Scripts/Inventory/`

**책임**: 아이템 정의·보관·이동. 기존 껍데기를 대체한다.

```csharp
[CreateAssetMenu(menuName = "Survive/Items/Item")]
public class ItemDataSO : ScriptableObject {
    public string     id;              // "scrap", "pickaxe", "oxygen_filter" …
    public string     displayName;
    [TextArea] public string description;
    public Sprite     icon;
    public int        maxStack = 1;
    public ItemCategory category;      // Resource | Tool | Consumable | Quest
    public GameObject worldPrefab;     // 바닥에 떨어질 때
}

public class ToolItemSO : ItemDataSO {
    public ToolType toolType;          // Pickaxe | Hammer | Axe
    public int      tier;              // 채집 가능 등급
    public float    harvestPower;      // 채집 속도 배율
    public float    damage;
    public float    attackRange;
    public float    attackCooldown;
    public string   socketChildName;   // "pickaxe01" 등 — 손 소켓 자식 이름
}

public class ConsumableItemSO : ItemDataSO {
    public string targetVitalId;       // "oxygen" | "health"
    public float  instantAmount;
    public float  durationSeconds;     // 0이면 즉시
    public float  ratePerSecond;       // 지속형일 때
}

[CreateAssetMenu(menuName = "Survive/Items/ItemDatabase")]
public class ItemDatabaseSO : ScriptableObject {
    public ItemDataSO[] items;
    public ItemDataSO GetById(string id);
    public bool TryGetById(string id, out ItemDataSO item);
    // OnValidate에서 id 중복·공백을 에디터 에러로 보고한다
}

[Serializable]
public class ItemStack {
    public ItemDataSO item;
    public int count;
    public bool IsEmpty { get; }
}

public class Inventory {                    // 순수 클래스 — EditMode 테스트 대상
    public Inventory(int slotCount);        // 챕터 1은 15 (PanelInven 그리드와 일치)
    public int SlotCount { get; }
    public IReadOnlyList<ItemStack> Slots { get; }

    public int  TryAdd(ItemDataSO item, int count);   // 반환값 = 넣지 못한 수량
    public bool TryRemove(string itemId, int count);
    public int  CountOf(string itemId);
    public bool Has(string itemId, int count);
    public void MoveOrSwap(int fromSlot, int toSlot);
    public event Action Changed;
}

public class PlayerInventory : MonoBehaviour, ISaveable {
    public Inventory Inventory { get; }
    public int ScrapCount { get; }          // CountOf("scrap") 편의 접근자
}
```

**스택 규칙**: `TryAdd`는 같은 아이템의 기존 스택을 먼저 채우고, 남으면 빈 슬롯을 쓴다. 다 못 넣으면 남은 수량을 반환한다 (호출자가 바닥에 떨군다).

**의존성**: Core.

### 4.6 Interaction — `Assets/02.Scripts/Interaction/`

**책임**: 바라보는 대상과의 상호작용. 월드 오브젝트 종류에 상관없이 동일한 통로.

```csharp
public interface IInteractable {
    string InteractionPrompt { get; }              // "[E] 곡괭이 줍기"
    bool   CanInteract(PlayerContext player);
    void   Interact(PlayerContext player);
}

public interface IHoldInteractable : IInteractable {
    float HoldDuration { get; }                     // 채집처럼 누르고 있어야 하는 것
    void  OnHoldProgress(float normalized);
    void  OnHoldCancelled();
}

public class PlayerInteractor : MonoBehaviour {
    // 카메라 전방 SphereCast(반경 0.3, 거리 3.0, 레이어 Interactable)
    public IInteractable Current { get; }
    public event Action<string> PromptChanged;      // null이면 프롬프트 숨김
    public event Action<float>  HoldProgressChanged;
}
```

구현체: `ItemPickup`, `HarvestNode`, `LootContainer`, `CraftingBench`, `PortalDevice`.

**의존성**: Core, Input, Player.

### 4.7 Harvesting — `Assets/02.Scripts/Harvesting/`

**책임**: 채집 노드에서 자원을 얻는다.

```csharp
[CreateAssetMenu(menuName = "Survive/World/HarvestNode")]
public class HarvestNodeSO : ScriptableObject {
    public string       displayName;
    public ToolType     requiredTool;      // None이면 맨손 가능
    public int          requiredTier;
    public float        baseDuration;      // 실제 = base / tool.harvestPower
    public LootTableSO  drops;
    public float        respawnSeconds;    // 0이면 재생성 없음
}

[CreateAssetMenu(menuName = "Survive/World/LootTable")]
public class LootTableSO : ScriptableObject {
    [Serializable] public class Entry {
        public ItemDataSO item;
        public int minCount, maxCount;
        [Range(0f,1f)] public float chance;
    }
    public Entry[] entries;
    public List<ItemStack> Roll(System.Random rng);   // rng 주입 — 테스트 가능하게
}

public class HarvestNode : MonoBehaviour, IHoldInteractable {
    // 도구 요건 미달 시 CanInteract=false, 프롬프트에 필요 도구를 표시
    // 완료 → drops.Roll() → 인벤토리 투입 → 비활성 → respawn 타이머
}
```

**의존성**: Core, Inventory, Interaction.

### 4.8 Combat — `Assets/02.Scripts/Combat/`

**책임**: 피해를 주고받는 최소 골격. 플레이어와 생물이 같은 인터페이스를 쓴다.

```csharp
public readonly struct DamageInfo {
    public readonly float      Amount;
    public readonly GameObject Source;
    public readonly Vector3    HitPoint;
    public readonly Vector3    HitNormal;
}

public interface IDamageable {
    bool IsDead { get; }
    void TakeDamage(in DamageInfo info);
}

public class MeleeSwing : MonoBehaviour {
    // PlayerToolHolder.EquippedTool에서 damage/range/cooldown을 읽는다.
    // 전방 원뿔(OverlapSphere + 각도 판정) 안의 IDamageable 전부에 1회 적용.
    // 도구가 없으면 발동하지 않는다.
}

public class CreatureHealth : MonoBehaviour, IDamageable {
    public event Action<CreatureHealth> Died;    // 사망 시 LootTable 드롭
}

public class PlayerDamageReceiver : MonoBehaviour, IDamageable {
    // PlayerVitals.Health를 깎는다
}
```

**의존성**: Core, Player(ToolHolder), Vitals.

### 4.9 Creatures — `Assets/02.Scripts/Creatures/`

**책임**: 생물의 정의와 행동. 챕터 1은 부유섬 서식종 **4종만** 구현한다 — 기획서상 부유섬에는 소형 분해자·소형 생산자밖에 없다.

```csharp
[CreateAssetMenu(menuName = "Survive/Creatures/CreatureDefinition")]
public class CreatureDefinitionSO : ScriptableObject {
    public string          id;
    public string          displayName;
    public TrophicTier     tier;             // Decomposer | Producer | Consumer1 | Consumer2 | Consumer3
    public LocomotionType  locomotion;       // Ground | Flying
    public BehaviorProfile behavior;         // Passive | Skittish | Defensive | Aggressive
    public float           maxHealth;
    public float           moveSpeed;
    public float           detectRadius;
    public float           attackDamage;
    public float           attackRange;
    public float           attackCooldown;
    public LootTableSO     drops;

    [Header("도감 — 챕터 2에서 사용")]
    [TextArea] public string codexDescription;
    public Sprite            codexSketch;
}

public class CreatureBrain : MonoBehaviour {
    // 상태: Idle → Wander → (Flee | Chase → Attack) → Dead
    // Ground = NavMeshAgent (AI Navigation 패키지 보유)
    // Flying = FlyerMotor (단순 스티어링 + 고도 유지)
}
```

**행동 프로필 정의**

| 프로필 | 감지 시 | 피격 시 |
|---|---|---|
| `Passive` | 무시 | 무시 |
| `Skittish` | 도주 | 도주 |
| `Defensive` | 무시 | 일정 시간 반격 후 이탈 |
| `Aggressive` | 추격 | 추격 |

**챕터 1 등장 4종** (전부 도감에 이미 있는 종)

| 종 | 등급 | 이동 | 프로필 | 비고 |
|---|---|---|---|---|
| **눈** | 분해자 | 비행 | Skittish | 도감: 부양 드론, 전방의 큰 눈이 탐지기관. 스크랩 드롭 |
| **공** | 분해자 | 지상 | Defensive | 도감: 구형, 굴러다님. 스크랩 + 부품 |
| **날개** | 생산자 | 비행 | Skittish | 도감: 잠자리형, 동굴 서식 |
| **열매게** | 생산자 | 지상 | Defensive | 도감: 다리 4개, **강가 서식** — 강 건너기 구간에 배치 |

**의존성**: Core, Combat, Harvesting(LootTable).

### 4.10 Crafting — `Assets/02.Scripts/Crafting/`

**책임**: 재료를 소비해 아이템을 만든다.

```csharp
[CreateAssetMenu(menuName = "Survive/Crafting/Recipe")]
public class RecipeSO : ScriptableObject {
    public string        id;
    public ItemStack[]   ingredients;
    public ItemStack     result;
    public float         craftSeconds;
    public StationType   requiredStation;   // None(휴대) | Bench
}

[CreateAssetMenu(menuName = "Survive/Crafting/RecipeBook")]
public class RecipeBookSO : ScriptableObject { public RecipeSO[] recipes; }

public static class CraftingService {          // 순수 로직 — EditMode 테스트 대상
    public static bool CanCraft(RecipeSO recipe, Inventory inv, StationType available);
    public static bool Craft(RecipeSO recipe, Inventory inv, StationType available);
}
```

**의존성**: Core, Inventory.

### 4.11 World — `Assets/02.Scripts/World/`

**책임**: 환경이 플레이어에게 거는 효과와 월드 장치.

```csharp
public class OxygenZone : MonoBehaviour, IOxygenModifier {
    [SerializeField] float oxygenDeltaPerSecond;   // 버섯 군락: 양수
    // Trigger Enter/Exit로 PlayerVitals에 자신을 등록/해제
}

public class HazardZone : MonoBehaviour, IOxygenModifier {
    [SerializeField] float oxygenDeltaPerSecond;   // 모래폭풍: 음수
    // StartScene의 DustStorm 4개에 부착한다
}

public class PortalDevice : MonoBehaviour, IInteractable {
    [SerializeField] ItemStack[]      requiredItems;
    [SerializeField] SceneReferenceSO destination;
    // 요건 미충족 시 프롬프트에 부족한 품목을 표시
    // 충족 시 아이템 소비 → 활성 연출 → SceneFlowService로 전환
}

public class Checkpoint : MonoBehaviour {              // 리스폰 지점
    // Trigger 진입 시 SaveService에 현재 상태 저장
}
```

**의존성**: Core, Vitals, Inventory, Interaction.

### 4.12 Progression — `Assets/02.Scripts/Progression/`

**책임**: 챕터 목표의 진행 추적. 시나리오를 코드가 아니라 에셋으로 표현한다.

```csharp
public abstract class ObjectiveSO : ScriptableObject {
    public string id;
    [TextArea] public string displayText;
    public abstract void Bind(ChapterDirector director);   // 필요한 이벤트 구독
    public abstract void Unbind();
    public abstract bool IsComplete { get; }
    public abstract float Progress { get; }                // 0~1, HUD 표시용
    public event Action Completed;
}

// 구현체
public class CollectItemObjective   : ObjectiveSO {}   // 아이템 N개 보유
public class ReachZoneObjective     : ObjectiveSO {}   // 지정 트리거 도달
public class InteractObjective      : ObjectiveSO {}   // 지정 대상 상호작용
public class KillCreatureObjective  : ObjectiveSO {}   // 지정 종 N마리 처치
public class CraftItemObjective     : ObjectiveSO {}   // 지정 레시피 제작

[CreateAssetMenu(menuName = "Survive/Progression/Chapter")]
public class ChapterSO : ScriptableObject {
    public string        id;
    public string        title;
    public ObjectiveSO[] objectives;      // 순차 진행
}

public class ChapterDirector : MonoBehaviour, ISaveable {
    public ObjectiveSO Current { get; }
    public int  CurrentIndex { get; }
    public event Action<ObjectiveSO> ObjectiveChanged;
    public event Action<ChapterSO>   ChapterCompleted;
    public void ForceCompleteCurrent();    // PlayMode 스모크 테스트용
}
```

**의존성**: Core, Inventory, Combat, Crafting, World.

### 4.13 Narrative — `Assets/02.Scripts/Narrative/`

**책임**: 자막과 연출 시퀀스. 프롤로그가 이 시스템에 의존한다.

```csharp
[CreateAssetMenu(menuName = "Survive/Narrative/Sequence")]
public class SequenceSO : ScriptableObject {
    [Serializable] public class Line {
        public string speaker;
        [TextArea] public string text;
        public float holdSeconds;
    }
    public Line[]           lines;
    public PlayableAsset    timeline;      // 선택 — 없으면 자막만
    public bool             lockInput = true;
    public float            fadeInSeconds, fadeOutSeconds;
}

public class SequenceDirector : MonoBehaviour {
    public IEnumerator Play(SequenceSO sequence);   // 입력잠금 → 재생 → 해제
    public event Action<SequenceSO> SequenceFinished;
}

public class ScreenFader : MonoBehaviour {          // CanvasGroup + DOTween
    public Tween FadeIn(float seconds);             // 화면이 밝아짐 (alpha 1 → 0)
    public Tween FadeOut(float seconds);            // 화면이 어두워짐 (alpha 0 → 1)
}
```

#### 기존 자막 수리 (2.3-7의 해소)

이 시스템이 곧 자막 고장의 수리다. 별도 임시 수정을 하지 않고 여기서 한 번에 처리한다.

| 현행 문제 | 조치 |
|---|---|
| 재생 로직 없음 | `SubtitleView` + `SequenceDirector`가 한 줄씩 순차 출력하고 `holdSeconds` 후 다음 줄로 넘어간다. 패널 4개를 동시에 띄우지 않는다 |
| 대사가 프리팹에 하드코딩 | 기존 4줄을 `Assets/08.Data/Sequences/Seq_Prologue_Intro.asset`(`SequenceSO`)으로 이관한다. **문구는 그대로 보존한다** — 이미 프롤로그 흐름과 맞는다 |
| 표시 상태가 반대 | `CvsUI.prefab`의 `PanelDialog`를 **기본 비활성**으로 바꾼다. 표시 여부는 `SequenceDirector`가 런타임에 결정한다 |
| 가독성 미달 | 글자색을 흰색 계열로, 배경 패널을 어두운 색 + 알파 상향으로 조정해 명암비 7:1 이상을 확보한다 |
| 패널 4개 구조 | `Panel (1)~(4)`를 **한 줄짜리 `SubtitleView` 하나**로 정리한다. 여러 줄 동시 표시가 필요 없다 |

`ChosunGu` 폰트는 한글을 지원하므로 유지한다. 다만 게이지 숫자에 쓰인 `LegacyRuntime` 폰트는 한글을 렌더링하지 못하므로, 해당 Text에 한글을 넣을 일이 생기면 `ChosunGu`로 교체해야 한다.

**의존성**: Core, Input(잠금), UI(자막 표시).

### 4.14 UI — `Assets/02.Scripts/UI/`

**책임**: 화면 표시 전반. 기존 `ValueBarScript`와 씬의 UI 레이아웃을 최대한 재활용한다.

```csharp
public class HUDController : MonoBehaviour {
    // 게이지 2종 + 스크랩 카운터 + 상호작용 프롬프트 + 목표 목록 + 크로스헤어
}

public class VitalBarView : MonoBehaviour {
    [SerializeField] ValueBarScript bar;      // 기존 스크립트를 감싼다
    [SerializeField] string vitalId;          // "health" | "oxygen"
}

public class ScrapCounterView : MonoBehaviour {}   // 기존 Water 슬롯 자리를 재사용
public class InteractionPromptView : MonoBehaviour {}
public class ObjectiveListView : MonoBehaviour {}  // 기존 PanelDialog 재사용
public class InventoryUI : MonoBehaviour {}        // 기존 PanelInven 그리드(15칸)에 바인딩
public class QuickSlotUI : MonoBehaviour {}        // 기존 QuickSlot(7칸)
public class CraftingUI : MonoBehaviour {}
public class SubtitleView : MonoBehaviour {}
public class PauseMenu : MonoBehaviour {}

public class UIStateService {                      // 어떤 패널이 열렸는지 관리
    public bool AnyPanelOpen { get; }
    public void Open(UIPanel panel);   // 열리면 InputReader를 UI 맵으로 전환
    public void CloseAll();
}
```

**주의**: `ValueBarScript`의 현행 `Update()`는 매 프레임 `RefreshColor()`를 호출한다. 값이 바뀔 때만 호출하도록 정리하되, **공개 시그니처는 유지**한다 (씬 참조가 걸려 있다).

**의존성**: Core, Input, Vitals, Inventory, Crafting, Progression, Narrative.

---

## 4.15 연출 자산 활용 방침 — Feel · Cinemachine · DOTween

세 자산을 적극적으로 쓴다. 다만 **거동 계층(`Assembly-CSharp`)에서만** 쓴다 — 도메인 계층은 이들을 모른다(10장 규칙 5).

### 역할 분담

| 자산 | 담당 | 쓰지 않을 곳 |
|---|---|---|
| **Feel (`MMF_Player`)** | 순간적인 게임 필 — 타격감, 획득감, 경고. 화면 흔들림·플래시·진동·시간 정지·파티클·사운드를 한 덩어리로 묶는다 | 지속적인 상태 표현(게이지 채우기 등) |
| **Cinemachine** | 카메라 전반 — 1인칭 리그, 임펄스(피격 반동), 프롤로그 컷신의 카메라 이동 | UI |
| **DOTween** | UI 전환과 값 보간 — 패널 열림/닫힘, 자막 페이드, 게이지 보간, 화면 암전 | 3D 카메라 흔들림(Cinemachine Impulse가 담당) |

### 챕터 1에서 붙일 피드백 지점

`MMF_Player`를 직렬화 필드로 노출해 디자이너가 에디터에서 조립하게 한다. 코드는 `player?.PlayFeedbacks()`만 호출한다.

| 지점 | 담당 컴포넌트 | 성격 |
|---|---|---|
| 근접 타격 명중 | `MeleeSwing` | 화면 흔들림(Impulse) + 히트스톱 + 타격음 |
| 생물 피격·사망 | `CreatureHealth` | 플래시 + 파편 파티클 + 사망음 |
| 플레이어 피격 | `PlayerDamageReceiver` | 화면 붉은 비네트 + 진동 |
| 아이템 획득 | `ItemPickup`, `HarvestNode` | 획득음 + UI 슬롯 펄스(DOTween) |
| 채집 완료 | `HarvestNode` | 파편 파티클 + 도구 임팩트 |
| **산소 위험(20% 이하)** | `PlayerVitals` 구독 UI | 반복 경고음 + 화면 가장자리 맥동. 챕터 1의 핵심 압박이므로 가장 공들인다 |
| 제작 완료 | `CraftingUI` | 완료음 + 결과 슬롯 강조 |
| 포탈 기동 | `PortalDevice` | 카메라 임펄스 + 발광 상승 + 저음 |

### 기존 `CameraShake`의 처지

`CameraShake.cs`는 Cinemachine 가상 카메라를 직접 흔드는 수작업 구현이다. 씬과 프리팹에서 참조 중이므로 **삭제하지 않는다.** 다만 신규 타격감은 Feel + Cinemachine Impulse로 만들고, `CameraShake`는 기존 이동 흔들림 용도로만 남긴다. 두 경로가 같은 가상 카메라를 동시에 흔들면 결과가 겹치므로, **한 시점에 한쪽만 동작하게** 한다.

---

## 5. 씬 구성과 플로우

```
StartScene (프롤로그, 화성 지표면)
    └─ 낙하 연출 → SceneFlowService
        └─ MainScene (챕터 1, 부유섬)
            └─ PortalDevice 기동 → 챕터 종료 (평야는 챕터 2)
```

Build Settings에 `StartScene`(0), `MainScene`(1)을 등록한다.

`MainScene`은 부유섬 컨셉으로 만들던 씬이므로 그대로 발전시킨다. 필요한 배치 작업:
- 섬을 가로지르는 **강** 정비 (`Ground/Water` 활용)
- **강 양 끝에 외계 구조물(포탈) 2기** — 하나는 도착 지점, 하나는 하강용
- **발광 버섯 군락 = `OxygenZone`** 3~4곳 (거점 역할)
- 채집 노드 배치, 생물 스폰 지점 배치
- NavMesh 베이크

---

## 6. 프롤로그 시나리오 (A안 — StartScene 확장)

각 단계가 시스템을 하나씩 소개하는 튜토리얼이다.

| # | 장면 | 소개 시스템 | 자막 | 필요 작업 |
|---|---|---|---|---|
| 1 | 암전 상태로 시작. 경보음, 격추와 불시착을 자막으로 서술 | Narrative | 신규 2~3줄 | `SequenceSO` 작성 |
| 2 | 페이드인 → 협곡 바닥에서 깨어남. 이동/시점 조작 안내 | Input, Player | **기존 ②** 생명 유지 장치 위험 | 기존 씬 그대로 |
| 3 | 근처 잔해에서 **곡괭이** 회수 | Interaction, Inventory | **기존 ③** 주변 탐색을 실시합니다 | `ItemPickup` 배치 |
| 4 | 협곡 밖으로 나가면 **모래폭풍** — 산소 급감, 시야 제한 | **Vitals** | **기존 ①** 모래 폭풍 접근 중, 철수 권고 | 기존 `DustStorm` 4개에 `HazardZone` 부착 |
| 5 | 폭풍 속에서 동굴 입구 탐색 | Vitals 압박 | **기존 ④** 피난처 감지. 표시합니다 | 동굴 입구 프롭 배치 |
| 6 | 동굴 진입 → 폭풍 이탈, 산소 회복 | `OxygenZone` | 신규 1줄 | 동굴 내부에 `OxygenZone` |
| 7 | 동굴 심부 → 바닥 붕괴 → 낙하 → 암전 | Narrative, SceneFlow | 신규 1줄 | 트리거 + 연출 |

기존에 작성된 자막 4줄은 **문구를 바꾸지 않고** 위 순서로 재배치한다. 원래 패널 순서(①②③④)와 실제 극 전개 순서가 다를 뿐, 내용은 이미 A안 흐름과 맞는다.

**신규 제작이 필요한 것**: 잔해 프롭 1점, 동굴 입구/내부 구간, 붕괴 트리거. 조종석·우주선 세트는 **만들지 않는다**(1단계를 자막으로 처리).

---

## 7. 챕터 1 시나리오 — 부유섬 (목표 6단계)

| # | 목표 | 목표 타입 | 게이팅하는 시스템 |
|---|---|---|---|
| 1 | 착지 지점을 벗어나 주변을 살핀다 | `FlagObjective` | Locomotion |
| 2 | **발광 버섯 군락**을 찾는다 | `FlagObjective` | `LightZone` — 밝은 거점 개념 학습 |
| 3 | 흩어진 잔해에서 **스크랩 10개**를 모은다 | `CollectItemObjective` | Harvesting (맨손 채집) |
| 4 | **곡괭이를 제작**한다 | `CollectItemObjective` | Crafting — 이후 단단한 노드를 캘 수 있다 |
| 5 | **랜턴을 제작**해 어둠에 대비한다 | `CollectItemObjective` | Crafting + 빛 압박 해소 수단 획득 |
| 6 | **포탈을 기동**한다 | `FlagObjective` | `PortalDevice` — 스크랩 15 + 외계 합금 2 납품 |

지하 대기는 호흡할 수 있으므로 산소는 압박이 아니다. 대신 **빛이 압박이다.** 랜턴을 얻기 전에는 발광 버섯 군락 주변으로만 움직일 수 있고, 랜턴을 얻은 뒤에도 배터리가 스크랩을 먹는다. 군락은 배터리를 무료로 채워 주는 거점이 된다.

---

## 8. 콘텐츠 데이터

### 8.1 아이템

| id | 이름 | 분류 | 최대 스택 | 비고 |
|---|---|---|---|---|
| `scrap` | 스크랩 | Resource | 999 | 연료 겸 화폐 겸 제작재. HUD 카운터 |
| `mushroom_cap` | 버섯 갓 | Resource | 50 | 발광 버섯 채집물 |
| `fern_fiber` | 양치 섬유 | Resource | 50 | 양치류 채집물 |
| `machine_part` | 기계 부품 | Resource | 50 | 기계 잔해·생물 드롭 |
| `alien_alloy` | 외계 합금 | Resource | 20 | 포탈 기동 재료 |
| `pickaxe` | 곡괭이 | Tool | 1 | 소켓 `pickaxe01`. 채집 티어 1, 근접 피해 |
| `oxygen_filter` | 산소 필터 | Consumable | 5 | 산소 감소율 완화(지속형) |
| `lantern` | 랜턴 | Tool | 1 | 시야 확보. 스크랩 소비 |
| `repair_kit` | 수리 키트 | Consumable | 5 | 체력 회복 |
| `portal_key` | 포탈 기동 키 | Quest | 1 | 목표 6 |

### 8.2 레시피

| 결과 | 재료 | 스테이션 |
|---|---|---|
| `pickaxe` | `scrap` ×5, `machine_part` ×2 | None |
| `oxygen_filter` | `scrap` ×8, `mushroom_cap` ×3 | None |
| `lantern` | `scrap` ×6, `machine_part` ×1 | None |
| `repair_kit` | `fern_fiber` ×4, `mushroom_cap` ×2 | None |
| `portal_key` | `alien_alloy` ×3, `machine_part` ×4, `scrap` ×20 | Bench |

수치는 초안이며, 챕터 1 플레이 시간 30~45분을 기준으로 조정한다.

### 8.3 채집 노드

| 노드 | 필요 도구 | 드롭 | 재생성 |
|---|---|---|---|
| 발광 버섯 | 없음 | `mushroom_cap` 1~3 | 120초 |
| 양치류 군락 | 없음 | `fern_fiber` 1~3 | 120초 |
| 기계 잔해 | 곡괭이 티어 1 | `scrap` 2~4, `machine_part` 0~1 | 없음 |
| 광맥 | 곡괭이 티어 1 | `alien_alloy` 1~2 | 없음 |

---

## 9. 폴더 구조

```
Assets/02.Scripts/Domain/          ← Survive.Domain.asmdef  (MonoBehaviour 금지, Feel·DOTween 금지)
├── Core/          EventChannelSO, EventChannels, GameServices, ISaveable, SceneReferenceSO
├── Vitals/        Vital, VitalDefinitionSO, IOxygenModifier, OxygenRate
├── Items/         ItemCategory, ToolType, ItemDataSO, ToolItemSO, ConsumableItemSO,
│                  ItemDatabaseSO, ItemStack, Inventory
├── Combat/        DamageInfo, IDamageable
├── Harvesting/    HarvestNodeSO, LootTableSO
├── Crafting/      RecipeSO, RecipeBookSO, CraftingService
├── Creatures/     CreatureDefinitionSO, TrophicTier, LocomotionType, BehaviorProfile
├── Progression/   ObjectiveSO 및 구현체, ChapterSO, IObjectiveContext
└── Narrative/     SequenceSO

Assets/02.Scripts/                 ← Assembly-CSharp  (Feel·DOTween·Cinemachine 자유롭게 사용)
├── Core/          GameBootstrap, SceneFlowService, SaveService
├── Input/         InputReaderSO, PlayerInputActions.inputactions
├── Player/        PlayerLocomotion, PlayerCameraRig, PlayerAnimatorDriver, PlayerToolHolder, PlayerContext
├── Vitals/        PlayerVitals
├── Inventory/     PlayerInventory
├── Interaction/   IInteractable, IHoldInteractable, PlayerInteractor, ItemPickup, LootContainer
├── Harvesting/    HarvestNode
├── Combat/        MeleeSwing, CreatureHealth, PlayerDamageReceiver
├── Creatures/     CreatureBrain, FlyerMotor, 상태 클래스
├── Crafting/      CraftingBench
├── World/         OxygenZone, HazardZone, PortalDevice, Checkpoint
├── Progression/   ChapterDirector
├── Narrative/     SequenceDirector, ScreenFader
├── UI/            HUDController, VitalBarView, ScrapCounterView, InventoryUI, QuickSlotUI,
│                  CraftingUI, ObjectiveListView, SubtitleView, PauseMenu, UIStateService,
│                  ValueBarScript (기존)
├── CameraShake.cs (기존, 유지)
└── Utill/         EasingFunctions.cs (기존, 유지)

Assets/08.Data/
├── Items/  Recipes/  Creatures/  LootTables/  HarvestNodes/
├── Objectives/  Chapters/  Sequences/  Vitals/
└── EventChannels/  Scenes/  Input/

Assets/09.Tests/
├── EditMode/  PlayMode/
```

---

## 10. 에이전트 작업 분할

병렬 실행의 전제는 **파일 소유권이 겹치지 않는 것**이다.

### 규칙
1. 각 워크스트림은 **자기 폴더의 `.cs`만** 수정한다.
2. **씬과 프리팹 편집은 W0과 W13만** 수행한다. 다른 워크스트림은 씬을 건드리지 않는다.
   단 하나의 예외: **W12는 `CvsUI.prefab`의 `PanelDialog` 하위만** 수정할 수 있다 (자막 구조 정리). W0이 `CvsUI` 단일화를 마친 뒤에 착수한다.
3. 데이터 에셋(`.asset`)은 각 워크스트림이 자기 담당 폴더에만 만든다.
4. 다른 시스템이 필요하면 **이벤트 채널이나 인터페이스로** 접근한다. 직접 참조 금지.
5. **어셈블리는 두 계층이다 — 도메인과 거동.**

   ```
   Assets/02.Scripts/Domain/   →  Survive.Domain.asmdef   (순수 로직 + 데이터 SO + 인터페이스)
   Assets/02.Scripts/<그 외>   →  Assembly-CSharp          (MonoBehaviour 전부)
   ```

   **`Domain`에 들어가는 것**: MonoBehaviour가 아니고, Feel·DOTween에 의존하지 않는 것 전부. `Vital`, `Inventory`, `ItemStack`, 아이템·레시피·전리품·목표·생물 정의 SO, `CraftingService`, `IDamageable`, `IOxygenModifier`, `ISaveable`, `EventChannelSO`, `GameServices`.

   **`Assembly-CSharp`에 남는 것**: 모든 MonoBehaviour. Feel `MMF_Player`, DOTween 전체(Modules 포함), Cinemachine을 제약 없이 쓴다.

   `Assembly-CSharp`는 모든 asmdef를 자동 참조하므로 의존은 **거동 → 도메인** 한 방향으로만 흐른다. 도메인은 거동을 모른다.

   **왜 이렇게 나누는가** — 두 요구가 정면으로 부딪히기 때문이다:
   - 테스트 어셈블리는 `Assembly-CSharp` 같은 사전 정의 어셈블리를 **참조할 수 없다.** asmdef가 없으면 11장의 EditMode 전략을 실행할 수 없다.
   - 그런데 asmdef도 사전 정의 어셈블리를 참조할 수 없다. Feel의 **`MMFeedbacks`(`MMF_Player`)에는 asmdef가 없어 `Assembly-CSharp`에 속한다.** DOTween의 `Modules/*.cs`(`DOFade` 등 UI 확장)도 `Assembly-CSharp-firstpass`에 있다.

   즉 게임플레이 코드를 통째로 asmdef에 넣으면 Feel을 쓸 수 없고, asmdef를 아예 안 쓰면 테스트를 못 한다. 순수 로직만 도메인으로 떼면 양쪽을 다 얻는다.

   **`Survive.Domain.asmdef`가 참조할 어셈블리**: 없음(UnityEngine만). 데이터와 순수 로직뿐이므로 Cinemachine·InputSystem·Feel·DOTween 어느 것도 필요 없다.

   **참고**: 제3자 자산(`Assets/Feel/`, `Assets/Plugins/Demigiant/`)에는 asmdef를 만들거나 고치지 않는다. Feel의 조건부 컴파일 구성을 건드리면 깨지기 쉽다.

### 워크스트림 간 계약

병렬 단계에서 서로를 참조하는 지점은 아래 둘뿐이다. 해당 타입을 **먼저 확정해 커밋한 뒤** 나머지 작업을 이어간다.

| 계약 | 정의 주체 | 소비 주체 | 처리 |
|---|---|---|---|
| `PlayerContext` | W5 (Player) | W4 (Interaction) | W5가 착수 즉시 `PlayerContext`의 프로퍼티 시그니처만 먼저 확정·커밋한다. 하위 참조는 `GetComponentInChildren`으로 채우므로 구현 순서에 얽매이지 않는다 |
| `IOxygenModifier` | W2 (Vitals) | W9 (World) | W2가 인터페이스를 4.4대로 먼저 커밋한다. 3단계 시작 시점엔 이미 존재한다 |

### 순서

| 단계 | 워크스트림 | 병렬 | 산출물 |
|---|---|---|---|
| **0** | **W0 씬 자산 정규화** | ❌ 단독 | Player 프리팹화(MainScene 버전 기준), `CvsUI` 단일화, HP 바를 `GaugeBarPrefab`으로 통일, URP 컴포넌트 정리, Build Settings 등록, 폴더 스캐폴딩 |
| **1** | **W1 Core + Input** | ❌ 단독 | 4.1·4.2 전부. Input System 패키지 추가 |
| **2** | W2 Vitals / W3 Inventory / W4 Interaction+Combat / W5 Player 리팩터 | ✅ 4개 | 4.3~4.6, 4.8 |
| **3** | W6 Harvesting / W7 Crafting / W8 Creatures / W9 World | ✅ 4개 | 4.7·4.9~4.11 |
| **4** | W10 Progression / W11 UI·HUD | ✅ 2개 | 4.12·4.14 |
| **5** | W12 Narrative + 프롤로그 시퀀스 + **자막 수리** | ❌ 단독 | 4.13 + 6장. 2.3-7의 자막 고장 4건 해소 포함 |
| **6** | **W13 콘텐츠 배치** | ❌ 단독 | 부유섬 레벨 배치, 생물 스폰, 채집 노드, 목표 연결, NavMesh 베이크 |

W0과 W1을 단독으로 두는 이유는 명확하다 — 정규화 전에 씬을 병렬로 만지면 이미 벌어진 Player/UI 분기가 더 악화된다.

---

## 11. 테스트 전략

Unity Test Framework 1.7.0을 사용한다. **순수 로직만 EditMode로 검증**하고, PlayMode는 최소로 둔다.

### EditMode

| 테스트 | 대상 |
|---|---|
| `InventoryTests` | 스택 병합, 초과분 반환값, 슬롯 이동·교환, 가득 찬 인벤토리, 존재하지 않는 아이템 제거 |
| `VitalsTests` | 0~Max 클램프, 산소 고갈 → 체력 감소 전이, `IOxygenModifier` 겹침 시 최댓값 채택 |
| `CraftingTests` | 재료 충족·부족 판정, 제작 후 재료 차감, 스테이션 요건 |
| `LootTableTests` | 확률 0/1 경계, min==max, 고정 시드로 결정적 결과 |
| `ObjectiveTests` | 목표 타입 5종의 완료 판정과 `Progress` 값 |
| `ItemDatabaseTests` | id 중복 검출, 미존재 id 조회 |

`Vital`, `Inventory`, `CraftingService`, `LootTableSO.Roll`을 순수 클래스/정적 메서드로 설계한 이유가 이것이다. `LootTableSO.Roll`이 `System.Random`을 주입받는 것도 결정적 테스트를 위해서다.

### PlayMode

| 테스트 | 내용 |
|---|---|
| `Chapter1SmokeTest` | `ChapterDirector.ForceCompleteCurrent()`로 목표 1→6을 순차 완료시켜 `ChapterCompleted`가 발생하는지 확인 |

---

## 12. 범위 밖 (YAGNI)

| 항목 | 사유 |
|---|---|
| **생물 도감 UI / 스캐너** | `CreatureDefinitionSO`에 `codexDescription`·`codexSketch` 필드만 미리 확보하고, 해금 UI는 챕터 2로 |
| 허기·갈증·체온 | D2. `Hunger` 바는 제거 |
| 건축·거점 | 챕터 1에 필요 없음 |
| 낮/밤 순환 | 지하라 의미 없음 |
| 매크로늄 조수 등 주기 이벤트 | 챕터 1 목표를 게이팅하지 않음 |
| 소비자급 생물 (랩터형·다관절형·거미형) | 부유섬 서식종이 아님. 챕터 2 이후 |
| 무기 종류 확장 | D4 |
| 오디오 시스템 | 개별 `AudioSource` 재생으로 충분 |
| 평야·절벽 구역 | 챕터 2·3 |

---

## 13. 미해결 사항

| 항목 | 언제 정할 것인가 |
|---|---|
| 아이템·레시피 수치 밸런스 | W13 콘텐츠 배치 후 실제 플레이로 조정 |
| 부유섬의 구체적 지형 레이아웃 (강 폭, 포탈 위치, 버섯 군락 개수) | W13에서 결정 |
| `MainScene` 파일명을 `Chapter1_FloatingIsland`로 바꿀지 | 선택 사항. 씬은 GUID로 참조되어 리네임해도 참조가 깨지지 않는다 |
| 챕터 2 진입 연출 | 챕터 2 설계 시 |
