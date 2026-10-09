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
/// <summary>
/// 对话行
/// </summary>
[Serializable]
public class DialogueLine
{
    public string iconName;
    public string speakerName;
    public string content;
}

public class TalkPanel : PanelBase, IPointerClickHandler
{
    [Header("文件引用")]
    private TalkIconView _iconView;
    private DailyHistoryView _historyView;

    [Header("UI组件引用")]
    [SerializeField] private MonoBehaviour _typewriterComponent;
    [SerializeField] private Text _speakerNameText;
    [SerializeField] private Image _iconImage;
    [SerializeField] private TalkPanelRight _talkPanelRight;
    private CharacterArchivePanel _archivePanel;

    private Coroutine _idleSwitchCoroutine;

    [Header("图标后缀约定")]
    [SerializeField] private string _talkSuffix = "_talk";
    [SerializeField] private string _idleSuffix = "_idle";

    [Header("对话控制器")]
    [SerializeField] private DialogueController _dialogueController = new DialogueController();
    public DialogueController Dialogue => _dialogueController;

    // 运行时获取接口实例
    private ITypewriterEffect Typewriter => _typewriterComponent as ITypewriterEffect;

    [Header("对话")]
    [SerializeField] private List<DialogueLine> _dialogueList = new List<DialogueLine>();//对话列表
    private bool _instantTextMode;// 由外部设置：是否立即显示文本（跳过打字机效果）
    /// <summary>设置是否跳过打字机直接显示文本</summary>
    public void SetInstantText(bool instant) => _instantTextMode = instant;


    [Header("按钮引用")]
    [SerializeField] private Button _descriptionButton;//历史对话
    [SerializeField] private Button _characterArchiveButton;//角色档案

    [Header("对话历史")]
    [SerializeField] private Transform _historyContent;      // ScrollView/Viewport/Content
    [SerializeField] private GameObject _historyItemPrefab;  // DialogueHistoryItem

    [Header("点击冷却")]
    [Tooltip("两次推进对话之间的最小间隔（秒），防止连点一次性跳过所有文本")]
    [SerializeField] private float _clickCooldown = 0.5f;

    private float _lastAdvanceTime = -999f;

    public GameObject leftDisplayArea;
    public GameObject RightDisplayArea;


    private bool _autoCompleteAtEnd = false; // 由 DialogueController 设置：最后一句打完是否自动触发完成
    //_自动完成协程
    private Coroutine _autoCompleteCoroutine;


    /// <summary>
    /// 对话全部播放完毕时触发
    /// </summary>
    public event Action OnDialogueComplete;
    /// <summary>
    /// 每条对话开始播放时触发 用于音效、动画等
    /// </summary>
    public event Action<DialogueLine> OnLineStart;

    private int _currentIndex;
    private string _currentSpeaker;
    private string _currentIcon;
    private bool _isInitialized;



    #region 生命周期与初始化

    /// <summary>
    /// 面板初始化（由 PanelBase.Awake 调用，只执行一次）
    /// </summary>
    public override void OnInit()
    {
        base.OnInit();

        _iconView = new TalkIconView(this, _iconImage, _talkSuffix, _idleSuffix);
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

        // 停止协程
        if (_autoCompleteCoroutine != null)
        {
            StopCoroutine(_autoCompleteCoroutine);
            _autoCompleteCoroutine = null;
        }

        if (_idleSwitchCoroutine != null)
        {
            StopCoroutine(_idleSwitchCoroutine);
            _idleSwitchCoroutine = null;
        }
        _iconView?.ReleaseAll();
        OnDialogueComplete = null;
        OnLineStart = null;

        TodayVisitorsReadyEvent.UnRegister(OnTodayVisitorsReady);
    }

    /// <summary>
    /// 初始化方法，传入对话列表
    /// </summary>
    public void Init(List<DialogueLine> dialogueList)
    {
        if (!ValidateReferences()) return;

        _dialogueList = dialogueList ?? new List<DialogueLine>();
        ResetState();
        if (_dialogueList.Count > 0)
        {   //初始化背景图片资源
            UpdateBackgroundImage(_dialogueList[0].iconName);
        }

        _isInitialized = true;
    }
    /// <summary>
    /// 校验关键UI引用是否已赋值
    /// </summary>
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

    /// <summary>
    /// 重置状态
    /// </summary>
    private void ResetState()
    {
        //清理携程
        if (_autoCompleteCoroutine != null)
        {
            StopCoroutine(_autoCompleteCoroutine);
            _autoCompleteCoroutine = null;
        }

        if (_idleSwitchCoroutine != null)
        {
            StopCoroutine(_idleSwitchCoroutine);
            _idleSwitchCoroutine = null;
        }

        _currentIndex = 0;
        _currentSpeaker = null;
        _currentIcon = null;
        _speakerNameText.text = string.Empty;

        _iconView.Clear();
        _historyView.BeginSegment();
        Typewriter?.Clear();
    }
    #endregion

    #region 点击交互

    /// <summary>
    /// 点击事件处理方法
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerClick(PointerEventData eventData)
    {
        MoveNext();

    }
    /// <summary>
    /// 推进对话到下一步（点击或外部强制推进）
    /// </summary>
    public void MoveNext()
    {
        if (!_isInitialized) return;

        // 冷却时间内忽略点击（打字机跳过除外，跳过可以立即响应）
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
            OnDialogueComplete?.Invoke();
            return;
        }

