using System;
using System.Collections.Generic;

namespace Survive.Localization
{
    /// <summary>
    /// 화면에 나갈 글자를 꺼내는 자리. 이름이 짧은 것은 214곳에 들어갈 것이기 때문이다.
    ///
    /// <code>
    /// Loc.T("UI", "craft_empty")            // 현재 로케일
    /// Loc.F("UI", "battery_percent", 62)    // 서식 인자
    /// </code>
    ///
    /// <b>반드시 동기다.</b> 제작 화면은 열려 있는 동안 매 프레임 목록을 다시 그린다.
    /// 조회 하나라도 비동기가 되면 그 자리는 한 프레임 비었다가 채워진다.
    /// 그래서 표는 전부 메모리에 올려 두고, 조회는 사전 한 번 뒤짐이다.
    ///
    /// <b>폴백 사슬은 셋이다.</b> 현재 로케일 → 기본 로케일(ko) → 키 자체.
    /// 어느 단계에서도 빈 문자열이나 예외를 내지 않는다 — 번역이 빠졌다고 화면이
    /// 비면 무엇이 빠졌는지조차 보이지 않는다. 키가 그대로 뜨면 적어도 어디를
    /// 고쳐야 하는지가 보인다.
    ///
    /// 표를 읽어 오는 일은 여기 없다. <c>Survive.Localization.LocalizationBootstrap</c>
    /// (MonoBehaviour 쪽)이 Resources에서 읽어 <see cref="Load"/>로 밀어 넣는다.
    /// </summary>
    public static class Loc
    {
        static StringCatalog _catalog = StringCatalog.Empty;
        static string _locale = StringCatalog.DefaultLocale;

        // 두 표를 직접 붙들고 있는 이유는 조회 경로에서 로케일 이름으로 사전을
        // 한 번 더 뒤지지 않기 위해서다.
        static IReadOnlyDictionary<LocKey, string> _current;
        static IReadOnlyDictionary<LocKey, string> _fallback;

        /// <summary>
        /// 지금 표의 글이 한국어인가. 조사 처리를 켤지 말지가 여기에 달렸다.
        /// 로케일 이름이 아니라 <b>값이 어느 표에서 나왔는가</b>로 정한다 —
        /// en 표에 없는 키는 ko로 폴백하고, 그 문장에는 조사가 붙어 있다.
        /// </summary>
        static bool _currentIsKorean;

        static readonly bool FallbackIsKorean =
            KoreanParticles.IsKoreanLocale(StringCatalog.DefaultLocale);

        /// <summary>
        /// 로케일이 바뀌었거나 표를 새로 읽었다. 이미 그려 둔 글자를 다시 쓰라는 신호다.
        /// 매 프레임 다시 그리는 화면은 구독할 필요가 없고, 한 번 쓰고 마는
        /// 정적 캡션(버튼 글자 등)만 이것을 듣는다.
        /// </summary>
        public static event Action LocaleChanged;

        public static StringCatalog Catalog => _catalog;

        public static string CurrentLocale => _locale;

        public static bool IsPseudo =>
            string.Equals(_locale, StringCatalog.PseudoLocale, StringComparison.OrdinalIgnoreCase);

        /// <summary>표가 실제로 실렸는가. 게이트와 부트스트랩이 본다.</summary>
        public static bool IsLoaded => _catalog.Keys.Count > 0;

        /// <summary>고를 수 있는 로케일. 표에 있는 것들 + 의사 번역.</summary>
        public static IReadOnlyList<string> AvailableLocales
        {
            get
            {
                var list = new List<string>(_catalog.Locales);
                list.Add(StringCatalog.PseudoLocale);
                return list;
            }
        }

        /// <summary>표를 갈아 끼운다. 로케일은 그대로 두고 표만 다시 묶는다.</summary>
        public static void Load(StringCatalog catalog)
        {
            _catalog = catalog ?? StringCatalog.Empty;
            Rebind();
            LocaleChanged?.Invoke();
        }

