using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using Survive.Localization;
using Survive.Narrative;
using Survive.Progression;

namespace Survive.Testing
{
    /// <summary>
    /// <b>현장 발견·연구 대사가 화면에서 로케일을 따라오는가.</b>
    ///
    /// <b>왜 화면을 읽는가.</b> <c>DataText.Line(DiscoverySO)</c>가 옳은 값을 내는지는
    /// EditMode(<c>UnlockLineLocTests</c>)가 이미 잰다. 여기서만 잴 수 있는 것은
    /// <b>그 값이 자막판까지 닿는가</b>다 — 2026-08-07 전까지 조회 함수는 멀쩡히
    /// 있었고 <see cref="UnlockService.Announce(DiscoverySO)"/>가 그것을 부르지
    /// 않았을 뿐이다(대사 줄을 날것으로 받아 큐에 세웠다). 조회를 검사하는 것으로는
    /// 그 구멍을 못 본다.
    ///
    /// <b>왜 프롤로그보다 이쪽이 급한가.</b> 프롤로그는 판당 한 번이고 이쪽은 재료를
    /// 처음 주울 때마다, 유물을 밝힐 때마다 나온다. 영어를 켜면 그 전부가 한국어로
    /// 남았다.
    ///
    /// <b>세 로케일을 각각 새로 띄운다.</b> 자막은 몇 초 뒤 사라지는 물건이라 이미 뜬
    /// 줄을 다시 그리지 않는다(<c>E2ESequenceLocale</c>의 계약과 같다).
    /// 큐에서 <b>기다리는</b> 줄이 로케일을 따라오는지는 EditMode 쪽에서 본다 —
    /// 거기서는 시간을 흘리지 않고도 잴 수 있다.
    /// </summary>
    public static class E2EUnlockLineLocale
    {
        /// <summary>en 칸이 차 있는 짝. 비어 있으면 로케일 전환을 화면에서 잴 수 없다.</summary>
        const string 발견id = "disc_scrap";
        const string 연구id = "res_surface_walker";

        static DiscoverySO _발견;
        static ResearchEntrySO _연구;
        static TMP_Text _자막글자;

        public static IEnumerator FullRun()
        {
            E2EHarness.ClearLog();
            E2EHarness.Log("=== 해금 대사 로케일 ===");

            string 처음로케일 = Loc.CurrentLocale;

            yield return 표와_에셋이_서_있다();
            yield return 실제_습득이_같은_문으로_들어온다();

            yield return 로케일마다_다시_띄운다(true, "현장 발견");
            yield return 로케일마다_다시_띄운다(false, "연구");

            Loc.SetLocale(처음로케일);
            yield return null;

            _발견 = null;
            _연구 = null;
            _자막글자 = null;

            E2EHarness.Log("=== 해금 대사 로케일 통과 ===");
        }

        // ── 1. 준비 ──────────────────────────────────────────────

        static IEnumerator 표와_에셋이_서_있다()
        {
            yield return E2EHarness.WaitUntil(() => Loc.IsLoaded, "번역 표가 실렸다", 8f);
            E2EHarness.Assert(Loc.Catalog.Problems.Count == 0,
                $"표에 건전성 문제가 없다 ({Loc.Catalog.Problems.Count}건)");

            var service = UnlockService.Instance;
            E2EHarness.Assert(service != null, "UnlockService가 서 있다");
            E2EHarness.Assert(service.Book != null, "발견 매핑표가 실렸다");

            _발견 = service.Book.discoveries.FirstOrDefault(d => d != null && d.id == 발견id);
            E2EHarness.Assert(_발견 != null, $"{발견id} 발견 정의를 찾았다");

            var 연구책 = Resources.Load<ResearchBookSO>(ResearchStation.BookResourceName);
            E2EHarness.Assert(연구책 != null, "연구 목록이 실렸다");
            _연구 = 연구책.entries.FirstOrDefault(e => e != null && e.id == 연구id);
            E2EHarness.Assert(_연구 != null, $"{연구id} 연구 항목을 찾았다");

            E2EHarness.Assert(Loc.Catalog.TryGet("en", DataText.LineKey(_발견), out _),
                $"{DataText.LineKey(_발견)}의 en 칸이 차 있다 — 비어 있으면 이 검사는 공회전한다");
            E2EHarness.Assert(Loc.Catalog.TryGet("en", DataText.LineKey(_연구), out _),
                $"{DataText.LineKey(_연구)}의 en 칸이 차 있다 — 비어 있으면 이 검사는 공회전한다");
        }

        // ── 2. 진짜 계기로도 같은 문을 지난다 ────────────────────

