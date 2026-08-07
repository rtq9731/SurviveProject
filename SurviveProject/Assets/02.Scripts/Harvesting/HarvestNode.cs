using System.Collections;
using UnityEngine;
using DG.Tweening;
using MoreMountains.Feedbacks;
using Survive.Audio;
using Survive.Combat;
using Survive.Domain.Audio;
using Survive.Interaction;
using Survive.Items;
using Survive.Localization;
using Survive.Player;
using Survive.World;

namespace Survive.Harvesting
{
    /// <summary>
    /// 자원을 주는 채집 노드. 두 가지 방식이 있다.
    ///
    /// <b>맨손 채집</b> — 눌러서 채운다. 잔해를 뒤지는 동작이라 시간이 걸린다.
    /// <b>도구 채집</b> — 때려서 부순다. 곡괭이를 만든 보람이 손끝에 있어야 해서,
    /// 광맥을 E로 홀드하는 대신 실제로 내리찍고 부서지는 것을 보게 한다.
    /// 부수면 자원이 인벤토리로 순간이동하지 않고 바닥에 튀어나와 굴러서,
    /// 플레이어가 가서 줍는다.
    /// </summary>
    public class HarvestNode : MonoBehaviour, IHoldInteractable, IDamageable, IWorldStateOwner
    {
        [SerializeField] HarvestNodeSO definition;

        [Header("표시")]
        [Tooltip("채집 후 숨길 대상. 비우면 이 오브젝트를 숨긴다")]
        [SerializeField] GameObject visual;

        [Tooltip("떨어뜨릴 아이템의 겉모습. 비우면 임시 큐브로 떨군다")]
        [SerializeField] GameObject dropPrefab;

        [Header("피드백")]
        [Tooltip("채집을 시작할 때")]
        [SerializeField] MMF_Player startFeedback;

        [Tooltip("채집이 끝났을 때. 파편·획득음")]
        [SerializeField] MMF_Player completeFeedback;

        [Tooltip("때렸는데 부서지지는 않았을 때. 파편·타격음")]
        [SerializeField] MMF_Player hitFeedback;

        [Header("소리")]
        // 광맥과 버섯이 같은 소리를 낼 이유는 없다. 다르게 하고 싶은 노드만
        // 여기를 채우고, 나머지는 소리 표의 값을 쓴다.
        [Tooltip("때렸을 때. 비우면 소리 표의 harvestHit")]
        [SerializeField] AudioCueSO hitCue;

        [Tooltip("부서질 때. 비우면 소리 표의 harvestBreak")]
        [SerializeField] AudioCueSO breakCue;

        [Tooltip("맞지 않는 도구로 때렸을 때. 비우면 소리 표의 harvestWrongTool")]
        [SerializeField] AudioCueSO wrongToolCue;

        bool _depleted;
        float _health = -1f;
        PlayerToolHolder _toolHolder;
        Tween _shake;
        Vector3 _baseScale;
        Vector3 _visualBaseScale;
        float _respawnOverride = -1f;
        float _depletedAt;
        Tween _regrow;

        /// <summary>
        /// 신원을 지은 자리. 난수도 같은 자리로 파생한다 — 신원과 어긋나면
        /// 「저장된 그 자리」와 「굴린 그 자리」가 다른 것을 가리킨다.
        /// </summary>
        Vector3 _site;

        /// <summary>
        /// 이 노드에서 몇 번째 소진인가. 재생으로 돌아온 노드가 <b>영영 같은
        /// 것</b>만 떨구지 않게 굴림마다 하나씩 는다.
        ///
        /// <b>저장하지 않는다.</b> 저장이 지켜야 하는 것은 「이 세계의 저 덤불에는
        /// 무엇이 있다」이고, 그것은 아직 굴리지 않은 <b>다음 한 번</b>의 답이다.
        /// 갓 불러온 세계에서 그 답이 같으려면 이 값도 같아야 하는데, 불러온
        /// 노드는 씬이 놓아둔 그대로 0에서 시작하고 저장할 때도 0이었다 —
        /// 이미 소진된 노드는 원장이 <c>gone</c>으로 들고 있어 다시 굴리지 않는다.
        /// 재생을 여러 번 건넌 뒤의 횟수까지 맞추려면 원장의 줄마다 칸을 하나
        /// 더 두어야 하는데, 그것으로 지키는 것은 <b>겉보기 변주</b>뿐이다.
        /// </summary>
        int _rolls;

