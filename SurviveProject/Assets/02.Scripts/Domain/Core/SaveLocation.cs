using System;
using System.IO;

namespace Survive.Core
{
    /// <summary>
    /// <b>저장본이 어느 폴더에 놓이는가.</b> 슬롯 이름을 정하는 것은
    /// <see cref="SaveSlots"/>이고, 여기는 그 파일들이 앉을 <b>자리</b>를 정한다.
    /// 파일도 Unity도 모르는 순수 규칙으로 서 있으므로 검사가 빌드 경로까지 굴려 볼 수 있다.
    ///
    /// <b>왜 슬롯 격리만으로 모자랐는가 (2026-08-07).</b> 앞 라운드가 세운
    /// <see cref="SaveSlots"/>는 <b>하네스를 지나는 실행</b>만 막는다
    /// (<c>E2ERunner.RunGuarded</c>가 걸고 푼다). 그런데 우리는 시나리오 없이도 재생을
    /// 켠다 — 실측·스크린샷·눈으로 확인. 그때는 격리가 걸리지 않고,
    /// <c>SaveCoordinator.autoSaveOnObjective</c>가 목표를 넘길 때마다 기본 슬롯에 쓴다.
    /// 저장 경로는 이 기계의 에디터 전부가 공유하므로 <b>그 한 번이 사람의 이어하기를
    /// 덮는다.</b> 실제로 클론에서 재생만 켠 사이에 <c>save_auto.json</c>의 지문이 바뀌었다.
    ///
    /// <b>그래서 규율이 아니라 자리를 가른다.</b> 에디터는 <b>자기 프로젝트 경로로
    /// 갈린 하위 폴더</b>에 저장한다. 시나리오를 거치든 안 거치든, 자동 저장이든
    /// 사람이 손으로 누른 저장이든, <b>무엇이 저장하든</b> 클론의 저장본은 메인의
    /// 저장본과 섞일 수 없다. 슬롯 격리가 「무엇을 쓰는가」를 막는다면 이쪽은
    /// 「어디에 쓰는가」를 막는다 — 둘은 겹치지 않고 포개진다.
    ///
    /// <b>왜 회사·제품 이름을 가르지 않았는가.</b> 그 길이 더 깨끗하지만
    /// <c>ProjectSettings.asset</c>은 git으로 공유된다 — 클론에서 고치면 메인까지 따라오고
    /// <b>빌드의 저장 경로가 바뀐다.</b> 프로젝트 경로는 클론마다 이미 다르고 git에
    /// 실리지 않는다.
    ///
    /// <b>빌드는 한 치도 안 바뀐다.</b> <paramref name="insideEditor"/>가 거짓이면
    /// 준 경로를 그대로 돌려준다. 이것이 이 파일에서 가장 중요한 한 줄이고,
    /// <c>SaveLocationTests</c>가 음성 확인까지 붙여 못 박는다.
    /// </summary>
    public static class SaveLocation
    {
        /// <summary>갈래 폴더임을 이름만 보고 알 수 있게 하는 머리말.</summary>
        public const string EditorFolderPrefix = "Editor_";

        /// <summary>
        /// 저장본이 앉을 폴더.
        ///
        /// <paramref name="insideEditor"/>가 거짓이면 <paramref name="persistentDataPath"/>
        /// <b>그대로다</b>. 참이면 그 아래 프로젝트별 갈래 폴더로 내려간다 —
        /// <b>위로 올라가지 않는다.</b> 밖으로 나가는 순간 빌드와 에디터가 다른 규칙으로
        /// 사는 정도가 아니라 아무도 예상 못 한 자리에 파일이 생긴다.
        /// </summary>
        public static string RootFor(string persistentDataPath, string projectPath, bool insideEditor)
        {
            if (!insideEditor) return persistentDataPath;
            return Path.Combine(persistentDataPath, EditorFolderFor(projectPath));
        }

        /// <summary>이 프로젝트가 쓸 갈래 폴더 이름.</summary>
        public static string EditorFolderFor(string projectPath) =>
            EditorFolderPrefix + Fingerprint(Normalize(projectPath));

        /// <summary>갈래 폴더 이름인가. 이름만으로 판정한다.</summary>
        public static bool IsEditorFolder(string folderName) =>
            !string.IsNullOrEmpty(folderName) &&
            folderName.StartsWith(EditorFolderPrefix, StringComparison.Ordinal);

        /// <summary>
        /// 경로 표기의 차이를 지운다.
        ///
        /// 같은 프로젝트를 <c>E:\x\Assets</c>로도 <c>e:/x/assets/</c>로도 부를 수 있고,
        /// Unity가 어느 쪽을 주는지는 호출 자리에 달렸다. 표기가 다르다고 폴더가
        /// 갈리면 <b>같은 에디터가 재시작 뒤에 자기 저장본을 잃는다.</b>
        ///
        /// <c>Assets</c> 꼬리를 떼는 이유: <c>Application.dataPath</c>는 그것까지 주고
        /// 프로젝트 루트는 안 준다. 둘 중 어느 것을 넘겨도 같은 자리를 얻어야 한다.
        /// </summary>
        static string Normalize(string projectPath)
        {
            if (string.IsNullOrEmpty(projectPath)) return "";

            var s = projectPath.Replace('\\', '/').TrimEnd('/');
            if (s.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - "/Assets".Length);

            return s.ToLowerInvariant();
        }

        /// <summary>
        /// 경로를 8자리 열여섯진수로. <b>FNV-1a를 손으로 적는다</b> —
        /// <c>string.GetHashCode</c>는 런타임이 세션마다 다른 값을 낼 수 있고,
        /// 그러면 에디터를 껐다 켤 때마다 저장 폴더가 갈려 저장본이 사라진 것처럼 보인다.
        /// </summary>
        static string Fingerprint(string text)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;

            uint hash = offset;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= prime;
            }
            return hash.ToString("x8");
        }
    }
}
