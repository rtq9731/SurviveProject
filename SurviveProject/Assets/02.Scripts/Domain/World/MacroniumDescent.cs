using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>
    /// 짙은 매크로늄 층을 <b>다 내려갔는가</b>. 챕터 1의 종막이 이 물음 하나로 끝난다
    /// (기획서 §6.2 "종막 — 뚫고 내려간다").
    ///
    /// <see cref="MacroniumContact"/>는 표면에서 벌어지는 일을 답한다 — 죽는가, 얹히는가,
    /// 가라앉기 시작하는가. 여기서 답하는 것은 그 다음이다: 가라앉기 시작한 사람이
    /// <b>층을 끝까지 지났는가</b>.
    ///
    /// <b>왜 둘을 나누는가.</b> 표면 접촉은 매 프레임 일어나는 일이고 층 돌파는 한 번뿐이다.
    /// 한 함수에 넣으면 "닿았지만 아직 못 뚫음"이라는 상태가 접촉 결과에 섞여 들어와,
    /// 액면 위를 걷는 것과 층 속을 내려가는 중인 것이 같은 값으로 보고된다.
    ///
    /// <b>판정은 새로 만들지 않는다.</b> "이 층을 뚫을 수단이 있는가"는 다른 관문과 똑같이
    /// <see cref="EnvironmentThreat"/>가 답한다 — 위협은 <see cref="EnvironmentHazard.MacroniumLayer"/>,
    /// 크기는 층의 두께(m), 장비는 <see cref="TraversalGear.Submersible"/>, 용량은 뚫을 수 있는
    /// 두께(m). 위협이 늘어도 규칙이 늘지 않는다는 원칙이 종막에서도 그대로 선다.
    ///
    /// Unity를 모른다 — 층이 어디에 깔렸는지는 <c>DescentZone</c>이 알고,
    /// 여기서는 높이 두 개와 갖춘 것만 받는다.
    /// </summary>
    public static class MacroniumDescent
    {
        /// <summary>
        /// 층의 윗면(= 액면)에서 발까지 얼마나 내려왔는가(m). 아직 위에 있으면 음수.
        /// </summary>
        public static float DepthBelow(float feetY, float layerTopY) => layerTopY - feetY;

        /// <summary>
        /// 이 층의 아랫면 높이. 여기를 넘어서면 층을 지난 것이다.
        /// </summary>
        public static float BottomY(float layerTopY, float thickness) => layerTopY - thickness;

        /// <summary>
        /// 이 층을 지금 갖춘 것으로 뚫을 수 있는가. 다른 관문과 같은 판정을 쓴다.
        /// </summary>
        public static PassageCheck Evaluate(HazardZone layer, IReadOnlyList<GearCapability> loadout) =>
            EnvironmentThreat.Evaluate(layer, loadout);

        /// <summary>
        /// 층을 다 지났는가.
        ///
        /// 두 가지가 동시에 참이어야 한다 — <b>뚫을 수단을 갖췄고</b>, <b>발이 층의 아랫면
        /// 아래로 내려갔다</b>. 수단 없이 어쩌다 그 깊이까지 간 경우까지 종막으로 치면
        /// 지형의 틈 하나가 챕터를 끝내 버린다.
        ///
        /// 층이 아닌 구간을 넘기면 언제나 false다 — 액면도, 어둠도, 폭도 뚫고 내려가는
        /// 종류가 아니다. <see cref="MacroniumContact.Resolve"/>가 액면 하나에만 규칙을
        /// 거는 것과 같은 이유다.
        /// </summary>
        public static bool Breached(float feetY, float layerTopY, HazardZone layer,
                                    IReadOnlyList<GearCapability> loadout)
        {
            if (layer.Hazard != EnvironmentHazard.MacroniumLayer) return false;
            if (!EnvironmentThreat.CanPass(layer, loadout)) return false;

            return feetY <= BottomY(layerTopY, layer.Magnitude);
        }

        /// <summary>
        /// 뚫기까지 얼마나 남았는가(m). 이미 지났으면 0, 아직 액면 위면 층의 두께 전부.
        /// 진행 표시와 검증이 "내려가고 있다"를 숫자로 보기 위한 것이다.
        /// </summary>
        public static float RemainingDepth(float feetY, float layerTopY, float thickness)
        {
            float gone = DepthBelow(feetY, layerTopY);
            if (gone <= 0f) return thickness;
            if (gone >= thickness) return 0f;
            return thickness - gone;
        }
    }
}
