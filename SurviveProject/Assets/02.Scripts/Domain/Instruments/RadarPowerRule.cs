using System;
using UnityEngine;

namespace Survive.Instruments
{
    /// <summary>레이더에 남은 전하. 저장본을 왕복한다.</summary>
    [Serializable]
    public class RadarChargeState
    {
        public float charge;
    }

    /// <summary>
    /// 관측을 걸기 전에 <b>셀을 몇 개 끼워야 하는가</b>.
    ///
    /// 순수 계산이라 Unity 없이 테스트한다. 실제로 소지품에서 셀을 빼는 것은
    /// 껍데기(<c>RadarController</c>)의 일이고, 여기서는 몇 개가 필요한지만 답한다 —
    /// 세는 자리와 빼는 자리가 같으면 "빼는 데 실패했는데 이미 세어 버린" 상태가
    /// 생긴다.
    /// </summary>
    public static class RadarPowerRule
    {
        /// <summary>
        /// 지금 전하로 한 번 관측하려면 셀이 몇 개 더 있어야 하는가.
        /// 이미 넉넉하면 0이다.
        /// </summary>
        public static int CellsNeeded(float charge, float cost, float chargePerCell)
        {
            if (charge >= cost) return 0;
            if (chargePerCell <= 0f) return int.MaxValue;

            return Mathf.CeilToInt((cost - charge) / chargePerCell);
        }

        /// <summary>셀을 끼운 뒤의 전하. 통보다 많이 담기지 않는다.</summary>
        public static float AfterInserting(float charge, int cells, float chargePerCell, float maxCharge) =>
            Mathf.Clamp(charge + cells * chargePerCell, 0f, Mathf.Max(0f, maxCharge));
    }
}
