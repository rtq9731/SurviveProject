using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Survive.Player;

namespace Survive.Testing
{
    /// <summary>
    /// E2E 검증의 기본 동작들.
    ///
    /// 왜 테스트 어셈블리가 아니라 여기 있는가:
    /// PlayMode 테스트 어셈블리는 Assembly-CSharp를 참조할 수 없는데,
    /// 검증 대상인 MonoBehaviour가 전부 거기 있다. 그래서 하네스도 같은
    /// 어셈블리에 두고 uloop 동적 코드로 구동한다.
    ///
    /// 입력은 uloop CLI가 아니라 InputSystem에 직접 넣는다. 외부 호출은
    /// 프레임 타이밍을 보장할 수 없어 검증이 흔들린다.
    /// </summary>
    public static class E2EHarness
    {
        public static StringBuilder Log { get; } = new StringBuilder();

        public static void 기록(string 줄)
        {
            Log.AppendLine(줄);
            Debug.Log("[E2E] " + 줄);
        }

        public static void 로그비우기() => Log.Clear();

        // ── 대상 찾기 ────────────────────────────────────────────

        public static PlayerContext Player
        {
            get
            {
                var p = UnityEngine.Object.FindFirstObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
                if (p == null) throw new InvalidOperationException("PlayerContext를 찾지 못했습니다");
                return p;
            }
        }

        public static Camera Eye
        {
            get
            {
                var c = Camera.main;
                if (c == null) throw new InvalidOperationException("Camera.main이 없습니다");
                return c;
            }
        }

        // ── 배치 ─────────────────────────────────────────────────

        /// <summary>플레이어를 순간이동시킨다. CharacterController를 잠깐 끄지 않으면 밀린다.</summary>
        public static void 순간이동(Vector3 pos)
        {
            var p = Player;
            var cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            p.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        /// <summary>
        /// 지정 좌표를 바라본다.
        /// transform.rotation을 돌리면 PlayerCameraRig가 덮어쓰므로 SetLook 경로를 쓴다.
        /// </summary>
        public static void 바라보기(Vector3 worldPos)
        {
            var rig = Player.CameraRig;
            if (rig == null) throw new InvalidOperationException("PlayerCameraRig가 없습니다");
            rig.LookAt(worldPos);
        }

        /// <summary>대상 앞 지정 거리에 서서 대상을 바라본다.</summary>
        public static IEnumerator 앞에서기(Transform target, float 거리 = 2.0f)
        {
            Vector3 d = Player.transform.position - target.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) d = Vector3.back;

            Vector3 서는곳 = target.position + d.normalized * 거리;

            // 지면에 발을 붙인다
            if (Physics.Raycast(서는곳 + Vector3.up * 30f, Vector3.down, out var hit, 200f,
                                ~0, QueryTriggerInteraction.Ignore))
                서는곳.y = hit.point.y + 1.0f;

            순간이동(서는곳);
            yield return null;          // 카메라가 따라올 프레임을 준다
            바라보기(target.position);
            yield return null;
            yield return null;          // Cinemachine이 실제로 반영되는 데 한 프레임 더
        }

        // ── 입력 ─────────────────────────────────────────────────

        static KeyboardState _keys;

        static IEnumerator 키상태보내기()
        {
            InputSystem.QueueStateEvent(Keyboard.current, _keys);
            InputSystem.Update();
            yield return null;
        }

        public static IEnumerator 키누르기(Key key)
        {
            _keys.Set(key, true);
            yield return 키상태보내기();
        }

        public static IEnumerator 키떼기(Key key)
        {
            _keys.Set(key, false);
            yield return 키상태보내기();
        }

        /// <summary>지정 시간 동안 키를 누르고 있는다. 채집처럼 홀드가 필요한 동작에 쓴다.</summary>
        public static IEnumerator 키홀드(Key key, float seconds)
        {
            yield return 키누르기(key);

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                // 누른 상태를 유지한다. 매 프레임 다시 보내지 않으면 떨어질 수 있다.
                InputSystem.QueueStateEvent(Keyboard.current, _keys);
                yield return null;
            }

            yield return 키떼기(key);
        }

        public static IEnumerator 키탭(Key key)
        {
            yield return 키누르기(key);
            yield return null;
            yield return 키떼기(key);
        }

        public static IEnumerator 모든키떼기()
        {
            _keys = new KeyboardState();
            yield return 키상태보내기();
        }

        // ── 이동 ─────────────────────────────────────────────────

        /// <summary>
        /// 목표 지점까지 실제로 걸어간다. 플래그를 코드로 세우는 것과 달리
        /// 트리거·콜라이더·NavMesh 같은 실제 조건을 전부 통과해야 한다.
        /// </summary>
        public static IEnumerator 걸어가기(Vector3 목표, float 도착반경 = 2.0f, float 제한시간 = 30f)
        {
            float t = 0f;
            var rig = Player.CameraRig;

            yield return 키누르기(Key.W);

            while (t < 제한시간)
            {
                var 현재 = Player.transform.position;
                Vector3 d = 목표 - 현재;
                d.y = 0f;

                if (d.magnitude <= 도착반경)
                {
                    yield return 키떼기(Key.W);
                    기록($"  걸어감: 도착 ({t:F1}초, 거리 {d.magnitude:F1}m)");
                    yield break;
                }

                // 매 프레임 목표 쪽으로 방향을 맞춘다
                rig.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);

                InputSystem.QueueStateEvent(Keyboard.current, _keys);
                t += Time.deltaTime;
                yield return null;
            }

            yield return 키떼기(Key.W);
            throw new TimeoutException($"걸어가기 실패: {제한시간}초 안에 도착하지 못함 " +
                                       $"(남은 거리 {(목표 - Player.transform.position).magnitude:F1}m)");
        }

        // ── 대기와 단언 ──────────────────────────────────────────

        public static IEnumerator 기다리기(Func<bool> 조건, string 무엇, float 제한시간 = 10f)
        {
            float t = 0f;
            while (t < 제한시간)
            {
                if (조건()) yield break;
                t += Time.deltaTime;
                yield return null;
            }
            throw new TimeoutException($"기다리기 실패: {무엇} ({제한시간}초 초과)");
        }

        public static void 단언(bool 조건, string 무엇)
        {
            if (!조건) throw new Exception("단언 실패: " + 무엇);
            기록("  OK  " + 무엇);
        }

        public static void 단언같음(object 실제, object 기대, string 무엇)
        {
            if (!Equals(실제, 기대))
                throw new Exception($"단언 실패: {무엇} — 기대 {기대}, 실제 {실제}");
            기록($"  OK  {무엇} = {실제}");
        }
    }
}
