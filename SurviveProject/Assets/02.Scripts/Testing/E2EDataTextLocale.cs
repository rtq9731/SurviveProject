using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using Survive.Interaction;
using Survive.Items;
using Survive.Localization;

namespace Survive.Testing
{
    /// <summary>
    /// 데이터 에셋의 이름이 <b>화면에서</b> 번역 표를 따라오는가.
    ///
    /// EditMode 게이트(<c>DataTextGateTests</c>)는 조회 함수의 답을 본다. 그것만으로는
    /// 부족하다 — 함수가 옳은 값을 내도 화면이 그 함수를 안 부르면 아무 일도 안 일어나고,
    /// 그 구멍은 그 화면을 그 로케일로 열어 보기 전까지 신호를 내지 않는다.
    ///
    /// <b>왜 줍기 프롬프트인가.</b> 로케일을 바꾼 <b>그 프레임에</b> 화면이 따라오는지를
    /// 봐야 하는데, 창을 닫았다 열면 언제나 통과한다. 줍기 프롬프트는
    /// <c>PlayerInteractor</c>가 매 프레임 다시 묻고 달라졌을 때만 HUD로 밀어 넣는
    /// 자리라, 아무것도 다시 열지 않고 글자가 바뀌는지를 볼 수 있다.
    ///
    /// 표에 <c>Item/scrap.name = Scrap</c>이 en 칸에 들어 있다(폴백 검증용 두 칸 중 하나).
    ///
    /// <b>이름만이 아니라 껍데기도 본다.</b> 프롬프트는 이름 하나가 아니라
    /// <c>[E] {0} 줍기</c>라는 <b>한 문장</b>이고, 그 문장 전체가 표의 한 칸
    /// (<c>Prompt/pickup</c>)에서 나온다. 이름만 검사하면 껍데기가 코드에 박혀 있어도
    /// 초록불이 뜬다 — 실제로 이 라운드 전까지 그랬다.
    /// </summary>
    public static class E2EDataTextLocale
    {
        const string ItemId = "scrap";

        /// <summary>줍기 프롬프트의 껍데기. 이 한 칸이 문장 전체를 들고 있다.</summary>
        static readonly LocKey ShellKey = new LocKey("Prompt", "pickup");

        static ItemDataSO _item;
        static GameObject _drop;
        static TMP_Text _promptLabel;

        public static IEnumerator FullRun()
        {
            string 처음로케일 = Loc.CurrentLocale;

            yield return 표와_에셋이_서_있다();
            yield return 바닥에_놓고_겨눈다();
            yield return 화면의_이름이_표에서_나온다();
            yield return 로케일을_바꾸면_그_자리에서_바뀐다();
            yield return 표를_치우면_에셋_원문으로_돌아간다();
            yield return 되돌린다(처음로케일);

            E2EHarness.Log("=== 데이터 에셋 번역 완주 ===");
        }

        /// <summary>
        /// 지금 로케일에서 표가 내놓아야 할 프롬프트. 화면과 이것을 <b>글자 그대로</b>
        /// 견준다 — 이름만 들어 있는지 보는 것으로는 껍데기가 코드에 박힌 것을 못 잡는다.
        /// </summary>
        static string 기대_프롬프트() => Loc.F(ShellKey.Category, ShellKey.Key, DataText.Name(_item));

        // ── 1. 준비 ──────────────────────────────────────────────

        static IEnumerator 표와_에셋이_서_있다()
        {
            yield return E2EHarness.WaitUntil(() => Loc.IsLoaded, "번역 표가 실렸다", 8f);
            E2EHarness.Assert(Loc.Catalog.Problems.Count == 0,
                $"표에 건전성 문제가 없다 ({Loc.Catalog.Problems.Count}건)");

            _item = Resources.FindObjectsOfTypeAll<ItemDataSO>().FirstOrDefault(i => i.id == ItemId);
            E2EHarness.Assert(_item != null, $"{ItemId} 아이템 정의를 찾았다");

            var key = DataText.NameKey(_item);
            E2EHarness.Assert(Loc.Catalog.Contains(key), $"표에 {key}가 있다");
            E2EHarness.Assert(Loc.Catalog.TryGet("en", key, out _),
                $"{key}의 en 칸이 차 있다 — 비어 있으면 이 검사는 공회전한다");

            E2EHarness.Assert(Loc.Catalog.Contains(ShellKey),
                $"표에 프롬프트 껍데기 {ShellKey}가 있다 — 없으면 문장이 아직 코드에 박혀 있다");
            E2EHarness.Assert(Loc.Catalog.TryGet("en", ShellKey, out var 영문껍데기) &&
                              !string.IsNullOrEmpty(영문껍데기),
                $"{ShellKey}의 en 칸이 차 있다 — 비어 있으면 껍데기 검사가 공회전한다");
        }

