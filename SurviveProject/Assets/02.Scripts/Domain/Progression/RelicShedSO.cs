using System.Collections.Generic;
using UnityEngine;
using Survive.Items;

namespace Survive.Progression
{
    /// <summary>
    /// 순찰 중 흘리고 가는 것 — <b>낫의 그림자에서 줍는 유물</b>(스펙 §2).
    ///
    /// 유물은 처치 전리품이 아니다. 낫은 챕터 1에서 이겨야 하는 상대가 아니라
    /// 피해야 하는 상대이고(백로그 30), 그 영역에 <b>머물다 온 것</b>이 대가다.
    /// 그래서 죽여야 나오는 <see cref="Survive.Harvesting.LootTableSO"/>가 아니라
    /// 시간에 붙은 규칙으로 둔다.
    ///
    /// <b>주기와 확률을 왜 인스펙터에 여는가.</b> "낫 영역에 1~2분 머물면 하나 본다"는
    /// 감각은 숫자로만 표현되는데, 그 감각은 지형 배치(§8-4)가 끝나야 확정된다.
    /// 코드에 박아 두면 그때 손대는 자리가 코드가 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Progression/Relic Shed")]
    public class RelicShedSO : ScriptableObject
    {
        /// <summary>유물 하나와 그것으로 밝혀지는 것.</summary>
        [System.Serializable]
        public class Relic
        {
            public ItemDataSO item;

            [Tooltip("이 유물의 연구가 여는 것. 이미 열렸으면 같은 유물을 다시 떨구지 않는다")]
            public BlueprintSO researchUnlock;
        }

        [Tooltip("떨굴 수 있는 유물들. 미보유가 하나도 없으면 아무것도 떨구지 않는다")]
        public Relic[] relics = new Relic[0];

        [Tooltip("떨굴지 말지 한 번 굴리는 간격(초)")]
        [Min(0.1f)] public float intervalSeconds = 25f;

        [Tooltip("한 번 굴릴 때 실제로 떨어질 확률")]
        [Range(0f, 1f)] public float chance = 0.25f;

        [Tooltip("이 거리 안에 사람이 있을 때만 굴린다. 아무도 없는 곳에 떨궈 봐야 아무도 못 본다")]
        [Min(1f)] public float witnessRadius = 40f;

        /// <summary>
        /// 규칙이 읽을 형태로 옮겨 담는다. 매 프레임 새 목록을 만들지 않도록
        /// 부르는 쪽이 자기 목록을 들고 온다.
        /// </summary>
        public void FillOptions(List<RelicOption> into, List<ItemDataSO> items)
        {
            into?.Clear();
            items?.Clear();
            if (relics == null) return;

            foreach (var r in relics)
            {
                if (r?.item == null || string.IsNullOrWhiteSpace(r.item.id)) continue;

                into?.Add(new RelicOption(r.item.id, r.researchUnlock != null ? r.researchUnlock.id : null));
                items?.Add(r.item);
            }
        }

        /// <summary>사람이 기다리는 평균 시간(초). 실측 근거를 테스트가 읽는다.</summary>
        public float MeanSecondsToDrop =>
            chance <= 0f ? float.PositiveInfinity : intervalSeconds / chance;
    }
}
