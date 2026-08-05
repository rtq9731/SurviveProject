using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Survive.Core;
using Survive.Items;
using Survive.Narrative;

namespace Survive.Progression
{
    /// <summary>
    /// 해금 원장의 주인이자 채널 1(현장 발견)의 귀.
    ///
    /// 씬에 두지 않고 스스로 선다. 무엇을 알고 있는가는 화면의 것도 특정
    /// 프리팹의 것도 아니고 판 전체의 상태라, 씬을 갈아 끼워도 살아 있어야 한다.
    /// HandCraftingService·DeathDropService와 같은 방식이다 — 씬 파일을 건드리지
    /// 않는다는 실질적 이점도 같다.
    ///
    /// 하는 일은 셋뿐이다.
    /// 1) 원장을 들고 GameServices에 내놓는다 (규칙 함수들이 여기서 집어 간다)
    /// 2) 플레이어 소지품의 첫 습득을 듣고 청사진을 연다
    /// 3) 그때 우주복 AI가 한 줄 말하게 한다 (기존 자막판을 그대로 쓴다)
    /// </summary>
    [DisallowMultipleComponent]
    public class UnlockService : MonoBehaviour, ISaveable
    {
        /// <summary>Resources 아래에서 매핑표를 찾는 이름.</summary>
        public const string BookResourceName = "DiscoveryBook";

        static UnlockService _instance;
        public static UnlockService Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("UnlockService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UnlockService>();
        }

        /// <summary>지금까지 알아낸 것 전부.</summary>
        public UnlockLedger Ledger { get; } = new UnlockLedger();

        /// <summary>아이템 id → 열리는 청사진·대사.</summary>
        public DiscoveryBookSO Book => _book;

        /// <summary>지금까지 AI가 뱉은 줄 수. 검증 하네스가 본다.</summary>
        public int LinesSpoken { get; private set; }

        /// <summary>마지막으로 뱉은 줄. 검증 하네스가 본다.</summary>
        public string LastLine { get; private set; }

        /// <summary>아직 뱉을 줄이 남았는가. 검증 하네스가 조용해지기를 기다릴 때 본다.</summary>
        public bool IsSpeaking => _narrating != null;

        DiscoveryBookSO _book;
        PlayerInventory _player;
        Inventory _bound;

        readonly Queue<SequenceSO.Line> _pending = new Queue<SequenceSO.Line>();
        Coroutine _narrating;
        SubtitleView _subtitle;

        void Awake()
        {
            _book = Resources.Load<DiscoveryBookSO>(BookResourceName);
            if (_book == null)
                Debug.LogWarning($"[UnlockService] Resources/{BookResourceName}을(를) 찾지 못했습니다. " +
                                 "현장 발견이 일어나지 않습니다.");
        }

        void OnEnable() => Publish();

        void OnDisable()
        {
            Unbind();
            GameServices.Unregister<UnlockLedger>();
            GameServices.Unregister<UnlockService>();
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            // GameBootstrap은 씬마다 GameServices.Clear()를 부른다. DontDestroyOnLoad로
            // 살아남은 이 서비스의 OnEnable은 다시 돌지 않으므로, 등록이 지워졌으면
            // 스스로 되돌린다. 원장을 못 찾으면 모든 청사진이 열린 것처럼 굴게 되는데,
            // 조용히 그렇게 되는 편이 제일 나쁘다.
            if (!GameServices.TryGet<UnlockLedger>(out var registered) ||
                !ReferenceEquals(registered, Ledger))
                Publish();

            RebindInventory();
        }

        void Publish()
        {
            GameServices.Register(Ledger);
            GameServices.Register(this);
        }

        // ── 채널 1: 첫 습득 ──────────────────────────────────────

        /// <summary>
        /// 소지품 객체는 저장 복원 때 통째로 갈린다(<see cref="PlayerInventory.RestoreState"/>).
        /// 구독을 한 번 걸어 두고 잊으면 불러오기 이후로 아무 발견도 일어나지 않는다.
        /// 참조가 바뀌었는지만 보고 갈아 끼운다.
        /// </summary>
        void RebindInventory()
        {
            if (_player == null) GameServices.TryGet<PlayerInventory>(out _player);

            var inv = _player != null ? _player.Inventory : null;
            if (ReferenceEquals(inv, _bound)) return;

            Unbind();
            _bound = inv;
            if (_bound != null) _bound.ItemAdded += OnItemAdded;
        }

