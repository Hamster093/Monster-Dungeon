/****************************************************
    文件：ItemActionPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-22 13:19:04
	功能：背包内右键点击物品后弹出的操作面板
*****************************************************/
using UnityEngine;
using UnityEngine.UI;

public class ItemActionPanel : PanelBase
{
    [SerializeField] private Button _examineButton;
    [SerializeField] private Button _dropButton;
    [SerializeField] private Vector2 _buttonOffset;
    [SerializeField] private Vector2 _fixedPosition = new Vector2(624f, 745f);

    // 模态，点遮罩可关闭
    public override bool IsModal => true;

    private ItemActionData _currentData;
    [SerializeField] private Button _useButton;
    [SerializeField] private Sprite _browseBackground, _useBackground;

    public override void OnInit()
    {
        _examineButton.onClick.AddListener(OnViewClick);
        _dropButton.onClick.AddListener(OnDropClick);
        _useButton.onClick.AddListener(() => {
            var callback = _currentData?.onUse;
            UIManager.Instance.Close(this);
            callback?.Invoke();
        });
    }
    /// <summary>
    /// 打开
    /// </summary>
    /// <param name="data"></param>
    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        if (data is ItemActionData d)
            _currentData = d;

        ApplyFixedPosition();
        ApplyActionsLayout();
    }
    /// <summary>
    /// 刷新
    /// </summary>
    /// <param name="data"></param>
    public override void OnRefresh(object data = null)
    {
        if (data is ItemActionData d)
            _currentData = d;

        ApplyFixedPosition();
        ApplyActionsLayout();
    }

    public override void OnClose()
    {
        _currentData = null; // 清掉引用，避免持有旧回调
        base.OnClose();
    }
    private void ApplyActionsLayout()
    {
        bool canUse = _currentData?.onUse != null;
        _useButton.gameObject.SetActive(canUse);
        GetComponent<Image>().sprite = canUse ? _useBackground : _browseBackground;
        ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, canUse ? 280 : 225);
        // 两套原素材的分隔线位置不同，命中区跟随素材，不再叠加重复文字。
        SetRow(_examineButton, canUse ? 0 : 48, canUse ? 80 : 90);
        SetRow(_dropButton, canUse ? -88 : -48, canUse ? 80 : 90);
    }
    private static void SetRow(Button button, float y, float h)
    {
        var r = (RectTransform)button.transform;
        // 行位置以菜单中心计算，不能沿用旧按钮的左上角锚点，否则文字可见但点击区在菜单外。
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = new Vector2(0, y); r.sizeDelta = new Vector2(192, h);
    }
    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    #region 按钮

    private void OnViewClick()
    {
        if (_currentData == null) return;

        UIManager.Instance.Open<DescriptionPanel>(_currentData.data);

        UIManager.Instance.Close(this); // 查看后关掉操作面板
    }

    private void OnDropClick()
    {
        _currentData?.onDrop?.Invoke();
        UIManager.Instance.Close(this);
    }

    #endregion

    #region 定位

    private void ApplyFixedPosition()
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
        // 原参数为设计图屏幕位置，转为画布坐标，随缩放一起定位。
        rect.anchoredPosition = _fixedPosition - new Vector2(960,540);
    }

    #endregion
}
