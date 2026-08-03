# P0 아트 방향 확립 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 저폴리 에셋 팩들을 하나의 세계로 묶는 조명·색 규칙을 세우고, 그 규칙을 자동으로 지키는 검사 도구를 만든다.

**Architecture:** 색과 규칙은 `Survive.Domain`에 순수 데이터·순수 함수로 둔다(테스트 가능). Unity 에디터 도구가 `AssetDatabase`로 에셋을 모아 그 순수 함수에 넘겨 위반을 보고한다. 씬의 조명은 `RenderSettings`와 구역별 URP `VolumeProfile` 에셋으로 다룬다.

**Tech Stack:** Unity 6000.5.6f1 / URP 17.5 / NUnit (EditMode) / uloop MCP 도구

## Global Constraints

프로젝트가 실제로 당한 사고에서 나온 규칙이다. 어기면 조용히 깨진다.

- **ScriptableObject·MonoBehaviour는 클래스 하나당 파일 하나.** 한 파일에 둘 이상 넣으면 Unity가 `m_Script` 참조를 연결하지 못한다
- **씬 저장은 `EditorSceneManager.MarkSceneDirty(scene)` + `EditorSceneManager.SaveScene(scene)`.** `SaveOpenScenes()`는 성공을 반환하고도 디스크에 안 쓰는 경우가 있다
- **`SerializedObject`로 값을 넣은 뒤 반드시 `EditorUtility.SetDirty`를 부른다**
- **동적 코드에서 `System.Type.GetType` 금지** — 보안 정책에 막힌다. 게임 타입은 `using`으로 직접 참조한다
- **동적 코드에서 `AssetDatabase.DeleteAsset`·`System.IO.*` 금지** — 파일 삭제는 터미널로
- **에셋을 재생성하면 GUID가 바뀌어 참조가 조용히 죽는다.** 재생성 대신 기존 에셋을 수정한다
- **코드 식별자는 영어. 주석과 UI 문구는 한국어**
- **색상값(hex)은 `docs/superpowers/specs/2026-08-03-p0-art-direction-design.md` §4·§6에서 확정된 것이다.** 임의로 바꾸지 않는다

**확정 색상값** (스펙 §4·§6에서 그대로 옮김)

| 이름 | hex | 용도 |
|---|---|---|
| `LightShaft` | `#E8D5A8` | 추락 구멍 빛기둥 |
| `Glowshroom` | `#2FE6C8` | 발광 버섯 |
| `Flame` | `#FF9A2E` | 랜턴·화톳불 |
| `Macronium` | `#A12EE0` | 매크로늄 |
| `MacroniumHighlight` | `#E77BFF` | 매크로늄 표면 하이라이트 |
| `WaterShallow` | `#2E5C7A` | 물 얕은 곳 |
| `WaterDeep` | `#0E1F2E` | 물 깊은 곳 |
| `FogSurface` | `#C4703A` | 프롤로그 화성 지표 |
| `FogIslands` | `#0C0F15` | 부유섬 |
| `FogPlains` | `#0D1A18` | 얕은 평야 (낮) |
| `FogPlainsNight` | `#140A1E` | 얕은 평야 (밤) |
| `FogCliffs` | `#2C1240` | 깊은 절벽 |

**스무스니스 3단**: 무광 `0.1` / 반광 `0.35` / 금속 `0.6`. 그 사이 임의 값 금지.

---

## 현황 실측 (플랜 작성 시점)

플랜의 전제다. 착수 전에 달라졌는지 확인한다.

| 항목 | 값 |
|---|---|
| `MainScene` `m_Fog` | `0` (꺼짐) |
| `MainScene` `m_AmbientMode` | `3` (Flat) |
| `MainScene` `m_AmbientIntensity` | `1.05` |
| `MainScene` `m_AmbientSkyColor` | `(0.17, 0.185, 0.235)` |
| `DefaultVolumeProfile.components` | `[]` (비어 있음) |
| 프로젝트 전체 `.mat` | 337개 |
| **씬에서 전이적으로 도달하는 `.mat`** | **26개** |
| 그중 EMISSION 켜진 것 | 4개 |
| Poly Universal Pack | 프리팹 6484개가 `M_Universal_A.mat` **하나**를 공유 (아틀라스) |

**EMISSION 켜진 4개**
- `polyperfect/Poly Universal Pack/Materials/M_Universal_A.mat`
- `Other Assets/Art/LowPolyRockPack/Materials/Green.mat`
- `Other Assets/Art/LowPolyRockPack/Materials/Grey.mat`
- `Other Assets/Art/LowPolyRockPack/Materials/Sand.mat`

---

## File Structure

| 파일 | 책임 |
|---|---|
| `Assets/02.Scripts/Domain/Art/ArtPalette.cs` | 확정 색상값 상수. 런타임·에디터·테스트가 모두 여기서 읽는다 |
| `Assets/02.Scripts/Domain/Art/MaterialFacts.cs` | 머티리얼 한 개에서 뽑은 검사 대상 값들 (순수 구조체) |
| `Assets/02.Scripts/Domain/Art/MaterialRule.cs` | 위반 판정 순수 함수. Unity 에디터 API를 쓰지 않는다 |
| `Assets/02.Scripts/Editor/ArtRuleChecker.cs` | `AssetDatabase`로 씬 도달 머티리얼을 모아 `MaterialRule`에 넘기고 보고 |
| `Assets/08.Data/Art/Volume_Islands.asset` | 부유섬 URP 볼륨 프로파일 |
| `Assets/08.Data/Art/Volume_Surface.asset` | 프롤로그 지표 볼륨 프로파일 |
| `Assets/05.Prefabs/Environment/LightShaft.prefab` | 추락 구멍 빛기둥 |
| `Assets/09.Tests/EditMode/ArtPaletteTests.cs` | 색상값이 스펙과 일치하는지 |
| `Assets/09.Tests/EditMode/MaterialRuleTests.cs` | 위반 판정 로직 |
| `Assets/01.Scenes/MainScene.unity` (수정) | 지하 조명 기준 |
| `Assets/01.Scenes/StartScene.unity` (수정) | 지표 조명 기준 |

**왜 `MaterialRule`을 Domain에 두는가**: `02.Scripts/Editor/`에는 asmdef가 없어 기본 어셈블리(`Assembly-CSharp-Editor`)에 들어간다. 테스트 어셈블리 `Survive.Tests.EditMode`는 asmdef라서 기본 어셈블리를 참조할 수 없다. 판정 로직을 `Survive.Domain`에 두면 테스트가 직접 부를 수 있고, 에디터 도구는 그것을 호출하기만 한다.

**평야·절벽 볼륨 프로파일은 만들지 않는다.** 해당 맵이 없다(P4·P5). 색상값은 `ArtPalette`에 미리 넣어두되 프로파일 에셋은 그때 만든다.

---

### Task 1: 색 상수와 위반 판정 규칙

**Files:**
- Create: `Assets/02.Scripts/Domain/Art/ArtPalette.cs`
- Create: `Assets/02.Scripts/Domain/Art/MaterialFacts.cs`
- Create: `Assets/02.Scripts/Domain/Art/MaterialRule.cs`
- Test: `Assets/09.Tests/EditMode/ArtPaletteTests.cs`
- Test: `Assets/09.Tests/EditMode/MaterialRuleTests.cs`

