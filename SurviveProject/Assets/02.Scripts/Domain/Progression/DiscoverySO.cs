using UnityEngine;
using Survive.Items;
using Survive.Narrative;

namespace Survive.Progression
{
    /// <summary>
    /// 현장 발견 하나 — "이 재료를 처음 손에 넣으면 AI가 이렇게 말하고
    /// 이것들이 열린다".
    ///
    /// 대사는 새 자료형을 만들지 않고 시퀀스 자막 한 줄(<see cref="SequenceSO.Line"/>)을
    /// 그대로 쓴다. 프롤로그와 같은 목소리로 같은 자막판에 뜨는 것이 맞다.
    ///
    /// <b>말투 규칙 — 우주복 AI는 기계다.</b> 여기에 대사를 새로 적는 사람은
    /// 반드시 이 형식을 지킨다. 하나만 사람처럼 말해도 목소리가 무너진다.
    /// 형식은 세 문장 고정이다:
    /// <b>"[성질 묘사] 분석... [용도 판정 — 관찰투]. 해당 물질을 사용한 제작법이 있습니다."</b>
    /// <list type="number">
    /// <item>1문장 — 관찰된 <b>성질</b>을 묘사하고 "분석..."으로 닫는다. 말줄임표는
    ///       아직 처리 중이라는 표시다.</item>
    /// <item><b>아이템 이름을 되뇌지 않는다.</b> "스크랩으로 분석됨"은 틀렸다 —
    ///       이름을 부르는 것은 이해가 아니다. AI는 자기가 본 성질을 말한다.</item>
    /// <item>2문장 — 용도 판정은 단정이 아니라 관찰 보고다: "~것으로 보입니다",
    ///       "~로 판단됩니다".</item>
    /// <item>3문장 — 해금 안내는 정형구를 쓴다: "해당 물질을 사용한 제작법이 있습니다."</item>
    /// <item>효율이 기조다. 수식어·접속사를 덜어내고 세 문장을 넘기지 않는다.</item>
    /// <item>금지: 감탄·추측·감상("~겠어", "~네요", "흥미롭군"), 이모지, 물음.</item>
    /// </list>
    /// 본보기: "외계 금속물체 분석... 에너지로 사용가능한 것으로 보입니다.
    /// 해당 물질을 사용한 제작법이 있습니다."
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Discovery")]
    public class DiscoverySO : ScriptableObject
    {
        [Tooltip("원장에 남는 열쇠. 이게 있으면 '이미 겪은 발견'이라 다시 울리지 않는다")]
        public string id;

        [Tooltip("처음 손에 넣었을 때 이 발견이 일어나는 재료")]
        public ItemDataSO item;

        [Tooltip("이때 열리는 청사진들")]
        public BlueprintSO[] unlocks = new BlueprintSO[0];

        [Tooltip("우주복 AI가 하는 말. 비우면 조용히 열리기만 한다")]
        public SequenceSO.Line line = new SequenceSO.Line();
    }
}
