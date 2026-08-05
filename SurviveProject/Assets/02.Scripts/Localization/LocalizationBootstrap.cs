using UnityEngine;

namespace Survive.Localization
{
    /// <summary>
    /// 번역 표를 읽어 <see cref="Loc"/>에 밀어 넣는 껍데기. 여기가 유일하게
    /// Unity(Resources)를 아는 자리다 — 파싱·폴백·의사 번역은 전부 Survive.Domain에 있다.
    ///
    /// <b>왜 Resources인가.</b> 이 프로젝트에는 Addressables가 없다. 번역 표는
    /// 게임이 뜨는 첫 프레임 전에, 씬과 무관하게, 동기로 읽혀야 한다 —
    /// 그 셋을 한꺼번에 만족하는 것이 Resources뿐이다. 표 한 장은 수십 KB라
    /// Resources가 통째로 빌드에 실린다는 단점도 문제가 되지 않는다.
    /// (나중에 Unity Localization으로 갈아탄다면 이 파일만 갈면 된다.)
    ///
    /// <b>왜 이 시점인가.</b> <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/>는
    /// 어떤 <c>Awake</c>보다 먼저 돈다. UI가 첫 글자를 쓰기 전에 표가 서 있어야
    /// 한 프레임 동안 키가 그대로 보이는 일이 없다.
    /// </summary>
    public static class LocalizationBootstrap
    {
        /// <summary>Resources 기준 경로. 실제 파일은 Assets/Resources/Localization/strings.csv.</summary>
        public const string ResourcePath = "Localization/strings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LoadOnStart() => Reload();

        /// <summary>
        /// 표를 다시 읽는다. 표가 없거나 망가져도 예외를 내지 않는다 —
        /// 그 경우 <see cref="Loc.T"/>가 키를 그대로 내보내므로 화면은 살아 있고,
        /// 무엇이 없는지가 화면에 그대로 적힌다.
        /// </summary>
        public static void Reload()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[번역] Resources/{ResourcePath}.csv를 못 읽었다. 키가 그대로 화면에 뜬다.");
                Loc.Load(StringCatalog.Empty);
                return;
            }

            var catalog = StringCatalog.Parse(asset.text);
            Loc.Load(catalog);

            // 표 자체가 망가진 것은 조용히 넘기면 안 된다. EditMode 게이트가 같은 것을
            // 실패로 잡지만, 빌드에서 표를 갈아 끼웠을 때는 이 로그가 유일한 신호다.
            foreach (var problem in catalog.Problems)
                Debug.LogWarning("[번역] " + problem);
        }
    }
}
