using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Survive.Items;
using Survive.Localization;
using Survive.Narrative;
using Survive.Progression;

/// <summary>
/// <b>우주복 AI가 말하지 않을 것을 못 박는 게이트</b> (기획서 §12 · 스펙 §13).
///
/// <b>이 저장소는 이미 규칙을 지키고 있다.</b> 게임 안에 배부름을 설명하는 말은
/// 한 글자도 없고, 방향을 알려 주는 대사도 없다. 코드가 문서보다 먼저 원칙을
/// 지키고 있던 자리라, 여기서 할 일은 고치는 것이 아니라 <b>되돌아갈 수 없게
/// 만드는 것</b>이다. 지키고 있다는 사실은 게이트가 없으면 하루아침에 사라진다 —
/// 대사 한 줄이면 되고, 그 줄은 아무 오류도 내지 않는다.
///
/// <b>지키는 것 셋.</b>
/// <list type="number">
/// <item><b>재료 해금까지만 말한다.</b> 생태계 판단(배가 찬 개체 · 잔해를 거둬 가는
///       개체 · 물이끼 단서)은 절대 말하지 않는다. 말하는 순간 차별화 축 ①
///       — 생태계를 <b>관찰해서</b> 이득을 본다 — 이 죽는다. AI가 결론을 주면
///       관찰할 이유가 사라지고, 관찰할 이유가 없으면 생태계는 배경이 된다.</item>
/// <item><b>낫 경고는 존재와 접근까지.</b> 방향·위치는 말하지 않는다.
///       <b>AI가 대신 봐 주면 어둠이 무의미해진다.</b> 어둠이 위협인 것은
///       무엇이 어디 있는지 모르기 때문이고, 좌표를 읽어 주는 순간 그것은
///       어두운 방이 아니라 미니맵이 켜진 방이다.</item>
/// <item><b>문체 규격.</b> 세 문장 이내, 관찰투, 감탄·의인화·물음·그림글자 금지,
///       아이템 이름을 되뇌지 않는다. 규격 원본은 <see cref="DiscoverySO"/>의
///       문서 주석과 <c>2026-08-05-techtree-design.md</c> §2.</item>
/// </list>
///
/// <b>무엇을 훑는가 — 눈이 멀지 않게 두 길로 본다.</b>
/// <list type="bullet">
/// <item><b>에셋</b>: <c>Assets/08.Data</c>의 모든 ScriptableObject에서
///       <see cref="SequenceSO.Line"/> 필드를 <b>반사로</b> 찾는다. 종류를 열거하지
///       않는 것이 핵심이다 — 다음 라운드에 낫 경고 SO가 생겨도 아무도 이 파일을
///       고칠 필요가 없고, 고치는 것을 잊어서 눈이 머는 일도 없다.</item>
/// <item><b>번역 표</b>: <c>.text</c>로 끝나는 열쇠 중 짝이 되는 <c>.speaker</c>가
///       우주복 AI인 것 전부를, <b>모든 로케일</b>에서. 구역 이름으로 고르지 않는
///       것도 같은 이유다 — 새 구역이 생겨도 따라온다.</item>
/// </list>
/// 화면에 실제로 나가는 것은 표 쪽이고 에셋은 폴백이다. 둘 다 봐야 한 쪽만 고친
/// 되살림을 잡는다.
/// </summary>
public class AiSpeechGateTests
{
    /// <summary>이 화자의 말만 이 게이트가 다룬다. 표와 에셋을 잇는 유일한 표식이다.</summary>
    const string AI화자 = "우주복 AI";

    // ── 금지어 ──────────────────────────────────────────────────
    //
    // 목록은 전부 조각으로 짓는다("배부" + "른" 꼴). 두 가지를 막으려는 것이다.
    //   1. 이 파일에 적은 낱말이 그대로 번역 표에 복사되어 되살아나는 일.
    //      직전 라운드의 도감 미분류 게이트가 같은 처방을 썼다.
    //   2. 저장소 전체를 훑는 다른 게이트(RetiredContentGateTests 등)가
    //      언젠가 같은 낱말을 찾게 되었을 때 검사 파일 자신이 걸리는 일.
    //
    // 넓게 잡지 않는다. 훑개가 너무 넓으면 멀쩡한 대사가 걸리고, 그러면 아무도
    // 못 고쳐 결국 예외 목록이 생긴다. 예외 목록이 생긴 게이트는 게이트가 아니다.

