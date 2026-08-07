using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survive.World
{
    /// <summary>
    /// 씬에 놓인 광원 중 <b>주인 없는 센 것</b>을 찾아 밝은 구역으로 세운다.
    ///
    /// <b>고친 것.</b> 시작 지점의 빛기둥(스폿 1200/60)이 <see cref="LitZoneRegistry"/>에
    /// 없었다. 그 일대는 화면이 환한데 규칙은 어둡다고 답했고, 그 위에서 어둠 감각을
    /// 재면 잰 값이 통째로 오염된다.
    ///
    /// <b>씬을 고치지 않는다.</b> MainScene은 병합할 수 없는 단일 파일이라 여러 갈래가
    /// 동시에 손대면 한쪽을 버려야 한다. <see cref="GlowGroveService"/>가 발광 군락에
    /// 하는 일을 그대로 따른다 — 실행할 때마다 씬을 훑어 스스로 붙는다.
    ///
    /// <b>무엇을 세우지 않는가.</b> 세 가지로 거른다.
    /// <list type="number">
    /// <item><b>주인이 있는 빛</b> — 화톳불·랜턴·발광 군락은 조상 어딘가에
    ///       <see cref="ILitZoneSource"/>를 이미 달고 있다. 그쪽은 연료와 전원을 알고
    ///       있으므로 여기서 덮으면 꺼진 불이 계속 밝은 구역으로 남는다</item>
    /// <item><b>Directional</b> — 자리가 없는 전역광이라 구역이 될 수 없다</item>
    /// <item><b>약한 빛</b> — <see cref="FixedLightRule.IsZoneWorthy"/>. 낙하물 표식·
    ///       매크로늄 석영·장식 버섯은 <b>눈에 보이라고</b> 놓은 것이지 안전 지대가 아니다.
    ///       그것들이 포식자를 막으면 플레이어는 버섯 옆에 서서 밤을 넘긴다</item>
    /// </list>
    /// </summary>
    public static class FixedLightZoneService
    {
        /// <summary>마지막으로 세운 구역 수. 검증에서 집는다.</summary>
        public static int InstalledZones { get; private set; }

        /// <summary>마지막으로 훑은 광원 수. 「몇 개 중 몇 개」를 보고할 때 쓴다.</summary>
        public static int ScannedLights { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // 두 번 걸리지 않게 먼저 뗀다. 도메인 리로드를 끄고 재생하면 static 구독이
            // 살아남아 같은 씬에 두 번 붙을 수 있다(GlowGroveService와 같은 사정).
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Build();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Build();

        /// <summary>
        /// 지금 열려 있는 씬을 훑어 구역을 세운다. 이미 선 것은 건드리지 않는다.
        /// </summary>
        /// <returns>세운(또는 이미 서 있던) 구역 수.</returns>
        public static int Build()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            ScannedLights = lights.Length;
            InstalledZones = 0;

            var built = new List<string>();
            foreach (var light in lights)
            {
                if (!Qualifies(light)) continue;

                var zone = light.GetComponent<FixedLightZone>();
                if (zone == null) zone = light.gameObject.AddComponent<FixedLightZone>();
                else zone.Measure();

                if (!zone.HasZone) continue;

                InstalledZones++;
                // 조각에 한글을 섞지 않는다. 이 파일은 화면 코드 범위 안이라
                // Debug.Log 바깥의 한글 리터럴이 게이트에 걸린다(LocSentenceGateTests).
                built.Add($"{light.name}({light.type} {light.intensity:0.#}/{light.range:0.#}) " +
                          $"-> {zone.LitZoneCenter.ToString("F1")} r{zone.LitZoneRadius:F1}m");
            }

            if (built.Count > 0)
                Debug.Log($"[FixedLightZoneService] 광원 {ScannedLights}개 중 {InstalledZones}개를 " +
                          $"밝은 구역으로 세웠습니다: {string.Join(" · ", built)}");
            return InstalledZones;
        }

        /// <summary>
        /// 이 광원이 밝은 구역이 될 자격이 있는가.
        /// <b>공개해 둔 이유</b>는 검증이 "무엇이 걸러졌는가"를 하나씩 물어볼 수 있어야
        /// 하기 때문이다 — 세운 것보다 <b>세우지 않은 것</b>이 이 규칙의 본문이다.
        /// </summary>
        public static bool Qualifies(Light light)
        {
            if (light == null) return false;

            bool on = light.enabled && light.gameObject.activeInHierarchy;
            return FixedLightRule.ShouldRegister(light.type, on, HasOwner(light),
                                                 light.intensity, light.range);
        }

        /// <summary>
        /// 이 빛에 이미 주인이 있는가 — 제 몸이나 조상 어딘가에 밝은 구역을 내는 것이 있는가.
        /// <see cref="FixedLightZone"/> 자신은 주인으로 세지 않는다. 그러면 한 번 붙은 뒤로
        /// 자격을 잃은 광원(세기를 낮춘)이 영원히 구역으로 남는다.
        /// </summary>
        static bool HasOwner(Light light)
        {
            for (var t = light.transform; t != null; t = t.parent)
            {
                var components = t.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                    if (components[i] is ILitZoneSource && !(components[i] is FixedLightZone))
                        return true;
            }
            return false;
        }
    }
}
