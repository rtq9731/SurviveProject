using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Survive.Building;
using Survive.Core;
using Survive.Player;
using Survive.World;

namespace Survive.Vitals
{
    /// <summary>
    /// 죽으면 마지막 화톳불에서, 타는 불이 없으면 착륙선에서, 배도 없으면
    /// 시작 지점에서 다시 일어선다.
    ///
    /// <b>어디로 돌려보낼지는 여기서 정하지 않는다.</b> 그 규칙은
    /// <see cref="Survive.World.RespawnRule"/>(어느 화톳불인가)과
    /// <see cref="Survive.World.HomeBaseRule"/>(화톳불인가 배인가)에 있고
    /// 둘 다 Unity 없이 테스트된다.
    /// 이 컴포넌트가 하는 일은 사망 시점을 잡아 후보를 모아 규칙에 묻고,
    /// 나온 자리로 몸을 옮겨 바이탈을 돌려주는 것뿐이다.
    ///
    /// <b>떨군 것은 건드리지 않는다.</b> 가방은 <see cref="DeathDropService"/>가
    /// 죽은 자리에 세우고 그대로 남는다 — 돌아가서 되찾는 왕복이 사망의 대가이고,
    /// 부활은 그 대가를 지우는 것이 아니라 되찾을 기회를 주는 쪽이다.
    ///
    /// <b>왜 씬에 놓지 않고 스스로 붙는가.</b> <see cref="DeathDropService"/>와 같은
    /// 이유다 — 플레이어 프리팹과 MainScene은 병합할 수 없는 단일 파일이라
    /// 여러 갈래로 나뉘어 일하는 동안 손대지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RespawnService : MonoBehaviour
    {
        static RespawnService _instance;

        public static RespawnService Instance => _instance;

        /// <summary>마지막으로 되살아난 자리. 검증에서 결과를 집기 위한 것이다.</summary>
        public static Vector3 LastRespawnPoint { get; private set; }

        /// <summary>
        /// 마지막 부활이 화톳불이었는가.
        ///
        /// <b>거점이 둘이 되었으므로 false의 뜻이 갈렸다</b> — 시작 지점일 수도 있고
        /// 착륙선일 수도 있다. 어느 쪽인지는 <see cref="LastRespawnSite"/>가 말한다.
        /// 이 손잡이를 남겨 두는 것은 앞서 선 검사들이 이것으로 화톳불을 가리기
        /// 때문이고, 그 물음("화톳불이었나")의 답은 갈리지 않았다.
        /// </summary>
        public static bool LastRespawnWasCampfire { get; private set; }

        /// <summary>마지막으로 일어선 곳의 격. 전진 기지인가 본거지인가 맨바닥인가.</summary>
        public static RespawnSite LastRespawnSite { get; private set; } = RespawnSite.StartPoint;

        /// <summary>부활한 횟수. 사망 한 번에 한 번만 오르는지 보려는 것이다.</summary>
        public static int RespawnCount { get; private set; }

        /// <summary>
        /// 숨이 끊기고 다시 일어설 때까지의 사이.
        ///
        /// 0으로 두면 사망 이벤트가 도는 같은 프레임에 몸이 옮겨진다. 그러면
        /// <see cref="DeathDropService"/>가 "죽은 자리"를 재기 전에 좌표가 바뀔 수 있고
        /// (구독 순서에 달린 문제라 어느 날 조용히 뒤집힌다), 화면에는 죽은 티도 나지 않는다.
        /// 사망 연출은 이 항목의 범위가 아니므로 눈 깜빡할 만큼만 둔다.
        /// </summary>
        const float ReviveDelay = 0.6f;

        /// <summary>불 속이 아니라 불 곁에 선다.</summary>
        const float StandOffFromFire = 1.4f;

        /// <summary>발끝이 아니라 발밑을 찾은 뒤 세울 높이. 하네스의 착지 보정과 같은 값이다.</summary>
        const float FootLift = 1.0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("RespawnService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RespawnService>();
        }

        PlayerVitals _vitals;
        Transform _body;
        Vector3 _startPoint;
        bool _hasStartPoint;
        bool _reviving;

        /// <summary>화톳불이 하나도 없을 때 돌아갈 자리. 씬이 플레이어를 세워 둔 곳이다.</summary>
        public Vector3 StartPoint => _startPoint;

        /// <summary>시작 지점을 실제로 적어 뒀는가. 좌표가 원점일 수도 있어 따로 둔다.</summary>
        public bool HasStartPoint => _hasStartPoint;

        void OnDisable() => Unhook();

        // DeathDropService와 같은 방식으로 플레이어를 잡는다 — 씬을 다시 올리면
        // 잡고 있던 플레이어가 사라지므로, 사라졌으면 다시 잡는다.
        void Update()
        {
            if (_vitals != null) return;

            Unhook();
            if (!GameServices.TryGet<PlayerVitals>(out var found) || found == null) return;

            _vitals = found;
            _vitals.Died += OnDied;
            _body = BodyOf(_vitals);

            // 시작 지점은 여기서 한 번 적는다. 씬이 올라온 첫 프레임의 자리이므로
            // 사람이 걷기 전의 좌표다 — 화톳불을 세우기 전에 죽으면 여기로 돌아온다.
            if (_body != null)
            {
                _startPoint = _body.position;
                _hasStartPoint = true;
            }
        }

        void Unhook()
        {
            if (_vitals != null) _vitals.Died -= OnDied;
            _vitals = null;
            _body = null;
        }

        static Transform BodyOf(PlayerVitals vitals)
        {
            if (vitals == null) return null;
            var ctx = vitals.GetComponentInParent<PlayerContext>();
            return ctx != null ? ctx.transform : vitals.transform;
        }

        void OnDied()
        {
            if (_reviving) return;
            StartCoroutine(ReviveShortly());
        }

        IEnumerator ReviveShortly()
        {
            _reviving = true;

            // 시간을 멈춘 채 화면을 확인하는 검사가 있어 실시간으로 센다.
            yield return new WaitForSecondsRealtime(ReviveDelay);

            // 사이에 누가 되살려 놨으면 그냥 둔다. 살아 있는 사람을 끌고 가면
            // 그쪽이 훨씬 이상한 일이 된다.
            if (_vitals != null && _vitals.Health.IsEmpty) Respawn();

            _reviving = false;
        }

        /// <summary>즉시 부활시킨다. 검증에서 기다림 없이 부르기 위해 열어 둔다.</summary>
        public void Respawn()
        {
            if (_vitals == null) return;
            if (_body == null) _body = BodyOf(_vitals);

            var point = ChooseRespawnPoint(out var site);
            MoveTo(point);
            _vitals.Revive();

            LastRespawnPoint = point;
            LastRespawnSite = site;
            LastRespawnWasCampfire = site == RespawnSite.ForwardCamp;
            RespawnCount++;

            // 자리 이름을 여기 인라인으로 적는다. 따로 뺀 함수에 넣으면 그 한글이
            // 「화면에 나가는 글」로 잡히는데(LocSentenceGateTests), 콘솔 진단문은
            // 사람 눈이 아니라 개발자 눈에 닿는 글이라 표로 옮기면 오히려 손해다.
            Debug.Log($"[RespawnService] 부활 — " +
                      $"{(site == RespawnSite.ForwardCamp ? "마지막 화톳불" : site == RespawnSite.Home ? "착륙선" : "시작 지점")} " +
                      $"{point.ToString("F1")}");
        }

        Vector3 ChooseRespawnPoint(out RespawnSite site)
        {
            var fires = Object.FindObjectsByType<Campfire>(FindObjectsInactive.Exclude);
            var anchors = new List<RespawnAnchor>(fires.Length);
            for (int i = 0; i < fires.Length; i++)
            {
                var f = fires[i];
                // 자리는 불꽃(LitZoneCenter)이 아니라 화톳불이 선 발밑을 쓴다.
                anchors.Add(new RespawnAnchor(f.transform.position, f.KindledAt, f.IsBurning));
            }

            // 아직 플레이어를 한 번도 못 잡았다면 옮길 곳을 모르므로 지금 자리를 쓴다.
            var fallback = _hasStartPoint ? _startPoint
                                          : (_body != null ? _body.position : Vector3.zero);

            site = HomeBaseRule.Choose(anchors, LanderBase.CurrentHome(), fallback,
                                       out var point, out int chosen);

            // 불 곁에 서는 보정은 화톳불에만 건다. 배는 자기가 설 자리를 이미 알고
            // 있고(LanderBase.HomeStandPoint), 화톳불처럼 사방으로 물러설 수도 없다 —
            // 배 뒤쪽은 액면일 수 있다.
            return site == RespawnSite.ForwardCamp
                       ? BesideTheFire(fires[chosen].transform.position)
                       : (site == RespawnSite.Home ? OnTheGround(point) : point);
        }

        /// <summary>배가 알려 준 자리를 지면 위로 내려놓는다.</summary>
        static Vector3 OnTheGround(Vector3 spot)
        {
            if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out var hit, 12f,
                                ~0, QueryTriggerInteraction.Ignore))
                spot.y = hit.point.y + FootLift;

            return spot;
        }

        /// <summary>불 속이 아니라 불 곁, 그리고 지면 위에 세운다.</summary>
        Vector3 BesideTheFire(Vector3 fire)
        {
            Vector3 away = _body != null ? _body.position - fire : Vector3.zero;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;

            var spot = fire + away.normalized * StandOffFromFire;

            if (Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out var hit, 12f,
                                ~0, QueryTriggerInteraction.Ignore))
                spot.y = hit.point.y + FootLift;

            return spot;
        }

        /// <summary>CharacterController를 잠깐 끄지 않으면 옮긴 자리에서 밀려난다.</summary>
        void MoveTo(Vector3 where)
        {
            if (_body == null) return;

            var cc = _body.GetComponentInChildren<CharacterController>(true);
            if (cc != null) cc.enabled = false;
            _body.position = where;
            if (cc != null) cc.enabled = true;
        }
    }
}