    /// <summary>
    /// 생태계 판단어. <b>AI가 관찰의 결론을 대신 내리는 말들</b>이다.
    ///
    /// <b>맨낱말 "회수"는 넣지 않았다.</b> 금지하려는 것은 회수라는 낱말이 아니라
    /// <b>"저 개체가 잔해를 거둬 간다"는 판정</b>이라, 그 판정이 되는 꼴만 막는다.
    /// 프롤로그가 "기체는 회수 불가 상태입니다"라고 말하던 시절에 실제로 걸렸다.
    ///
    /// <b>맨낱말 "잔해"도 넣지 않았다.</b> 연구 대사가 공을 두고 "죽은 기계의 잔해를
    /// 삼켜 에너지로 바꾸는 것으로 보입니다"라고 말하는데, 이것은 <b>관찰 보고</b>이지
    /// 이득의 결론이 아니다. 게다가 연구는 유물과 스크랩을 치르고 산 정보다 —
    /// 공짜로 흘리는 것과 값을 치르고 얻는 것은 다른 일이다(스펙 §2 채널 둘).
    /// </summary>
    static readonly string[] 생태계_판단어 =
    {
        // 배가 찬 개체 — 게임 안에 한 글자도 없어야 하는 말. 관찰로만 알아야 한다.
        "배부" + "른", "배부" + "름", "배가 " + "부른", "포" + "만", "축" + "적량", "축" + "적한",

        // 잔해를 거둬 가는 개체.
        "잔해" + " 회수", "잔해" + "를 회수", "사체" + " 회수", "사체" + "를 회수",
        "회수" + "해 갑니다", "회수" + "하는 개체", "회수" + "하는 것으로",

        // 이끼 단서.
        "이" + "끼",

        // 영양 단계. 도감에서 걷어낸 것이 대사로 돌아오면 같은 것을 잃는다.
        "분해" + "자", "생산" + "자", "소비" + "자", "영양 " + "단계", "먹이 " + "사슬",

        // 이득의 결론. "관찰하면 좋다"까지가 AI의 몫이 아니다.
        "이" + "득입니다", "더 " + "많이 나옵니다", "더 " + "많은 스크랩",
    };

    /// <summary>
    /// 방향 지시어. <b>낫 경고가 존재와 접근에서 멈추게 하는 자물쇠다.</b>
    ///
    /// <b>아직 낫 경고 대사는 없다 — 이번 라운드에서 붙이지 않았다.</b> 기획서 §12가
    /// 말하는 3단(레이더 감지 → 접근 경고 → 실제 조우)은 지금 셋 다 걸 자리가 없다.
    /// <list type="number">
    /// <item><b>낫이 레이더에 잡히지 않는다.</b> <c>IRadarContactSource</c>를 구현한 것은
    ///       <c>RadarLandmark</c>(가만히 있는 표식)뿐이라, 생물은 접촉으로 등록되지
    ///       않는다. 1단은 대사 이전에 감지가 없다.</item>
    /// <item><b>발령을 거는 것이 아무것도 없다.</b> <c>ScytheAlert</c>는 자리만 열려
    ///       있고 값을 올리는 것은 E2E 하네스뿐이다(스펙 §5가 다음 라운드다).
    ///       상태가 없으면 경고할 상태 변화도 없다.</item>
    /// <item><b>되풀이해 말할 채널이 없다.</b> <c>UnlockService.Announce</c>는 원장에
    ///       걸린 <b>한 번짜리</b> 알림이고 빈도 제한도 재발화도 없다. 경고를 거기
    ///       얹으면 처음 한 번 말하고 영영 침묵한다.</item>
    /// </list>
    /// 억지로 붙이면 셋 중 어느 것도 시험할 수 없는 배선이 남는다. 그래서 이번에는
    /// <b>규칙과 자물쇠만</b> 세운다. 지금 이 목록은 0건을 지키고, 값을 하는 것은
    /// <b>대사가 생기는 날</b>이다 — 그날 이 게이트는 이미 서 있고, 훑개가 종류를
    /// 열거하지 않으므로 낫 경고 SO가 새로 생겨도 자동으로 검사 안에 들어온다.
    ///
    /// <b>"위"와 "아래"는 넣지 않았다.</b> 깊이는 방향이 아니라 이 게임의 축이고,
    /// 실제로 "지각 아래 밀도 이상 분석"과 "위에서 덮쳐 먹이를 가두는" 같은
    /// 멀쩡한 관찰 보고가 그 낱말을 쓴다. 막으려는 것은 <b>수평면에서 어느 쪽인가</b>다.
    /// </summary>
    static readonly string[] 방향_지시어 =
    {
        "왼" + "쪽", "오른" + "쪽", "좌" + "측", "우" + "측", "뒤" + "쪽", "등 " + "뒤",
        "북" + "쪽", "남" + "쪽", "동" + "쪽", "서" + "쪽",
        "북" + "동", "북" + "서", "남" + "동", "남" + "서",
        "저" + "기에", "저" + "쪽", "그" + "쪽", "이" + "쪽",
        "정" + "면", "측" + "면", "방" + "위", "좌" + "표", "시 " + "방향",
        "방" + "향", "위" + "치",

        // 화살표는 낱말 없이 방향을 말한다.
        "→", "←", "↑", "↓", "↖", "↗", "↘", "↙",
    };

