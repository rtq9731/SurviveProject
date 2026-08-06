using System.Collections.Generic;
using UnityEngine;

namespace Survive.Interaction
{
    /// <summary>후보가 왜 탈락했는가. 통과한 것만 <see cref="AimReject.None"/>이다.</summary>
    public enum AimReject
    {
        None = 0,
        /// <summary>자기 몸. 카메라가 몸 안에 있어 언제나 후보로 잡힌다.</summary>
        Self,
        /// <summary>겨냥점이 등 뒤에 있다.</summary>
        Behind,
        /// <summary>손이 닿는 거리 밖이다.</summary>
        TooFar,
        /// <summary>시선에서 옆으로 너무 벗어났다. 겨눈 것이 아니다.</summary>
        OffAxis,
        /// <summary>
        /// 눈이 대상의 몸 안에 들어가 있다. 방향이 서지 않으므로 겨눴다고 볼 수 없다.
        ///
        /// 거대 버섯의 경계 상자는 21m다 — 갓 아래 어디에 서든 눈이 그 안에 있다.
        /// 이것을 차선책으로라도 잡아 주면, 아무것도 안 겨눴을 때마다 "거대 버섯 벌목"이
        /// 뜬다. 사용자가 적은 "바라보는 물체와 자막의 물체가 다르다"가 정확히 그 모습이다.
        /// 밑동을 실제로 겨누면 <see cref="AimCandidate.ExactHit"/>이 잡아 주므로
        /// 여기서 물러설 이유가 없다.
        /// </summary>
        Inside,
        /// <summary>사이에 막는 것이 있다.</summary>
        Blocked,
    }

    /// <summary>
    /// 조준 후보 하나. <see cref="Body"/>는 <b>눈에 보이는 몸</b>이어야 한다 —
    /// 채집 편의를 위해 붙여 둔 넓은 트리거(<c>InteractBounds</c>)가 아니라.
    /// 그 구별이 이 규칙 전체의 요점이다.
    /// </summary>
    public readonly struct AimCandidate
    {
        /// <summary>부르는 쪽이 후보를 되찾는 손잡이. 이 규칙은 뜻을 묻지 않는다.</summary>
        public readonly int Id;

        /// <summary>대상의 몸을 감싸는 상자(월드 좌표).</summary>
        public readonly Bounds Body;

        /// <summary>
        /// 시선이 실제로 이 대상의 <b>단단한 표면</b>을 뚫고 지나갔다면 그 지점.
        /// 있으면 상자보다 이쪽을 믿는다 — 거대 버섯처럼 상자가 통째로 큰 것은
        /// 상자만 봐서는 어디를 겨눴는지 알 수 없다.
        /// </summary>
        public readonly Vector3 ExactHit;

        /// <summary><see cref="ExactHit"/>이 유효한가.</summary>
        public readonly bool HasExactHit;

        /// <summary>휘두른 본인의 몸인가.</summary>
        public readonly bool IsSelf;

        public AimCandidate(int id, Bounds body, bool isSelf = false)
        {
            Id = id;
            Body = body;
            ExactHit = default;
            HasExactHit = false;
            IsSelf = isSelf;
        }

        public AimCandidate(int id, Bounds body, Vector3 exactHit, bool isSelf = false)
        {
            Id = id;
            Body = body;
            ExactHit = exactHit;
            HasExactHit = true;
            IsSelf = isSelf;
        }
    }

    /// <summary>어디서 어느 쪽을 얼마나 너그럽게 보는가.</summary>
    public readonly struct AimView
    {
        public readonly Vector3 Origin;
        /// <summary>보는 방향. 정규화되어 있다고 본다.</summary>
        public readonly Vector3 Forward;
        /// <summary>손이 닿는 거리.</summary>
        public readonly float MaxDistance;
        /// <summary>시선에서 이만큼까지는 겨눈 것으로 봐준다(미터).</summary>
        public readonly float Radius;
        /// <summary>이 안쪽의 각도 차이는 비긴 것으로 보고 가까운 쪽을 고른다(도).</summary>
        public readonly float TieDegrees;

        public AimView(Vector3 origin, Vector3 forward, float maxDistance, float radius,
                       float tieDegrees = AimSelection.DefaultTieDegrees)
        {
            Origin = origin;
            Forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
            MaxDistance = maxDistance;
            Radius = radius;
            TieDegrees = tieDegrees > 0f ? tieDegrees : AimSelection.DefaultTieDegrees;
        }
    }