        static IEnumerator 바닥에_놓고_겨눈다()
        {
            var cam = E2EHarness.Eye;

            // 코앞(1.2m)에 놓는 이유: 시작 지점 둘레에 포탈 장치와 버섯이 서 있고
            // PlayerInteractor는 <b>가장 가까운</b> 것을 고른다. 2.5m에 놓으면
            // 1.7m에 있는 포탈이 이긴다 — 이 검사는 조준 실력이 아니라 글자를 보는 것이므로
            // 아무것도 끼어들 수 없는 거리에 둔다.
            _drop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _drop.name = "E2E_LocaleDrop";
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
            yield return null;   // PlayerInteractor가 HUD로 프롬프트를 밀어 넣을 한 프레임
        }

        // ── 2. 화면에 선 글자를 직접 읽는다 ──────────────────────

        /// <summary>
        /// 지금 화면에 서 있는 상호작용 프롬프트. <c>HUDController</c>가 채우는 그 자리를
        /// 이름이 아니라 <b>내용</b>으로 찾는다 — 오브젝트 이름은 화면 조립 방식이
        /// 바뀌면 흔들리지만, 프롬프트가 화면에 있다는 사실은 흔들리지 않는다.
        /// </summary>
        static TMP_Text 프롬프트_글자()
        {
            string want = E2EHarness.Player.Interactor.Current?.InteractionPrompt;
            if (string.IsNullOrEmpty(want)) return null;

            return Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude)
                         .FirstOrDefault(t => t.gameObject.activeInHierarchy && t.text == want);
        }

        static IEnumerator 화면의_이름이_표에서_나온다()
        {
            yield return E2EHarness.WaitUntil(() => 프롬프트_글자() != null,
                                              "화면에 줍기 프롬프트가 서 있다", 5f);
            _promptLabel = 프롬프트_글자();

            string ko = Loc.Catalog.TableFor("ko")[DataText.NameKey(_item)];
            E2EHarness.Log($"  ko: \"{_promptLabel.text}\"");
            E2EHarness.Assert(_promptLabel.text.Contains(ko),
                              $"프롬프트에 표의 ko 값 \"{ko}\"가 들어 있다");

            E2EHarness.Assert(_promptLabel.text == 기대_프롬프트(),
                              $"문장 전체가 표에서 나왔다 (기대: \"{기대_프롬프트()}\")");
        }

        // ── 3. 창을 열어 둔 채로 로케일을 바꾼다 ─────────────────

        static IEnumerator 로케일을_바꾸면_그_자리에서_바뀐다()
        {
            E2EHarness.Log("— 겨눈 채로 en으로 —");

            string 전 = _promptLabel.text;
            Loc.Catalog.TryGet("en", DataText.NameKey(_item), out var english);
            Loc.Catalog.TryGet("ko", ShellKey, out var 한글껍데기);

            Loc.SetLocale("en");
            yield return null;
            yield return null;

            E2EHarness.Log($"  \"{전}\" -> \"{_promptLabel.text}\"");
            E2EHarness.Assert(_promptLabel.gameObject.activeInHierarchy, "같은 글자 오브젝트가 그대로 서 있다");
            E2EHarness.Assert(_promptLabel.text != 전, "아무것도 다시 열지 않았는데 글자가 바뀌었다");
            E2EHarness.Assert(_promptLabel.text.Contains(english),
                              $"en 칸의 값 \"{english}\"가 화면에 나왔다");
            E2EHarness.Assert(_promptLabel.text == 기대_프롬프트(),
                              $"en에서도 문장 전체가 표에서 나왔다 (기대: \"{기대_프롬프트()}\")");

            // 껍데기가 코드에 박혀 있으면 이름만 바뀌고 "줍기"는 그대로 남는다.
            // 그 한 가지를 콕 집는다.
            E2EHarness.Assert(!LocSentenceRules.HasHangul(_promptLabel.text),
                              $"en 프롬프트에 한글이 한 글자도 없다 (ko 껍데기: \"{한글껍데기}\")");

            E2EHarness.Log("— 겨눈 채로 pseudo로 —");
            Loc.SetLocale(StringCatalog.PseudoLocale);
            yield return null;
            yield return null;

            E2EHarness.Log($"  pseudo: \"{_promptLabel.text}\"");
            E2EHarness.Assert(PseudoLocalizer.IsTransformed(_promptLabel.text) ||
                              _promptLabel.text.Contains("[!!"),
                              "의사 번역이 프롬프트 안쪽까지 닿았다");
            E2EHarness.Assert(_promptLabel.text == 기대_프롬프트(),
                              "pseudo에서도 화면과 표의 답이 같다");
        }

