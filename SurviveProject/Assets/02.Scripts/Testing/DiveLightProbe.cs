using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using Survive.Core;
using Survive.Items;
using Survive.Player;
using Survive.Vitals;
using Survive.World;

namespace Survive.Testing
{
    /// <summary>
    /// <b>랜턴이 몇 미터까지 실제로 보이게 하는가</b>를 픽셀로 재는 자.
    ///
    /// 스크린샷으로는 "물속이 어둡다"까지만 말할 수 있다. 그런데 우리가 답해야
    /// 하는 물음은 둘이고 둘 다 숫자다 — <b>반경 안이 보이는가</b>와
    /// <b>반경 밖이 여전히 새까만가</b>. 뒤엣것이 빠지면 "밝게 만들었다"와
    /// "빛이 어둠을 이기는 반경을 확보했다"가 구별되지 않는다.
    ///
    /// 재는 방법은 <b>자를 세우는 것</b>이다. 카메라 정면 d미터에 회색 판을 하나
    /// 세우고 그 판이 화면에 얼마나 밝게 찍히는지를 읽는다. 세계의 지형을 재면
    /// 물속에는 애초에 8m 안에 벽이 없는 자리가 많아 "빛이 약하다"와 "비출 것이
    /// 없다"가 섞인다. 판은 늘 같은 회색이므로 섞이지 않는다.
    ///
    /// 판은 씬의 다른 물체와 똑같은 URP/Lit로 그린다 — 안개도 광원도 그림자도
    /// 세계와 같은 경로를 지나야 여기서 잰 값이 화면과 같은 말을 한다.
    /// </summary>
    public static class DiveLightProbe
    {
        /// <summary>시험판의 반사율. 중간 회색이라 밝기가 곧 도달한 빛의 양이 된다.</summary>
        const float TargetAlbedo = 0.5f;

        static Shader _lit;
        static Shader Lit
        {
            get
            {
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null) _lit = Shader.Find("Standard");
                return _lit;
            }
        }

        // ── 한 번에 네 줄 재기 ──────────────────────────────────
        //
        // 물속/뭍 × 랜턴 켬/끔 네 줄을 한 호출에 잰다. 나누어 재면 그 사이에
        // 바다가 몸을 깎아 되살아나고(LiquidContactService), 되살아나면 자리가
        // 처음으로 돌아가서 네 줄이 서로 다른 자리의 값이 된다 — 실제로 한 번
        // 그렇게 잃었다.

        /// <summary>가장 최근 보고. 코루틴이 끝나면 여기 앉는다.</summary>
        public static string LastReport { get; private set; } = "";

        /// <summary>아직 도는 중인가. 폴링하는 쪽이 본다.</summary>
        public static bool Running { get; private set; }

        /// <summary>네 줄 재기를 시작한다. 결과는 <see cref="LastReport"/>로 온다.</summary>
        public static string Begin(string distances = "2,4,6,8,10,12,16,24,32")
        {
            if (Running) return "{\"error\":\"already running\"}";

            var go = new GameObject("DiveLightProbeRunner") { hideFlags = HideFlags.HideAndDontSave };
            var host = go.AddComponent<Host>();
            Running = true;
            LastReport = "";
            host.StartCoroutine(Report(distances, go));
            return "{\"started\":true}";
        }

        class Host : MonoBehaviour { }

        static IEnumerator Report(string distances, GameObject host)
        {
            var sb = new StringBuilder();
            sb.Append("{\"rows\":[");
            bool first = true;

            GrantLantern();

            // 물속 → 뭍 순서다. 물속이 목적이고 뭍은 견줄 값이라, 먼저 재야
            // 물속 값이 앞선 이동의 잔상을 안 받는다.
            foreach (var wet in new[] { true, false })
            {
                yield return Move(wet);

                foreach (bool on in new[] { true, false })
                {
                    SetLantern(on);
                    // 스위치가 램프에 닿고 한 프레임 그려질 때까지 기다린다.
                    for (int i = 0; i < 3; i++)
                    {
                        if (wet) Submerge(); else Surface();
                        KeepAlive();
                        yield return null;
                    }

                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"wet\":").Append(B(wet)).Append(",\"lantern\":").Append(B(on))
                      .Append(",\"frame\":").Append(Frame())
                      .Append(",\"sweep\":").Append(Sweep(distances))
                      .Append('}');
                }
            }