    /// <summary>한 후보를 재어 본 결과.</summary>
    public readonly struct AimScore
    {
        public readonly int Id;
        /// <summary>이 대상에서 겨눈 것으로 친 지점.</summary>
        public readonly Vector3 Point;
        public readonly float Distance;
        /// <summary>시선에서 옆으로 벗어난 거리(미터).</summary>
        public readonly float Lateral;
        /// <summary>시선에서 벗어난 각도(도). 사람이 화면에서 느끼는 어긋남이 이것이다.</summary>
        public readonly float AngleDegrees;
        public readonly AimReject Reject;

        public bool Accepted => Reject == AimReject.None;

        public AimScore(int id, Vector3 point, float distance, float lateral,
                        float angleDegrees, AimReject reject)
        {
            Id = id;
            Point = point;
            Distance = distance;
            Lateral = lateral;
            AngleDegrees = angleDegrees;
            Reject = reject;
        }
    }

    /// <summary>사이에 막는 것이 있는지 물어보는 자리. 물리 질의는 바깥이 한다.</summary>
    public interface IAimObstruction
    {
        bool IsBlocked(in AimScore score);
    }

    /// <summary>
    /// 무엇을 조준하고 있는가를 정하는 규칙. 전부 순수 함수다 —
    /// 씬도 물리 질의도 시간도 건드리지 않고, 넣은 값만 보고 답한다.
    ///
    /// <b>왜 따로 떼어 냈는가.</b> 예전 규칙은 "스피어캐스트에 맞은 것 중 가장 가까운 것"
    /// 하나였다. 그런데 이 레벨의 채집물에는 편의를 위해 실제 몸보다 서너 배 큰
    /// 트리거(<c>InteractBounds</c>: 잔해 0.3m짜리에 1.6m 상자, 재 고사리에 4.7m 상자)가
    /// 붙어 있다. 그 부피의 옆면은 <b>언제나</b> 코앞에 있으므로, 저 멀리 있는 것을
    /// 똑바로 겨누고 있어도 발밑을 스치는 남의 부피가 먼저 이겼다.
    /// 사용자가 "곡괭이가 닿지도 않는데 곡괭이로 채굴이라고 뜬다"고 적은 것이 이 현상이다.
    ///
    /// 그래서 둘을 바꿨다.
    /// <list type="number">
    /// <item><b>재는 대상을 몸으로 바꿨다.</b> 트리거는 후보를 모으는 데만 쓰고,
    /// 겨눴는지는 눈에 보이는 몸으로 판정한다.</item>
    /// <item><b>이기는 기준을 각도로 바꿨다.</b> 거리가 아니라 시선에서 벗어난 각도가
    /// 먼저다. 사람은 화면에서 얼마나 어긋났는지로 "겨눴다"를 판단하지,
    /// 몇 미터인지로 판단하지 않는다.</item>
    /// </list>
    ///
    /// 가림 검사는 <see cref="IAimObstruction"/>에 맡긴다. 순위가 정해진 뒤
    /// <b>이길 만한 것부터</b> 물어보므로, 대개 물리 질의 한 번으로 끝난다.
    /// </summary>
    public static class AimSelection
    {
        /// <summary>기본 무승부 폭(도).</summary>
        public const float DefaultTieDegrees = 2f;

        /// <summary>이 거리 안쪽이면 겨냥점이 눈과 겹친 것으로 본다(1cm).</summary>
        public const float Epsilon = 0.01f;

        /// <summary>상자 밖의 가장 가까운 점을 찾을 때 되풀이하는 횟수.</summary>
        const int ClosestIterations = 4;

        /// <summary>
        /// 시선이 이 상자에 <b>가장 가까이 스치는</b> 지점.
        ///
        /// 시선이 상자를 실제로 뚫고 지나가면 들어가는 지점을 준다 — 그 자리가 곧
        /// 화면 한가운데에 보이는 곳이다. 빗나가면 상자 표면에서 시선에 제일 가까운
        /// 점을 되풀이로 좁혀 찾는다(<see cref="Bounds.ClosestPoint"/>와 시선 위 발점을
        /// 번갈아 갱신하면 몇 번 만에 수렴한다).
        ///
        /// 눈이 상자 안에 있으면 <see cref="Bounds.IntersectRay(Ray, out float)"/>가 0을
        /// 주므로 결과는 눈 자리 그대로다. 그 경우는 <see cref="Measure"/>가
        /// <see cref="AimReject.Inside"/>로 떨어뜨린다.
        /// </summary>
        public static Vector3 ClosestPointOnRay(in Bounds body, Vector3 origin, Vector3 forward,
                                                float maxDistance)
        {
            if (body.IntersectRay(new Ray(origin, forward), out float enter))
                return origin + forward * Mathf.Max(enter, 0f);

            // 시선 위의 발점과 상자 위의 가장 가까운 점을 번갈아 갱신한다.
            float t = Mathf.Clamp(Vector3.Dot(body.center - origin, forward), 0f, maxDistance);
            Vector3 p = body.ClosestPoint(origin + forward * t);

            for (int i = 0; i < ClosestIterations; i++)
            {
                t = Mathf.Clamp(Vector3.Dot(p - origin, forward), 0f, maxDistance);
                p = body.ClosestPoint(origin + forward * t);
            }

            return p;
        }