**Interfaces:**
- Consumes: 없음 (첫 태스크)
- Produces:
  - `Survive.Domain.Art.ArtPalette` — `static readonly Color` 필드들(위 표의 이름 그대로), `static Color FromHex(int rgb)`, `static readonly Color[] AllowedEmission`
  - `Survive.Domain.Art.MaterialFacts` — `readonly struct`, 생성자 `MaterialFacts(string assetPath, string shaderName, float smoothness, float metallic, bool hasEmission, Color emissionColor)`, 동명의 읽기 전용 프로퍼티
  - `Survive.Domain.Art.MaterialRule` — `static IReadOnlyList<string> Violations(in MaterialFacts m)`, `static bool IsAllowedShader(string shaderName)`, `static bool IsBandedSmoothness(float v)`, `static bool IsAllowedEmission(Color c)`, 상수 `SmoothnessMatte`/`SmoothnessSemi`/`SmoothnessMetal`/`SmoothnessTolerance`/`EmissionChannelTolerance`

- [ ] **Step 1: 색 상수 테스트를 먼저 쓴다**

`Assets/09.Tests/EditMode/ArtPaletteTests.cs`

```csharp
using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 색상값은 P0 스펙 §4·§6에서 확정된 것이다.
    /// 이 테스트가 깨지면 코드가 아니라 스펙을 먼저 고쳤는지 확인한다.
    /// </summary>
    public class ArtPaletteTests
    {
        static void AssertHex(Color actual, int expectedRgb)
        {
            int r = Mathf.RoundToInt(actual.r * 255f);
            int g = Mathf.RoundToInt(actual.g * 255f);
            int b = Mathf.RoundToInt(actual.b * 255f);
            int packed = (r << 16) | (g << 8) | b;
            Assert.AreEqual(expectedRgb, packed, $"기대 #{expectedRgb:X6} / 실제 #{packed:X6}");
        }

        [Test] public void LightShaft_is_E8D5A8() => AssertHex(ArtPalette.LightShaft, 0xE8D5A8);
        [Test] public void Glowshroom_is_2FE6C8() => AssertHex(ArtPalette.Glowshroom, 0x2FE6C8);
        [Test] public void Flame_is_FF9A2E() => AssertHex(ArtPalette.Flame, 0xFF9A2E);
        [Test] public void Macronium_is_A12EE0() => AssertHex(ArtPalette.Macronium, 0xA12EE0);
        [Test] public void MacroniumHighlight_is_E77BFF() => AssertHex(ArtPalette.MacroniumHighlight, 0xE77BFF);

        [Test] public void WaterShallow_is_2E5C7A() => AssertHex(ArtPalette.WaterShallow, 0x2E5C7A);
        [Test] public void WaterDeep_is_0E1F2E() => AssertHex(ArtPalette.WaterDeep, 0x0E1F2E);

        [Test] public void FogSurface_is_C4703A() => AssertHex(ArtPalette.FogSurface, 0xC4703A);
        [Test] public void FogIslands_is_0C0F15() => AssertHex(ArtPalette.FogIslands, 0x0C0F15);
        [Test] public void FogPlains_is_0D1A18() => AssertHex(ArtPalette.FogPlains, 0x0D1A18);
        [Test] public void FogPlainsNight_is_140A1E() => AssertHex(ArtPalette.FogPlainsNight, 0x140A1E);
        [Test] public void FogCliffs_is_2C1240() => AssertHex(ArtPalette.FogCliffs, 0x2C1240);

        [Test]
        public void AllowedEmission_has_exactly_the_five_light_colors()
        {
            Assert.AreEqual(5, ArtPalette.AllowedEmission.Length,
                "광원은 넷이고 매크로늄만 하이라이트를 하나 더 갖는다. 늘리려면 스펙 §4를 먼저 고친다.");
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.LightShaft);
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.Glowshroom);
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.Flame);
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.Macronium);
            CollectionAssert.Contains(ArtPalette.AllowedEmission, ArtPalette.MacroniumHighlight);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

uloop `compile` 실행. 기대: `ArtPalette`가 없어 컴파일 에러.

- [ ] **Step 3: `ArtPalette`를 만든다**

`Assets/02.Scripts/Domain/Art/ArtPalette.cs`

```csharp
using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 지하에서 빛나는 것은 넷뿐이다 — 빛기둥·발광 버섯·불꽃·매크로늄.
    /// 그 외의 발광은 금지한다. 이것이 출처가 섞인 저폴리 에셋 팩들을
    /// 한 세계로 묶는 가장 강한 규칙이다.
    ///
    /// 값을 바꾸려면 코드가 아니라
    /// docs/superpowers/specs/2026-08-03-p0-art-direction-design.md 를 먼저 고친다.
    /// </summary>
    public static class ArtPalette
    {
        // 광원 — 색상환에 고르게 떨어뜨려 어둠 속에서 색만으로 구분되게 한다
        public static readonly Color LightShaft = FromHex(0xE8D5A8);         // 추락 구멍. 모래폭풍에 걸러진 지표광
        public static readonly Color Glowshroom = FromHex(0x2FE6C8);         // 발광 버섯. 생명·안전·무료 충전
        public static readonly Color Flame = FromHex(0xFF9A2E);              // 랜턴·화톳불. 내 빛이자 카운트다운
        public static readonly Color Macronium = FromHex(0xA12EE0);          // MARSO의 인공물. 가로막는 것
        public static readonly Color MacroniumHighlight = FromHex(0xE77BFF); // 매크로늄 표면 하이라이트

        // 물은 발광하지 않는다. 반사와 굴절로만 존재한다
        public static readonly Color WaterShallow = FromHex(0x2E5C7A);
        public static readonly Color WaterDeep = FromHex(0x0E1F2E);

        // 포그 색이 곧 구역의 색이다. 깊이가 곧 자홍의 농도다
        public static readonly Color FogSurface = FromHex(0xC4703A);      // 프롤로그 화성 지표. 모래폭풍
        public static readonly Color FogIslands = FromHex(0x0C0F15);      // 부유섬. 매크로늄은 발밑에만
        public static readonly Color FogPlains = FromHex(0x0D1A18);       // 얕은 평야. 등불버섯이 천장을 밝힌다
        public static readonly Color FogPlainsNight = FromHex(0x140A1E);  // 평야의 밤. 자홍 기운이 돈다
        public static readonly Color FogCliffs = FromHex(0x2C1240);       // 깊은 절벽. 매크로늄이 노출되어 고인다

        /// <summary>발광이 허용되는 색. 이 밖의 Emission은 위반이다.</summary>
        public static readonly Color[] AllowedEmission =
        {
            LightShaft, Glowshroom, Flame, Macronium, MacroniumHighlight,
        };

        /// <summary>0xRRGGBB 정수를 불투명 Color로. 스펙의 hex를 그대로 옮겨 쓰기 위한 것이다.</summary>
        public static Color FromHex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }
}
```

- [ ] **Step 4: 색 테스트가 통과하는지 확인한다**

uloop `run-tests` (EditMode, 필터 `ArtPaletteTests`). 기대: 전부 PASS.

- [ ] **Step 5: 위반 판정 테스트를 쓴다**

`Assets/09.Tests/EditMode/MaterialRuleTests.cs`

```csharp
using NUnit.Framework;
using UnityEngine;
using Survive.Domain.Art;

