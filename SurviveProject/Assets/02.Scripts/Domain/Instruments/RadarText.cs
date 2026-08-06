using UnityEngine;
using Survive.Localization;

namespace Survive.Instruments
{
    /// <summary>
    /// 레이더 화면에 <b>무엇이 적히는가</b>.
    ///
    /// 그리는 쪽에서 떼어 낸 이유는 <see cref="Survive.UI.PickupFeedText"/>와 같다 —
    /// 글자를 짓는 자리가 MonoBehaviour 안에 흩어져 있으면 "표에서 나온 문장인가"를
    /// EditMode에서 되물을 수 없다.
    ///
    /// <b>종류 이름도 표를 거친다.</b> 잡힌 것에 이름을 붙이는 일은 판정이 아니라
    /// 번역의 일이다. 여기서 한국어를 적으면 다른 로케일에서만 조용히 깨진다.
    /// </summary>
    public static class RadarText
    {
        public const string Category = "Radar";

        /// <summary>잡힌 것 한 줄. 종류 · 방위 · 거리.</summary>
        public static string Reading(RadarContact contact)
        {
            if (contact == null) return "";

            return Loc.F(Category, "reading",
                         KindName(contact.kind),
                         Mathf.RoundToInt(contact.bearingDegrees),
                         Mathf.RoundToInt(contact.distanceMeters));
        }

        /// <summary>결과 화면에 적히는 종류 이름.</summary>
        public static string KindName(RadarContactKind kind) => Loc.T(Category, KindKey(kind));

        /// <summary>왜 끊겼는지 알리는 한 줄. 끊기지 않았으면 빈 문자열.</summary>
        public static string CancelLine(RadarCancelReason reason)
        {
            switch (reason)
            {
                case RadarCancelReason.Moved: return Loc.T(Category, "cancel_moved");
                case RadarCancelReason.PowerOut: return Loc.T(Category, "cancel_power");
                case RadarCancelReason.Aborted: return Loc.T(Category, "cancel_aborted");
                default: return "";
            }
        }

        static string KindKey(RadarContactKind kind)
        {
            switch (kind)
            {
                case RadarContactKind.Island: return "kind_island";
                case RadarContactKind.Cavity: return "kind_cavity";
                case RadarContactKind.DeepLayer: return "kind_deep_layer";
                case RadarContactKind.Structure: return "kind_structure";
                case RadarContactKind.Creature: return "kind_creature";
                case RadarContactKind.Fissure: return "kind_fissure";
                default: return "kind_unknown";
            }
        }
    }
}
