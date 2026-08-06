using System.Collections.Generic;
using UnityEngine;
using Survive.Combat;
using Survive.Core;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;

namespace Survive.World
{
    /// <summary>
    /// 매크로늄 액면에 발이 닿으면 대가를 치르게 한다 (P2 스펙 §3의 네 번째 관문).
    ///
    /// <b>무슨 일이 벌어지는지는 여기서 정하지 않는다.</b> 그 규칙은
    /// <see cref="MacroniumContact"/>에 있고 Unity 없이 테스트된다. 이 컴포넌트가 하는 일은
    /// 발밑을 재서 규칙에 묻고, 나온 답을 세계에 반영하는 것뿐이다 —
    /// 죽이거나, 받쳐 주거나, 가라앉게 두거나.
    ///
    /// <b>가라앉히는 데는 새 이동 상태가 필요 없다.</b> 받침을 걷어 주기만 하면
    /// 그다음은 평소의 중력이 한다 — 받침을 까는 것으로 "액면 위를 걷는다"를 만든 것과
    /// 정확히 뒤집힌 짝이다. 하강을 이동 쪽의 세 번째 상태로 만들면 수영·걷기와
    /// 겹치는 경우를 전부 손으로 맞춰야 한다.
    ///
    /// <b>죽이는 방법은 새로 만들지 않는다.</b> 이미 있는 피해 창구
    /// (<see cref="PlayerDamageReceiver"/>)로 치명상을 넣고, 그 뒤는 기존 고리가 받는다 —
    /// 체력 0 → <see cref="PlayerVitals.Died"/> → <see cref="DeathDropService"/>가 가방을
    /// 남기고 → <see cref="RespawnService"/>가 일으켜 세운다. 익사와 같은 길이다.
    ///
    /// <b>왜 씬에 놓지 않고 스스로 붙는가.</b> <see cref="DeathDropService"/>와 같은 이유다 —
    /// 플레이어 프리팹과 MainScene은 병합할 수 없는 단일 파일이라 여러 갈래로 나뉘어
    /// 일하는 동안 손대지 않는다. 대신 실행 시점에 스스로 서고, 씬이 다시 올라오면
    /// 새 플레이어를 다시 잡는다.
    ///
    /// 액면 구역이 씬에 하나도 없으면(§8-4 배치 전) 이 서비스는 매 프레임 아무것도 하지
    /// 않는다 — <see cref="MacroniumSurfaceZone.TryGetSurfaceAt"/>가 곧바로 false다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MacroniumContactService : MonoBehaviour
    {
        static MacroniumContactService _instance;

        public static MacroniumContactService Instance => _instance;

        /// <summary>가장 최근에 낸 판정. 검증에서 결과를 집기 위한 것이다.</summary>
        public static MacroniumContactOutcome LastOutcome { get; private set; }

        /// <summary>액면 때문에 죽인 횟수. 한 번 닿아 한 번만 죽는지 보려는 것이다.</summary>
        public static int LethalContacts { get; private set; }

        /// <summary>
        /// 돌파정을 걸치고 액면을 통과한 횟수. 죽인 횟수와 짝이 되는 값이라
        /// "같은 자리에서 무엇이 달라졌는가"를 숫자로 볼 수 있다.
        /// </summary>
        public static int DescentContacts { get; private set; }

        /// <summary>
        /// 액면 위를 걸을 때 발밑에 깔아 주는 판의 한 변(m).
        ///
        /// 구역 전체를 덮는 판을 세우지 않는 이유: 액면섬의 구역은 반경 수십 미터라
        /// 그만한 콜라이더 하나가 생물의 길찾기·건축 판정·조준 레이에 통째로 끼어든다.
        /// 실제로 필요한 것은 <b>발밑</b>뿐이므로 플레이어를 따라다니는 작은 판을 쓴다.
        /// </summary>
        const float PlateSize = 6f;

        /// <summary>발판의 두께(m). 얇으면 빠르게 떨어질 때 뚫고 지나간다.</summary>
        const float PlateThickness = 0.5f;

        /// <summary>
        /// 액면 위 이 높이 안으로 들어오면 미리 발판을 깐다.
        ///
        /// "닿은 뒤에 깐다"로 하면 한 프레임 사이에 발이 액면 아래로 내려간 상태에서
        /// 판이 생겨 몸이 판 속에 박힌다. 닿기 전에 깔아 두면 그냥 착지가 된다.
        /// </summary>
        const float SupportReach = 1.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            LastOutcome = MacroniumContactOutcome.None;
            LethalContacts = 0;
            DescentContacts = 0;

            var go = new GameObject("MacroniumContactService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MacroniumContactService>();
        }

        PlayerVitals _vitals;
        Transform _body;
        CharacterController _cc;
        PlayerTraversalGear _gear;
        PlayerInventory _inventory;

        // 매 프레임 물어도 쓰레기가 생기지 않게 한 벌만 두고 다시 채운다.
        readonly List<GearCapability> _loadout = new List<GearCapability>();

        BoxCollider _plate;
        bool _killed;
        bool _sinking;

        /// <summary>지금 액면이 몸을 받치고 있는가.</summary>
        public bool IsSupported => _plate != null && _plate.gameObject.activeSelf;

        /// <summary>지금 액면을 뚫고 내려가는 중인가.</summary>
        public bool IsDescending => LastOutcome == MacroniumContactOutcome.Descending;

        /// <summary>발바닥 높이. 액면과 견주는 값이 이것이다.</summary>
        public float FeetY => _cc != null
            ? _body.position.y - _cc.height * 0.5f + _cc.center.y
            : (_body != null ? _body.position.y : 0f);

        void OnDisable()
        {
            Unhook();
            if (_plate != null) Destroy(_plate.gameObject);
        }

        // 씬을 다시 올리면 지금 잡고 있던 플레이어가 사라진다.
        // DeathDropService와 같은 방식 — 사라졌으면 다시 잡는다.
        void Update()
        {
            if (!Acquire()) { Support(false, 0f); return; }

            if (!_vitals.Health.IsEmpty) _killed = false;

            if (!MacroniumSurfaceZone.TryGetSurfaceAt(_body.position, out float surfaceY, out var zone))
            {
                LastOutcome = MacroniumContactOutcome.None;
                _sinking = false;
                Support(false, 0f);
                return;
            }

            float feetY = FeetY;
            var loadout = CurrentLoadout();
            bool wantsDescent = DescentHeld();
            var outcome = MacroniumContact.Resolve(feetY, surfaceY, zone.Zone, loadout, wantsDescent);
            LastOutcome = outcome;

            // 받침은 "닿았는가"보다 먼저 깔린다 — 위 SupportReach 주석 참고.
            // 다만 가라앉기를 택한 순간에는 깔지 않는다. 깔아 두면 하강 키를 눌러도
            // 발밑의 판이 그대로 받쳐 버려 아무 일도 일어나지 않는다.
            bool canWalk = EnvironmentThreat.CanPass(zone.Zone, loadout);
            bool sinking = outcome == MacroniumContactOutcome.Descending;
            Support(canWalk && !sinking && feetY <= surfaceY + SupportReach, surfaceY);

            if (sinking && !_sinking)
            {
                _sinking = true;
                DescentContacts++;
                Debug.Log($"[MacroniumContactService] 돌파정을 걸치고 액면으로 내려간다 — " +
                          $"발 {feetY:F2}, 액면 {surfaceY:F2} ({zone.name})");
            }
            else if (!sinking)
            {
                _sinking = false;
            }

            if (outcome == MacroniumContactOutcome.Lethal) Kill(zone, feetY, surfaceY);
        }

        /// <summary>
        /// 지금 하강 키(Ctrl)를 누르고 있는가. 물속에서 가라앉는 데 쓰는 그 키다
        /// (기획서 §5.6) — 액면 위에서만 쓰는 조작을 새로 만들면 배울 것이 하나 늘어난다.
        ///
        /// 입력을 못 찾으면 "누르지 않았다"로 친다. 입력이 없는 상태에서 기본값이
        /// 하강이면 액면 보행 장비만으로 걷던 사람이 갑자기 빠지기 시작한다.
        /// </summary>
        bool DescentHeld() =>
            GameServices.TryGet<Survive.Input.InputReaderSO>(out var input) &&
            input != null && input.IsDescending;

        bool Acquire()
        {
            if (_vitals != null && _body != null) return true;

            Unhook();
            if (!GameServices.TryGet<PlayerVitals>(out var found) || found == null) return false;

            _vitals = found;
            _body = found.GetComponentInParent<PlayerContext>()?.transform ?? found.transform;
            _cc = _body.GetComponentInChildren<CharacterController>();
            _gear = _body.GetComponentInChildren<PlayerTraversalGear>(true);
            _inventory = _body.GetComponentInChildren<PlayerInventory>(true);
            _killed = false;
            return true;
        }

        void Unhook()
        {
            _vitals = null;
            _body = null;
            _cc = null;
            _gear = null;
            _inventory = null;
        }

        /// <summary>
        /// 지금 갖춘 이동 장비.
        ///
        /// <see cref="PlayerTraversalGear"/>가 붙어 있으면 그쪽에 묻는다 — 인벤토리 밖에서
        /// 오는 장비(수영·다리·랜턴)까지 얹어 주는 것이 그 컴포넌트의 몫이라, 나중에
        /// 그쪽이 늘어나도 여기가 따라갈 필요가 없다.
        ///
        /// 아직 안 붙어 있으면(§8-4 배치 전) 같은 출처인 인벤토리에서 직접 만든다.
        /// 액면 보행 장비는 인벤토리에서 나오는 물건이라 두 길의 답이 같다.
        /// </summary>
        IReadOnlyList<GearCapability> CurrentLoadout()
        {
            if (_gear != null) return _gear.Loadout;

            _loadout.Clear();
            TraversalLoadout.Collect(_inventory?.Inventory, _loadout);
            return _loadout;
        }

        /// <summary>
        /// 치명상을 넣는다. 얼마를 넣든 결과는 같지만, 남은 체력을 넘겨야
        /// 한 방에 끝난다 — 조금씩 깎이면 "닿으면 죽는다"가 "닿으면 아프다"가 된다.
        /// </summary>
        void Kill(MacroniumSurfaceZone zone, float feetY, float surfaceY)
        {
            if (_killed || _vitals.Health.IsEmpty) return;

            var receiver = _body.GetComponentInChildren<IDamageable>();
            if (receiver == null)
            {
                Debug.LogWarning("[MacroniumContactService] 피해를 받을 곳을 찾지 못해 죽이지 못했습니다.");
                return;
            }

            _killed = true;
            LethalContacts++;

            float lethal = Mathf.Max(_vitals.Health.Current, _vitals.Health.Max);
            var at = new Vector3(_body.position.x, surfaceY, _body.position.z);

            // 가해자를 비워 둔다. 자기 몸에서 나온 피해로 보이면 문 앞에서 걸러진다
            // (PlayerDamageReceiver의 자가 타격 차단). 액면은 남이 때리는 것이 아니라
            // 환경이 죽이는 것이므로 출처 없는 피해가 맞다.
            receiver.TakeDamage(new DamageInfo(lethal, null, at, Vector3.up));

            Debug.Log($"[MacroniumContactService] 맨몸으로 매크로늄 액면에 닿았다 — " +
                      $"발 {feetY:F2}, 액면 {surfaceY:F2} ({zone.name})");
        }

        /// <summary>
        /// 액면 위에 발판을 깔거나 치운다.
        ///
        /// 콜라이더 하나를 발밑에 두는 것으로 끝내는 이유: 이렇게 하면
        /// <see cref="PlayerLocomotion"/>이 평소처럼 중력·접지·점프를 계산한다.
        /// 이동 쪽에 "액면 위인가" 분기를 새로 내면 수영과 걷기 다음의 세 번째 상태가
        /// 되고, 그 셋이 겹치는 경우를 전부 손으로 맞춰야 한다.
        /// </summary>
        void Support(bool on, float surfaceY)
        {
            if (!on)
            {
                if (_plate != null && _plate.gameObject.activeSelf) _plate.gameObject.SetActive(false);
                return;
            }

            if (_plate == null)
            {
                var go = new GameObject("MacroniumFooting");
                go.transform.SetParent(transform, false);
                _plate = go.AddComponent<BoxCollider>();
                _plate.size = new Vector3(PlateSize, PlateThickness, PlateSize);
            }

            if (!_plate.gameObject.activeSelf) _plate.gameObject.SetActive(true);

            // 판의 윗면이 액면과 같은 높이가 되게 놓는다. 발이 표면에 얹힌 그림이 된다.
            _plate.transform.position = new Vector3(
                _body.position.x, surfaceY - PlateThickness * 0.5f, _body.position.z);
        }
    }
}

