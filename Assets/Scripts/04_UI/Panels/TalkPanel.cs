/****************************************************
    文件：TalkPanle.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 12:20:23
	功能：谈话面板
*****************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>对话行</summary>
[Serializable]
public class DialogueLine
{
    public string iconName;     // 完整地址；留空 = 不替换图片
    public string sourceId;     // 稳定剧情地址，仅用于表现与记录，不执行业务动作。
    public string speakerName;
    public string content;
}

public class TalkPanel : PanelBase, IPointerClickHandler
{
    [Header("文件引用")]
    private DailyHistoryView _historyView;

    [Header("UI组件引用")]
    [SerializeField] private MonoBehaviour _typewriterComponent;
    [SerializeField] private Text _speakerNameText;
    [SerializeField] private Image _iconImage;
    [Serializable] public sealed class PortraitLayout
    {
        public string addressPrefix;
        public float height=920,maxWidth=840;
        public Vector2 baseline=new Vector2(-35,-530);
    }
    [Header("人物独立显示预设：高度、最大宽度和脚下基线")]
    [SerializeField] private PortraitLayout[] _portraitLayouts;
    private int _portraitRequest;
    public bool IsPortraitLoading { get; private set; }
    [SerializeField] private TalkPanelRight _talkPanelRight;
    private CharacterArchivePanel _archivePanel;
    private VisitSystem _archiveVisitors;

    [Header("对话控制器")]
    [SerializeField] private DialogueController _dialogueController = new DialogueController();
    public DialogueController Dialogue => _dialogueController;

    // 运行时获取接口实例
    private ITypewriterEffect Typewriter => _typewriterComponent as ITypewriterEffect;

    [Header("对话")]
    [SerializeField] private List<DialogueLine> _dialogueList = new List<DialogueLine>();
    private bool _instantTextMode;
    public void SetInstantText(bool instant) => _instantTextMode = instant;

    [Header("按钮引用")]
    [SerializeField] private Button _descriptionButton;      // 历史对话
    [SerializeField] private Button _characterArchiveButton; // 角色档案

    [Header("对话历史")]
    [SerializeField] private Transform _historyContent;      // ScrollView/Viewport/Content
    [SerializeField] private GameObject _historyItemPrefab;  // DialogueHistoryItem
    private int _lastModelIndex = -1;

    [Header("点击冷却")]
    [Tooltip("两次推进对话之间的最小间隔（秒），防止连点一次性跳过所有文本")]
    [SerializeField] private float _clickCooldown = 0.26f;

    private float _lastAdvanceTime = -999f;
    private float _lastPointerAdvanceTime = -999f;

    public GameObject leftDisplayArea;
    public GameObject RightDisplayArea;

    private bool _autoCompleteAtEnd = false;
    private Coroutine _autoCompleteCoroutine;

    /// <summary>对话全部播放完毕时触发</summary>
    public event Action OnDialogueComplete;
    /// <summary>每条对话开始播放时触发 用于音效、动画等</summary>
    public event Action<DialogueLine> OnLineStart;

    private int _currentIndex;
    private string _currentSpeaker;
    private string _currentIcon;        // 当前已加载的 iconName，避免重复加载
    private bool _isInitialized;
    private bool _segmentCompleted;


    #region 生命周期与初始化

    public override void OnInit()
    {
        base.OnInit();

        _historyView = new DailyHistoryView(_historyContent, _historyItemPrefab);

        _dialogueController ??= new DialogueController();
        _dialogueController.Init(this);

        if (_descriptionButton != null)
            _descriptionButton.onClick.AddListener(OnDescription);
        else
            Debug.LogError("[TalkPanel] DescriptionButton 未赋值!", this);

        if (_characterArchiveButton != null)
            _characterArchiveButton.onClick.AddListener(OnCharacterArchive);
        else
            Debug.LogError("[TalkPanel] CharacterArchiveButton 未赋值!", this);

        try
        {
            if (_dialogueList.Count > 0 && !_isInitialized)
                Init(_dialogueList);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }

        if (leftDisplayArea != null)
        {
            _archivePanel = leftDisplayArea.GetComponentInChildren<CharacterArchivePanel>(true);
            if (_archivePanel != null) { UIManager.Instance.RegisterEmbedded(_archivePanel, leftDisplayArea); leftDisplayArea.SetActive(false); }
            if (_archivePanel == null)
                Debug.LogWarning("[TalkPanel] leftDisplayArea 下找不到 CharacterArchivePanel", this);
        }

        TodayVisitorsReadyEvent.Register(OnTodayVisitorsReady);
        _archiveVisitors = SystemManager.Instance.VisitSystem;
        if (_archiveVisitors != null) _archiveVisitors.OnCurrentVisitorChanged += OnArchiveVisitorChanged;
    }

    public override void OnDestroy()
    {
        if (_descriptionButton != null)
            _descriptionButton.onClick.RemoveListener(OnDescription);
        if (_characterArchiveButton != null)
            _characterArchiveButton.onClick.RemoveListener(OnCharacterArchive);

        _dialogueController?.Dispose();
        _dialogueController = null;

        base.OnDestroy();

        if (_autoCompleteCoroutine != null)
        {
            StopCoroutine(_autoCompleteCoroutine);
            _autoCompleteCoroutine = null;
        }

        OnDialogueComplete = null;
        OnLineStart = null;

        TodayVisitorsReadyEvent.UnRegister(OnTodayVisitorsReady);
        if (_archiveVisitors != null) _archiveVisitors.OnCurrentVisitorChanged -= OnArchiveVisitorChanged;
    }

    private void OnArchiveVisitorChanged(int id)
    {
        // 档案允许与剧情并行；下一位来访时跟随人物刷新，保留所选页签。
        if (_archivePanel == null || !_archivePanel.IsOpen || id < 1) return;
        _archivePanel.Refresh(id);
        RefreshArchivePortrait(id);
    }

    /// <summary>初始化方法，传入对话列表</summary>
    public void Init(List<DialogueLine> dialogueList)
    {
        if (!ValidateReferences()) return;

        _dialogueList = dialogueList ?? new List<DialogueLine>();
        ResetState();

        if (_dialogueList.Count > 0)
            LoadIconAsync(_dialogueList[0].iconName);   // 第一句的 iconName（留空不动）

        _isInitialized = true;
    }

    /// <summary>校验关键 UI 引用是否已赋值</summary>
    private bool ValidateReferences()
    {
        bool isValid = true;

        if (_typewriterComponent == null)
        {
            Debug.LogError("[TalkPanel] Typewriter 组件引用未赋值!", this);
            isValid = false;
        }
        else if (Typewriter == null)
        {
            Debug.LogError($"[TalkPanel] 挂载的 {_typewriterComponent.GetType().Name} 未实现 ITypewriterEffect!", this);
            isValid = false;
        }

        if (_speakerNameText == null)
        {
            Debug.LogError("[TalkPanel] SpeakerNameText 引用未赋值!", this);
            isValid = false;
        }

        return isValid;
    }

    /// <summary>重置状态</summary>
    private void ResetState()
    {
        if (_autoCompleteCoroutine != null)
        {
            StopCoroutine(_autoCompleteCoroutine);
            _autoCompleteCoroutine = null;
        }

        _currentIndex = 0;
        _lastAdvanceTime = -999f;
        _segmentCompleted = false;
        _currentSpeaker = null;
        _currentIcon = null;
        _speakerNameText.text = string.Empty;

        _historyView.BeginSegment();
        Typewriter?.Clear();
        // 图片置空——保留当前 sprite
        _lastModelIndex = -1;
    }



    #endregion


    #region 点击交互

    public void OnPointerClick(PointerEventData eventData)
    {
        // 档案嵌在对话预制体里；左侧正文的点击不能沿父节点冒泡成剧情点击。
        if (_archivePanel != null && _archivePanel.IsOpen && leftDisplayArea.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_archivePanel.transform, eventData.position, eventData.pressEventCamera)) return;
        // 指针输入跨剧情帧保持间隔；内部开始显示下一帧仍即时执行。
        if(UIManager.Instance.HasLeftOverlay||Time.unscaledTime-_lastPointerAdvanceTime<_clickCooldown)return;
        _lastPointerAdvanceTime=Time.unscaledTime;
        MoveNext();
    }

    /// <summary>推进对话到下一步（点击或外部强制推进）</summary>
    public void MoveNext()
    {
        if (!_isInitialized || UIManager.Instance.HasLeftOverlay) return;

        // 冷却时间内忽略点击（打字机跳过除外）
        if (Typewriter != null && !Typewriter.IsTyping)
        {
            if (Time.unscaledTime - _lastAdvanceTime < _clickCooldown)
                return;
        }

        // 打字中：点击 = 立即跳过当前句
        if (Typewriter != null && Typewriter.IsTyping)
        {
            Typewriter.Skip();
            _lastAdvanceTime=Time.unscaledTime;
            return;
        }

        if (_currentIndex >= _dialogueList.Count)
        {
            CompleteDialogue();
            return;
        }

        PlayCurrentLine();
        _lastAdvanceTime = Time.unscaledTime;
    }

    private void PlayCurrentLine()
    {
        // ★ 补写上一句（和 _historyView 同一时机）
        int prev = _currentIndex - 1;
        if (prev >= 0 && prev < _dialogueList.Count)
        {
            _historyView.RecordLineIfNeeded(prev, _dialogueList[prev]);
            WriteModelIfNeeded(prev);
        }

        DialogueLine line = _dialogueList[_currentIndex];
        if(_dialogueController.IsNarrativeMode)SystemManager.Instance.Narrative.ApplyAudioLine(line.sourceId);

        // ★ 图标：仅在 iconName 非空且变化时才替换
        if (!string.IsNullOrEmpty(line.iconName) && _currentIcon != line.iconName)
        {
            _currentIcon = line.iconName;
            LoadIconAsync(line.iconName);
        }


        // 说话人
        if (_currentSpeaker != line.speakerName)
        {
            _currentSpeaker = line.speakerName;
            _speakerNameText.text = line.speakerName;
        }

        OnLineStart?.Invoke(line);

        // 打字机
        Typewriter.Play(line.content);

        if (_instantTextMode) Typewriter.Skip();
        _currentIndex++;

        // 最后一句自动完成
        if (_currentIndex >= _dialogueList.Count && _autoCompleteAtEnd)
        {
            if (_autoCompleteCoroutine != null) StopCoroutine(_autoCompleteCoroutine);
            _autoCompleteCoroutine = StartCoroutine(WaitTypewriterEndThenComplete());
        }
    }

    private IEnumerator WaitTypewriterEndThenComplete()
    {
        yield return null;
        yield return new WaitUntil(() => !Typewriter.IsTyping && !UIManager.Instance.HasLeftOverlay);
        _autoCompleteCoroutine = null;
        CompleteDialogue();
    }

    private void OnTodayVisitorsReady(List<int> ids)
    {
        int today = SystemManager.Instance.RunState.run.currentDay;
        _historyView.CheckDayChangedAndClear(today);
    }

    /// <summary>在历史内容末尾显示选项按钮</summary>
    public void ShowOptions(List<DialogueOption> options, Func<DialogueOption, bool> onSelected)
    {
        if (_talkPanelRight == null)
        {
            Debug.LogError("[TalkPanel] TalkPanelRight 未赋值");
            return;
        }
        _talkPanelRight.ShowOptions(options, onSelected);
    }

    public DialogueLine CurrentDisplayedLine => _currentIndex>0 && _currentIndex<=_dialogueList.Count ? _dialogueList[_currentIndex-1] : null;
    /// <summary>自动检定也在当前句后标注。仅刷新已有文本，不推进、不额外写历史。</summary>
    public void ShowCheckAnnotation(D20Service.Presentation check)
    {
        if(string.IsNullOrEmpty(check.storyText))return;
        _speakerNameText.text=check.storySpeaker??"";
        Typewriter.Play(check.storyText+"\n"+check.annotation); Typewriter.Skip();
    }

    public void ClearOptions()
    {
        _talkPanelRight?.ClearOptions();
    }

    public void CheckDayChangedAndClear()
    {
        int today = SystemManager.Instance.RunState.run.currentDay;
        _historyView.CheckDayChangedAndClear(today);
    }

    #endregion


    #region 图标加载

    /// <summary>
    /// 异步加载 iconName 对应的立绘；留空则不动图片
    /// </summary>
    private async void LoadIconAsync(string address)
    {
        int request=++_portraitRequest;
        if(address=="Narrative/blank")
        {
            IsPortraitLoading=false;
            if(_iconImage)_iconImage.enabled=false;
            return;
        }
        if(string.IsNullOrEmpty(address)){IsPortraitLoading=false;return;}
        IsPortraitLoading=true;
        var sprite=await SpriteLoader.LoadAsync(address);
        if(this==null||request!=_portraitRequest)return;
        IsPortraitLoading=false;
        if(!_iconImage)return;
        // 旧异步请求不能覆盖新人物；缺图时也不继续显示上一位来客。
        if(sprite==null){_iconImage.enabled=false;return;}
        if(_currentIcon!=address&&_currentIcon!=null)return;
        _iconImage.sprite=sprite;_iconImage.preserveAspect=true;_iconImage.raycastTarget=false;
        ApplyPortraitLayout(address,sprite);
        _iconImage.enabled=true;
    }
    private void ApplyPortraitLayout(string address,Sprite sprite)
    {
        if(_portraitLayouts==null)return;
        foreach(var layout in _portraitLayouts)
        {
            if(layout==null||string.IsNullOrEmpty(layout.addressPrefix)||!address.StartsWith(layout.addressPrefix,StringComparison.Ordinal))continue;
            float aspect=sprite.rect.width/sprite.rect.height;
            float height=Mathf.Min(layout.height,layout.maxWidth/aspect);
            var rect=_iconImage.rectTransform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,0);
            rect.localScale=Vector3.one;rect.sizeDelta=new Vector2(height*aspect,height);rect.anchoredPosition=layout.baseline;
            break;
        }
    }
    /// <summary>只在人物转场的黑幕下清除旧画面并预载下一位；不提前登记或推进台词。</summary>
    /// <summary>在跨天全黑时撤掉旧画面，只清可见当日日志；不删除历史模型或人物记录。</summary>
    public void PrepareBusinessDay()
    {
        if(_autoCompleteCoroutine!=null){StopCoroutine(_autoCompleteCoroutine);_autoCompleteCoroutine=null;}
        ClearOptions();Typewriter?.Clear();_currentSpeaker=null;_currentIcon=null;
        ++_portraitRequest;IsPortraitLoading=false;if(_iconImage)_iconImage.enabled=false;
        if(_speakerNameText)_speakerNameText.text="";
        _historyView?.CheckDayChangedAndClear(RunStateManager.Instance.run.currentDay);
    }
    public void PrepareVisitorPortrait(string address)
    {
        ClearOptions();Typewriter?.Clear();if(_speakerNameText)_speakerNameText.text="";
        if(!string.IsNullOrEmpty(address))UpdateBackgroundImage(address);
    }

    /// <summary>
    /// 更新背景图（外部调用）；留空则不动
    /// </summary>
    public void UpdateBackgroundImage(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return;      // ★ 留空 → 不动
        if (_currentIcon == iconName) return;            // 已加载

        _currentIcon = iconName;
        LoadIconAsync(iconName);
    }

    /// <summary>异步加载并设置角色头像</summary>
    private void RefreshArchivePortrait(int id)
    {
        // 档案拥有自己的头像绑定，不再异步加载中央整张立绘覆盖头像裁片。
        _archivePanel?.Refresh(id);
    }
    private string GetIllustrationName(int id)
    {
        var cfg = ConfigDatabase.Instance.Adventurers.TryGetValue(id, out var c) ? c : null;
        return cfg != null && !string.IsNullOrEmpty(cfg.IllustrationName)
            ? cfg.IllustrationName
            : id.ToString();
    }

    #endregion


    #region 按钮回调

    private void OnDescription()
    {
        //点击了历史对话按钮，打开历史对话面板
        UIManager.Instance.Open<DialogueHistoryPanel>();
    }

    private void OnCharacterArchive() => ToggleCharacterArchive();

    /// <summary>切换角色档案显示（供 HUD 等外部调用）</summary>
    public void ToggleCharacterArchive()
    {
        if (leftDisplayArea == null)
        {
            Debug.LogWarning("[TalkPanel] leftDisplayArea 未赋值", this);
            return;
        }

        bool willShow = _archivePanel != null && (!_archivePanel.IsOpen || !leftDisplayArea.activeSelf);

        if (willShow)
        {
            int id = SystemManager.Instance.VisitSystem.CurrentVisitorId;
            if (id < 0)
            {
                Debug.LogWarning("[TalkPanel] 当前没有访客，无法刷新档案");
                return;
            }
            OpenArchive(id, _archivePanel.CurrentTab);
        }
        else if (_archivePanel != null) UIManager.Instance.Close(_archivePanel);
    }
    public void OpenArchive(int id, int tab)
    {
        if (_archivePanel == null) return;
        UIManager.Instance.Open<CharacterArchivePanel>();
        _archivePanel.Refresh(id);
        _archivePanel.SelectTab(tab);
        RefreshArchivePortrait(id);
    }
   
    #endregion

    #region 历史对话写入
    /// <summary>把指定索引的对话写入全局历史模型（同一索引只写一次）</summary>
    private void WriteModelIfNeeded(int index)
    {
        if (index < 0 || index >= _dialogueList.Count) return;
        if (index == _lastModelIndex) return;
        _lastModelIndex = index;
        DialogueHistoryModel.Instance?.Add(_dialogueList[index]);
        if (_dialogueController.IsNarrativeMode) SystemManager.Instance.Narrative.RecordDisplayed(_dialogueList[index], "line:" + index);
    }

    /// <summary>补写最后一句 + 触发完成事件</summary>
    private void CompleteDialogue()
    {
        if (_segmentCompleted) return;
        _segmentCompleted = true;
        FlushLastLine();
        OnDialogueComplete?.Invoke();
    }

    /// <summary>补写最后一句（对话全部播放完毕时调用）</summary>
    private void FlushLastLine()
    {
        if (_dialogueList.Count == 0) return;
        int last = _dialogueList.Count - 1;

        // 当前面板用的每日历史（自带去重，重复调用安全）
        _historyView.RecordLineIfNeeded(last, _dialogueList[last]);

        // 全局历史模型
        WriteModelIfNeeded(last);
    }
    #endregion

    #region 公共工具方法

    public void ResetDialogue() => ResetState();

    public bool IsDialogueFinished => _currentIndex >= _dialogueList.Count;

    public float Progress => _dialogueList.Count > 0
        ? Mathf.Clamp01((float)_currentIndex / _dialogueList.Count)
        : 0f;

    public void SetAutoCompleteAtEnd(bool value) => _autoCompleteAtEnd = value;

    public void RecordPlayerChoice(string buttonText)
    {
        AudioManager.Instance?.PlayCue("click");
        //  当前面板的每日历史
        _historyView.AppendChoice(buttonText);

        //  补写上一句（避免选项插到未写的上一句之前）
        int prev = _currentIndex - 1;
        if (prev >= 0 && prev < _dialogueList.Count)
        {
            _historyView.RecordLineIfNeeded(prev, _dialogueList[prev]);
            WriteModelIfNeeded(prev);
        }

        //  再追加选项本身
        DialogueHistoryModel.Instance?.AddChoice(buttonText);
        if (_dialogueController.IsNarrativeMode) SystemManager.Instance.Narrative.RecordDisplayed(new DialogueLine { speakerName = "你", content = buttonText }, "choice:" + buttonText);
    }

    #endregion


}
