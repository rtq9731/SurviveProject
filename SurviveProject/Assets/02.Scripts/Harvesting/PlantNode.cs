using System;
using UnityEngine;
using MoreMountains.Feedbacks;
using Survive.Audio;
using Survive.Domain.Audio;
using Survive.Interaction;
using Survive.Localization;
using Survive.Player;
using Survive.Progression;
using Survive.World;

namespace Survive.Harvesting
{
    /// <summary>
    /// 자라는 식물. 생태계 순환의 출발점이다.
    ///
    /// 플레이어가 캐거나 생산자가 먹으면 단계가 내려가고, 시간이 지나면 다시 자란다.
    /// 0단계로 오래 방치되면 시들어 사라진다 — 남획하면 그 구역이 빈다.
    /// </summary>
    public class PlantNode : MonoBehaviour, IHoldInteractable, IWorldStateOwner
    {
        [SerializeField] PlantNodeSO definition;

        [Tooltip("크기를 조절할 대상. 비우면 이 오브젝트")]
        [SerializeField] Transform visual;

        [Header("피드백")]
        [SerializeField] MMF_Player harvestFeedback;
        [SerializeField] MMF_Player witherFeedback;

        [Header("소리")]
        [Tooltip("캘 때. 비우면 소리 표의 plantHarvest")]
        [SerializeField] AudioCueSO harvestCue;

        [Tooltip("시들어 사라질 때. 비우면 소리 표의 plantWither")]
        [SerializeField] AudioCueSO witherCue;

        int _stage;
        float _growTimer;
        float _witherTimer;
        bool _gone;

        /// <summary>신원을 지은 자리. 난수도 같은 자리로 파생한다.</summary>
        Vector3 _site;

        /// <summary>
        /// 이 식물에서 몇 번째 수확인가. <b>단계를 쓰지 않는 이유</b>는 단계가
        /// 다시 자라며 되풀이되기 때문이다 — 3단계에서 딸 때마다 같은 것이
        /// 나오면 그것은 변주가 아니라 표다. 저장하지 않는 이유는
        /// <c>HarvestNode._rolls</c>에 적어 두었다.
        /// </summary>
        int _rolls;

        public PlantNodeSO Definition => definition;
        public int Stage => _stage;
        public bool IsEdible => !_gone && _stage > 0;

        /// <summary>먹히거나 캐여서 단계가 내려갈 때.</summary>
        public event Action<PlantNode> Consumed;

        void Awake()
        {
            if (visual == null) visual = transform;
            _stage = definition != null ? definition.maxStage : 1;
            RefreshScale();

            // 신원은 깨어날 때의 자리로 짓고 그 뒤로 바꾸지 않는다.
            _site = transform.position;
            _worldId = Survive.World.WorldId.At(WorldLedgerScope.Plant, _site);
            WorldLedgerRegistry.Register(this);
        }

        // <b>비활성화가 아니라 철거에서 뺀다.</b> 시든 식물은 스스로를 꺼 버리는데
        // (<see cref="Wither"/>), 그때 원장에서 빠지면 「시들었다」는 사실이 저장되지
        // 않아 불러온 세계에서 도로 자라 있게 된다. 없어진 것도 세계의 상태다.
        void OnDestroy() => WorldLedgerRegistry.Unregister(this);

        void Update()
        {
            if (_gone || definition == null) return;

            if (_stage < definition.maxStage)
            {
                _growTimer += Time.deltaTime;
                if (_growTimer >= definition.growSeconds)
                {
                    _growTimer = 0f;
                    _stage++;
                    _witherTimer = 0f;
                    RefreshScale();
                }
            }

            // 0단계로 오래 남아 있으면 시든다
            if (_stage <= 0 && definition.witherSeconds > 0f)
            {
                _witherTimer += Time.deltaTime;
                if (_witherTimer >= definition.witherSeconds) Wither();
            }
        }

        void RefreshScale()
        {
            if (definition == null || visual == null) return;
            float t = definition.maxStage <= 0 ? 1f : _stage / (float)definition.maxStage;
            float s = Mathf.Lerp(definition.minScale, definition.maxScale, t);
            visual.localScale = Vector3.one * s;
            visual.gameObject.SetActive(_stage > 0);
        }

