using UnityEngine;
using Survive.Items;
using Survive.Narrative;

namespace Survive.Progression
{
    /// <summary>
    /// 연구 항목 하나 — "이 유물을 스크랩을 태워 이만큼 들여다보면 이것들이 열린다".
    ///
    /// 해금 채널 둘 중 <b>두 번째</b>다(스펙 §2). 채널 1(<see cref="DiscoverySO"/>)이
    /// 손에 쥐는 순간 공짜로 열리는 것이라면, 이쪽은 <b>대가를 치른다</b> —
    /// 낫의 영역에서 주워 온 유물을 내놓고, 스크랩에 갇힌 에너지를 태우고,
    /// 시간을 기다린다. 진행 장비가 이 뒤에 서는 이유다.
    ///
    /// 산출물이 아이템이 아니라 <b>원장의 한 줄</b>이라는 점만 빼면 제작 레시피와
    /// 같은 모양이다(<see cref="Survive.Crafting.RecipeSO"/>). 그래서 소재도 시간도
    /// 취소 환급도 제작과 같은 규칙을 따른다.
    ///
    /// <b>대사 말투 — 우주복 AI는 기계다.</b> <see cref="DiscoverySO"/>에 적어 둔 규칙을
    /// 그대로 지킨다. 세 문장 고정, 아이템 이름을 되뇌지 않고 관찰된 성질을 묘사,
    /// 용도 판정은 관찰투("~것으로 보입니다"). 다만 마지막 정형구만 다르다 —
    /// 현장 발견은 <i>"해당 물질을 사용한 제작법이 있습니다."</i>지만,
    /// 연구는 방금 <b>알아낸</b> 것이므로 <b>"해당 구조를 적용한 제작법을 확보했습니다."</b>다.
    /// 주운 것과 밝혀낸 것은 같은 문장으로 말할 수 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Research Entry")]
    public class ResearchEntrySO : ScriptableObject
    {
        [Tooltip("연구 항목의 이름표. 화면의 줄 이름(Row_<id>)에도 쓰이므로 레시피 id와 겹치지 않게")]
        public string id;

        public string displayName;

        [Tooltip("들여다볼 대상. 보통 낫이 떨어뜨린 유물 하나다")]
        public ItemStack[] materials = new ItemStack[0];

        /// <summary>
        /// 태울 스크랩 개수.
        ///
        /// 스크랩은 연소재가 아니라 <b>에너지 저장 매체</b>다(백로그 35의 세계관 확정).
        /// 연구대에 붙은 AI가 추가 연산을 돌리려면 전력이 있어야 하고, 이 세계에서
        /// 전력을 꺼낼 수 있는 것은 스크랩뿐이다. 화톳불이 스크랩에서 배터리 셀을
        /// 뽑아내는 것과 같은 픽션의 다른 얼굴이다.
        /// </summary>
        [Min(0)] public int energyCost = 10;

        [Tooltip("들여다보는 데 걸리는 시간(초). 0이면 즉시 끝난다")]
        [Min(0f)] public float researchSeconds = 30f;

        [Tooltip("다 보고 나면 열리는 청사진들. unlockKeys와 둘 다 비면 아무것도 열지 못하는 헛 항목이다")]
        public BlueprintSO[] unlocks = new BlueprintSO[0];

        /// <summary>
        /// 청사진이 아닌 원장 열쇠들.
        ///
        /// <b>왜 따로 두는가.</b> 연구의 산출물이 언제나 "만드는 법"인 것은 아니다.
        /// 생물 부품을 들여다봐서 알게 되는 것은 제작법이 아니라 <b>그 개체가
        /// 무엇인가</b>이고(도감 열쇠 <c>codex_&lt;생물id&gt;</c>), 그것을 청사진으로
        /// 위장해 두면 잠긴 레시피 줄에 "codex_공 청사진이 필요하다"가 뜬다.
        ///
        /// 원장은 어차피 문자열만 기억한다(<see cref="UnlockLedger"/>). 청사진을
        /// 거치지 않는 산출물은 그 문자열을 그대로 적는다.
        /// </summary>
        [Tooltip("청사진을 거치지 않는 원장 열쇠들. 도감 항목(codex_<생물id>) 같은 것")]
        public string[] unlockKeys = new string[0];

        [Tooltip("다 봤을 때 우주복 AI가 하는 말. 비우면 조용히 열리기만 한다")]
        public SequenceSO.Line line = new SequenceSO.Line();
    }
}