            sb.Append("]}");
            LastReport = sb.ToString();
            Running = false;
            Object.Destroy(host);
        }

        static IEnumerator Move(bool wet)
        {
            // 안개는 0.35초에 걸쳐 차오른다(UnderwaterView.blendSeconds).
            // 다 차기 전에 재면 물속을 물속이 아닌 값으로 적게 된다.
            // 그동안 몸은 떠오르므로 매 프레임 다시 눌러 둔다.
            for (int i = 0; i < 40; i++)
            {
                if (wet) Submerge(); else Surface();
                KeepAlive();
                yield return null;
            }
        }

        /// <summary>
        /// 물속에 붙들어 둔다. 시나리오가 매 프레임 부르는 창구다 —
        /// 몸은 떠오르고 바다는 살을 깎으므로, 손을 떼면 몇 초 뒤에는
        /// 물속을 잰다고 믿은 값이 뭍의 값이 된다.
        /// </summary>
        public static void PinUnderwater()
        {
            Submerge();
            KeepAlive();
        }

        /// <summary>
        /// 바다는 초당 살을 깎는다(백로그 32). 재는 동안 죽으면 부활 지점으로
        /// 끌려가 네 줄이 서로 다른 자리의 값이 된다.
        /// </summary>
        static void KeepAlive()
        {
            if (GameServices.TryGet<PlayerVitals>(out var v) && v != null &&
                v.Health.Normalized < 0.9f) v.Revive();
        }

        static void GrantLantern()
        {
            var player = E2EHarness.Player;
            var pack = player != null ? player.GetComponentInChildren<PlayerInventory>(true) : null;
            if (pack?.Inventory == null || pack.Database == null) return;
            if (pack.Inventory.CountOf(LanternRule.ItemId) > 0) return;

            var def = pack.Database.GetById(LanternRule.ItemId);
            if (def != null) pack.Inventory.TryAdd(def, 1);
        }

        // ── 상태 만들기 ─────────────────────────────────────────

        // 한 번 고른 자리를 계속 쓴다. 잴 때마다 다시 고르면 물속 네 줄과
        // 스크린샷 두 장이 서로 다른 자리의 값이 되어 비교가 성립하지 않는다.
        static Vector3 _spot;
        static bool _hasSpot;
        static float _spotDepth;

        /// <summary>고른 자리를 버린다. 다음 잠수가 다시 찾는다.</summary>
        public static void Reset() => _hasSpot = false;

        /// <summary>
        /// 물속 깊은 자리로 몸을 옮긴다. 머리가 잠겨야 <see cref="UnderwaterView"/>가
        /// 물속 안개로 갈아 끼우므로, 발이 아니라 <b>머리</b>가 기준이다.
        ///
        /// <b>몸은 저절로 떠오른다.</b> 한 번 옮겨 놓고 손을 떼면 몇 초 뒤에
        /// 수면 위라, 물속을 잰다고 믿은 값이 뭍의 값이 된다. 그래서 재는 동안
        /// 매 프레임 이 함수를 다시 부른다(<see cref="Hold"/>).
        /// </summary>
        public static string Submerge(float scan = 90f, float clearance = 2.0f)
        {
            var player = E2EHarness.Player;
            if (player == null) return Err("no player");

            if (!E2EWaterProbe.TryFindSurface(out float surface, out _)) return Err("no water");

            if (!_hasSpot)
            {
                if (!E2EWaterProbe.TryFindDeepest(player.transform.position, surface, scan,
                                                  out var bed, out float depth))
                    return Err("no deep spot");

                // 바닥에서 조금 띄우되 머리는 확실히 수면 아래로.
                _spot = new Vector3(bed.x, Mathf.Min(bed.y + clearance, surface - 2.0f), bed.z);
                _spotDepth = depth;
                _hasSpot = true;
            }

            E2EHarness.Teleport(_spot);

            var swim = player.GetComponentInChildren<PlayerSwimming>(true);
            return "{\"surfaceY\":" + F(surface) + ",\"depth\":" + F(_spotDepth) +
                   ",\"pos\":\"" + player.transform.position.ToString("F2") + "\"" +
                   ",\"headSubmerged\":" + B(swim != null && swim.IsHeadSubmerged) + "}";
        }

        /// <summary>
        /// 사람 손이 스크린샷을 찍을 동안 상태를 붙들어 둔다.
        /// 몸은 떠오르고 바다는 살을 깎으므로, 매 프레임 눌러 두지 않으면
        /// 셔터를 누른 순간에는 이미 다른 화면이다.
        /// </summary>
        public static string Hold(bool lanternOn, float seconds = 60f)
        {
            if (Running) return "{\"error\":\"already running\"}";

            var go = new GameObject("DiveLightProbeHold") { hideFlags = HideFlags.HideAndDontSave };
            var host = go.AddComponent<Host>();
            Running = true;
            host.StartCoroutine(HoldRoutine(lanternOn, seconds, go));
            return "{\"holding\":true,\"lantern\":" + B(lanternOn) + "}";
        }

        static IEnumerator HoldRoutine(bool lanternOn, float seconds, GameObject host)
        {
            GrantLantern();
            Submerge();
            yield return null;

            // 겨냥은 한 번만 정하고 붙든다. 매 프레임 "앞쪽 아래"를 다시 재면
            // 겨냥이 제 꼬리를 물고 돌아 두 장의 구도가 달라진다.
            var player = E2EHarness.Player;
            Vector3 aim = _spot + (player != null ? player.transform.forward : Vector3.forward) * 6f
                                + Vector3.down * 2.5f;

            float end = Time.unscaledTime + seconds;
            while (Running && Time.unscaledTime < end)
            {
                Submerge();
                KeepAlive();
                SetLantern(lanternOn);
                E2EHarness.LookAt(aim);
                yield return null;
            }
            Running = false;
            Object.Destroy(host);
        }

        /// <summary>지금 붙들고 있는 자리를 그만 붙든다.</summary>
        public static string Release()
        {
            Running = false;
            return "{\"released\":true}";
        }

        /// <summary>
        /// 물 밖 같은 조건으로 되돌린다 — 같은 자리에서 위로만 올린다.
        /// 자리를 바꾸면 지형이 달라져 뭍/물속 비교가 성립하지 않는다.
        /// </summary>
        public static string Surface(float above = 6f)
        {
            var player = E2EHarness.Player;
            if (player == null) return Err("no player");
            if (!E2EWaterProbe.TryFindSurface(out float surface, out _)) return Err("no water");

            var p = player.transform.position;
            E2EHarness.Teleport(new Vector3(p.x, surface + above, p.z));

            var swim = player.GetComponentInChildren<PlayerSwimming>(true);
            return "{\"pos\":\"" + player.transform.position.ToString("F2") + "\"" +
                   ",\"headSubmerged\":" + B(swim != null && swim.IsHeadSubmerged) + "}";
        }

        /// <summary>
        /// 랜턴을 켜거나 끈다. <b>스위치로</b> 끈다 — 배터리를 태우는 길
        /// (<see cref="E2EHarness.DarkenLantern"/>)과 달리 눈금이 남고 배터리가
        /// 멈춘다. 재려는 것이 "불이 없을 때의 화면"이므로 어느 쪽이든 되지만,
        /// 스위치 쪽이 왕복이 되어 같은 자리에서 두 장을 찍을 수 있다.
        /// </summary>
        public static string SetLantern(bool on)
        {
            if (!GameServices.TryGet<LanternController>(out var lamp) || lamp == null)
                return Err("no lantern");

            // 재는 동안 배터리가 바닥나면 "켠 화면"이라고 믿은 값이 실은 꺼진 화면이다.
            // 실제로 한 번 그렇게 속았다(PostFxProbe.Stage의 같은 주석).
            if (on) lamp.Recharge(LanternRule.MaxBattery * 10f);
            lamp.SetSwitch(on);

            return "{\"tier\":" + lamp.Tier + ",\"battery\":" + F(lamp.Battery) +
                   ",\"switch\":" + B(lamp.SwitchedOn) + ",\"isOn\":" + B(lamp.IsOn) + "}";
        }

        // ── 재기 ────────────────────────────────────────────────

        /// <summary>
        /// 카메라 정면 여러 거리에 시험판을 세워 각각의 화면 밝기를 잰다.
        ///
        /// 돌려주는 <c>lum</c>은 0~1로 정규화한 휘도이고, <c>v255</c>는 그것을
        /// 8비트로 옮긴 값이다 — 보고에 적을 단위가 이쪽이다.
        /// </summary>
        public static string Sweep(string distances = "2,4,6,8,10,12,16,24,32",
                                   int width = 480, int height = 270, int patch = 40)
        {
            var cam = Camera.main;
            if (cam == null) return Err("no camera");

            var parts = distances.Split(',');
            var sb = new StringBuilder();
            sb.Append('{');
            AppendContext(sb, cam);
            sb.Append(",\"samples\":[");

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.sRGB);
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previousTarget = cam.targetTexture;
            var previousActive = RenderTexture.active;

            try
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!float.TryParse(parts[i].Trim(), NumberStyles.Float,
                                        CultureInfo.InvariantCulture, out float d)) continue;

                    var board = BuildBoard(cam, d);
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
                        Object.DestroyImmediate(board);
                    }

                    float lum = CenterLuminance(readback, width, height, patch);
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"d\":").Append(F(d))
                      .Append(",\"lum\":").Append(lum.ToString("F5", CultureInfo.InvariantCulture))
                      .Append(",\"v255\":").Append((lum * 255f).ToString("F2", CultureInfo.InvariantCulture))
                      .Append('}');
                }
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(readback);
                RenderTexture.ReleaseTemporary(rt);
            }

            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// 시험판 없이 지금 화면 자체를 잰다. <b>반경 밖이 새까만가</b>를 묻는 쪽이다 —
        /// 판을 세우면 그 판이 곧 반경 밖의 물체가 되어 버리므로, 세계를 그대로 보는
        /// 값도 함께 있어야 "밝게 만들었다"와 갈린다.
        /// </summary>
        public static string Frame(int width = 480, int height = 270) =>
            PostFxProbe.Sample(width, height);

        static void AppendContext(StringBuilder sb, Camera cam)
        {
            var swim = E2EHarness.Player != null
                ? E2EHarness.Player.GetComponentInChildren<PlayerSwimming>(true) : null;
            GameServices.TryGet<LanternController>(out var lamp);
            var lampLight = lamp != null ? lamp.Lamp : null;

            sb.Append("\"submerged\":").Append(B(swim != null && swim.IsHeadSubmerged))
              .Append(",\"fogDensity\":").Append(F(RenderSettings.fogDensity))
              .Append(",\"fogColor\":\"").Append(RenderSettings.fogColor.ToString("F3")).Append('"')
              .Append(",\"lanternOn\":").Append(B(lamp != null && lamp.IsOn))
              .Append(",\"lampEnabled\":").Append(B(lampLight != null && lampLight.enabled))
              .Append(",\"lampRange\":").Append(F(lampLight != null ? lampLight.range : 0f))
              .Append(",\"lampIntensity\":").Append(F(lampLight != null ? lampLight.intensity : 0f))
              .Append(",\"lampToCam\":").Append(F(lampLight != null && cam != null
                        ? Vector3.Distance(lampLight.transform.position, cam.transform.position) : -1f));
        }

        /// <summary>정면 d미터에 카메라를 마주 보고 서는 회색 판. 화면을 다 덮을 만큼 크게 세운다.</summary>
        static GameObject BuildBoard(Camera cam, float d)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "DiveLightProbeBoard";
            go.hideFlags = HideFlags.HideAndDontSave;

            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);   // 물리에 끼어들면 안 된다

            var t = cam.transform;
            go.transform.position = t.position + t.forward * d;

            // Quad의 법선은 로컬 -Z다. 카메라 쪽을 향하게 하려면 로컬 +Z를
            // 카메라가 보는 쪽으로 돌려야 한다. 반대로 두면 판이 보이기는 하되
            // <b>법선이 램프 반대쪽</b>이라 빛을 하나도 안 받는다 —
            // 실제로 그렇게 재고 "빛이 4m를 못 간다"로 잘못 읽었다.
            go.transform.rotation = Quaternion.LookRotation(t.forward, t.up);

            float halfH = d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float h = halfH * 2f * 1.4f;
            go.transform.localScale = new Vector3(h * Mathf.Max(1f, cam.aspect), h, 1f);

            var mat = new Material(Lit) { hideFlags = HideFlags.HideAndDontSave };

            // 양면으로 그린다. Quad의 앞면이 어느 쪽인지에 따라 판이 통째로
            // 사라지는 것을 실제로 한 번 겪었다 — 그러면 거리와 무관하게 늘
            // 같은 값이 나와서 "빛이 안 닿는다"로 잘못 읽힌다.
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.doubleSidedGI = true;

            var gray = new Color(TargetAlbedo, TargetAlbedo, TargetAlbedo, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gray);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", gray);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return go;
        }

        static float CenterLuminance(Texture2D tex, int width, int height, int patch)
        {
            int half = Mathf.Max(1, patch / 2);
            int x0 = Mathf.Clamp(width / 2 - half, 0, width - 1);
            int y0 = Mathf.Clamp(height / 2 - half, 0, height - 1);
            int w = Mathf.Min(patch, width - x0);
            int h = Mathf.Min(patch, height - y0);

            var pixels = tex.GetPixels(x0, y0, w, h);
            double sum = 0d;
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                sum += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            }
            return pixels.Length == 0 ? 0f : (float)(sum / pixels.Length);
        }

        static string Err(string what) => "{\"error\":\"" + what + "\"}";
        static string B(bool v) => v ? "true" : "false";
        static string F(float v) => v.ToString("F4", CultureInfo.InvariantCulture);
    }
}
