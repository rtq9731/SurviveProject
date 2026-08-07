using Survive.World;

namespace Survive.Building
{
    /// <summary>
    /// 돌파정을 놓으려는 자리에 대해 세계가 아는 것 전부.
    ///
    /// <b>왜 자리를 구조체로 받는가.</b> 판정이 <c>Physics.Raycast</c>나
    /// <c>DescentZone</c>을 직접 뒤지면 Unity 없이 시험할 수 없고, 시험할 수 없는
    /// 판정은 경계값이 어디인지 아무도 모르는 채로 굳는다. 세계를 읽는 일은
    /// 껍데기가 하고(<c>Survive.Vehicles.BreachPodDeployer</c>) 여기는 값만 본다 —
    /// <see cref="HazardZone"/>·<see cref="MacroniumContact"/>가 잡아 둔 결과 그대로다.
    /// </summary>
    public readonly struct BreachPodSite
    {
        /// <summary>조준한 곳에 놓을 면이 있는가. 허공을 보고 있으면 거짓이다.</summary>
        public readonly bool HasSurface;

        /// <summary>그 자리에 구간이 하나 걸려 있는가. 아무 구간도 없으면 거짓.</summary>
        public readonly bool HasZone;

        /// <summary>걸린 구간이 무슨 위협인가. 짙은 층이어야 돌파정이 선다.</summary>
        public readonly EnvironmentHazard Hazard;

        /// <summary>층의 윗면 높이. 이것이 곧 <b>드러난 면</b>이다.</summary>
        public readonly float LayerTopY;

        /// <summary>놓으려는 면의 높이. 층의 윗면과 견주는 값이 이것이다.</summary>
        public readonly float SurfaceY;

        /// <summary>그 자리에 이미 돌파정이 서 있는가.</summary>
        public readonly bool Occupied;

        public BreachPodSite(bool hasSurface, bool hasZone, EnvironmentHazard hazard,
                             float layerTopY, float surfaceY, bool occupied)
        {
            HasSurface = hasSurface;
            HasZone = hasZone;
            Hazard = hazard;
            LayerTopY = layerTopY;
            SurfaceY = surfaceY;
            Occupied = occupied;
        }

        /// <summary>아무것도 없는 자리. 허공을 보고 있을 때가 이것이다.</summary>
        public static BreachPodSite Nowhere =>
            new BreachPodSite(false, false, EnvironmentHazard.None, 0f, 0f, false);

        /// <summary>층이 드러난 자리 하나. 검증과 시험이 쓰는 지름길이다.</summary>
        public static BreachPodSite OnLayer(float layerTopY, float surfaceY, bool occupied = false) =>
            new BreachPodSite(true, true, EnvironmentHazard.MacroniumLayer,
                              layerTopY, surfaceY, occupied);
    }

