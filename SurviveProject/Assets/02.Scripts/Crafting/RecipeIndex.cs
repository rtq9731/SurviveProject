using System.Collections.Generic;
using UnityEngine;

namespace Survive.Crafting
{
    /// <summary>
    /// <b>id로 레시피를 찾는 자리.</b>
    ///
    /// <see cref="RecipeBookSO"/>에는 조회 문이 없다 — 제작 화면이 배열을 처음부터
    /// 끝까지 훑기만 하면 됐기 때문이다(<c>ItemDatabaseSO</c>·<c>BuildCatalogSO</c>와
    /// 다른 점). 그런데 저장본이 <b>걸어 둔 제작</b>을 싣기 시작하면서 글자에서
    /// 레시피로 돌아오는 길이 필요해졌다. <c>RecipeBookTests</c>가 오래전에
    /// "겹친 id는 저장이 레시피를 id로 가리키기 시작하는 순간 조용히 엉뚱한 것을
    /// 가리킨다"고 적어 두었는데, 그 순간이 지금이다.
    ///
    /// <b>표를 스스로 찾되, 빌드에서도 닿는 길을 먼저 본다.</b> 예전에는
    /// <c>Resources.FindObjectsOfTypeAll</c> 하나뿐이었는데, 그것은 <b>이미 메모리에
    /// 올라와 있는</b> 것만 준다 — 에디터에서는 프로젝트의 에셋이 대개 올라와 있어서
    /// 언제나 찾아지고, 빌드에서는 씬이 그 목록을 참조할 때만 찾아진다. 즉
    /// <b>에디터에서는 영영 안 보이는 사고</b>가 될 수 있는 자리였고, 못 찾으면
    /// 걸어 둔 제작 하나가 경고와 함께 사라진다(재료는 걸 때 이미 빠진 뒤다).
    ///
    /// 그래서 길을 셋으로 두고 <b>이 차례</b>로 본다.
    /// <list type="number">
    /// <item><see cref="Adopt"/>로 콕 집어 준 표 — 부르는 쪽이 아는 판.</item>
    /// <item><see cref="RecipeBookLocatorSO"/> — <c>Resources.Load</c>로 닿는다.
    ///   <b>빌드에서 도는 길이 이것이다.</b></item>
    /// <item>이미 불려 온 에셋 훑기 — 위 둘이 다 없을 때의 그물.</item>
    /// </list>
    ///
    /// <see cref="LastSource"/>가 <b>어느 길로 답했는지</b>를 남긴다. 검사가
    /// "빌드에서 도는 길이 실제로 답했다"를 집을 수 있어야 하기 때문이다 —
    /// 에디터에서는 세 길이 전부 같은 답을 내므로, 답만 봐서는 아무것도 모른다.
    /// </summary>
    public static class RecipeIndex
    {
        /// <summary>표를 어디서 얻었는가.</summary>
        public enum Source
        {
            /// <summary>아직 안 지었다.</summary>
            None,

            /// <summary><see cref="Adopt"/>로 받았다.</summary>
            Adopted,

            /// <summary><see cref="RecipeBookLocatorSO"/> — 빌드에서 도는 길.</summary>
            Resources,

            /// <summary>이미 불려 온 에셋을 훑었다 — 에디터에서만 믿을 수 있는 길.</summary>
            LoadedScan,
        }

        static readonly Dictionary<string, RecipeSO> _byId = new Dictionary<string, RecipeSO>();
        static bool _built;

        /// <summary>
        /// 마지막으로 표를 지었을 때 <b>실제로 레시피를 내놓은</b> 길.
        /// 아무 길도 못 냈으면 <see cref="Source.None"/>.
        /// </summary>
        public static Source LastSource { get; private set; } = Source.None;

        /// <summary>지금 표에 담긴 레시피 수. 검사가 "정말 실렸는가"를 집는다.</summary>
        public static int Count => _byId.Count;

