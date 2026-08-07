using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Survive.Art;
using Survive.Domain.Art;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>지상에서 세계가 몇 미터에서 닫히는가</b>를 픽셀로 재는 자
    /// (상세기획서 §7.4).
    ///
    /// 재는 방법은 <see cref="DiveLightProbe"/>와 같다 — 카메라 정면 d미터에
    /// 회색 판을 세우고 그 판이 화면에 얼마나 밝게 찍히는지 읽는다. 세계의 지형을
    /// 재면 "안개가 삼켰다"와 "애초에 저기 아무것도 없다"가 섞인다.
    ///
    /// <b>여기서 답하는 물음 둘.</b>
    /// <list type="number">
    /// <item><b>수평 가시거리</b> — 몇 미터에서 회색 판이 안개색에 잠기는가</item>
    /// <item><b>천정 대 지평선</b> — 같은 자리에서 위를 볼 때와 수평을 볼 때
    ///       안개가 몇 배 차이인가. <b>이것이 「탁한 것이 아니라 두꺼운 것」의
    ///       숫자</b>다</item>
    /// </list>
    ///
    /// 낮과 밤 각각 잰다. 밤에 자홍이 짙어지는 것은 값이 아니라 색이므로
    /// 안개색도 함께 적는다.
    ///
    /// <b>읽는 법 — 곡선의 기울기가 아니라 「어디서 눕는가」를 본다.</b>
    /// 여기서 읽는 픽셀은 톤매핑을 지난 값이라, 안개가 선형으로 섞인 것이
    /// 화면에서는 <b>가까이서 급히 오르고 멀리서 눕는</b> 꼴로 보인다.
    /// 그래서 중간 지점의 비율은 안개의 비율이 아니다. 반면 <b>눕는 자리</b>는
    /// 톤매핑이 옮기지 못한다 — 판이 안개와 구별되지 않게 된 거리이고,
    /// 그것이 곧 <b>세계가 닫히는 거리</b>다.
    /// </summary>
    public static class SurfaceFogProbe
    {
        /// <summary>가장 최근 보고. 코루틴이 끝나면 여기 앉는다.</summary>
        public static string LastReport { get; private set; } = "";

        /// <summary>아직 도는 중인가. 폴링하는 쪽이 본다.</summary>
        public static bool Running { get; private set; }

        class Host : MonoBehaviour { }

        /// <summary>재기를 시작한다. 결과는 <see cref="LastReport"/>로 온다.</summary>
        public static string Begin(string distances = "10,25,50,75,100,150,200,300,400,600")
        {
            if (Running) return "{\"error\":\"already running\"}";

            var go = new GameObject("SurfaceFogProbeRunner") { hideFlags = HideFlags.HideAndDontSave };
            var host = go.AddComponent<Host>();
            Running = true;
            LastReport = "";
            host.StartCoroutine(Report(distances, go));
            return "{\"started\":true}";
        }

        static IEnumerator Report(string distances, GameObject host)
        {
            var 시계 = DayNightService.Instance;
            bool 얼렸던가 = 시계 != null && 시계.Frozen;
            if (시계 != null) 시계.Frozen = true;

            float 트인거리 = 전망을_고른다(out var 자리);

            var sb = new StringBuilder();
            sb.Append("{\"spot\":\"").Append(자리.ToString("F2")).Append('"')
              .Append(",\"lift\":").Append(F(_올림))
              .Append(",\"clearBearing\":\"").Append(_수평방향.ToString("F2")).Append('"')
              .Append(",\"clearDistance\":").Append(F(트인거리))
              .Append(",\"airLineY\":").Append(F(DepthFog.AirLineY))
              .Append(",\"zenithClearing\":").Append(F(DepthFog.ZenithClearing))
              .Append(",\"horizonReach\":").Append(F(DepthFog.SurfaceHorizonReach))
              .Append(",\"rows\":[");

            bool first = true;
            foreach (var (국면, 시각) in new[] { ("낮", 0.5f), ("밤", 0f) })
            {
                if (시계 != null) 시계.SetTimeOfDay(시각);

                foreach (var (겨냥, 천정) in new[] { ("수평", false), ("천정", true) })
                {
                    yield return 자리를_잡는다(자리, 천정);

                    if (!first) sb.Append(',');
                    first = false;

                    sb.Append("{\"phase\":\"").Append(국면).Append('"')
                      .Append(",\"aim\":\"").Append(겨냥).Append('"')
                      .Append(",\"timeOfDay\":").Append(F(시각))
                      .Append(',').Append(맥락())
                      .Append(",\"frame\":").Append(DiveLightProbe.Frame())
                      .Append(",\"sweep\":").Append(DiveLightProbe.Sweep(distances))
                      .Append('}');
                }
            }

            sb.Append("]}");
            LastReport = sb.ToString();

            if (시계 != null)
            {
                시계.Frozen = 얼렸던가;
                시계.SetTimeOfDay(DayNightService.StartTimeOfDay);
            }

            Running = false;
            Object.Destroy(host);
        }

        /// <summary>
        /// 사람 손이 스크린샷을 찍을 동안 상태를 붙들어 둔다.
        /// 시계가 흐르면 셔터를 누른 순간에는 이미 다른 시각이다.
        /// </summary>
        public static string Hold(float timeOfDay, bool zenith, float seconds = 90f)
        {
            if (Running) return "{\"error\":\"already running\"}";

            var go = new GameObject("SurfaceFogProbeHold") { hideFlags = HideFlags.HideAndDontSave };
            var host = go.AddComponent<Host>();
            Running = true;
            host.StartCoroutine(HoldRoutine(timeOfDay, zenith, seconds, go));
            return "{\"holding\":true,\"timeOfDay\":" + F(timeOfDay) + ",\"zenith\":" + B(zenith) + "}";
        }

        static IEnumerator HoldRoutine(float timeOfDay, bool zenith, float seconds, GameObject host)
        {
            var 시계 = DayNightService.Instance;
            if (시계 != null) { 시계.Frozen = true; 시계.SetTimeOfDay(timeOfDay); }

            // 자리를 한 번만 정하고 붙든다. 매 프레임 다시 고르면 넉 장의
            // 구도가 서로 달라져 비교가 성립하지 않는다.
            전망을_고른다(out var 자리);

            float end = Time.unscaledTime + seconds;
            while (Running && Time.unscaledTime < end)
            {
                누른다(자리, zenith);
                yield return null;
            }

            Running = false;
            Object.Destroy(host);
        }

        /// <summary>지금 붙들고 있는 것을 그만 붙든다.</summary>
        public static string Release()
        {
            Running = false;
            return "{\"released\":true}";
        }

        // ══════════════════════════════════════════════════════════
        // 한 프레임 안의 대비 — 스카이박스가 선 뒤의 자
        // ══════════════════════════════════════════════════════════
        //
        // <b>앞 라운드는 프레임을 갈라 쟀다.</b> 수평을 보는 장과 천정을 보는 장을
        // 따로 찍어 견주었고, 그것으로 나온 10.2배는 "안개 규칙이 고도를 따라
        // 움직인다"는 증거이지 <b>「한 화면 안에서 대비가 보인다」의 증거가
        // 아니다.</b> 스카이박스가 서면 그 둘이 갈라진다 — 이제 한 장 안에서 잰다.
        //
        // <b>하늘만 따로 잰다.</b> 카메라의 컬링 마스크를 비우면 배경만 남는다.
        // 지형이 섞이면 재고 있는 것이 하늘의 대비인지 산의 그림자인지 갈리지 않는다.
        // 화면에 실제로 보이는 값도 함께 적어 둘이 어긋나지 않는지 본다.
        //
        // <b>별은 두 번 그려서 뺀다.</b> 밝은 점을 문턱으로 세면 "저것이 별인가
        // 지평선의 밝은 띠인가"를 못 가른다. 별을 끈 장과 켠 장의 차이는 정확히
        // 별이 그린 화소다.

        /// <summary>한 프레임 안에서 위아래 띠의 휘도를 잰다. 결과는 <see cref="LastReport"/>로 온다.</summary>
        public static string BeginContrast(int strips = 10, float pitch = 45f, float fov = 100f,
                                           int width = 960, int height = 540)
        {
            if (Running) return "{\"error\":\"already running\"}";

            var go = new GameObject("SurfaceFogContrastRunner") { hideFlags = HideFlags.HideAndDontSave };
            var host = go.AddComponent<Host>();
            Running = true;
            LastReport = "";
            host.StartCoroutine(Contrast(strips, pitch, fov, width, height, go));
            return "{\"started\":true}";
        }

        static IEnumerator Contrast(int strips, float pitch, float fov, int width, int height,
                                    GameObject host)
        {
            var 시계 = DayNightService.Instance;
            bool 얼렸던가 = 시계 != null && 시계.Frozen;
            if (시계 != null) 시계.Frozen = true;

            float 트인거리 = 전망을_고른다(out var 자리);

            var sb = new StringBuilder();
            sb.Append("{\"spot\":\"").Append(자리.ToString("F2")).Append('"')
              .Append(",\"lift\":").Append(F(_올림))
              .Append(",\"clearDistance\":").Append(F(트인거리))
              .Append(",\"pitchDeg\":").Append(F(pitch))
              .Append(",\"fovDeg\":").Append(F(fov))
              .Append(",\"strips\":").Append(strips)
              .Append(",\"zenithClearing\":").Append(F(DepthFog.ZenithClearing))
              .Append(",\"skyboxStanding\":").Append(B(SkyDome.Standing))
              .Append(",\"approxStars\":").Append(NightSky.ApproximateStarCount)
              .Append(",\"starAngularDeg\":").Append(F5(NightSky.StarAngularDiameterDegrees))
              .Append(",\"shots\":[");

            bool first = true;
            foreach (var (국면, 시각) in new[] { ("낮", 0.5f), ("밤", 0f) })
            {
                if (시계 != null) 시계.SetTimeOfDay(시각);

                // 안개는 LateUpdate에서 깔리고 후처리가 한 박자 늦는다.
                for (int i = 0; i < 8; i++) { 기울여_누른다(자리, pitch); yield return null; }

                if (!first) sb.Append(',');
                first = false;

                sb.Append("{\"phase\":\"").Append(국면).Append('"')
                  .Append(",\"timeOfDay\":").Append(F(시각))
                  .Append(",\"starVisibility\":").Append(F5(SkyDome.LastStarVisibility))
                  .Append(',').Append(맥락())
                  .Append(',').Append(띠를_잰다(strips, fov, width, height))
                  .Append('}');
            }

            sb.Append("]}");
            LastReport = sb.ToString();

            if (시계 != null)
            {
                시계.Frozen = 얼렸던가;
                시계.SetTimeOfDay(DayNightService.StartTimeOfDay);
            }

            Running = false;
            Object.Destroy(host);
        }

        /// <summary>
        /// 지금 카메라가 보고 있는 것을 가로 띠로 잘라 각 띠의 휘도를 낸다.
        ///
        /// 세 장을 그린다 — 하늘만(별 켬) · 하늘만(별 끔) · 화면 그대로.
        /// 앞 둘의 차이가 별이고, 셋째가 사람이 실제로 보는 값이다.
        /// </summary>
        static string 띠를_잰다(int strips, float fov, int width, int height)
        {
            var cam = Camera.main;
            if (cam == null) return "\"error\":\"no camera\"";

            float 원래화각 = cam.fieldOfView;
            int 원래마스크 = cam.cullingMask;
            var 원래타깃 = cam.targetTexture;
            var 원래활성 = RenderTexture.active;

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.sRGB);
            var 하늘 = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var 별없는하늘 = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var 화면 = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                cam.fieldOfView = fov;
                cam.targetTexture = rt;

                cam.cullingMask = 0;                       // 배경만 남긴다
                그려_읽는다(cam, rt, 하늘, width, height);

                float 별원래 = 별을_끈다();
                그려_읽는다(cam, rt, 별없는하늘, width, height);
                별을_되돌린다(별원래);

                cam.cullingMask = 원래마스크;
                그려_읽는다(cam, rt, 화면, width, height);

                return 띠_보고(cam, 하늘, 별없는하늘, 화면, strips, fov, width, height);
            }
            finally
            {
                cam.fieldOfView = 원래화각;
                cam.cullingMask = 원래마스크;
                cam.targetTexture = 원래타깃;
                RenderTexture.active = 원래활성;
                Object.DestroyImmediate(하늘);
                Object.DestroyImmediate(별없는하늘);
                Object.DestroyImmediate(화면);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static void 그려_읽는다(Camera cam, RenderTexture rt, Texture2D into, int width, int height)
        {
            cam.Render();
            RenderTexture.active = rt;
            into.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            into.Apply(false);
        }

        static float 별을_끈다()
        {
            var m = SkyDome.Runtime;
            if (m == null) return -1f;

            float before = m.GetFloat("_StarVisibility");
            m.SetFloat("_StarVisibility", 0f);
            return before;
        }

        static void 별을_되돌린다(float before)
        {
            var m = SkyDome.Runtime;
            if (m != null && before >= 0f) m.SetFloat("_StarVisibility", before);
        }

        static string 띠_보고(Camera cam, Texture2D 하늘, Texture2D 별없는하늘, Texture2D 화면,
                              int strips, float fov, int width, int height)
        {
            var sb = new StringBuilder();
            sb.Append("\"bands\":[");

            int 띠높이 = Mathf.Max(1, height / strips);
            float tanHalf = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            float 위 = 0f, 아래 = 0f;
            int 별화소합 = 0;
            float 별최대 = 0f;

            for (int s = 0; s < strips; s++)
            {
                // ReadPixels의 y는 아래에서 위로 자란다. 0번 띠를 화면 위로 잡는다.
                int y0 = height - (s + 1) * 띠높이;
                int h = 띠높이;
                if (s == strips - 1) { h += y0; y0 = 0; }

                var 하늘화소 = 하늘.GetPixels(0, y0, width, h);
                var 별없는화소 = 별없는하늘.GetPixels(0, y0, width, h);
                var 화면화소 = 화면.GetPixels(0, y0, width, h);

                float 하늘휘도 = 평균휘도(하늘화소);
                float 화면휘도 = 평균휘도(화면화소);

                int 별화소 = 0;
                float 이띠최대 = 0f;
                for (int i = 0; i < 하늘화소.Length; i++)
                {
                    float diff = 휘도(하늘화소[i]) - 휘도(별없는화소[i]);
                    if (diff > 0.01f) 별화소++;
                    if (diff > 이띠최대) 이띠최대 = diff;
                }
                별화소합 += 별화소;
                if (이띠최대 > 별최대) 별최대 = 이띠최대;

                // 이 띠 한가운데를 지나는 시선의 고도. 원근 투영을 그대로 되짚는다.
                float ndc = 2f * ((y0 + h * 0.5f) / height) - 1f;
                var camDir = new Vector3(0f, ndc * tanHalf, 1f).normalized;
                float elevation = Mathf.Asin(Mathf.Clamp((cam.transform.rotation * camDir).y, -1f, 1f))
                                  * Mathf.Rad2Deg;

                if (s == 0) 위 = 하늘휘도;
                if (s == strips - 1) 아래 = 하늘휘도;

                if (s > 0) sb.Append(',');
                sb.Append("{\"i\":").Append(s)
                  .Append(",\"elevationDeg\":").Append(F(elevation))
                  .Append(",\"skyLum\":").Append(F5(하늘휘도))
                  .Append(",\"screenLum\":").Append(F5(화면휘도))
                  .Append(",\"starPixels\":").Append(별화소)
                  .Append(",\"starMaxDelta\":").Append(F5(이띠최대))
                  .Append('}');
            }

            sb.Append(']')
              .Append(",\"topBandLum\":").Append(F5(위))
              .Append(",\"bottomBandLum\":").Append(F5(아래))
              .Append(",\"bottomOverTop\":").Append(F(아래 <= 0f ? -1f : 위 <= 0f ? -1f : 아래 / 위))
              .Append(",\"starPixelsTotal\":").Append(별화소합)
              .Append(",\"starMaxDelta\":").Append(F5(별최대));

            return sb.ToString();
        }

        static float 휘도(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static float 평균휘도(Color[] pixels)
        {
            if (pixels.Length == 0) return 0f;
            double sum = 0d;
            for (int i = 0; i < pixels.Length; i++) sum += 휘도(pixels[i]);
            return (float)(sum / pixels.Length);
        }

        /// <summary>
        /// 스크린샷용. 지평선에서 천정까지 한 장에 담기는 화각으로 붙들어 둔다.
        /// 화각을 넓히는 것은 <b>한 장 안에 대비가 보이는지</b>가 이 라운드의
        /// 요점이기 때문이다 — 기본 화각으로는 지평선과 천정이 같은 장에 안 들어온다.
        /// </summary>
        public static string HoldWide(float timeOfDay, float pitch = 45f, float fov = 100f,
                                      float seconds = 120f)
        {
            if (Running) return "{\"error\":\"already running\"}";

            var go = new GameObject("SurfaceFogProbeHoldWide") { hideFlags = HideFlags.HideAndDontSave };
            var host = go.AddComponent<Host>();
            Running = true;
            host.StartCoroutine(HoldWideRoutine(timeOfDay, pitch, fov, seconds, go));
            return "{\"holding\":true,\"timeOfDay\":" + F(timeOfDay) +
                   ",\"pitch\":" + F(pitch) + ",\"fov\":" + F(fov) + "}";
        }

        static IEnumerator HoldWideRoutine(float timeOfDay, float pitch, float fov, float seconds,
                                           GameObject host)
        {
            var 시계 = DayNightService.Instance;
            if (시계 != null) { 시계.Frozen = true; 시계.SetTimeOfDay(timeOfDay); }

            전망을_고른다(out var 자리);
            화각을_붙든다(fov);

            float end = Time.unscaledTime + seconds;
            while (Running && Time.unscaledTime < end)
            {
                기울여_누른다(자리, pitch);
                yield return null;
            }

            화각을_놓는다();
            Running = false;
            Object.Destroy(host);
        }

        // ── 화각을 붙드는 법 ─────────────────────────────────────
        //
        // <b>Update에서 넣은 화각은 그려지기 전에 사라진다.</b> CinemachineBrain이
        // 매 프레임 LateUpdate에서 가상 카메라의 렌즈로 카메라를 통째로 덮어쓰기
        // 때문이다 — DepthFogService가 원거리 평면에서 이미 당한 것과 같은 자리다.
        // 그래서 그리기 직전에 밀어 넣는다.
        //
        // 재는 쪽(<see cref="띠를_잰다"/>)은 이 갈고리가 필요 없다. 화각을 넣고
        // 곧바로 <c>cam.Render()</c>를 부르므로 그 사이에 Cinemachine이 끼어들 틈이 없다.

        static float _붙든화각 = -1f;
        static bool _갈고리;

        static void 화각을_붙든다(float fov)
        {
            _붙든화각 = fov;
            if (_갈고리) return;

            RenderPipelineManager.beginCameraRendering += 화각을_밀어넣는다;
            _갈고리 = true;
        }

        static void 화각을_놓는다()
        {
            if (!_갈고리) return;

            RenderPipelineManager.beginCameraRendering -= 화각을_밀어넣는다;
            _갈고리 = false;
            _붙든화각 = -1f;
        }

        static void 화각을_밀어넣는다(ScriptableRenderContext context, Camera camera)
        {
            if (_붙든화각 > 0f && camera == Camera.main) camera.fieldOfView = _붙든화각;
        }

        /// <summary>자리를 누르고 시선을 이 고도로 올린다. 방위는 트인 쪽 그대로다.</summary>
        static void 기울여_누른다(Vector3 자리, float pitch)
        {
            E2EHarness.Teleport(자리);

            var cam = Camera.main;
            Vector3 눈 = cam != null ? cam.transform.position : 자리;

            var dir = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, _수평방향).normalized)
                      * _수평방향;
            E2EHarness.LookAt(눈 + dir.normalized * 200f);
        }

        /// <summary>지금 화면의 안개 상태를 한 줄로. 스크린샷 옆에 적을 값이다.</summary>
        public static string Now() => "{" + 맥락() + "}";

        // ── 겨냥 ────────────────────────────────────────────────

        static IEnumerator 자리를_잡는다(Vector3 자리, bool 천정)
        {
            // 안개는 LateUpdate에서 깔리고 후처리 블렌딩이 한 박자 늦는다.
            // 몇 프레임 그린 뒤에 읽어야 앞 상태의 잔상을 안 잰다.
            for (int i = 0; i < 8; i++)
            {
                누른다(자리, 천정);
                yield return null;
            }
        }

        /// <summary>
        /// 자리와 겨냥을 다시 눌러 둔다. 몸은 중력에 끌려 내려가고 시선은
        /// 손을 떼면 흐르므로, 매 프레임 눌러야 넉 장이 같은 구도가 된다.
        /// </summary>
        static void 누른다(Vector3 자리, bool 천정)
        {
            E2EHarness.Teleport(자리);

            var cam = Camera.main;
            Vector3 눈 = cam != null ? cam.transform.position : 자리;

            // <b>겨냥을 눈높이에서 만든다.</b> 카메라가 몸보다 높으므로 몸 좌표에
            // 겨냥을 걸면 수평이 아니라 살짝 내려다보는 각이 된다 — 그러면
            // "수평에서 잰 값"이 수평의 값이 아니다.
            Vector3 대상 = 천정 ? 눈 + Vector3.up * 200f : 눈 + _수평방향 * 100f;

            E2EHarness.LookAt(대상);
        }

        // ── 전망 고르기 ─────────────────────────────────────────

        static Vector3 _수평방향 = Vector3.forward;

        /// <summary>착륙 지점에서 몇 미터 올려 섰는가. 보고에 그대로 적는다.</summary>
        static float _올림;

        /// <summary>지평선까지 트였다고 볼 거리(m).</summary>
        const float 트임기준 = 250f;

        /// <summary>
        /// <b>지평선이 실제로 보이는 자리와 쪽을 고른다.</b>
        ///
        /// 처음 잰 값이 거리와 무관하게 딱 붙어 있길래 보니, 착륙 지점 코앞
        /// <b>2.3m</b>에 버섯 군락이 서 있어서 뒤에 세운 시험판을 전부 가리고
        /// 있었다. 그러면 재고 있는 것은 안개가 아니라 <b>버섯의 밝기</b>다.
        /// 눈으로는 구별되지 않는 종류의 사고라, 자리를 손으로 적지 않고
        /// 매번 다시 고른다.
        ///
        /// <b>한 줄기가 아니라 부채로 쏜다.</b> 바위 틈으로 한 줄기가 빠져나가는
        /// 자리를 "트였다"고 고르면, 재는 값은 멀쩡한데 <b>화면은 온통 바위</b>다.
        /// 스크린샷과 실측이 같은 것을 말하려면 화면이 담는 만큼을 봐야 한다.
        ///
        /// <b>필요하면 올려 선다.</b> 착륙 지점은 지형에 파묻혀 있어 서 있는 높이로는
        /// 어느 쪽도 트이지 않는다. 올린 높이는 보고에 적는다 — 「사람이 걸어가서
        /// 보는 그림」이 아니라 「같은 자리에서 지평선을 보는 그림」이다.
        /// </summary>
        static float 전망을_고른다(out Vector3 자리)
        {
            var 사람 = E2EHarness.Player.transform.position;
            var cam = Camera.main;
            float 눈높이 = cam != null ? cam.transform.position.y - 사람.y : 1.5f;

            float 최고 = -1f;
            var 최고방향 = Vector3.forward;
            float 최고올림 = 0f;

            for (float lift = 0f; lift <= 40f; lift += 4f)
            {
                Vector3 눈 = new Vector3(사람.x, 사람.y + lift + 눈높이, 사람.z);
                float d = 트인_쪽을_고른다(눈, out var dir);

                if (d > 최고) { 최고 = d; 최고방향 = dir; 최고올림 = lift; }
                if (d >= 트임기준) break;
            }

            _수평방향 = 최고방향;
            _올림 = 최고올림;
            자리 = new Vector3(사람.x, 사람.y + 최고올림, 사람.z);
            return 최고;
        }

        /// <summary>이 눈높이에서 가장 멀리 트인 쪽. 부채의 가장 가까운 벽으로 점수를 매긴다.</summary>
        static float 트인_쪽을_고른다(Vector3 눈, out Vector3 방향)
        {
            float best = -1f;
            방향 = Vector3.forward;

            for (int i = 0; i < 36; i++)
            {
                var yaw = Quaternion.Euler(0f, i * 10f, 0f);

                float 점수 = float.MaxValue;
                foreach (float pitch in new[] { 8f, 0f, -4f })
                    foreach (float spread in new[] { -12f, 0f, 12f })
                    {
                        var dir = yaw * Quaternion.Euler(-pitch, spread, 0f) * Vector3.forward;
                        float d = Physics.Raycast(눈, dir, out var hit, 1000f, ~0,
                                                  QueryTriggerInteraction.Ignore)
                            ? hit.distance : 1000f;
                        if (d < 점수) 점수 = d;
                    }

                if (점수 <= best) continue;
                best = 점수;
                방향 = yaw * Vector3.forward;
            }

            return best;
        }

        // ── 맥락 ────────────────────────────────────────────────

        static string 맥락()
        {
            var cam = Camera.main;
            float elevation = cam == null ? 0f
                : Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

            float density = RenderSettings.fogDensity;

            return "\"onSurface\":" + B(DepthFogService.LastOnSurface) +
                   ",\"elevationDeg\":" + F(elevation) +
                   ",\"playerY\":" + F(E2EHarness.Player != null ? E2EHarness.Player.transform.position.y : 0f) +
                   ",\"daylight\":" + F(DepthFogService.LastDaylight) +
                   ",\"fogDensity\":" + F5(density) +
                   ",\"fullDistance\":" + F(DepthFog.FullDistance(density)) +
                   ",\"fogColor\":\"" + RenderSettings.fogColor.ToString("F3") + "\"" +
                   ",\"fogLuminance\":" + F5(ArtPalette.Luminance(RenderSettings.fogColor)) +
                   ",\"skyColor\":\"" + DepthFogService.LastSkyColor.ToString("F3") + "\"" +
                   ",\"skyLuminance\":" + F5(ArtPalette.Luminance(DepthFogService.LastSkyColor)) +
                   ",\"farClip\":" + F(cam != null ? cam.farClipPlane : 0f);
        }

        static string B(bool v) => v ? "true" : "false";
        static string F(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
        static string F5(float v) => v.ToString("F5", CultureInfo.InvariantCulture);
    }
}
