using System.Text;
using UnityEngine;
using Survive.Art;
using Survive.Core;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// 후처리를 눈이 아니라 <b>픽셀 값</b>으로 확인하는 자다 (백로그 40).
    ///
    /// 스크린샷만으로는 "어두운 것 같다"밖에 말할 수 없다. 이 게임의 후처리에서
    /// 가장 중요한 단언은 "순수 어둠이 들리지 않았다"인데, 그것은 0.02와 0.05를
    /// 구별해야 하는 이야기라 사람 눈으로는 판정이 안 된다. 그래서 카메라를
    /// 렌더 텍스처에 그려서 암부 픽셀의 분포를 직접 센다.
    ///
    /// PlayMode에서 uloop의 execute-dynamic-code가 한 줄로 부르는 용도다.
    /// </summary>
    public static class PostFxProbe
    {
        /// <summary>이 밝기 아래를 "순수 어둠"으로 센다.</summary>
        public const float DarkThreshold = 0.02f;

        /// <summary>
        /// 지금 화면의 밝기 분포. 평균·중앙값·암부 비율을 JSON 한 줄로 돌려준다.
        ///
        /// 후처리를 켠 화면을 그대로 읽으므로 톤매핑·블룸·그레인이 전부 반영된
        /// 최종 픽셀이다. 감마 슬라이더를 옮긴 뒤 이 값이 실제로 움직이는지가
        /// 백로그 42의 게이트다.
        /// </summary>
        public static string Sample(int width = 640, int height = 360)
        {
            var cam = Camera.main;
            if (cam == null) return "{\"error\":\"no camera\"}";

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.sRGB);
            var previousTarget = cam.targetTexture;
            var previousActive = RenderTexture.active;

            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
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

            var pixels = readback.GetPixels();
            Object.Destroy(readback);

            double sum = 0d;
            float min = 1f, max = 0f;
            int dark = 0;
            var histogram = new int[256];

            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                sum += lum;
                if (lum < min) min = lum;
                if (lum > max) max = lum;
                if (lum < DarkThreshold) dark++;
                histogram[Mathf.Clamp(Mathf.RoundToInt(lum * 255f), 0, 255)]++;
            }

            float mean = pixels.Length == 0 ? 0f : (float)(sum / pixels.Length);
            float median = Percentile(histogram, pixels.Length, 0.5f);
            float p01 = Percentile(histogram, pixels.Length, 0.01f);
            float darkRatio = pixels.Length == 0 ? 0f : (float)dark / pixels.Length;

            var sb = new StringBuilder();
            sb.Append("{\"mean\":").Append(mean.ToString("F5"))
              .Append(",\"median\":").Append(median.ToString("F5"))
              .Append(",\"p01\":").Append(p01.ToString("F5"))
              .Append(",\"min\":").Append(min.ToString("F5"))
              .Append(",\"max\":").Append(max.ToString("F5"))
              .Append(",\"darkRatio\":").Append(darkRatio.ToString("F5"))
              .Append(",\"pixels\":").Append(pixels.Length)
              .Append('}');
            return sb.ToString();
        }

        static float Percentile(int[] histogram, int total, float fraction)
        {
            if (total <= 0) return 0f;
            int target = Mathf.Max(1, Mathf.RoundToInt(total * fraction));
            int running = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                running += histogram[i];
                if (running >= target) return i / 255f;
            }
            return 1f;
        }

        /// <summary>
        /// 화면을 특정 상태로 세운다. 상태별 스크린샷 세트를 사람 손 없이
        /// 같은 구도로 찍기 위한 것이다.
        /// </summary>
        public static string Stage(float x, float y, float z, float yaw,
                                   bool lantern, bool tool)
        {
            GammaCalibrationScreen.AutoShow = false;
            GammaCalibrationScreen.Close();
            GammaSettings.ForceNeutral = true;

            if (!GameServices.TryGet<PlayerVitals>(out var vitals) || vitals == null)
                return "{\"error\":\"no player\"}";

            var root = vitals.transform.root;
            var controller = root.GetComponentInChildren<CharacterController>();
            if (controller != null) controller.enabled = false;
            root.position = new Vector3(x, y, z);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (controller != null) controller.enabled = true;

            if (GameServices.TryGet<LanternController>(out var lamp) && lamp != null)
            {
                // 배터리를 먼저 채운다. 안 그러면 재 두는 동안(초당 1.6) 배터리가
                // 바닥나서 램프가 꺼지고, "켠 상태"라고 믿은 스크린샷이 실은
                // 꺼진 화면이 된다 — 실제로 한 번 그렇게 속았다.
                lamp.Recharge(99999f);
                lamp.SetOn(lantern);
            }

            var pickaxe = FindChild(root, "pickaxe01");
            if (pickaxe != null) pickaxe.gameObject.SetActive(tool);

            return "{\"pos\":\"" + root.position.ToString("F2") + "\",\"lantern\":" +
                   (lantern ? "true" : "false") + ",\"tool\":" + (tool ? "true" : "false") + "}";
        }

        /// <summary>
        /// 손에 든 도구가 화면에 들어오도록 세운다 (백로그 41의 전후 비교용).
        ///
        /// 1인칭이라 도구는 평소 화면 밖에 있다 — 광맥을 캘 때처럼 아래를 볼 때만
        /// 오른쪽 아래로 들어온다. 그 순간을 손으로 재현하면 구도가 매번 달라져서
        /// 전후를 비교할 수 없으므로, 카메라를 고정 각도로 못 박고 손 소켓을
        /// 내리친 자세로 세운다. Cinemachine은 매 프레임 카메라를 덮어쓰므로 끈다.
        /// </summary>
        public static string StageToolShot(float pitch = 58f, float yaw = 22f)
        {
            var cam = Camera.main;
            if (cam == null) return "{\"error\":\"no camera\"}";

            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;

            if (!GameServices.TryGet<PlayerVitals>(out var vitals) || vitals == null)
                return "{\"error\":\"no player\"}";

            var socket = FindChild(vitals.transform.root, "jointItemR");
            if (socket == null) return "{\"error\":\"no socket\"}";

            socket.localRotation = _socketRest == default ? socket.localRotation : _socketRest;
            _socketRest = socket.localRotation;
            socket.localRotation *= Quaternion.Euler(48f, -14f, 0f);   // ToolSwingAnimator의 내리친 각도

            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            var renderer = socket.GetComponentInChildren<Renderer>();
            var viewport = renderer != null ? cam.WorldToViewportPoint(renderer.bounds.center) : Vector3.zero;
            return "{\"viewport\":\"" + viewport.ToString("F2") + "\"}";
        }

        static Quaternion _socketRest;

        /// <summary>램프를 플레이어 기준 어디에 둘지 (전후 비교에서 자리만 바꿔 재기 위한 것).</summary>
        public static string MoveLamp(float x, float y, float z)
        {
            if (!GameServices.TryGet<LanternController>(out var lamp) || lamp == null)
                return "{\"error\":\"no lantern\"}";

            // 램프는 LanternController의 자식이 아니다(백로그 41에서 몸 바깥으로
            // 옮겼다). 이름으로 플레이어 전체에서 찾는다.
            var root = lamp.transform.root;
            var anchor = FindChild(root, "LampLight");
            var light = anchor != null ? anchor.GetComponent<Light>() : null;
            if (light == null) return "{\"error\":\"no light\"}";
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(x, y, z);
            return "{\"world\":\"" + light.transform.position.ToString("F3") + "\",\"on\":" +
                   (light.enabled ? "true" : "false") + "}";
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