namespace Survive.Tests.EditMode
{
    /// <summary>
    /// 사람이 눈으로 지키는 규칙은 지켜지지 않는다. 그래서 판정을 코드로 둔다.
    /// </summary>
    public class MaterialRuleTests
    {
        static MaterialFacts Facts(
            string shader = "Universal Render Pipeline/Lit",
            float smoothness = MaterialRule.SmoothnessMatte,
            float metallic = 0f,
            bool hasEmission = false,
            Color? emission = null)
            => new MaterialFacts("Assets/dummy.mat", shader, smoothness, metallic,
                                 hasEmission, emission ?? Color.black);

        [Test]
        public void Clean_material_has_no_violations()
        {
            Assert.IsEmpty(MaterialRule.Violations(Facts()));
        }

        [Test]
        public void Unlisted_shader_is_a_violation()
        {
            var v = MaterialRule.Violations(Facts(shader: "Custom/SomePackToon"));
            Assert.AreEqual(1, v.Count);
            StringAssert.Contains("셰이더", v[0]);
        }

        [Test]
        public void Water_shader_is_allowed()
        {
            Assert.IsTrue(MaterialRule.IsAllowedShader("Stylized Water For URP/Water"));
        }

        [TestCase(0.1f)]
        [TestCase(0.35f)]
        [TestCase(0.6f)]
        public void Banded_smoothness_is_allowed(float v)
        {
            Assert.IsTrue(MaterialRule.IsBandedSmoothness(v));
        }

        [TestCase(0.0f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(1.0f)]
        public void Off_band_smoothness_is_a_violation(float v)
        {
            Assert.IsFalse(MaterialRule.IsBandedSmoothness(v));
            var list = MaterialRule.Violations(Facts(smoothness: v));
            Assert.AreEqual(1, list.Count);
            StringAssert.Contains("스무스니스", list[0]);
        }

        [Test]
        public void Smoothness_within_tolerance_is_allowed()
        {
            Assert.IsTrue(MaterialRule.IsBandedSmoothness(
                MaterialRule.SmoothnessSemi + MaterialRule.SmoothnessTolerance * 0.5f));
        }

        [Test]
        public void Emission_in_palette_is_allowed()
        {
            Assert.IsEmpty(MaterialRule.Violations(
                Facts(hasEmission: true, emission: ArtPalette.Glowshroom)));
        }

        [Test]
        public void Emission_outside_palette_is_a_violation()
        {
            var v = MaterialRule.Violations(
                Facts(hasEmission: true, emission: ArtPalette.FromHex(0x00FF00)));
            Assert.AreEqual(1, v.Count);
            StringAssert.Contains("발광", v[0]);
        }

        [Test]
        public void Emission_off_ignores_the_color_field()
        {
            Assert.IsEmpty(MaterialRule.Violations(
                Facts(hasEmission: false, emission: ArtPalette.FromHex(0x00FF00))));
        }

        [Test]
        public void Hdr_emission_beyond_one_still_matches_by_hue()
        {
            var bright = ArtPalette.Macronium * 3f;
            Assert.IsTrue(MaterialRule.IsAllowedEmission(bright),
                "HDR 발광은 강도가 1을 넘는다. 강도가 아니라 색으로 판정해야 한다.");
        }

        [Test]
        public void Violations_are_reported_together()
        {
            var v = MaterialRule.Violations(Facts(
                shader: "Custom/SomePackToon", smoothness: 0.42f,
                hasEmission: true, emission: ArtPalette.FromHex(0x00FF00)));
            Assert.AreEqual(3, v.Count, "위반은 하나만 보고하고 멈추면 안 된다");
        }
    }
}
```

- [ ] **Step 6: 테스트가 실패하는지 확인한다**

uloop `compile`. 기대: `MaterialFacts`·`MaterialRule`이 없어 컴파일 에러.

- [ ] **Step 7: `MaterialFacts`를 만든다**

`Assets/02.Scripts/Domain/Art/MaterialFacts.cs`

```csharp
using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 머티리얼 하나에서 뽑아낸 검사 대상 값들.
    /// 에디터 API에 의존하지 않으므로 테스트에서 손으로 만들 수 있다.
    /// </summary>
    public readonly struct MaterialFacts
    {
        public string AssetPath { get; }
        public string ShaderName { get; }
        public float Smoothness { get; }
        public float Metallic { get; }
        public bool HasEmission { get; }
        public Color EmissionColor { get; }

        public MaterialFacts(string assetPath, string shaderName, float smoothness,
                             float metallic, bool hasEmission, Color emissionColor)
        {
            AssetPath = assetPath;
            ShaderName = shaderName;
            Smoothness = smoothness;
            Metallic = metallic;
            HasEmission = hasEmission;
            EmissionColor = emissionColor;
        }
    }
}
```

- [ ] **Step 8: `MaterialRule`을 만든다**

`Assets/02.Scripts/Domain/Art/MaterialRule.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 팩 출처가 섞인 머티리얼을 한 세계로 묶기 위한 판정.
    ///
    /// 세 가지만 본다.
    ///   1) 셰이더가 허용 목록 안인가
    ///   2) 스무스니스가 3단 중 하나인가
    ///   3) 발광색이 광원 4색(+하이라이트) 안인가
    ///
    /// Metallic은 판정하지 않는다. "기계와 인공 구조물에만"이라는 규칙은
    /// 자동 분류가 불가능하므로, 도구가 목록만 뽑아 사람이 본다.
    /// </summary>
    public static class MaterialRule
    {
        public const float SmoothnessMatte = 0.1f;
        public const float SmoothnessSemi = 0.35f;
        public const float SmoothnessMetal = 0.6f;
        public const float SmoothnessTolerance = 0.02f;

        /// <summary>발광색 비교 허용 오차(채널당). 정규화한 색끼리 비교한다.</summary>
        public const float EmissionChannelTolerance = 0.04f;

        static readonly string[] AllowedShaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Terrain/Lit",
            "Stylized Water For URP/Water",
            "TextMeshPro/Distance Field",
            "TextMeshPro/Mobile/Distance Field",
            "Skybox/Procedural",
        };

        public static bool IsAllowedShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            foreach (var s in AllowedShaders)
                if (s == shaderName) return true;
            return false;
        }

        public static bool IsBandedSmoothness(float v)
            => Mathf.Abs(v - SmoothnessMatte) <= SmoothnessTolerance
            || Mathf.Abs(v - SmoothnessSemi) <= SmoothnessTolerance
            || Mathf.Abs(v - SmoothnessMetal) <= SmoothnessTolerance;

        /// <summary>
        /// HDR 발광은 강도가 1을 넘으므로 밝기가 아니라 색으로 본다.
        /// 가장 밝은 채널을 1로 맞춘 뒤 비교한다.
        /// </summary>
        public static bool IsAllowedEmission(Color c)
        {
            var a = Normalize(c);
            foreach (var allowed in ArtPalette.AllowedEmission)
            {
                var b = Normalize(allowed);
                if (Mathf.Abs(a.r - b.r) <= EmissionChannelTolerance
                 && Mathf.Abs(a.g - b.g) <= EmissionChannelTolerance
                 && Mathf.Abs(a.b - b.b) <= EmissionChannelTolerance)
                    return true;
            }
            return false;
        }

        static Color Normalize(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (max <= 0.0001f) return Color.black;
            return new Color(c.r / max, c.g / max, c.b / max, 1f);
        }

        /// <summary>위반을 전부 모아 돌려준다. 하나만 보고하고 멈추지 않는다.</summary>
        public static IReadOnlyList<string> Violations(in MaterialFacts m)
        {
            var list = new List<string>();

            if (!IsAllowedShader(m.ShaderName))
                list.Add($"허용 목록 밖 셰이더: '{m.ShaderName}'");

            if (!IsBandedSmoothness(m.Smoothness))
                list.Add($"스무스니스가 3단 밖: {m.Smoothness:0.###} " +
                         $"(허용 {SmoothnessMatte} / {SmoothnessSemi} / {SmoothnessMetal})");

            if (m.HasEmission && !IsAllowedEmission(m.EmissionColor))
                list.Add($"광원 4색 밖 발광: {ColorUtility.ToHtmlStringRGB(Normalize(m.EmissionColor))}");

            return list;
        }
    }
}
```

- [ ] **Step 9: 테스트 전체를 돌린다**

uloop `run-tests` (EditMode 전체). 기대: 신규 테스트 전부 PASS, **기존 테스트도 전부 PASS**.

- [ ] **Step 10: 커밋**

```bash
git add SurviveProject/Assets/02.Scripts/Domain/Art SurviveProject/Assets/09.Tests/EditMode/ArtPaletteTests.cs SurviveProject/Assets/09.Tests/EditMode/MaterialRuleTests.cs
git commit -m "P0: 광원 4색과 머티리얼 규칙을 코드로 못박았다"
```

---

### Task 2: 규칙 위반 머티리얼 전수 보고 도구

**Files:**
- Create: `Assets/02.Scripts/Editor/ArtRuleChecker.cs`

**Interfaces:**
- Consumes: `Survive.Domain.Art.MaterialFacts`, `Survive.Domain.Art.MaterialRule`
- Produces: `Survive.EditorTools.ArtRuleChecker` — 메뉴 `Tools/Survive/아트 규칙 점검`, `public static string Run()` (보고서 문자열 반환), `public static int ViolationCount()` 

**왜 보고만 하고 고치지 않는가**: 자동 수정은 팩 에셋을 건드려 되돌리기 어렵다. Task 6에서 사람이 판단해 고친다.

- [ ] **Step 1: 도구를 만든다**

`Assets/02.Scripts/Editor/ArtRuleChecker.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Survive.Domain.Art;
using UnityEditor;
using UnityEngine;

