using UnityEngine;

namespace Survive.UI
{
    /// <summary>
    /// 커서 옆에 뜨는 쪽지를 어디에 놓을 것인가 (백로그: 사장된 아이템 설명문).
    ///
    /// <b>왜 순수 로직인가.</b> 이 규칙이 틀리면 화면 밖으로 반쯤 나간 쪽지가 뜨는데,
    /// 그건 눈으로만 잡히는 종류의 버그다. 게다가 잘못 뜨는 자리는 대개
    /// <b>모서리</b>라서 사람이 재현하기도 번거롭다. 자리 계산을 여기로 빼 두면
    /// 네 모서리 전부를 Unity 없이 확인할 수 있다.
    ///
    /// <b>좌표계.</b> 화면 좌하단이 원점이고 y가 위로 자란다 (Unity 화면 좌표).
    /// 다만 픽셀이 아니라 <b>캔버스 단위</b>로 넘겨도 된다 — 여기서는 크기의
    /// 절대값을 모르고, 넘어온 것끼리만 비교한다. 부르는 쪽(<see cref="ItemTooltipView"/>)이
    /// 스케일을 나눠 준다.
    /// </summary>
    public static class TooltipPlacement
    {
        /// <summary>커서와 쪽지 사이. 0이면 커서 끝이 쪽지에 닿아 글자를 가린다.</summary>
        public const float Gap = 18f;

        /// <summary>화면 가장자리에서 띄우는 여백. 붙여 놓으면 잘린 것처럼 보인다.</summary>
        public const float Margin = 12f;

        /// <summary>
        /// 쪽지의 최대 너비. 설명문 한 줄이 화면을 가로지르면 눈이 되돌아올 자리를 잃는다.
        /// 세로로 접히는 편이 읽힌다.
        /// </summary>
        public const float MaxWidth = 380f;

        /// <summary>
        /// 실제로 쓸 너비. 원하는 값이 최대치를 넘으면 자르고, 화면이 그보다 좁으면
        /// 화면에 맞춘다 — 세로 화면이나 작은 창에서는 380조차 넓다.
        /// </summary>
        public static float ClampWidth(float desired, float screenWidth,
                                       float margin = Margin, float maxWidth = MaxWidth)
        {
            float room = Mathf.Max(0f, screenWidth - margin * 2f);
            float limit = Mathf.Min(maxWidth, room);
            if (desired <= 0f) return limit;
            return Mathf.Min(desired, limit);
        }

        /// <summary>
        /// 쪽지가 놓일 자리.
        ///
        /// 기본은 <b>커서의 오른쪽 아래</b>다. 오른쪽이 모자라면 왼쪽으로, 아래가
        /// 모자라면 위로 뒤집는다. 뒤집기가 성공하는 한 쪽지는 커서를 덮지 않는다 —
        /// 덮으면 지금 무엇을 가리키고 있는지가 사라진다.
        ///
        /// 양쪽 다 모자랄 만큼 쪽지가 크면 마지막에 화면 안으로 밀어 넣는다.
        /// 그 경우에만 커서와 겹칠 수 있는데, 반쯤 잘린 쪽지보다는 낫다.
        /// </summary>
        public static Rect Place(Vector2 cursor, Vector2 size, Vector2 screen,
                                 float gap = Gap, float margin = Margin)
        {
            float w = Mathf.Clamp(size.x, 0f, Mathf.Max(0f, screen.x - margin * 2f));
            float h = Mathf.Clamp(size.y, 0f, Mathf.Max(0f, screen.y - margin * 2f));

            float x = cursor.x + gap;
            if (x + w > screen.x - margin)
            {
                float flipped = cursor.x - gap - w;
                x = flipped >= margin
                    ? flipped
                    : Mathf.Clamp(x, margin, Mathf.Max(margin, screen.x - margin - w));
            }

            // y는 아래 변이다. 기본 자리에서 쪽지의 위 변이 커서 바로 아래에 온다.
            float y = cursor.y - gap - h;
            if (y < margin)
            {
                float flipped = cursor.y + gap;
                y = flipped + h <= screen.y - margin
                    ? flipped
                    : Mathf.Clamp(y, margin, Mathf.Max(margin, screen.y - margin - h));
            }

            return new Rect(x, y, w, h);
        }
    }
}
