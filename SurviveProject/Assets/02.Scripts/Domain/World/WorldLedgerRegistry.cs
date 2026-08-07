using System.Collections.Generic;

namespace Survive.World
{
    /// <summary>
    /// 원장에 자기 상태를 맡기는 것. 채집물·군락의 갓·식물이 구현한다.
    /// </summary>
    public interface IWorldStateOwner
    {
        /// <summary>
        /// 원장 안에서의 신원. <b>한 번 정해지면 변하지 않아야 한다</b> —
        /// 자리로 짓되 <b>깨어날 때의 자리</b>로 짓는다. 매번 지금 자리로 지으면
        /// 물리나 검증이 물체를 조금 밀어 놓은 것만으로 저장할 때와 불러올 때의
        /// 열쇠가 달라진다.
        /// </summary>
        string WorldId { get; }

        /// <summary>
        /// 지금 상태를 원장에 맡긴다. <b>씬이 놓아둔 그대로면 <c>null</c></b> —
        /// 원장은 달라진 것만 담는다.
        /// </summary>
        WorldRecord CaptureWorld();

        /// <summary>
        /// 원장이 돌려주는 상태. 원장에 줄이 없으면 <c>null</c>이 오고,
        /// 그때 올바른 모습은 <b>씬이 놓아둔 그대로</b>다.
        /// </summary>
        void RestoreWorld(WorldRecord record);
    }

    /// <summary>
    /// 원장이 훑을 대상들의 등록부.
    ///
    /// <b>왜 <c>SaveCoordinator.Collect()</c>를 늘리지 않았는가.</b> 저쪽은
    /// <b>Start에서 한 번</b> 씬을 훑는다. 세계의 물건은 그 뒤에 태어난다 —
    /// 군락의 갓과 거대 버섯 그루터기는 <c>GlowGroveService</c>·
    /// <c>MushroomLumberService</c>가 실행 시점에 붙이고, 그 시점은 저 한 번의
    /// 훑기보다 뒤일 수도 앞일 수도 있다. 한 번 훑기에 얹으면 <b>어떤 날은
    /// 담기고 어떤 날은 안 담기는</b> 저장본이 나오고, 그것은 화면에 아무
    /// 오류도 내지 않는다.
    ///
    /// 그래서 <see cref="LitZoneRegistry"/>와 같은 모양을 쓴다 — 물건이
    /// <c>OnEnable</c>에서 스스로 들어오고 <c>OnDisable</c>에서 나간다.
    /// Unity가 빌드·철거·비활성화·씬 언로드 전부에서 그 콜백을 보장하므로
    /// 생명주기를 여기서 따로 신경 쓸 필요가 없다.
    ///
    /// <b>원장이 훑고, 물건은 부르지 않는다.</b> 열넷이 각자 저장에 참여하면
    /// 창구가 열넷이 되고, 그것이 「권한자 경유」가 막으려는 모양 그대로다.
    /// 물건이 하는 일은 <b>등록과 응답</b>뿐이다.
    /// </summary>
    public static class WorldLedgerRegistry
    {
        static readonly List<IWorldStateOwner> _owners = new List<IWorldStateOwner>();

        /// <summary>지금 등록된 것들.</summary>
        public static IReadOnlyList<IWorldStateOwner> Owners => _owners;

        /// <summary>
        /// 등록한다. 등록되는 순간 <see cref="Arrived"/>가 불리므로,
        /// 원장이 이미 저장본을 읽어 둔 뒤에 태어난 물건도 제 상태를 받는다.
        /// </summary>
        public static void Register(IWorldStateOwner owner)
        {
            if (owner == null || _owners.Contains(owner)) return;
            _owners.Add(owner);
            Arrived?.Invoke(owner);
        }

        public static void Unregister(IWorldStateOwner owner) => _owners.Remove(owner);

        /// <summary>
        /// 새로 등록된 것이 있다. 원장을 들고 있는 쪽이 여기 붙어
        /// <b>늦게 태어난 물건에게도 제 줄을 건넨다.</b>
        /// </summary>
        public static event System.Action<IWorldStateOwner> Arrived;

        /// <summary>검증·씬 전환 사이에 비운다.</summary>
        public static void Clear() => _owners.Clear();
    }
}