namespace Survive.EditorTools
{
    /// <summary>
    /// 씬이 실제로 도달하는 머티리얼만 검사한다.
    ///
    /// 프로젝트 전체 .mat은 337개지만 게임에 나오는 것은 26개뿐이다.
    /// Feel 데모나 안 쓰는 팩 에셋까지 고치는 것은 낭비이고,
    /// 보고서가 길어지면 아무도 안 읽는다.
    /// </summary>
    public static class ArtRuleChecker
    {
        static readonly string[] ScenePaths =
        {
            "Assets/01.Scenes/MainScene.unity",
            "Assets/01.Scenes/StartScene.unity",
        };

        static readonly string[] PrefabRoots = { "Assets/05.Prefabs" };

        [MenuItem("Tools/Survive/아트 규칙 점검")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static int ViolationCount()
        {
            int n = 0;
            foreach (var f in Collect())
                n += MaterialRule.Violations(f).Count;
            return n;
        }

        public static string Run()
        {
            var facts = Collect();
            var sb = new StringBuilder();
            sb.AppendLine($"[아트 규칙 점검] 씬 도달 머티리얼 {facts.Count}개");

            int violations = 0;
            foreach (var f in facts.OrderBy(x => x.AssetPath))
            {
                var v = MaterialRule.Violations(f);
                if (v.Count == 0) continue;
                violations += v.Count;
                sb.AppendLine($"  {f.AssetPath}");
                foreach (var line in v) sb.AppendLine($"      - {line}");
            }

            sb.AppendLine(violations == 0
                ? "위반 없음."
                : $"위반 {violations}건.");

            // Metallic은 자동 판정하지 않는다. 사람이 볼 목록만 낸다.
            var metallic = facts.Where(f => f.Metallic > 0.01f).OrderBy(f => f.AssetPath).ToList();
            if (metallic.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[사람 확인] Metallic > 0 인 머티리얼 {metallic.Count}개 — 기계·인공 구조물만 허용된다");
                foreach (var f in metallic)
                    sb.AppendLine($"  {f.AssetPath}  (metallic {f.Metallic:0.##})");
            }

            return sb.ToString();
        }

        static List<MaterialFacts> Collect()
        {
            var roots = new List<string>(ScenePaths);
            roots.AddRange(AssetDatabase
                .FindAssets("t:Prefab", PrefabRoots)
                .Select(AssetDatabase.GUIDToAssetPath));

            var matPaths = new HashSet<string>();
            foreach (var dep in AssetDatabase.GetDependencies(roots.ToArray(), true))
                if (dep.EndsWith(".mat")) matPaths.Add(dep);

            var result = new List<MaterialFacts>();
            foreach (var path in matPaths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string shaderName = mat.shader != null ? mat.shader.name : "";
                float smoothness = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness")
                                 : mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness")
                                 : MaterialRule.SmoothnessMatte;
                float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                bool hasEmission = mat.IsKeywordEnabled("_EMISSION");
                Color emission = mat.HasProperty("_EmissionColor")
                    ? mat.GetColor("_EmissionColor") : Color.black;

                result.Add(new MaterialFacts(path, shaderName, smoothness, metallic, hasEmission, emission));
            }
            return result;
        }
    }
}
```

- [ ] **Step 2: 컴파일한다**

uloop `compile`. 기대: 에러 0.

- [ ] **Step 3: 도구를 돌려 현재 위반을 확인한다**

uloop `execute-dynamic-code`로 `Survive.EditorTools.ArtRuleChecker.Run()`을 부르고 결과를 출력한다.

```csharp
using Survive.EditorTools;
using UnityEngine;
Debug.Log(ArtRuleChecker.Run());
```

기대: 위반이 **여러 건 나온다**. 이 시점에 0이면 수집이 안 된 것이므로 `Collect()`를 의심한다. 머티리얼 개수가 26 내외인지 먼저 확인한다.

- [ ] **Step 4: 보고서 원문을 파일로 남긴다**

Task 6에서 무엇을 고칠지의 근거이자, 나중에 "원래 어땠는지"를 아는 유일한 기록이다.

Step 3에서 콘솔에 찍힌 `ArtRuleChecker.Run()` 출력 전문을 **Write 도구로** 아래 경로에 그대로 저장한다. 요약하거나 줄이지 않는다 — Task 6이 이 목록을 하나씩 지운다.

경로: `docs/superpowers/plans/2026-08-03-p0-art-baseline.txt`

맨 위에 한 줄을 붙인다:
```
# ArtRuleChecker 착수 시점 출력 — 2026-08-03, 커밋 <현재 HEAD 해시>
```

- [ ] **Step 5: 커밋**

```bash
git add SurviveProject/Assets/02.Scripts/Editor/ArtRuleChecker.cs docs/superpowers/plans/2026-08-03-p0-art-baseline.txt
git commit -m "P0: 아트 규칙 점검 도구와 착수 시점 위반 목록"
```

---

### Task 3: 포스트프로세싱 스택

**Files:**
- Create: `Assets/08.Data/Art/Volume_Islands.asset` (부유섬)
- Create: `Assets/08.Data/Art/Volume_Surface.asset` (프롤로그 지표)

**Interfaces:**
- Consumes: `Survive.Domain.Art.ArtPalette` (White Balance·Color Adjustments 색 기준)
- Produces: 위 두 `VolumeProfile` 에셋. Task 4·5가 씬의 Global Volume에 물린다

**왜 `DefaultVolumeProfile`을 고치지 않는가**: 구역마다 다른 색이 필요하고, `DefaultVolumeProfile`은 URP 전역 기본값이라 구역 블렌딩에 쓸 수 없다.

- [ ] **Step 1: 프로파일 두 개를 만든다**

uloop `execute-dynamic-code`:

```csharp
using System.IO;
using Survive.Domain.Art;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 폴더는 AssetDatabase로 만든다 (System.IO 금지 규칙)
if (!AssetDatabase.IsValidFolder("Assets/08.Data/Art"))
    AssetDatabase.CreateFolder("Assets/08.Data", "Art");

VolumeProfile MakeProfile(string path, Color tint, float exposure, float saturation, float bloom, float vignette)
{
    var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
    if (p == null)
    {
        p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);
    }