    /// <summary>
    /// 감탄·의인화 어미. <see cref="DiscoverySO"/> 문서 주석의 금지 항목 그대로다.
    /// 하나만 사람처럼 말해도 목소리가 무너진다.
    /// </summary>
    static readonly string[] 사람_말투 =
    {
        "겠" + "어", "네" + "요", "흥" + "미", "군" + "요", "로" + "구나", "네" + "그려",
        "안타" + "깝", "다" + "행이", "걱" + "정", "미" + "안",
    };

    /// <summary>
    /// 대사를 닫는 정형구. 하나가 아닌 이유는 <b>무엇을 알게 되었는지가 다르기</b>
    /// 때문이다 — 주운 것(현장 발견)과 밝혀낸 것(연구)은 같은 문장으로 말할 수 없고,
    /// 물질을 얻은 것과 개체를 알게 된 것도 다르다.
    /// </summary>
    static readonly string[] 정형구 =
    {
        "해당 물질을 사용한 제작법이 있습니다.",   // 현장 발견 — 재료
        "해당 장비의 제작법이 있습니다.",          // 현장 발견 — 자리(레이더)
        "해당 구조를 적용한 제작법을 확보했습니다.", // 연구 — 만드는 법
        "해당 개체의 정보가 정리되었습니다.",      // 연구 — 도감
    };

    /// <summary>용도 판정은 단정이 아니라 관찰 보고다.</summary>
    static readonly string[] 관찰투 = { "것으로 보입니다", "판단됩니다" };

    string _처음로케일;

    [SetUp]
    public void 로케일을_기억해_둔다() => _처음로케일 = Loc.CurrentLocale;

    [TearDown]
    public void 로케일을_되돌린다() => Loc.SetLocale(_처음로케일);

    // ── ① 금지어 전수 검사 ──────────────────────────────────────

