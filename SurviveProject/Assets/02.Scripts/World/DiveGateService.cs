using System.Collections.Generic;
using UnityEngine;
using Survive.Core;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;

namespace Survive.World
{
    /// <summary>
    /// 잠수 통로의 문지기 (실행 스펙 §8-1).
    ///
    /// <b>무슨 일이 벌어지는지는 여기서 정하지 않는다.</b> 그 규칙은
    /// <see cref="DiveRule"/>에 있고 Unity 없이 테스트된다. 이 컴포넌트가 하는 일은
    /// 발밑을 재서 규칙에 묻고, 나온 답을 세계에 반영하는 것뿐이다 —
    /// <see cref="MacroniumContactService"/>와 같은 자세다.
    ///
    /// <b>액면의 문지기와 정확히 뒤집힌 짝이다.</b> 액면은 <b>지날 수 있을 때</b>
    /// 발판을 깔고 못 지나면 죽인다. 잠수는 <b>지날 수 없을 때</b> 발판을 깔고
    /// 지날 수 있으면 걷어 준다. 같은 부품 하나로 두 관문이 서고, 그 차이가
    /// 곧 위협 계층 원칙이다 — <b>환경은 죽이지 않고 생물만 죽인다</b>.
    /// 방호복 없이 통로에 들어가려는 사람은 익사하지 않는다. 물이 밀어낸다.
    ///
    /// <b>왜 씬에 놓지 않고 스스로 붙는가.</b> <see cref="MacroniumContactService"/>와
    /// 같은 이유다 — 플레이어 프리팹과 MainScene은 병합할 수 없는 단일 파일이라
    /// 여러 갈래로 나뉘어 일하는 동안 손대지 않는다.
    ///
    /// 잠수 통로가 씬에 하나도 없으면 이 서비스는 매 프레임 아무것도 하지 않는다 —
    /// <see cref="DiveZone.TryGetAt"/>가 곧바로 false다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DiveGateService : MonoBehaviour
    {
        static DiveGateService _instance;

        public static DiveGateService Instance => _instance;

        /// <summary>가장 최근에 낸 판정. 검증에서 결과를 집기 위한 것이다.</summary>
        public static DiveOutcome LastOutcome { get; private set; }

        /// <summary>방호복이 없어 되돌려 보낸 횟수.</summary>
        public static int RefusedEntries { get; private set; }

        /// <summary>방호복을 걸치고 통로에 들어간 횟수. 거절 횟수와 짝이 되는 값이다.</summary>
        public static int SealedEntries { get; private set; }

        /// <summary>
        /// 막을 때 발밑에 깔아 주는 판의 한 변(m).
        /// <see cref="MacroniumContactService"/>가 같은 크기를 쓰는 이유를 그대로 따른다 —
        /// 구역을 통째로 덮는 콜라이더는 길찾기·조준·건축 판정에 끼어든다.
        /// </summary>
        const float PlateSize = 6f;

        /// <summary>판의 두께(m). 얇으면 빠르게 내려갈 때 뚫고 지나간다.</summary>
        const float PlateThickness = 0.5f;

        /// <summary>
        /// 입구 위 이 높이 안으로 들어오면 미리 판을 깐다.
        ///
        /// "내려간 뒤에 깐다"로 하면 한 프레임 사이에 발이 입구 아래로 내려간 상태에서
        /// 판이 생겨 몸이 판 속에 박힌다. 닿기 전에 깔아 두면 그냥 수면에 뜬 그림이 된다.
        /// </summary>
        const float BlockReach = 1.5f;

        /// <summary>
        /// 발이 입구보다 이만큼 아래로 내려가야 "들어갔다"로 친다(m).
        ///
        /// 0으로 두면 수면 위에서 출렁이는 것만으로 들어간 것이 되어,
        /// 통로 곁을 헤엄쳐 지나가기만 해도 횟수가 오른다.
        /// </summary>
        const float EntryDepth = 0.35f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            LastOutcome = DiveOutcome.None;
            RefusedEntries = 0;
            SealedEntries = 0;

            var go = new GameObject("DiveGateService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DiveGateService>();
        }

        PlayerVitals _vitals;
        Transform _body;
        CharacterController _cc;
        PlayerTraversalGear _gear;
        PlayerInventory _inventory;

        // 매 프레임 물어도 쓰레기가 생기지 않게 한 벌만 두고 다시 채운다.
        readonly List<GearCapability> _loadout = new List<GearCapability>();

        BoxCollider _plate;
        bool _inside;

        /// <summary>지금 물이 몸을 밀어내고 있는가.</summary>
        public bool IsBlocked => _plate != null && _plate.gameObject.activeSelf;

        /// <summary>지금 통로 안에 있는가. 잠수 연출이 이것을 본다.</summary>
        public bool IsDiving => _inside && LastOutcome == DiveOutcome.Sealed;

        /// <summary>발바닥 높이. 입구와 견주는 값이 이것이다.</summary>
        public float FeetY => _cc != null
            ? _body.position.y - _cc.height * 0.5f + _cc.center.y
            : (_body != null ? _body.position.y : 0f);

        void OnDisable()
        {
            Unhook();
            if (_plate != null) Destroy(_plate.gameObject);
        }

        void Update()
        {
            if (!Acquire()) { Block(false, 0f); return; }

            if (!DiveZone.TryGetAt(_body.position, out var zone))
            {
                LastOutcome = DiveOutcome.None;
                _inside = false;
                Block(false, 0f);
                return;
            }

            float feetY = FeetY;
            float mouthY = zone.MouthY;
            var loadout = CurrentLoadout();

            // 입구 언저리에 왔으면 이미 판정한다. 다 내려간 뒤에 묻는 것은 늦다 —
            // 막을 사람은 내려가기 전에 막아야 밀려나는 것이 아니라 못 들어가는 것이 된다.
            bool atMouth = feetY <= mouthY + BlockReach;
            var outcome = DiveRule.Resolve(atMouth, zone.Zone, loadout);
            LastOutcome = outcome;

            bool refused = outcome == DiveOutcome.NoSuit || outcome == DiveOutcome.NotEnoughAir;
            Block(refused && atMouth, mouthY);

            if (refused)
            {
                if (!_inside)
                {
                    _inside = true;
                    RefusedEntries++;
                    Debug.Log($"[DiveGateService] 잠수 통로가 몸을 밀어냈다 ({outcome}) — " +
                              $"발 {feetY:F2}, 입구 {mouthY:F2}, 통로 {zone.Magnitude:F1}초 ({zone.name})");
                }
                return;
            }

            bool entered = outcome == DiveOutcome.Sealed && feetY <= mouthY - EntryDepth;
            if (entered && !_inside)
            {
                _inside = true;
                SealedEntries++;
                Debug.Log($"[DiveGateService] 방호복을 걸치고 잠수 통로에 들어갔다 — " +
                          $"발 {feetY:F2}, 입구 {mouthY:F2}, " +
                          $"통로 {zone.Magnitude:F1}초 / {zone.PassageMeters:F1}m ({zone.name})");
            }
            else if (!entered && feetY > mouthY + BlockReach)
            {
                _inside = false;
            }
        }

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
            _inside = false;
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
        /// 지금 갖춘 이동 장비. <see cref="MacroniumContactService"/>가 잡은 방식 그대로다 —
        /// <see cref="PlayerTraversalGear"/>가 있으면 그쪽에 묻고, 없으면 인벤토리에서 만든다.
        /// </summary>
        IReadOnlyList<GearCapability> CurrentLoadout()
        {
            if (_gear != null) return _gear.Loadout;

            _loadout.Clear();
            TraversalLoadout.Collect(_inventory?.Inventory, _loadout);
            return _loadout;
        }

        /// <summary>
        /// 입구를 막거나 연다. 콜라이더 하나를 발밑에 두는 것으로 끝내는 이유는
        /// <see cref="MacroniumContactService"/>의 받침에 적어 둔 것과 같다 —
        /// 이동 쪽에 "잠수 통로인가" 분기를 새로 내면 수영·걷기 다음의 세 번째 상태가 된다.
        /// </summary>
        void Block(bool on, float mouthY)
        {
            if (!on)
            {
                if (_plate != null && _plate.gameObject.activeSelf) _plate.gameObject.SetActive(false);
                return;
            }

            if (_plate == null)
            {
                var go = new GameObject("DiveMouthBlock");
                go.transform.SetParent(transform, false);
                _plate = go.AddComponent<BoxCollider>();
                _plate.size = new Vector3(PlateSize, PlateThickness, PlateSize);
            }

            if (!_plate.gameObject.activeSelf) _plate.gameObject.SetActive(true);

            // 판의 윗면이 입구와 같은 높이가 되게 놓는다. 수면에 떠 있는 그림이 된다.
            _plate.transform.position = new Vector3(
                _body.position.x, mouthY - PlateThickness * 0.5f, _body.position.z);
        }

        /// <summary>검증이 실행 사이에 상태를 비운다.</summary>
        public static void ResetCounters()
        {
            LastOutcome = DiveOutcome.None;
            RefusedEntries = 0;
            SealedEntries = 0;
            if (_instance != null) _instance._inside = false;
        }
    }
}