    // Tonemapping — 이것이 없으면 HDR 발광이 전부 흰 덩어리가 된다
    if (!p.TryGet<Tonemapping>(out var tm)) tm = p.Add<Tonemapping>(true);
    tm.mode.overrideState = true;
    tm.mode.value = TonemappingMode.ACES;

    // Bloom — 발광체가 실제로 빛나 보이게 하는 핵심
    if (!p.TryGet<Bloom>(out var bl)) bl = p.Add<Bloom>(true);
    bl.threshold.overrideState = true; bl.threshold.value = 0.8f;
    bl.intensity.overrideState = true; bl.intensity.value = bloom;
    bl.scatter.overrideState = true;   bl.scatter.value = 0.7f;

    // Color Adjustments — 팩 원본의 튀는 채도를 눌러 한 세계로 묶는다
    if (!p.TryGet<ColorAdjustments>(out var ca)) ca = p.Add<ColorAdjustments>(true);
    ca.postExposure.overrideState = true; ca.postExposure.value = exposure;
    ca.saturation.overrideState = true;   ca.saturation.value = saturation;
    ca.contrast.overrideState = true;     ca.contrast.value = 10f;
    ca.colorFilter.overrideState = true;  ca.colorFilter.value = tint;

    // Vignette — 랜턴 시야를 강조한다. 과하면 답답하다
    if (!p.TryGet<Vignette>(out var vg)) vg = p.Add<Vignette>(true);
    vg.intensity.overrideState = true; vg.intensity.value = vignette;
    vg.smoothness.overrideState = true; vg.smoothness.value = 0.4f;

    EditorUtility.SetDirty(p);
    return p;
}

// 부유섬 — 어둡고 대비가 크다. 발광체가 강하게 번져야 한다
MakeProfile("Assets/08.Data/Art/Volume_Islands.asset",
    Color.white, exposure: 0.2f, saturation: -12f, bloom: 1.2f, vignette: 0.28f);

// 지표 — 모래폭풍. 눈이 아플 만큼 밝고 채도가 낮다
MakeProfile("Assets/08.Data/Art/Volume_Surface.asset",
    ArtPalette.FogSurface, exposure: 0.9f, saturation: -25f, bloom: 0.5f, vignette: 0.15f);

AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
Debug.Log("볼륨 프로파일 2개 생성 완료");
```

- [ ] **Step 2: 에셋이 실제로 생겼는지 확인한다**

```bash
ls "E:/SurviveProject/SurviveProject/Assets/08.Data/Art/"
```
기대: `Volume_Islands.asset`, `Volume_Surface.asset`과 각 `.meta`.

- [ ] **Step 3: 프로파일 내용이 비어 있지 않은지 확인한다**

uloop `execute-dynamic-code`:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
foreach (var path in new[]{"Assets/08.Data/Art/Volume_Islands.asset","Assets/08.Data/Art/Volume_Surface.asset"})
{
    var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
    Debug.Log($"{path} — components {p.components.Count}");
}
```
기대: 각 `components 4`. `0`이면 `DefaultVolumeProfile`과 같은 빈 상태이므로 Step 1이 실패한 것이다.

- [ ] **Step 4: 커밋**

```bash
git add SurviveProject/Assets/08.Data/Art
git commit -m "P0: 구역별 포스트프로세싱 프로파일 (부유섬·지표)"
```

---

### Task 4: 지하 조명 기준 — MainScene

**Files:**
- Modify: `Assets/01.Scenes/MainScene.unity` (`RenderSettings` + Global Volume 오브젝트)

**Interfaces:**
- Consumes: `ArtPalette.FogIslands`, `Assets/08.Data/Art/Volume_Islands.asset`
- Produces: 없음 (씬 상태)

**되돌리는 법**: `git checkout -- SurviveProject/Assets/01.Scenes/MainScene.unity`

- [ ] **Step 1: 적용 전 스크린샷을 남긴다**

uloop `screenshot` (Game View). 파일명 `p0-before-mainscene.png`. Task 8의 비교 기준이 된다.

- [ ] **Step 2: 렌더 설정과 Global Volume을 적용한다**

uloop `execute-dynamic-code`:

```csharp
using Survive.Domain.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

var scene = EditorSceneManager.OpenScene("Assets/01.Scenes/MainScene.unity", OpenSceneMode.Single);

// 전역 환경광 — 지하에 태양이 없으니 출처 없는 빛은 존재할 수 없다.
// 다만 0으로 두면 광원 반경 밖이 순수 검정이 되어 판독이 불가능하므로
// 형태만 겨우 읽히는 하한을 남긴다. 이 값은 Task 8 스크린샷으로 조정한다.
RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
RenderSettings.ambientLight = ArtPalette.FogIslands;
RenderSettings.ambientIntensity = 0.15f;

// 포그 — 거리감과 구역 정체성의 주 수단
RenderSettings.fog = true;
RenderSettings.fogMode = FogMode.ExponentialSquared;
RenderSettings.fogColor = ArtPalette.FogIslands;
RenderSettings.fogDensity = 0.018f;

// Global Volume — 없으면 만든다.
// 방금 Single로 연 씬이 활성 씬이므로 새 GameObject는 자동으로 그 씬에 들어간다.
// (MoveGameObjectToScene은 EditorSceneManager가 아니라 SceneManager에 있다. 여기서는 불필요하다)
var existing = GameObject.Find("GlobalVolume_Islands");
if (existing == null) existing = new GameObject("GlobalVolume_Islands");
var vol = existing.GetComponent<Volume>();
if (vol == null) vol = existing.AddComponent<Volume>();
vol.isGlobal = true;
vol.priority = 0f;
vol.weight = 1f;
vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/08.Data/Art/Volume_Islands.asset");
EditorUtility.SetDirty(existing);

EditorSceneManager.MarkSceneDirty(scene);
EditorSceneManager.SaveScene(scene);
Debug.Log($"MainScene 적용 — fog {RenderSettings.fog}, ambient {RenderSettings.ambientIntensity}, profile {(vol.sharedProfile != null ? "연결됨" : "NULL")}");
```

- [ ] **Step 3: 디스크에 실제로 쓰였는지 확인한다**

`SaveOpenScenes()`가 아니라 `SaveScene(scene)`을 썼지만, 그래도 파일을 직접 본다.

