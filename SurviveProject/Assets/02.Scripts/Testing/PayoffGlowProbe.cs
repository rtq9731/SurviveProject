using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Survive.Combat;
using Survive.Creatures;

namespace Survive.Testing
{
    /// <summary>
    /// <b>배부른 것이 눈으로 읽히는가</b>를 픽셀로 재는 자 (기획서 §3.4).
    ///
    /// 기획서는 "배부른 생산자는 몸체가 부풀고 접합부가 빛난다"고 적었고,
    /// 그 한 줄 위에 차별화 축이 통째로 얹혀 있다 — <b>아무도 알려 주지 않으므로
    /// 플레이어가 볼 수 있어야만 고를 수 있다.</b> 그런데 "빛난다"는 스크린샷으로
    /// 판정할 수 없는 종류의 문장이다. 두 개체를 나란히 세워 놓고 <b>같은 프레임에서</b>
    /// 각자의 몸이 차지한 화면 조각의 휘도를 읽어야 "몇 배"라고 말할 수 있다.
    ///
    /// 재는 방법은 <see cref="DiveLightProbe"/>와 같은 결이다 — 카메라를 렌더
    /// 텍스처에 그리고 화면 조각의 휘도를 센다. 다만 회색 판을 세우지 않는다.
    /// 여기서 재려는 것이 <b>몸 자체의 밝기</b>라, 판을 세우면 그 판이 곧
    /// 재려던 대상을 가린다.
    ///
    /// 사람 눈으로 볼 스크린샷도 이 무대에서 찍는다. 무대가 둘이면 숫자와 그림이
    /// 서로 다른 자리의 말을 하게 된다.
    /// </summary>
    public static class PayoffGlowProbe
    {
        /// <summary>굶은 개체. 무대를 세우면 여기 앉는다.</summary>
        public static CreatureFeeding Starved { get; private set; }

        /// <summary>배부른 개체.</summary>
        public static CreatureFeeding Full { get; private set; }

        /// <summary>무대에 세워 둔 두 개체의 뿌리. 치울 때 쓴다.</summary>
        static GameObject _starvedGo, _fullGo;

        // ── 무대 세우기 ─────────────────────────────────────────

