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
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
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

    [Header("UI组件引用")]
    [SerializeField] private MonoBehaviour _typewriterComponent;
    [SerializeField] private Text _speakerNameText;
    [SerializeField] private Image _iconImage;     //目前挂载的是背景图，后续可改为头像图 另外需要增加背景图的引用，或者直接在对话行里增加背景图字段 背景图进入对话时使用 UpdateBackgroundImage切换背景图，头像图使用 SetIcon 切换头像图

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

    public GameObject leftDisplayArea;
    public GameObject RightDisplayArea;

    private readonly Dictionary<string, AsyncOperationHandle<Sprite>> _iconHandles = new();

    private bool _autoCompleteAtEnd = false; // 由 DialogueController 设置：最后一句打完是否自动触发完成
    //_自动完成协程
    private Coroutine _autoCompleteCoroutine;


    /// <summary>
    /// 对话全部播放完毕时触发
    /// </summary>
    public Action OnDialogueComplete;
    /// <summary>
    /// 每条对话开始播放时触发 用于音效、动画等
    /// </summary>
    public Action<DialogueLine> OnLineStart;

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

        ClearIconCache();
        OnDialogueComplete = null;
        OnLineStart = null;
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

        _currentIndex = 0;
        _currentSpeaker = null;
        _currentIcon = null;
        _speakerNameText.text = string.Empty;
        // 重置头像显示
        if (_iconImage != null)
        {
            _iconImage.sprite = null;
            _iconImage.enabled = false;
        }
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

        if (Typewriter.IsTyping)
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
    }
    /// <summary>
    /// 写入历史对话
    /// </summary>
    private void PlayCurrentLine()
    {
        DialogueLine line = _dialogueList[_currentIndex];
        //写入历史对话
        DialogueHistoryModel.Instance?.Add(line);
        //写入人物档案
        if (!string.IsNullOrEmpty(line.speakerName))
        {
            CharacterArchiveModel.Instance?.Add(line.speakerName, line);
        }
        // 仅在说话人变化时更新文本（减少GC与重绘）
        if (_currentSpeaker != line.speakerName)
        {
            _currentSpeaker = line.speakerName;
            _speakerNameText.text = line.speakerName;
        }
        // 仅在图标变化时更新背景
        if (_currentIcon != line.iconName)
        {
            _currentIcon = line.iconName;
            SetIcon(line.iconName);
        }
        // 触发单条对话开始事件
        OnLineStart?.Invoke(line);
        // 播放打字机效果
        Typewriter.Play(line.content);
        if (_instantTextMode) Typewriter.Skip();
        _currentIndex++;

        // 最后一句，且外部告知“后面还有选项”，才在打字结束后自动触发完成
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
            Debug.LogWarning("[TalkPanel] leftDisplayArea 未赋值，无法切换显示", this);
            return;
        }

        // 如果仍需校验说话人，可保留下方注释的判断
        if (string.IsNullOrEmpty(_currentSpeaker))
        {
            Debug.LogWarning("[TalkPanel] 当前没有具体说话人，无法打开人物档案", this);
            return;
        }

        leftDisplayArea.SetActive(!leftDisplayArea.activeSelf);
    }

    #endregion

    #region 图标资源管理

    /// <summary>
    /// 设置头像图标（按需加载 + 自动缓存）
    /// </summary>
    private async void SetIcon(string iconName)
    {
        if (_iconImage == null) return;

        if (string.IsNullOrEmpty(iconName))
        {
            _iconImage.sprite = null;
            _iconImage.enabled = false;
            return;
        }

        // 1. 先查缓存（已加载过的直接命中，零开销）
        if (_iconHandles.TryGetValue(iconName, out var cachedHandle)
            && cachedHandle.Status == AsyncOperationStatus.Succeeded)
        {
            _iconImage.sprite = cachedHandle.Result;
            _iconImage.enabled = true;
            return;
        }

        // 2. 未缓存则异步加载（仅首次触发 IO）
        try
        {
            var handle = Addressables.LoadAssetAsync<Sprite>(iconName);
            _iconHandles[iconName] = handle;

            await handle.Task;

            // await 返回后检查对象是否仍存活
            if (this == null || _iconImage == null)
            {
                // 对象已销毁，释放刚加载的资源避免泄漏
                if (handle.IsValid())
                    Addressables.Release(handle);
                return;
            }

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _iconImage.sprite = handle.Result;
                _iconImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[TalkPanel] 图标加载失败: {iconName}", this);
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TalkPanel] 图标加载异常: {iconName}\n{e}", this);
            _iconImage.sprite = null;
            _iconImage.enabled = false;
        }
    }

    /// <summary>
    /// 释放所有 Addressables 图标资源
    /// </summary>
    public void ClearIconCache()
    {
        foreach (var handle in _iconHandles.Values)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }
        _iconHandles.Clear();

        if (_iconImage != null)
        {
            _iconImage.sprite = null;
            _iconImage.enabled = false;
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
            SetIcon(iconName);
        }
    }
    #endregion

}