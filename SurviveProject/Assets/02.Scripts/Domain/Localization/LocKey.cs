using System;

namespace Survive.Localization
{
    /// <summary>
    /// 번역 한 줄을 가리키는 이름표: <c>Category</c> + <c>Key</c>.
    ///
    /// <b>왜 구조체인가.</b> 제작 화면은 열려 있는 동안 매 프레임 목록을 다시 그린다.
    /// 조회를 <c>category + "/" + key</c>로 이어 붙여 사전을 뒤지면 프레임마다
    /// 문자열이 새로 생긴다. 두 참조를 그대로 들고 다니는 값 형식이면 조회에
    /// 할당이 없다(<see cref="IEquatable{T}"/>를 구현해 두었으므로
    /// <c>Dictionary</c>가 박싱 없이 비교한다).
    ///
    /// <b>정규화.</b> 앞뒤 공백은 떼고, 그 밖에는 손대지 않는다. 대소문자는 구별한다 —
    /// 표에 <c>craft_empty</c>와 <c>Craft_Empty</c>가 따로 있는 것은 실수이지 기능이 아니고,
    /// 그 실수는 중복 검사가 아니라 "없는 키" 게이트가 잡는 편이 빠르다.
    ///
    /// <c>Category</c>는 나중에 Unity Localization으로 갈아탈 때 그쪽의
    /// <c>String Table Collection</c> 이름에, <c>Key</c>는 <c>Entry Name</c>에 그대로 대응한다.
    /// </summary>
    public readonly struct LocKey : IEquatable<LocKey>
    {
        public readonly string Category;
        public readonly string Key;

        public LocKey(string category, string key)
        {
            Category = Normalize(category);
            Key = Normalize(key);
        }

        static string Normalize(string s) => string.IsNullOrEmpty(s) ? "" : s.Trim();

        public bool IsEmpty => Category.Length == 0 && Key.Length == 0;

        public bool Equals(LocKey other) =>
            string.Equals(Key, other.Key, StringComparison.Ordinal) &&
            string.Equals(Category, other.Category, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LocKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked { return (Category.GetHashCode() * 397) ^ Key.GetHashCode(); }
        }

        /// <summary>오류 문구에 쓰는 표기. 조회 경로에서는 부르지 않는다(할당이 생긴다).</summary>
        public override string ToString() => Category + "/" + Key;
    }
}
