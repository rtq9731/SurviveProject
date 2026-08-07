using UnityEngine;
using Survive.Core;
using Survive.Player;
using Survive.World;

namespace Survive.Vitals
{
    /// <summary>
    /// 수분과 식량을 줄이고, 액체 앞에 서면 채운다 (기획서 §5.15 · 스펙 §6).
    ///
    /// <b>얼마나 줄고 얼마나 차는지는 여기서 정하지 않는다.</b> 그 규칙은
    /// <see cref="Sustenance"/>에 있고 Unity 없이 시험된다. 이 컴포넌트가 하는 일은
    /// 지금 몸이 <b>무슨 액체에 닿아 있는지</b>를 재서 규칙에 묻고, 나온 값을
    /// 게이지에 반영하는 것뿐이다 — <see cref="LiquidContactService"/>와 같은 모양이다.
    ///
    /// <b>키를 만들지 않았다. 물가에 서 있으면 저절로 채워진다.</b>
    /// 이 항목의 설계 목표가 「잡무가 되지 않는 것」인데, 마시는 데 단추를 하나
    /// 배정하면 그 단추가 곧 잡무다. 거점이 호수 옆이므로 <b>돌아오면 저절로
    /// 채워져 있는 것</b>이 「돌아가는 김에 채운다」의 가장 정직한 구현이다.
    ///
    /// <b>왜 씬에 놓지 않고 스스로 붙는가.</b> <see cref="LiquidContactService"/>·
    /// <see cref="DeathDropService"/>와 같은 이유다 — 플레이어 프리팹과 MainScene은
    /// 병합할 수 없는 단일 파일이라 여러 갈래로 나뉘어 일하는 동안 손대지 않는다.
    ///
    /// <b>밤낮과 물리지 않는다.</b> 자는 것이 없으므로 밤이라고 덜 마르거나 더
    /// 마를 근거가 없고, 물리는 순간 「밤에 나가면 두 배로 목마르다」 같은 규칙이
    /// 하나 더 늘어난다. 밤이 이미 어둠과 낫이라는 값을 매기고 있으므로
    /// 거기에 세 번째를 얹지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SustenanceService : MonoBehaviour
    {
        static SustenanceService _instance;
        public static SustenanceService Instance => _instance;

        /// <summary>
        /// 정제 수단을 갖췄는가. 참이면 매크로늄 표면에서도 물을 얻는다
        /// (<see cref="Sustenance.HydrationPerSecondFrom"/>).
        ///
        /// <b>지금 이것을 참으로 만드는 물건은 세계에 없다.</b> 착륙선의 처리장치가
        /// 스펙 §7에서 오고, 그때 이 값을 켜는 주인이 생긴다. 규칙은 이미 완결이고
        /// 세계에 아직 정제기가 없을 뿐이다 — 그래서 여기 가짜 값을 채워 두지 않는다.
        /// </summary>
        public static bool PurifierAvailable { get; set; }

        // ── 바깥이 읽는 상태 ─────────────────────────────────────

        public static float HydrationNormalized { get; private set; } = 1f;
        public static float FoodNormalized { get; private set; } = 1f;

        /// <summary>화면에 나타나야 하는가. <b>원정 한 번에 참이 되면 설계가 무너진 것이다.</b></summary>
        public static bool HydrationWarning => Sustenance.IsWarning(HydrationNormalized);

        /// <inheritdoc cref="HydrationWarning"/>
        public static bool FoodWarning => Sustenance.IsWarning(FoodNormalized);

        /// <summary>둘 중 하나라도 경고선 아래인가.</summary>
        public static bool AnyWarning => HydrationWarning || FoodWarning;

        /// <summary>지금 걸리는 이동 배율. 경고선 위에서는 언제나 1이다.</summary>
        public static float MoveFactor { get; private set; } = 1f;

        /// <summary>지금 발이 액체에 닿아 있는가.</summary>
        public static bool AtLiquid { get; private set; }

        /// <summary>닿아 있는 액체의 종류. 밖이면 직전 것이 남는다.</summary>
        public static LiquidKind LastLiquid { get; private set; }

        /// <summary>지금 실제로 채워지고 있는가. 「닿아 있다」와는 다른 물음이다.</summary>
        public static bool Refilling { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            HydrationNormalized = 1f;
            FoodNormalized = 1f;
            MoveFactor = 1f;
            AtLiquid = false;
            Refilling = false;
            LastLiquid = LiquidKind.Macronium;
            PurifierAvailable = false;

            var go = new GameObject("SustenanceService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SustenanceService>();
        }

        PlayerVitals _vitals;
        PlayerSwimming _swim;
        PlayerLocomotion _loco;

        void OnDisable() => Unhook();

        void Update()
        {
            if (!Acquire()) { Idle(); return; }

            float dt = Time.deltaTime;

            // 발이 액면 아래인가. 「액체의 수평 범위 안인가」로는 안 된다 - 바다의
            // 평면이 지도 전체를 덮고 있어서, 그것으로 물으면 언덕 위에서도 참이 된다.
            bool inLiquid = _swim.TryGetLiquid(out var kind);
            AtLiquid = inLiquid && _swim.SubmergedDepth > 0f;
            if (AtLiquid) LastLiquid = kind;

            float water = AtLiquid
                ? Sustenance.HydrationPerSecondFrom(LastLiquid, PurifierAvailable)
                : 0f;
            float feed = AtLiquid ? Sustenance.FoodPerSecondFrom(LastLiquid) : 0f;

            Refilling = water > 0f || feed > 0f;

            // 채우는 값이 감소보다 크므로, 물가에 서 있으면 순증한다. 굳이 감소를
            // 멈추지 않는 이유는 규칙을 하나로 두기 위해서다 - 「물가에서는 마르지
            // 않는다」를 따로 적으면 물가를 벗어나는 순간의 값이 두 규칙 사이에서 튄다.
            _vitals.Hydration.Modify((water - Sustenance.HydrationDrainPerSecond) * dt);
            _vitals.Food.Modify((feed - Sustenance.FoodDrainPerSecond) * dt);

            HydrationNormalized = _vitals.Hydration.Normalized;
            FoodNormalized = _vitals.Food.Normalized;

            MoveFactor = Sustenance.WorstMoveFactor(HydrationNormalized, FoodNormalized);
            if (_loco != null) _loco.ExternalSpeedFactor = MoveFactor;
        }

        void Idle()
        {
            AtLiquid = false;
            Refilling = false;
            MoveFactor = 1f;
        }

        bool Acquire()
        {
            if (_vitals != null && _swim != null) return true;

            Unhook();
            if (!GameServices.TryGet<PlayerVitals>(out var found) || found == null) return false;

            _vitals = found;
            var body = found.GetComponentInParent<PlayerContext>()?.transform ?? found.transform;
            _swim = body.GetComponentInChildren<PlayerSwimming>(true);
            _loco = body.GetComponentInChildren<PlayerLocomotion>(true);
            return _swim != null;
        }

        void Unhook()
        {
            // 잡고 있던 사람을 놓을 때 벌도 함께 푼다. 안 그러면 씬을 다시 올린 뒤에도
            // 배율이 걸린 채로 남아, 게이지는 가득인데 걸음만 느린 몸이 된다.
            if (_loco != null) _loco.ExternalSpeedFactor = 1f;

            _vitals = null;
            _swim = null;
            _loco = null;
        }
    }
}