        public HarvestNodeSO Definition => definition;
        public bool IsDepleted => _depleted;

        /// <summary>
        /// 이 노드가 실제로 쓰는 재생 주기(초). 0이면 돌아오지 않는다.
        /// 보통은 정의가 들고 있는 값이고, 검증이 덮어쓴 값이 있으면 그쪽이 이긴다.
        /// </summary>
        public float RespawnSeconds =>
            _respawnOverride >= 0f ? _respawnOverride
            : definition != null ? definition.respawnSeconds : 0f;

        /// <summary>
        /// 이 노드 하나의 재생 주기를 바꾼다. <b>검증에서만 쓴다</b> —
        /// 300초를 실시간으로 기다리는 검사는 아무도 돌리지 않게 된다.
        /// 정의(에셋)는 건드리지 않는다. 에셋을 고치면 에디터가 그것을
        /// 디스크에 저장해 버려, 검증 한 번이 게임의 수치를 바꾼다.
        /// </summary>
        public void OverrideRespawnSeconds(float seconds) => _respawnOverride = Mathf.Max(0f, seconds);

        /// <summary>
        /// 돌아오기까지 남은 초. 다 캐지 않았으면 0이다.
        ///
        /// <b>세계 시계로 잰다</b>(<see cref="WorldClock"/>). 프레임 시계는 저장되지
        /// 않고, 배속을 타고, 씬 로드로 0이 된다 — 셋 다 재생이 원하지 않는 성질이다.
        /// 특히 마지막 것은 조용히 치명적이다: 시계만 0으로 돌아가고 <c>_depletedAt</c>이
        /// 큰 값으로 남으면 그 자리는 <b>영영</b> 안 돌아온다.
        /// </summary>
        public float RespawnRemaining => !_depleted
            ? 0f
            : HarvestRespawnRule.Remaining(_depletedAt, WorldClock.Seconds, RespawnSeconds);

        /// <summary>
        /// 실행 시점에 노드를 세우는 서비스가 정의를 물려 준다.
        ///
        /// 씬에 놓인 노드는 인스펙터에서 정의를 받지만, MainScene을 건드리지 않고
        /// 세워야 하는 것들(거대 버섯)은 코드로 붙는다 — 그때 쓰는 창구다.
        /// 이미 정의가 있는 노드는 덮어쓰지 않는다.
        /// </summary>
        public void Bind(HarvestNodeSO def)
        {
            if (def == null || definition != null) return;
            definition = def;
            _health = def.durability;
        }

        /// <summary>도구가 필요한 노드는 눌러서 캐는 게 아니라 부순다.</summary>
        public bool IsBreakable => definition != null && definition.requiredTool != ToolType.None;

        /// <summary>남은 내구도 비율. 0~1. 부술 수 없는 노드는 항상 1.</summary>
        public float HealthNormalized =>
            !IsBreakable || definition.durability <= 0f ? 1f
            : Mathf.Clamp01(CurrentHealth / definition.durability);

        float CurrentHealth
        {
            get
            {
                if (_health < 0f) _health = definition != null ? definition.durability : 1f;
                return _health;
            }
        }

        void Awake()
        {
            if (visual == null) visual = gameObject;
            _baseScale = transform.localScale;
            _visualBaseScale = visual.transform.localScale;

            // <b>신원은 깨어날 때의 자리로 짓고 그 뒤로 바꾸지 않는다.</b>
            // 매번 지금 자리로 지으면 물리나 검증이 물체를 조금 밀어 놓은 것만으로
            // 저장할 때와 불러올 때의 열쇠가 달라져, 저장본이 아무 자리도 못 찾는다.
            _site = transform.position;
            _worldId = Survive.World.WorldId.At(WorldLedgerScope.Harvest, _site);

            // <b>비활성화가 아니라 철거에서 뺀다.</b> 다 캔 노드는 겉모습을 끄는데
            // (돌아오지 않는 것은 통째로), 그때 원장에서 빠지면 「다 캤다」가
            // 저장되지 않아 불러온 세계에서 도로 서 있게 된다.
            WorldLedgerRegistry.Register(this);
        }

