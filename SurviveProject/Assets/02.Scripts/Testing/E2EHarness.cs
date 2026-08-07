using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Survive.Core;
using Survive.Creatures;
using Survive.Player;
using Survive.World;
using Debug = UnityEngine.Debug;

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
                var p = UnityEngine.Object.FindAnyObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
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

        /// <summary>
        /// 방금 옮긴 트랜스폼을 물리에 <b>즉시</b> 반영한다.
        ///
        /// 이 프로젝트는 <c>Physics.autoSyncTransforms = false</c>다(DynamicsManager).
        /// 트랜스폼을 옮겨도 다음 물리 틱이 돌기 전까지 <c>Collider.bounds</c>도,
        /// 조준에 쓰는 레이·스피어캐스트도 <b>옛 자리</b>를 본다. 검사는 대상을
        /// 눈앞으로 옮기고 곧바로 겨누므로 그 한 틱이 통째로 어긋난다.
        ///
        /// 더 나쁜 것은 <c>position += 겨냥 - bounds.center</c> 꼴의 이동이다.
        /// 옛 중심으로 델타를 재면 이미 한 번 적용한 이동이 두 번 들어가
        /// 대상이 엉뚱한 곳으로 날아간다 — 챕터1 완주 검사가 세 번에 한 번만
        /// 통과하던 이유가 이것이었다.
        /// </summary>
        public static void SyncPhysics() => Physics.SyncTransforms();

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

        // ── 진짜 입력 장치 떼어 놓기 ─────────────────────────────

        static readonly List<InputDevice> _muted = new List<InputDevice>();
        static bool _isolated;

        /// <summary>
        /// 검사가 도는 동안 <b>진짜</b> 키보드·마우스를 떼어 놓는다.
        ///
        /// <b>이것이 없으면 무슨 일이 벌어졌는가.</b> 에디터 창이 앞에 없을 때
        /// 진짜 장치의 상태는 <b>마지막으로 알던 값에 얼어붙는다</b>. 실제로 이 리포에서는
        /// 키보드의 W와 마우스 왼쪽 버튼이 눌린 채로 굳어 있었다. 그러면
        /// <list type="bullet">
        /// <item>마우스 델타가 매 프레임 시선을 돌려 <see cref="LookAt"/>로 겨눈 것이
        ///   다음 프레임에 빗나가고,</item>
        /// <item>공격 버튼이 이미 "눌림"이라 가상 클릭이 새 입력으로 읽히지 않으며,</item>
        /// <item>걷기는 아무 데로나 밀린다.</item>
        /// </list>
        /// 오래 "지형 결함"으로 적혀 있던 E2E 실패들(E2EChapter1·E2EWalkthrough의
        /// 첫 구간 끼임)이 실은 이것이었다 — 플레이어는 끼어 있던 것이 아니라
        /// 애초에 입력을 받지 못했다.
        ///
        /// <b>왜 포커스 설정만으로는 부족한가.</b> 포커스를 무시하게 만들면
        /// 가상 장치의 입력은 통하지만, 같은 설정이 얼어붙은 진짜 장치의 상태까지
        /// 함께 통하게 한다. 그래서 둘 다 해야 한다 — 포커스를 무시하고,
        /// 진짜 장치는 재우고.
        /// </summary>
        public static void IsolateInput()
        {
            if (_isolated) return;
            _isolated = true;

            // 포커스 설정은 여기서 건드리지 않는다. 재생 중에 InputSettings 에셋을 쓰면
            // Unity가 에셋 변경으로 보고 도메인을 다시 올리면서 <b>재생 모드가 꺼진다</b>
            // (실측 확인). 그 설정은 재생에 들어가기 전에 편집 모드에서 맞춰 두어야 하고,
            // 그 일은 Tools/Survive/E2E 입력 포커스 우회가 맡는다.
            if (!FocusBypassed)
                Debug.LogWarning("[E2EHarness] 게임 뷰가 앞에 없으면 입력이 전달되지 않습니다. " +
                                 "재생 전에 Survive.EditorTools.E2EPlayModeInput.Enable()을 " +
                                 "부르거나 메뉴 Tools/Survive/E2E 입력 포커스 우회를 켜십시오.");

            _muted.Clear();
            foreach (var d in InputSystem.devices)
            {
                if (!(d is Keyboard || d is Mouse)) continue;
                if (d.name != null && d.name.StartsWith("E2E")) continue;   // 우리 것은 남긴다
                if (!d.enabled) continue;

                InputSystem.ResetDevice(d);     // 굳어 있던 상태를 먼저 푼다
                InputSystem.DisableDevice(d);
                _muted.Add(d);
            }
        }

        /// <summary>포커스 우회가 켜져 있는가.</summary>
        public static bool FocusBypassed =>
            InputSystem.settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus;

        /// <summary>떼어 놓았던 진짜 장치를 돌려준다. 사람이 다시 만질 수 있어야 한다.</summary>
        public static void RestoreInput()
        {
            if (!_isolated) return;
            _isolated = false;

            foreach (var d in _muted)
                if (d != null && d.added) InputSystem.EnableDevice(d);
            _muted.Clear();
        }

        // ── 저장 슬롯 격리 ───────────────────────────────────────
        //
        // 진짜 장치를 떼어 놓는 것과 같은 종류의 일이다. 저장 경로
        // (LocalLow/DefaultCompany/SurviveProject)는 이 기계의 에디터 전부와
        // 사람이 직접 플레이하는 창까지 공유한다. 검사가 챕터 목표를 하나 넘기면
        // 자동 저장이 걸리고, 그것이 사람의 이어하기를 그대로 덮는다 —
        // 2026-08-07에 save_auto.json이 커진 것을 보고 알아냈다.
        //
        // 규칙 자체는 SaveSlots(Domain)에 있고 여기 있는 것은 그것을 켜고 끄는 손,
        // 그리고 자기 슬롯을 치우는 뒷정리다.

        static string _isolatedSlot;

        /// <summary>지금 검사가 쓰고 있는 슬롯. 격리 중이 아니면 null.</summary>
        public static string IsolatedSlot => _isolatedSlot;

        /// <summary>
        /// 이 에디터의 세션 번호. <b>프로세스 아이디를 쓴다</b> — 클론 셋이 동시에
        /// 돌면 슬롯 이름까지 같아져 서로의 검사를 밟기 때문이다.
        /// </summary>
        public static int SessionId => Process.GetCurrentProcess().Id;

        /// <summary>파일 자리. 슬롯 이름을 풀이하지 않고 <b>준 이름 그대로</b> 본다.</summary>
        public static string SlotPath(string slot) =>
            Path.Combine(Application.persistentDataPath, SaveSlots.FileNameOf(slot));

        /// <summary>
        /// 파일 지문 — <b>있는가 · 몇 바이트인가 · 언제 썼는가</b>를 한 줄로.
        ///
        /// 시나리오 앞뒤로 이것을 재서 같으면 「한 바이트도 안 썼다」가 성립한다.
        /// 크기만 재면 같은 크기로 덮어쓴 경우를 놓치므로 쓴 시각을 함께 담는다.
        /// </summary>
        public static string Fingerprint(string slot)
        {
            var path = SlotPath(slot);
            if (!File.Exists(path)) return "없음";

            var info = new FileInfo(path);
            return $"{info.Length}바이트 @{info.LastWriteTimeUtc.Ticks}";
        }

        /// <summary>
        /// 지금부터 저장은 이 세션의 전용 슬롯으로 간다.
        ///
        /// <b>자동 저장을 끄지 않는다.</b> 끄면 안전해지지만 그 순간 자동 저장이
        /// 어느 검사도 지나가지 않는 길이 된다 — 사각지대를 만들어 안전을 사는 셈이다.
        /// 저장은 그대로 돌리고 <b>쓰는 자리만</b> 옮긴다.
        /// </summary>
        public static void IsolateSave()
        {
            if (_isolatedSlot != null) return;

            _isolatedSlot = SaveSlots.IsolatedNameFor(SessionId);
            SaveSlots.Isolate(_isolatedSlot);

            // 앞 실행이 재생 강제 종료 따위로 뒷정리를 못 했을 수 있다.
            // 시나리오는 빈 자리에서 시작해야 한다.
            DeleteSlotFile(_isolatedSlot);
        }

        /// <summary>격리를 풀고 <b>자기 슬롯을 지운다.</b> 검사가 남긴 파일은 검사가 치운다.</summary>
        public static void ReleaseSave()
        {
            if (_isolatedSlot == null) return;

            DeleteSlotFile(_isolatedSlot);
            _isolatedSlot = null;
            SaveSlots.Release();
        }

        static void DeleteSlotFile(string slot)
        {
            try
            {
                var path = SlotPath(slot);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                // 지우지 못한 것으로 검사를 세울 일은 아니다. 다만 조용히 넘기면
                // 다음 시나리오가 앞 판의 저장본 위에서 시작한 이유를 알 수 없다.
                Debug.LogWarning($"[E2EHarness] 격리 슬롯 '{slot}'을 지우지 못했다: {e.Message}");
            }
        }

        // ── 세계 격리 ────────────────────────────────────────────
        //
        // 입력을 떼어 놓는 것만으로는 부족하다. 씬은 검사가 도는 동안에도 살아 있어서,
        // <b>그 자리에 무엇이 있었는가</b>가 판정에 그대로 섞여 들어온다. 여기 있는 것들은
        // 그 섞임을 시나리오가 명시적으로 걷어 낼 수 있게 하는 도구다.
        //
        // 전부 <b>되돌릴 수 있게</b> 만든다 — 사람이 재생을 이어서 만질 수 있어야 하고,
        // 같은 시나리오 안에서 다시 켜고 확인하는 단계가 있을 수 있다.

        static readonly List<CreatureBrain> _asleep = new List<CreatureBrain>();
        static readonly List<MonoBehaviour> _mutedZones = new List<MonoBehaviour>();

        /// <summary>
        /// 씬에 원래 살고 있던 생물들을 <b>재운다</b>.
        ///
        /// <b>왜 필요한가.</b> 생물 시나리오는 대개 개체 하나를 불러 놓고 그 하나의
        /// 상태·거리·속도를 잰다. 그런데 씬에는 이미 일곱 마리가 배회하고 있고,
        /// 그들은 검사가 옮겨 놓은 플레이어 주위로 몰려와 몸을 밀고(콜라이더),
        /// 근접 공격의 전방 원뿔을 가로채고, 죽으면 전리품을 떨궈 "떨어진 것" 계수를
        /// 흔든다. 어느 것도 재려던 것이 아니다.
        ///
        /// 재우는 방식은 <b>두뇌를 끄고 이동을 멈추는 것</b>이다. 개체를 지우거나
        /// 멀리 옮기지 않는다 — 지우면 되돌릴 수 없고, 옮기면 NavMesh 밖으로 떨어져
        /// 깨워도 다시 걷지 못하는 개체가 생긴다(실제로 겪을 수 있는 함정이다).
        /// 컴포넌트만 끄면 상태도 자리도 그대로 남고, <see cref="WakeWildCreatures"/>가
        /// 켜는 순간 하던 대로 돌아간다.
        /// </summary>
        /// <param name="except">재우지 않을 개체들. 시나리오가 부른 주인공을 넘긴다.</param>
        /// <returns>이번에 재운 개체 수.</returns>
        public static int SleepWildCreatures(params GameObject[] except)
        {
            int slept = 0;
            foreach (var brain in UnityEngine.Object.FindObjectsByType<CreatureBrain>())
            {
                if (brain == null || !brain.enabled) continue;
                if (except != null && System.Array.IndexOf(except, brain.gameObject) >= 0) continue;
                if (_asleep.Contains(brain)) continue;

                brain.enabled = false;

                // 두뇌만 꺼서는 멈추지 않는다. NavMeshAgent는 마지막으로 받은 목적지를
                // 향해 계속 걷고, FlyerMotor도 마찬가지다. 몸에도 멈추라고 말한다.
                var agent = brain.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    agent.isStopped = true;

                var flyer = brain.GetComponent<FlyerMotor>();
                if (flyer != null) flyer.Stop();

                _asleep.Add(brain);
                slept++;
            }
            return slept;
        }

        /// <summary>재운 생물을 전부 깨운다. 재운 적이 없으면 아무 일도 하지 않는다.</summary>
        public static void WakeWildCreatures()
        {
            for (int i = 0; i < _asleep.Count; i++)
            {
                var brain = _asleep[i];
                if (brain == null) continue;

                brain.enabled = true;
                var agent = brain.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    agent.isStopped = false;
            }
            _asleep.Clear();
        }

        /// <summary>
        /// 세계에 깔려 있는 <b>주변 광원</b>을 밝은 구역 목록에서 잠시 뺀다.
        ///
        /// <b>이것이 없어서 무슨 일이 벌어졌는가.</b> 소비자 시나리오는 "랜턴을 켜면
        /// 포식자가 물러난다"를 확인한다. 그런데 씬에는 반경 11m짜리 발광 버섯 군락이
        /// 셋 있고 그 원들이 시작 지점 주변을 거의 덮는다. 검사가 플레이어를 감지 반경
        /// 안으로 옮기면 그 자리가 <b>이미 밝은</b> 경우가 있고, 그러면 낫은 랜턴이
        /// 꺼져 있는데도 다가오지 않는다(<c>LightVerdict.Blocked</c>). 실측된 실패가
        /// 정확히 이것이었다 — 플레이어 (16.4, 53.7, 1.1), 군락 중심에서 9.6m,
        /// <c>playerLit=True</c>, 낫은 7.2m 앞에서 Wander. 자리에 따라 갈리므로
        /// 다섯 번에 한 번만 통과했다.
        ///
        /// 플레이어의 랜턴은 건드리지 않는다. 시나리오가 켜고 끄면서 재려는 것이
        /// 바로 그 광원이고, 그것까지 빼면 확인할 것이 남지 않는다.
        /// </summary>
        /// <param name="except">그대로 둘 광원들. 랜턴은 넘기지 않아도 언제나 남는다.</param>
        /// <returns>이번에 뺀 광원 수.</returns>
        public static int MuteAmbientLitZones(params MonoBehaviour[] except)
        {
            int muted = 0;
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                                   FindObjectsInactive.Exclude))
            {
                if (mb == null || !mb.enabled) continue;
                var source = mb as ILitZoneSource;
                if (source == null) continue;
                if (mb is LanternController) continue;
                if (except != null && System.Array.IndexOf(except, mb) >= 0) continue;
                if (_mutedZones.Contains(mb)) continue;

                // 컴포넌트를 끄면 OnDisable이 스스로 등록을 뺀다(화톳불·군락 둘 다 그렇다).
                // 그 규약에 기대지 않고 한 번 더 빼 둔다 — 등록만 하고 해제는 하지 않는
                // 구현이 나중에 생겨도 격리는 성립해야 한다.
                mb.enabled = false;
                LitZoneRegistry.Unregister(source);

                _mutedZones.Add(mb);
                muted++;
            }
            return muted;
        }

        /// <summary>뺐던 주변 광원을 돌려놓는다. 켜지는 순간 스스로 다시 등록한다.</summary>
        public static void RestoreAmbientLitZones()
        {
            for (int i = 0; i < _mutedZones.Count; i++)
                if (_mutedZones[i] != null) _mutedZones[i].enabled = true;
            _mutedZones.Clear();
        }

        /// <summary>
        /// <paramref name="from"/>에서 <paramref name="distance"/>쯤 떨어진 자리 중,
        /// <b>거기까지 막히지 않고 거의 곧게 걸어올 수 있는</b> 자리를 고른다.
        ///
        /// <b>왜 그냥 NavMesh에 묻고 끝내면 안 되는가.</b> 생물 시나리오는 대부분
        /// "N미터 떨어져 서서 무엇이 일어나는지 본다"는 모양이다. 그런데 그 자리를
        /// <see cref="NavMesh.SamplePosition"/>만으로 정하면, 걸을 수 있는 면 위이긴 해도
        /// 생물과 그 사이에 바위가 통째로 끼어 있을 수 있다. 그러면 생물은 제대로
        /// 판단하고 제 속도로 달리는데도
        /// <list type="bullet">
        /// <item>추격 속도가 0.60 m/s로 찍히고(바위에 몸이 눌린다),</item>
        /// <item>"다가온다"를 재는 창에서 오히려 멀어진다(12.1m → 13.9m — 돌아가는 중이다).</item>
        /// </list>
        /// 둘 다 실측된 실패다. 재려던 것은 판단과 속도이지 길찾기가 아니므로,
        /// 잴 수 있는 자리를 골라 서는 것이 옳다.
        ///
        /// 원하는 방향에 가까운 각도부터 훑어 <b>첫 번째로 통과하는</b> 자리를 쓴다.
        /// 대개는 0도가 바로 통과하므로 경로 계산은 한 번뿐이다.
        /// </summary>
        /// <param name="from">기준점. 대개 생물의 자리다.</param>
        /// <param name="preferredDirection">되도록 이쪽. 수평 성분만 쓴다.</param>
        /// <param name="distance">기준점에서 떨어질 거리.</param>
        /// <param name="spot">고른 자리.</param>
        /// <param name="straightness">허용할 경로 길이 배수. 1.35면 35%까지 돌아가도 된다.</param>
        public static bool TryFindClearSpot(Vector3 from, Vector3 preferredDirection, float distance,
                                            out Vector3 spot, float straightness = 1.35f)
        {
            preferredDirection.y = 0f;
            if (preferredDirection.sqrMagnitude < 1e-4f) preferredDirection = Vector3.forward;
            preferredDirection.Normalize();

            spot = from + preferredDirection * distance;

            float sampleRadius = Mathf.Clamp(distance * 0.35f, 0.8f, 4f);
            var path = new NavMeshPath();

            for (int i = 0; i < 24; i++)
            {
                float degrees = (i % 2 == 0 ? 1f : -1f) * 15f * ((i + 1) / 2);
                Vector3 dir = Quaternion.Euler(0f, degrees, 0f) * preferredDirection;

                if (!NavMesh.SamplePosition(from + dir * distance, out var hit, sampleRadius,
                                            NavMesh.AllAreas)) continue;

                // 표본이 기준점 쪽으로 끌려오면 거리 자체가 판정의 전제인 검사가 무너진다.
                float actual = Vector3.Distance(hit.position, from);
                if (actual < distance * 0.8f || actual > distance * 1.25f) continue;

                if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path)) continue;
                if (path.status != NavMeshPathStatus.PathComplete) continue;
                if (PathLength(path) > actual * straightness) continue;

                spot = hit.position;
                return true;
            }
            return false;
        }

        static float PathLength(NavMeshPath path)
        {
            var corners = path.corners;
            if (corners == null || corners.Length < 2) return 0f;

            float total = 0f;
            for (int i = 1; i < corners.Length; i++)
                total += Vector3.Distance(corners[i - 1], corners[i]);
            return total;
        }

        /// <summary>
        /// 생물 하나가 <b>지금 왜 그러고 있는지</b>를 한 줄로 적는다.
        ///
        /// "쫓고 있는데 안 움직인다"는 그 자체로는 아무것도 알려 주지 않는다.
        /// 길이 끊긴 것인지(<c>pathStatus</c>), 멈추라는 표시가 남은 것인지
        /// (<c>isStopped</c>), 가려는 마음은 있는데 몸이 막힌 것인지
        /// (<c>desired</c>는 큰데 <c>vel</c>이 0) — 셋은 고치는 자리가 전부 다르다.
        /// </summary>
        public static string Describe(CreatureBrain creature)
        {
            if (creature == null) return "(없음)";

            var agent = creature.GetComponent<NavMeshAgent>();
            string body = agent == null || !agent.isOnNavMesh
                ? "NavMesh 밖"
                : $"길 {agent.pathStatus}, 멈춤 {agent.isStopped}, 남은 {agent.remainingDistance:F1}m, " +
                  $"속도 {agent.velocity.magnitude:F2}(원함 {agent.desiredVelocity.magnitude:F2})";

            return $"{creature.State} @ {creature.transform.position.ToString("F1")} — {body}";
        }

        /// <summary>
        /// 격리해 둔 것을 전부 되돌린다.
        ///
        /// 시나리오가 도중에 실패해도 세계는 원래대로 돌아와야 한다 —
        /// 그래서 <see cref="E2ERunner"/>가 성공·실패 양쪽 끝에서 이것을 부른다.
        /// 시나리오 쪽에서 먼저 되돌려 놓았으면 아무 일도 하지 않는다.
        /// </summary>
        public static void RestoreWorld()
        {
            WakeWildCreatures();
            RestoreAmbientLitZones();

            // 경계 등급은 이제 월드가 하나로 들고 있다(스펙 §20). 정적 하나이므로
            // 발령을 걸어 본 시나리오가 그것을 남기면 <b>다음 시나리오가 발령 상태로
            // 시작한다</b> — 낫이 육지로 올라오고 꼬리가 처음부터 올라가 있다.
            // 개체마다 값을 들고 있던 시절에는 개체를 치우면 같이 사라지던 것이라,
            // 소유권을 옮긴 이 라운드에 되돌릴 자리도 함께 옮긴다.
            Survive.Creatures.ScytheWatch.Reset();
        }

        // ── 랜턴 ────────────────────────────────────────────────
        //
        // 불이 없는 상태에 이르는 길은 둘이다 — 껐거나(F, 검토회신 ②), 다
        // 태웠거나. 여기 있는 것은 뒤엣길이다.
        //
        // 스위치가 돌아온 뒤에도 이쪽을 그대로 두는 이유: 대부분의 시나리오가
        // 원하는 것은 "어두운 자리"이고, 배터리를 태우는 길은 그 자리를 만들면서
        // 동시에 <b>셀 자동 교체</b>(TryInsertBatteryCell)까지 지난다. 스위치로
        // 바꾸면 그 경로가 어느 시나리오에서도 안 돌게 된다. 스위치 쪽 왕복은
        // E2ELantern·E2EDiveLantern이 F를 실제로 눌러서 따로 본다.

        public static LanternController Lantern =>
            UnityEngine.Object.FindAnyObjectByType<LanternController>(FindObjectsInactive.Include);

        /// <summary>
        /// 랜턴을 끈다 — 정확히는 배터리를 다 쓴다.
        ///
        /// 여분 셀을 지니고 있으면 0이 되는 순간 저절로 갈아 끼워지므로
        /// (<see cref="LanternController.TryInsertBatteryCell"/>) 셀도 함께 치운다.
        /// 재려는 것이 어둠인데 세계가 알아서 불을 되살리면 아무것도 재지 못한다.
        /// </summary>
        /// <returns>실제로 불이 꺼졌는가. 랜턴이 아예 없으면 이미 꺼진 것이므로 true.</returns>
        public static bool DarkenLantern()
        {
            // Player는 못 찾으면 던진다. 여기서는 인벤토리가 없어도 할 일이 남으므로 조용히 찾는다.
            var owner = UnityEngine.Object.FindAnyObjectByType<PlayerContext>(FindObjectsInactive.Exclude);
            var inv = owner != null && owner.Inventory != null ? owner.Inventory.Inventory : null;
            if (inv != null)
            {
                int cells = inv.CountOf(LanternController.BatteryCellId);
                if (cells > 0) inv.TryRemove(LanternController.BatteryCellId, cells);
            }

            var lamp = Lantern;
            if (lamp == null) return true;

            lamp.Drain(LanternRule.MaxBattery * 2f);
            return !lamp.IsOn;
        }

        /// <summary>
        /// 랜턴에 불이 들어오게 한다 — 정확히는 배터리를 채운다.
        /// 랜턴을 지니고 있지 않으면 채워도 불은 안 들어온다(그것이 규칙이다).
        /// </summary>
        public static bool LightLantern()
        {
            var lamp = Lantern;
            if (lamp == null) return false;

            lamp.Recharge(LanternRule.MaxBattery);
            return lamp.IsOn;
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
        /// 굽고 난 뒤에 놓인 소품(스폰 둘레의 버섯 기둥 같은 것) 때문에 벽 한가운데를
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
            Vector3 lastFree = lastReported;
            int sidestep = 0, frozen = 0;
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

                // 매 프레임 W를 다시 세운다. 우회 처리가 키를 떼고 돌아오면
                // 이후로는 영원히 제자리다 — 실제로 그렇게 49번을 헛돌았다.
                _keys.Set(Key.W, true);
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
                    if (!stuck) lastFree = current;   // 마지막으로 실제로 걷던 자리
                    lastStuckPos = current;
                    nextStuckCheck = Time.time + 0.5f;

                    if (stuck)
                    {
                        sidestep++;
                        Log($"    막힘 — 우회 {sidestep}회");

                        var beforeDetour = Player.transform.position;
                        yield return Unstick(leg, sidestep);

                        // 우회해도 한 발짝도 못 움직이면 낀 게 아니라 박힌 것이다.
                        // 남은 시간을 다 태워도 결과는 같으니 여기서 접고 알린다.
                        frozen = Vector3.Distance(beforeDetour, Player.transform.position) < 0.2f
                               ? frozen + 1 : 0;
                        if (frozen >= 6)
                        {
                            yield return ReleaseKey(Key.W);
                            Log($"    [끼임] {current.ToString("F1")}에서 우회 6회 연속 무효 — " +
                                $"마지막으로 걷던 자리 {lastFree.ToString("F1")}로 되돌린다");
                            // 박힌 자리에 그대로 두면 이후 모든 걸음이 여기서 죽는다.
                            // 되돌리되 반드시 남긴다 — 여기서 낀다는 사실이 곧 지형 정보다.
                            Teleport(lastFree);
                            yield break;
                        }

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
        /// 챕터 1 첫 목표 앞을 막고 있던 외계 구조물(지금은 걷어냈다) 앞에서
        /// 옆걸음 24회를 그렇게 소진하고 제자리에서 시간이 끝났다.
        /// 왕복은 시도 횟수만 늘리고 아무것도 바꾸지 않는다.
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

            // 시도가 거듭될수록 조금씩 더 멀리 — 큰 소품은 몇 미터로는 돌아 나가지지
            // 않는다. 다만 너무 멀리 가면 목표를 잃고 헤매게 되므로 상한을 둔다.
            float hold = Mathf.Min(0.9f + 0.35f * attempt, 1.8f);
            float t = 0f;
            while (t < hold)
            {
                rig.SetLook(Mathf.Atan2(detour.x, detour.z) * Mathf.Rad2Deg, 0f);
                QueueKeys();
                t += Time.deltaTime;
                yield return null;
            }

            // W는 떼지 않는다. 부르는 쪽(WalkLeg)이 계속 누른 상태를 전제로 돈다.
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
        /// 그 중심은 스폰에서 한참 떨어진 바위 위다. 게임이 요구하는 것은 "판을 밟는 것"이고
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

            float deadline = Time.time + timeout;

            // 먼저 "이 볼륨 안 아무 데나"를 목표로 길을 찾는다. 점 하나를 정해 놓고
            // 안 되면 다른 점을 잡는 식은, 볼륨 안 수백 곳 중 어디가 걸어서 닿는
            // 곳인지 걸어 봐야 알기 때문에 시간만 쓰고 자주 실패한다.
            List<Vector3> route = null;
            yield return E2ETerrainPath.FindInto(Player.transform.position, box, r => route = r);

            if (route != null && route.Count > 1)
            {
                Log($"  {volume.name}까지 지형에서 {route.Count - 1}구간 " +
                    $"(셀 {E2ETerrainPath.LastExpandedCells}개, " +
                    (E2ETerrainPath.LastPathComplete ? "볼륨 안까지" : "갈 수 있는 데까지") + ")");

                for (int i = 1; i < route.Count && !Inside() && Time.time < deadline; i++)
                    yield return WalkLeg(route[i], 1.0f, deadline, Inside);
            }

            // 길을 못 찾았거나 도중에 막혔으면, 설 수 있는 자리들을 차례로 노려 본다.
            if (!Inside())
            {
                var spots = StandableSpotsInside(box);
                if (spots.Count == 0)
                {
                    Log($"  [배치 문제] {volume.name} 안에 설 수 있는 자리가 없다 — 중심으로 시도한다");
                    spots.Add(volume.transform.position);
                }
                Log($"  {volume.name}: 설 수 있는 자리 {spots.Count}곳, " +
                    $"가장 가까운 곳 {spots[0].ToString("F1")}");

                // 남은 시간을 셋으로 나눈다. 첫 자리가 시간을 다 쓰면
                // 나머지는 이름만 시도가 된다.
                for (int i = 0; i < 3 && i < spots.Count && !Inside(); i++)
                {
                    float share = Mathf.Max(6f, (deadline - Time.time) / (3 - i));
                    yield return WalkTo(spots[i], 1.0f, share,
                                        throwOnTimeout: false, arrived: Inside);
                    if (!Inside()) Log($"  {spots[i].ToString("F1")}에 닿지 못했다 — 다음 자리를 본다");
                }
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

            // 가장자리는 뺀다. 도착 판정 반경만큼 못 미치면 그대로 볼륨 밖이라
            // "도착했는데 안 들어갔다"가 된다.
            float inset = Mathf.Min(1.5f, Mathf.Min(box.size.x, box.size.z) * 0.25f);

            for (float x = box.min.x + inset; x <= box.max.x - inset; x += step)
            for (float z = box.min.z + inset; z <= box.max.z - inset; z += step)
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
