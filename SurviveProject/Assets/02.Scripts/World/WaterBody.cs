using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 액체 덩어리 하나. <b>무엇으로 된 것인지</b>와 수면 높이와 수평 범위를 알려준다.
    ///
    /// 씬의 물 오브젝트에는 콜라이더가 없다. 트리거를 새로 붙이는 대신
    /// 렌더러 경계에서 수면과 범위를 읽는다 - 기존 프리팹을 건드리지 않아도 된다.
    ///
    /// <b>왜 액체마다 새 컴포넌트를 만들지 않는가.</b> 호수와 매크로늄 바다는 서로
    /// 다른 물건이 아니라 <b>같은 물건의 두 값</b>이다(<see cref="LiquidKind"/>).
    /// 컴포넌트를 둘로 가르면 잠기는 판정도 둘이 되고, 둘이 어긋나는 날
    /// "헤엄은 치는데 안 아프다"거나 그 반대가 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterBody : MonoBehaviour
    {
        static readonly List<WaterBody> _all = new List<WaterBody>();

        /// <summary>
        /// 이 액체가 무엇인가.
        ///
        /// <b>기본값을 주지 않는다.</b> 비워 두면 0이고 0은 이름이 없다 - 그러면
        /// 이 덩어리는 아예 등록되지 않아서 헤엄조차 쳐지지 않는다. 조용히 물이나
        /// 매크로늄으로 두는 것보다 <b>눈에 띄게 망가지는 편</b>이 낫다.
        /// 씬의 모든 덩어리가 종류를 갖는지는 <c>LiquidKindTests</c>가 지킨다.
        /// </summary>
        [Tooltip("이 액체가 무엇인가. 반드시 골라야 한다 - 비워 두면 이 덩어리는 없는 것이 된다")]
        [SerializeField] LiquidKind kind;

        [Tooltip("비우면 렌더러 경계에서 자동으로 읽는다")]
        [SerializeField] float surfaceOverride = 0f;
        [SerializeField] bool useOverride = false;

        Bounds _bounds;
        bool _boundsCached;

        public float SurfaceY => useOverride ? surfaceOverride : WorldBounds.max.y;

        /// <summary>종류가 적혀 있는가. 안 적혀 있으면 이 덩어리는 세계에 없는 것으로 친다.</summary>
        public bool HasKind => Liquid.IsKnown(kind);

        /// <summary>이 액체의 종류. 안 적혀 있으면 던진다.</summary>
        public LiquidKind Kind => Liquid.Require(kind);

        /// <summary>검사와 편집 도구가 종류를 적어 넣는 자리.</summary>
        public void SetKind(LiquidKind value) => kind = value;

        Bounds WorldBounds
        {
            get
            {
                if (_boundsCached) return _bounds;

                var rends = GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    _bounds = rends[0].bounds;
                    foreach (var r in rends) _bounds.Encapsulate(r.bounds);
                }
                else _bounds = new Bounds(transform.position, Vector3.one);

                _boundsCached = true;
                return _bounds;
            }
        }

        void OnEnable()
        {
            _boundsCached = false;

            // 종류가 없는 덩어리는 등록하지 않는다. 규칙에게 물을 것이 없기 때문이다.
            // 조용히 넘어가면 씬이 틀린 채로 초록불이 켜지므로 한 줄 남긴다.
            //
            // 글이 영어인 이유: 개발자 콘솔에만 닿는 진단문인데 이 폴더는 화면 코드
            // 범위라 한글 리터럴이 금지되어 있다(LocSentenceGateTests).
            if (!HasKind)
            {
                Debug.LogError($"WaterBody '{name}' has no liquid kind. " +
                               "Pick Water or Macronium, otherwise it is not in the world.", this);
                return;
            }
            _all.Add(this);
        }

        void OnDisable() => _all.Remove(this);

        public bool ContainsHorizontally(Vector3 p)
        {
            var b = WorldBounds;
            return p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;
        }

        /// <summary>
        /// 이 지점의 수면 높이. 물이 없으면 false.
        /// 겹치면 가장 높은 수면을 쓴다.
        /// </summary>
        public static bool TryGetSurfaceAt(Vector3 p, out float surfaceY) =>
            TryGetAt(p, out surfaceY, out _);

        /// <summary>
        /// 이 지점의 수면 높이<b>와 종류</b>. 액체가 없으면 false.
        ///
        /// <b>고르는 규칙은 하나다</b> - 겹치면 가장 높은 수면이 이긴다. 높이와 종류를
        /// 따로 고르면 "호수 높이에 바다 종류" 같은 답이 나올 수 있으므로 한 번에 고른다.
        /// 뭍 위의 호수가 바다보다 높은 것은 물리적으로도 그쪽이 자연스럽다.
        /// </summary>
        public static bool TryGetAt(Vector3 p, out float surfaceY, out LiquidKind kind)
        {
            surfaceY = float.MinValue;
            kind = default;
            bool found = false;

            for (int i = 0; i < _all.Count; i++)
            {
                var w = _all[i];
                if (w == null || !w.ContainsHorizontally(p)) continue;

                float s = w.SurfaceY;
                // 수면보다 훨씬 위에 있으면 그 물과는 무관하다
                if (p.y > s + 30f) continue;

                if (!found || s > surfaceY) { surfaceY = s; kind = w.Kind; found = true; }
            }
            return found;
        }
    }
}