        /// <summary>
        /// 굶은 개체와 배부른 개체를 <b>나란히</b> 세우고 그 둘을 함께 바라본다.
        ///
        /// 나란히 세우는 것이 요점이다. 따로 찍은 두 장은 노출도 각도도 다르므로
        /// "더 밝다"를 말할 수 없다 — <b>같은 프레임에 둘이 함께 있어야</b>
        /// 사람이 눈으로도 기계가 픽셀로도 같은 것을 판정한다.
        /// </summary>
        /// <param name="gap">둘 사이(m).</param>
        /// <param name="back">사람이 물러서는 거리(m).</param>
        public static string Stage(float gap = 2.6f, float back = 6.5f, bool lantern = true)
        {
            var 원본 = 생산자원본();
            if (원본 == null) return Err("no producer in scene");

            Teardown();

            var 가운데 = 설_자리(원본.transform.position);
            var 옆 = Vector3.right;

            _starvedGo = 세운다(원본, 가운데 - 옆 * (gap * 0.5f), "E2E_굶은개체");
            _fullGo = 세운다(원본, 가운데 + 옆 * (gap * 0.5f), "E2E_배부른개체");
            if (_starvedGo == null || _fullGo == null) return Err("clone failed");

            Starved = _starvedGo.GetComponent<CreatureFeeding>();
            Full = _fullGo.GetComponent<CreatureFeeding>();

            Starved.Prefill(0f);
            Full.Prefill(Full.Capacity);

            // 사람은 뒤로 물러서서 둘을 함께 본다. 눈높이는 몸통 높이에 맞춘다 —
            // 위에서 내려다보면 부푼 몸이 원근으로 상쇄되어 "커 보이지 않는다"가 된다.
            var 뒤 = 가운데 - Vector3.forward * back + Vector3.up * 1.2f;
            E2EHarness.Teleport(뒤);
            E2EHarness.SyncPhysics();

            _eye = 뒤;
            _aim = 가운데 + Vector3.up * 0.2f;
            눈을_못박는다(_eye, _aim);

            if (lantern) E2EHarness.LightLantern();
            else E2EHarness.DarkenLantern();

            var sb = new StringBuilder();
            sb.Append("{\"center\":\"").Append(가운데.ToString("F1")).Append('"')
              .Append(",\"starved\":\"").Append(_starvedGo.transform.position.ToString("F1")).Append('"')
              .Append(",\"full\":\"").Append(_fullGo.transform.position.ToString("F1")).Append('"')
              .Append(",\"capacity\":").Append(F(Full.Capacity))
              .Append(",\"eye\":\"").Append(뒤.ToString("F1")).Append('"')
              .Append(",\"lantern\":").Append(B(lantern))
              .Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// <b>카메라를 그 자리에 못 박는다.</b>
        ///
        /// <b>Cinemachine이 매 프레임 화각을 되돌린다</b>(<c>DepthFogService</c>가
        /// 원거리 평면에서 겪은 것과 같은 자리다). 사람의 시선을 옮기는 길
        /// (<see cref="E2EHarness.LookAt"/>)로 세우면 두 개체가 화면 위쪽 밖으로
        /// 밀려나 조각이 배경만 담는다 — 실측에서 사각형이 높이 1픽셀로 찌그러졌다.
        /// 재는 것도 찍는 것도 같은 구도라야 하므로 여기서 한 번에 못 박는다.
        /// </summary>
        static void 눈을_못박는다(Vector3 눈, Vector3 겨냥)
        {
            var cam = Camera.main;
            if (cam == null) return;

            _brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (_brain != null) _brain.enabled = false;

            cam.transform.position = 눈;
            cam.transform.rotation = Quaternion.LookRotation(겨냥 - 눈, Vector3.up);
        }

        static Unity.Cinemachine.CinemachineBrain _brain;
        static Vector3 _eye, _aim;

        /// <summary>
        /// 못 박은 자리를 다시 못 박는다. 사람이 스크린샷을 찍는 사이에도 무대가
        /// 흔들리지 않게 하는 문이고, <see cref="Measure"/>도 그리기 직전에 부른다.
        /// </summary>
        public static string Reaim()
        {
            if (Starved == null || Full == null) return Err("not staged");
            눈을_못박는다(_eye, _aim);
            var cam = Camera.main;
            return "{\"cam\":\"" + (cam != null ? cam.transform.position.ToString("F2") : "-") + "\"" +
                   ",\"vpStarved\":\"" + (cam != null
                       ? cam.WorldToViewportPoint(Starved.transform.position).ToString("F2") : "-") + "\"" +
                   ",\"vpFull\":\"" + (cam != null
                       ? cam.WorldToViewportPoint(Full.transform.position).ToString("F2") : "-") + "\"}";
        }

        /// <summary>랜턴만 켜고 끈다. 같은 무대에서 두 조건을 재기 위한 문이다.</summary>
        public static string SetLantern(bool on)
        {
            if (on) E2EHarness.LightLantern(); else E2EHarness.DarkenLantern();
            var lamp = E2EHarness.Lantern;
            return "{\"isOn\":" + B(lamp != null && lamp.IsOn) + "}";
        }

        /// <summary>
        /// 세워 둔 두 개체를 치운다. 무대를 남기면 다음 시나리오가 그것을 센다.
        /// <b>못 박아 둔 카메라도 여기서 푼다</b> — 안 풀면 다음 판이 굳은 화면으로 시작한다.
        /// </summary>
        public static string Teardown()
        {
            int 치움 = 0;
            if (_starvedGo != null) { Object.DestroyImmediate(_starvedGo); 치움++; }
            if (_fullGo != null) { Object.DestroyImmediate(_fullGo); 치움++; }
            _starvedGo = _fullGo = null;
            Starved = Full = null;

            if (_brain != null) { _brain.enabled = true; _brain = null; }
            return "{\"removed\":" + 치움 + "}";
        }

        // ── 재기 ────────────────────────────────────────────────

        /// <summary>
        /// 지금 화면에서 두 개체가 차지한 조각의 휘도를 읽는다.
        ///
        /// <c>mean</c>은 그 조각의 평균이고 <c>peak</c>는 가장 밝은 픽셀이다.
        /// <b>둘 다 필요하다</b> — 접합부처럼 좁은 자리만 빛나면 평균은 거의 안
        /// 움직이고 최고값만 뛴다. 평균만 보면 "안 밝아졌다"로, 최고값만 보면
        /// "밝아졌다"로 읽힌다.
        /// </summary>
        public static string Measure(int width = 960, int height = 540)
        {
            var cam = Camera.main;
            if (cam == null) return Err("no camera");
            if (Starved == null || Full == null) return Err("not staged");

            // 그리기 직전에 다시 못 박는다. 한 프레임이라도 놓치면 화각이 되돌아가
            // 조각이 배경만 담는다.
            눈을_못박는다(_eye, _aim);

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.sRGB);
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previousTarget = cam.targetTexture;
            var previousActive = RenderTexture.active;

            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply(false);
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
            }

