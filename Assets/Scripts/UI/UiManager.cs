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
    private readonly List<PanelBase> _openStack = new List<PanelBase>();

    public IReadOnlyList<PanelBase> OpenStack => _openStack;

    private void Awake()
    {
        //todo 背包初始化 后续应移到游戏入口
        InventoryModel.Initialize(20,2);
        //历史对话初始化
        DialogueHistoryModel.Initialize();

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
            _modalBlocker.gameObject.SetActive(false);

            var btn = _modalBlocker.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(CloseTop);
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
    }

    private void Update()
    {
        if (_listenEscapeInUpdate && Input.GetKeyDown(KeyCode.Escape))
            HandleEscape();
    }

    #region 打开

    public T Open<T>(object data = null) where T : PanelBase
    {
        return Open(typeof(T).Name, data) as T;
    }

    public PanelBase Open(string panelName, object data = null)
    {
        var panel = GetOrCreate(panelName);
        if (panel == null) return null;

        // 已经打开就只刷新，不重复入栈
        if (panel.IsOpen)
        {
            panel.OnRefresh();
            return panel;
        }

        panel.OnOpen(data);

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

        panel.OnClose();
        _openStack.Remove(panel);
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
                Close(panel);
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
}