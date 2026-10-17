/****************************************************
    文件：GamePassEvent.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-22 13:19:04
	功能：描述面板
*****************************************************/
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DescriptionPanel 的组合入参。
/// 目的：不往 ItemData 里塞表现层字段（等级、回调），由调用方组装。
/// </summary>
public class DescriptionPanelArgs
{
    public ItemData Item;                       // 必填
    public string Level;                        // 可选：null/空 则不显示等级块
    public bool ShowUseButton;                  // 是否显示"使用"按钮
    public Action OnUse;                        // 点击"使用"时回调，可空
    public bool UseButtonInteractable = true;   // 使用按钮是否可点
}
public class DescriptionPanel : PanelBase
{
    [Header("显示组件")]
    [SerializeField] private Image _itemImage;
    [SerializeField] private Text _itemNameText;
    [SerializeField] private Text _itemDescriptionText;

    [Header("等级显示")]
    [SerializeField] private GameObject _levelRoot;      // 整块显隐容器
    [SerializeField] private Text _levelText;
    [SerializeField] private Color _sLevelColor = new Color(1f, 0.8f, 0.2f);

    [Header("按钮")]
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _useButton;

    // 记录当前正在展示的物品 id，用于异步加载回来后校验
    private int _currentItemId = -1;
    private Action _onUse;
    public override bool IsModal => true;
    private bool _useLock;
    private Color _levelDefaultColor = Color.white;

    public override void OnInit()
    {
        // 记录 Inspector 里配的默认色，避免被 S 级高亮污染
        if (_levelText != null) _levelDefaultColor = _levelText.color;

        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(OnConfirmClick);
        if (_useButton != null)
            _useButton.onClick.AddListener(OnUseClick);
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        // 兼容两种入参：新的 Args，或旧的 ItemData
        DescriptionPanelArgs args = null;
        if (data is DescriptionPanelArgs a) args = a;
        else if (data is ItemData d) args = new DescriptionPanelArgs { Item = d };

        if (args == null || args.Item == null)
        {
            ClearDisplay();
            return;
        }

        var item = args.Item;
        if (_itemNameText != null) _itemNameText.text = item.itemName;
        if (_itemDescriptionText != null) _itemDescriptionText.text = item.description;

        SetLevel(args.Level);
        SetUseButton(args.ShowUseButton, args.OnUse, args.UseButtonInteractable);

        // 图标异步加载
        _currentItemId = item.id;
        SetIconAsync(item.id, item.iconAddress);
    }

    public override void OnClose()
    {
        ClearDisplay();
        base.OnClose();
    }

    // ---------------- 等级 ----------------

    private void SetLevel(string level)
    {
        bool has = !string.IsNullOrEmpty(level);

        if (_levelRoot != null) _levelRoot.SetActive(has);

        if (_levelText != null)
        {
            _levelText.text = has ? level : string.Empty;
            // S 级给个高亮，其它等级用默认色
            _levelText.color = (has && level == "S") ? _sLevelColor : _levelDefaultColor;
        }
    }

    // ---------------- 使用按钮 ----------------

    private void SetUseButton(bool show, Action onUse, bool interactable)
    {
        _onUse = onUse;
        _useLock = false;

        if (_useButton == null) return;
        _useButton.gameObject.SetActive(show);
        _useButton.interactable = interactable;
    }

    private void OnUseClick()
    {
        if (_useLock || _onUse == null) return;

        _useLock = true;   // 防连点，回调内不用再自己加锁
        try
        {
            _onUse.Invoke();
        }
        finally
        {
            _useLock = false;
        }
    }

    // ---------------- 清理 ----------------

    private void ClearDisplay()
    {
        _currentItemId = -1;
        _onUse = null;
        _useLock = false;

        if (_itemNameText != null) _itemNameText.text = "";
        if (_itemDescriptionText != null) _itemDescriptionText.text = "";

        if (_itemImage != null)
        {
            _itemImage.sprite = null;
            _itemImage.enabled = false;
        }

        if (_levelRoot != null) _levelRoot.SetActive(false);
        if (_levelText != null) _levelText.text = "";

        if (_useButton != null) _useButton.gameObject.SetActive(false);
    }

    // ---------------- 图标异步 ----------------

    private async void SetIconAsync(int itemId, string address)
    {
        if (_itemImage == null) return;

        if (string.IsNullOrEmpty(address))
        {
            _itemImage.sprite = null;
            _itemImage.enabled = false;
            return;
        }

        var sp = await SpriteLoader.LoadAsync(address);

        if (this == null || _itemImage == null) return;
        if (_currentItemId != itemId) return;

        _itemImage.sprite = sp;
        _itemImage.enabled = sp != null;
    }

    // ---------------- 关闭 ----------------

    private void OnConfirmClick()
    {
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 点击关闭/确认
        UIManager.Instance.Close(this);
    }

    public override bool OnEscapePressed()
    {
        AudioManager.Instance.PlaySFX(AudioKeys.SFX_Back); // ★ 点击关闭/确认
        UIManager.Instance.Close(this);
        return true;
    }
}