    /// <summary><b>이 게이트가 이 파일의 알맹이다.</b> 차별화 축 ①을 지킨다.</summary>
    [Test]
    public void AI가_하는_모든_말에_생태계_판단어가_없다()
    {
        var 걸린것 = 걸리는_자리(AI가_하는_모든_말(), 생태계_판단어);

        Assert.IsEmpty(걸린것,
            $"AI가 생태계의 결론을 대신 내리고 있다 ({걸린것.Count}군데). " +
            "힌트를 주려면 결론이 아니라 관찰까지만이다 (기획서 §12):\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary><b>어둠을 지킨다.</b> AI가 대신 봐 주면 어둠이 무의미해진다.</summary>
    [Test]
    public void AI가_하는_모든_말에_방향_지시어가_없다()
    {
        var 걸린것 = 걸리는_자리(AI가_하는_모든_말(), 방향_지시어);

        Assert.IsEmpty(걸린것,
            $"AI가 방향이나 위치를 알려 주고 있다 ({걸린것.Count}군데). " +
            "경고는 존재와 접근까지다 (기획서 §12):\n  " +
            string.Join("\n  ", 걸린것));
    }

    // ── ② 음성 확인 — 훑개가 살아 있는가 ────────────────────────

    /// <summary>
    /// 위 둘은 초록불일 때 아무 말도 하지 않는다. 모으는 쪽이 조용히 망가져 늘 빈
    /// 목록을 내도 통과한다. 그래서 <b>훑는 자리에 진짜 AI의 말이 들어 있는지</b>를
    /// 따로 못 박는다.
    /// </summary>
    [Test]
    public void 훑는_자리에_진짜_AI의_말이_들어_있다()
    {
        var 말들 = AI가_하는_모든_말();

        Assert.Greater(말들.Count, 20, "모은 것이 너무 적다 — 실제 대사를 못 읽었을 수 있다");
        Assert.IsTrue(말들.Any(m => m.글.Contains("외계 금속물체 분석")), "현장 발견 대사를 못 담았다");
        Assert.IsTrue(말들.Any(m => m.글.Contains("해당 구조를 적용한 제작법을 확보했습니다")),
            "연구 대사를 못 담았다");
        Assert.IsTrue(말들.Any(m => m.글.Contains("착륙 완료")), "연출 자막을 못 담았다");
        Assert.IsTrue(말들.Any(m => m.자리.StartsWith("에셋")), "에셋 쪽을 못 담았다");
        Assert.IsTrue(말들.Any(m => m.자리.StartsWith("표")), "번역 표 쪽을 못 담았다");
    }

    /// <summary>
    /// <b>두 길이 같은 것을 본다.</b> 에셋에만 있고 표에 없는 대사가 생기면
    /// (또는 그 반대면) 한쪽 게이트가 그만큼 눈이 먼다.
    /// </summary>
    [Test]
    public void 에셋의_대사와_표의_대사가_한_줄도_어긋나지_않는다()
    {
        var 에셋글 = new HashSet<string>(에셋에_적힌_AI의_말().Select(m => m.글));
        var 표글 = new HashSet<string>(
            표에_적힌_AI의_말(StringCatalog.DefaultLocale).Select(m => m.글));

        Assert.IsNotEmpty(에셋글, "에셋에서 AI 대사를 하나도 못 찾았다 — 반사 훑개가 죽었다");
        CollectionAssert.AreEquivalent(에셋글, 표글,
            "에셋과 번역 표의 AI 대사가 다르다. 한쪽만 고치면 다른 쪽 게이트가 눈이 먼다");
    }

    /// <summary>
    /// <b>반사 훑개가 종류를 열거하지 않는다는 것</b>을 못 박는다. 지금 대사를 들고 있는
    /// SO는 셋인데, 셋을 <c>switch</c>로 적어 두면 넷째가 생기는 날 조용히 새어 나간다.
    /// </summary>
    [Test]
    public void 대사를_든_SO의_종류를_열거하지_않고_찾아낸다()
    {
        var 종류 = new HashSet<string>(에셋에_적힌_AI의_말().Select(m => m.종류));

        CollectionAssert.IsSupersetOf(종류,
            new[] { nameof(DiscoverySO), nameof(ResearchEntrySO), nameof(SequenceSO) },
            "지금 아는 세 종류조차 반사로 못 찾는다");

        // 훑개가 무엇을 찾는지 — 종류 이름이 아니라 필드의 자료형이다.
        var 대사필드 = typeof(DiscoverySO)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Count(f => f.FieldType == typeof(SequenceSO.Line));
        Assert.AreEqual(1, 대사필드,
            "DiscoverySO의 대사 필드가 SequenceSO.Line이 아니게 되었다 — 반사 훑개가 못 본다");
    }

    /// <summary><b>음성 확인.</b> 금지어를 일부러 되살려 놓고 게이트가 무너지는지 본다.</summary>
    [Test]
    public void 생태계_판단어를_되살리면_게이트가_무너진다()
    {
        foreach (var 말 in 생태계_판단어)
        {
            var 시험체 = new[] { (자리: "시험체", 종류: "probe", 화자: AI화자, 글: $"관측 결과 {말} 상태로 보입니다.") };
            Assert.IsNotEmpty(걸리는_자리(시험체, 생태계_판단어),
                $"\"{말}\"을 되살렸는데 게이트가 잡지 못한다 — 0건은 아무 뜻이 없다");
        }
    }

    /// <summary><b>음성 확인 둘.</b> 방향 쪽도 같은 방식으로 확인한다.</summary>
    [Test]
    public void 방향_지시어를_되살리면_게이트가_무너진다()
    {
        foreach (var 말 in 방향_지시어)
        {
            var 시험체 = new[] { (자리: "시험체", 종류: "probe", 화자: AI화자, 글: $"접근 개체 {말} 감지.") };
            Assert.IsNotEmpty(걸리는_자리(시험체, 방향_지시어),
                $"\"{말}\"을 되살렸는데 게이트가 잡지 못한다");
        }
    }

    /// <summary>
    /// <b>금지어 목록이 서로를 잡아먹지 않는가.</b> 어느 낱말이 실제 대사를 걸면
    /// 다음 사람은 대사를 고치지 않고 목록을 깎는다. 그래서 <b>지금 대사 중
    /// 아슬아슬한 것</b>이 무엇인지를 검사 스스로 알고 있게 한다.
    /// </summary>
    [Test]
    public void 금지어가_멀쩡한_대사를_잡는_일이_없다()
    {
        // 실제로 있는, 걸리면 안 되는 말들. 목록을 넓히다가 이것들이 걸리면 즉시 안다.
        var 멀쩡한것 = new[]
        {
            "착륙 완료. 성간 항행에 쓸 연료는 남지 않았습니다.",
            "구형 외피와 안쪽 흡입 흠집 분석... 죽은 기계의 잔해를 삼켜 에너지로 바꾸는 것으로 보입니다.",
            "지각 아래 밀도 이상 분석... 추가 지하 구조물이 존재하는 것으로 보입니다.",
            "사족 관절과 상부 포획 구조 분석... 위에서 덮쳐 먹이를 가두는 것으로 보입니다.",
            "액체를 밀어내는 표면 구조 분석... 하중을 면 전체로 흩어 가라앉지 않는 것으로 보입니다.",
            "단안 광학계와 부양 추진부 분석... 죽은 조직을 태워 에너지를 꺼내는 것으로 보입니다.",
        };

        var 시험체 = 멀쩡한것.Select((s, i) => (자리: $"멀쩡한 대사 {i}", 종류: "probe", 화자: AI화자, 글: s)).ToArray();

        Assert.IsEmpty(걸리는_자리(시험체, 생태계_판단어),
            "생태계 판단어 목록이 멀쩡한 대사를 잡는다 — 목록이 너무 넓다");
        Assert.IsEmpty(걸리는_자리(시험체, 방향_지시어),
            "방향 지시어 목록이 멀쩡한 대사를 잡는다 — 목록이 너무 넓다");
    }

    // ── ③ 문체 규격 전수 검사 ───────────────────────────────────

    [Test]
    public void 모든_대사가_세_문장을_넘지_않는다()
    {
        var 걸린것 = AI가_하는_모든_말()
            .Where(m => AiVoice.SentenceCount(m.글) > AiVoice.MaxSentences)
            .Select(m => $"{m.자리}: {AiVoice.SentenceCount(m.글)}문장 \"{m.글}\"")
            .ToList();

        Assert.IsEmpty(걸린것,
            $"세 문장을 넘는 대사가 있다 ({걸린것.Count}군데). 효율이 기조다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    [Test]
    public void 모든_대사에_그림글자가_없다()
    {
        var 걸린것 = AI가_하는_모든_말()
            .Where(m => AiVoice.HasEmoji(m.글))
            .Select(m => $"{m.자리}: \"{m.글}\"")
            .ToList();

        Assert.IsEmpty(걸린것, "AI 대사에 그림글자가 있다. 기계는 이모지를 쓰지 않는다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    [Test]
    public void 모든_대사에_물음표와_느낌표가_없다()
    {
        var 걸린것 = AI가_하는_모든_말()
            .Where(m => m.글.Contains("?") || m.글.Contains("!"))
            .Select(m => $"{m.자리}: \"{m.글}\"")
            .ToList();

        Assert.IsEmpty(걸린것,
            "AI 대사에 물음이나 감탄이 있다. 분석기는 묻지도 놀라지도 않는다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    [Test]
    public void 모든_대사가_사람_말투를_쓰지_않는다()
    {
        var 걸린것 = 걸리는_자리(AI가_하는_모든_말(), 사람_말투);

        Assert.IsEmpty(걸린것,
            $"AI가 사람처럼 말한다 ({걸린것.Count}군데). 하나만 무너져도 목소리가 무너진다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    [Test]
    public void 모든_대사의_화자가_우주복_AI다()
    {
        // 훑개가 화자로 대사를 고르므로 표 쪽은 정의상 통과한다. 값을 하는 것은
        // 에셋 쪽이다 — 에셋에 적힌 화자가 표와 어긋나면 폴백이 다른 이름을 낸다.
        var 걸린것 = 에셋에_적힌_AI의_말()
            .Where(m => m.화자 != AI화자)
            .Select(m => $"{m.자리}: 화자가 \"{m.화자}\"")
            .ToList();

        Assert.IsEmpty(걸린것, "우주복 AI가 아닌 화자가 있다:\n  " + string.Join("\n  ", 걸린것));
    }

    // ── ④ 분석 대사의 형식 ──────────────────────────────────────
    //
    // 여기부터는 현장 발견·연구 대사만 본다. 프롤로그 자막은 분석 보고가 아니라
    // 상황 통보라 세 문장 형식을 요구할 수 없다 — 요구하면 "착륙 완료"가
    // 규격 위반이 되고, 그것은 규격이 틀린 것이다.

    [Test]
    public void 분석_대사는_분석_말줄임표로_연다()
    {
        var 걸린것 = 분석_대사()
            .Where(m => !m.글.Contains("분석..."))
            .Select(m => $"{m.자리}: \"{m.글}\"")
            .ToList();

        Assert.IsEmpty(걸린것,
            "분석 대사가 \"분석...\"으로 열지 않는다. 말줄임표는 아직 처리 중이라는 표시다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    [Test]
    public void 분석_대사는_관찰투로_판정한다()
    {
        var 걸린것 = 분석_대사()
            .Where(m => !관찰투.Any(t => m.글.Contains(t)))
            .Select(m => $"{m.자리}: \"{m.글}\"")
            .ToList();

        Assert.IsEmpty(걸린것,
            "용도 판정이 단정이다. 관찰 보고여야 한다 (\"~것으로 보입니다\"):\n  " +
            string.Join("\n  ", 걸린것));
    }

    [Test]
    public void 분석_대사는_정해진_정형구로_닫는다()
    {
        var 걸린것 = 분석_대사()
            .Where(m => !정형구.Any(f => m.글.TrimEnd().EndsWith(f, StringComparison.Ordinal)))
            .Select(m => $"{m.자리}: \"{m.글}\"")
            .ToList();

        Assert.IsEmpty(걸린것,
            "해금 안내가 정형구가 아니다. 정형구를 늘리려면 왜 새 종류인지가 먼저다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// <b>해금 통보는 그 문장 하나로 선다</b> (2026-08-07 회신 ⑯).
    ///
    /// 분석 결과와 해금 통보는 <b>별개의 보고</b>다 — 하나는 방금 본 것이고 하나는
    /// 그래서 무엇을 만들 수 있게 되었는가다. 레이더 발견 대사가 그 둘을 접속사로
    /// 이어 붙여 <i>"…탐색을 권장하며, 해당 장비의 제작법이 있습니다"</i>라고 말하고
    /// 있었다. 붙여 놓으면 정형구가 앞말에 딸린 종속절이 되어, 화면을 보는 사람은
    /// <b>무엇이 열렸는지</b>를 문장 중간에서 주워 담아야 한다.
    ///
    /// <b>정형구가 마지막 문장의 전부인가</b>로 판정한다. 끝만 보면
    /// (<see cref="분석_대사는_정해진_정형구로_닫는다"/>) 접속사로 이어 붙인 것이
    /// 그대로 통과한다 — 실제로 통과하고 있었다.
    /// </summary>
    [Test]
    public void 해금_통보가_다른_보고와_한_문장에_묶이지_않는다()
    {
        var 걸린것 = 분석_대사()
            .Where(m => !정형구가_문장_하나를_통째로_이룬다(m.글))
            .Select(m => $"{m.자리}: \"{AiVoice.SplitSentences(m.글).LastOrDefault()}\"")
            .ToList();

        Assert.IsEmpty(걸린것,
            $"해금 통보가 다른 보고에 이어 붙어 있다 ({걸린것.Count}군데). " +
            "제안과 해금 통보는 별개의 보고이므로 별개의 문장으로 (회신 ⑯):\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary><b>음성 확인.</b> 이어 붙인 꼴을 되살리면 이 검사가 무너지는가.</summary>
    [Test]
    public void 정형구를_접속사로_이어_붙이면_걸린다()
    {
        const string 나눈것 = "지각 아래 밀도 이상 분석... 추가 지하 구조물이 존재하는 것으로 보입니다. " +
                            "해당 장비의 제작법이 있습니다.";
        const string 붙인것 = "지각 아래 밀도 이상 분석... 전자파 관측을 통한 탐색을 권장하며, " +
                            "해당 장비의 제작법이 있습니다.";

        Assert.IsTrue(정형구가_문장_하나를_통째로_이룬다(나눈것), "나눠 놓은 것을 걸어 버린다");
        Assert.IsFalse(정형구가_문장_하나를_통째로_이룬다(붙인것),
            "접속사로 이어 붙인 것을 못 잡는다 — 끝만 보면 이것도 정형구로 닫힌다");

        // 끝만 보는 검사는 둘을 구별하지 못한다. 그것이 이 검사를 따로 세운 이유다.
        Assert.IsTrue(정형구.Any(f => 붙인것.EndsWith(f, StringComparison.Ordinal)),
            "본보기가 정형구로 닫히지 않는다 — 시험체가 틀렸다");
    }

    /// <summary>마지막 문장이 정형구 그 자체인가. 앞에 아무것도 딸려 있으면 안 된다.</summary>
    static bool 정형구가_문장_하나를_통째로_이룬다(string 글)
    {
        var 문장들 = AiVoice.SplitSentences(글);
        if (문장들.Count == 0) return false;

        // SplitSentences는 마침표를 떼어 낸다. 정형구는 마침표까지 포함하므로 되붙인다.
        string 마지막 = 문장들[문장들.Count - 1].Trim() + ".";
        return 정형구.Any(f => string.Equals(마지막, f, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>아이템 이름을 되뇌지 않는다.</b> "스크랩으로 분석됨"은 틀렸다 — 이름을
    /// 부르는 것은 이해가 아니다. 분석기는 라벨을 모르고 성질만 본다.
    /// </summary>
    [Test]
    public void 분석_대사가_아이템_이름을_되뇌지_않는다()
    {
        var 걸린것 = new List<string>();

        foreach (var d in 모든_에셋<DiscoverySO>())
        {
            string 글 = DataText.Line(d);
            if (string.IsNullOrEmpty(글) || d.item == null) continue;
            string 이름 = DataText.Name(d.item);
            if (!string.IsNullOrEmpty(이름) && 글.Contains(이름))
                걸린것.Add($"{d.name}: \"{이름}\"을 되뇐다");
        }

        foreach (var e in 모든_에셋<ResearchEntrySO>())
        {
            string 글 = DataText.Line(e);
            if (string.IsNullOrEmpty(글)) continue;
            foreach (var 재료 in e.materials ?? new ItemStack[0])
            {
                if (재료?.item == null) continue;
                string 이름 = DataText.Name(재료.item);
                if (!string.IsNullOrEmpty(이름) && 글.Contains(이름))
                    걸린것.Add($"{e.name}: \"{이름}\"을 되뇐다");
            }
        }

        Assert.IsEmpty(걸린것,
            "AI가 아이템 이름을 부른다. 분석기는 자기가 본 성질을 말한다:\n  " +
            string.Join("\n  ", 걸린것));
    }

    /// <summary>
    /// <b>음성 확인 셋.</b> 규격을 어긴 대사를 넣으면 문체 검사가 실제로 실패하는가.
    /// 검사마다 하나씩 어겨 본다 — 하나만 시험하면 나머지 검사가 죽어도 모른다.
    /// </summary>
    [Test]
    public void 문체를_어긴_대사는_반드시_걸린다()
    {
        const string 정상 = "외계 금속물체 분석... 에너지로 사용가능한 것으로 보입니다. 해당 물질을 사용한 제작법이 있습니다.";

        Assert.LessOrEqual(AiVoice.SentenceCount(정상), AiVoice.MaxSentences, "본보기부터 규격을 어긴다");
        Assert.IsFalse(AiVoice.HasEmoji(정상), "본보기에 그림글자가 있다");

        Assert.Greater(AiVoice.SentenceCount(정상 + " 한 문장 더 붙입니다."), AiVoice.MaxSentences,
            "네 문장짜리를 세 문장으로 센다");
        Assert.IsTrue(AiVoice.HasEmoji(정상 + " \U0001F600"), "그림글자를 못 잡는다");

        var 사람처럼 = new[] { (자리: "시험체", 종류: "probe", 화자: AI화자, 글: "쓸 만한 물건이겠어.") };
        Assert.IsNotEmpty(걸리는_자리(사람처럼, 사람_말투), "사람 말투를 못 잡는다");

        // 정형구·관찰투·말줄임표를 각각 빼 보면 그 검사가 실제로 답을 바꾼다.
        Assert.IsFalse(정형구.Any(f => "외계 금속물체 분석... 쓸 수 있습니다.".EndsWith(f, StringComparison.Ordinal)),
            "정형구 없이도 통과한다");
        Assert.IsFalse(관찰투.Any(t => "외계 금속물체 분석... 에너지다. 해당 물질을 사용한 제작법이 있습니다.".Contains(t)),
            "단정투가 관찰투로 통과한다");
    }

    // ── ⑤ 훑개 ──────────────────────────────────────────────────

    /// <summary>표와 에셋을 합친, AI가 하는 모든 말.</summary>
    static List<(string 자리, string 종류, string 화자, string 글)> AI가_하는_모든_말()
    {
        var 모은것 = new List<(string, string, string, string)>();

        foreach (var m in 에셋에_적힌_AI의_말())
            모은것.Add(("에셋 " + m.자리, m.종류, m.화자, m.글));

        var catalog = LocalizationTestBootstrap.LoadCatalogFromDisk();
        foreach (var locale in catalog.Locales)
            foreach (var m in 표에_적힌_AI의_말(locale))
                모은것.Add(("표 " + m.자리, "표", m.화자, m.글));

        return 모은것.Select(t => (자리: t.Item1, 종류: t.Item2, 화자: t.Item3, 글: t.Item4)).ToList();
    }

    /// <summary>
    /// <c>Assets/08.Data</c>의 모든 SO에서 <see cref="SequenceSO.Line"/>을 <b>반사로</b> 찾는다.
    /// 종류를 열거하지 않는 것이 이 훑개의 전부다.
    /// </summary>
    static List<(string 자리, string 종류, string 화자, string 글)> 에셋에_적힌_AI의_말()
    {
        var 모은것 = new List<(string, string, string, string)>();

        foreach (var so in DataTextAssets.FindAll())
        {
            string 경로 = AssetDatabase.GetAssetPath(so);
            string 종류 = so.GetType().Name;

            foreach (var f in so.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(SequenceSO.Line))
                {
                    담는다(모은것, $"{경로} .{f.Name}", 종류, f.GetValue(so) as SequenceSO.Line);
                }
                else if (f.FieldType == typeof(SequenceSO.Line[]))
                {
                    var 줄들 = f.GetValue(so) as SequenceSO.Line[];
                    if (줄들 == null) continue;
                    for (int i = 0; i < 줄들.Length; i++)
                        담는다(모은것, $"{경로} .{f.Name}[{i}]", 종류, 줄들[i]);
                }
            }
        }

        return 모은것.Select(t => (자리: t.Item1, 종류: t.Item2, 화자: t.Item3, 글: t.Item4)).ToList();
    }

    static void 담는다(List<(string, string, string, string)> into, string 자리, string 종류, SequenceSO.Line line)
    {
        if (line == null) return;
        if (string.IsNullOrWhiteSpace(line.text)) return;
        into.Add((자리, 종류, line.speaker, line.text));
    }

    /// <summary>
    /// 이 로케일의 표에서 AI가 하는 말. <b>구역 이름이 아니라 화자로 고른다</b> —
    /// 새 구역이 생겨도 훑개가 따라가야 한다.
    ///
    /// 어떤 열쇠가 AI의 말인지는 <b>기준 로케일</b>에서 정한다. 번역이 덜 된 로케일은
    /// 화자 칸이 비어 있어, 로케일마다 새로 고르면 그쪽이 통째로 검사 밖으로 샌다.
    /// </summary>
    static List<(string 자리, string 화자, string 글)> 표에_적힌_AI의_말(string locale)
    {
        var catalog = LocalizationTestBootstrap.LoadCatalogFromDisk();
        var 기준 = catalog.TableFor(StringCatalog.DefaultLocale);
        var 이표 = catalog.TableFor(locale);

        var 모은것 = new List<(string, string, string)>();

        foreach (var pair in 기준)
        {
            const string 대사접미어 = ".text";
            if (!pair.Key.Key.EndsWith(대사접미어, StringComparison.Ordinal)) continue;

            var 화자열쇠 = new LocKey(pair.Key.Category,
                pair.Key.Key.Substring(0, pair.Key.Key.Length - 대사접미어.Length) + ".speaker");
            if (!기준.TryGetValue(화자열쇠, out var 기준화자)) continue;
            if (기준화자 != AI화자) continue;

            if (!이표.TryGetValue(pair.Key, out var 글) || string.IsNullOrWhiteSpace(글)) continue;
            이표.TryGetValue(화자열쇠, out var 화자);

            모은것.Add(($"{locale} {pair.Key}", 화자, 글));
        }

        return 모은것.Select(t => (자리: t.Item1, 화자: t.Item2, 글: t.Item3)).ToList();
    }

    /// <summary>현장 발견·연구 대사만. 표를 거친 값(화면에 실제로 나가는 것)을 본다.</summary>
    static List<(string 자리, string 글)> 분석_대사()
    {
        var 모은것 = new List<(string 자리, string 글)>();

        foreach (var d in 모든_에셋<DiscoverySO>())
        {
            string 글 = DataText.Line(d);
            if (!string.IsNullOrWhiteSpace(글)) 모은것.Add((d.name, 글));
        }
        foreach (var e in 모든_에셋<ResearchEntrySO>())
        {
            string 글 = DataText.Line(e);
            if (!string.IsNullOrWhiteSpace(글)) 모은것.Add((e.name, 글));
        }

        Assert.IsNotEmpty(모은것, "분석 대사를 하나도 못 찾았다 — 훑개가 헛돈다");
        return 모은것;
    }

    static List<T> 모든_에셋<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(a => a != null)
            .ToList();

    static List<string> 걸리는_자리(
        IEnumerable<(string 자리, string 종류, string 화자, string 글)> 말들, string[] 금지어)
    {
        var 걸린것 = new List<string>();
        foreach (var m in 말들)
        {
            if (string.IsNullOrEmpty(m.글)) continue;
            foreach (var 말 in 금지어)
                if (m.글.IndexOf(말, StringComparison.OrdinalIgnoreCase) >= 0)
                    걸린것.Add($"{m.자리}: \"{m.글.Replace("\n", " / ")}\" ← {말}");
        }
        return 걸린것;
    }
}
