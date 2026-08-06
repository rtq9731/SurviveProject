using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// "이 자리는 밝은가"를 답하는 창구.
    ///
    /// 화톳불은 지금까지 랜턴을 충전하는 것 말고는 아무 의미가 없었다 — 다른 시스템이
    /// 화톳불의 존재를 조회할 방법이 없었다. 이 레지스트리가 그 창구다. P4의 야간 습격
    /// AI는 매 틱 자기 위치를 물어보고, UI는 플레이어 위치를 물어볼 것이다. 둘 다 특정
    /// Campfire 인스턴스를 참조할 필요가 없다 — <see cref="ILitZoneSource"/>만 구현하면
    /// 화톳불이든, 나중에 붙을 발광 버섯 군락 같은 고정 광원이든 똑같이 조회된다.
    ///
    /// 순수 C# 정적 등록부다. 등록·해제는 소스(대개 MonoBehaviour)의 OnEnable/OnDisable에서
    /// 하면 된다 — Unity가 빌드·철거(Destroy)·비활성화·씬 언로드 전부에서 그 콜백을
    /// 보장하므로 여기서 따로 생명주기를 신경 쓸 필요가 없다.
    /// </summary>
    public static class LitZoneRegistry
    {
        static readonly List<ILitZoneSource> _sources = new List<ILitZoneSource>();

        public static void Register(ILitZoneSource source)
        {
            if (source == null) return;
            if (!_sources.Contains(source)) _sources.Add(source);
        }

        public static void Unregister(ILitZoneSource source) => _sources.Remove(source);

        /// <summary>
        /// 이 위치가 켜져 있는 구역 중 하나에라도 들어가는가.
        /// 연료가 떨어진 화톳불은 <see cref="ILitZoneSource.IsLit"/>이 false이므로
        /// 배치돼 있어도 자동으로 빠진다 — 살아 있는 상태를 반영한다.
        /// </summary>
        public static bool IsLit(Vector3 position)
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null || !source.IsLit) continue;

                float r = source.LitZoneRadius;
                if ((position - source.LitZoneCenter).sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        /// <summary>
        /// 이 위치를 밝히고 있는 광원 중 <b>중심이 가장 가까운</b> 것의 중심.
        /// 밝지 않으면 false다.
        ///
        /// 빛을 꺼리는 생물이 빛에서 물러날 때 필요하다. "플레이어에게서 멀어진다"로
        /// 대신할 수 없다 — 화톳불 건너편에 플레이어가 서 있으면 그 방향은
        /// 불 속으로 들어가는 방향이다.
        /// </summary>
        public static bool TryGetLitCenter(Vector3 position, out Vector3 center)
        {
            center = Vector3.zero;
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null || !source.IsLit) continue;

                float r = source.LitZoneRadius;
                float d2 = (position - source.LitZoneCenter).sqrMagnitude;
                if (d2 > r * r) continue;

                if (d2 >= best) continue;
                best = d2;
                center = source.LitZoneCenter;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// 이 자리가 누군가가 <b>내준 쪽</b>인가 — 곧 등 뒤 사각이다 (기획서 §9).
        ///
        /// <b>낫이 읽을 창구다.</b> 낫 쪽에서 "랜턴이 어디 있고 사람이 어디를 보는가"를
        /// 조립하게 두면 규칙이 두 군데로 갈라진다. 물어볼 것은 하나다 —
        /// 여기로 파고들 수 있는가.
        ///
        /// <b>어둡기만 하면 되는 것이 아니라 「내준 쪽」이어야 한다.</b> 처음에는
        /// "뒤이면서 어두운 자리"로 두었는데, 그러면 등 뒤 5m 안쪽은 랜턴 불빛이
        /// 조금 닿는다는 이유로 다시 지켜지는 자리가 되어, 낫이 5m 언저리에서
        /// 영원히 오르내리기만 하고 <b>끝내 닿지 못했다</b>(실측 2.77m에서 정체).
        /// 기획서 §5는 그렇게 적혀 있지 않다 — <b>랜턴은 앞쪽만 지키므로 등 뒤를
        /// 내주고, 붙어 있는 개체를 떼어내지 못한다.</b> 뒤쪽으로 새는 빛은 사람이
        /// <b>보기</b> 위한 것이지 지키는 것이 아니다.
        ///
        /// <b>다만 고정 조명은 메운다.</b> 화톳불은 앞뒤가 없고 사람과 함께 돌지도
        /// 않으므로, 그 안에 들어온 자리는 누구의 등 뒤라도 내준 쪽이 아니다.
        /// 기획서 §3의 <b>Beware → Patrol 전이가 고정 조명 접근</b>인 것과 같은 말이다.
        ///
        /// <b>밀린 것이 없으면 내준 쪽도 없다.</b> 오프셋 0이면 원이 사람을 가운데
        /// 두므로 사방이 대칭이고, 그때 등 뒤가 어두운 것은 그냥 <b>먼 것</b>이다.
        /// 그 가지가 이 규칙 전체의 회귀선이다.
        /// </summary>
        public static bool IsBlindSide(Vector3 position)
        {
            if (LitByFixed(position)) return false;

            for (int i = 0; i < _sources.Count; i++)
            {
                if (!(_sources[i] is IOffsetLitSource offset)) continue;
                if (!offset.IsLit) continue;
                if ((offset.LitZoneCenter - offset.LitAnchor).sqrMagnitude < 1e-6f) continue;

                if (LanternRule.IsBehind(offset.LitAnchor, offset.LitForward, position))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 앞뒤가 없는 광원(화톳불·발광 군락)이 이 자리를 밝히고 있는가.
        /// 사람을 따라다니는 광원은 세지 않는다 — 그쪽은 내주는 쪽이 있기 때문이다.
        /// </summary>
        static bool LitByFixed(Vector3 position)
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                var source = _sources[i];
                if (source == null || !source.IsLit) continue;
                if (source is IOffsetLitSource) continue;

                float r = source.LitZoneRadius;
                if ((position - source.LitZoneCenter).sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        /// <summary>
        /// 지금 사각을 만들고 있는 광원의 <b>선 자리와 바라보는 쪽</b>.
        /// 사각이 어디인지 <b>찾아가야</b> 하는 쪽(낫의 재등장 위치 선정, 기획서 §3)이
        /// 쓴다. 판정만 필요하면 <see cref="IsBlindSpot"/>이면 된다.
        /// </summary>
        public static bool TryGetOffsetSource(out Vector3 anchor, out Vector3 forward)
        {
            for (int i = 0; i < _sources.Count; i++)
            {
                if (!(_sources[i] is IOffsetLitSource offset)) continue;
                if (!offset.IsLit) continue;

                var f = LanternRule.Facing(offset.LitForward);
                if (f == Vector3.zero) continue;

                anchor = offset.LitAnchor;
                forward = f;
                return true;
            }

            anchor = Vector3.zero;
            forward = Vector3.zero;
            return false;
        }

        /// <summary>테스트·씬 전환 사이에 상태를 비운다.</summary>
        public static void Clear() => _sources.Clear();
    }
}
