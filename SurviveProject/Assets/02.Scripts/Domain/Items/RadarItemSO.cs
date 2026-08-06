using UnityEngine;
using Survive.Instruments;

namespace Survive.Items
{
    /// <summary>
    /// 레이더 — 배터리를 써서 다른 섬과 바다 아래를 찾는 장비 (챕터 1 스펙 §8-2).
    ///
    /// <see cref="TraversalGearItemSO"/>가 "무엇을 뚫는가"를 데이터로 내놓듯, 이쪽은
    /// <b>어떤 전자파를 쓰는가</b>만 내놓는다. 무엇이 잡히는지는 코드가 아니라
    /// <see cref="RadarBand.wavelengthMeters"/>가 정하므로, 나중에 더 좋은 레이더를
    /// 붙일 때 늘어나는 것은 에셋뿐이다.
    ///
    /// <b>손 소켓에 걸지 않는다.</b> 랜턴·액면 보행 장비와 같은 판단이다 — 세워 놓고
    /// 기다리는 물건이라 곡괭이와 자리를 다툴 이유가 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Items/Radar")]
    public class RadarItemSO : ItemDataSO
    {
        [Tooltip("쓰는 전자파. 파장 하나가 무엇이 잡히는지를 전부 정한다")]
        public RadarBand band = new RadarBand();

        [Tooltip("한 번 관측하는 데 서 있어야 하는 시간(초)")]
        [Min(0.5f)] public float scanSeconds = 6f;

        [Tooltip("관측하는 동안 초당 먹는 배터리")]
        [Min(0f)] public float batteryPerSecond = 4f;

        /// <summary>
        /// 이 장치가 한 번에 담아 둘 수 있는 전하.
        ///
        /// <b>왜 랜턴의 통을 함께 쓰지 않는가.</b> 그러는 편이 "빛과 정보가 같은 값을
        /// 두고 다툰다"를 더 곧게 보여 준다. 그런데 랜턴의 통은 밖에서 뺄 수가 없고
        /// (<c>LanternController.Recharge</c>는 음수를 거절한다), 그 문을 열려면 랜턴을
        /// 고쳐야 한다. 지금 랜턴은 다른 갈래가 쥐고 있다.
        ///
        /// 그래서 통은 따로 두되 <b>넣는 것은 같은 물건</b>으로 한다 — 배터리 셀이다.
        /// 다툼은 통이 아니라 셀에서 일어나고, 그것이 오히려 랜턴이 이미 적어 둔
        /// 규칙과 맞는다: "몇 개를 들고 갈 것인가를 출발 전의 결정으로 만든다."
        /// </summary>
        [Tooltip("담아 둘 수 있는 전하의 최대")]
        [Min(1f)] public float maxCharge = 100f;

        [Tooltip("배터리 셀 하나가 채우는 전하. 랜턴의 batteryPerCell과 같은 값이어야 총량이 맞는다")]
        [Min(1f)] public float chargePerCell = 100f;

        /// <summary>한 번 관측에 드는 배터리 전부. 화면과 게이트가 함께 읽는다.</summary>
        public float ScanCost => scanSeconds * batteryPerSecond;

        /// <summary>이 에셋대로 관측 하나를 만든다.</summary>
        public RadarScan NewScan() => new RadarScan(band, scanSeconds, batteryPerSecond);
    }
}
