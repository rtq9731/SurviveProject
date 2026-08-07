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
    /// <b>표를 스스로 찾는다.</b> 목록 에셋을 들고 있는 것은 제작 화면 하나뿐이라
    /// 그쪽에 문을 내면 화면이 없는 판에서 저장이 못 돈다. 이미 <b>불려 온</b>
    /// 에셋 중에서 찾는다 — 씬이 그 목록을 참조하고 있으므로 씬이 열려 있는 동안은
    /// 언제나 메모리에 있다.
    /// </summary>
    public static class RecipeIndex
    {
        static readonly Dictionary<string, RecipeSO> _byId = new Dictionary<string, RecipeSO>();
        static bool _built;

        /// <summary>
        /// 표를 콕 집어 준다. 굳이 찾게 하지 않아도 되는 자리(검사 등)를 위한 문이고,
        /// 부르지 않아도 <see cref="Find"/>가 알아서 찾는다.
        /// </summary>
        public static void Adopt(RecipeBookSO book)
        {
            if (book == null) return;
            담는다(book);
            _built = true;
        }

        /// <summary>표를 다시 짓게 한다. 씬을 갈아 끼웠을 때.</summary>
        public static void Forget()
        {
            _byId.Clear();
            _built = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void 판이_바뀌면_비운다() => Forget();

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

            foreach (var book in Resources.FindObjectsOfTypeAll<RecipeBookSO>())
                담는다(book);
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
    }
}
