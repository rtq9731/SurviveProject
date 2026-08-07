using UnityEngine;
using Survive.Domain.Art;

namespace Survive.Art
{
    /// <summary>
    /// <b>스카이박스를 세우고 매 프레임 시각을 넣는 손.</b> 판단은 여기 없다 —
    /// 하늘색은 <see cref="DepthFog.SkyColor"/>가, 별은 <see cref="NightSky"/>가
    /// Unity 없이 답하고, 이 파일은 그 값을 셰이더에 꽂기만 한다.
    ///
    /// <b>왜 스카이박스가 필요했는가.</b> 앞 라운드는 카메라가 향한 고도로 안개
    /// 밀도를 갈아 끼웠다(<see cref="DepthFogService"/>). 그러면 "위를 보면 맑고
    /// 수평을 보면 뿌옇다"는 공짜로 나오지만 <b>한 화면 안에서 두 밀도가 동시에
    /// 보이지 않는다</b> — 넉 장을 견주면 보이는데 한 장 안에서는 안 보인다.
    /// 스카이박스는 화소마다 제 시선을 알므로 그 대비를 한 프레임에 낸다.
    ///
    /// <b>가장 값싼 길을 골랐다.</b> 하늘색은 정확히 이렇게 갈라진다:
    /// <code>SkyColor(고도, 햇빛) = HorizonColor(햇빛) × SkyCoverage(고도)</code>
    /// 뒤엣것에 <b>햇빛이 들어가지 않는다.</b> 그러므로 시선 고도별 표는
    /// <b>한 번만</b> 구우면 되고, 매 프레임 바뀌는 것은 색 하나다. 대기 적분을
    /// 화소마다 다시 푸는 절차적 스카이박스보다 싸고, 무엇보다 <b>규칙이
    /// 한 곳에만 적힌다</b>.
    ///
    /// <b>머티리얼 원본을 건드리지 않는다.</b> 에디터에서 공유 머티리얼의 값을
    /// 매 프레임 바꾸면 에셋이 더럽혀져 재생을 끈 자리의 시각이 파일에 남는다.
    /// 그래서 실행 중 사본을 만들어 그것을 씌우고, 끝나면 원본으로 되돌린다.
    /// </summary>
    public static class SkyDome
    {
        /// <summary>씬이 물고 있는 스카이박스 머티리얼의 셰이더 이름.</summary>
        public const string ShaderName = "Survive/Sky";

        static Material _runtime;
        static Material _sceneSkybox;
        static Texture2D _coverage;
        static bool _tried;

        /// <summary>지금 스카이박스가 서 있는가. 검증이 값으로 집기 위한 것이다.</summary>
        public static bool Standing => _runtime != null && RenderSettings.skybox == _runtime;

        /// <summary>가장 최근에 넣은 별의 남은 정도(0~1).</summary>
        public static float LastStarVisibility { get; private set; }

        /// <summary>실행 중 사본. 검증이 프로퍼티를 직접 읽거나 눌러 보기 위한 것이다.</summary>
        public static Material Runtime => _runtime;

        /// <summary>
        /// 하늘을 세우고 이 시각의 색을 넣는다. 지상에서만 부른다.
        /// 셰이더나 머티리얼이 없으면 <c>false</c>를 돌려주고 아무것도 하지 않는다 —
        /// 그때는 부르는 쪽이 예전처럼 단색 배경으로 지운다.
        /// </summary>
        public static bool Show(Camera eye, float daylight)
        {
            if (!Ensure()) return false;

            if (RenderSettings.skybox != _runtime) RenderSettings.skybox = _runtime;

            _runtime.SetColor(HorizonColorId, DepthFog.HorizonColor(daylight));

            LastStarVisibility = NightSky.StarVisibility(daylight);
            _runtime.SetFloat(StarVisibilityId, LastStarVisibility);

            if (eye != null) eye.clearFlags = CameraClearFlags.Skybox;
            return true;
        }

