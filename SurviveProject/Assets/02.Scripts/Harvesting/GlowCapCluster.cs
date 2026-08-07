using UnityEngine;
using Survive.Interaction;
using Survive.Items;
using Survive.Localization;
using Survive.Player;
using Survive.World;

namespace Survive.Harvesting
{
    /// <summary>
    /// 발광 버섯 군락에서 갓을 따는 자리. 홀드형 채집 노드다.
    ///
    /// <b>왜 <see cref="HarvestNode"/>를 그대로 쓰지 않는가.</b> 두 가지가 맞지 않는다.
    /// <list type="number">
    /// <item><b>재생의 주인이 다르다.</b> HarvestNode는 스스로 코루틴을 돌려 되살아난다.
    ///   여기서는 재생 시각이 곧 <b>군락의 밝기가 돌아오는 시각</b>이라
    ///   그 판정이 <see cref="GlowGroveRule"/>(테스트되는 순수 함수)을 지나야 한다.</item>
    /// <item><b>딴 결과가 세계를 바꾼다.</b> 잔해·광맥은 사라지면 그만이지만
    ///   갓은 사라지는 순간 군락이 어두워지고 포식자에게 길이 열린다.
    ///   HarvestNode에 그 통보를 심으면 씬에 이미 놓인 노드 스물몇 개와
    ///   그것들을 쓰는 E2E 세 편의 감촉까지 함께 흔들린다.</item>
    /// </list>
    /// 그래서 관례만 따른다 — <see cref="IHoldInteractable"/>, 같은 문구 형식,
    /// 같은 "다 캐면 시각적으로 사라진다"는 약속.
    ///
    /// <b>갓 하나가 아니라 무더기다.</b> 씬의 발광 버섯은 갓이 따로 떨어진 메시가
    /// 아니라 군락 하나가 통짜 메시 세 덩이로 놓여 있다(조사 결과). 갓 하나하나를
    /// 만들려면 메시를 새로 잘라야 하고 그것은 사람 눈이 필요한 일이다. 대신
    /// <b>덩이 하나 = 갓 한 무더기</b>로 세어 "남은 갓 n / 전체 m"을 성립시킨다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GlowCapCluster : MonoBehaviour, IHoldInteractable, IWorldStateOwner
    {
        /// <summary>따서 얻는 것. 08.Data/Items의 "버섯 갓"(발광 버섯의 갓)이다.</summary>
        public const string CapItemId = "mushroom_cap";

        /// <summary>
        /// 화면에 보일 이름. 스펙 §3 섬2의 자원 이름을 따른다.
        ///
        /// <b><c>const</c>가 아니다.</b> 상수로 두면 부르는 쪽에 글자가 박혀
        /// 로케일을 바꿔도 이 이름만 옛 언어로 남는다. 08.Data에 에셋이 없어
        /// 이름표는 손으로 지었다 (<c>World/glow_cap_name</c>).
        /// </summary>
        public static string DisplayName => Loc.T("World", "glow_cap_name");

        /// <summary>
        /// 맨손 채집 시간. 기존 발광 버섯 노드(08.Data/HarvestNodes/Mushroom)의
        /// baseDuration과 같은 값을 쓴다 — 같은 것을 따는데 손맛이 달라질 이유가 없다.
        /// </summary>
        public const float HoldSeconds = 0.65f;

        /// <summary>한 무더기에서 나오는 갓 수. LootTables/Mushroom의 1~3과 같다.</summary>
        public const int MinYield = 1;
        public const int MaxYield = 3;

        GlowGrove _grove;
        Renderer[] _renderers;
        Collider[] _colliders;

        bool _harvested;
        float _harvestedAt;

        /// <summary>신원을 지은 자리. 난수도 같은 자리로 파생한다.</summary>
        Vector3 _site;

        /// <summary>
        /// 이 무더기에서 몇 번째 채집인가. 다시 자란 갓이 영영 같은 개수를
        /// 내놓지 않게 굴림마다 하나씩 는다. 저장하지 않는 이유는
        /// <c>HarvestNode._rolls</c>에 적어 두었다.
        /// </summary>
        int _rolls;
        float _regrowSeconds = GlowGroveRule.RegrowSeconds;

        /// <summary>지금 따여 있는가. 군락이 남은 갓을 셀 때 본다.</summary>
        public bool IsHarvested => _harvested;

        /// <summary>다시 자라기까지 걸리는 시간. 기본값은 규칙의 180초다.</summary>
        public float RegrowSeconds
        {
            get => _regrowSeconds;
            set => _regrowSeconds = Mathf.Max(0f, value);
        }

        /// <summary>
        /// 돌아오기까지 남은 초. 따여 있지 않으면 0이다.
        /// <b>세계 시계로 잰다</b> — 이유는 <see cref="WorldClock"/>에 적어 두었다.
        /// </summary>
        public float RegrowRemaining => _harvested
            ? GlowGroveRule.RegrowRemaining(_harvestedAt, WorldClock.Seconds, _regrowSeconds)
            : 0f;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);

            // 신원은 깨어날 때의 자리로 짓고 그 뒤로 바꾸지 않는다.
            _site = transform.position;
            _worldId = Survive.World.WorldId.At(WorldLedgerScope.GlowCap, _site);

