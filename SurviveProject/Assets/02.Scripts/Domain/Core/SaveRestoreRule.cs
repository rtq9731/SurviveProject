using System;
using System.Collections.Generic;

namespace Survive.Core
{
    /// <summary>
    /// <b>저장본의 절을 되돌리는 순서 규칙.</b> 정확히는 <b>순서에 기대지 않는</b> 규칙이다.
    ///
    /// <b>왜 필요한가.</b> 절 하나를 되돌리는 일이 <b>다음 절의 주인을 태어나게</b> 한다.
    /// 「세계」 절은 생성 목록대로 몸을 다시 세우고, 그 몸들은 태어나는 자리에서
    /// 저장 대상으로 등록한다. 그래서 저장본을 위에서 아래로 한 번만 훑으면
    /// <b>아직 없는 주인을 가리키는 절</b>이 조용히 버려진다 — 그리고 그 절이
    /// 무엇이었는지는 아무 데도 남지 않는다.
    ///
    /// 지금 게임이 쓰는 저장본은 「세계」 절이 앞에 오도록 만들어지지만,
    /// <b>옛 저장본은 우리가 만든 순서로만 오지 않는다.</b> 사람이 고칠 수도 있고,
    /// 대상이 붙고 떨어지는 순서가 바뀌기만 해도 줄 차례가 달라진다. 순서를
    /// 약속으로 두면 그 약속이 깨지는 날 <b>저장이 물건을 먹고도 조용하다.</b>
    ///
    /// <b>그래서 훑기를 되풀이한다.</b> 한 바퀴에서 하나라도 앉았으면 그 바퀴가
    /// 새 주인을 만들었을 수 있으므로 남은 것을 들고 한 바퀴 더 돈다. 아무것도
    /// 못 앉힌 바퀴에서 멈춘다 — 그때 남은 것은 이 판이 모르는 열쇠이고,
    /// 그것은 정상이다(옛 저장본·지운 기능·다른 씬의 절).
    ///
    /// 최악은 절 수의 제곱이지만 절은 수십 개 규모이고, 실제로는 대개 두 바퀴에
    /// 끝난다. <b>몸에 딸린 것을 줄이 싣는 설계</b>(<c>SpawnRecord</c>)가 이미
    /// 대부분의 순서 물음을 없앴고, 여기 있는 것은 <b>그 설계가 닿지 않는
    /// 옛 저장본</b>을 위한 그물이다.
    /// </summary>
    public static class SaveRestoreRule
    {
        /// <summary>
        /// 절을 전부 앉힌다. 주인을 못 찾은 절은 미뤄 두었다가 다음 바퀴에 다시 본다.
        /// </summary>
        /// <param name="entries">저장본의 절들. null 칸은 무시한다.</param>
        /// <param name="tryApply">
        /// 절 하나를 앉혀 본다. <b>주인을 찾아 되돌렸으면 true</b>, 주인이 아직
        /// 없으면 false. <b>false는 「실패」가 아니라 「아직」</b>이라는 뜻이라야
        /// 한다 — 주인은 있는데 내용이 깨진 경우까지 false로 돌려주면 그 절이
        /// 매 바퀴 다시 시도된다.
        /// </param>
        /// <param name="unmatched">끝내 주인을 못 찾은 절의 수.</param>
        /// <returns>실제로 앉힌 절의 수.</returns>
        public static int Apply(IReadOnlyList<SaveEntry> entries,
                                Func<SaveEntry, bool> tryApply,
                                out int unmatched)
        {
            unmatched = 0;
            if (entries == null || tryApply == null) return 0;

            var 남은것 = new List<SaveEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null) 남은것.Add(entries[i]);

            int 앉힌것 = 0;

            while (남은것.Count > 0)
            {
                var 미룬것 = new List<SaveEntry>();
                int 이번바퀴 = 0;

                for (int i = 0; i < 남은것.Count; i++)
                {
                    if (tryApply(남은것[i])) 이번바퀴++;
                    else 미룬것.Add(남은것[i]);
                }

                앉힌것 += 이번바퀴;
                남은것 = 미룬것;

                // 한 바퀴를 통째로 돌고도 아무것도 못 앉혔으면 다음 바퀴도 같다.
                if (이번바퀴 == 0) break;
            }

            unmatched = 남은것.Count;
            return 앉힌것;
        }
    }
}
