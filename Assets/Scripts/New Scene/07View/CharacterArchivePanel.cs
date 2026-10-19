/****************************************************
    文件：CharacterArchivePanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 12:22:31
	功能： 查看冒险者档案的面板
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterArchivePanel : PanelBase
{

    [Header("按钮")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _basicInfo; //基本信息
    [SerializeField] private Button _dialogueLog;//对话记录
    [SerializeField] private Button _validInfo;//有效信息
    [SerializeField] private Button _investigationLog;//调查记录

    [Header("雷达图")]
    public RadarChart radarChart;

    [Header("人物信息")]
    [SerializeField] private Text _Introduction; //简要介绍
    [SerializeField] private Text _name;
    [SerializeField] private Text _succession;
    [SerializeField] private Text _level; //冒险者名称
    [SerializeField] private Image _iconImage;//冒险者头像
    [System.Serializable]
    public struct ArchivePortrait
    {
        public int actorId;
        public Sprite portrait;
    }
    [Header("独立档案头像：只引用裁片，不影响中央立绘")]
    [SerializeField] private ArchivePortrait[] _portraits;
    [SerializeField] private Text _portraitPlaceholder;

    [Header("七维数值文本 (按顺序拖入: 理智,勇气,虔诚,宽容,仁爱,坚定,勤勉)")]
    public List<Text> statTexts = new List<Text>(7);

    public override bool IsModal => false;
    public int CurrentActor { get; private set; } = -1;
    public int CurrentTab { get; private set; }
    private int _guideOriginalTab = -1, _guideStep = -1;
    private float _guideOriginalScroll;
    /// <summary>引导临时切页只展示既有内容，结束后恢复原页及滚动位置。</summary>
    public void BeginGuide()
    {
        if(_guideOriginalTab>=0)return;
        _guideOriginalTab=CurrentTab;_guideStep=-1;
        _guideOriginalScroll=_recordScroll?_recordScroll.verticalNormalizedPosition:1;
    }
    public RectTransform GuideTarget(int step)
    {
        int tab=step<2?0:Mathf.Clamp(step-1,1,3);
        if(_guideStep!=step) { _guideStep=step;SelectTab(tab);Canvas.ForceUpdateCanvases(); }
        if(step==0)return _statsRoot?_statsRoot.transform as RectTransform:radarChart.transform as RectTransform;
        if(step==1&&_succession)return _succession.rectTransform;
        return _recordScroll?_recordScroll.viewport:transform as RectTransform;
    }
    public void EndGuide()
    {
        if(_guideOriginalTab<0)return;
        int tab=_guideOriginalTab;float scroll=_guideOriginalScroll;
        _guideOriginalTab=-1;_guideStep=-1;SelectTab(tab);
        if(_recordScroll)_recordScroll.verticalNormalizedPosition=scroll;
    }
    [SerializeField] private ScrollRect _recordScroll;
    [SerializeField] private GameObject _statsRoot;
    [SerializeField] private Text _headerIntroduction;
    [SerializeField] private Vector2 _basicRecordPosition, _basicRecordSize, _otherRecordPosition, _otherRecordSize;

    public override void OnInit()
    {
        if (_closeButton) _closeButton.onClick.AddListener(OnCloseClick);
        if (_basicInfo) _basicInfo.onClick.AddListener(() => SelectTab(0));
        if (_dialogueLog) _dialogueLog.onClick.AddListener(() => SelectTab(1));
        if (_validInfo) _validInfo.onClick.AddListener(() => SelectTab(2));
        if (_investigationLog) _investigationLog.onClick.AddListener(() => SelectTab(3));
        EnsureScroll();
        TopBarRefreshEvent.Register(RefreshLive);
        if (_iconImage) _iconImage.preserveAspect = true;
    }

    public override void OnDestroy()
    {
        TopBarRefreshEvent.UnRegister(RefreshLive);
        if (_closeButton) _closeButton.onClick.RemoveListener(OnCloseClick);
    }

    private void RefreshSuccession()
    {
        if(!_succession)return;
        if(CurrentActor<1||SystemManager.Instance?.Narrative?.Enabled!=true) {_succession.text="";return;}
        var a=SystemManager.Instance.Narrative.AssessSuccession(CurrentActor);
        // 倾向读取正式评估，界面不展示任何条件数值。
        _succession.text=a.dead?"已死亡 · 不可接任":"接任倾向："+a.tendency;
        // 两次透镜前只显示倾向；完整揭示后仅给出培养方向，不泄露条件阈值与差距。
        if(!a.dead && SystemManager.Instance.Narrative.HasFullBackground(CurrentActor))
        {
            _succession.text+="\n"+SystemManager.Instance.Narrative.RevealedSuccessionFocus(CurrentActor);
        }
    }
    private void RefreshLive()
    {
        if (!IsOpen || CurrentActor < 0) return;
        var run = RunStateManager.Instance.run;
        if (run != null && run.adventurers.TryGetValue(CurrentActor, out var actor)) UpdateStatsVisuals(actor);
    }

    public void RefreshRecords()
    {
        if (!IsOpen || _recordScroll == null) return;
        float position = _recordScroll.verticalNormalizedPosition;
        SelectTab(CurrentTab);
        _recordScroll.verticalNormalizedPosition = position;
    }
    private void OnCloseClick()
    {
        UIManager.Instance.Close(this);
    }
    public override bool OnEscapePressed() { OnCloseClick(); return true; }

    /// <summary>身份区保持，只有基本信息显示七维；记录页使用扩大后的正文区域。</summary>
    public void SelectTab(int tab)
    {
        CurrentTab = Mathf.Clamp(tab, 0, 3);
        if (_statsRoot) _statsRoot.SetActive(CurrentTab == 0);
        if (_level) _level.gameObject.SetActive(true);
        if (_succession) _succession.gameObject.SetActive(CurrentTab == 0);
        if (_recordScroll)
        {
            var viewport = _recordScroll.viewport;
            viewport.anchoredPosition = CurrentTab == 0 ? _basicRecordPosition : _otherRecordPosition;
            viewport.sizeDelta = CurrentTab == 0 ? _basicRecordSize : _otherRecordSize;
        }
        var buttons = new[] { _basicInfo, _dialogueLog, _validInfo, _investigationLog };
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null)
            {
                var colors = buttons[i].colors;
                colors.normalColor = colors.selectedColor = i == CurrentTab ? Color.white : new Color(0.65f, 0.65f, 0.65f);
                buttons[i].colors = colors;
            }
        if (!_Introduction || CurrentActor < 0) return;
        var run = RunStateManager.Instance?.run;
        if (run == null || !run.adventurers.TryGetValue(CurrentActor, out var runtime)) return;
        string text;
        switch (CurrentTab)
        {
            case 1: text = runtime.historyDialogue.Count == 0 ? "尚无对话记录。" : string.Join("\n\n", runtime.historyDialogue); break;
            case 2: text = runtime.validInfo.Count == 0 ? "尚未获得有效信息。" : string.Join("\n\n", runtime.validInfo); break;
            case 3: text = runtime.investigationArchive.Count == 0 ? "尚未调查。？？？" : string.Join("\n\n", runtime.investigationArchive); break;
            default: text = ""; break;
        }
        _Introduction.text = text;
        _Introduction.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(_recordScroll.viewport.rect.height, _Introduction.preferredHeight + 12));
        _recordScroll.verticalNormalizedPosition = 1;
    }

    private void EnsureScroll()
    {
        if (!_Introduction || _recordScroll != null) return;
        // 复用原简介 Text 作为可滚动正文，只给原位置加视口，不另建档案界面。
        var original = _Introduction.rectTransform;
        var viewport = new GameObject("ArchiveTextViewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        var rect = (RectTransform)viewport.transform;
        rect.SetParent(original.parent, false);
        rect.anchorMin = original.anchorMin; rect.anchorMax = original.anchorMax;
        rect.pivot = original.pivot; rect.anchoredPosition = original.anchoredPosition; rect.sizeDelta = original.sizeDelta;
        original.SetParent(rect, false);
        original.anchorMin = new Vector2(0, 1); original.anchorMax = Vector2.one;
        original.pivot = new Vector2(0.5f, 1); original.anchoredPosition = Vector2.zero;
        original.sizeDelta = new Vector2(0, rect.rect.height);
        _Introduction.alignment = TextAnchor.UpperLeft;
        _Introduction.horizontalOverflow = HorizontalWrapMode.Wrap;
        _Introduction.verticalOverflow = VerticalWrapMode.Overflow;
        _Introduction.raycastTarget = true;
        _recordScroll = viewport.GetComponent<ScrollRect>();
        _recordScroll.viewport = rect; _recordScroll.content = original;
        _recordScroll.horizontal = false; _recordScroll.scrollSensitivity = 24;
        _recordScroll.movementType = ScrollRect.MovementType.Clamped;
    }

    /// <summary>由外部（TalkPanel）调用：刷新指定冒险者的档案</summary> 图片命名规则  id_portrait
    public void Refresh(int adventurerId, Sprite iconSprite = null)
    {
        // 人物刷新沿用当前页签；头像来源独立于中央立绘。
        CurrentActor = adventurerId;
        // 清空状态
        if (adventurerId < 0)
        {
            ClearBaseInfo();
            UpdateStatsVisuals(null);
            return;
        }

        // 获取静态配置
        if (!ConfigDatabase.Instance.Adventurers.TryGetValue(adventurerId, out var adv))
        {
            Debug.LogWarning($"[CharacterArchivePanel] 找不到冒险者静态配置 id={adventurerId}");
            ClearBaseInfo();
            UpdateStatsVisuals(null);
            return;
        }

        // 更新基础信息
        if (_name) _name.text = adventurerId==3?"Clot·Lach·Artos":adv.name;
        if (_level) _level.text = "等级：" + adv.baseLevel;
        if (_headerIntroduction) _headerIntroduction.text = adv.briefStory;
        SelectTab(CurrentTab);
        if (_iconImage)
        {
            // 每个人的脸部裁片由编辑器分别配置；没有素材时明确待补，不再裁切整张立绘。
            var portrait = _portraits != null && _portraits.Length > 0
                ? System.Array.Find(_portraits, item => item.actorId == adventurerId).portrait : iconSprite;
            _iconImage.sprite = portrait;
            _iconImage.enabled = portrait != null;
            if (_portraitPlaceholder) _portraitPlaceholder.gameObject.SetActive(portrait == null);
        }

        // 获取运行时数据并刷新
        var runState = RunStateManager.Instance?.run; 
        if (runState != null && runState.adventurers.TryGetValue(adventurerId, out var runtimeData))
        {
            UpdateStatsVisuals(runtimeData);
        }
        else
        {
            Debug.LogWarning($"[CharacterArchivePanel] 找不到冒险者运行时数据 id={adventurerId}");
            UpdateStatsVisuals(null);
        }
    }

    /// <summary>统一刷新雷达图和七维文本</summary>
    private void UpdateStatsVisuals(AdventurerRuntimeData runtimeData)
    {
        // 数据为空：雷达图归零，文本清空
        if (runtimeData == null || runtimeData.currentSevenStats == null)
        {
            if (_succession) _succession.text = "";
            if (radarChart != null) radarChart.Refresh(new Dictionary<SevenDimension, int>());
            foreach (var t in statTexts) if (t) t.text = "0";
            return;
        }
        RefreshSuccession();

        // 刷新雷达图
        if (radarChart != null) radarChart.Refresh(runtimeData);

        // 遍历更新7个文本
        var stats = runtimeData.currentSevenStats;
        for (int i = 0; i < statTexts.Count && i < 7; i++)
        {
            if (statTexts[i] == null) continue;
            SevenDimension dim = (SevenDimension)i; // 枚举顺序与列表顺序一致
            statTexts[i].text = SevenDimensionDisplay.GetName(dim) + " " + (stats.TryGetValue(dim, out int val) ? val.ToString() : "0");
        }
    }

    /// <summary>清空基础信息</summary>
    private void ClearBaseInfo()
    {
        if (_name) _name.text = "";
        if (_level) _level.text = "";
        if (_succession) _succession.text = "";
        if (_Introduction) _Introduction.text = "";
        if (_iconImage) _iconImage.sprite = null;
        if (_iconImage) _iconImage.enabled = false;
        if (_portraitPlaceholder) _portraitPlaceholder.gameObject.SetActive(false);
    }
}