        void OnDestroy()
        {
            WorldLedgerRegistry.Unregister(this);
            _shake?.Kill();
            _regrow?.Kill();
        }

        ToolItemSO equipped => _toolHolder != null ? _toolHolder.EquippedTool : null;

        // ── 맨손 채집 (홀드) ─────────────────────────────────────

        public float HoldDuration
        {
            get
            {
                if (definition == null) return 1f;
                if (IsBreakable) return 0f;      // 부수는 노드는 홀드하지 않는다
                float power = equipped != null ? Mathf.Max(0.01f, equipped.harvestPower) : 1f;
                return definition.baseDuration / power;
            }
        }

        public string InteractionPrompt
        {
            get
            {
                if (definition == null || _depleted) return "";

                if (IsBreakable)
                {
                    if (!ToolSatisfied(equipped))
                        return Loc.F("Prompt", "harvest_tool_needed",
                                     DataText.Name(definition), ToolName(definition.requiredTool));

                    // 부수는 대상에는 E가 아니라 공격 키를 안내해야 한다
                    int pct = Mathf.CeilToInt(HealthNormalized * 100f);
                    return Loc.F("Prompt", "harvest_break", DataText.Name(definition), pct);
                }

                // 홀드형이라는 것이 문구에서 드러나야 한다 — "[E]"만 쓰면 탭으로 오해한다.
                // 그 사정은 표의 ko 칸("길게 눌러")에 들어 있다.
                return Loc.F("Prompt", "harvest_hold", DataText.Name(definition));
            }
        }

        public bool CanInteract(PlayerContext player)
        {
            if (definition == null || _depleted || player == null) return false;
            _toolHolder = player.ToolHolder;

            // 부수는 노드는 E로 상호작용하지 않는다. 프롬프트만 보여준다.
            if (IsBreakable) return false;
            return ToolSatisfied(equipped);
        }

        /// <summary>
        /// 규칙 자체는 <see cref="ToolMatch"/>에 있다 — 도구가 전용이라는 것은
        /// 이 컴포넌트의 사정이 아니라 게임의 규칙이라 Unity 없이 검증되어야 한다.
        /// </summary>
        bool ToolSatisfied(ToolItemSO tool) => ToolMatch.Satisfies(
            definition.requiredTool, definition.requiredTier,
            tool != null ? tool.toolType : ToolType.None,
            tool != null ? tool.tier : 0);

        static string ToolName(ToolType t) => ToolMatch.Name(t);

        public void OnHoldProgress(float normalized)
        {
            if (normalized > 0f && startFeedback != null && !startFeedback.IsPlaying)
                startFeedback.PlayFeedbacks();
        }

        public void OnHoldCancelled() => startFeedback?.StopFeedbacks();

        public void Interact(PlayerContext player)
        {
            if (_depleted || definition == null || IsBreakable) return;

            startFeedback?.StopFeedbacks();
            Deplete(toInventory: player);
        }

        // ── 도구 채집 (파괴) ─────────────────────────────────────

        bool IDamageable.IsDead => _depleted;

        public void TakeDamage(in DamageInfo info)
        {
            if (_depleted || definition == null || !IsBreakable) return;

            var book = AudioService.Book;

            // 맞는 순간의 도구를 본다. 곡괭이 없이 주먹으로는 광맥이 깨지지 않는다.
            var holder = info.Source != null
                ? info.Source.GetComponentInParent<PlayerToolHolder>()
                : null;
            if (holder != null) _toolHolder = holder;

            if (!ToolSatisfied(equipped))
            {
                // 튕겨나가는 반응. 아무 일도 안 일어나면 버그로 오해한다.
                Recoil(0.06f);
                WrongToolHits++;
                Announce();

                AudioService.Play(
                    AudioCueBookSO.Or(wrongToolCue, book != null ? book.harvestWrongTool : null),
                    info.HitPoint);
                return;
            }

            _health = CurrentHealth - Mathf.Max(0.01f, info.Amount);
            hitFeedback?.PlayFeedbacks();

            AudioService.Play(AudioCueBookSO.Or(hitCue, book != null ? book.harvestHit : null),
                              info.HitPoint);

            if (_health > 0f)
            {
                Recoil(0.14f);
                return;
            }

            Break(info.HitPoint);
        }

