using Survive.Narrative;
using Survive.Progression;

namespace Survive.Localization
{
    /// <summary>
    /// 자막판에 세울 대사 한 줄을 <b>글자가 아니라 줄의 주인으로</b> 들고 다니는 이름표.
    ///
    /// <b>왜 필요한가.</b> <c>UnlockService</c>는 대사를 큐에 세운다. 큐가
    /// <see cref="SequenceSO.Line"/>을 날것으로 물고 있으면 화면에 나가는 글자가
    /// 에셋에서 곧장 오고, 그러면 번역 표를 통째로 우회한다 — 지금은 표와 에셋의
    /// 값이 같아 티가 안 나지만 로케일을 바꾸는 순간 현장 발견·연구 대사만
    /// 한국어로 남는다. 프롤로그 자막이 2026-08-07에 같은 이유로 뚫려 있었다.
    ///
    /// <b>조회 시점이 이 자료형의 알맹이다.</b> 큐에 넣을 때 문자열을 만들어 두지
    /// 않는다. <see cref="Text"/>·<see cref="Speaker"/>는 <b>읽는 그 순간</b> 표를
    /// 뒤진다. 대사는 앞줄이 끝나기를 몇 초씩 기다리는 물건이라, 기다리는 동안
    /// 로케일이 바뀌면 큐에 남은 줄은 새 로케일로 나와야 한다. 넣을 때 굳혀 두면
    /// 그 전환이 이 줄들만 비껴간다.
    ///
    /// <b>왜 공통 창구인가.</b> 주인이 둘이다(<see cref="DiscoverySO"/>·
    /// <see cref="ResearchEntrySO"/>). 큐는 하나이므로 원소 자료형도 하나여야 하고,
    /// 주인마다 큐를 두면 순서를 잡는 자리가 갈라져 두 대사가 서로를 덮어쓴다.
    /// 바깥으로 드러나는 꼴은 <see cref="DataText"/>와 같은 결이다 —
    /// 주인 종류마다 <see cref="Of(DiscoverySO)"/> 과부하가 하나씩 붙는다.
    ///
    /// <b>왜 <c>Domain/Localization</c>에 있는가.</b> 에셋 원문(<c>line.text</c>)을
    /// 읽는 자리이기 때문이다. 그 읽기가 허용되는 곳은 표를 거치는 층 하나뿐이고,
    /// <c>DataTextGateTests.대사_한_줄을_직접_읽는_곳이_없다</c>가 나머지 전부를 막는다.
    /// </summary>
    public readonly struct SpokenLine
    {
        readonly LocKey _speakerKey;
        readonly LocKey _textKey;

        /// <summary>표에 그 줄이 없을 때 물러설 자리. 초안의 원본이자 마지막 그물이다.</summary>
        readonly SequenceSO.Line _asset;

        /// <summary>
        /// <c>default(SpokenLine)</c>과 진짜 줄을 가른다. <c>default(LocKey)</c>는
        /// 두 필드가 <b>null</b>이라 <c>IsEmpty</c>부터 터진다 — 그래서 이름표를
        /// 만졌는지 여부를 따로 들고 있어야 한다.
        /// </summary>
        readonly bool _bound;

        SpokenLine(LocKey speakerKey, LocKey textKey, SequenceSO.Line asset)
        {
            _speakerKey = speakerKey;
            _textKey = textKey;
            _asset = asset;
            _bound = true;
        }

        // ── 주인마다 하나씩 ─────────────────────────────────────

        /// <summary>현장 발견의 대사. 재료를 처음 쥐었거나 그 자리에 처음 닿았을 때.</summary>
        public static SpokenLine Of(DiscoverySO owner) =>
            owner == null
                ? default
                : new SpokenLine(DataText.SpeakerKey(owner), DataText.LineKey(owner), owner.line);

        /// <summary>연구 항목을 끝냈을 때의 대사.</summary>
        public static SpokenLine Of(ResearchEntrySO owner) =>
            owner == null
                ? default
                : new SpokenLine(DataText.SpeakerKey(owner), DataText.LineKey(owner), owner.line);

        /// <summary>
        /// 짜인 시퀀스의 한 줄. <c>SequenceDirector</c>는 줄마다 곧바로 조회하므로
        /// 지금 이것을 부르는 자리는 없다. 큐를 쓰는 연출이 생기면 여기가 그 문이다.
        /// </summary>
        public static SpokenLine Of(SequenceSO owner, int index) =>
            owner == null || owner.lines == null || index < 0 || index >= owner.lines.Length
                ? default
                : new SpokenLine(DataText.SpeakerKey(owner, index), DataText.LineKey(owner, index),
                                 owner.lines[index]);

        // ── 읽기 ────────────────────────────────────────────────

        /// <summary>지금 로케일의 화자 이름. 표가 이기고, 없으면 에셋 원문.</summary>
        public string Speaker => _bound ? DataText.Resolve(_speakerKey, _asset?.speaker) : "";

        /// <summary>지금 로케일의 대사. 표가 이기고, 없으면 에셋 원문.</summary>
        public string Text => _bound ? DataText.Resolve(_textKey, _asset?.text) : "";

        /// <summary>
        /// 이 줄을 유지할 시간(초). <b>번역하지 않는다</b> — 배치 값이지 글자가 아니다.
        /// </summary>
        public float HoldSeconds => _asset == null ? 0f : _asset.holdSeconds;

        /// <summary>
        /// 띄울 것이 없는 줄. <b>표를 거친 값으로 판정한다</b> — 에셋이 비어 있어도
        /// 표에 번역이 있으면 그것은 띄울 것이 있는 줄이다. 반대로 원문만 보면
        /// 그 자리가 곧 표를 우회하는 다음 구멍이 된다.
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
    }
}
