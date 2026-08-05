using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Survive.Harvesting;

namespace Survive.World
{
    /// <summary>
    /// 씬에 이미 놓여 있는 발광 버섯 군락에 채집 노드와 밝기 규칙을 실행 시점에 붙인다.
    ///
    /// <b>왜 씬을 고치지 않는가.</b> MainScene은 병합할 수 없는 단일 파일이라
    /// 여러 갈래로 나뉘어 일하는 동안 손대지 않는다 — <see cref="MacroniumContactService"/>·
    /// <see cref="Survive.Vitals.RespawnService"/>와 같은 이유다. 대신 실행할 때마다
    /// 씬을 훑어 스스로 붙는다. 사람이 §8-4에서 군락을 더 놓거나 옮겨도
    /// 이 코드는 그대로 따라간다.
    ///
    /// <b>무엇을 군락으로 보는가.</b> <see cref="LightZone"/>이 하나 있으면 군락 하나다.
    /// 씬에는 발광 버섯 메시가 여기저기 흩어져 있지만(Chapter1_Flora의 장식),
    /// <b>배터리를 채워 주는 자리</b>만이 게임이 "거점"으로 약속한 곳이고
    /// (챕터 1 목표 2 "발광 버섯 군락을 찾는다"), 그 약속을 되돌리는 것이 이 기능이다.
    /// 장식용 버섯까지 딸 수 있게 하면 무엇이 거점인지 읽히지 않는다.
    ///
    /// <b>어느 갓이 어느 군락인가.</b> 군락 구역과 같은 부모 아래 있는 버섯 메시 중
    /// <b>가장 가까운 구역</b>에 붙인다. 계층 순서로 묶으면 사람이 오브젝트를
    /// 옮기거나 정렬하는 순간 조용히 틀어진다.
    /// </summary>
    public static class GlowGroveService
    {
        /// <summary>갓 무더기로 볼 오브젝트 이름의 앞머리. 씬의 버섯 메시가 전부 이 꼴이다.</summary>
        public const string CapNamePrefix = "Mushroom";

        /// <summary>군락 구역에서 이 거리 안에 있는 라이트를 그 군락의 빛으로 본다.</summary>
        const float LightSearchRadius = 8f;

        /// <summary>마지막으로 세운 군락 수. 검증에서 집는다.</summary>
        public static int InstalledGroves { get; private set; }

        /// <summary>마지막으로 붙인 갓 무더기 수.</summary>
        public static int InstalledCaps { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // 두 번 걸리지 않게 먼저 뗀다. 도메인 리로드를 끄고 재생하면
            // static 구독이 살아남아 같은 씬에 두 번 붙을 수 있다.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Build();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Build();

        /// <summary>
        /// 지금 열려 있는 씬을 훑어 군락을 세운다. 이미 세워진 것은 건드리지 않는다.
        /// </summary>
        /// <returns>세운(또는 이미 서 있던) 군락 수.</returns>
        public static int Build()
        {
            var zones = Object.FindObjectsByType<LightZone>();
            InstalledGroves = 0;
            InstalledCaps = 0;
            if (zones.Length == 0) return 0;

            // 구역 → 군락. 라이트를 못 찾은 구역은 빠진다.
            var groves = new Dictionary<LightZone, GlowGrove>();
            foreach (var zone in zones)
            {
                var light = FindGroveLight(zone);
                if (light == null)
                {
                    Debug.LogWarning($"[GlowGroveService] '{zone.name}' 곁에서 군락의 빛을 찾지 못했습니다.", zone);
                    continue;
                }

                var grove = light.GetComponent<GlowGrove>();
                if (grove == null) grove = light.gameObject.AddComponent<GlowGrove>();

                groves[zone] = grove;
                zone.BindGrove(grove);
                InstalledGroves++;
            }
            if (groves.Count == 0) return 0;

            // 후보 갓 무더기 — 군락 구역들과 같은 부모 아래 있는 버섯 메시.
            var parents = new HashSet<Transform>();
            foreach (var zone in groves.Keys)
                if (zone.transform.parent != null) parents.Add(zone.transform.parent);

            foreach (var parent in parents)
            {
                foreach (Transform child in parent)
                {
                    if (!child.name.StartsWith(CapNamePrefix)) continue;
                    if (child.GetComponent<GlowCapCluster>() != null) { InstalledCaps++; continue; }

                    var nearest = Nearest(groves, child.position);
                    if (nearest == null) continue;

                    var cap = child.gameObject.AddComponent<GlowCapCluster>();
                    cap.Bind(nearest);
                    InstalledCaps++;
                }
            }

            foreach (var grove in groves.Values) grove.Refresh();

            Debug.Log($"[GlowGroveService] 발광 버섯 군락 {InstalledGroves}곳에 갓 {InstalledCaps}무더기를 붙였습니다.");
            return InstalledGroves;
        }

        /// <summary>
        /// 구역 곁의 빛을 찾는다. 같은 부모 아래에서 가장 가까운 Point 라이트다.
        ///
        /// 플레이어의 랜턴이나 다른 군락의 빛을 잡지 않도록 같은 부모로 좁히고
        /// 거리도 제한한다.
        /// </summary>
        static Light FindGroveLight(LightZone zone)
        {
            var parent = zone.transform.parent;
            if (parent == null) return null;

            Light best = null;
            float bestSqr = LightSearchRadius * LightSearchRadius;

            foreach (Transform child in parent)
            {
                var light = child.GetComponent<Light>();
                if (light == null || light.type != LightType.Point) continue;

                float d = (child.position - zone.transform.position).sqrMagnitude;
                if (d > bestSqr) continue;
                bestSqr = d;
                best = light;
            }
            return best;
        }

        static GlowGrove Nearest(Dictionary<LightZone, GlowGrove> groves, Vector3 position)
        {
            GlowGrove best = null;
            float bestSqr = float.MaxValue;

            foreach (var pair in groves)
            {
                float d = (pair.Key.transform.position - position).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = pair.Value;
            }
            return best;
        }
    }
}
