using Survive.Items;
using Survive.Localization;

namespace Survive.Harvesting
{
    /// <summary>
    /// 거대 버섯 벌목의 규칙. Unity에 기대지 않는 순수 부분만 여기 있다.
    ///
    /// <b>왜 에셋이 아니라 상수인가.</b> 벌목 노드는 씬에 놓여 있지 않고
    /// <c>Survive.World.MushroomLumberService</c>가 실행 시점에 세운다
    /// (MainScene은 병합할 수 없는 단일 파일이라 손대지 않는다). 세우는 쪽이
    /// 정의를 스스로 만들어야 하므로 수치의 주인도 코드여야 한다 —
    /// <c>GlowCapCluster</c>가 갓 수확량을 상수로 든 것과 같은 이유다.
    /// 그 대신 이 수치들은 EditMode 테스트가 직접 지킨다.
    /// </summary>
    public static class MushroomLumberRule
    {
        /// <summary>벌목으로 나오는 것. 세계에서 유일하게 <b>타는</b> 물질이다.</summary>
        public const string WoodItemId = "mushroom_wood";

        /// <summary>
        /// 노드 이름. 프롬프트에 그대로 나온다.
        ///
        /// <b>이 하나만 상수가 아니다.</b> 나머지 수치는 규칙이지만 이것은 <b>말</b>이라
        /// 번역 표가 주인이다. <c>const</c>인 채로는 로케일을 따라올 수 없다 —
        /// 컴파일 시점에 부르는 쪽에 박혀 버려 표를 다시 볼 기회가 없다.
        /// 실행 시점에 세우는 노드라 08.Data에 에셋이 없어서 이름표도 손으로 짓는다
        /// (<c>World/mushroom_tree_name</c>).
        /// </summary>
        public static string DisplayName => Loc.T("World", "mushroom_tree_name");

        /// <summary>
        /// 그루터기에서 다시 자라기까지. 300초다.
        ///
        /// 기존 재생 관례의 자릿수를 따르되 가장 느린 쪽이다 —
        /// 발광 버섯 노드 90초, 군락 갓 180초(<see cref="Survive.World.GlowGroveRule"/>),
        /// 그리고 나무 한 그루가 다시 서는 데는 그보다 오래 걸려야 한다.
        /// 이보다 빠르면 한자리에 서서 같은 버섯만 베는 것이 최적해가 되고,
        /// 그러면 "벌목량 = 세울 수 있는 것과 지킬 수 있는 불"이라는 밸런스 축이 무의미해진다.
        /// (다리 관문이 빠지며 축이 이쪽으로 옮겨 왔다 — 기획서 §6.4.)
        /// </summary>
        public const float RegrowSeconds = 300f;

        /// <summary>한 그루에서 나오는 목재. 평균 5개다.</summary>
        public const int MinYield = 4;
        public const int MaxYield = 6;

        /// <summary>
        /// 내구도. 도끼(damage 12) 두 번이면 넘어간다.
        /// 매크로늄 광맥 34(곡괭이 세 번)보다 무르고 기계 잔해 16보다 단단하다 —
        /// 살아 있는 것이 돌보다 무른 것은 당연하고,
        /// 그래도 한 방에 쓰러지면 도구를 만든 보람이 없다.
        /// </summary>
        public const float Durability = 20f;

        /// <summary>
        /// 벨 수 있는 도구. <b>도끼뿐이다.</b>
        ///
        /// 곡괭이로도 되게 하면 도끼를 만들 이유가 사라지고, 도구 이름이
        /// 그 도구가 하는 일을 말해 주지 않게 된다(<see cref="ToolMatch"/>).
        /// 잠기는 것이 걱정될 일은 없다 — 도끼는 스크랩과 기계 부품만으로
        /// 손에서 만들 수 있고, 죽어도 도구는 떨어지지 않는다.
        /// </summary>
        public const ToolType RequiredTool = ToolType.Axe;

        /// <summary>요구 등급. 지금 세계에 있는 도끼는 1등급 하나뿐이다.</summary>
        public const int RequiredTier = 1;

        /// <summary>
        /// 이 이름의 오브젝트를 거대 버섯으로 본다.
        ///
        /// 씬의 버섯 메시는 전부 <c>Mushroom_...</c> 꼴이고 크기가 이름에 적혀 있다
        /// (Giant / Big / Medium). <b>Giant만</b> 벤다 — 나머지는 발밑의 풀이고,
        /// 그것까지 도끼질 대상으로 만들면 무엇이 자원인지 읽히지 않는다
        /// (<c>GlowGroveService</c>가 장식 버섯을 갓에서
        /// 제외한 것과 같은 판단).
        /// </summary>
        public static bool IsGiant(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            return objectName.StartsWith("Mushroom") && objectName.Contains("Giant");
        }

        /// <summary>
        /// 벤 시각으로부터 다시 자랐는가. 경계는 자란 것으로 본다 —
        /// <see cref="Survive.World.GlowGroveRule.HasRegrown"/>과 같은 관례다.
        /// </summary>
        public static bool HasRegrown(float felledAt, float now, float regrowSeconds)
            => now - felledAt >= regrowSeconds;
    }
}