        /// <summary>
        /// 로케일을 바꾼다. 표에 없는 이름을 줘도 막지 않는다 — 그 로케일 표가
        /// 없을 뿐이고 폴백이 전부 기본 로케일로 흐른다.
        /// </summary>
        public static void SetLocale(string locale)
        {
            string next = string.IsNullOrWhiteSpace(locale)
                ? StringCatalog.DefaultLocale
                : locale.Trim();

            if (string.Equals(next, _locale, StringComparison.OrdinalIgnoreCase)) return;

            _locale = next;
            Rebind();
            LocaleChanged?.Invoke();
        }

        static void Rebind()
        {
            _fallback = _catalog.TableFor(StringCatalog.DefaultLocale);

            // 의사 번역은 표에 없다. 기본 로케일 표를 통째로 부풀려 한 번 만들어 두고,
            // 그 뒤로는 다른 로케일과 똑같이 사전 한 번 뒤짐이 된다.
            _current = IsPseudo
                ? PseudoLocalizer.BuildTable(_fallback)
                : _catalog.TableFor(_locale);

            // 의사 번역은 기본 로케일 값을 부풀린 것이라 글의 언어는 그쪽을 따른다.
            _currentIsKorean = IsPseudo ? FallbackIsKorean : KoreanParticles.IsKoreanLocale(_locale);
        }

        // ── 조회 ────────────────────────────────────────────────

        public static string T(string category, string key) => T(new LocKey(category, key));

        public static string T(LocKey key) => Lookup(key, out _);

        /// <summary>
        /// 표에서 글을 꺼내면서 <b>그 글이 한국어인가</b>를 함께 알린다.
        /// 조사 처리를 켤지 말지가 그 한 가지에 달렸다.
        /// </summary>
        static string Lookup(LocKey key, out bool korean)
        {
            if (_current != null && _current.TryGetValue(key, out var value))
            {
                korean = _currentIsKorean;
                return value;
            }
            if (_fallback != null && _fallback.TryGetValue(key, out value))
            {
                korean = FallbackIsKorean;
                return value;
            }

            // 마지막 자리. 빈 문자열은 절대 돌려주지 않는다.
            // 키를 그대로 내는 자리라 조사를 붙일 글이 아니다.
            korean = false;
            if (key.Key.Length > 0) return key.Key;
            if (key.Category.Length > 0) return key.Category;
            return "?";
        }

        /// <summary>
        /// 자리표 <c>{0} {1} {2}</c>에 값을 끼운다.
        ///
        /// <b>값만 넘길 수 있다.</b> 숫자, 데이터에서 온 이름, 서식된 시간 같은 것.
        /// 코드에 적은 문자열 리터럴(<c>"개"</c>, <c>", "</c>)을 넘기면 안 된다 —
        /// 그것은 값이 아니라 말이고, 말은 표 안에 있어야 번역가가 어순을 바꿀 수 있다.
        /// EditMode 게이트(<c>LocSentenceRules</c>)가 이것을 막는다.
        ///
        /// <b>절대 예외를 던지지 않는다.</b> 인자가 모자라도 남아도 마찬가지다
        /// (<see cref="LocFormat"/>).
        /// </summary>
        public static string F(string category, string key, object arg0)
        {
            string format = Lookup(new LocKey(category, key), out bool korean);
            return LocFormat.Apply(format, LocArgs.Of(arg0), Particles(korean));
        }

        public static string F(string category, string key, object arg0, object arg1)
        {
            string format = Lookup(new LocKey(category, key), out bool korean);
            return LocFormat.Apply(format, LocArgs.Of(arg0, arg1), Particles(korean));
        }

        public static string F(string category, string key, object arg0, object arg1, object arg2)
        {
            string format = Lookup(new LocKey(category, key), out bool korean);
            return LocFormat.Apply(format, LocArgs.Of(arg0, arg1, arg2), Particles(korean));
        }

        public static string F(string category, string key, params object[] args)
        {
            string format = Lookup(new LocKey(category, key), out bool korean);
            return LocFormat.Apply(format, LocArgs.Of(args), Particles(korean));
        }

        /// <summary>
        /// 조사 해석기. 한국어가 아니면 null을 넘겨 처리 자체를 끈다 —
        /// en 문장의 글자를 조사로 오인할 자리를 아예 만들지 않는다.
        /// </summary>
        static IParticleResolver Particles(bool korean) => korean ? KoreanParticles.Standard : null;
    }
}
