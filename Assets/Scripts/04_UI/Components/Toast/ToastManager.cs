/****************************************************
    文件：ToastDuplicateMode.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/14 15:34:30
	功能：重复消息的处理器
*****************************************************/

// ToastManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>重复消息的处理策略</summary>
public enum ToastDuplicateMode
{
    /// <summary>相同消息合并计数，显示为「内容 ×N」（推荐）</summary>
    Merge,
    /// <summary>相同消息直接丢弃，不排队也不计数</summary>
    Drop,
    /// <summary>不去重，全部排队依次播放</summary>
    Allow
}

/// <summary>
/// 消息提示队列管理器
///
/// 用法：
///   1. 场景里新建一个空物体，挂上本脚本
///   2. 调用 ToastManager.Show("保存成功");
///
/// 特性：
///   · 队列串行播放，前一条播完才轮到下一条
///   · 去重范围是「正在显示的 + 整个待显示队列」，而不是只比相邻一条
///   · 单条动画：快速淡入出现 → 向上飘 + 缓慢淡出 → 消失，共 2.25 秒
/// </summary>
[DisallowMultipleComponent]
public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    // ------------------------------------------------------------
    // Inspector 配置
    // ------------------------------------------------------------
    [Header("行为")]
    [Tooltip("单条提示的完整生命周期（秒）")]
    [SerializeField] float duration = 2.25f;

    [Tooltip("重复消息的处理方式")]
    [SerializeField] ToastDuplicateMode duplicateMode = ToastDuplicateMode.Merge;

    [Header("外观")]
    [Tooltip("字体。留空使用 Unity 内置字体；中文请拖入中文字体资源")]
    [SerializeField] Font font;
    [Tooltip("背景图。留空则使用程序生成的圆角白色 Sprite")]
    [SerializeField] Sprite backgroundSprite;

    [SerializeField] int fontSize = 28;
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.78f);
    [SerializeField] Color textColor = Color.white;
    [SerializeField] Vector2 padding = new Vector2(40f, 18f);

    [Header("布局")]
    [Tooltip("提示相对屏幕中心的位置偏移（设计分辨率像素）")]
    [SerializeField] Vector2 anchorOffset = new Vector2(0f, 120f);

    [Tooltip("Canvas 排序层级，越大越靠前")]
    [SerializeField] int sortingOrder = 32760;

    [Header("动画")]
    [Tooltip("淡入阶段占总时长的比例")]
    [SerializeField, Range(0.02f, 0.3f)] float fadeInRatio = 0.08f;
    [Tooltip("淡出阶段占总时长的比例。越小淡出越快")]
    [SerializeField, Range(0.05f, 0.8f)] float fadeOutRatio = 0.5f;

    [Tooltip("出现时从下方多少像素开始")]
    [SerializeField] float startOffsetY = -18f;

    [Tooltip("之后向上飘多少像素")]
    [SerializeField] float riseDistance = 42f;

    // ------------------------------------------------------------
    // 内部状态
    // ------------------------------------------------------------
    class Entry
    {
        public string Key;
        public string Text;
        public int Count;
    }

    readonly List<Entry> pending = new List<Entry>();
    readonly List<Entry> feedbackPending = new List<Entry>();
    ToastItem current;
    ToastItem feedbackCurrent;
    Coroutine loop;
    Coroutine feedbackLoop;
    RectTransform container;
    int sceneHandle;

    // ------------------------------------------------------------
    // 生命周期
    // ------------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        sceneHandle=gameObject.scene.handle;
        DontDestroyOnLoad(gameObject);   // 不需要跨场景可以删掉这一行
        BuildUI();
        SceneManager.sceneLoaded+=OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded-=OnSceneLoaded;
        if (Instance == this) Instance = null;
    }
    private void OnSceneLoaded(Scene scene,LoadSceneMode mode)
    {
        // 新局、读档重进游戏、返回标题都创建了新的主场景；同场景切面板不清队列。
        if(mode!=LoadSceneMode.Single)return;
        if(scene.handle!=sceneHandle)Clear();
        sceneHandle=scene.handle;
    }

    // ------------------------------------------------------------
    // 对外 API
    // ------------------------------------------------------------

    /// <summary>弹出一条提示</summary>
    public static void Show(string text)
    {
        Show(text, text);
    }

    /// <summary>弹出一条提示，key 用于去重判定</summary>
    public static void Show(string text, string key)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[ToastManager] 场景中没有 ToastManager 实例");
            return;
        }
        Instance.Enqueue(text, key);
    }
    /// <summary>当前操作的原因与结果可立即显示；业务变化仍用 Show，避免盖住功能弹窗。</summary>
    public static void ShowFeedback(string text,string key=null)
    {
        if(Instance==null){Debug.LogWarning("[ToastManager] 场景中没有 ToastManager 实例");return;}
        Instance.Enqueue(text,key,true);
    }

    /// <summary>清空队列并立即移除当前提示</summary>
    public void Clear()
    {
        pending.Clear();
        feedbackPending.Clear();

        if (current != null)
        {
            Destroy(current.gameObject);
            current = null;
        }

        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
        if(feedbackCurrent!=null){Destroy(feedbackCurrent.gameObject);feedbackCurrent=null;}
        if(feedbackLoop!=null){StopCoroutine(feedbackLoop);feedbackLoop=null;}
    }

    // ------------------------------------------------------------
    // 入队 + 去重
    // ------------------------------------------------------------
    void Enqueue(string text, string key,bool feedback=false)
    {
        text=NarrativeSystem.HidePrivateSuccessionProgress(text);
        if(string.IsNullOrWhiteSpace(text))return;
        text = text ?? string.Empty;
        if (string.IsNullOrEmpty(key)) key = text;

        var queue=feedback?feedbackPending:pending;
        var showing=feedback?feedbackCurrent:current;
        if (duplicateMode != ToastDuplicateMode.Allow)
        {
            // ① 与「正在显示的那条」比较
            if (showing != null && showing.Key == key)
            {
                if (duplicateMode == ToastDuplicateMode.Merge)
                    showing.Merge(text);
                return;
            }

            // ② 与「整个待显示队列」比较（不是只比相邻一条）
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].Key != key) continue;

                if (duplicateMode == ToastDuplicateMode.Merge)
                {
                    queue[i].Count++;
                    queue[i].Text = text;
                }
                return;
            }
        }

        queue.Add(new Entry { Key = key, Text = text, Count = 1 });

        if(feedback){if(feedbackLoop==null)feedbackLoop=StartCoroutine(RunFeedbackQueue());}
        else if (loop == null) loop = StartCoroutine(RunQueue());
    }

    // ------------------------------------------------------------
    // 队列调度：一条播完才轮到下一条
    // ------------------------------------------------------------
    IEnumerator RunQueue()
    {
        while (pending.Count > 0)
        {
            while(BusinessPaused())yield return null;
            var e = pending[0];
            pending.RemoveAt(0);

            current = ToastItem.Create(container, new ToastStyle
            {
                font = font,
                fontSize = fontSize,
                backgroundColor = backgroundColor,
                backgroundSprite = backgroundSprite,
                textColor = textColor,
                padding = padding
            });

            current.Init(e.Key, e.Text, e.Count,
              duration, fadeInRatio, fadeOutRatio,   // 新增
              startOffsetY, riseDistance);

            // 手动推进原动画迭代器，功能操作期间不计时、不消失，返回后从原进度续播。
            var animation=current.Play();
            while(current!=null)
            {
                bool paused=BusinessPaused();
                current.gameObject.SetActive(!paused);
                if(paused){yield return null;continue;}
                if(!animation.MoveNext())break;
                yield return animation.Current;
            }

            if (current != null) Destroy(current.gameObject);
            current = null;
        }

        loop = null;
    }
    private bool BusinessPaused()
    {
        if(GuideHelpOverlay.IsShowing||PresentationEffects.IsBusy||feedbackLoop!=null||feedbackCurrent!=null||feedbackPending.Count>0||SaveSlotsPanel.IsShowing)return true;
        var ui=UIManager.Instance;
        if(ui==null)return false;
        foreach(var panel in ui.OpenStack)
            if(panel!=null&&panel.IsOpen&&panel.gameObject.activeInHierarchy&&
                (panel.IsModal||panel is BackpackPanel bag&&bag.IsFunctionContext))return true;
        // 冒险者档案和浏览背包都是非模态，继续显示正常业务提示。
        return false;
    }
    private IEnumerator RunFeedbackQueue()
    {
        if(current!=null)current.gameObject.SetActive(false);
        while(feedbackPending.Count>0)
        {
            var e=feedbackPending[0];feedbackPending.RemoveAt(0);
            feedbackCurrent=ToastItem.Create(container,new ToastStyle{font=font,fontSize=fontSize,
                backgroundColor=backgroundColor,backgroundSprite=backgroundSprite,textColor=textColor,padding=padding});
            feedbackCurrent.Init(e.Key,e.Text,e.Count,duration,fadeInRatio,fadeOutRatio,startOffsetY,riseDistance);
            var animation=feedbackCurrent.Play();
            while(feedbackCurrent!=null) {
                bool paused=GuideHelpOverlay.IsShowing||PresentationEffects.IsBusy;
                feedbackCurrent.gameObject.SetActive(!paused);
                if(paused){yield return null;continue;}
                if(!animation.MoveNext())break;
                yield return animation.Current;
            }
            if(feedbackCurrent!=null)Destroy(feedbackCurrent.gameObject);
            feedbackCurrent=null;
        }
        feedbackLoop=null;
    }

    // ------------------------------------------------------------
    // 运行时搭建 UI（无需预制体）
    // ------------------------------------------------------------
    void BuildUI()
    {
        var canvasGo = new GameObject("ToastCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var group=canvasGo.GetComponent<CanvasGroup>();
        group.interactable=false;group.blocksRaycasts=false; // 提示只展示，不截获鼠标或键盘操作。

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var containerGo = new GameObject("Container", typeof(RectTransform));
        containerGo.transform.SetParent(canvasGo.transform, false);

        container = containerGo.GetComponent<RectTransform>();
        container.anchorMin = new Vector2(0.5f, 0.5f);
        container.anchorMax = new Vector2(0.5f, 0.5f);
        container.pivot = new Vector2(0.5f, 0.5f);
        container.anchoredPosition = anchorOffset;
        container.sizeDelta = Vector2.zero;
    }
}