    /// <summary>
    /// 돌파정의 <b>배치 판정</b> (스펙 §6). 챕터 1의 출구를 어디에 놓을 수 있는가.
    ///
    /// <b>이 물건은 건축물도 아니고 손에 드는 도구도 아니다.</b> 둘의 성질을 나눠 갖는다 —
    /// <b>놓을 자리를 판정하고</b>(건축), <b>놓은 뒤 탄다</b>(탈것). 그래서 판정은
    /// 건축에서 빌리고 탑승은 <see cref="BreachPodLaunch"/>가 든다.
    ///
    /// <b>축을 새로 만들지 않는다.</b> 답은 건축과 같은 <see cref="PlacementResult"/>이고,
    /// 묻는 순서도 <c>Survive.Building.BuildPlacer.Evaluate</c>와 같다 —
    /// <b>지을 줄 아는가 → 놓을 면이 있는가 → 그 면이 맞는 면인가 → 겹치지 않는가 →
    /// 재료가 있는가</b>. 순서가 어긋나면 같은 상황에서 건축과 돌파정이 다른 사유를
    /// 내고, 플레이어는 규칙이 둘이라고 배운다. 그 순서를 시험이 못 박는다
    /// (<c>BreachPodPlacementTests</c>).
    ///
    /// <b>돌파정만 다른 것은 「맞는 면」의 뜻 하나다.</b> 건축은 지면이냐 구조물이냐를
    /// 묻고(<see cref="PlacementMode"/>), 돌파정은 <b>진한 층이 드러났는가</b>를 묻는다.
    /// 그 하나를 위해 사유가 하나 늘었을 뿐이다(<see cref="PlacementResult.NotDenseLayer"/>).
    ///
    /// <b>왜 「드러났는가」를 높이로 재는가.</b> 층은 액면 아래에 깔려 있고, 그 위를 바위나
    /// 갓이 덮고 있으면 층이 있어도 닿을 수 없다. 닿을 수 있다는 것은 <b>놓을 면이 곧
    /// 층의 윗면</b>이라는 뜻이다 — 그 위에 무엇이 얹혀 있으면 면의 높이가 층의 윗면보다
    /// 높게 나온다. 덮인 자리와 드러난 자리를 가르는 것이 이 한 줄이다.
    /// </summary>
    public static class BreachPodPlacement
    {
        /// <summary>
        /// 놓을 면이 층의 윗면과 같다고 치는 여유(m).
        ///
        /// 0으로 두면 층의 윗면에 정확히 놓인 경우만 통과한다 — 지형 콜라이더의
        /// 부동소수점 오차 하나로 종막이 막힌다. <see cref="MacroniumContact.ContactSkin"/>이
        /// 같은 이유로 발바닥 두께만큼을 봐 주고 있고, 여기는 그보다 넉넉하다:
        /// 발 하나가 아니라 <b>물건 하나</b>가 얹히는 자리라 바닥이 고르지 않다.
        /// </summary>
        public const float ExposureSkin = 0.3f;

        /// <summary>
        /// 층이 <b>드러났는가</b>. 놓을 면과 층의 윗면이 같은 높이면 드러난 것이다.
        ///
        /// 위쪽만 막지 않는 이유: 면이 층의 윗면보다 <b>낮게</b> 잡히는 것도 정상이 아니다.
        /// 그 자리는 이미 층 속이고, 층 속에서 층을 뚫기 시작할 수는 없다.
        /// </summary>
        public static bool IsExposed(float surfaceY, float layerTopY)
        {
            float gap = surfaceY - layerTopY;
            if (gap < 0f) gap = -gap;
            return gap <= ExposureSkin;
        }

        /// <summary>
        /// 여기에 돌파정을 놓을 수 있는가. 못 놓으면 왜 못 놓는지.
        /// </summary>
        /// <param name="unlocked">돌파 설계가 열렸는가. 건축의 청사진 관문과 같은 자리다.</param>
        /// <param name="hasPod">손에 돌파정이 있는가. 건축의 재료 검사와 같은 자리다.</param>
        public static PlacementResult Evaluate(in BreachPodSite site, bool unlocked, bool hasPod)
        {
            // 순서는 BuildPlacer.Evaluate와 같다. 아래 주석의 번호가 그쪽 단계다.

            // ① 지을 줄 모르면 자리를 따질 것도 없다.
            if (!unlocked) return PlacementResult.NotResearched;

            // ② 놓을 면이 있는가.
            if (!site.HasSurface) return PlacementResult.NoSurface;

            // ③ 그 면이 맞는 면인가. 건축은 지면/구조물을 묻고 여기는 층을 묻는다.
            if (!site.HasZone || site.Hazard != EnvironmentHazard.MacroniumLayer)
                return PlacementResult.NotDenseLayer;
            if (!IsExposed(site.SurfaceY, site.LayerTopY))
                return PlacementResult.NotDenseLayer;

            // ④ 겹치는가.
            if (site.Occupied) return PlacementResult.Blocked;

            // ⑤ 치를 것이 있는가.
            if (!hasPod) return PlacementResult.NotEnoughResources;

            return PlacementResult.Ok;
        }
    }
}
