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
        }

        // ── 조회 ────────────────────────────────────────────────

        public static string T(string category, string key) => T(new LocKey(category, key));

        public static string T(LocKey key)
        {
            if (_current != null && _current.TryGetValue(key, out var value)) return value;
            if (_fallback != null && _fallback.TryGetValue(key, out value)) return value;

            // 마지막 자리. 빈 문자열은 절대 돌려주지 않는다.
            if (key.Key.Length > 0) return key.Key;
            if (key.Category.Length > 0) return key.Category;
            return "?";
        }

        /// <summary>
        /// 표에 있으면 꺼내고, 없으면 <c>false</c>다. <see cref="T(LocKey)"/>와 달리
        /// <b>키를 대신 내주지 않는다</b>.
        ///
        /// 데이터 에셋 경로(<see cref="DataText"/>)가 이것을 쓴다. 그쪽의 마지막 폴백은
        /// 키가 아니라 <b>에셋에 적힌 한국어 원문</b>이라, 표에 있는지 없는지를
        /// 먼저 알아야 한다. 조회 비용은 <see cref="T(LocKey)"/>와 같다.
        /// </summary>
        public static bool TryT(LocKey key, out string value)
        {
            if (_current != null && _current.TryGetValue(key, out value)) return true;
            if (_fallback != null && _fallback.TryGetValue(key, out value)) return true;
            value = null;
            return false;
        }

        /// <summary>
        /// 서식 인자를 끼운다. 서식이 번역과 어긋나 <c>FormatException</c>이 나면
        /// 번역문을 그대로 낸다 — 화면이 비거나 게임이 멈추는 것보다 낫다.
        /// </summary>
        public static string F(string category, string key, object arg0)
        {
            string format = T(category, key);
            try { return string.Format(format, arg0); }
            catch (FormatException) { return format; }
        }

        public static string F(string category, string key, object arg0, object arg1)
        {
            string format = T(category, key);
            try { return string.Format(format, arg0, arg1); }
            catch (FormatException) { return format; }
        }

        public static string F(string category, string key, object arg0, object arg1, object arg2)
        {
            string format = T(category, key);
            try { return string.Format(format, arg0, arg1, arg2); }
            catch (FormatException) { return format; }
        }

        public static string F(string category, string key, params object[] args)
        {
            string format = T(category, key);
            if (args == null || args.Length == 0) return format;
            try { return string.Format(format, args); }
            catch (FormatException) { return format; }
        }
    }
}