        PlayCurrentLine();
        _lastAdvanceTime = Time.unscaledTime;
    }
    /// <summary>
    /// 写入历史对话
    /// </summary>
    private void PlayCurrentLine()
    {
        // 兜底：上一句若还没记（跳过时可能来不及记），补记
        int prev = _currentIndex - 1;
        if (prev >= 0 && prev < _dialogueList.Count)
            _historyView.RecordLineIfNeeded(prev, _dialogueList[prev]);

        DialogueLine line = _dialogueList[_currentIndex];
        int playingIndex = _currentIndex;   // 捕获给协程

        // 图标切换（只保留一段）
        if (_currentIcon != line.iconName)
        {
            _currentIcon = line.iconName;
            _iconView.SetCharacter(line.iconName, OnIconLoaded);
        }
        else
        {
            _iconView.ShowTalk();
        }

        // 历史对话模型（跨天保留，原逻辑不变）
        DialogueHistoryModel.Instance?.Add(line);

        // 说话人
        if (_currentSpeaker != line.speakerName)
        {
            _currentSpeaker = line.speakerName;
            _speakerNameText.text = line.speakerName;
        }

        OnLineStart?.Invoke(line);

        // 打字机
        Typewriter.Play(line.content);

        // idle 切换 + 行号记录
        if (_idleSwitchCoroutine != null) StopCoroutine(_idleSwitchCoroutine);
        _idleSwitchCoroutine = StartCoroutine(SwitchToIdleWhenTypingDone(playingIndex));

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
        yield return null; // 等打字机真正开始
        yield return new WaitUntil(() => !Typewriter.IsTyping); // 等打字结束
        _autoCompleteCoroutine = null;
        OnDialogueComplete?.Invoke();
    }
    private IEnumerator SwitchToIdleWhenTypingDone(int lineIndex)
    {
        yield return null;
        yield return new WaitUntil(() => !Typewriter.IsTyping);

        if (lineIndex >= 0 && lineIndex < _dialogueList.Count)
            _historyView.RecordLineIfNeeded(lineIndex, _dialogueList[lineIndex]);

        _iconView.ShowIdle();
        _idleSwitchCoroutine = null;
    }

    private void OnIconLoaded()
    {
        if (!_isInitialized) return;
        bool typing = Typewriter != null && Typewriter.IsTyping;
        if (typing) _iconView.ShowTalk();
        else _iconView.ShowIdle();
    }

    private void OnTodayVisitorsReady(List<int> ids)
    {
        int today = SystemManager.Instance.RunState.run.currentDay;
        _historyView.CheckDayChangedAndClear(today);
    }

    /// <summary>
    /// 在历史内容末尾显示选项按钮
    /// </summary>
    public void ShowOptions(List<DialogueOption> options, Action<DialogueOption> onSelected)
    {
        if (_talkPanelRight == null)
        {
            Debug.LogError("[TalkPanel] TalkPanelRight 未赋值");
            return;
        }
        _talkPanelRight.ShowOptions(options, onSelected);
    }

    /// <summary>
    /// 清空选项按钮
    /// </summary>
    public void ClearOptions()
    {
        _talkPanelRight?.ClearOptions();
    }
    /// <summary>
    /// 跨天时清空右侧窗口
    /// </summary>
    public void CheckDayChangedAndClear()
    {
        int today = SystemManager.Instance.RunState.run.currentDay;
        _historyView.CheckDayChangedAndClear(today);
    }

    #endregion

    #region 按钮回调
    /// <summary>
    /// 历史对话按钮点击事件
    /// </summary>
    private void OnDescription()
    {
        UIManager.Instance.Open<DialogueHistoryPanel>();
    }

    /// <summary>
    /// 人物档案按钮点击事件
    /// </summary>
    private void OnCharacterArchive()
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
                _archivePanel.Refresh(id, _iconView.IdleSprite);
        }
    }

    #endregion

    #region 公共工具方法
    /// <summary>
    /// 重置对话状态（供外部调用
    /// </summary>
    public void ResetDialogue()
    {
        ResetState();
    }
    /// <summary>
    /// 当前对话是否已全部播放完毕
    /// </summary>
    public bool IsDialogueFinished => _currentIndex >= _dialogueList.Count;
    /// <summary>
    /// 当前对话进度 (0~1)
    /// </summary>
    public float Progress => _dialogueList.Count > 0
        ? Mathf.Clamp01((float)_currentIndex / _dialogueList.Count)
        : 0f;
    /// <summary>
    /// 由外部控制器调用：设置“最后一句播完是否自动触发 OnDialogueComplete”
    /// true  → 有选项，打完自动弹选项
    /// false → 无后续，打完停在最后一句
    /// </summary>
    public void SetAutoCompleteAtEnd(bool value)
    {
        _autoCompleteAtEnd = value;
    }

    public void UpdateBackgroundImage(string iconName)
    {
        if (_currentIcon != iconName)
        {
            _currentIcon = iconName;
            _iconView.SetCharacter(iconName, OnIconLoaded);
        }
    }

    public void RecordPlayerChoice(string buttonText)
    {
        _historyView.AppendChoice(buttonText);
    }
    #endregion

}