        // ── 4. 표가 없어도 게임은 한국어로 돌아간다 ──────────────

        /// <summary>
        /// 표가 모자랄 때 무엇으로 물러서는가. <b>두 가지가 다르다.</b>
        ///
        /// 데이터 에셋의 이름은 표에 없으면 <b>에셋 원문</b>으로 돌아간다
        /// (<see cref="DataText"/>). 코드가 부르는 문장은 표에 없으면 <b>키</b>가 뜬다
        /// (<see cref="Loc"/>). 뒤쪽은 고장이 아니라 신호다 — 어디를 고쳐야 하는지
        /// 화면에 적히는 것이 조용히 비는 것보다 낫다.
        ///
        /// 그래서 두 번 나눠 본다. 껍데기만 남긴 표로 앞의 계약을,
        /// 표를 통째로 치워 뒤의 계약을 확인한다.
        /// </summary>
        static IEnumerator 표를_치우면_에셋_원문으로_돌아간다()
        {
            var 원래표 = Loc.Catalog;
            Loc.SetLocale(StringCatalog.DefaultLocale);

            E2EHarness.Log("— 데이터 에셋 칸만 빠진 표 —");
            Loc.Load(StringCatalog.Parse(껍데기만_있는_표));
            yield return null;
            yield return null;

            E2EHarness.Log($"  껍데기만: \"{_promptLabel.text}\"");
            E2EHarness.Assert(_promptLabel.text.Contains(_item.displayName),
                              $"에셋의 원문 \"{_item.displayName}\"이 그대로 나온다 (키가 아니라)");
            E2EHarness.Assert(!_promptLabel.text.Contains(".name"),
                              "폴백이 데이터 에셋 키를 화면에 내보내지 않는다");

            E2EHarness.Log("— 표를 통째로 치운다 (배포본에서 CSV가 빠진 상황) —");
            Loc.Load(StringCatalog.Empty);
            yield return null;
            yield return null;

            E2EHarness.Log($"  표 없음: \"{_promptLabel.text}\"");
            E2EHarness.Assert(!string.IsNullOrEmpty(_promptLabel.text),
                              "표가 없어도 프롬프트 자리가 비지 않는다");
            E2EHarness.Assert(_promptLabel.text.Contains(ShellKey.Key),
                              $"표가 없으면 키 \"{ShellKey.Key}\"가 뜬다 — 무엇이 빠졌는지 보인다");

            Loc.Load(원래표);
            yield return null;
        }

        /// <summary>
        /// 프롬프트 껍데기 한 줄만 든 표. 데이터 에셋 칸이 통째로 없는 상황을 만든다.
        /// </summary>
        const string 껍데기만_있는_표 =
            "Category,Key,ko,en,Comment\n" +
            "Prompt,pickup,[E] {0} 줍기,[E] Pick up {0},{0}=아이템 이름\n";

        static IEnumerator 되돌린다(string 처음로케일)
        {
            Loc.SetLocale(처음로케일);
            yield return null;

            if (_drop != null) Object.Destroy(_drop);
            yield return null;

            _drop = null;
            _promptLabel = null;
            _item = null;
        }
    }
}