        void Wither()
        {
            _gone = true;
            witherFeedback?.PlayFeedbacks();

            // 다음 줄에서 이 오브젝트가 꺼진다. 자기 몸으로 냈다면 소리도 함께 꺼진다.
            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(witherCue, book != null ? book.plantWither : null),
                              transform.position);

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 손으로 한 번 따 본 식물을 도감에 적는다 (검토회신 ⑪).
        ///
        /// <b>왜 채집이 관찰의 기준인가.</b> 보는 것만으로 열면 지나가다 눈에 스친
        /// 것까지 다 열려 목록이 처음부터 가득 찬다. 물질 분석이 「손에 쥐어 본 것」을
        /// 기준으로 삼는 것과 같은 선이고, 도감이 적는 값(자라는 단계·따는 데 걸린
        /// 시간)도 실제로 따 봐야 알 수 있는 것들이다.
        ///
        /// <b>먹힌 것은 세지 않는다.</b> <see cref="Eat"/>는 생산자가 부르는 길이라
        /// 사람이 본 적이 없다. 여기서만 적는 것이 곧 그 구분이다.
        ///
        /// 원장이 아직 서지 않았으면 조용히 넘긴다 — 도감 한 줄 때문에 채집이
        /// 실패하는 것이 더 나쁘다.
        /// </summary>
        void RecordObservation()
        {
            var key = CodexCatalog.PlantKey(definition);
            if (key == null) return;

            UnlockService.Instance?.Ledger?.Unlock(key);
        }

        /// <summary>
        /// 생산자가 한 입 먹는다. 플레이어의 채집과 같은 경로를 쓰되 전리품은 주지 않는다.
        /// </summary>
        /// <returns>얻은 영양가. 먹을 수 없으면 0.</returns>
        public float Eat()
        {
            if (!IsEdible) return 0f;
            _stage--;
            _growTimer = 0f;
            _witherTimer = 0f;
            RefreshScale();
            Consumed?.Invoke(this);
            return definition.nutritionPerStage;
        }

        // ── 플레이어 채집 ────────────────────────────────────────

        public float HoldDuration => definition != null ? definition.harvestSeconds : 1f;

        public string InteractionPrompt
        {
            get
            {
                if (definition == null || _gone) return "";
                if (_stage <= 0) return Loc.F("Prompt", "plant_not_grown", DataText.Name(definition));
                return Loc.F("Prompt", "harvest_hold", DataText.Name(definition));
            }
        }

        public bool CanInteract(PlayerContext player) => IsEdible && player?.Inventory != null;

        public void OnHoldProgress(float normalized) { }
        public void OnHoldCancelled() { }

        public void Interact(PlayerContext player)
        {
            if (!IsEdible) return;

            _stage--;
            _growTimer = 0f;
            _witherTimer = 0f;
            RefreshScale();
            harvestFeedback?.PlayFeedbacks();

            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(harvestCue, book != null ? book.plantHarvest : null),
                              transform.position);

            if (definition.dropsPerStage != null)
            {
                // 난수의 주인은 세계 시드다 (WorldSeed). 여기서 새 난수를 만들면
                // 같은 세계를 두 번 돌려도 같은 것이 안 나온다.
                var 굴림 = definition.dropsPerStage.Roll(
                    WorldSeed.Rng(WorldSeedBranch.PlantLoot, _site, _rolls));
                _rolls++;

                foreach (var stack in 굴림)
                {
                    int remaining = player.Inventory.Add(stack.item, stack.count);
                    if (remaining > 0)
                        Debug.LogWarning($"[PlantNode] 인벤토리가 가득 차 {DataText.Name(stack.item)} {remaining}개를 넣지 못했습니다.", this);
                }
            }

            RecordObservation();
            Consumed?.Invoke(this);
        }

        // ── 세계 원장 ────────────────────────────────────────────
        //
        // <b>담는 것은 단계와 사라짐뿐이다.</b> 자라는 중의 타이머
        // (<c>_growTimer</c>·<c>_witherTimer</c>)는 담지 않는다 — 그것은 다음
        // 단계까지 남은 시간이라 <b>파생값</b>이고, 남은 시간을 적으면 불러온
        // 순간부터 다시 세므로 저장해 둔 사이에 흐른 시간이 사라진다.
        // 그렇다고 절대 시각으로 바꿔 적을 값도 아니다: 사람은 식물이 몇 단계인지를
        // 보지 「다음 단계까지 3초」를 보지 않으므로, 반 뼘쯤 자란 것을 한 단계
        // 처음으로 되돌려도 세계가 달라 보이지 않는다.

        string _worldId;

        public string WorldId => _worldId;

        public WorldRecord CaptureWorld()
        {
            int 처음단계 = definition != null ? definition.maxStage : 1;
            if (!_gone && _stage == 처음단계) return null;      // 씬이 놓아둔 그대로다

            return new WorldRecord
            {
                kind = WorldLedgerScope.Plant,
                gone = _gone,
                amount = _stage,
            };
        }

        public void RestoreWorld(WorldRecord record)
        {
            if (record == null)
            {
                _gone = false;
                _stage = definition != null ? definition.maxStage : 1;
            }
            else
            {
                _gone = record.gone;
                _stage = Mathf.RoundToInt(record.amount);
            }

            _growTimer = 0f;
            _witherTimer = 0f;

            if (_gone) { gameObject.SetActive(false); return; }

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            RefreshScale();
        }
    }
}