        /// <summary>후보 하나를 재어 본다. 가림 검사는 여기서 하지 않는다.</summary>
        public static AimScore Measure(in AimCandidate candidate, in AimView view)
        {
            if (candidate.IsSelf)
                return new AimScore(candidate.Id, candidate.Body.center, 0f, 0f, 0f,
                                    AimReject.Self);

            // 단단한 표면을 실제로 뚫고 지나갔으면 그 지점이 답이다. 상자는 근사일 뿐이다.
            Vector3 point = candidate.HasExactHit
                ? candidate.ExactHit
                : ClosestPointOnRay(candidate.Body, view.Origin, view.Forward, view.MaxDistance);

            Vector3 to = point - view.Origin;
            float distance = to.magnitude;

            // 눈이 대상 안에 들어가 있다. 방향이 서지 않으므로 각도로 겨룰 수 없다.
            if (distance <= Epsilon)
                return new AimScore(candidate.Id, point, 0f, 0f, 0f, AimReject.Inside);

            float along = Vector3.Dot(to, view.Forward);
            if (along <= 0f)
                return new AimScore(candidate.Id, point, distance, distance, 180f,
                                    AimReject.Behind);

            if (distance > view.MaxDistance)
                return new AimScore(candidate.Id, point, distance, 0f, 0f,
                                    AimReject.TooFar);

            float lateral = Mathf.Sqrt(Mathf.Max(0f, distance * distance - along * along));
            float angle = Mathf.Atan2(lateral, along) * Mathf.Rad2Deg;

            if (lateral > view.Radius)
                return new AimScore(candidate.Id, point, distance, lateral, angle,
                                    AimReject.OffAxis);

            return new AimScore(candidate.Id, point, distance, lateral, angle,
                                AimReject.None);
        }

        /// <summary>
        /// 둘 중 어느 쪽이 더 겨눈 것인가. 음수면 <paramref name="a"/>가 이긴다.
        ///
        /// 순서는 둘이다. ① 시선에서 덜 벗어난 쪽이 이긴다
        /// ② 그 차이가 무승부 폭 안이면 가까운 쪽이 이긴다.
        ///
        /// 각도를 무승부 폭으로 <b>나눠 떨어뜨려</b> 비교하는 것은 일부러다.
        /// "차이가 폭보다 작으면 비긴 것"으로 두면 A~B, B~C인데 A&lt;C인 경우가 생겨
        /// 정렬이 입력 순서에 따라 다른 답을 낸다.
        /// </summary>
        public static int Compare(in AimScore a, in AimScore b, float tieDegrees)
        {
            int bandA = Mathf.FloorToInt(a.AngleDegrees / tieDegrees);
            int bandB = Mathf.FloorToInt(b.AngleDegrees / tieDegrees);
            if (bandA != bandB) return bandA < bandB ? -1 : 1;

            if (!Mathf.Approximately(a.Distance, b.Distance))
                return a.Distance < b.Distance ? -1 : 1;

            return a.Id.CompareTo(b.Id);        // 같은 값이면 넣은 순서를 지킨다
        }

        /// <summary>
        /// 통과한 후보만 <paramref name="ranked"/>에 이길 순서대로 담는다.
        /// 목록은 부르는 쪽이 준다 — 매 프레임 도는 자리라 새로 만들지 않는다.
        /// </summary>
        public static void Rank(IReadOnlyList<AimCandidate> candidates, in AimView view,
                                List<AimScore> ranked)
        {
            ranked.Clear();
            if (candidates == null) return;

            float tie = view.TieDegrees;

            // 삽입 정렬이다. 후보는 많아야 서넛이고, 이 자리는 매 프레임 돈다 —
            // List.Sort에 람다를 넘기면 그 비교자를 프레임마다 새로 만든다.
            for (int i = 0; i < candidates.Count; i++)
            {
                var score = Measure(candidates[i], view);
                if (!score.Accepted) continue;

                int at = ranked.Count;
                while (at > 0 && Compare(score, ranked[at - 1], tie) < 0) at--;
                ranked.Insert(at, score);
            }
        }

        /// <summary>
        /// 조준 대상을 고른다. 이길 순서대로 가림 검사를 물어, 처음으로 뚫려 있는 것을 준다.
        /// </summary>
        /// <param name="obstruction">null이면 가림 검사를 하지 않는다.</param>
        /// <param name="ranked">부르는 쪽이 재사용하는 버퍼. 결과 순위가 그대로 남는다.</param>
        public static bool TrySelect(IReadOnlyList<AimCandidate> candidates, in AimView view,
                                     IAimObstruction obstruction, List<AimScore> ranked,
                                     out AimScore best)
        {
            Rank(candidates, view, ranked);

            for (int i = 0; i < ranked.Count; i++)
            {
                if (obstruction != null && obstruction.IsBlocked(ranked[i])) continue;
                best = ranked[i];
                return true;
            }

            best = default;
            return false;
        }
    }
}
