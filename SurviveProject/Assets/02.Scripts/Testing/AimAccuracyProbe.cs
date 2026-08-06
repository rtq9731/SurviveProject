using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Survive.Interaction;

namespace Survive.Testing
{
    /// <summary>
    /// "겨눈 것과 잡힌 것이 같은가"를 씬 전체에서 세어 본다.
    ///
    /// 조준은 눈으로 보면 언제나 그럴듯하다. 사용자가 적은 증상("바라보는 물체와
    /// 자막의 물체가 다르다")도 몇 번에 한 번씩만 나므로, 몇 자리 서 보고 고쳤다고
    /// 말할 수 없다. 그래서 씬의 상호작용 대상 하나하나를 여러 방향·여러 거리에서
    /// 겨눠 보고 <b>수를 센다</b>.
    ///
    /// 옛 규칙("맞은 것 중 가장 가까운 것")도 여기서 같이 돌려 견준다.
    /// 코드를 되돌려 두 번 재는 것보다 한 번에 나란히 두는 편이 정직하다 —
    /// 씬도 표본도 완전히 같기 때문이다.
    ///
    /// 플레이 모드가 아니어도 돈다. 콜라이더만 있으면 되는 계산이다.
    /// </summary>
    public static class AimAccuracyProbe
    {
        /// <summary>서 보는 거리(m). 사람이 실제로 채집하는 간격이다.</summary>
        static readonly float[] Distances = { 1.2f, 2.2f };

        /// <summary>대상을 둘러싸고 서 보는 방향(도).</summary>
        static readonly float[] Yaws = { 0f, 90f, 180f, 270f };

        /// <summary>대상을 똑바로 본 뒤 시선을 이만큼 돌려도 본다(도). 0은 똑바로 본 것.</summary>
        static readonly float[] Offsets = { 0f, 40f, -40f };

        /// <summary>눈높이(m).</summary>
        const float EyeHeight = 1.6f;

        /// <summary>손이 닿는 거리(m). 값을 바꿔 가며 재 보라고 상수가 아니다.</summary>
        public static float MaxDistance = 3f;

        /// <summary>시선에서 봐주는 폭(m). 값을 바꿔 가며 재 보라고 상수가 아니다.</summary>
        public static float Radius = 0.2f;

        struct Known
        {
            public Transform Root;
            public IInteractable Target;
            public Bounds Body;
        }

        class Tally
        {
            public int 표본;
            public int 맞음;          // 잡힌 것 == 겨눈 것 (둘 다 없는 경우도 일치로 센다)
            public int 엉뚱한것;      // 무언가 잡혔는데 겨눈 것이 아니다 ← 사용자가 겪은 증상
            public int 놓침;          // 겨눈 것이 있는데 아무것도 안 잡혔다
            public int 가림뚫음;      // 가려져 보이지 않는데 잡혔다

            public float 정확도 => 표본 == 0 ? 0f : 100f * 맞음 / 표본;
        }

        public static string Run()
        {
            var known = Collect();

            var player = Object.FindAnyObjectByType<CharacterController>(FindObjectsInactive.Exclude);
            Transform self = player != null ? player.transform : null;

            var query = new AimQuery
            {
                MaxDistance = MaxDistance,
                Radius = Radius,
                Mask = ~0,
                Self = self,
            };

            var 똑바로_옛 = new Tally();
            var 똑바로_새 = new Tally();
            var 비껴_옛 = new Tally();
            var 비껴_새 = new Tally();
            var 나빠진것 = new List<string>();

            foreach (var it in known)
            {
                int 놓친수 = 0;
                int 자리수 = 0;

                foreach (float distance in Distances)
                foreach (float yaw in Yaws)
                {
                    Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
                    Vector3 stand = it.Body.center + dir * distance;
                    if (!TryGround(stand, out float groundY)) continue;

                    var eye = new Vector3(stand.x, groundY + EyeHeight, stand.z);
                    Vector3 toTarget = it.Body.center - eye;
                    if (toTarget.sqrMagnitude < 0.0001f) continue;
                    toTarget.Normalize();

                    foreach (float offset in Offsets)
                    {
                        Vector3 forward = Quaternion.AngleAxis(offset, Vector3.up) * toTarget;

                        IInteractable 진실 = Truth(known, eye, forward, self);
                        IInteractable 옛것 = OldPick(eye, forward, self);
                        query.TryPick(eye, forward, out IInteractable 새것, out _, out _);

                        bool 똑바로 = Mathf.Approximately(offset, 0f);
                        Count(똑바로 ? 똑바로_옛 : 비껴_옛, 진실, 옛것);
                        Count(똑바로 ? 똑바로_새 : 비껴_새, 진실, 새것);

                        if (!똑바로) continue;
                        자리수++;
                        if (ReferenceEquals(진실, it.Target) && !ReferenceEquals(새것, it.Target))
                            놓친수++;
                    }
                }

                // 똑바로 겨눴는데도 새 규칙이 절반 넘게 놓치는 대상은 이름을 남긴다.
                // 판정이 과해진 신호이므로 하네스를 고칠 것이 아니라 이쪽을 의심해야 한다.
                if (자리수 > 0 && 놓친수 * 2 > 자리수)
                    나빠진것.Add($"{it.Root.name} ({놓친수}/{자리수})");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[조준 실측] 상호작용 대상 {known.Count}개, " +
                          $"자리 {Distances.Length}x{Yaws.Length}, 시선 {Offsets.Length}갈래");
            sb.AppendLine();
            sb.AppendLine($"똑바로 겨눴을 때 (표본 {똑바로_옛.표본})");
            sb.AppendLine(Line("  옛 규칙", 똑바로_옛));
            sb.AppendLine(Line("  새 규칙", 똑바로_새));
            sb.AppendLine();
            sb.AppendLine($"시선을 40도 비껴 놓았을 때 (표본 {비껴_옛.표본})");
            sb.AppendLine(Line("  옛 규칙", 비껴_옛));
            sb.AppendLine(Line("  새 규칙", 비껴_새));
            sb.AppendLine();

            if (나빠진것.Count == 0)
                sb.AppendLine("똑바로 겨눠도 절반 넘게 놓치는 대상: 없음");
            else
            {
                sb.AppendLine($"똑바로 겨눠도 절반 넘게 놓치는 대상 {나빠진것.Count}개:");
                foreach (var n in 나빠진것) sb.AppendLine("  " + n);
            }

            return sb.ToString();
        }

