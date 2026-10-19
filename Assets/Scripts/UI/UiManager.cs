/****************************************************
    文件：UIManager.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-22 02:25:04
	功能：功能：UI 面板统一管理器，负责面板的加载、缓存、
          打开、关闭、ESC 分发和模态遮罩
*****************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum UILayer
{
    Bottom = 0,
    Normal = 1,
    Popup = 2,
    Modal = 3,
    Top = 4,
}

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public Transform PanelRoot => _panelRoot;
    public Canvas RootCanvas => _panelRoot != null
        ? _panelRoot.GetComponentInParent<Canvas>()?.rootCanvas
        : null;

    [Serializable]
    public class PanelConfig
    {
        [Tooltip("面板类名")]
        public string panelName;

        [Tooltip("面板预制体，需挂载 PanelBase 子类")]
        public PanelBase prefab;

        public UILayer layer = UILayer.Normal;

        [Tooltip("启动时预创建")]
        public bool preload = false;
    }

    [Header("面板配置")]
    [SerializeField] private List<PanelConfig> _configs = new List<PanelConfig>();
    public List<PanelConfig> Configs => _configs;

    [Header("场景引用")]
    [Tooltip("所有面板的父节点")]
    [SerializeField] private Transform _panelRoot;

    [Tooltip("模态遮罩， _panelRoot 的子物体")]
    [SerializeField] private Image _modalBlocker;

    [Tooltip("是否由 UIManager 在 Update 里监听 ESC")]
    [SerializeField] private bool _listenEscapeInUpdate = true;

    [SerializeField] private RectTransform[] _layers;

    private readonly Dictionary<string, PanelConfig> _configMap = new Dictionary<string, PanelConfig>();
    private readonly Dictionary<string, PanelBase> _cache = new Dictionary<string, PanelBase>();
    private float _modalAlpha;
    private readonly List<PanelBase> _openStack = new List<PanelBase>();
    private int _handledEscapeFrame = -1;
    public int LastModalCloseFrame { get; private set; } = -1;
    public void ConsumeModalCloseInput() => LastModalCloseFrame = Time.frameCount;
    private readonly Dictionary<PanelBase, List<PanelBase>> _coveredLeft = new Dictionary<PanelBase, List<PanelBase>>();
    private readonly Dictionary<PanelBase, GameObject> _embeddedRoots = new Dictionary<PanelBase, GameObject>();
    private GameObject VisualRoot(PanelBase panel) => _embeddedRoots.TryGetValue(panel, out var root) ? root : panel.gameObject;
    private static bool IsLeft(PanelBase p) => p is CharacterArchivePanel || p is CommissionPanel || p is BackpackPanel || p is ShopPanel || p is AbyssalAltarPanel;
    public bool HasLeftOverlay => EndingCredits.IsShowing || GuideHelpOverlay.BlocksInput || CGGalleryPanel.BlocksInput || PresentationEffects.IsBusy || PlayerBuildPanel.IsShowing || RouteSelectionPanel.IsShowing || EvolutionPanel.IsShowing || DiceCheckPanel.IsShowing || SaveSlotsPanel.IsShowing || _openStack.Exists(p => p != null && p.IsOpen &&
        (p is EndingPresentationPanel || p is CommissionPanel || p is BackpackPanel bag && bag.IsFunctionContext || p is SettingsPanel || p is DialogueHistoryPanel || p is ItemActionPanel));
    public string CurrentLeftPanelName => _openStack.FindLast(p => p != null && p.IsOpen && IsLeft(p) && VisualRoot(p).activeSelf)?.GetType().Name;

    /// <summary>档案是 TalkPanel 的嵌套预制体，注册现有实例，避免另生成一份丢失按钮绑定的档案。</summary>
    public void RegisterEmbedded(PanelBase panel, GameObject visualRoot)
    { _cache[panel.GetType().Name] = panel; _embeddedRoots[panel] = visualRoot; }

    private bool _openingAltarChild, _returnToAltar;
    public IReadOnlyList<PanelBase> OpenStack => _openStack;

    public void OpenAltarMonsterShop()
    {
        _openingAltarChild = true;
        try { ShopPanel.OpenMonsterShop(); } finally { _openingAltarChild = false; }
    }
    public void NotifyShopRootSwitch()
    {
        _returnToAltar = _openingAltarChild;
        Close<DescriptionPanel>(); Close<ItemActionPanel>(); Close<ShopItemActionPanel>();
    }
    public void ReturnFromShop()
    {
        bool parent = _returnToAltar; _returnToAltar = false;
        Close<ShopPanel>();
        if (parent) Open<AbyssalAltarPanel>();
    }
    /// <summary>开始接待时清理整备窗口；右侧未确认分解由原面板安全退回。</summary>
    public void ClosePreparationViews()
    {
        _returnToAltar = false; _coveredLeft.Clear();
        Close<DescriptionPanel>(); Close<ItemActionPanel>(); Close<ShopItemActionPanel>();
        Close<ShopPanel>(); Close<AbyssalAltarPanel>(); Close<BackpackPanel>(); Close<DecomposePanel>();
    }


    private void Awake()
    {
        //todo 背包初始化 后续应移到游戏入口
        InventoryModel.Initialize(20,99);
        //历史对话初始化
        DialogueHistoryModel.Initialize();
        CharacterArchiveModel.Initialize();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (var cfg in _configs)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.panelName) || cfg.prefab == null)
            {
                Debug.LogWarning("[UIManager] 存在无效的面板配置，已跳过。");
                continue;
            }
            _configMap[cfg.panelName] = cfg;
        }

        if (_modalBlocker != null)
        {
            _modalAlpha=_modalBlocker.color.a;
            _modalBlocker.gameObject.SetActive(false);

            //  用 EventTrigger 支持左右键点击遮罩关闭
            var trigger = _modalBlocker.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = _modalBlocker.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerClick
            };
            entry.callback.AddListener(data =>
            {
                var ped = data as PointerEventData;
                if (ped == null) return;

                if (ped.button == PointerEventData.InputButton.Left
                    || ped.button == PointerEventData.InputButton.Right)
                {
                    // 指路只能从明确返回入口退出，点击灰色背景不提交也不取消。
                    if (!RouteSelectionPanel.IsShowing && !EvolutionPanel.IsShowing && !DiceCheckPanel.IsShowing) CloseTop();
                }
            });
            trigger.triggers.Add(entry);
        }
        //预创建面板
        foreach (var cfg in _configs)
        {
            if (cfg == null || cfg.prefab == null) continue;
            if (!cfg.preload) continue;

            // 如果场景里已经手动放了一个，就不再 Instantiate
            if (_cache.ContainsKey(cfg.panelName)) continue;

            var panel = Instantiate(cfg.prefab, GetLayerRoot(cfg.layer));
            panel.name = cfg.panelName;
            panel.gameObject.SetActive(false); // 保持关闭
            _cache[cfg.panelName] = panel;
        }
        UIManager.Instance.Open<HUDPanel>();
        UIManager.Instance.Open<PreparationPanel>();

    }

    private void Update()
    {
        if (EndingCredits.IsShowing || GuideHelpOverlay.BlocksInput || PresentationEffects.IsBusy) return;
        if (_listenEscapeInUpdate && Input.GetKeyDown(KeyCode.Escape))
        {
            if(BackpackBoard.Instance?.IsEditing==true){BackpackBoard.Instance.CancelPlacement();return;}
            HandleEscape();
        }
    }

    #region 打开

    public T Open<T>(object data = null) where T : PanelBase
    {
        return Open(typeof(T).Name, data) as T;
    }

    public PanelBase Open(string panelName, object data = null)
    {
        if (panelName == nameof(SpecialCommissionPanel)) return null;
        var panel = GetOrCreate(panelName);
        if (panel == null) return null;

        bool preparationLeft = SystemManager.Instance?.Narrative?.IsPreparing == true && IsLeft(panel);
        if (preparationLeft)
        {
            // 一级入口直接替换，不在旧覆盖栈里留下可意外复现的页面。
            _returnToAltar = _openingAltarChild;
            _coveredLeft.Clear();
            Close<DescriptionPanel>(); Close<ItemActionPanel>(); Close<ShopItemActionPanel>();
            foreach (var old in new List<PanelBase>(_openStack))
                if (old != null && old != panel && IsLeft(old)) Close(old);
        }
        // 替换等待中的功能窗口时，先取消旧上下文，再打开新窗口，防止旧返回回调覆盖新状态。
        if (IsLeft(panel))
        {
            var previous = _openStack.FindLast(p => p != null && p.IsOpen && p != panel &&
                (p is CommissionPanel || p is BackpackPanel bag && bag.IsFunctionContext));
            if (previous != null) Close(previous);
        }
        if (panel is BackpackPanel currentBag && currentBag.IsOpen && currentBag.IsFunctionContext && !(data is Action)) Close(panel);
        // 已经打开就只刷新，不重复入栈
        if (panel.IsOpen && (!IsLeft(panel) || VisualRoot(panel).activeSelf))
        {
            panel.OnRefresh(data);
            return panel;
        }

        if (IsLeft(panel))
        {
            var covered = new List<PanelBase>();
            foreach (var previous in _openStack)
                if (previous != panel && previous != null && IsLeft(previous) && VisualRoot(previous).activeSelf)
                { VisualRoot(previous).SetActive(false); covered.Add(previous); }
            _coveredLeft[panel] = covered;
            VisualRoot(panel).SetActive(true);
        }
        // 全屏设置/历史挡住顶部入口，局部委托仍允许通过顶部切换档案或背包。
        if (panel is PlayerBuildPanel || panel is SettingsPanel || panel is DialogueHistoryPanel || panel is RouteSelectionPanel || panel is EvolutionPanel || panel is DiceCheckPanel || panel is DescriptionPanel && RouteSelectionPanel.IsShowing) panel.transform.SetParent(GetLayerRoot(UILayer.Top), false);
        // 非模态的浏览背包也要显示在同层剧情之前；嵌套档案保持原预制体层级。
        if (!_embeddedRoots.ContainsKey(panel)) panel.transform.SetAsLastSibling();
        panel.OnOpen(data);
        if(!panel.IsOpen)return panel;

        _openStack.Remove(panel);
        _openStack.Add(panel);

        UpdateModalBlocker();
        return panel;
    }

    #endregion

    #region 关闭

    public void Close(PanelBase panel)
    {
        if (panel == null || !panel.IsOpen) return;
        if (panel.IsModal) LastModalCloseFrame = Time.frameCount;

        _openStack.Remove(panel);
        if (IsLeft(panel)) VisualRoot(panel).SetActive(false);
        if (_coveredLeft.TryGetValue(panel, out var previousLeft))
        {
            _coveredLeft.Remove(panel);
            foreach (var previous in previousLeft) if (previous != null && previous.IsOpen) VisualRoot(previous).SetActive(true);
        }
        panel.OnClose();
        UpdateModalBlocker();
    }

    public void Close<T>() where T : PanelBase
    {
        var panel = GetCached(typeof(T).Name);
        if (panel != null) Close(panel);
    }

    /// <summary>关闭栈顶正在显示的面板</summary>
    public void CloseTop()
    {
        for (int i = _openStack.Count - 1; i >= 0; i--)
        {
            var panel = _openStack[i];
            if (panel != null && panel.IsOpen)
            {
                if (!panel.OnEscapePressed()) Close(panel);
                return;
            }
        }
    }

    public void CloseAll()
    {
        for (int i = _openStack.Count - 1; i >= 0; i--)
        {
            var panel = _openStack[i];
            if (panel != null && panel.IsOpen)
                panel.OnClose();
        }

        _openStack.Clear();
        UpdateModalBlocker();
    }

    #endregion

    #region 刷新

    public void Refresh<T>() where T : PanelBase
    {
        var panel = GetCached(typeof(T).Name);
        if (panel != null && panel.IsOpen)
            panel.OnRefresh();
    }

    public void RefreshAll()
    {
        foreach (var panel in _openStack)
        {
            if (panel != null && panel.IsOpen)
                panel.OnRefresh();
        }
    }

    #endregion

    #region ESC

    /// <summary>
    /// 从栈顶向下分发 ESC，遇到已消费或模态面板就停止
    /// </summary>
    public void HandleEscape()
    {
        if (EndingCredits.IsShowing || GuideHelpOverlay.HandleEscape() || CGGalleryPanel.HandleEscape() || PresentationEffects.IsBusy) return;
        // 同一帧只分派一次，四槽是独立实例也由这里统一消费ESC。
        if (_handledEscapeFrame == Time.frameCount) return;
        _handledEscapeFrame = Time.frameCount;
        LastModalCloseFrame = Time.frameCount;
        if (SaveSlotsPanel.HandleEscape()) return;
        for (int i = _openStack.Count - 1; i >= 0; i--)
        {
            var panel = _openStack[i];
            if (panel == null || !panel.IsOpen) continue;

            if (panel.OnEscapePressed()) return; // 面板已消费 ESC
            if (panel.IsModal) return;           // 模态面板挡住后面的 ESC
        }
    }

    #endregion

    #region 内部

    private PanelBase GetCached(string panelName)
    {
        return _cache.TryGetValue(panelName, out var panel) ? panel : null;
    }

    private PanelBase GetOrCreate(string panelName)
    {
        if (_cache.TryGetValue(panelName, out var cached) && cached != null)
            return cached;

        if (!_configMap.TryGetValue(panelName, out var cfg))
        {
            Debug.LogError($"[UIManager] 未找到面板配置：{panelName}");
            return null;
        }

        var panel = Instantiate(cfg.prefab, GetLayerRoot(cfg.layer), false);
        panel.name = panelName;
        _cache[panelName] = panel;
        return panel;
    }

    /// <summary>
    /// 根据栈顶第一个打开的面板决定是否显示遮罩，
    /// 并把遮罩压在模态面板下方
    /// </summary>
    private void UpdateModalBlocker()
    {
        if (_modalBlocker == null) return;

        PanelBase topPanel = null;
        for (int i = _openStack.Count - 1; i >= 0; i--)
        {
            var panel = _openStack[i];
            if (panel == null || !panel.IsOpen) continue;
            topPanel = panel;
            break;
        }

        bool show = topPanel != null && topPanel.IsModal;
        // 终局面板自带渐暗与输入遮挡，共用遮罩只拦输入，避免先瞬间压暗一次。
        bool ownMask = topPanel is EndingPresentationPanel || topPanel is EvolutionPanel || topPanel is PlayerBuildPanel || topPanel is DiceCheckPanel;
        var tint=_modalBlocker.color;tint.a=ownMask?0:_modalAlpha;_modalBlocker.color=tint;
        _modalBlocker.gameObject.SetActive(show);

        if (show)
        {
            // 遮罩跟随模态面板所在层
            var layerRoot = topPanel.transform.parent;
            if (_modalBlocker.transform.parent != layerRoot)
                _modalBlocker.transform.SetParent(layerRoot, false);

            _modalBlocker.transform.SetAsLastSibling(); // 遮罩在该层置顶
            topPanel.transform.SetAsLastSibling();      // 面板再置顶，压住遮罩
        }
    }
    /// <summary>
    /// 查询面板是否打开
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public bool IsOpen<T>() where T : PanelBase
    {
        var panel = GetCached(typeof(T).Name);
        return panel != null && panel.IsOpen;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private Transform GetLayerRoot(UILayer layer)
    {
        int idx = (int)layer;
        if (idx < 0 || idx >= _layers.Length) return _panelRoot;
        return _layers[idx];
    }

    #endregion
    public T Get<T>() where T : PanelBase
    {
        string key = typeof(T).Name;

        if (_cache.TryGetValue(key, out var cached))
        {
            if (cached != null) return cached as T;
            _cache.Remove(key);   // 底层对象已销毁，清掉失效缓存
        }

        return null;
    }
}
