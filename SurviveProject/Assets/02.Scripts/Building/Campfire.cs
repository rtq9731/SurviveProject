using System.Collections;
using UnityEngine;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Core;
using Survive.Interaction;
using Survive.Items;
using Survive.Player;
using Survive.World;

namespace Survive.Building
{
    /// <summary>
    /// 화톳불. 거점을 표시하고, 랜턴을 채우고, 연료를 먹는다.
    ///
    /// 지하는 어둡고 랜턴 배터리는 유한하다. 발광 버섯 군락까지 매번 돌아가는 대신
    /// 자기 거점을 만들 수 있어야 건설에 이유가 생긴다 —
    /// 화톳불은 "여기가 내 자리다"를 세우는 첫 물건이다.
    ///
    /// 연료를 계속 넣어야 꺼지지 않는다. 켜 두면 알아서 되는 것이면
    /// 거점이 아니라 배경이 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class Campfire : MonoBehaviour, IInteractable
    {
        [SerializeField] Light flame;

        [Header("연료")]
        [Tooltip("가득 찼을 때의 연료. 초 단위로 탄다")]
        [SerializeField] float maxFuel = 180f;

        [Tooltip("스크랩 하나가 주는 연료(초)")]
        [SerializeField] float fuelPerScrap = 45f;

        [Tooltip("한 번에 넣는 스크랩 수")]
        [SerializeField] int scrapPerRefuel = 2;

        [Header("빛")]
        [SerializeField] float fullIntensity = 1.9f;
        [SerializeField] float fullRange = 10f;

        [Tooltip("불빛이 일렁이는 폭")]
        [SerializeField] float flickerAmount = 0.18f;

        [Header("랜턴 충전")]
        [Tooltip("불 곁에 있으면 랜턴이 초당 이만큼 찬다")]
        [SerializeField] float lanternRechargePerSecond = 8f;

        [SerializeField] float warmthRadius = 6f;

        [Header("피드백")]
        [SerializeField] MMF_Player refuelFeedback;

        float _fuel;
        Tween _flicker;

        public bool IsBurning => _fuel > 0f;
        public float FuelNormalized => maxFuel <= 0f ? 0f : Mathf.Clamp01(_fuel / maxFuel);

        void Awake()
        {
            if (flame == null) flame = GetComponentInChildren<Light>(true);

            // 세우자마자 한 번은 타야 한다. 지어 놓고 연료부터 넣으라고 하면
            // 무엇을 지은 건지 알 수 없다.
            _fuel = maxFuel * 0.5f;
            ApplyLight();
        }

        void Update()
        {
            if (_fuel > 0f)
            {
                _fuel = Mathf.Max(0f, _fuel - Time.deltaTime);
                if (_fuel <= 0f) ApplyLight();
            }

            if (IsBurning) WarmNearbyLantern();
        }

        void WarmNearbyLantern()
        {
            if (!GameServices.TryGet<LanternController>(out var lantern)) return;
            if (lantern == null) return;

            float d = Vector3.Distance(transform.position, lantern.transform.position);
            if (d > warmthRadius) return;

            lantern.Recharge(lanternRechargePerSecond * Time.deltaTime);
        }

        void ApplyLight()
        {
            if (flame == null) return;

            flame.enabled = IsBurning;
            _flicker?.Kill();

            if (!IsBurning) return;

            flame.range = fullRange;
            flame.intensity = fullIntensity;

            // 일렁임. 고정된 밝기는 불로 안 보인다.
            _flicker = flame.DOIntensity(fullIntensity * (1f - flickerAmount), 0.35f)
                            .SetLoops(-1, LoopType.Yoyo)
                            .SetEase(Ease.InOutSine)
                            .SetLink(gameObject);
        }

        void OnDestroy() => _flicker?.Kill();

        // ── 상호작용 ─────────────────────────────────────────────

        public string InteractionPrompt
        {
            get
            {
                int pct = Mathf.RoundToInt(FuelNormalized * 100f);
                return IsBurning
                    ? $"[E] 화톳불에 스크랩 넣기 (연료 {pct}%)"
                    : "[E] 화톳불 지피기";
            }
        }

        public bool CanInteract(PlayerContext player) =>
            player?.Inventory?.Inventory != null &&
            player.Inventory.Inventory.Has(PlayerInventory.ScrapId, 1);

        public void Interact(PlayerContext player)
        {
            var inv = player?.Inventory?.Inventory;
            if (inv == null) return;

            // 가진 만큼만 넣는다. 하나도 없으면 CanInteract에서 걸러진다.
            int take = Mathf.Min(scrapPerRefuel, inv.CountOf(PlayerInventory.ScrapId));
            if (take <= 0) return;
            if (!inv.TryRemove(PlayerInventory.ScrapId, take)) return;

            bool wasOut = !IsBurning;
            _fuel = Mathf.Min(maxFuel, _fuel + fuelPerScrap * take);

            if (wasOut) ApplyLight();
            refuelFeedback?.PlayFeedbacks();
        }
    }
}
