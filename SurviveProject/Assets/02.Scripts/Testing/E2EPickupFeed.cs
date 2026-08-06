using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Survive.Interaction;
using Survive.Items;
using Survive.Localization;
using Survive.UI;

namespace Survive.Testing
{
    /// <summary>
    /// 무언가를 주우면 화면 왼쪽 아래에 <b>무엇을 얻었는지</b>가 뜬다.
    ///
    /// EditMode(<c>PickupFeedTests</c>)는 규칙만 본다 — 합치기·만료·최대 줄 수.
    /// 규칙이 옳아도 화면이 그 규칙을 안 부르면 아무 일도 안 일어나고, 그 구멍은
    /// 실제로 주워 보기 전까지 아무 신호도 내지 않는다. 그래서 여기서는
    /// <b>진짜로 줍는다</b> — 바닥에 놓고, 겨누고, E를 누른다.
    ///
    /// 화면에 선 글자를 <b>직접</b> 읽고 표가 내놓아야 할 문장과 글자 그대로
    /// 견준다. 이름만 들어 있는지 보는 것으로는 껍데기(<c>{0} 획득</c>)가 코드에
    /// 박혀 있어도 초록불이 뜬다.
    /// </summary>
    public static class E2EPickupFeed
    {
        const string ItemId = "scrap";

        static readonly LocKey OneKey = new LocKey("Feed", "pickup_one");
        static readonly LocKey ManyKey = new LocKey("Feed", "pickup_many");

        static ItemDataSO _item;
        static GameObject _drop;

        public static IEnumerator FullRun()
        {
            string 처음로케일 = Loc.CurrentLocale;

            yield return 표와_화면이_서_있다();
            yield return 하나_주우면_줄이_뜬다();
            yield return 같은_것을_또_주우면_합쳐진다();
            yield return 로케일을_바꾸면_그_자리에서_바뀐다();
            yield return 시간이_지나면_사라진다();
            yield return 되돌린다(처음로케일);

            E2EHarness.Log("=== 획득 알림 완주 ===");
        }

        static PickupFeedView View => PickupFeedView.Instance;

        /// <summary>지금 로케일에서 표가 내놓아야 할 줄. 화면과 글자 그대로 견준다.</summary>
        static string 기대_줄(int count) => count > 1
            ? Loc.F(ManyKey.Category, ManyKey.Key, DataText.Name(_item), count)
            : Loc.F(OneKey.Category, OneKey.Key, DataText.Name(_item));

        // ── 1. 준비 ──────────────────────────────────────────────

        static IEnumerator 표와_화면이_서_있다()
        {
            yield return E2EHarness.WaitUntil(() => Loc.IsLoaded, "번역 표가 실렸다", 8f);
            E2EHarness.Assert(Loc.Catalog.Problems.Count == 0,
                $"표에 건전성 문제가 없다 ({Loc.Catalog.Problems.Count}건)");

            foreach (var key in new[] { OneKey, ManyKey })
            {
                E2EHarness.Assert(Loc.Catalog.Contains(key),
                    $"표에 {key}가 있다 — 없으면 문장이 코드에 박혀 있다는 뜻이다");
                E2EHarness.Assert(Loc.Catalog.TryGet("en", key, out var 영문) &&
                                  !string.IsNullOrEmpty(영문),
                    $"{key}의 en 칸이 차 있다 — 비어 있으면 로케일 검사가 공회전한다");
            }

            _item = Resources.FindObjectsOfTypeAll<ItemDataSO>().FirstOrDefault(i => i.id == ItemId);
            E2EHarness.Assert(_item != null, $"{ItemId} 아이템 정의를 찾았다");

            yield return E2EHarness.WaitUntil(() => View != null,
                "획득 알림 화면이 스스로 서 있다 (RuntimeInitializeOnLoadMethod)", 5f);

            // 앞선 시나리오가 남긴 줄이 섞이면 수를 셀 수 없다.
            View.Feed.Clear();
            yield return null;
        }

        // ── 2. 진짜로 줍는다 ─────────────────────────────────────

        static IEnumerator 하나_줍는다()
        {
            var cam = E2EHarness.Eye;

            // 코앞(1.2m)에 놓는다. 시작 지점 둘레에 주울 수 있는 것들이 서 있고
            // PlayerInteractor는 가장 가까운 것을 고른다 — 이 검사는 조준 실력이
            // 아니라 글자를 보는 것이므로 아무것도 끼어들 수 없는 거리에 둔다.
            _drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _drop.name = "E2E_FeedDrop";
            _drop.transform.localScale = Vector3.one * 0.4f;
            _drop.transform.position = cam.transform.position + cam.transform.forward * 1.2f;
            _drop.AddComponent<ItemPickup>().Setup(_item, 1);

            E2EHarness.SyncPhysics();
            yield return null;
            E2EHarness.LookAt(_drop.transform.position);
            yield return null;

            var interactor = E2EHarness.Player.Interactor;
            yield return E2EHarness.WaitUntil(() => interactor.Current is ItemPickup,
                "줍기 대상이 탐지된다 (지금 잡힌 것: " +
                (interactor.Current?.GetType().Name ?? "없음") + ")", 5f);

            yield return E2EHarness.TapKey(Key.E);
            yield return null;
            yield return null;
        }