            var 굶 = Patch(readback, cam, Starved, width, height);
            var 배 = Patch(readback, cam, Full, width, height);
            Object.DestroyImmediate(readback);

            var lamp = E2EHarness.Lantern;

            var sb = new StringBuilder();
            sb.Append("{\"lanternOn\":").Append(B(lamp != null && lamp.IsOn))
              .Append(",\"starved\":").Append(굶.Json())
              .Append(",\"full\":").Append(배.Json())
              .Append(",\"meanRatio\":").Append(F(비(배.Mean, 굶.Mean)))
              .Append(",\"peakRatio\":").Append(F(비(배.Peak, 굶.Peak)))
              .Append(",\"areaRatio\":").Append(F(비(배.Pixels, 굶.Pixels)))
              .Append('}');
            return sb.ToString();
        }

        /// <summary>0으로 나누지 않는다. 잴 수 없으면 0이라고 말한다.</summary>
        static float 비(float 위, float 아래) => 아래 <= 1e-6f ? 0f : 위 / 아래;

        readonly struct Reading
        {
            public readonly float Mean, Peak;
            public readonly Color Rgb;
            public readonly int Pixels;
            public readonly Rect Screen;

            public Reading(float mean, float peak, Color rgb, int pixels, Rect screen)
            {
                Mean = mean; Peak = peak; Rgb = rgb; Pixels = pixels; Screen = screen;
            }

            // 채널을 따로 적는 이유: 배부른 몸은 잿빛(0.35,0.36,0.40)에서
            // 연둣빛(0.55,0.95,0.70)으로 간다. 휘도 하나만 보면 "안 밝아졌다"와
            // "색이 안 바뀌었다"가 구별되지 않는다.
            public string Json() =>
                "{\"mean\":" + F(Mean) + ",\"v255\":" + F(Mean * 255f) +
                ",\"peak\":" + F(Peak) + ",\"peak255\":" + F(Peak * 255f) +
                ",\"r\":" + F(Rgb.r) + ",\"g\":" + F(Rgb.g) + ",\"b\":" + F(Rgb.b) +
                ",\"pixels\":" + Pixels +
                ",\"rect\":\"" + Screen.ToString() + "\"}";
        }

