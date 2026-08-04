using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survive.Core
{
    /// <summary>
    /// 저장본 ↔ 텍스트. 파일도 경로도 모르는 순수 변환이다.
    ///
    /// 읽기를 예외 대신 <c>TryDeserialize</c>로 둔 이유: 저장본은 사람이 고칠 수도,
    /// 쓰다 만 채로 남을 수도 있다. 그건 예외 상황이 아니라 <b>예상되는 입력</b>이고,
    /// 그럴 때 게임이 죽는 대신 "이어할 수 없다"로 끝나야 한다.
    /// </summary>
    public static class SaveSerializer
    {
        /// <summary>
        /// 저장본을 텍스트로. 쓰는 시점에 현재 버전을 찍는다 —
        /// 버전을 만드는 곳이 한 군데뿐이어야 나중에 올릴 때 빠뜨리지 않는다.
        /// </summary>
        public static string Serialize(SaveSnapshot snapshot)
        {
            if (snapshot == null) return string.Empty;

            snapshot.version = SaveSnapshot.CurrentVersion;
            snapshot.entries ??= new List<SaveEntry>();

            return JsonUtility.ToJson(snapshot, true);
        }

        /// <summary>
        /// 텍스트에서 저장본을. 읽어 낼 수 없으면 false를 돌려주고
        /// <paramref name="error"/>에 사람이 읽을 사유를 담는다.
        ///
        /// 읽어 낸 저장본은 정규화된다 — <c>entries</c>는 절대 null이 아니고,
        /// 키가 빈 칸은 버린다(어떤 저장 대상과도 이어질 수 없는 칸이다).
        /// </summary>
        public static bool TryDeserialize(string text, out SaveSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "저장 데이터가 비어 있습니다";
                return false;
            }

            SaveSnapshot parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveSnapshot>(text);
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }

            if (parsed == null)
            {
                error = "저장 데이터를 해석하지 못했습니다";
                return false;
            }

            if (parsed.entries == null) parsed.entries = new List<SaveEntry>();
            else parsed.entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.key));

            snapshot = parsed;
            return true;
        }
    }
}