```bash
cd E:/SurviveProject && git diff --stat SurviveProject/Assets/01.Scenes/MainScene.unity && grep -E "m_Fog:|m_AmbientIntensity:|m_FogDensity:" SurviveProject/Assets/01.Scenes/MainScene.unity
```
기대: `m_Fog: 1`, `m_AmbientIntensity: 0.15`, `m_FogDensity: 0.018`.

- [ ] **Step 4: 적용 후 스크린샷을 남긴다**

uloop `screenshot` (Game View). 파일명 `p0-after-mainscene.png`.

- [ ] **Step 5: 참조 무결성을 확인한다**

Global Volume의 `sharedProfile`이 끊기면 조용히 아무 효과도 없다.

uloop `execute-dynamic-code`:
```csharp
using Survive.EditorTools;
using UnityEngine;
Debug.Log(ReferenceIntegrityChecker.Run());
```
기대: 신규 null 참조 0건.

- [ ] **Step 6: 커밋**

```bash
git add SurviveProject/Assets/01.Scenes/MainScene.unity
git commit -m "P0: 지하 조명 기준 — 환경광을 내리고 포그를 켰다"
```

---

### Task 5: 지표 조명 기준 — StartScene

**Files:**
- Modify: `Assets/01.Scenes/StartScene.unity`

**Interfaces:**
- Consumes: `ArtPalette.FogSurface`, `Assets/08.Data/Art/Volume_Surface.asset`
- Produces: 없음 (씬 상태)

**지하와 반대로 간다.** 지표에는 태양이 있으므로 전역광이 정당하다. 그 대비 자체가 연출이다 — 눈이 아플 만큼 밝은 지표에서 캄캄한 지하로 떨어진다.

- [ ] **Step 1: 적용 전 스크린샷**

uloop `screenshot`. 파일명 `p0-before-startscene.png`.

- [ ] **Step 2: 지표 렌더 설정을 적용한다**

uloop `execute-dynamic-code`:

```csharp
using Survive.Domain.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

var scene = EditorSceneManager.OpenScene("Assets/01.Scenes/StartScene.unity", OpenSceneMode.Single);

// 지표에는 태양이 있다. 모래폭풍에 산란된 강한 확산광이 맞다.
RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
RenderSettings.ambientLight = ArtPalette.FogSurface;
RenderSettings.ambientIntensity = 1.3f;

// 모래폭풍 — 시야가 막혀 동굴로 들어갈 수밖에 없다
RenderSettings.fog = true;
RenderSettings.fogMode = FogMode.ExponentialSquared;
RenderSettings.fogColor = ArtPalette.FogSurface;
RenderSettings.fogDensity = 0.035f;

// 방금 Single로 연 씬이 활성 씬이므로 새 GameObject는 자동으로 그 씬에 들어간다
var go = GameObject.Find("GlobalVolume_Surface");
if (go == null) go = new GameObject("GlobalVolume_Surface");
var vol = go.GetComponent<Volume>();
if (vol == null) vol = go.AddComponent<Volume>();
vol.isGlobal = true;
vol.priority = 0f;
vol.weight = 1f;
vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/08.Data/Art/Volume_Surface.asset");
EditorUtility.SetDirty(go);

EditorSceneManager.MarkSceneDirty(scene);
EditorSceneManager.SaveScene(scene);
Debug.Log($"StartScene 적용 — fogDensity {RenderSettings.fogDensity}, profile {(vol.sharedProfile != null ? "연결됨" : "NULL")}");
```

- [ ] **Step 3: 디스크 확인**

```bash
cd E:/SurviveProject && grep -E "m_Fog:|m_AmbientIntensity:|m_FogDensity:" SurviveProject/Assets/01.Scenes/StartScene.unity
```
기대: `m_Fog: 1`, `m_AmbientIntensity: 1.3`, `m_FogDensity: 0.035`.

- [ ] **Step 4: 적용 후 스크린샷**

uloop `screenshot`. 파일명 `p0-after-startscene.png`.

- [ ] **Step 5: 커밋**

```bash
git add SurviveProject/Assets/01.Scenes/StartScene.unity
git commit -m "P0: 지표 조명 기준 — 모래폭풍이 시야를 막는다"
```

---

### Task 6: 머티리얼을 규칙에 맞춘다

**Files:**
- Modify: Task 2의 보고서(`docs/superpowers/plans/2026-08-03-p0-art-baseline.txt`)가 지목한 `.mat` 파일들

**Interfaces:**
- Consumes: `ArtRuleChecker.Run()` 보고서
- Produces: `ArtRuleChecker.ViolationCount() == 0`

**주의**: 팩 에셋을 직접 수정한다. 에셋을 **재생성하지 않는다** — GUID가 바뀌면 팩 프리팹 6484개의 참조가 조용히 죽는다. 기존 파일의 값만 바꾼다.

- [ ] **Step 1: EMISSION 4개를 먼저 판단한다**

착수 시점 확인된 것:

| 파일 | 판단 |
|---|---|
| `polyperfect/…/M_Universal_A.mat` | 팩 전체 공유 아틀라스. **발광이 켜져 있으면 6484개 전부가 빛난다.** 발광색이 광원 4색이 아니면 **끈다** |
| `Other Assets/Art/LowPolyRockPack/Materials/Green.mat` | 바위. 자연물이 발광할 이유가 없다 → **끈다** |
| `…/Grey.mat` | 동일 → **끈다** |
| `…/Sand.mat` | 동일 → **끈다** |

발광을 살려야 할 대상(불씨버섯, 매크로늄)은 **전용 머티리얼을 새로 만들어** 붙인다. 공유 머티리얼의 발광을 켜면 팩 전체가 빛난다.

uloop `execute-dynamic-code`:

```csharp
using UnityEditor;
using UnityEngine;

string[] targets =
{
    "Assets/polyperfect/Poly Universal Pack/Materials/M_Universal_A.mat",
    "Assets/Other Assets/Art/LowPolyRockPack/Materials/Green.mat",
    "Assets/Other Assets/Art/LowPolyRockPack/Materials/Grey.mat",
    "Assets/Other Assets/Art/LowPolyRockPack/Materials/Sand.mat",
};

foreach (var path in targets)
{
    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (m == null) { Debug.LogWarning($"없음: {path}"); continue; }
    m.DisableKeyword("_EMISSION");
    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    EditorUtility.SetDirty(m);
    Debug.Log($"발광 끔: {path}");
}
AssetDatabase.SaveAssets();
```

- [ ] **Step 2: 스무스니스를 3단으로 맞춘다**

가장 가까운 밴드로 스냅한다. 판단이 필요한 것은 밴드 선택이 아니라 "이게 금속인가"인데, 그것은 Metallic 목록으로 사람이 본다.

uloop `execute-dynamic-code`:

