using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 회귀 테스트 — "올라오는 자막에 Panel이 엄청 작아져있다".
///
/// 원인은 <c>SubtitleLine</c>에 붙어 있던 ContentSizeFitter가 잴 것이 없었다는
/// 데 있다. Fitter는 같은 GameObject의 레이아웃 부품만 본다. 거기 있던 것은
/// 9슬라이스 Image 하나뿐이고, 그 최소 높이가 10px이라 판이 10px로 굳었다.
/// 글자는 자식이었으므로 계산에 아예 들어오지 않았다.
///
/// 그래서 이 테스트는 "부품이 이렇게 붙어 있는가"가 아니라 "글자를 넣으면
/// 판이 그만큼 자라는가"를 잰다. 어떤 방식으로 고치든 이 성질만 지키면 된다.
/// </summary>
public class SubtitlePanelLayoutTests
{
    const string PrefabPath = "Assets/05.Prefabs/UI/PanelDialog.prefab";

    GameObject _canvasGo;
    GameObject _panel;
    TMP_Text _label;
    RectTransform _root;

    [SetUp]
    public void SetUp()
    {
        // 캔버스 없이는 레이아웃이 돌지 않는다. 1920x1080 기준으로 세운다.
        _canvasGo = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
        var canvasRect = (RectTransform)_canvasGo.transform;
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, PrefabPath + " 를 찾지 못했다");

        _panel = Object.Instantiate(prefab, _canvasGo.transform);
        _root = (RectTransform)_panel.transform;
        _label = _panel.GetComponentInChildren<TMP_Text>(true);
        Assert.IsNotNull(_label, "자막 글자가 없다");
    }

    [TearDown]
    public void TearDown()
    {
        if (_panel != null) Object.DestroyImmediate(_panel);
        if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
    }

    /// <summary>레이아웃은 한 번으로 안 끝난다 — 너비가 정해져야 글자 높이가 나온다.</summary>
    void Rebuild()
    {
        for (int i = 0; i < 3; i++)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
        }
    }

    RectTransform Plate => (RectTransform)_root.GetChild(0);

    [Test]
    public void 한_줄을_넣으면_판이_글자를_담을_높이를_갖는다()
    {
        _label.text = "우주복 AI : 비상 착륙 완료. 기체는 회수 불가 상태입니다.";
        Rebuild();

        // 예전 결함은 10px였다. 24px 글자에 위아래 여백이 붙은 높이여야 한다.
        Assert.Greater(Plate.rect.height, _label.preferredHeight,
                       "판이 글자보다 낮다 — 글자가 판 밖으로 삐져나온다");
        Assert.GreaterOrEqual(Plate.rect.height, 40f,
                              "판 높이가 " + Plate.rect.height + " 이다. 자막판으로 보이지 않는다");
        Assert.GreaterOrEqual(Plate.rect.width, 600f,
                              "판 너비가 " + Plate.rect.width + " 이다. 한 줄이 들어가지 않는다");
    }

    [Test]
    public void 뿌리도_자식_판만큼_커진다()
    {
        _label.text = "짧은 한 줄.";
        Rebuild();

        Assert.GreaterOrEqual(_root.rect.height, Plate.rect.height,
                              "그릇이 담긴 판보다 작다");
        Assert.GreaterOrEqual(_root.rect.width, Plate.rect.width,
                              "그릇이 담긴 판보다 좁다");
    }

    [Test]
    public void 두_줄짜리_대사는_판을_더_높인다()
    {
        _label.text = "짧은 한 줄.";
        Rebuild();
        var oneLine = Plate.rect.height;

        _label.text = "판이 정말 글자를 따라 자라는지 보려면 한 줄에 담기지 않을 만큼 " +
                      "길게 써야 한다. 이 문장은 판 너비를 넘겨 두 줄, 어쩌면 세 줄이 " +
                      "될 것이다. 그때 판 높이가 늘어나야 옳다.";
        Rebuild();
        var manyLines = Plate.rect.height;

        Assert.Greater(_label.textInfo.lineCount, 1, "긴 문장이 한 줄로 접혔다 — 줄바꿈이 꺼져 있다");
        Assert.Greater(manyLines, oneLine,
                       "줄이 늘었는데 판 높이가 " + oneLine + " 그대로다");
        Assert.Greater(manyLines, _label.preferredHeight, "여러 줄이 판 밖으로 삐져나온다");
    }

    [Test]
    public void 판_너비는_대사_길이에_흔들리지_않는다()
    {
        _label.text = "짧은 한 줄.";
        Rebuild();
        var narrow = Plate.rect.width;

        _label.text = "훨씬 더 길게 쓴 대사 한 줄. 그래도 판 너비는 그대로여야 한다.";
        Rebuild();

        // 줄마다 판이 늘었다 줄면 눈이 피로하다. 높이만 따라가고 너비는 붙박이다.
        Assert.AreEqual(narrow, Plate.rect.width, 0.5f, "대사 길이에 따라 판 너비가 변한다");
    }

    [Test]
    public void 자막은_퀵슬롯과_대기열_띠_위에_앉는다()
    {
        _label.text = "우주복 AI : 비상 착륙 완료.";
        Rebuild();

        Assert.AreEqual(new Vector2(0.5f, 0f), _root.anchorMin, "화면 하단 중앙에 걸려 있지 않다");
        Assert.AreEqual(new Vector2(0.5f, 0f), _root.anchorMax, "화면 하단 중앙에 걸려 있지 않다");

        // 대기열 띠는 y 100에서 56 높이(CraftQueueView)를 차지한다. 그 위여야 한다.
        var bottom = _root.anchoredPosition.y - _root.rect.height * _root.pivot.y;
        Assert.GreaterOrEqual(bottom, 156f,
                              "자막 아래끝이 y=" + bottom + " 이다. 대기열 띠(100~156)와 겹친다");
    }
}
