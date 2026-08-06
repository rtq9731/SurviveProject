using Survive.Items;
using Survive.Localization;

namespace Survive.UI
{
    /// <summary>
    /// 획득 알림 한 줄에 <b>무엇이 적히는가</b>.
    ///
    /// <b>왜 그리는 쪽에서 떼어 냈는가.</b> 문장 규칙 게이트(<c>LocSentenceGateTests</c>)는
    /// 소스를 훑어 판정한다. 글자를 짓는 자리가 <c>MonoBehaviour</c> 안에 흩어져 있으면
    /// 그 판정을 단위 테스트로 되받을 수 없다. 여기 모아 두면 EditMode에서
    /// "표에서 나온 문장인가"를 직접 물을 수 있다.
    ///
    /// <b>수가 1일 때와 여럿일 때를 두 문장으로 가른다.</b> <c>" ×3"</c>을 이어 붙이면
    /// 그 조각의 자리를 언어가 못 옮긴다 — 세는 단위는 언어마다 아예 없거나(영어)
    /// 명사마다 다르다(한국어 개/자루). <c>Prompt/pickup</c>·<c>pickup_many</c>가
    /// 같은 문제를 이미 그렇게 풀었다.
    /// </summary>
    public static class PickupFeedText
    {
        public const string Category = "Feed";

        public static string Line(PickupFeedRow row) =>
            row == null ? "" : Line(row.Item, row.Count);

        public static string Line(ItemDataSO item, int count)
        {
            // 이름은 반드시 DataText를 거친다. displayName을 직접 읽으면 이 줄만
            // 로케일을 안 따라오고, 그 구멍은 그 로케일로 주워 보기 전까지 조용하다.
            string name = DataText.Name(item);

            return count > 1
                ? Loc.F(Category, "pickup_many", name, count)
                : Loc.F(Category, "pickup_one", name);
        }

        /// <summary>
        /// 아이콘이 없는 아이템의 자리를 지키는 한 글자.
        ///
        /// 아이콘을 통째로 지우면 그 줄만 글자가 왼쪽으로 밀려 목록이 어긋난다.
        /// 이름의 첫 글자를 대신 넣는다 — 제작 대기열 띠가 이미 쓰는 처방이다.
        /// 말이 아니라 <b>값에서 잘라 낸 글자</b>라 표에 넣을 것이 아니다.
        /// </summary>
        public static string IconLetter(ItemDataSO item)
        {
            string name = DataText.Name(item);
            if (string.IsNullOrEmpty(name)) return "";

            string trimmed = name.Trim();
            return trimmed.Length == 0 ? "" : trimmed.Substring(0, 1);
        }
    }
}