        void Unbind()
        {
            if (_bound == null) return;
            _bound.ItemAdded -= OnItemAdded;
            _bound = null;
        }

        void OnItemAdded(ItemDataSO item, int count)
        {
            if (item == null) return;
            if (!FieldDiscovery.TryDiscover(_book, Ledger, item.id, out var discovery)) return;

            Announce(discovery.line);
        }

        // ── AI 대사 ──────────────────────────────────────────────

        /// <summary>
        /// 자막을 한 줄씩 줄 세워 내보낸다.
        ///
        /// <see cref="SequenceDirector"/>를 쓰지 않는 이유: 그쪽은 대기열도
        /// 재진입 방지도 없어서, 상자 하나를 열어 새 재료 셋이 한꺼번에 들어오면
        /// 세 대사가 서로를 덮어쓴다. 자막을 그리는 부품(<see cref="SubtitleView"/>)은
        /// 그대로 쓰고 순서만 여기서 잡는다.
        ///
        /// 바깥에 열어 두는 이유: 알아낸 것을 말하는 자리가 첫 습득 하나뿐이 아니게
        /// 됐다. 연구대(<see cref="ResearchStation"/>)가 항목 하나를 다 보고 나서
        /// 같은 목소리로 말한다 — 화자가 같으면 줄을 세우는 자리도 같아야 한다.
        /// </summary>
        public void Announce(SequenceSO.Line line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.text)) return;

            _pending.Enqueue(line);
            if (_narrating == null) _narrating = StartCoroutine(Narrate());
        }

        IEnumerator Narrate()
        {
            while (_pending.Count > 0)
            {
                // 프롤로그 같은 짜인 시퀀스가 돌고 있으면 끼어들지 않는다.
                while (GameServices.TryGet<SequenceDirector>(out var director) &&
                       director != null && director.IsPlaying)
                    yield return null;

                var line = _pending.Dequeue();
                var view = EnsureSubtitle();

                LastLine = line.text;
                LinesSpoken++;

                if (view != null) view.Show(line.speaker, line.text);
                yield return new WaitForSeconds(Mathf.Max(0.5f, line.holdSeconds));

                // 다음 줄이 이어지면 판을 내렸다 올리지 않는다 — 깜빡임만 는다.
                if (view != null && _pending.Count == 0) view.HideView();
            }

            _narrating = null;
        }

        SubtitleView EnsureSubtitle()
        {
            if (_subtitle != null) return _subtitle;

            // 씬에 이미 자막판이 있으면 그것을 쓴다 (프롤로그 씬).
            foreach (var v in Object.FindObjectsByType<SubtitleView>(FindObjectsInactive.Include))
            {
                if (v == null) continue;
                _subtitle = v;
                return _subtitle;
            }

            var canvas = PickCanvas();
            if (canvas == null) return null;

            _subtitle = SubtitleView.Ensure(canvas.transform, FontOf(canvas));
            return _subtitle;
        }

        /// <summary>화면에 겹쳐 그리는 캔버스 중 가장 위에 있는 것.</summary>
        static Canvas PickCanvas()
        {
            Canvas best = null;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            {
                if (c == null || !c.isRootCanvas) continue;
                if (c.renderMode == RenderMode.WorldSpace) continue;
                if (best == null || c.sortingOrder > best.sortingOrder) best = c;
            }
            return best;
        }

        /// <summary>
        /// 한글 폰트를 따로 물고 있지 않으므로 이미 캔버스에 있는 글자에서 빌린다.
        /// TMP 기본 폰트에는 한글 글리프가 없어 전부 네모로 찍힌다.
        /// </summary>
        static TMP_FontAsset FontOf(Canvas canvas)
        {
            var text = canvas.GetComponentInChildren<TMP_Text>(true);
            return text != null ? text.font : null;
        }

        // ── 저장 ─────────────────────────────────────────────────

        public string SaveKey => "unlock_ledger";

        public object CaptureState() => Ledger.Capture();

        public void RestoreState(object state)
        {
            if (state is not UnlockLedgerState s) return;
            Ledger.Restore(s);
        }
    }
}
