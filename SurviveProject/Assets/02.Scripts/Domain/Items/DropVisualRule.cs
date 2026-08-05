using UnityEngine;

namespace Survive.Items
{
    /// <summary>바닥에 떨어진 아이템을 무엇으로 그릴지.</summary>
    public enum DropVisualKind
    {
        /// <summary>아이템 자신의 3D 프리팹. 있으면 언제나 이것이 이긴다.</summary>
        WorldPrefab,

        /// <summary>떨구는 쪽이 넘긴 프리팹. 특정 대상만 다르게 보이려는 덮어쓰기.</summary>
        SpawnerPrefab,

        /// <summary>인벤토리 아이콘을 세운 판. 프리팹이 없는 대다수가 여기로 온다.</summary>
        IconBillboard,

        /// <summary>아이콘조차 없을 때의 마지막 수단. 무엇인지 알 수 없는 덩어리다.</summary>
        Fallback,
    }

    /// <summary>
    /// 떨어진 것이 무엇인지 보이게 하는 판정.
    ///
    /// 예전에는 프리팹이 없으면 무조건 같은 베이지색 큐브였다. 스물세 종 가운데
    /// 프리팹을 가진 것은 셋뿐이라, 실제로 굴러다니는 스무 종이 전부 같은 모양이었다.
    /// 세계에 놓을 3D 모델을 스무 개 만드는 것은 이 자리에서 할 일이 아니고,
    /// <b>인벤토리 아이콘은 이미 다 있다</b> — 그것을 그대로 세워 쓴다.
    ///
    /// 판정만 여기 둔 이유는 테스트 때문이다. "어떤 아이템도 Fallback으로 떨어지지
    /// 않는다"를 데이터베이스 전 항목에 대해 EditMode에서 돌 수 있어야,
    /// 아이콘 하나를 빠뜨린 새 아이템이 조용히 들어오는 일을 막는다.
    /// </summary>
    public static class DropVisualRule
    {
        /// <summary>
        /// 아이콘 판의 한 변(m). 사람 무릎보다 낮은 물건이라는 감각을 유지하되,
        /// 3m쯤 떨어져서도 무엇인지 읽히는 크기.
        /// </summary>
        public const float IconSize = 0.36f;

        /// <summary>
        /// 아이콘 판의 자체 발광 세기.
        ///
        /// 이 게임의 대원칙은 어둠을 지키는 것이다. 1을 넘기면 낙하물이 스스로
        /// 조명이 되어 바닥을 물들이고, 아이콘은 흰 덩어리로 뭉개져 오히려
        /// 무엇인지 알 수 없게 된다. 아이콘의 <b>색과 형태</b>가 검은 배경에서
        /// 겨우 떠오를 만큼만 준다.
        /// </summary>
        public const float IconEmission = 0.5f;

        /// <summary>아이콘이 없을 때 쓰는 색. 무채색에 가깝게 둬서 눈에 띄되 무엇인 척하지 않는다.</summary>
        public static readonly Color FallbackTint = new Color(0.85f, 0.85f, 0.70f);

        /// <summary>
        /// 겉모습을 고른다. 순서에 이유가 있다:
        /// 손으로 만든 3D 모델 → 떨구는 쪽의 덮어쓰기 → 아이콘 → 마지막 수단.
        /// </summary>
        public static DropVisualKind Choose(ItemDataSO item, bool spawnerPrefabGiven)
        {
            if (item == null) return DropVisualKind.Fallback;
            if (item.worldPrefab != null) return DropVisualKind.WorldPrefab;
            if (spawnerPrefabGiven) return DropVisualKind.SpawnerPrefab;
            if (item.icon != null) return DropVisualKind.IconBillboard;
            return DropVisualKind.Fallback;
        }

        /// <summary>떨어진 것을 보고 무엇인지 알 수 있는가. Fallback만 아니면 된다.</summary>
        public static bool IsRecognizable(DropVisualKind kind) => kind != DropVisualKind.Fallback;
    }
}
