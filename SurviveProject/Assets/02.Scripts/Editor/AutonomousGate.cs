using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Survive.EditorTools
{
    /// <summary>
    /// 자율 주행 게이트를 한 번에 돌리는 진입점.
    ///
    /// 지금까지는 게이트를 통과했다고 말하려면 메뉴 두 개(참조 무결성·아트 규칙)를
    /// 각각 실행하고, 콘솔에 쏟아진 한국어 보고서를 사람이 읽어 "위반 없음"이라는
    /// 문장을 눈으로 확인해야 했다. 자동으로 도는 쪽에서는 그 문장을 문자열로
    /// 찾아 맞히는 수밖에 없었고, 보고서 문구를 조금만 손봐도 게이트가 조용히
    /// 아무것도 막지 못하는 상태가 된다.
    ///
    /// 그래서 여기서는 <b>숫자</b>를 낸다. <see cref="Run"/>이 두 점검을 코드로
    /// 호출해 건수를 받고, 표식 사이에 JSON 한 덩어리로 찍는다. 바깥에서는
    /// uloop get-logs로 그 덩어리만 떼어 파싱하면 된다.
    ///
    /// 점검 로직은 여기에 없다 — <see cref="ReferenceIntegrityChecker"/>와
    /// <see cref="ArtRuleChecker"/>가 그대로 한다. 복사해 두면 규칙이 갈라진다.
    ///
    /// EditMode 테스트는 넣지 않았다. uloop run-tests가 이미 그 일을 하고,
    /// 여기서 테스트 러너를 다시 띄우면 같은 것을 두 번 도는 셈이 된다.
    /// </summary>
    public static class AutonomousGate
    {
        /// <summary>JSON 덩어리의 시작. 로그에서 이 줄 다음부터 잘라 읽는다.</summary>
        public const string BeginMarker = "===== AUTONOMOUS_GATE_JSON_BEGIN =====";

        /// <summary>JSON 덩어리의 끝.</summary>
        public const string EndMarker = "===== AUTONOMOUS_GATE_JSON_END =====";

        /// <summary>게이트 판정 결과. JsonUtility가 그대로 직렬화한다.</summary>
        [Serializable]
        public class Result
        {
            /// <summary>둘 다 0이고 예외도 없었는가.</summary>
            public bool pass;

            /// <summary>끊긴 참조·근거 없는 빈 참조 건수. 기준선은 0.</summary>
            public int brokenReferences;

            /// <summary>아트 규칙 위반 건수(머티리얼 + 라이트). 기준선은 0.</summary>
            public int artViolations;

            /// <summary>점검 중 튀어나온 예외. 비어 있는 것이 정상이다.</summary>
            public string error;

            /// <summary>점검에 걸린 시간(초). 씬을 여러 번 여느라 대개 수 초 걸린다.</summary>
            public float seconds;
        }

        [MenuItem("Tools/Survive/자율 게이트 실행")]
        public static void RunFromMenu() => Run();

        /// <summary>
        /// 두 점검을 돌리고 결과를 콘솔에 남긴다.
        /// 사람이 읽을 보고서는 따로 찍고, 판정에 쓰는 숫자만 JSON으로 묶는다.
        /// </summary>
        public static Result Run()
        {
            var result = new Result { pass = false, error = "" };
            var clock = Stopwatch.StartNew();

            try
            {
                string referenceReport = ReferenceIntegrityChecker.Run(out int broken);
                result.brokenReferences = broken;
                Debug.Log("[자율 게이트] 참조 무결성 점검\n" + referenceReport);

                string artReport = ArtRuleChecker.Run(out int violations);
                result.artViolations = violations;
                Debug.Log("[자율 게이트] 아트 규칙 점검\n" + artReport);

                result.pass = broken == 0 && violations == 0;
            }
            catch (Exception e)
            {
                // 예외를 삼키고 통과시키면 게이트가 있으나 마나다. 실패로 적고 남긴다.
                result.error = e.GetType().Name + ": " + e.Message;
                result.pass = false;
                Debug.LogError("[자율 게이트] 점검 중 예외\n" + e);
            }

            clock.Stop();
            result.seconds = (float)clock.Elapsed.TotalSeconds;

            Debug.Log(BeginMarker + "\n" + JsonUtility.ToJson(result) + "\n" + EndMarker);
            return result;
        }

        /// <summary>
        /// 배치(-executeMethod)에서 부르는 진입점. 통과하면 0, 아니면 1로 끝낸다.
        /// 에디터를 띄워 둔 채 부를 때는 종료하지 않는다.
        /// </summary>
        public static void RunBatch()
        {
            var result = Run();
            if (Application.isBatchMode) EditorApplication.Exit(result.pass ? 0 : 1);
        }
    }
}