        /// <summary>
        /// 표를 콕 집어 준다. 굳이 찾게 하지 않아도 되는 자리(검사 등)를 위한 문이고,
        /// 부르지 않아도 <see cref="Find"/>가 알아서 찾는다.
        /// </summary>
        public static void Adopt(RecipeBookSO book)
        {
            if (book == null) return;
            담는다(book);
            _built = true;
            if (_byId.Count > 0) LastSource = Source.Adopted;
        }

        /// <summary>표를 다시 짓게 한다. 씬을 갈아 끼웠을 때.</summary>
        public static void Forget()
        {
            _byId.Clear();
            _built = false;
            LastSource = Source.None;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void 판이_바뀌면_비운다() => Forget();

        /// <summary>
        /// <b>빌드에서 닿는 길로만</b> 표를 짓는다. 검사가 그 길 하나를 따로 재려고
        /// 쓴다 — 에디터에서는 훑기가 언제나 성공하므로, 섞어 두면 이 길이 죽어도
        /// 아무도 모른다.
        /// </summary>
        /// <returns>그 길로 담은 레시피 수. 0이면 그 길이 끊겼다는 뜻이다.</returns>
        public static int BuildFromResourcesOnly()
        {
            _byId.Clear();
            _built = true;
            LastSource = Source.None;

            var locator = Resources.Load<RecipeBookLocatorSO>(RecipeBookLocatorSO.ResourceName);
            담는다(locator != null ? locator.Book : null);

            if (_byId.Count > 0) LastSource = Source.Resources;
            return _byId.Count;
        }

        /// <summary>
        /// id로 레시피를 찾는다. 없으면 null — 부르는 쪽이 사유를 적는다.
        ///
        /// <b>죽은 항목을 지운 뒤 다시 찾는다.</b> 도메인 리로드를 끈 에디터에서는
        /// 사전이 앞 판을 넘어오는데, 그때 담겨 있던 <see cref="ScriptableObject"/>가
        /// 파괴돼 있으면 null과 구별되지 않는 좀비를 돌려주게 된다.
        /// </summary>
        public static RecipeSO Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (!_built) 짓는다();

            if (_byId.TryGetValue(id, out var found) && found != null) return found;

            // 한 번은 다시 짓고 물어본다. 목록이 늦게 불려 왔을 수도 있다.
            짓는다();
            return _byId.TryGetValue(id, out var again) && again != null ? again : null;
        }

        static void 짓는다()
        {
            _byId.Clear();
            _built = true;
            LastSource = Source.None;

            // ① 빌드에서 도는 길. 여기서 채워지면 아래 훑기는 덧붙이기만 한다.
            var locator = Resources.Load<RecipeBookLocatorSO>(RecipeBookLocatorSO.ResourceName);
            담는다(locator != null ? locator.Book : null);
            if (_byId.Count > 0) LastSource = Source.Resources;

            // ② 그물. 종이가 빠졌거나 목록이 둘 이상인 판을 위해 남겨 둔다 —
            //    덮어쓰는 것이 아니라 <b>없는 id만</b> 채우므로, 위에서 얻은 표가
            //    이긴다. 그래야 "빌드에서 도는 길"과 "에디터에서 보이는 것"이
            //    어긋날 때 빌드 쪽 답이 참이 된다.
            int 훑기전 = _byId.Count;
            foreach (var book in Resources.FindObjectsOfTypeAll<RecipeBookSO>())
                빠진것만_담는다(book);

            if (LastSource == Source.None && _byId.Count > 훑기전)
                LastSource = Source.LoadedScan;
        }

        static void 담는다(RecipeBookSO book)
        {
            if (book == null || book.recipes == null) return;

            foreach (var r in book.recipes)
            {
                if (r == null || string.IsNullOrEmpty(r.id)) continue;
                _byId[r.id] = r;
            }
        }

        static void 빠진것만_담는다(RecipeBookSO book)
        {
            if (book == null || book.recipes == null) return;

            foreach (var r in book.recipes)
            {
                if (r == null || string.IsNullOrEmpty(r.id)) continue;
                if (_byId.TryGetValue(r.id, out var 이미) && 이미 != null) continue;
                _byId[r.id] = r;
            }
        }
    }
}
