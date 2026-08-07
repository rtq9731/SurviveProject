using UnityEngine;

namespace Survive.Testing
{
    /// <summary>
    /// 씬에 서 있는 <b>뭍덩이</b>를 찾는 단 하나의 창구.
    ///
    /// <b>왜 따로 뺐는가 — 조용히 통과하는 검사가 제일 나쁘다.</b>
    /// 2026-08-07 이전에는 두 시나리오가 각자 <c>GameObject.Find("...")</c>를 하고
    /// <b>못 찾으면 <c>false</c>를 돌려주거나 「건너뜀」을 찍고 넘어갔다.</b> 그 상태에서는
    /// 이름이 하나만 바뀌어도 시나리오가 <b>초록불인 채로 아무것도 재지 않는다</b> —
    /// 실패보다 나쁜 것은 재지 않았다는 사실이 안 보이는 것이다.
    ///
    /// 그래서 여기서는 <b>못 찾으면 단언이 깨진다.</b> 그리고 이름이 나오는 자리를
    /// 한 군데로 모아, 지형이 새로 서는 날 고칠 곳이 하나가 되게 한다.
    ///
    /// <b>이름은 왜 옛 지도의 것인가.</b> 이 이름들은 프리팹
    /// (<c>05.Prefabs/Environment/Archipelago</c>)이 들고 있는 <b>배치 데이터</b>이고,
    /// 이 라운드는 프리팹을 건드리지 않는다. 지형은 통째로 다시 만들어질 예정이므로
    /// (스펙 §13) 그때 이 상수 셋만 갈면 된다.
    /// </summary>
    public static class E2ELandmass
    {
        /// <summary>착륙선이 선 뭍. 걸어 나가는 출발점이다.</summary>
        public const string StartName = "Island1_Landing";

        /// <summary>액면 하나 건너의 뭍. 횡단을 실측하는 상대편이 여기다.</summary>
        public const string AcrossName = "Island2_Shoal";

        /// <summary>그보다 더 먼 뭍. 통로가 어디서 끊기는가를 재는 끝점이다.</summary>
        public const string BeyondName = "Island3_Far";

        /// <summary>
        /// 이름으로 찾는다. <b>없으면 던진다.</b>
        /// 돌려주는 값이 <c>null</c>일 수 없다는 것이 이 함수의 존재 이유다.
        /// </summary>
        public static GameObject Require(string sceneName)
        {
            var go = GameObject.Find(sceneName);
            E2EHarness.Assert(go != null,
                $"뭍 「{sceneName}」이 씬에 있다 (없으면 이 시나리오는 아무것도 재지 못한다)");
            return go;
        }

        /// <summary>
        /// 그 뭍이 차지한 부피. <b>렌더러가 하나도 없으면 던진다</b> —
        /// 빈 <see cref="Bounds"/>를 돌려주면 그 뒤의 좌표 계산이 전부 원점 근처를 훑는다.
        /// </summary>
        public static Bounds RequireBounds(GameObject go)
        {
            E2EHarness.Assert(go != null, "뭍이 null이 아니다");

            var rends = go.GetComponentsInChildren<Renderer>(true);
            E2EHarness.Assert(rends.Length > 0, $"뭍 「{go.name}」에 그려지는 것이 있다");

            var bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);

            E2EHarness.Assert(bounds.size.sqrMagnitude > 1f,
                $"뭍 「{go.name}」의 크기가 0이 아니다 ({bounds.size})");
            return bounds;
        }

        /// <summary>이름으로 찾아 부피까지 한 번에. 둘 중 어느 쪽이 없어도 던진다.</summary>
        public static Bounds RequireBounds(string sceneName) => RequireBounds(Require(sceneName));
    }
}
