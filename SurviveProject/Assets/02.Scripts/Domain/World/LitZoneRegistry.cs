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

        /// <summary>테스트·씬 전환 사이에 상태를 비운다.</summary>
        public static void Clear() => _sources.Clear();
    }
}
