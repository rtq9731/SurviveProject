using System;
using System.Collections.Generic;

namespace Survive.Progression
{
    /// <summary>
    /// 저장에 실려 나가는 원장의 몸통. JsonUtility는 HashSet을 못 다루므로
    /// 문자열 목록 하나로 납작하게 편다.
    /// </summary>
    [Serializable]
    public class UnlockLedgerState
    {
        public List<string> keys = new List<string>();
    }

    /// <summary>
    /// 해금된 것들의 <b>단일 원장</b>.
    ///
    /// 청사진이든 "이 재료를 이미 겪었다"는 발견 기록이든 전부 문자열 열쇠
    /// 하나로 들어온다. 둘을 나눠 두면 저장 항목이 둘, 복원 순서 문제도 둘이
    /// 되는데, 실제로 필요한 질문은 언제나 "이 열쇠가 있는가" 하나뿐이다.
    ///
    /// MonoBehaviour가 아니므로 Unity 실행 없이 테스트한다.
    /// </summary>
    public class UnlockLedger
    {
        readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>해금된 순서. 저장본을 사람이 읽을 때 순서가 있는 편이 낫다.</summary>
        readonly List<string> _order = new List<string>();

        public int Count => _order.Count;
        public IReadOnlyList<string> Keys => _order;

        /// <summary>새로 열린 열쇠 하나. 이미 있던 것은 울리지 않는다.</summary>
        public event Action<string> KeyUnlocked;

        /// <summary>
        /// 열려 있는가.
        ///
        /// <b>빈 열쇠는 항상 열려 있다.</b> "요구 청사진 없음"을 뜻하기 때문이다 —
        /// 이 규칙 덕분에 기존 레시피·건축 데이터는 한 글자도 고치지 않아도
        /// 예전 그대로 동작한다. 반대로 <b>모르는 열쇠는 잠겨 있다</b> —
        /// 아무도 열어 주지 않는 청사진을 요구하면 영영 잠긴 것이 맞다.
        /// </summary>
        public bool IsUnlocked(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return true;
            return _keys.Contains(key);
        }

        /// <returns>이번에 새로 열렸으면 true. 이미 있었거나 빈 열쇠면 false.</returns>
        public bool Unlock(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (!_keys.Add(key)) return false;

            _order.Add(key);
            KeyUnlocked?.Invoke(key);
            return true;
        }

        public void Clear()
        {
            _keys.Clear();
            _order.Clear();
        }

        // ── 저장 ─────────────────────────────────────────────────

        public UnlockLedgerState Capture() => new UnlockLedgerState { keys = new List<string>(_order) };

        /// <summary>
        /// 복원은 <see cref="KeyUnlocked"/>를 울리지 않는다. 불러오기는 발견이
        /// 아니다 — 여기서 울리면 저장본을 열 때마다 AI가 처음부터 다시 떠든다.
        /// </summary>
        public void Restore(UnlockLedgerState state)
        {
            Clear();
            if (state?.keys == null) return;

            foreach (var key in state.keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (_keys.Add(key)) _order.Add(key);
            }
        }
    }
}
