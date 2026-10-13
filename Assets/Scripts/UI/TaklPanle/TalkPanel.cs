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
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>对话行</summary>
[Serializable]
public class DialogueLine
{
    public string iconName;     // 完整地址；留空 = 不替换图片
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
    [SerializeField] private TalkPanelRight _talkPanelRight;
    private CharacterArchivePanel _archivePanel;

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
    [SerializeField] private float _clickCooldown = 0.5f;

    private float _lastAdvanceTime = -999f;

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
            if (_archivePanel == null)
                Debug.LogWarning("[TalkPanel] leftDisplayArea 下找不到 CharacterArchivePanel", this);
        }

        TodayVisitorsReadyEvent.Register(OnTodayVisitorsReady);
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
        MoveNext();
    }

    /// <summary>推进对话到下一步（点击或外部强制推进）</summary>
    public void MoveNext()
    {
        if (!_isInitialized) return;

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
        yield return new WaitUntil(() => !Typewriter.IsTyping);
        _autoCompleteCoroutine = null;
        CompleteDialogue();
    }

    private void OnTodayVisitorsReady(List<int> ids)
    {
        int today = SystemManager.Instance.RunState.run.currentDay;
        _historyView.CheckDayChangedAndClear(today);
    }

    /// <summary>在历史内容末尾显示选项按钮</summary>
    public void ShowOptions(List<DialogueOption> options, Action<DialogueOption> onSelected)
    {
        if (_talkPanelRight == null)
        {
            Debug.LogError("[TalkPanel] TalkPanelRight 未赋值");
            return;
        }
        _talkPanelRight.ShowOptions(options, onSelected);
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
        if (string.IsNullOrEmpty(address)) return;   // ★ 留空 → 不动

        var sp = await SpriteLoader.LoadAsync(address);
        if (this == null || _iconImage == null) return;
        if (sp != null) _iconImage.sprite = sp;
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

    #endregion


    #region 按钮回调

    private void OnDescription()
    {
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

        bool willShow = !leftDisplayArea.activeSelf;
        leftDisplayArea.SetActive(willShow);

        if (willShow)
        {
            int id = SystemManager.Instance.VisitSystem.CurrentVisitorId;
            if (id < 0)
            {
                Debug.LogWarning("[TalkPanel] 当前没有访客，无法刷新档案");
                return;
            }

            if (_archivePanel != null)
                _archivePanel.Refresh(id, _iconImage != null ? _iconImage.sprite : null);
        }
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
    }

    /// <summary>补写最后一句 + 触发完成事件</summary>
    private void CompleteDialogue()
    {
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
    }

    #endregion
}