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
    /// <list type="number">
    /// <item>구조: <b>[분석 대상]. [판정/적성]. [해금 안내].</b> <b>세 문장을 넘기지 않는다.</b></item>
    /// <item><b>첫 문장에 분석 대상의 이름을 반드시 쓴다</b> — "현무암으로 분석됨"처럼
    ///       무엇을 봤는지가 문장 안에 있어야 한다. "잔류 에너지가 확인됨"은 대상이 빠져 틀렸다.</item>
    /// <item>효율이 최우선 기조다. 수식어·접속사를 덜어내고 필요한 정보만 남긴다.</item>
    /// <item>어미: "~로 분석됨"(관형 보고체)과 "~합니다/있습니다"(건조한 정중체)를 섞는다.</item>
    /// <item>금지: 감탄·추측·감상("~겠어", "~네요", "흥미롭군"), 이모지, 물음.</item>
    /// </list>
    /// 본보기: "현무암으로 분석됨. 도구 사용에 적합합니다. 이를 활용한 도구 제작법이 있습니다."
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
