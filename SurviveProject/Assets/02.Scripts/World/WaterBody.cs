using System.Collections.Generic;
using UnityEngine;

namespace Survive.World
{
    /// <summary>
    /// 물 덩어리. 수면 높이와 수평 범위를 알려준다.
    ///
    /// 씬의 물 오브젝트에는 콜라이더가 없다. 트리거를 새로 붙이는 대신
    /// 렌더러 경계에서 수면과 범위를 읽는다 — 기존 프리팹을 건드리지 않아도 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaterBody : MonoBehaviour
    {
        static readonly List<WaterBody> _all = new List<WaterBody>();

        [Tooltip("비우면 렌더러 경계에서 자동으로 읽는다")]
        [SerializeField] float surfaceOverride = 0f;
        [SerializeField] bool useOverride = false;

        Bounds _bounds;
        bool _boundsCached;

        public float SurfaceY => useOverride ? surfaceOverride : WorldBounds.max.y;

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

        void OnEnable() { _boundsCached = false; _all.Add(this); }
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
        public static bool TryGetSurfaceAt(Vector3 p, out float surfaceY)
        {
            surfaceY = float.MinValue;
            bool found = false;

            for (int i = 0; i < _all.Count; i++)
            {
                var w = _all[i];
                if (w == null || !w.ContainsHorizontally(p)) continue;

                float s = w.SurfaceY;
                // 수면보다 훨씬 위에 있으면 그 물과는 무관하다
                if (p.y > s + 30f) continue;

                if (!found || s > surfaceY) { surfaceY = s; found = true; }
            }
            return found;
        }
    }
}