```csharp
using System.Linq;
using Survive.Domain.Art;
using UnityEditor;
using UnityEngine;

float Snap(float v)
{
    float[] bands = { MaterialRule.SmoothnessMatte, MaterialRule.SmoothnessSemi, MaterialRule.SmoothnessMetal };
    return bands.OrderBy(b => Mathf.Abs(b - v)).First();
}

var roots = new System.Collections.Generic.List<string>
{
    "Assets/01.Scenes/MainScene.unity",
    "Assets/01.Scenes/StartScene.unity",
};
roots.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/05.Prefabs" })
    .Select(AssetDatabase.GUIDToAssetPath));

int changed = 0;
foreach (var dep in AssetDatabase.GetDependencies(roots.ToArray(), true).Where(d => d.EndsWith(".mat")))
{
    var m = AssetDatabase.LoadAssetAtPath<Material>(dep);
    if (m == null) continue;
    string prop = m.HasProperty("_Smoothness") ? "_Smoothness"
                : m.HasProperty("_Glossiness") ? "_Glossiness" : null;
    if (prop == null) continue;

    float cur = m.GetFloat(prop);
    if (MaterialRule.IsBandedSmoothness(cur)) continue;

    float snapped = Snap(cur);
    m.SetFloat(prop, snapped);
    EditorUtility.SetDirty(m);
    changed++;
    Debug.Log($"스무스니스 {cur:0.###} → {snapped}  {dep}");
}
AssetDatabase.SaveAssets();
Debug.Log($"조정한 머티리얼 {changed}개");
```

- [ ] **Step 3: 남은 위반(셰이더)을 확인한다**

셰이더 위반은 자동으로 고칠 수 없다. 치환하면 머티리얼 속성이 날아간다.

uloop `execute-dynamic-code`:
```csharp
using Survive.EditorTools;
using UnityEngine;
Debug.Log(ArtRuleChecker.Run());
```

남은 것이 있으면 둘 중 하나를 선택한다.
- **정당한 예외** → `MaterialRule.AllowedShaders`에 추가하고 **주석에 근거를 적는다** (예: 물 셰이더는 굴절이 필요해 Lit으로 대체 불가)
- **불필요한 팩 셰이더** → `URP/Lit`으로 바꾸고 `_BaseColor`·`_BaseMap`을 손으로 옮긴다

근거 없이 허용 목록에 넣지 않는다. 그러면 도구가 아무것도 막지 못한다.

- [ ] **Step 4: 위반 0을 확인한다**

uloop `execute-dynamic-code`:
```csharp
using Survive.EditorTools;
using UnityEngine;
Debug.Log($"남은 위반 {ArtRuleChecker.ViolationCount()}건");
```
기대: `0건`.

- [ ] **Step 5: 회귀를 확인한다**

uloop `run-tests` (EditMode 전체) + uloop `get-logs`.
기대: 테스트 전부 PASS, 콘솔 에러 0.

- [ ] **Step 6: 커밋**

**`git add -A`를 쓰지 않는다.** 작업 트리에는 내 변경이 아닌 Unity 생성 dirt가 셋 있다
(`Assets/07.Fonts/TMP/ChosunGu SDF.asset`, `ProjectSettings/EditorSettings.asset`,
`ProjectSettings/TimeManager.asset`). 쓸어담으면 이 커밋이 무엇을 바꿨는지 알 수 없게 된다.

`git status --short`로 실제 수정된 `.mat` 경로를 확인한 뒤 그것만 지정한다.

```bash
cd E:/SurviveProject
git status --short | grep '\.mat$'          # 바뀐 머티리얼만 확인
git add <위에서 확인한 .mat 경로들>          # 하나씩 명시한다
git status --short                           # dirt 3개가 스테이징 안 됐는지 재확인
git commit -m "P0: 씬이 쓰는 머티리얼을 규칙에 맞췄다"
```

`MaterialRule.cs`의 허용 셰이더 목록을 수정했다면 그 파일도 함께 add 한다.

---

### Task 7: 추락 구멍 빛기둥

**Files:**
- Create: `Assets/05.Prefabs/Environment/LightShaft.prefab`
- Modify: `Assets/01.Scenes/MainScene.unity` (시작 지점에 배치)

**Interfaces:**
- Consumes: `ArtPalette.LightShaft`
- Produces: 프리팹 경로 `Assets/05.Prefabs/Environment/LightShaft.prefab`. Task 8의 스크린샷 1번 대상

**무엇인가**: 세계관의 "바닥이 무너져내리며 지하로 진입"을 시각화한 것. 시작점이자 랜드마크이며, 올려다보면 보이지만 갈 수 없는 곳이다.

- [ ] **Step 1: 프리팹을 만든다**

`Spot` 라이트를 위에서 아래로 쏜다. 볼류메트릭 셰이더 없이도 포그와 블룸이 이미 켜져 있으므로 빛기둥이 보인다.

uloop `execute-dynamic-code`:

```csharp
using Survive.Domain.Art;
using UnityEditor;
using UnityEngine;

if (!AssetDatabase.IsValidFolder("Assets/05.Prefabs/Environment"))
    AssetDatabase.CreateFolder("Assets/05.Prefabs", "Environment");

var root = new GameObject("LightShaft");

var lightGo = new GameObject("Shaft");
lightGo.transform.SetParent(root.transform, false);
lightGo.transform.localPosition = new Vector3(0f, 40f, 0f);
lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 아래를 향한다

var light = lightGo.AddComponent<Light>();
light.type = LightType.Spot;
light.color = ArtPalette.LightShaft;
light.intensity = 12f;
light.range = 60f;
light.spotAngle = 22f;
light.innerSpotAngle = 12f;
light.shadows = LightShadows.Soft;

var path = "Assets/05.Prefabs/Environment/LightShaft.prefab";
PrefabUtility.SaveAsPrefabAsset(root, path);
Object.DestroyImmediate(root);
AssetDatabase.Refresh();
Debug.Log($"생성: {path}");
```

- [ ] **Step 2: 프리팹이 생겼는지 확인한다**

```bash
ls "E:/SurviveProject/SurviveProject/Assets/05.Prefabs/Environment/"
```
기대: `LightShaft.prefab`, `LightShaft.prefab.meta`.

- [ ] **Step 3: 플레이어 시작 지점에 배치한다**

uloop `execute-dynamic-code`:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

var scene = EditorSceneManager.OpenScene("Assets/01.Scenes/MainScene.unity", OpenSceneMode.Single);