        /// <summary>
        /// 맞지 않는 도구로 때린 횟수. 검증에서 "정말 안 먹혔는가"를 집는다.
        /// </summary>
        public static int WrongToolHits { get; private set; }

        /// <summary>
        /// 안 먹힌다고 한 줄 알려 준다.
        ///
        /// 흔들리기만 하면 "때리는 중"으로 읽힌다. 특히 곡괭이로 광맥을 부수던
        /// 사람이 같은 손으로 버섯을 때릴 때, 왜 안 부서지는지 화면 어디에도
        /// 적혀 있지 않으면 고장으로 읽는다.
        ///
        /// 안내 창구는 새로 만들지 않는다 — 조작키를 알려 주던
        /// <see cref="Survive.UI.ControlHintView"/>가 이미 "필요한 순간에 한 번만"
        /// 이라는 규칙을 들고 있으므로 그것을 그대로 쓴다.
        /// </summary>
        void Announce()
        {
            if (_hint == null)
                _hint = FindAnyObjectByType<Survive.UI.ControlHintView>(FindObjectsInactive.Include);

            _hint?.Show("wrongtool_" + definition.requiredTool,
                        Loc.F("Prompt", "wrong_tool",
                              DataText.Name(definition), ToolName(definition.requiredTool)));
        }

        Survive.UI.ControlHintView _hint;

        /// <summary>맞은 티가 나게 흔든다. 남은 내구도가 적을수록 크게 흔들린다.</summary>
        void Recoil(float strength)
        {
            _shake?.Kill();
            transform.localScale = _baseScale;
            float extra = Mathf.Lerp(1.4f, 1f, HealthNormalized);
            _shake = transform.DOPunchScale(_baseScale * strength * extra, 0.22f, 9, 0.7f);
        }

        void Break(Vector3 hitPoint)
        {
            _shake?.Kill();
            transform.localScale = _baseScale;

            Vector3 origin = visual != null
                ? visual.GetComponentInChildren<Renderer>()?.bounds.center ?? transform.position
                : transform.position;

            Deplete(toInventory: null, dropAt: origin);
        }

        // ── 공통 ─────────────────────────────────────────────────

        /// <summary>
        /// 노드를 소진시킨다. <paramref name="toInventory"/>가 있으면 바로 넣고,
        /// 없으면 바닥에 떨군다.
        /// </summary>
        void Deplete(PlayerContext toInventory, Vector3? dropAt = null)
        {
            _depleted = true;

            if (definition.drops != null)
            {
                // <b>난수의 주인은 세계 시드다.</b> 예전에는 여기서 new System.Random()을
                // 만들어 넘겼고, 그래서 같은 세계를 두 번 돌려도 같은 것이 안 나왔다.
                // 자리는 깨어날 때 잡은 것을 쓴다 — 원장의 신원과 같은 자리여야
                // 「저장된 그 자리」와 「굴린 그 자리」가 어긋나지 않는다.
                var loot = definition.drops.Roll(
                    WorldSeed.Rng(WorldSeedBranch.HarvestLoot, _site, _rolls));
                _rolls++;

                // 한 번의 소진에서 떨구는 것들이 서로 다른 자리에 내려앉게, 흩뿌림에는
                // 이 소진 안에서의 순번을 준다. 같은 번호를 주면 전부 겹쳐 떨어진다.
                //
                // 소진 번호에 자리를 띄워 두는 것(×64)은, 재생으로 돌아온 노드가
                // 앞 번 소진과 <b>같은 착지 지점</b>을 다시 쓰지 않게 하려는 것이다.
                // 한 번에 64개를 넘게 떨구면 다음 번과 겹치는데, 그때 일어나는
                // 최악은 물건 하나가 앞 번과 같은 자리에 내려앉는 것뿐이다.
                int 떨군수 = 0;

                foreach (var stack in loot)
                {
                    if (toInventory != null)
                    {
                        int remaining = toInventory.Inventory.Add(stack.item, stack.count);
                        if (remaining > 0)
                            Debug.LogWarning($"[HarvestNode] 인벤토리가 가득 차 {DataText.Name(stack.item)} " +
                                             $"{remaining}개를 넣지 못했습니다.", this);
                    }
                    else
                    {
                        // 하나씩 떨궈야 흩어지는 맛이 난다
                        for (int i = 0; i < stack.count; i++)
                            ItemDropper.Drop(stack.item, 1, dropAt ?? transform.position, dropPrefab,
                                             occasion: _rolls * 64 + 떨군수++);
                    }
                }
            }

            completeFeedback?.PlayFeedbacks();

            // 맨손 채집이든 부숴서 얻은 것이든 여기 한 곳을 지난다.
            // 소리도 여기 하나만 두면 새 채집 경로가 생겨도 저절로 따라온다.
            var book = AudioService.Book;
            AudioService.Play(AudioCueBookSO.Or(breakCue, book != null ? book.harvestBreak : null),
                              dropAt ?? transform.position);

            _depletedAt = WorldClock.Seconds;
            SetVisible(false);

            if (RespawnSeconds > 0f) StartCoroutine(Respawn());
        }

