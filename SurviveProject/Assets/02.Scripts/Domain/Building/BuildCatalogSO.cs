using UnityEngine;

namespace Survive.Building
{
    /// <summary>
    /// 지을 수 있는 것들의 목록.
    /// RecipeBookSO와 같은 이유로 따로 둔다 — 씬이 개별 에셋을 일일이 참조하면
    /// 하나 늘 때마다 씬을 고쳐야 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Survive/Building/Build Catalog")]
    public class BuildCatalogSO : ScriptableObject
    {
        public BuildableSO[] entries = new BuildableSO[0];

        public BuildableSO GetById(string id)
        {
            if (string.IsNullOrEmpty(id) || entries == null) return null;
            foreach (var e in entries)
                if (e != null && e.id == id) return e;
            return null;
        }
    }
}
