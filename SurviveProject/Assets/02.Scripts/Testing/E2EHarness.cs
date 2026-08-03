using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
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
        public static StringBuilder LogBuffer { get; } = new StringBuilder();

        public static void Log(string line)
        {
            LogBuffer.AppendLine(line);
            Debug.Log("[E2E] " + line);
        }

        public static void ClearLog() => LogBuffer.Clear();

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
        public static void Teleport(Vector3 pos)
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
        public static void LookAt(Vector3 worldPos)
        {
            var rig = Player.CameraRig;
            if (rig == null) throw new InvalidOperationException("PlayerCameraRig가 없습니다");
            rig.LookAt(worldPos);
        }

        /// <summary>대상 앞 지정 거리에 서서 대상을 바라본다.</summary>
        public static IEnumerator StandInFrontOf(Transform target, float distance = 2.0f)
        {
            Vector3 d = Player.transform.position - target.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) d = Vector3.back;

            Vector3 standAt = target.position + d.normalized * distance;

            // 지면에 발을 붙인다
            if (Physics.Raycast(standAt + Vector3.up * 30f, Vector3.down, out var hit, 200f,
                                ~0, QueryTriggerInteraction.Ignore))
                standAt.y = hit.point.y + 1.0f;

            Teleport(standAt);
            yield return null;          // 카메라가 따라올 프레임을 준다
            LookAt(target.position);
            yield return null;
            yield return null;          // Cinemachine이 실제로 반영되는 데 한 프레임 더
        }

        // ── 입력 ─────────────────────────────────────────────────

        static KeyboardState _keys;
        static Keyboard _device;

        /// <summary>
        /// 하네스 전용 가상 키보드.
        ///
        /// 실제 키보드에 상태를 주입하면 안 된다. Unity의 네이티브 입력 백엔드가
        /// 매 프레임 진짜 키보드 상태(전부 뗌)를 같은 디바이스에 밀어 넣기 때문에,
        /// 주입한 키가 프레임에 따라 살기도 하고 죽기도 한다 — 걷기처럼 매 프레임
        /// 다시 넣는 동작은 그럭저럭 되지만 탭이나 홀드는 산발적으로 실패한다.
        /// 백엔드가 건드리지 않는 별도 디바이스를 만들어 거기에만 넣는다.
        /// </summary>
        static Keyboard Device
        {
            get
            {
                // 여기서 _keys를 초기화하면 안 된다. PressKey는 키를 먼저 세우고
                // 나서 큐잉하므로, 그때 디바이스가 만들어지면서 방금 세운 키가 지워진다.
                if (_device == null || !_device.added)
                    _device = InputSystem.AddDevice<Keyboard>("E2EKeyboard");
                return _device;
            }
        }

        /// <summary>현재 키 상태를 큐에 넣는다. 처리는 Unity의 정상 업데이트에 맡긴다.</summary>
        public static void QueueKeys() => InputSystem.QueueStateEvent(Device, _keys);

        // ── 가상 마우스 ──────────────────────────────────────────
        // 공격은 좌클릭에 묶여 있다. 키보드만으로는 곡괭이를 휘두를 수 없어
        // 마우스도 같은 방식으로 만든다.

        static Mouse _mouse;
        static MouseState _mouseState;

        static Mouse MouseDevice
        {
            get
            {
                if (_mouse == null || !_mouse.added)
                    _mouse = InputSystem.AddDevice<Mouse>("E2EMouse");
                return _mouse;
            }
        }

        static void QueueMouse() => InputSystem.QueueStateEvent(MouseDevice, _mouseState);

        /// <summary>좌클릭 한 번. 공격은 눌린 순간에 발동한다.</summary>
        public static IEnumerator ClickAttack()
        {
            _mouseState.WithButton(MouseButton.Left, true);
            QueueMouse();
            yield return null;
            QueueMouse();
            yield return null;

            _mouseState.WithButton(MouseButton.Left, false);
            QueueMouse();
            yield return null;
            QueueMouse();
            yield return null;
        }

        /// <summary>시나리오가 끝나면 가상 입력 장치를 치운다.</summary>
        public static void RemoveDevice()
        {
            if (_device != null && _device.added) InputSystem.RemoveDevice(_device);
            _device = null;
            _keys = new KeyboardState();

            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
            _mouseState = new MouseState();
        }

        static IEnumerator SendKeyState()
        {
            QueueKeys();
            yield return null;
            // 큐잉한 상태가 실제로 반영되는 데 한 프레임이 더 필요할 수 있다
            QueueKeys();
            yield return null;
        }

        public static IEnumerator PressKey(Key key)
        {
            _keys.Set(key, true);
            yield return SendKeyState();
        }

        public static IEnumerator ReleaseKey(Key key)
        {
            _keys.Set(key, false);
            yield return SendKeyState();
        }

        /// <summary>지정 시간 동안 키를 누르고 있는다. 채집처럼 홀드가 필요한 동작에 쓴다.</summary>
        public static IEnumerator HoldKey(Key key, float seconds)
        {
            yield return PressKey(key);

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                // 누른 상태를 유지한다. 매 프레임 다시 보내지 않으면 떨어진다.
                QueueKeys();
                yield return null;
            }

            yield return ReleaseKey(key);
        }

        public static IEnumerator TapKey(Key key)
        {
            yield return PressKey(key);
            yield return null;
            yield return ReleaseKey(key);
        }

        public static IEnumerator ReleaseAllKeys()
        {
            _keys = new KeyboardState();
            yield return SendKeyState();
        }

        // ── 이동 ─────────────────────────────────────────────────

        /// <summary>
        /// 목표 지점까지 실제로 걸어간다. 플래그를 코드로 세우는 것과 달리
        /// 트리거·콜라이더 같은 실제 조건을 전부 통과해야 한다.
        ///
        /// 직선으로만 걸으면 바위 하나에 영원히 막힌다 — 실제로 그랬다.
        /// NavMesh로 경로를 뽑아 구간별로 걷고, 그래도 끼면 옆걸음으로 뺀다.
        /// </summary>
        /// <summary>
        /// 마지막 WalkTo가 도착했는지. TryWalkTo가 이 값을 남긴다.
        /// 코루틴은 값을 돌려줄 수 없어서 이렇게 둔다.
        /// </summary>
        public static bool LastWalkArrived { get; private set; }

        /// <summary>
        /// 못 가도 예외를 던지지 않는 걸어가기.
        /// 노드 하나에 갇힌 것만으로 검증 전체가 죽으면, 나머지를 못 본다.
        /// 부르는 쪽이 LastWalkArrived를 보고 넘어갈지 정한다.
        /// </summary>
        public static IEnumerator TryWalkTo(Vector3 destination, float arriveRadius = 2.0f, float timeout = 30f)
        {
            LastWalkArrived = false;
            yield return WalkTo(destination, arriveRadius, timeout, throwOnTimeout: false);

            var d = destination - Player.transform.position;
            d.y = 0f;
            LastWalkArrived = d.magnitude <= arriveRadius + 0.6f;
        }

        public static IEnumerator WalkTo(Vector3 destination, float arriveRadius = 2.0f,
                                         float timeout = 30f, bool throwOnTimeout = true)
        {
            var corners = PathCorners(Player.transform.position, destination);
            float deadline = Time.time + timeout;

            if (corners.Length > 1)
                Log($"  경로 {corners.Length - 1}구간");

            // 마지막 코너를 뺀 중간 코너들은 통과만 하면 되므로 넉넉히 잡는다
            for (int i = 1; i < corners.Length; i++)
            {
                bool isLast = i == corners.Length - 1;
                Vector3 leg = isLast ? destination : corners[i];
                float radius = isLast ? arriveRadius : 1.2f;

                yield return WalkLeg(leg, radius, deadline);
            }

            var remain = destination - Player.transform.position;
            remain.y = 0f;
            if (remain.magnitude <= arriveRadius) yield break;

            if (throwOnTimeout)
                throw new TimeoutException($"걸어가기 실패: {timeout}초 안에 도착하지 못함 " +
                                           $"(남은 거리 {remain.magnitude:F1}m)");

            Log($"  [도달 실패] {timeout}초 안에 도착하지 못함 (남은 거리 {remain.magnitude:F1}m)");
        }

        /// <summary>
        /// NavMesh 경로의 꼭짓점들. 못 뽑으면 시작점과 목표점만 돌려준다.
        /// 굽는 NavMesh가 없거나 목표가 메시 밖일 수 있으므로 실패해도 진행한다.
        /// </summary>
        static Vector3[] PathCorners(Vector3 from, Vector3 to)
        {
            var fallback = new[] { from, to };

            if (!NavMesh.SamplePosition(from, out var a, 6f, NavMesh.AllAreas)) return fallback;
            if (!NavMesh.SamplePosition(to, out var b, 6f, NavMesh.AllAreas)) return fallback;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)) return fallback;
            if (path.corners == null || path.corners.Length < 2) return fallback;

            return path.corners;
        }

        /// <summary>한 구간을 걷는다. 끼면 옆걸음과 점프로 빼낸다.</summary>
        static IEnumerator WalkLeg(Vector3 leg, float arriveRadius, float deadline)
        {
            var rig = Player.CameraRig;

            float nextReport = Time.time + 1f;
            float nextStuckCheck = Time.time + 0.5f;
            Vector3 lastReported = Player.transform.position;
            Vector3 lastStuckPos = lastReported;
            int sidestep = 0;

            yield return PressKey(Key.W);

            while (Time.time < deadline)
            {
                var current = Player.transform.position;
                Vector3 d = leg - current;
                d.y = 0f;

                if (d.magnitude <= arriveRadius)
                {
                    yield return ReleaseKey(Key.W);
                    Log($"  걸어감: 도착 (거리 {d.magnitude:F1}m)");
                    yield break;
                }

                rig.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0f);
                QueueKeys();

                // 왜 매 초 기록하는가: 실패했을 때 "안 움직였다 / 막혔다 / 엉뚱한 데로 갔다"를
                // 구별하지 못하면 고칠 수가 없다. 남은 거리 하나만으로는 알 수 없다.
                if (Time.time >= nextReport)
                {
                    Log($"    남은 {d.magnitude:F1}m, 직전 1초 이동 " +
                        $"{Vector3.Distance(current, lastReported):F2}m, 위치 {current.ToString("F1")}");
                    lastReported = current;
                    nextReport = Time.time + 1f;
                }

                if (Time.time >= nextStuckCheck)
                {
                    bool stuck = Vector3.Distance(current, lastStuckPos) < 0.15f;
                    lastStuckPos = current;
                    nextStuckCheck = Time.time + 0.5f;

                    if (stuck)
                    {
                        sidestep++;
                        Log($"    막힘 — 옆걸음 {sidestep}회");
                        yield return Unstick(sidestep);
                        lastStuckPos = Player.transform.position;
                        nextStuckCheck = Time.time + 0.5f;
                        continue;
                    }
                }

                yield return null;
            }

            yield return ReleaseKey(Key.W);
        }

        /// <summary>
        /// 낀 상태에서 빠져나온다. 좌우를 번갈아 시도하고 점프도 섞는다 —
        /// 낮은 턱이면 점프로, 옆이 막힌 바위면 옆걸음으로 풀린다.
        /// </summary>
        static IEnumerator Unstick(int attempt)
        {
            Key side = attempt % 2 == 1 ? Key.A : Key.D;

            _keys.Set(side, true);
            QueueKeys();

            if (attempt % 3 == 0) yield return TapKey(Key.Space);

            float t = 0f;
            while (t < 0.7f)
            {
                QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }

            _keys.Set(side, false);
            yield return SendKeyState();
        }

        // ── 대기와 단언 ──────────────────────────────────────────

        public static IEnumerator WaitUntil(Func<bool> condition, string what, float timeout = 10f)
        {
            float t = 0f;
            while (t < timeout)
            {
                if (condition()) yield break;
                t += Time.deltaTime;
                yield return null;
            }
            throw new TimeoutException($"기다리기 실패: {what} ({timeout}초 초과)");
        }

        public static void Assert(bool condition, string what)
        {
            if (!condition) throw new Exception("단언 실패: " + what);
            Log("  OK  " + what);
        }

        public static void AssertEqual(object actual, object expected, string what)
        {
            if (!Equals(actual, expected))
                throw new Exception($"단언 실패: {what} — 기대 {expected}, 실제 {actual}");
            Log($"  OK  {what} = {actual}");
        }
    }
}