var player = GameObject.Find("Player");
if (player == null) { Debug.LogError("Player를 찾지 못했다. 배치를 중단한다."); }
else
{
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/05.Prefabs/Environment/LightShaft.prefab");
    var existing = GameObject.Find("LightShaft");
    if (existing != null) Object.DestroyImmediate(existing);

    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
    go.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, player.transform.position.z);
    EditorUtility.SetDirty(go);

    EditorSceneManager.MarkSceneDirty(scene);
    EditorSceneManager.SaveScene(scene);
    Debug.Log($"빛기둥 배치: {go.transform.position}");
}
```

- [ ] **Step 4: 스크린샷으로 확인한다**

uloop `screenshot` (Game View). 파일명 `p0-lightshaft.png`.

확인할 것: 빛기둥이 **보이는가**(포그가 꺼져 있으면 안 보인다), 색이 따뜻한 회백인가, 반경 밖이 실제로 어두운가.

- [ ] **Step 5: 참조 무결성 + 회귀**

uloop `execute-dynamic-code`로 `ReferenceIntegrityChecker.Run()`, 이어서 uloop `run-tests`.
기대: 신규 null 0건, 테스트 전부 PASS.

- [ ] **Step 6: 커밋**

```bash
git add SurviveProject/Assets/05.Prefabs/Environment SurviveProject/Assets/01.Scenes/MainScene.unity
git commit -m "P0: 추락 구멍 빛기둥 — 시작점이자 돌아갈 수 없는 곳"
```

---

### Task 8: 검증과 [사람] 게이트

**Files:**
- Create: `docs/superpowers/plans/2026-08-03-p0-verification.md` (스크린샷 목록과 판정 기록)

**Interfaces:**
- Consumes: Task 1~7 전부
- Produces: 사람의 판정. 통과하면 P0가 끝난다

**이 게이트는 자동으로 대신할 수 없다.** 프로그램은 "포그가 켜졌다"까지만 말할 수 있고 "어두운 게 답답한가"는 말할 수 없다.

- [ ] **Step 1: 자동 검증을 전부 돌린다**

```
uloop clear-console
uloop compile          → 에러 0
uloop run-tests        → EditMode 전부 PASS
```

uloop `execute-dynamic-code`:
```csharp
using Survive.EditorTools;
using UnityEngine;
Debug.Log($"아트 규칙 위반 {ArtRuleChecker.ViolationCount()}건");
Debug.Log(ReferenceIntegrityChecker.Run());
```
기대: 위반 0건, 신규 null 0건.

`uloop get-logs` → 콘솔 에러 0.

- [ ] **Step 2: 판정용 스크린샷 4장을 찍는다**

스펙 §11이 지정한 넷이다. 플레이 모드에서 `E2EHarness`로 플레이어를 세우고 `uloop screenshot`을 찍는다.

`uloop control-play-mode`로 Play를 켠 뒤, 각 장면마다 uloop `execute-dynamic-code`로 위치를 잡는다.

```csharp
using Survive.Testing;
using UnityEngine;
// 좌표는 씬에서 대상 오브젝트를 찾아 그 앞에 세운다
var target = GameObject.Find("<대상 오브젝트 이름>");
E2EHarness.Teleport(target.transform.position + new Vector3(0f, 1.6f, -6f));
E2EHarness.LookAt(target.transform.position);
```

`E2EHarness.LookAt`을 반드시 쓴다. 플레이어 회전을 `transform`으로 직접 바꾸면 `PlayerCameraRig`가 매 프레임 `_yaw`로 덮어써서 엉뚱한 방향이 찍힌다.

| # | 장면 | 확인할 것 | 파일명 |
|---|---|---|---|
| 1 | 빛기둥 아래 (시작점) | 밝기의 근거가 보이는가. 랜드마크로 읽히는가 | `p0-verify-1-shaft.png` |
| 2 | 불씨버섯 군락 | 안전한 밝은 곳으로 읽히는가 | `p0-verify-2-glowshroom.png` |
| 3 | 어둠 속, 랜턴만 켠 상태 | **이동이 가능한가.** 답답한가 | `p0-verify-3-lantern.png` |
| 4 | 물가 | 물이 발광하지 않고 반사로만 보이는가 | `p0-verify-4-water.png` |

**매크로늄 액면은 아직 찍을 수 없다.** 해당 지형이 P2에서 만들어진다. 스펙 §11의 4번 항목은 P2로 이월하고, 대신 물만 확인한다. 이 이월을 검증 문서에 적는다.

- [ ] **Step 3: 적용 전후를 나란히 둔다**

Task 4·5에서 찍은 `p0-before-*.png`와 비교한다. 대비가 생겼는지가 핵심이다 — 밝기가 아니라 **밝은 곳과 어두운 곳의 차이**를 본다.

- [ ] **Step 4: 기존 생성 FBX 4개가 실루엣 규칙에 맞는지 점검한다**

스펙 §9는 P0에서 **규칙을 확정하고, 이미 만들어둔 생물 모델이 그 규칙에 맞는지 점검**하라고 한다. 고치는 것은 P3이지만, 어긋난 것을 지금 알아야 P3의 범위가 정해진다.

대상은 `Assets/10.Generated/`의 넷이다.

| 파일 | 도감상 계층 | 규칙상 기대 실루엣 |
|---|---|---|
| `ball.fbx` | 분해자 «공» | 작고 둥글다. 부속 없음 |
| `eye.fbx` | 분해자 «눈» | 작고 둥글다. 부속 없음 |
| `wing.fbx` | 소형 생산자 «날개» | 이동부(팬)가 보이고 저장부가 있다 |
| `fruitcrab.fbx` | 소형 생산자 «열매게» | 4족 + 저장부 |

`uloop screenshot`으로 넷을 한 화면에 나란히 놓고 찍는다. 파일명 `p0-verify-5-silhouettes.png`.

**판정 기준**: 실루엣만 보고 "무해한 것"과 "먹이를 저장한 것"이 구분되는가. 색과 디테일을 지운 검은 실루엣으로 봤을 때도 구분되어야 한다.

어긋난 항목을 검증 문서에 적는다. **여기서 모델을 고치지 않는다** — P3의 작업이다.

- [ ] **Step 5: 검증 문서를 쓴다**

`docs/superpowers/plans/2026-08-03-p0-verification.md`에 다음을 적는다.
- 자동 검증 결과 (테스트 수, 위반 건수, 에러 건수)
- 스크린샷 5장의 경로
- 스펙 §11 4번(매크로늄 액면)을 P2로 이월한 사실과 이유
- 실루엣 점검에서 어긋난 항목 (P3 과제로 넘어간다)
- **사람이 답할 칸을 비워둔다**

```markdown
| 질문 | 답 |
|---|---|
| 랜턴 없이 초반 이동이 가능한가 | |
| 밝은 곳이 특별하게 느껴지는가 | |
| 네 광원이 헷갈리지 않는가 | |
```

- [ ] **Step 6: 정지하고 사람에게 넘긴다**

**여기서 멈춘다.** 스크린샷을 보여주고 위 세 질문에 답을 받는다. 스스로 통과 처리하지 않는다.

답에 따라:
- **통과** → Step 7
- **너무 어둡다** → `RenderSettings.ambientIntensity`(현재 0.15)와 `fogDensity`(현재 0.018)를 조정하고 Step 2부터 다시. **색상값은 건드리지 않는다** — 밝기 문제를 색으로 덮는 것이 게이트 C1에서 한 실수다

- [ ] **Step 7: 커밋**

```bash
git add docs/superpowers/plans/2026-08-03-p0-verification.md
git commit -m "P0: 검증 통과 — 아트 방향 확립 완료"
```

---

## 완료 조건

- [ ] EditMode 테스트 전부 통과 (기존 + 신규)
- [ ] `ArtRuleChecker.ViolationCount() == 0`
- [ ] `ReferenceIntegrityChecker` 신규 null 0건
- [ ] 콘솔 에러 0
- [ ] 두 씬에 포그와 볼륨 프로파일이 물려 있다
- [ ] 빛기둥이 시작 지점에 있고 화면에서 보인다
- [ ] 기존 생성 FBX 4개의 실루엣 점검 결과가 검증 문서에 기록됐다
- [ ] **[사람] 검증 문서의 세 질문에 답이 채워졌고 통과 판정을 받았다**

## 이 플랜에서 하지 않는 것

- 신규 3D 모델 제작 — 기존 에셋의 머티리얼만 다룬다
- 커스텀 셰이더 그래프 — URP 내장으로 해결한다
- 평야·절벽 볼륨 프로파일 — 해당 맵이 없다 (P4·P5)
- 매크로늄 액면 실물 — 지형이 P2에서 만들어진다
- 생물 모델과 실루엣 적용 — P3
- 성능 최적화 — 규칙을 세우는 단계이지 다듬는 단계가 아니다