        /// <summary>
        /// 흔적으로 남아 있다가 조건이 맞으면 다시 찬다.
        ///
        /// <b>기다리는 것이 둘이다.</b> 하나는 시간(<see cref="HarvestRespawnRule.HasRespawned"/>),
        /// 다른 하나는 <b>사람이 보고 있지 않은 순간</b>이다. 눈앞에서 물체가 튀어나오는
        /// 것은 규칙이 아니라 사고로 읽힌다. 두 번째 조건 때문에 <c>WaitForSeconds</c>
        /// 한 번으로는 끝낼 수 없어 되물어 본다 — 주기를 다 채운 뒤에도 사람이 붙어
        /// 서 있으면 <b>그대로 기다린다</b>. 시계를 되돌리지는 않는다. 되돌리면 노드
        /// 옆에 서 있는 것만으로 그 자리를 영구히 죽일 수 있다.
        /// </summary>
        IEnumerator Respawn()
        {
            var 간격 = new WaitForSeconds(HarvestRespawnRule.PollSeconds);

            while (true)
            {
                yield return 간격;
                if (RespawnSeconds <= 0f) yield break;      // 검증이 도중에 껐다
                if (!HarvestRespawnRule.HasRespawned(_depletedAt, WorldClock.Seconds, RespawnSeconds)) continue;
                if (!Unseen()) continue;
                break;
            }

            SetVisible(true);
            _health = definition != null ? definition.durability : 1f;
            _depleted = false;
        }

        /// <summary>
        /// 지금 이 자리가 사람 눈에 안 잡히는가. 보는 주체는 카메라다 —
        /// 사람이 어디에 서 있느냐가 아니라 <b>화면에 무엇이 담기느냐</b>가
        /// 판정을 정해야 하기 때문이다. 카메라가 없으면(검증·로딩 중) 막지 않는다.
        /// </summary>
        bool Unseen()
        {
            var eye = Camera.main;
            if (eye == null) return true;

            var t = eye.transform;
            return HarvestRespawnRule.IsUnseen(t.position, t.forward, VisualCenter());
        }

        /// <summary>흔적의 한가운데. 겉모습이 없으면 발밑 좌표.</summary>
        Vector3 VisualCenter()
        {
            var r = visual != null ? visual.GetComponentInChildren<Renderer>(true) : null;
            return r != null ? r.bounds.center : transform.position;
        }

