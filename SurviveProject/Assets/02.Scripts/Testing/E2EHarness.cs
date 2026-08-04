using System;
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>좌클릭을 누른 채로 버틴다. 꾹 눌러 연속으로 휘두르는지 볼 때 쓴다.</summary>
        public static IEnumerator HoldAttack(float seconds)
        {
            _mouseState.WithButton(MouseButton.Left, true);
            QueueMouse();
            yield return null;

            float t = 0f;
            while (t < seconds)
            {
                QueueMouse();           // 누른 상태를 매 프레임 유지한다
                t += Time.deltaTime;
                yield return null;
            }

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
                                         float timeout = 30f, bool throwOnTimeout = true,
                                         Func<bool> arrived = null)
        {
            float deadline = Time.time + timeout;

            Vector3[] corners = null;
            yield return ResolvePath(Player.transform.position, destination, r => corners = r);

            if (corners.Length > 1)
                Log($"  경로 {corners.Length - 1}구간");

            // 마지막 코너를 뺀 중간 코너들은 통과만 하면 되므로 넉넉히 잡는다
            for (int i = 1; i < corners.Length; i++)
            {
                if (arrived != null && arrived()) yield break;

                bool isLast = i == corners.Length - 1;
                Vector3 leg = isLast ? destination : corners[i];
                float radius = isLast ? arriveRadius : 1.2f;

                yield return WalkLeg(leg, radius, deadline, arrived);
            }

            if (arrived != null && arrived()) yield break;

            var remain = destination - Player.transform.position;
            remain.y = 0f;
            if (remain.magnitude <= arriveRadius) yield break;

            if (throwOnTimeout)
                throw new TimeoutException($"걸어가기 실패: {timeout}초 안에 도착하지 못함 " +
                                           $"(남은 거리 {remain.magnitude:F1}m)");

            Log($"  [도달 실패] {timeout}초 안에 도착하지 못함 (남은 거리 {remain.magnitude:F1}m)");
        }

        /// <summary>물리로 길을 다시 뚫어 볼 최대 거리. 이보다 멀면 비용이 이득을 넘는다.</summary>
        const float TerrainPathMaxDistance = 60f;

        /// <summary>
        /// 걸어갈 길의 꼭짓점들.
        ///
        /// NavMesh를 먼저 묻고, <b>온전한 경로일 때만</b> 쓴다. 부분 경로(PathPartial)는
        /// "여기까지는 갈 수 있다"가 아니라 "이 방향으로 최선을 다했다"에 가깝다 —
        /// 굽고 난 뒤에 놓인 소품(챕터 1 첫 목표 앞의 포탈 장치) 때문에 벽 한가운데를
        /// 가리키는 경우가 있고, 믿고 걸으면 그 벽에 코를 박는다.
        ///
        /// 그럴 때는 지형을 직접 두드려 길을 찾는다. 그것도 실패하면 직선이다 —
        /// 못 가는 것과 안 가 본 것은 다르므로, 일단 걸어 보고 실패는 실패로 남긴다.
        /// </summary>
        static IEnumerator ResolvePath(Vector3 from, Vector3 to, Action<Vector3[]> result)
        {
            var straight = new[] { from, to };

            var flat = to - from;
            flat.y = 0f;
            bool canProbe = flat.magnitude <= TerrainPathMaxDistance;

            if (NavMesh.SamplePosition(from, out var a, 6f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(to, out var b, 6f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete &&
                    path.corners != null && path.corners.Length >= 2)
                {
                    // 온전한 경로라도 그대로 믿지 않는다. NavMesh는 굽고 난 뒤에 놓인
                    // 소품을 모르므로, "온전한 길"이 소품 한가운데를 지날 수 있다.
                    // 사람 몸통이 실제로 들어가는지 구간마다 찍어 본다.
                    if (!canProbe || LegsFitAPerson(path.corners))
                    {
                        result(path.corners);
                        yield break;
                    }
                    Log("  [경로] NavMesh 경로가 소품을 관통한다 — 지형을 직접 두드린다");
                }
            }

            if (!canProbe)
            {
                Log($"  [경로] NavMesh가 온전한 길을 못 냈다. {flat.magnitude:F0}m는 " +
                    "직접 두드리기엔 멀어 직선으로 간다");
                result(straight);
                yield break;
            }

            List<Vector3> terrain = null;
            yield return E2ETerrainPath.Find(from, to, r => terrain = r);

            if (terrain == null || terrain.Count < 2)
            {
                Log("  [경로] 지형을 두드려도 길이 없다. 직선으로 간다");
                result(straight);
                yield break;
            }

            Log($"  [경로] NavMesh 대신 지형에서 {terrain.Count - 1}구간을 찾았다 " +
                $"(셀 {E2ETerrainPath.LastExpandedCells}개, " +
                (E2ETerrainPath.LastPathComplete ? "목표까지" : "갈 수 있는 데까지") + ")");
            result(terrain.ToArray());
        }

        /// <summary>
        /// 경로 위 어디에서나 사람이 서 있을 수 있는가.
        ///
        /// 1m 간격으로 지면을 찾고 그 자리에 몸통이 들어가는지 본다.
        /// 경사는 통과시키고 벽·소품은 걸러 낸다 — 직선 캡슐 캐스트로 검사하면
        /// 오르막마다 걸려 멀쩡한 경로까지 버리게 된다.
        /// </summary>
        static bool LegsFitAPerson(Vector3[] corners)
        {
            var cc = Player.GetComponent<CharacterController>();
            float radius = cc != null ? cc.radius : 0.5f;
            float height = cc != null ? cc.height : 1.8f;
            var mine = new HashSet<Collider>(Player.GetComponentsInChildren<Collider>(true));

            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 from = corners[i - 1], to = corners[i];
                float len = Vector3.Distance(from, to);
                int steps = Mathf.CeilToInt(len);

                // 시작점은 지금 서 있는 자리이므로 건너뛴다
                for (int s = 1; s <= steps; s++)
                {
                    var p = Vector3.Lerp(from, to, s / (float)steps);

                    var hits = Physics.RaycastAll(p + Vector3.up * 6f, Vector3.down, 14f,
                                                  ~0, QueryTriggerInteraction.Ignore);
                    float groundY = float.NaN, nearest = float.MaxValue;
                    for (int h = 0; h < hits.Length; h++)
                    {
                        if (mine.Contains(hits[h].collider) || hits[h].distance >= nearest) continue;
                        nearest = hits[h].distance;
                        groundY = hits[h].point.y;
                    }
                    if (float.IsNaN(groundY)) continue;   // 지면을 못 찾으면 판단을 보류한다

                    var overlap = Physics.OverlapCapsule(
                        new Vector3(p.x, groundY + radius + 0.06f, p.z),
                        new Vector3(p.x, groundY + height - radius + 0.06f, p.z),
                        radius * 0.85f, ~0, QueryTriggerInteraction.Ignore);

                    for (int o = 0; o < overlap.Length; o++)
                        if (!mine.Contains(overlap[o])) return false;
                }
            }
            return true;
        }

        /// <summary>한 구간을 걷는다. 끼면 열린 쪽으로 밀어붙여 빼낸다.</summary>
        static IEnumerator WalkLeg(Vector3 leg, float arriveRadius, float deadline,
                                   Func<bool> arrived = null)
        {
            var rig = Player.CameraRig;

            float nextReport = Time.time + 1f;
            float nextStuckCheck = Time.time + 0.5f;
            Vector3 lastReported = Player.transform.position;
            Vector3 lastStuckPos = lastReported;
            int sidestep = 0;
            _detourSign = 0;

            yield return PressKey(Key.W);

            while (Time.time < deadline)
            {
                if (arrived != null && arrived())
                {
                    yield return ReleaseKey(Key.W);
                    yield break;
                }

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
                        Log($"    막힘 — 우회 {sidestep}회");
                        yield return Unstick(leg, sidestep);
                        lastStuckPos = Player.transform.position;
                        nextStuckCheck = Time.time + 0.5f;
                        continue;
                    }
                }

                yield return null;
            }

            yield return ReleaseKey(Key.W);
        }

        /// <summary>이번 구간에서 고른 우회 방향. 0이면 아직 안 골랐다.</summary>
        static int _detourSign;

        /// <summary>
        /// 낀 상태에서 빠져나온다.
        ///
        /// 전에는 A/D를 번갈아 눌렀다. 그러면 같은 벽 앞을 좌우로 왕복만 한다 —
        /// 챕터 1 첫 목표 앞의 포탈 장치에서 옆걸음 24회를 그렇게 소진하고
        /// 제자리에서 시간이 끝났다. 왕복은 시도 횟수만 늘리고 아무것도 바꾸지 않는다.
        ///
        /// 이제는 실제로 <b>뚫려 있는 방향을 찾아</b> 그쪽으로 밀어붙인다.
        /// 한 번 고른 쪽은 계속 유지하고, 시도가 거듭될수록 더 멀리 간다 —
        /// 장애물을 돌아 나가려면 결국 한쪽으로 충분히 가야 한다.
        /// 낮은 턱은 점프로 넘어가므로 점프도 섞는다.
        /// </summary>
        static IEnumerator Unstick(Vector3 goal, int attempt)
        {
            var rig = Player.CameraRig;
            Vector3 toGoal = goal - Player.transform.position;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 1e-4f) toGoal = Player.transform.forward;
            toGoal.Normalize();

            if (_detourSign == 0) _detourSign = 1;
            // 같은 쪽으로 세 번 밀어도 안 되면 반대쪽이 정답이다
            else if (attempt % 3 == 1) _detourSign = -_detourSign;

            Vector3 detour = PickOpenDirection(toGoal, _detourSign);

            // 뒤로 물러설 곳조차 없으면 점프로라도 턱을 넘어 본다
            if (detour == Vector3.zero)
            {
                yield return TapKey(Key.Space);
                float w = 0f;
                while (w < 0.4f) { QueueKeys(); w += Time.deltaTime; yield return null; }
                yield break;
            }

            rig.SetLook(Mathf.Atan2(detour.x, detour.z) * Mathf.Rad2Deg, 0f);
            yield return PressKey(Key.W);

            if (attempt % 3 == 0) yield return TapKey(Key.Space);

            // 시도가 거듭될수록 더 멀리 — 큰 소품은 몇 미터로는 돌아 나가지지 않는다
            float hold = Mathf.Min(0.9f + 0.5f * attempt, 3.0f);
            float t = 0f;
            while (t < hold)
            {
                rig.SetLook(Mathf.Atan2(detour.x, detour.z) * Mathf.Rad2Deg, 0f);
                QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }

            yield return ReleaseKey(Key.W);
        }

        /// <summary>
        /// 목표 쪽을 향하면서도 실제로 뚫려 있는 방향.
        /// 목표에 가까운 각도부터 훑어 첫 번째로 뚫린 것을 고른다.
        /// 아무 데도 안 뚫렸으면 Vector3.zero.
        /// </summary>
        static Vector3 PickOpenDirection(Vector3 toGoal, int sign)
        {
            var cc = Player.GetComponent<CharacterController>();
            float radius = cc != null ? cc.radius : 0.5f;
            float height = cc != null ? cc.height : 1.8f;

            Vector3 feet = Player.transform.position - Vector3.up * (height * 0.5f - radius);
            Vector3 head = feet + Vector3.up * (height - 2f * radius);

            foreach (float degrees in new[] { 55f, 90f, 125f, 160f })
            {
                var dir = Quaternion.Euler(0f, degrees * sign, 0f) * toGoal;
                float need = Mathf.Lerp(2.5f, 1.2f, degrees / 160f);

                if (!Physics.CapsuleCast(feet, head, radius * 0.9f, dir, out _, need,
                                         ~0, QueryTriggerInteraction.Ignore))
                    return dir;
            }
            return Vector3.zero;
        }

        // ── 볼륨 안으로 들어가기 ─────────────────────────────────

        /// <summary>
        /// 트리거 볼륨 <b>안으로</b> 걸어 들어간다.
        ///
        /// 왜 중심으로 걸어가면 안 되는가: 챕터 1의 탐색 트리거는 44m 폭의 판인데
        /// 그 중심은 포탈 장치 너머 바위 위다. 게임이 요구하는 것은 "판을 밟는 것"이고
        /// 중심에 설 이유는 어디에도 없다. 중심을 목표로 잡으면 밟을 필요도 없는
        /// 바위를 기어오르다 실패하고, 그건 게임의 결함이 아니라 검사의 결함이다.
        ///
        /// 그래서 볼륨 안에서 <b>실제로 설 수 있는 가장 가까운 자리</b>를 골라 걸어가고,
        /// 걷는 도중 볼륨에 들어간 순간 멈춘다.
        /// </summary>
        public static IEnumerator WalkInto(GameObject volume, float timeout = 45f,
                                           bool throwOnTimeout = true)
        {
            if (volume == null) throw new InvalidOperationException("들어갈 볼륨이 없습니다");

            var col = volume.GetComponent<Collider>() ?? volume.GetComponentInChildren<Collider>();
            if (col == null)
            {
                Log($"  {volume.name}에 콜라이더가 없다 — 중심으로 걸어간다");
                yield return WalkTo(volume.transform.position, 3f, timeout, throwOnTimeout);
                yield break;
            }

            var box = col.bounds;
            bool Inside() => box.Contains(Player.transform.position);

            if (Inside())
            {
                Log($"  이미 {volume.name} 안에 있다");
                yield break;
            }

            var spots = StandableSpotsInside(box);
            if (spots.Count == 0)
            {
                Log($"  [배치 문제] {volume.name} 안에 설 수 있는 자리가 없다 — 중심으로 시도한다");
                spots.Add(volume.transform.position);
            }

            Log($"  {volume.name}: 설 수 있는 자리 {spots.Count}곳, " +
                $"가장 가까운 곳 {spots[0].ToString("F1")}");

            float deadline = Time.time + timeout;
            int tried = 0;

            foreach (var spot in spots)
            {
                if (tried++ >= 3 || Time.time >= deadline) break;

                yield return WalkTo(spot, 1.5f, Mathf.Max(4f, deadline - Time.time),
                                    throwOnTimeout: false, arrived: Inside);
                if (Inside()) break;

                Log($"  {spot.ToString("F1")}에 닿지 못했다 — 다음 자리를 본다");
            }

            if (Inside())
            {
                Log($"  {volume.name} 안으로 들어갔다 ({Player.transform.position.ToString("F1")})");
                yield break;
            }

            var d = volume.transform.position - Player.transform.position;
            d.y = 0f;
            string reason = $"볼륨 진입 실패: {volume.name} 안으로 들어가지 못함 " +
                            $"(중심까지 {d.magnitude:F1}m)";
            if (throwOnTimeout) throw new TimeoutException(reason);
            Log("  [도달 실패] " + reason);
        }

        /// <summary>
        /// 볼륨 안에서 사람이 실제로 설 수 있는 자리들. 가까운 순.
        /// 지면을 찾고 몸통이 들어가는지까지 확인한다 — 바위 속은 자리가 아니다.
        /// </summary>
        static List<Vector3> StandableSpotsInside(Bounds box)
        {
            var cc = Player.GetComponent<CharacterController>();
            float radius = cc != null ? cc.radius : 0.5f;
            float height = cc != null ? cc.height : 1.8f;

            var mine = new HashSet<Collider>(Player.GetComponentsInChildren<Collider>(true));
            var from = Player.transform.position;
            var found = new List<Vector3>();

            // 넓은 판은 전부 훑을 이유가 없다. 플레이어 쪽 가장자리부터 촘촘히 본다.
            float step = Mathf.Max(1f, Mathf.Min(box.size.x, box.size.z) / 12f);

            for (float x = box.min.x + step * 0.5f; x <= box.max.x; x += step)
            for (float z = box.min.z + step * 0.5f; z <= box.max.z; z += step)
            {
                var top = new Vector3(x, box.max.y + 1f, z);
                var hits = Physics.RaycastAll(top, Vector3.down, box.size.y + 3f,
                                              ~0, QueryTriggerInteraction.Ignore);
                float groundY = float.NaN, nearest = float.MaxValue;
                for (int i = 0; i < hits.Length; i++)
                {
                    if (mine.Contains(hits[i].collider) || hits[i].distance >= nearest) continue;
                    nearest = hits[i].distance;
                    groundY = hits[i].point.y;
                }
                if (float.IsNaN(groundY)) continue;

                var stand = new Vector3(x, groundY + height * 0.5f, z);
                if (!box.Contains(stand)) continue;

                var overlap = Physics.OverlapCapsule(
                    new Vector3(x, groundY + radius + 0.06f, z),
                    new Vector3(x, groundY + height - radius + 0.06f, z),
                    radius * 0.9f, ~0, QueryTriggerInteraction.Ignore);

                bool blocked = false;
                for (int i = 0; i < overlap.Length; i++)
                    if (!mine.Contains(overlap[i])) { blocked = true; break; }
                if (blocked) continue;

                found.Add(stand);
            }

            // 높이 차이에 벌점을 준다. 몇 미터 옆의 평지를 두고 바로 앞의 바위 위를
            // 고르면, 오를 수 있을지도 모르는 턱을 기어오르느라 시간을 다 쓴다.
            float Cost(Vector3 p) =>
                Vector2.Distance(new Vector2(p.x, p.z), new Vector2(from.x, from.z))
                + Mathf.Abs(p.y - from.y) * 3f;

            found.Sort((a, b) => Cost(a).CompareTo(Cost(b)));
            return found;
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
