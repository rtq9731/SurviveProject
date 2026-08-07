using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Vitals
{
    /// <summary>
    /// 게이지 넷을 저장본에 담은 모양. <see cref="ids"/>와 <see cref="values"/>는 짝이다.
    ///
    /// 필드를 <c>health</c>·<c>oxygen</c>처럼 이름으로 늘어놓지 않은 이유:
    /// 게이지는 이미 한 번 둘에서 넷이 되었고(2026-08-07), 다음에 하나가 더 붙을 때
    /// <b>저장본 형식이 바뀌면 옛 저장본이 열리지 않는다.</b> 목록으로 담으면
    /// 늘어난 것은 저장본에 한 칸 더 생길 뿐이고, 줄어든 것은 읽는 쪽이 모르는
    /// 아이디로 만나 조용히 무시된다.
    /// </summary>
    [Serializable]
    public class VitalsSaveState
    {
        public string[] ids;
        public float[] values;
    }

    /// <summary>
    /// <b>게이지는 저장된 값 그대로 돌아온다.</b> 넷을 한 규칙으로 본다.
    ///
    /// <h3>왜 그대로인가</h3>
    /// 밤낮 시각을 저장하기로 한 라운드가 남긴 근거가 그대로 적용된다 —
    /// <i>"시각은 세계 상태이지 화면 설정이 아니다. 밤에 저장하고 아침에 로드하면
    /// 감수한 위험이 사라진다."</i> 게이지도 같다. 수분 5%로 저장하고 불러왔더니
    /// 100%가 되면 <b>불러오기가 곧 최적의 회복 수단</b>이 되고, 그 순간 물가로
    /// 돌아갈 이유가 사라진다(<see cref="Sustenance"/>가 존재하는 이유 자체가 그것이다).
    ///
    /// <h3>죽음을 흉내내지 않는다</h3>
    /// "죽으면 체력이 가득 차 돌아오니 불러오기도 그래야 하지 않는가"는 뒤집힌
    /// 물음이다. 체력을 채우는 것은 <see cref="PlayerVitals.Revive"/>이고, 그 일이
    /// 벌어진 뒤의 몸이 그대로 저장된다 — 죽어서 가득 찬 몸을 저장하면 가득 찬 채로
    /// 돌아온다. 저장은 <b>죽음의 규칙을 다시 쓰는 자리가 아니라 그 결과를 담는
    /// 자리</b>다. 반대로 죽기 직전(체력 3)에 저장했다면 체력 3으로 돌아온다.
    /// 죽음의 대가는 <b>시간뿐</b>이라는 규칙(기획서 §5.7)이 여기서도 지켜진다 —
    /// 불러오기가 죽음을 지우지도, 죽음이 불러오기를 대신하지도 않는다.
    ///
    /// <h3>산소도 예외가 아니다</h3>
    /// 산소는 상시 자원이 아니라 수중·특수 필드에서만 준다(기획서 §5.1). 그래서
    /// "물속에서 저장하고 나갈 수 있는가"가 물음이 되는데, <b>저장본에는 위치가
    /// 없다.</b> 불러오기는 사람을 물속에 되돌려 놓지 않으므로, 산소를 그대로
    /// 복원해도 갇히지 않는다. 반대로 가득 채워 주면 <b>잠수 직전 저장 → 불러오기</b>가
    /// 무한 산소통이 된다. 특례를 만들 이유가 없다.
    ///
    /// <h3>넷을 한 규칙으로 보는 이유</h3>
    /// 특례 넷보다 규칙 하나가 낫다. 게이지마다 다르게 다루면 플레이어가 배울 규칙이
    /// 넷이 되고, 다음에 붙는 다섯 번째가 어느 쪽인지 아무도 모른다.
    /// </summary>
    public static class VitalsSave
    {
        /// <summary>저장본에서 이 항목을 찾는 열쇠.</summary>
        public const string Key = "player_vitals";

        /// <summary>
        /// 지금 값을 그대로 담는다. 짝이 안 맞으면 <b>짧은 쪽까지만</b> 담는다 —
        /// 절반만 담긴 저장본이 아예 없는 저장본보다 낫다.
        /// </summary>
        public static VitalsSaveState Capture(IReadOnlyList<string> ids, IReadOnlyList<Vital> vitals)
        {
            int n = ids == null || vitals == null ? 0 : Math.Min(ids.Count, vitals.Count);

            var state = new VitalsSaveState
            {
                ids = new string[n],
                values = new float[n],
            };

            for (int i = 0; i < n; i++)
            {
                state.ids[i] = ids[i];
                state.values[i] = vitals[i] != null ? vitals[i].Current : 0f;
            }
            return state;
        }

        /// <summary>
        /// 이 아이디의 저장값을 꺼낸다. 없으면 거짓 — <b>0을 대신 주지 않는다.</b>
        /// 0은 "굶어 죽기 직전"이라는 뜻이 있는 값이고, 그것을 "적혀 있지 않다"의
        /// 기본값으로 쓰면 게이지 하나가 늘어날 때마다 옛 저장본이 사람을 바닥에서
        /// 깨운다.
        /// </summary>
        public static bool TryValueFor(VitalsSaveState state, string id, out float value)
        {
            value = 0f;
            if (state == null || state.ids == null || state.values == null) return false;
            if (string.IsNullOrEmpty(id)) return false;

            int n = Math.Min(state.ids.Length, state.values.Length);
            for (int i = 0; i < n; i++)
            {
                if (state.ids[i] != id) continue;

                float saved = state.values[i];

                // 파일은 사람도 고칠 수 있고 예전 코드가 쓴 것일 수도 있다.
                // 숫자가 아닌 것은 "적혀 있지 않은 것"과 같이 다룬다 — 게이지에
                // NaN이 들어가면 그 뒤로 모든 비교가 거짓이 되어 몸이 굳는다.
                if (float.IsNaN(saved) || float.IsInfinity(saved)) return false;

                value = saved;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 저장값을 이 게이지가 받아들일 수 있는 범위로 깎는다.
        ///
        /// 최대치는 정의 에셋이 정하므로 <b>저장한 뒤에 바뀔 수 있다.</b> 그때 옛
        /// 저장본의 100이 새 최대치 80을 넘겨 버리면 게이지가 자기 눈금 밖에 선다.
        /// </summary>
        public static float Clamped(float saved, float max) =>
            Mathf.Clamp(saved, 0f, Mathf.Max(0f, max));

        /// <summary>
        /// 저장값을 게이지에 되돌린다. 적혀 있지 않으면 <b>손대지 않는다</b> —
        /// 그것이 "게이지 열쇠가 없는 옛 저장본이 그냥 열린다"의 실체다.
        /// </summary>
        /// <returns>실제로 되돌렸으면 참.</returns>
        public static bool RestoreInto(VitalsSaveState state, string id, Vital vital)
        {
            if (vital == null) return false;
            if (!TryValueFor(state, id, out float saved)) return false;

            float target = Clamped(saved, vital.Max);
            vital.Modify(target - vital.Current);
            return true;
        }
    }
}