        /// <summary>
        /// 하늘을 치운다. <b>지하와 물속에는 하늘이 없다</b> — 여기서 손을 떼지 않으면
        /// 액면 아래에서 머리 위로 자홍 그라데이션이 보인다.
        ///
        /// 머티리얼은 그대로 두고 카메라만 단색으로 돌린다. 매 프레임 스카이박스를
        /// 끼웠다 뺐다 하면 Unity가 환경 반사를 다시 굽는다.
        /// </summary>
        public static void Hide(Camera eye)
        {
            if (eye != null && eye.clearFlags == CameraClearFlags.Skybox)
                eye.clearFlags = CameraClearFlags.SolidColor;
        }

        /// <summary>
        /// 손댄 것을 되돌린다. 재생을 끄면 씬이 다시 읽히므로 대개 필요 없지만,
        /// 검증이 껐다 켜며 견주는 동안 앞 판의 사본이 남지 않게 한다.
        /// </summary>
        public static void Restore()
        {
            if (_runtime != null && RenderSettings.skybox == _runtime)
                RenderSettings.skybox = _sceneSkybox;

            if (_runtime != null) Object.Destroy(_runtime);
            if (_coverage != null) Object.Destroy(_coverage);

            _runtime = null;
            _coverage = null;
            _sceneSkybox = null;
            _tried = false;
        }

        // ── 세우기 ──────────────────────────────────────────────

        static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
        static readonly int StarColorId = Shader.PropertyToID("_StarColor");
        static readonly int StarPeakId = Shader.PropertyToID("_StarPeak");
        static readonly int StarDimmestId = Shader.PropertyToID("_StarDimmest");
        static readonly int StarCellsId = Shader.PropertyToID("_StarCells");
        static readonly int StarChanceId = Shader.PropertyToID("_StarChance");
        static readonly int StarRadiusId = Shader.PropertyToID("_StarRadius");
        static readonly int StarVisibilityId = Shader.PropertyToID("_StarVisibility");

        static bool Ensure()
        {
            if (_runtime != null) return true;
            if (_tried) return false;
            _tried = true;

            // 씬이 물고 있는 것을 먼저 본다. 되돌릴 것이기도 하고,
            // 사람이 인스펙터에서 손댄 값이 있으면 그 위에서 시작해야 한다.
            _sceneSkybox = RenderSettings.skybox;

            bool 씬것이_우리것 = _sceneSkybox != null && _sceneSkybox.shader != null &&
                                 _sceneSkybox.shader.name == ShaderName;

            if (씬것이_우리것)
            {
                _runtime = new Material(_sceneSkybox);
            }
            else
            {
                // 씬이 아직 물고 있지 않아도 선다. 셰이더만 있으면 된다.
                var shader = Shader.Find(ShaderName);
                if (shader == null) return false;
                _runtime = new Material(shader);
            }

            _runtime.hideFlags = HideFlags.HideAndDontSave;
            _runtime.name = "Sky (runtime)";

            // 값의 주인은 Domain이다. 머티리얼에 저장된 값이 무엇이든 여기서 덮는다 —
            // 두 곳에 값이 있으면 인스펙터에서 한쪽만 고친 날에 규칙이 갈라진다.
            _coverage = BuildCoverage();
            _runtime.SetTexture(CoverageId, _coverage);
            _runtime.SetColor(StarColorId, NightSky.StarColor);
            _runtime.SetFloat(StarPeakId, NightSky.StarPeak);
            _runtime.SetFloat(StarDimmestId, NightSky.StarDimmest);
            _runtime.SetFloat(StarCellsId, NightSky.StarCells);
            _runtime.SetFloat(StarChanceId, NightSky.StarChance);
            _runtime.SetFloat(StarRadiusId, NightSky.StarRadius);
            _runtime.SetFloat(StarVisibilityId, 1f);

            return true;
        }

        /// <summary>
        /// 시선 고도(사인)별 대기 두께 표를 텍스처로 굽는다. <b>한 번만 굽는다</b> —
        /// 이 표에는 시각이 들어가지 않는다.
        ///
        /// 폭이 1인 세로 띠다. 선형 보간을 켜 두므로 칸 사이는 GPU가 이어 준다.
        /// </summary>
        static Texture2D BuildCoverage()
        {
            var table = NightSky.CoverageTable();

            var tex = new Texture2D(1, table.Length, TextureFormat.RFloat, false, true)
            {
                name = "SkyCoverage",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[table.Length];
            for (int i = 0; i < table.Length; i++) pixels[i] = new Color(table[i], 0f, 0f, 1f);

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
