using UnityEngine;

namespace Survive.Progression
{
    /// <summary>
    /// 연구대에 걸린 항목 하나 — "무엇을, 지금 어디까지".
    ///
    /// <see cref="Survive.Crafting.CraftJob"/>과 같은 모양이되 <b>수량이 없다</b>.
    /// 같은 것을 두 번 알아낼 수는 없기 때문이다. 그래서 남은 시간도 한 개분뿐이고
    /// "다 만들었는데 넣을 자리가 없다"는 상태도 없다 — 산출물이 아이템이 아니라
    /// 원장의 한 줄이라 소지품이 찰 일이 없다.
    /// </summary>
    public class ResearchJob
    {
        public ResearchJob(ResearchEntrySO entry, float seconds)
        {
            Entry = entry;
            Seconds = Mathf.Max(0f, seconds);
        }

        public ResearchEntrySO Entry { get; }

        /// <summary>여기까지 들여다본 시간.</summary>
        public float Elapsed { get; internal set; }

        /// <summary>다 보는 데 드는 시간. 에셋의 researchSeconds를 걸 때 찍어 둔 값이다.</summary>
        public float Seconds { get; }

        /// <summary>게이지가 읽는 값 0~1.</summary>
        public float Progress => Seconds <= 0f ? 1f : Mathf.Clamp01(Elapsed / Seconds);

        public float SecondsLeft => Mathf.Max(0f, Seconds - Elapsed);

        public bool IsDone => Elapsed >= Seconds;
    }
}