        /// <summary>
        /// 캔 자리를 감추거나 되돌린다. <b>돌아올 것과 안 돌아올 것이 다르게 사라진다.</b>
        ///
        /// <list type="number">
        /// <item><b>돌아오지 않는 것</b>(합금 더미)은 전과 같이 자취 없이 사라진다.
        ///   다 캔 매장지에 남길 것이 없다.</item>
        /// <item><b>돌아오는 것</b>은 <see cref="HarvestRespawnRule.RemnantScale"/>만큼
        ///   납작하게 눌린 <b>흔적</b>으로 남는다. 흔적이 하는 일이 둘이다 —
        ///   통째로 사라졌다가 튀어나오는 것을 막고, "여기는 다시 찬다"를
        ///   플레이어에게 <b>가르친다</b>. 세계에서 유일하게 되살아나는 자리를
        ///   아무 표시 없이 두면 배울 방법이 없다.</item>
        /// </list>
        ///
        /// 어느 쪽이든 <b>오브젝트를 끄지는 않는다</b> — 끄는 순간 <see cref="Respawn"/>
        /// 코루틴이 함께 죽어 영영 돌아오지 않는다(<c>GlowCapCluster</c>가 같은 이유로
        /// 택한 방식이다). 콜라이더를 끄므로 흔적은 겨눠지지도, 부딪히지도 않는다.
        ///
        /// 빛도 함께 끈다. 발광하는 거대 버섯을 베면 그 자리의 빛도 사라져야 한다.
        /// 밑동만 남은 자리가 여전히 환하면 무엇을 벤 것인지 알 수 없다.
        /// </summary>
        void SetVisible(bool on)
        {
            bool 돌아온다 = RespawnSeconds > 0f;

            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            if (_colliders == null) _colliders = GetComponentsInChildren<Collider>(true);
            if (_lights == null) _lights = GetComponentsInChildren<Light>(true);

            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = on || 돌아온다;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = on;
            for (int i = 0; i < _lights.Length; i++)
                if (_lights[i] != null) _lights[i].enabled = on;

            if (!돌아온다)
            {
                // 재생하지 않는 노드는 통째로 끈다. 코루틴도 돌 일이 없다.
                if (visual != null) visual.SetActive(on);
                return;
            }

            _regrow?.Kill();
            var target = on
                ? _visualBaseScale
                : Vector3.Scale(_visualBaseScale, HarvestRespawnRule.RemnantScale);

            if (!on)
            {
                // 캐는 순간은 즉시 줄어든다. 사람이 한 일의 결과라 지체가 곧 무반응으로 읽힌다.
                visual.transform.localScale = target;
                return;
            }

            // 돌아올 때는 <b>부풀어 오른다</b>. 값이 툭 바뀌면 조건을 다 지켜도 팝으로 읽힌다.
            _regrow = visual.transform.DOScale(target, HarvestRespawnRule.RegrowTweenSeconds)
                                      .SetEase(Ease.OutBack);
        }

        Renderer[] _renderers;
        Collider[] _colliders;
        Light[] _lights;

        // ── 세계 원장 ────────────────────────────────────────────
        //
        // <b>이 컴포넌트는 스스로 저장하지 않는다.</b> 세계의 상태를 저장본에
        // 싣는 창구는 <see cref="WorldLedgerService"/> 하나이고, 여기서 하는 일은
        // 등록과 응답뿐이다. 잔해가 스물몇 개인데 각자 저장에 참여하면
        // 창구가 스물몇 개가 된다.

        string _worldId;

        public string WorldId => _worldId;

        /// <summary>
        /// <b>씬이 놓아둔 그대로면 아무것도 적지 않는다</b>(<c>null</c>).
        /// 원장은 달라진 것만 담고, 그 덕분에 「다시 찬 자리」가 저절로 원장에서
        /// 빠져 불러온 세계에서 다시 서 있게 된다.
        ///
        /// <b>남은 시간이 아니라 다 캔 시각을 적는다.</b> 남은 시간을 적으면
        /// 불러온 순간부터 다시 세므로, 저장해 둔 사이에 흐른 세계 시간이
        /// 통째로 사라진다.
        /// </summary>
        public WorldRecord CaptureWorld() => !_depleted
            ? null
            : new WorldRecord
            {
                kind = WorldLedgerScope.Harvest,
                gone = true,
                at = _depletedAt,
            };

        /// <summary>
        /// 원장이 돌려주는 상태. <c>null</c>이면 씬이 놓아둔 그대로 — 곧 서 있다.
        ///
        /// <b>여기서 시각을 다시 재지 않는다.</b> 다 캔 시각만 앉히고 판정은
        /// <see cref="Respawn"/>의 되물음에 맡긴다. 불러오기 도중에는 시계 항목과
        /// 세계 항목 중 어느 쪽이 먼저 복원됐는지 알 수 없어, 그 순간 재면
        /// 저장본의 항목 순서가 게임 결과를 바꾼다.
        /// </summary>
        public void RestoreWorld(WorldRecord record)
        {
            bool 캐여있다 = record != null && record.gone;

            if (!캐여있다)
            {
                if (!_depleted) return;
                _depleted = false;
                _health = definition != null ? definition.durability : 1f;
                SetVisible(true);
                return;
            }

            _depletedAt = record.at;
            if (_depleted) return;

            _depleted = true;
            SetVisible(false);
            if (RespawnSeconds > 0f && isActiveAndEnabled) StartCoroutine(Respawn());
        }
    }
}
