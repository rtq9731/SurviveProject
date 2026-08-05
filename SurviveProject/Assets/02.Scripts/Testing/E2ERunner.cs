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
        void OnDestroy() => E2EHarness.RestoreInput();

        /// <summary>씬에 러너가 없으면 만든다.</summary>
        public static E2ERunner EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("E2ERunner");
            return go.AddComponent<E2ERunner>();
        }

        public static void Run(string name, IEnumerator routine)
        {
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
                    E2EHarness.Log("실패: " + e.Message);
                    E2EHarness.Log($"=== {name} 실패 ({ElapsedSeconds:F1}초) ===");
                    E2EHarness.RemoveDevice();
                    E2EHarness.RestoreInput();
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
            // 시나리오가 시간을 멈춘 채 끝나면 다음 실행이 얼어붙는다. 되돌린다.
            Time.timeScale = 1f;
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
