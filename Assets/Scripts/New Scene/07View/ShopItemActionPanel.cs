/****************************************************
    文件：ShopItemActionPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/4 18:00:15
	功能：商店右键操作面板
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

public class ShopItemActionPanel : PanelBase
{
    [SerializeField] private Button _examineButton;
    [SerializeField] private Button _buyButton;
    [SerializeField] private Vector2 _buttonOffset;
    [SerializeField] private Vector2 _fixedPosition = new Vector2(624f, 745f);

    public override bool IsModal => true;

    private ShopActionData _currentData;

    public override void OnInit()
    {
        _examineButton.onClick.AddListener(OnViewClick);
        _buyButton.onClick.AddListener(OnBuyClick);
    }

    public override void OnDestroy()
    {
        _examineButton.onClick.RemoveListener(OnViewClick);
        _buyButton.onClick.RemoveListener(OnBuyClick);
        base.OnDestroy();
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        if (data is ShopActionData d) _currentData = d;

        _buyButton.interactable = _currentData != null
            && SystemManager.Instance.Economy.Cash >= _currentData.price;

        ApplyFixedPosition();
    }

    public override void OnRefresh(object data = null)
    {
        base.OnRefresh(data);
        if (data is ShopActionData d) _currentData = d;
        ApplyFixedPosition();
    }

    public override void OnClose()
    {
        _currentData = null;
        base.OnClose();
    }

    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }

    // ---------- 按钮 ----------

    private void OnViewClick()
    {
        if (_currentData?.data == null) return;

        UIManager.Instance.Open<DescriptionPanel>(_currentData.data);
        UIManager.Instance.Close(this);
    }

    private void OnBuyClick()
    {
        _currentData?.onBuy?.Invoke();
        UIManager.Instance.Close(this);
    }

    // ---------- 定位 ----------

    private void ApplyFixedPosition()
    {
        // 使用画布设计坐标，随 CanvasScaler 缩放，不能把它当作屏幕像素。
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
        rect.anchoredPosition = _fixedPosition;
    }
}
