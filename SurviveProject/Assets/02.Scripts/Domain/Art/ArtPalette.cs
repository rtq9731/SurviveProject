using System.Collections.Generic;
using UnityEngine;

namespace Survive.Domain.Art
{
    /// <summary>
    /// 지하에서 빛나는 것은 넷뿐이다 — 빛기둥·발광 버섯·불꽃·매크로늄.
    /// 그 외의 발광은 금지한다. 이것이 출처가 섞인 저폴리 에셋 팩들을
    /// 한 세계로 묶는 가장 강한 규칙이다.
    ///
    /// 값을 바꾸려면 코드가 아니라
    /// docs/superpowers/specs/2026-08-03-p0-art-direction-design.md 를 먼저 고친다.
    /// </summary>
    public static class ArtPalette
    {
        // 광원 — 색상환에 고르게 떨어뜨려 어둠 속에서 색만으로 구분되게 한다
        public static readonly Color LightShaft = FromHex(0xE8D5A8);         // 추락 구멍. 모래폭풍에 걸러진 지표광
        public static readonly Color Glowshroom = FromHex(0x2FE6C8);         // 발광 버섯. 생명·안전·무료 충전
        public static readonly Color Flame = FromHex(0xFF9A2E);              // 랜턴·화톳불. 내 빛이자 카운트다운
        public static readonly Color Macronium = FromHex(0xA12EE0);          // MARSO의 인공물. 가로막는 것
        public static readonly Color MacroniumHighlight = FromHex(0xE77BFF); // 매크로늄 표면 하이라이트

        // 물은 발광하지 않는다. 반사와 굴절로만 존재한다
        public static readonly Color WaterShallow = FromHex(0x2E5C7A);
        public static readonly Color WaterDeep = FromHex(0x0E1F2E);

        // 포그 색이 곧 구역의 색이다. 깊이가 곧 자홍의 농도다
        public static readonly Color FogSurface = FromHex(0xC4703A);      // 프롤로그 화성 지표. 모래폭풍
        public static readonly Color FogIslands = FromHex(0x0C0F15);      // 부유섬. 매크로늄은 발밑에만
        public static readonly Color FogPlains = FromHex(0x0D1A18);       // 얕은 평야. 등불버섯이 천장을 밝힌다
        public static readonly Color FogPlainsNight = FromHex(0x140A1E);  // 평야의 밤. 자홍 기운이 돈다
        public static readonly Color FogCliffs = FromHex(0x2C1240);       // 깊은 절벽. 매크로늄이 노출되어 고인다

        // readonly 배열은 참조만 고정할 뿐 원소는 그대로 변경 가능하다
        // (ArtPalette.AllowedEmission[0] = Color.red 가 컴파일된다). 이 배열은
        // MaterialRule·LightRule이 검증 기준으로 삼는 팔레트 그 자체이므로,
        // 바깥에는 인덱서 set이 없는 IReadOnlyList로만 노출한다.
        static readonly Color[] AllowedEmissionColors =
        {
            LightShaft, Glowshroom, Flame, Macronium, MacroniumHighlight,
        };

        /// <summary>발광이 허용되는 색. 이 밖의 Emission은 위반이다.</summary>
        public static IReadOnlyList<Color> AllowedEmission => AllowedEmissionColors;

        /// <summary>0xRRGGBB 정수를 불투명 Color로. 스펙의 hex를 그대로 옮겨 쓰기 위한 것이다.</summary>
        public static Color FromHex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }
}