        static string Line(string 이름, Tally t) =>
            $"{이름}: 일치 {t.맞음}/{t.표본} ({t.정확도:0.0}%)" +
            $"  엉뚱한 것 {t.엉뚱한것}  놓침 {t.놓침}  가림 뚫음 {t.가림뚫음}";

        static void Count(Tally t, IInteractable 진실, IInteractable 잡힌것)
        {
            t.표본++;
            if (ReferenceEquals(진실, 잡힌것)) { t.맞음++; return; }
            if (잡힌것 == null) { t.놓침++; return; }

            t.엉뚱한것++;
            if (진실 == null) t.가림뚫음++;
        }

        /// <summary>씬의 상호작용 대상과 그 몸. 한 번만 재고 계속 쓴다.</summary>
        static List<Known> Collect()
        {
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                .Where(m => m is IInteractable)
                .GroupBy(m => m.transform)
                .Select(g => g.First())
                .Select(m => new Known
                {
                    Root = m.transform,
                    Target = (IInteractable)m,
                    Body = AimQuery.BodyBounds(m.transform),
                })
                .ToList();
        }

        /// <summary>
        /// 이 시선이 <b>정말로</b> 가리키는 상호작용 대상.
        ///
        /// 판정 규칙과 겹치지 않게 따로 세운다 — 규칙으로 규칙을 채점하면 언제나 만점이다.
        /// 여기서는 너그러움(반경 0.3m)도, 순위도 쓰지 않는다. 화면 한가운데를 지나는
        /// <b>가느다란 선 하나</b>가 무엇에 먼저 닿는가만 본다.
        /// <list type="number">
        /// <item>3m 안에서 시선이 어느 대상의 몸 상자에 가장 먼저 들어가는가.</item>
        /// <item>그보다 앞에 상호작용 대상이 아닌 단단한 것(지형·바위)이 있으면 가려진 것이다.</item>
        /// </list>
        /// </summary>
        static IInteractable Truth(List<Known> known, Vector3 eye, Vector3 forward, Transform self)
        {
            var ray = new Ray(eye, forward);

            // 시선을 가로막는 첫 번째 단단한 것
            float barrier = MaxDistance;
            IInteractable barrierTarget = null;
            var solid = Physics.RaycastAll(eye, forward, MaxDistance, ~0,
                                           QueryTriggerInteraction.Ignore);
            foreach (var hit in solid)
            {
                if (self != null && (hit.collider.transform == self ||
                                     hit.collider.transform.IsChildOf(self))) continue;
                if (hit.distance >= barrier) continue;
                barrier = hit.distance;
                barrierTarget = hit.collider.GetComponentInParent<IInteractable>();
            }

            IInteractable best = null;
            float bestEnter = float.MaxValue;
            foreach (var it in known)
            {
                // 눈이 그 상자 안에 있으면 IntersectRay가 0을 준다. 그대로 두면
                // 21m짜리 거대 버섯이 갓 아래 어디서나 "겨눈 것"이 되어 채점이 무너진다.
                // 그런 대상은 단단한 표면에 실제로 닿았을 때만(위의 barrierTarget) 인정한다.
                if (it.Body.Contains(eye)) continue;
                if (!it.Body.IntersectRay(ray, out float enter)) continue;
                if (enter > MaxDistance || enter >= bestEnter) continue;
                bestEnter = enter;
                best = it.Target;
            }

            if (best != null && bestEnter <= barrier + AimQuery.SurfaceSlack) return best;
            return barrierTarget;
        }

        /// <summary>
        /// 고치기 전의 규칙. 맞은 것 중 가장 가까운 것을 고르고, 몸이 그 안에 들어간
        /// 것(거리 0)은 차선책으로 내렸다. 가림 검사는 없었다.
        /// </summary>
        static IInteractable OldPick(Vector3 origin, Vector3 forward, Transform self)
        {
            var hits = Physics.SphereCastAll(origin, Radius, forward, MaxDistance, ~0,
                                             QueryTriggerInteraction.Collide);

            IInteractable found = null;
            IInteractable enclosing = null;
            float nearest = float.MaxValue;

            foreach (var hit in hits)
            {
                if (self != null && (hit.collider.transform == self ||
                                     hit.collider.transform.IsChildOf(self))) continue;

                var candidate = hit.collider.GetComponentInParent<IInteractable>();
                if (candidate == null) continue;

                if (hit.distance <= 0f)
                {
                    if (enclosing == null) enclosing = candidate;
                    continue;
                }

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    found = candidate;
                }
            }

            return found ?? enclosing;
        }

        static bool TryGround(Vector3 at, out float y)
        {
            y = 0f;
            if (!Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out var hit, 200f, ~0,
                                 QueryTriggerInteraction.Ignore))
                return false;
            y = hit.point.y;
            return true;
        }
    }
}