            // 비활성화가 아니라 철거에서 뺀다 — 딴 갓은 오브젝트를 끄지 않지만,
            // 관례를 갈래마다 다르게 두면 어느 날 하나가 조용히 저장에서 빠진다.
            WorldLedgerRegistry.Register(this);
        }

        void OnDestroy() => WorldLedgerRegistry.Unregister(this);

        /// <summary>어느 군락의 갓인지 알려 준다. 설치 서비스가 부른다.</summary>
        public void Bind(GlowGrove grove)
        {
            _grove = grove;
            grove?.Attach(this);
        }

        void Update()
        {
            if (!_harvested) return;
            if (!GlowGroveRule.HasRegrown(_harvestedAt, WorldClock.Seconds, _regrowSeconds)) return;
            Regrow();
        }

        // ── 홀드 채집 ────────────────────────────────────────────

        /// <summary>남은 초를 소수점 없이. 프롬프트 인자로 넘기기 전에 여기서 서식한다.</summary>
        const string SecondsFormat = "F0";

        public float HoldDuration => HoldSeconds;

        public string InteractionPrompt
        {
            get
            {
                if (!_harvested) return Loc.F("Prompt", "harvest_hold", DisplayName);

                // 따고 나면 콜라이더를 껐으므로 보통은 조준되지 않는다.
                // 그래도 문구를 비워 두지 않는다 — 겹친 콜라이더로 잡히는 경우가 있고,
                // 그때 아무 말도 없으면 "고장났다"로 읽힌다.
                //
                // 서식 지정자는 지역 변수로 뺀다. 인자 자리에 "F0"을 그대로 두면
                // 문장 게이트가 그것을 "말을 인자로 넘겼다"로 읽는다 —
                // 서식인지 말인지는 정적 분석으로 가릴 수 없다.
                string 남은초 = RegrowRemaining.ToString(SecondsFormat);
                return Loc.F("Prompt", "harvest_regrowing", DisplayName, 남은초);
            }
        }

        public bool CanInteract(PlayerContext player)
            => !_harvested && player != null && player.Inventory?.Inventory != null;

        public void OnHoldProgress(float normalized) { }
        public void OnHoldCancelled() { }

        public void Interact(PlayerContext player)
        {
            if (!CanInteract(player)) return;

            var item = Resolve(player);
            if (item == null)
            {
                Debug.LogWarning($"[GlowCapCluster] '{CapItemId}' 아이템 정의를 찾지 못해 채집을 취소했습니다.", this);
                return;
            }

            // 난수의 주인은 세계 시드다 (WorldSeed). UnityEngine.Random은 주인이 없어
            // 같은 세계를 두 번 돌려도 같은 개수가 나오지 않았다.
            int yield = WorldSeed.Rng(WorldSeedBranch.GlowCapYield, _site, _rolls)
                                 .Next(MinYield, MaxYield + 1);
            _rolls++;
            int leftover = player.Inventory.Inventory.TryAdd(item, yield);
            if (leftover > 0)
                Debug.LogWarning($"[GlowCapCluster] 인벤토리가 가득 차 {DisplayName} {leftover}개를 넣지 못했습니다.", this);

            Harvest();
        }

        /// <summary>
        /// 아이템 정의는 플레이어가 들고 있는 데이터베이스에서 꺼낸다.
        ///
        /// 설치 서비스가 미리 물려 주지 않는 이유: 그러면 서비스가 플레이어보다
        /// 먼저 서는 순서 문제를 떠안는다. 딸 때는 반드시 플레이어가 눈앞에 있으므로
        /// 그 순간에 묻는 것이 언제나 안전하다.
        /// </summary>
        static ItemDataSO Resolve(PlayerContext player)
        {
            var db = player.Inventory.Database;
            if (db == null) return null;
            return db.GetById(CapItemId);
        }

        void Harvest()
        {
            _harvested = true;
            _harvestedAt = WorldClock.Seconds;
            SetVisible(false);
            _grove?.Refresh();
        }

        // ── 세계 원장 ────────────────────────────────────────────
        //
        // 갓은 <b>여럿</b>이라 고정 문자열을 열쇠로 삼을 수 없다. 자리로 짓는다.
        // 딴 갓 하나가 군락의 밝기를 낮추므로, 저장을 건너 이어지지 않으면
        // 불러온 세계에서 어두워졌던 자리가 도로 환해진다.

        string _worldId;

        public string WorldId => _worldId;

        public WorldRecord CaptureWorld() => !_harvested
            ? null
            : new WorldRecord
            {
                kind = WorldLedgerScope.GlowCap,
                gone = true,
                at = _harvestedAt,
            };

        public void RestoreWorld(WorldRecord record)
        {
            bool 따여있다 = record != null && record.gone;

            if (!따여있다)
            {
                if (_harvested) Regrow();
                return;
            }

            _harvestedAt = record.at;
            if (_harvested) return;

            _harvested = true;
            SetVisible(false);
            _grove?.Refresh();
        }

        void Regrow()
        {
            _harvested = false;
            SetVisible(true);
            _grove?.Refresh();
        }

        /// <summary>
        /// 게임오브젝트를 통째로 끄지 않는다. 꺼 버리면 <see cref="Update"/>가 멈춰
        /// 다시 자랄 시각을 아무도 보지 않게 된다. 보이는 것과 만져지는 것만 끈다.
        /// </summary>
        void SetVisible(bool on)
        {
            if (_renderers != null)
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null) _renderers[i].enabled = on;

            if (_colliders != null)
                for (int i = 0; i < _colliders.Length; i++)
                    if (_colliders[i] != null) _colliders[i].enabled = on;
        }
    }
}