        /// <summary>지금 화면에 실제로 서 있는 알림 줄의 글자.</summary>
        static string[] 화면의_줄들() =>
            View.VisibleLabels
                .Where(t => t != null && t.gameObject.activeInHierarchy)
                .Select(t => t.text)
                .ToArray();

        static IEnumerator 하나_주우면_줄이_뜬다()
        {
            int 전 = View.Feed.Count;
            yield return 하나_줍는다();

            yield return E2EHarness.WaitUntil(() => View.Feed.Count > 전,
                "주웠더니 알림 줄이 하나 생겼다", 3f);
            yield return null;

            var 줄들 = 화면의_줄들();
            E2EHarness.Log($"  화면: [{string.Join(" | ", 줄들)}]");
            E2EHarness.AssertEqual(줄들.Length, 1, "화면에 선 줄 수");
            E2EHarness.Assert(줄들[0] == 기대_줄(1),
                $"문장 전체가 표에서 나왔다 (기대: \"{기대_줄(1)}\")");
        }

        // ── 3. 같은 것을 또 주우면 합쳐진다 ──────────────────────

        static IEnumerator 같은_것을_또_주우면_합쳐진다()
        {
            E2EHarness.Log("— 같은 것을 두 번 더 줍는다 —");

            yield return 하나_줍는다();
            yield return 하나_줍는다();

            yield return E2EHarness.WaitUntil(() => View.Feed.Count == 1 &&
                                                    View.Feed.Rows[0].Count == 3,
                "세 번 주웠는데 줄은 하나이고 수가 3이다", 3f);
            yield return null;

            var 줄들 = 화면의_줄들();
            E2EHarness.Log($"  화면: [{string.Join(" | ", 줄들)}]");
            E2EHarness.AssertEqual(줄들.Length, 1,
                "합쳐졌으므로 화면의 줄도 하나여야 한다 (세 줄이면 화면이 가려진다)");
            E2EHarness.Assert(줄들[0] == 기대_줄(3),
                $"여럿일 때는 다른 문장을 쓴다 (기대: \"{기대_줄(3)}\")");
        }

        // ── 4. 뜬 채로 로케일을 바꾼다 ───────────────────────────

        static IEnumerator 로케일을_바꾸면_그_자리에서_바뀐다()
        {
            E2EHarness.Log("— 줄이 떠 있는 채로 en으로 —");

            var label = View.VisibleLabels.FirstOrDefault();
            E2EHarness.Assert(label != null, "바꾸기 전에 줄이 화면에 서 있다");

            string 전 = label.text;

            Loc.SetLocale("en");
            yield return null;
            yield return null;

            E2EHarness.Log($"  \"{전}\" -> \"{label.text}\"");
            E2EHarness.Assert(label.gameObject.activeInHierarchy,
                "같은 글자 오브젝트가 그대로 서 있다 (다시 그린 것이 아니다)");
            E2EHarness.Assert(label.text != 전,
                "아무것도 다시 열지 않았는데 글자가 바뀌었다");
            E2EHarness.Assert(label.text == 기대_줄(3),
                $"en에서도 문장 전체가 표에서 나왔다 (기대: \"{기대_줄(3)}\")");
            E2EHarness.Assert(!LocSentenceRules.HasHangul(label.text),
                "en 줄에 한글이 한 글자도 없다 — 껍데기가 코드에 박혀 있으면 남는다");

            Loc.SetLocale("ko");
            yield return null;
            yield return null;

            E2EHarness.Assert(label.text == 기대_줄(3), "되돌리면 한국어 문장으로 돌아온다");
        }

        // ── 5. 스스로 사라진다 ──────────────────────────────────

        static IEnumerator 시간이_지나면_사라진다()
        {
            float 수명 = View.Feed.LifetimeSeconds;
            E2EHarness.Log($"— {수명}초 뒤 스스로 사라지는지 본다 —");

            yield return E2EHarness.WaitUntil(() => View.Feed.Count == 0,
                "수명이 지나 목록이 비었다", 수명 + 3f);

            // 옅어지는 트윈이 끝날 시간을 준다. 규칙에서 빠진 것과 화면에서 지워진
            // 것은 다른 순간이고, 사람 눈에 보이는 것은 뒤쪽이다.
            float t = 0f;
            while (t < 1.5f && 화면의_줄들().Length > 0) { t += Time.unscaledDeltaTime; yield return null; }

            E2EHarness.AssertEqual(화면의_줄들().Length, 0, "화면에서도 줄이 걷혔다");
        }

        static IEnumerator 되돌린다(string 처음로케일)
        {
            Loc.SetLocale(처음로케일);
            View?.Feed.Clear();
            if (_drop != null) Object.Destroy(_drop);
            _drop = null;
            yield return null;
        }
    }
}
