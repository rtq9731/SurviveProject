#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Survive.Localization
{
    /// <summary>
    /// 데이터 에셋을 디스크에서 찾아 오는 자리. <b>에디터 전용</b>이다.
    ///
    /// <b>왜 Domain 안에 두었는가.</b> 초안 생성 도구(<c>Assembly-CSharp-Editor</c>)와
    /// 게이트(<c>Survive.Tests.EditMode</c>)가 <b>정확히 같은 목록</b>을 봐야 한다.
    /// 두 어셈블리는 서로를 참조할 수 없고, 각자 훑으면 도구가 만들지 않는 키를
    /// 게이트가 요구하는 사태가 언제든 생긴다. 둘 다 참조하는 어셈블리는
    /// <c>Survive.Domain</c>뿐이라 여기가 유일한 자리다.
    ///
    /// 런타임에는 컴파일조차 되지 않으므로 Domain의 순수성은 그대로다.
    /// </summary>
    public static class DataTextAssets
    {
        /// <summary>데이터 에셋이 사는 곳. 여기 없는 ScriptableObject는 번역 대상이 아니다.</summary>
        public const string Root = "Assets/08.Data";

        /// <summary>
        /// <see cref="Root"/> 아래의 ScriptableObject 전부. 경로 순으로 정렬해
        /// 두 번 불러도 같은 순서가 나오게 한다.
        /// </summary>
        public static List<ScriptableObject> FindAll()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            paths.Sort(System.StringComparer.Ordinal);

            var found = new List<ScriptableObject>(paths.Count);
            foreach (var path in paths)
            {
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so != null) found.Add(so);
            }
            return found;
        }

        /// <summary>디스크의 에셋에서 번역 대상 칸을 전부 뽑는다.</summary>
        public static List<DataTextEntry> CollectAll()
        {
            var entries = new List<DataTextEntry>();
            DataTextCatalog.Collect(FindAll(), entries);
            return entries;
        }
    }
}
#endif
