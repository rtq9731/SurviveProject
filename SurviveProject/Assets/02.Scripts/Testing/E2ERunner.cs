using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Survive.Testing
{
    /// <summary>
    /// 시나리오를 실행하고 결과를 남긴다.
    ///
    /// uloop는 요청-응답 모델이라 코루틴이 끝날 때까지 기다릴 수 없다.
    /// 그래서 시작만 시키고 <see cref="Status"/>를 폴링해 결과를 받는다.
    /// </summary>
    public class E2ERunner : MonoBehaviour
    {
        public enum RunStatus { Idle, Running, Passed, Failed }

        public static E2ERunner Instance { get; private set; }

        public static RunStatus Status { get; private set; } = RunStatus.Idle;
        public static string ScenarioName { get; private set; } = "";
        public static string FailReason { get; private set; } = "";
        public static float ElapsedSeconds { get; private set; }

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // 재생이 끝나는 순간에도 진짜 장치를 돌려준다. 시나리오 도중에 재생을 멈추면
        // RunGuarded의 끝을 지나지 못하는데, 그대로 두면 사람의 키보드가 잠긴 채 남는다.
        // 격리해 둔 세계(재운 생물·꺼 둔 광원)도 같은 이유로 여기서 되돌린다.
        void OnDestroy()
        {
            E2EHarness.RestoreInput();
            E2EHarness.RestoreWorld();
        }

        /// <summary>씬에 러너가 없으면 만든다.</summary>
        public static E2ERunner EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("E2ERunner");
            return go.AddComponent<E2ERunner>();
        }

        public static void Run(string name, IEnumerator routine)
        {
            // 화면 밝기는 사람이 정하는 값이지만, 검증은 언제나 기본값으로 돌아야 한다.
            // 안 그러면 같은 빌드가 기계마다(그 기계의 감마 설정마다) 다른 결과를 낸다.
            // 조절 화면도 스크린샷 위에 겹치지 않게 비켜 세운다.
            Survive.Art.GammaSettings.ForceNeutral = true;
            Survive.Art.GammaCalibrationScreen.AutoShow = false;
            Survive.Art.GammaCalibrationScreen.Close();

            var runner = EnsureExists();
            runner.StopAllCoroutines();
            runner.StartCoroutine(runner.RunGuarded(name, routine));
        }

        IEnumerator RunGuarded(string name, IEnumerator routine)
        {
            E2EHarness.ClearLog();
            ScenarioName = name;
            FailReason = "";
            Status = RunStatus.Running;
            ElapsedSeconds = 0f;

            // 진짜 키보드·마우스를 떼어 놓는다. 창이 앞에 없으면 그 상태가 얼어붙어
            // 가상 입력을 덮어쓴다 — E2EHarness.IsolateInput에 자세히 적어 두었다.
            E2EHarness.IsolateInput();

            var sw = Stopwatch.StartNew();
            E2EHarness.Log("=== " + name + " 시작 ===");

            // 코루틴 안에서 던진 예외는 바깥에서 try/catch로 못 잡는다.
            // 한 단계씩 직접 굴리면서 잡는다.
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(routine);
            stack.Push(WaitForWorld());   // Stack이므로 이쪽이 먼저 돈다

            while (stack.Count > 0)
            {
                var top = stack.Peek();
                object current = null;
                bool hasNext;

                try
                {
                    hasNext = top.MoveNext();
                    if (hasNext) current = top.Current;
                }
                catch (Exception e)
                {
                    sw.Stop();
                    ElapsedSeconds = (float)sw.Elapsed.TotalSeconds;
                    FailReason = e.Message;
                    Status = RunStatus.Failed;
                    E2EHarness.Log("실패: " + e.GetType().Name + " — " + e.Message);

                    // 단언·타임아웃은 메시지 한 줄로 원인을 알 수 있지만, NullReference처럼
                    // 예상하지 못한 예외는 어디서 터졌는지를 모르면 고칠 수가 없다.
                    // 로그 버퍼에 스택을 통째로 남긴다 — 이것이 없어서 산발적으로 나던
                    // "NullReferenceException | 0.0초"의 자리를 오래 못 짚었다.
                    if (!(e is TimeoutException) && e.StackTrace != null)
                        E2EHarness.Log("스택:\n" + e.StackTrace);

                    E2EHarness.Log($"=== {name} 실패 ({ElapsedSeconds:F1}초) ===");
                    E2EHarness.RemoveDevice();
                    E2EHarness.RestoreInput();
                    E2EHarness.RestoreWorld();
            // 시나리오가 시간을 멈춘 채 끝나면 다음 실행이 얼어붙는다. 되돌린다.
            Time.timeScale = 1f;
                    yield break;
                }

                if (!hasNext) { stack.Pop(); continue; }

                if (current is IEnumerator inner) stack.Push(inner);
                else yield return current;
            }

            sw.Stop();
            ElapsedSeconds = (float)sw.Elapsed.TotalSeconds;
            Status = RunStatus.Passed;
            E2EHarness.Log($"=== {name} 통과 ({ElapsedSeconds:F1}초) ===");
            E2EHarness.RemoveDevice();
            E2EHarness.RestoreInput();
            E2EHarness.RestoreWorld();
            // 시나리오가 시간을 멈춘 채 끝나면 다음 실행이 얼어붙는다. 되돌린다.
            Time.timeScale = 1f;
        }

        /// <summary>
        /// 세계가 실제로 깨어날 때까지 기다린다.
        ///
        /// <b>왜 필요한가.</b> 시나리오의 첫 대목은 대개 인벤토리나 활력치를 읽는데,
        /// 그것들은 <c>Awake</c>에서 만들어진다(<c>PlayerInventory.Inventory</c>,
        /// <c>PlayerVitals.Health</c>). 재생에 막 들어간 프레임에 시나리오를 시작하면
        /// 아직 null이라 <b>0.0초에 NullReferenceException</b>이 터지고, 메시지에는
        /// "Object reference not set"만 남아 무엇이 없었는지가 보이지 않는다.
        /// 산발적으로 보고되던 그 실패의 정체가 이것으로 설명된다 —
        /// 기다렸다가 시작하고, 그래도 없으면 <b>무엇이</b> 없는지 말하고 끝낸다.
        /// </summary>
        static IEnumerator WaitForWorld()
        {
            const float Limit = 5f;
            float waited = 0f;
            string missing;

            while (true)
            {
                missing = WhatIsMissing();
                if (missing == null) yield break;
                if (waited >= Limit) break;

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            throw new InvalidOperationException(
                $"세계가 아직 준비되지 않았다: {missing} ({Limit}초 기다림). " +
                "재생에 들어간 직후가 아니라 씬이 깨어난 뒤에 시나리오를 시작하십시오.");
        }

        /// <summary>없는 것의 이름. 전부 있으면 null.</summary>
        static string WhatIsMissing()
        {
            var player = UnityEngine.Object.FindAnyObjectByType<Survive.Player.PlayerContext>(
                             FindObjectsInactive.Exclude);
            if (player == null) return "PlayerContext";
            if (player.Vitals == null) return "PlayerVitals";
            if (player.Vitals.Health == null) return "PlayerVitals.Health";
            if (player.Inventory == null) return "PlayerInventory";
            if (player.Inventory.Inventory == null) return "PlayerInventory.Inventory";
            if (Camera.main == null) return "Camera.main";
            return null;
        }

        /// <summary>uloop에서 폴링할 한 줄 요약.</summary>
        public static string Summary()
        {
            return $"{ScenarioName} | {Status}" +
                   (Status == RunStatus.Failed ? " | " + FailReason : "") +
                   $" | {ElapsedSeconds:F1}초";
        }
    }
}