        /// <summary>
        /// 직접 <c>Announce</c>를 부르는 것만으로는 <b>프로덕션 호출자가 그 문을 쓰는가</b>를
        /// 못 본다. 재료를 실제로 손에 넣어(<c>Inventory.TryAdd</c>) 한 번 지나가 본다.
        ///
        /// 발견은 판당 한 번뿐이라, 앞 시나리오가 이미 스크랩을 쥐었으면 이 자리는
        /// 울리지 않는다. 그때는 실패가 아니라 <b>건너뛴 것</b>으로 적는다 — 없는 신호를
        /// 실패로 읽으면 다음 사람이 이 시나리오를 지운다.
        /// </summary>
        static IEnumerator 실제_습득이_같은_문으로_들어온다()
        {
            var service = UnlockService.Instance;
            var inv = E2EHarness.Player.Inventory.Inventory;
            var item = E2EHarness.Player.Inventory.Database.GetById("scrap");
            E2EHarness.Assert(item != null, "스크랩 정의가 있다");

            if (service.Ledger.IsUnlocked(발견id))
            {
                E2EHarness.Log("  [건너뜀] 이 판에서 스크랩을 이미 쥐었다 — 첫 습득을 다시 만들 수 없다");
                yield break;
            }

            Loc.SetLocale(StringCatalog.DefaultLocale);
            yield return null;

            int 말한줄수 = service.LinesSpoken;
            inv.TryAdd(item, 1);

            yield return E2EHarness.WaitUntil(() => service.LinesSpoken > 말한줄수,
                                              "스크랩을 처음 쥐자 우주복 AI가 말했다", 6f);

            string 표의답 = DataText.Line(_발견);
            E2EHarness.Log($"  AI: {service.LastLine}");
            E2EHarness.Assert(service.LastLine == 표의답,
                $"뱉은 줄이 표의 답과 글자 그대로 같다 (기대: \"{표의답}\")");

            yield return E2EHarness.WaitUntil(() => !service.IsSpeaking, "자막이 조용해졌다", 20f);
        }

        // ── 3. 로케일마다 다시 띄워 화면을 읽는다 ────────────────

        /// <summary>
        /// 지금 로케일에서 자막판에 떠야 할 한 줄. 화자와 대사를 잇는 꼴까지
        /// 표에서 나온다(<c>UI/subtitle_line</c>) — 이름만 견주면 껍데기가 코드에
        /// 박혀 있어도 초록불이 뜬다.
        /// </summary>
        static string 기대_자막(bool 발견인가) => 발견인가
            ? Loc.F("UI", "subtitle_line", DataText.Speaker(_발견), DataText.Line(_발견))
            : Loc.F("UI", "subtitle_line", DataText.Speaker(_연구), DataText.Line(_연구));

        static void 띄운다(bool 발견인가)
        {
            if (발견인가) UnlockService.Instance.Announce(_발견);
            else UnlockService.Instance.Announce(_연구);
        }

        static IEnumerator 로케일마다_다시_띄운다(bool 발견인가, string 갈래)
        {
            E2EHarness.Log($"— {갈래} —");

            string ko = null, en = null, 의사 = null;

            yield return 한_로케일에서_읽는다(발견인가, StringCatalog.DefaultLocale, s => ko = s);
            yield return 한_로케일에서_읽는다(발견인가, "en", s => en = s);
            yield return 한_로케일에서_읽는다(발견인가, StringCatalog.PseudoLocale, s => 의사 = s);

            E2EHarness.Assert(en != ko,
                $"{갈래} 대사가 en에서 바뀌었다 — 안 바뀌면 그 자리는 표를 우회해 " +
                $"에셋을 직접 띄우고 있다 (\"{ko}\")");
            E2EHarness.Assert(!LocSentenceRules.HasHangul(en),
                $"en {갈래} 자막에 한글이 한 글자도 없다 — \"{en}\"");
            E2EHarness.Assert(PseudoLocalizer.IsTransformed(의사),
                $"{갈래} 대사가 pseudo에서 부풀었다 — \"{의사}\"");
        }

        /// <summary>
        /// 로케일을 세우고 대사를 하나 띄운 뒤 <b>자막판에 실제로 뜬 글자</b>를 읽는다.
        /// 다음 로케일로 넘어가기 전에 자막이 사라지기를 기다린다 — 이어 띄우면
        /// 앞 줄이 판을 붙들고 있어 새 줄이 언제 올라왔는지 알 수 없다.
        /// </summary>
        static IEnumerator 한_로케일에서_읽는다(bool 발견인가, string 로케일,
                                                System.Action<string> 담는다)
        {
            var service = UnlockService.Instance;
            yield return E2EHarness.WaitUntil(() => !service.IsSpeaking, "앞 자막이 끝났다", 20f);

            Loc.SetLocale(로케일);
            yield return null;

            띄운다(발견인가);
            yield return E2EHarness.WaitUntil(() => service.IsSpeaking, $"{로케일} 자막이 시작된다", 8f);

            yield return E2EHarness.WaitUntil(() => 자막글자() != null, "자막판이 화면에 섰다", 8f);
            _자막글자 = 자막글자();

            string 바라는것 = 기대_자막(발견인가);
            yield return E2EHarness.WaitUntil(() => _자막글자.text == 바라는것,
                $"[{로케일}] 화면과 표의 답이 글자 그대로 같다 (기대: \"{바라는것}\")", 8f);

            E2EHarness.Log($"  [{로케일}] {_자막글자.text}");
            담는다(_자막글자.text);
        }

        /// <summary>
        /// 자막판의 글자칸. <see cref="UnlockService"/>가 씬에 없으면 스스로 세우므로
        /// 첫 대사 뒤에야 존재한다.
        /// </summary>
        static TMP_Text 자막글자()
        {
            var view = Object.FindObjectsByType<SubtitleView>(FindObjectsInactive.Include)
                             .FirstOrDefault(v => v != null);
            return view == null ? null : view.GetComponentInChildren<TMP_Text>(true);
        }
    }
}