        /// <summary>
        /// 이 개체의 몸이 화면에서 차지한 사각형을 읽는다.
        ///
        /// <b>몸의 경계 상자를 화면으로 접어 쓴다.</b> 가운데 한 점만 찍으면 배부른
        /// 개체가 1.22배로 부푼 만큼 조각이 몸의 다른 자리에 걸리고, 그 차이가
        /// 밝기 차이로 둔갑한다. 사각형으로 재면 부푼 것은 <b>넓이</b>로 잡히고
        /// 밝아진 것은 <b>평균</b>으로 잡혀 둘이 갈린다.
        /// </summary>
        static Reading Patch(Texture2D tex, Camera cam, CreatureFeeding who, int width, int height)
        {
            var r = who.GetComponentInChildren<Renderer>();
            if (r == null) return new Reading(0f, 0f, Color.black, 0, new Rect());

            var b = r.bounds;
            // 몸이 옮겨진 직후라면 bounds가 아직 옛 자리다 (Physics.autoSyncTransforms=false).
            // 여기서는 렌더러를 읽으므로 물리 동기화와 무관하지만, 조각이 몸을 벗어나면
            // 배경만 담으므로 사각형 좌표를 함께 적어 사람이 확인할 수 있게 한다.
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var v = cam.WorldToViewportPoint(corner);
                if (v.z <= 0f) continue;
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            if (minX > maxX) return new Reading(0f, 0f, Color.black, 0, new Rect());

            int x0 = Mathf.Clamp(Mathf.FloorToInt(minX * width), 0, width - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX * width), 0, width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(minY * height), 0, height - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY * height), 0, height - 1);

            int w = Mathf.Max(1, x1 - x0 + 1);
            int h = Mathf.Max(1, y1 - y0 + 1);
            var pixels = tex.GetPixels(x0, y0, w, h);

            double sum = 0d, sr = 0d, sg = 0d, sb2 = 0d;
            float peak = 0f;
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                sum += lum; sr += c.r; sg += c.g; sb2 += c.b;
                if (lum > peak) peak = lum;
            }

            int n = Mathf.Max(1, pixels.Length);
            float mean = pixels.Length == 0 ? 0f : (float)(sum / n);
            var rgb = new Color((float)(sr / n), (float)(sg / n), (float)(sb2 / n));
            return new Reading(mean, peak, rgb, pixels.Length, new Rect(x0, y0, w, h));
        }

        // ── 무대 부품 ───────────────────────────────────────────

        /// <summary>
        /// 씬에 서 있는 생산자 하나. <b>같은 종을 둘로 복제</b>해야 기본 전리품 표가
        /// 같아지고, 같아야 차이가 축적량 하나로 좁혀진다.
        /// </summary>
        public static CreatureFeeding 생산자원본()
        {
            return Object.FindObjectsByType<CreatureFeeding>(FindObjectsInactive.Exclude)
                         .Where(f => f != null && !f.name.StartsWith("E2E_"))
                         .Where(f => f.GetComponent<CreatureHealth>()?.Definition != null)
                         .OrderByDescending(f => f.GetComponent<CreatureHealth>().Definition.id == "fruit_crab")
                         .FirstOrDefault();
        }

        /// <summary>
        /// 원본 곁의 <b>걸을 수 있는 평지</b>. 원본 자리에 겹쳐 세우면 복제본끼리
        /// 서로를 밀어내고, 밀려난 자리는 NavMesh 밖일 수 있다.
        /// </summary>
        static Vector3 설_자리(Vector3 곁)
        {
            for (int a = 0; a < 12; a++)
            {
                var dir = Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward;
                var 후보 = 곁 + dir * 5f;
                if (NavMesh.SamplePosition(후보, out var hit, 4f, NavMesh.AllAreas))
                    return hit.position;
            }
            return 곁;
        }

        /// <summary>
        /// 복제해 세운다. <b>두뇌를 끈다</b> — 재려는 것은 밝기와 산출이지 배회가
        /// 아니고, 걸어 나가 버리면 두 개체가 같은 프레임에 남지 않는다.
        /// </summary>
        static GameObject 세운다(CreatureFeeding 원본, Vector3 자리, string 이름)
        {
            if (NavMesh.SamplePosition(자리, out var hit, 4f, NavMesh.AllAreas)) 자리 = hit.position;

            var go = Object.Instantiate(원본.gameObject, 자리 + Vector3.up * 0.15f,
                                        Quaternion.Euler(0f, 180f, 0f));
            go.name = 이름;

            var brain = go.GetComponent<CreatureBrain>();
            if (brain != null) brain.enabled = false;

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;

            var flyer = go.GetComponent<FlyerMotor>();
            if (flyer != null) flyer.Stop();

            return go;
        }

        static string Err(string what) => "{\"error\":\"" + what + "\"}";
        static string B(bool v) => v ? "true" : "false";
        static string F(float v) => v.ToString("F4", CultureInfo.InvariantCulture);
    }
}
